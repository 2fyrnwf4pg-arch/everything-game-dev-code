using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FiveDSudoku.Core.Sudoku;

// The namespace is plural while the folder is not: a type may not share its name
// with an enclosing namespace without making every unqualified use of `Timeline`
// resolve to the namespace instead. The folder layout is fixed by CLAUDE.md, so
// the namespace is the part that gives way.
namespace FiveDSudoku.Core.Timelines;

/// <summary>
/// One line of history: an ordered run of immutable board states, plus the
/// metadata that locates it in the multiverse.
///
/// A timeline is itself immutable. Playing a move does not append to this object;
/// it produces a new <see cref="Timeline"/> that shares the previous states. Every
/// state that was ever played therefore stays reachable and unchanged.
///
/// Only the frontier — the newest state — is playable. Historical states can be
/// read through <see cref="StateAt"/> but there is no way to write to them: the
/// only mutator is internal, so a timeline can only ever be advanced by the game
/// rules, never by a caller reaching in from outside.
/// </summary>
public sealed class Timeline
{
    private readonly SudokuBoard[] _states;
    private readonly ReadOnlyCollection<SudokuBoard> _statesView;

    /// <summary>Takes ownership of <paramref name="states"/>; callers must not keep a reference.</summary>
    private Timeline(
        int id,
        int? parentId,
        int? branchTime,
        int firstStateTime,
        SudokuBoard[] states,
        TimelineStatus status,
        bool occupiesActiveSlot)
    {
        Id = id;
        ParentId = parentId;
        BranchTime = branchTime;
        FirstStateTime = firstStateTime;
        Status = status;

        // A timeline that is finished or not in play holds no slot. Enforcing it
        // here rather than at each call site means the accounting cannot drift:
        // a dead timeline can never keep a slot occupied that another timeline is
        // waiting for, and an inactive one can never quietly hold one it does not
        // have.
        OccupiesActiveSlot = occupiesActiveSlot
            && status != TimelineStatus.Dead
            && status != TimelineStatus.Inactive;

        _states = states;
        _statesView = new ReadOnlyCollection<SudokuBoard>(states);
    }

    /// <summary>Identifier of this timeline, unique within one game.</summary>
    public int Id { get; }

    /// <summary>Timeline this one branched from, or <c>null</c> for the root timeline.</summary>
    public int? ParentId { get; }

    /// <summary>
    /// Time in the parent timeline that this one branched from, or <c>null</c> for
    /// the root timeline, which branched from nothing.
    /// </summary>
    public int? BranchTime { get; }

    /// <summary>Time index of this timeline's earliest state.</summary>
    public int FirstStateTime { get; }

    /// <summary>Lifecycle state of this timeline.</summary>
    public TimelineStatus Status { get; }

    /// <summary>Whether this timeline currently holds one of the level's active slots.</summary>
    public bool OccupiesActiveSlot { get; }

    /// <summary>Time index of the frontier, the newest state.</summary>
    public int FrontierTime => FirstStateTime + _states.Length - 1;

    /// <summary>The newest state, and the only one that can be played on.</summary>
    public SudokuBoard Frontier => _states[_states.Length - 1];

    /// <summary>Number of states this timeline holds.</summary>
    public int StateCount => _states.Length;

    /// <summary>All states of this timeline, oldest first.</summary>
    public IReadOnlyList<SudokuBoard> States => _statesView;

    /// <summary>
    /// True when this timeline can still be advanced: it is active, holds a slot,
    /// and its frontier still admits at least one legal placement.
    /// </summary>
    public bool IsCapableOfFurtherPlay =>
        Status == TimelineStatus.Active && OccupiesActiveSlot && Frontier.HasAnyLegalPlacement();

    /// <summary>Creates the root timeline of a game, starting at time 0.</summary>
    internal static Timeline CreateRoot(
        int id,
        SudokuBoard startingBoard,
        TimelineStatus status,
        bool occupiesActiveSlot)
    {
        if (startingBoard is null)
        {
            throw new ArgumentNullException(nameof(startingBoard));
        }

        return new Timeline(
            id,
            parentId: null,
            branchTime: null,
            firstStateTime: 0,
            states: new[] { startingBoard },
            status,
            occupiesActiveSlot);
    }

