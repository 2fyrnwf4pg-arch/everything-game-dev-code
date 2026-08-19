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
        OccupiesActiveSlot = occupiesActiveSlot;
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

    /// <summary>Returns a new timeline with a different status and the same history.</summary>
    internal Timeline WithStatus(TimelineStatus status)
    {
        if (status == Status)
        {
            return this;
        }

        return new Timeline(Id, ParentId, BranchTime, FirstStateTime, _states, status, OccupiesActiveSlot);
    }

    /// <summary>Returns a new timeline holding or releasing an active slot.</summary>
    internal Timeline WithActiveSlot(bool occupiesActiveSlot)
    {
        if (occupiesActiveSlot == OccupiesActiveSlot)
        {
            return this;
        }

        return new Timeline(Id, ParentId, BranchTime, FirstStateTime, _states, Status, occupiesActiveSlot);
    }

    public override string ToString() =>
        $"L{Id} {Status} frontier=T{FrontierTime}{(OccupiesActiveSlot ? string.Empty : " (no slot)")}";
}
