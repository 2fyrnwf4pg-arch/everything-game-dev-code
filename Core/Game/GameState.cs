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

    /// <summary>
    /// Active slots the level grants that no timeline currently holds. A timeline
    /// releases its slot the moment it dies, which is how a slot ever comes free
    /// for an inactive timeline to take.
    /// </summary>
    public int FreeActiveSlots
    {
        get
        {
            int taken = 0;

            for (int index = 0; index < _timelines.Length; index++)
            {
                if (_timelines[index].OccupiesActiveSlot)
                {
                    taken++;
                }
            }

            return Level.MaxActiveTimelines - taken;
        }
    }

    /// <summary>Starts a run: one root timeline holding the level's starting board.</summary>
    public static GameState Start(LevelDefinition level)
    {
        if (level is null)
        {
            throw new ArgumentNullException(nameof(level));
        }

        SudokuBoard startingBoard = level.StartingBoard;
        TimelineStatus status = TimelineClassifier.ClassifyAfterPlacement(startingBoard);

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
        TimelineStatus nextStatus = TimelineClassifier.ClassifyAfterPlacement(nextBoard);

        Timeline[] updated = ReplaceTimeline(timeline.WithNextState(nextBoard, nextStatus));

        return MoveResult.Applied(new GameState(Level, updated, SelectedTimelineId, RemainingTemporalBudget));
    }

    /// <summary>
    /// Checks a Temporal Move without performing it, and reports which of the
    /// branch conditions fails first.
    /// </summary>
    public TemporalMoveRejection ValidateTemporalMove(
        int sourceTimelineId,
        int sourceTime,
        int row,
        int column,
        int value)
    {
        // A won run is finished. A lost one is not: a Temporal Move is exactly the
        // way back from a dead end, so it stays available while budget and a
        // reachable historical state remain.
        if (Outcome == GameOutcome.Won)
        {
            return TemporalMoveRejection.RunAlreadyWon;
        }

        // 1. temporal budget must be left
        if (RemainingTemporalBudget <= 0)
        {
            return TemporalMoveRejection.NoTemporalBudget;
        }

        // 2. the source timeline must exist
        Timeline? source = FindTimeline(sourceTimelineId);

        if (source is null)
        {
            return TemporalMoveRejection.SourceTimelineNotFound;
        }

        // 3. it must hold a state at the requested time
        if (!source.ContainsTime(sourceTime))
        {
            return TemporalMoveRejection.SourceTimeNotInTimeline;
        }

        // 4. that state must lie strictly behind the source's own frontier
        if (sourceTime >= source.FrontierTime)
        {
            return TemporalMoveRejection.SourceTimeIsNotHistorical;
        }

        // Conditions 5 and 6 measure from the present. A lost run may have no
        // present at all — nothing is active — and yet reaching back into history
        // is exactly what should still be possible there. In that case the source
        // timeline's own frontier stands in: it is where that line of history
        // actually ended, and so where a player reaching back counts from. While a
        // present exists, nothing about these two conditions changes.
        int reachFrom = Present ?? source.FrontierTime;

        // 5. the source state must lie strictly before that point
        if (sourceTime >= reachFrom)
        {
            return TemporalMoveRejection.SourceTimeNotBeforePresent;
        }

        // 6. and within the level's temporal window
        int reach = reachFrom - sourceTime;

        if (reach > Level.TemporalWindow)
        {
            return TemporalMoveRejection.OutsideTemporalWindow;
        }

        // 7. the alternative placement must be legal Sudoku on the source board
        SudokuBoard sourceBoard = source.StateAt(sourceTime);

        if (!sourceBoard.IsPlacementLegal(row, column, value))
        {
            return TemporalMoveRejection.PlacementNotLegal;
        }

        // 8. and it must actually change that board. A legal placement always
        // lands on an empty cell, so condition 7 already implies this; the check
        // stays because the rule is stated separately and a future change to what
        // counts as a legal placement must not quietly weaken it.
        if (sourceBoard.WithValue(row, column, value).Equals(sourceBoard))
        {
            return TemporalMoveRejection.BranchDoesNotChangeTheBoard;
        }

        return TemporalMoveRejection.None;
    }

    /// <summary>
    /// Performs a Temporal Move: branches a new timeline off a historical state of
    /// an existing timeline, carrying that state over and playing one alternative
    /// legal placement on it.
    ///
    /// The source timeline is never modified — a branch is not an edit to anyone's
    /// history. The new timeline is classified immediately, so a branch that is
    /// legal but impossible to complete is reported dead rather than left looking
    /// playable. Costs exactly one unit of temporal budget.
    /// </summary>
    public TemporalMoveResult PerformTemporalMove(
        int sourceTimelineId,
        int sourceTime,
        int row,
        int column,
        int value)
    {
        TemporalMoveRejection rejection = ValidateTemporalMove(sourceTimelineId, sourceTime, row, column, value);

        if (rejection != TemporalMoveRejection.None)
        {
            return TemporalMoveResult.Refused(rejection, this);
        }

        Timeline source = GetTimeline(sourceTimelineId);
        SudokuBoard branchedBoard = source.StateAt(sourceTime).WithValue(row, column, value);

        // With a slot to spare the branch enters play and is classified at once, so
        // an impossible one is never left looking playable. With no slot to spare it
        // is created inactive: it exists and can be inspected, but it takes no part
        // in the run until a slot frees up and it is activated, and only then is it
        // worth asking the solver what it is.
        bool slotAvailable = FreeActiveSlots > 0;
        TimelineStatus status = slotAvailable
            ? TimelineClassifier.ClassifyBoard(branchedBoard)
            : TimelineStatus.Inactive;

        Timeline branch = Timeline.CreateBranch(
            NextTimelineId(),
            source,
            sourceTime,
            branchedBoard,
            status,
            occupiesActiveSlot: slotAvailable);

        Timeline[] extended = new Timeline[_timelines.Length + 1];
        Array.Copy(_timelines, extended, _timelines.Length);
        extended[_timelines.Length] = branch;

        GameState next = new GameState(Level, extended, SelectedTimelineId, RemainingTemporalBudget - 1);

        return TemporalMoveResult.Created(next, branch.Id);
    }

    /// <summary>
    /// Checks whether an inactive timeline can be brought into play without doing
    /// it, and reports what stands in the way.
    /// </summary>
    public TimelineSlotChangeRejection ValidateActivation(int timelineId)
    {
        if (Outcome == GameOutcome.Won)
        {
            return TimelineSlotChangeRejection.RunAlreadyWon;
        }

        Timeline? timeline = FindTimeline(timelineId);

        if (timeline is null)
        {
            return TimelineSlotChangeRejection.TimelineNotFound;
        }

        if (timeline.Status != TimelineStatus.Inactive)
        {
            return TimelineSlotChangeRejection.TimelineNotInactive;
        }

        if (FreeActiveSlots <= 0)
        {
            return TimelineSlotChangeRejection.NoFreeActiveSlot;
        }

        return TimelineSlotChangeRejection.None;
    }

    /// <summary>
    /// Checks whether an active timeline can be parked without doing it, and
    /// reports what stands in the way.
    /// </summary>
    public TimelineSlotChangeRejection ValidateDeactivation(int timelineId)
    {
        if (Outcome == GameOutcome.Won)
        {
            return TimelineSlotChangeRejection.RunAlreadyWon;
        }

        Timeline? timeline = FindTimeline(timelineId);

        if (timeline is null)
        {
            return TimelineSlotChangeRejection.TimelineNotFound;
        }

        if (timeline.Status != TimelineStatus.Active)
        {
            return TimelineSlotChangeRejection.TimelineNotActive;
        }

        return TimelineSlotChangeRejection.None;
    }

    /// <summary>
    /// Brings an inactive timeline into play. It takes a free active slot and is
    /// classified on the spot, so a timeline that turns out to be impossible enters
    /// as dead rather than as something to keep spending moves on.
    ///
    /// Costs no temporal budget: this is a management action, not a move.
    /// </summary>
    public TimelineSlotChangeResult ActivateTimeline(int timelineId)
    {
        TimelineSlotChangeRejection rejection = ValidateActivation(timelineId);

        if (rejection != TimelineSlotChangeRejection.None)
        {
            return TimelineSlotChangeResult.Refused(rejection, this);
        }

        Timeline timeline = GetTimeline(timelineId);
        TimelineStatus status = TimelineClassifier.ClassifyBoard(timeline.Frontier);

        Timeline[] updated = ReplaceTimeline(timeline.WithStatusAndSlot(status, occupiesActiveSlot: true));

        return TimelineSlotChangeResult.Applied(
            new GameState(Level, updated, SelectedTimelineId, RemainingTemporalBudget));
    }

    /// <summary>
    /// Parks an active timeline: it gives up its slot and becomes inactive, so
    /// another timeline can take the slot instead. The counterpart to
    /// <see cref="ActivateTimeline"/>, and the only way to free a slot without
    /// waiting for a timeline to die.
    ///
    /// A parked timeline keeps its whole history and can be inspected and switched
    /// to, but stops counting towards the present and cannot be played. Costs no
    /// temporal budget.
    ///
    /// Parking is not free of consequence. Bringing a timeline back in classifies
    /// it, which ordinary play never does, so a line that was quietly doomed all
    /// along comes back dead rather than active. Deciding to park something is
    /// therefore a real decision and not a costless toggle.
    /// </summary>
    public TimelineSlotChangeResult DeactivateTimeline(int timelineId)
    {
        TimelineSlotChangeRejection rejection = ValidateDeactivation(timelineId);

        if (rejection != TimelineSlotChangeRejection.None)
        {
            return TimelineSlotChangeResult.Refused(rejection, this);
        }

        Timeline timeline = GetTimeline(timelineId);

        Timeline[] updated = ReplaceTimeline(
            timeline.WithStatusAndSlot(TimelineStatus.Inactive, occupiesActiveSlot: false));

        return TimelineSlotChangeResult.Applied(
            new GameState(Level, updated, SelectedTimelineId, RemainingTemporalBudget));
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
    /// Won as soon as any timeline holding an active slot has reached a complete,
    /// valid solution — the root or any branch, it makes no difference. A solved
    /// timeline that holds no slot has not entered play and so cannot end the
    /// level until it is activated.
    ///
    /// Otherwise the run is over once nothing is left that could still be played.
    /// A dead or inactive timeline never counts as playable, and one branch dying
    /// never ends a run while another active timeline can still be advanced.
    ///
    /// Never inferred from the present.
    /// </summary>
    private static GameOutcome ComputeOutcome(Timeline[] timelines)
    {
        bool anyPlayable = false;

        for (int index = 0; index < timelines.Length; index++)
        {
            Timeline timeline = timelines[index];

            if (timeline.Status == TimelineStatus.Solved && timeline.OccupiesActiveSlot)
            {
                return GameOutcome.Won;
            }

            anyPlayable |= timeline.IsCapableOfFurtherPlay;
        }

        return anyPlayable ? GameOutcome.InProgress : GameOutcome.GameOver;
    }

    /// <summary>
    /// Next free timeline id. Ids are handed out in ascending order and never
    /// reused, so the same action sequence always produces the same ids.
    /// </summary>
    private int NextTimelineId()
    {
        int highest = _timelines[0].Id;

        for (int index = 1; index < _timelines.Length; index++)
        {
            if (_timelines[index].Id > highest)
            {
                highest = _timelines[index].Id;
            }
        }

        return highest + 1;
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