    /// <summary>
    /// Creates a timeline that branches off <paramref name="parent"/> at
    /// <paramref name="sourceTime"/>.
    ///
    /// The new timeline holds two states: the parent's state at the source time,
    /// carried over unchanged, and then that same board with one alternative
    /// placement on it. Its own time axis therefore starts at the source time and
    /// its frontier sits one step later, exactly as if that alternative had been
    /// played back then.
    ///
    /// The parent is only read here. It is an immutable object and its states are
    /// immutable too, so the carried-over state is a copy in every sense that
    /// matters: nothing either timeline can do will ever change what the other sees.
    /// </summary>
    internal static Timeline CreateBranch(
        int id,
        Timeline parent,
        int sourceTime,
        SudokuBoard branchedBoard,
        TimelineStatus status,
        bool occupiesActiveSlot)
    {
        if (parent is null)
        {
            throw new ArgumentNullException(nameof(parent));
        }

        if (branchedBoard is null)
        {
            throw new ArgumentNullException(nameof(branchedBoard));
        }

        SudokuBoard carriedOver = parent.StateAt(sourceTime);

        return new Timeline(
            id,
            parentId: parent.Id,
            branchTime: sourceTime,
            firstStateTime: sourceTime,
            states: new[] { carriedOver, branchedBoard },
            status,
            occupiesActiveSlot);
    }

    /// <summary>
    /// Rebuilds a timeline from a save. Only persistence uses this: it is the one
    /// path that legitimately produces a timeline in the middle of its life rather
    /// than at its start, so it takes every field as read.
    /// </summary>
    internal static Timeline Restore(
        int id,
        int? parentId,
        int? branchTime,
        int firstStateTime,
        SudokuBoard[] states,
        TimelineStatus status,
        bool occupiesActiveSlot)
    {
        if (states is null)
        {
            throw new ArgumentNullException(nameof(states));
        }

        if (states.Length == 0)
        {
            throw new ArgumentException("A timeline needs at least one state.", nameof(states));
        }

        return new Timeline(id, parentId, branchTime, firstStateTime, states, status, occupiesActiveSlot);
    }

    /// <summary>True when this timeline holds a state at <paramref name="time"/>.</summary>
    public bool ContainsTime(int time) => time >= FirstStateTime && time <= FrontierTime;

    /// <summary>
    /// The state at <paramref name="time"/>. Historical states are readable but not
    /// writable — the returned board is immutable.
    /// </summary>
    public SudokuBoard StateAt(int time)
    {
        if (!ContainsTime(time))
        {
            throw new ArgumentOutOfRangeException(
                nameof(time),
                time,
                $"Timeline {Id} holds times [{FirstStateTime}, {FrontierTime}].");
        }

        return _states[time - FirstStateTime];
    }

    /// <summary>
    /// Returns a new timeline with <paramref name="board"/> appended as the new
    /// frontier. This timeline is not modified, so the caller's reference keeps
    /// showing the history as it was.
    /// </summary>
    internal Timeline WithNextState(SudokuBoard board, TimelineStatus status)
    {
        if (board is null)
        {
            throw new ArgumentNullException(nameof(board));
        }

        SudokuBoard[] extended = new SudokuBoard[_states.Length + 1];
        Array.Copy(_states, extended, _states.Length);
        extended[_states.Length] = board;

        return new Timeline(Id, ParentId, BranchTime, FirstStateTime, extended, status, OccupiesActiveSlot);
    }

    /// <summary>
    /// Returns a new timeline with a different status and slot, and the same
    /// history. Used when an inactive timeline is brought into play.
    /// </summary>
    internal Timeline WithStatusAndSlot(TimelineStatus status, bool occupiesActiveSlot)
    {
        if (status == Status && occupiesActiveSlot == OccupiesActiveSlot)
        {
            return this;
        }

        return new Timeline(Id, ParentId, BranchTime, FirstStateTime, _states, status, occupiesActiveSlot);
    }

    public override string ToString() =>
        $"L{Id} {Status} frontier=T{FrontierTime}{(OccupiesActiveSlot ? string.Empty : " (no slot)")}";
}
