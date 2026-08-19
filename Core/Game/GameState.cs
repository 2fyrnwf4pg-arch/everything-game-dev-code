using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FiveDSudoku.Core.Level;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;

namespace FiveDSudoku.Core.Game;

/// <summary>
/// The whole run at one moment: every timeline, which one is selected, where the
/// present sits, what is left of the temporal budget, and how the run stands.
///
/// A game state is immutable. Every action returns a new state and leaves the old
/// one intact and fully inspectable, which is what makes history auditable and
/// replays reproducible. There is deliberately no undo: nothing here rewinds a
/// state, discards history, or edits a state that has already been played.
/// </summary>
public sealed class GameState
{
    private readonly Timeline[] _timelines;
    private readonly ReadOnlyCollection<Timeline> _timelinesView;

    /// <summary>Takes ownership of <paramref name="timelines"/>; callers must not keep a reference.</summary>
    private GameState(
        LevelDefinition level,
        Timeline[] timelines,
        int selectedTimelineId,
        int remainingTemporalBudget)
    {
        Level = level;
        _timelines = timelines;
        _timelinesView = new ReadOnlyCollection<Timeline>(timelines);
        SelectedTimelineId = selectedTimelineId;
        RemainingTemporalBudget = remainingTemporalBudget;
        Present = ComputePresent(timelines);
        Outcome = ComputeOutcome(timelines);
    }

    /// <summary>The level being played.</summary>
    public LevelDefinition Level { get; }

    /// <summary>Every timeline of this run, including ones that can no longer be played.</summary>
    public IReadOnlyList<Timeline> Timelines => _timelinesView;

    /// <summary>Id of the timeline the player is currently looking at.</summary>
    public int SelectedTimelineId { get; }

    /// <summary>Temporal Moves still available.</summary>
    public int RemainingTemporalBudget { get; }

    /// <summary>
    /// The global present: the earliest frontier among the timelines that are
    /// <see cref="TimelineStatus.Active"/> and hold an active slot.
    ///
    /// <c>null</c> means no such timeline exists, so there is no present at all.
    /// The run's outcome is never read off this value — <see cref="Outcome"/> is
    /// computed independently — and modelling "no present" as an absent value
    /// rather than as a magic number keeps it that way by construction.
    /// </summary>
    public int? Present { get; }

    /// <summary>How the run stands.</summary>
    public GameOutcome Outcome { get; }

    /// <summary>The timeline the player is currently looking at.</summary>
    public Timeline SelectedTimeline => GetTimeline(SelectedTimelineId);

    /// <summary>True when a timeline has reached a complete, valid solution.</summary>
    public bool IsWon => Outcome == GameOutcome.Won;

    /// <summary>True when the run cannot be continued and was not won.</summary>
    public bool IsGameOver => Outcome == GameOutcome.GameOver;

    /// <summary>Starts a run: one root timeline holding the level's starting board.</summary>
    public static GameState Start(LevelDefinition level)
    {
        if (level is null)
        {
            throw new ArgumentNullException(nameof(level));
        }

        SudokuBoard startingBoard = level.StartingBoard;
        TimelineStatus status = startingBoard.IsSolved() ? TimelineStatus.Solved : TimelineStatus.Active;

        // The root timeline occupies one of the level's active slots.
        Timeline root = Timeline.CreateRoot(RootTimelineId, startingBoard, status, occupiesActiveSlot: true);

        return new GameState(level, new[] { root }, RootTimelineId, level.TemporalBudget);
    }

    /// <summary>Id the root timeline always has.</summary>
    public const int RootTimelineId = 0;

    /// <summary>The timeline with the given id.</summary>
    public Timeline GetTimeline(int timelineId)
    {
        Timeline? timeline = FindTimeline(timelineId);

        if (timeline is null)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timelineId), timelineId, "No timeline with that id exists in this game.");
        }

        return timeline;
    }

    /// <summary>True when a timeline with the given id exists.</summary>
    public bool HasTimeline(int timelineId) => FindTimeline(timelineId) is not null;

    /// <summary>
    /// Selects which timeline the player is looking at.
    ///
    /// This is a view operation, not a move: it spends no temporal budget, advances
    /// no time, produces no state, touches no board, and does not by itself move
    /// the present.
    /// </summary>
    public GameState SelectTimeline(int timelineId)
    {
        if (!HasTimeline(timelineId))
        {
            throw new ArgumentOutOfRangeException(
                nameof(timelineId), timelineId, "No timeline with that id exists in this game.");
        }

        if (timelineId == SelectedTimelineId)
        {
            return this;
        }

        return new GameState(Level, _timelines, timelineId, RemainingTemporalBudget);
    }

    /// <summary>
    /// Checks a placement on the selected timeline without applying it, and reports
    /// which condition fails.
    /// </summary>
    public MoveRejection ValidatePlacement(int row, int column, int value)
    {
        if (Outcome != GameOutcome.InProgress)
        {
            return MoveRejection.GameAlreadyFinished;
        }

        Timeline timeline = SelectedTimeline;

        if (timeline.Status != TimelineStatus.Active)
        {
            return MoveRejection.TimelineNotActive;
        }

        if (Present is null || timeline.FrontierTime != Present.Value)
        {
            return MoveRejection.TimelineNotAtPresent;
        }

        BoardSize size = Level.Size;

        if (!size.IsIndexInRange(row) || !size.IsIndexInRange(column))
        {
            return MoveRejection.CoordinatesOutOfRange;
        }

        SudokuBoard board = timeline.Frontier;

        if (!board.IsEmpty(row, column))
        {
            return MoveRejection.CellNotEmpty;
        }

        if (!size.IsValueInRange(value))
        {
            return MoveRejection.ValueOutOfRange;
        }

        // Coordinates, emptiness and range are already settled, so the only thing
        // left that can make the placement illegal is a row/column/box conflict.
        if (!board.IsPlacementLegal(row, column, value))
        {
            return MoveRejection.ViolatesSudokuConstraint;
        }

        return MoveRejection.None;
    }

    /// <summary>
    /// Plays a value on the selected timeline's frontier, producing the next state
    /// of that timeline. Costs no temporal budget.
    /// </summary>
    public MoveResult PlaceValue(int row, int column, int value)
    {
        MoveRejection rejection = ValidatePlacement(row, column, value);

        if (rejection != MoveRejection.None)
        {
            return MoveResult.Refused(rejection, this);
        }

        Timeline timeline = SelectedTimeline;
        SudokuBoard nextBoard = timeline.Frontier.WithValue(row, column, value);
        TimelineStatus nextStatus = nextBoard.IsSolved() ? TimelineStatus.Solved : TimelineStatus.Active;

        Timeline[] updated = ReplaceTimeline(timeline.WithNextState(nextBoard, nextStatus));

        return MoveResult.Applied(new GameState(Level, updated, SelectedTimelineId, RemainingTemporalBudget));
    }

    public override string ToString()
    {
        string present = Present is null ? "none" : $"T{Present.Value}";

        return $"{Level.Id}: present={present}, selected=L{SelectedTimelineId}, " +
            $"budget={RemainingTemporalBudget}/{Level.TemporalBudget}, {Outcome}";
    }

    /// <summary>
    /// The present is the earliest frontier among timelines that are active and
    /// hold a slot. Timelines that are finished or hold no slot do not count.
    /// </summary>
    private static int? ComputePresent(Timeline[] timelines)
    {
        int? present = null;

        for (int index = 0; index < timelines.Length; index++)
        {
            Timeline timeline = timelines[index];

            if (timeline.Status != TimelineStatus.Active || !timeline.OccupiesActiveSlot)
            {
                continue;
            }

            if (present is null || timeline.FrontierTime < present.Value)
            {
                present = timeline.FrontierTime;
            }
        }

        return present;
    }

    /// <summary>
    /// Won as soon as any timeline holds a complete, valid solution. Otherwise over
    /// once nothing is left that could still be played. Never inferred from the
    /// present.
    /// </summary>
    private static GameOutcome ComputeOutcome(Timeline[] timelines)
    {
        bool anyPlayable = false;

        for (int index = 0; index < timelines.Length; index++)
        {
            Timeline timeline = timelines[index];

            if (timeline.Status == TimelineStatus.Solved)
            {
                return GameOutcome.Won;
            }

            anyPlayable |= timeline.IsCapableOfFurtherPlay;
        }

        return anyPlayable ? GameOutcome.InProgress : GameOutcome.GameOver;
    }

    private Timeline? FindTimeline(int timelineId)
    {
        for (int index = 0; index < _timelines.Length; index++)
        {
            if (_timelines[index].Id == timelineId)
            {
                return _timelines[index];
            }
        }

        return null;
    }

    private Timeline[] ReplaceTimeline(Timeline replacement)
    {
        Timeline[] updated = new Timeline[_timelines.Length];

        for (int index = 0; index < _timelines.Length; index++)
        {
            updated[index] = _timelines[index].Id == replacement.Id ? replacement : _timelines[index];
        }

        return updated;
    }
}
