using System;
using System.Collections.Generic;
using System.Text;
using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Level;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;
using FiveDSudoku.Core.Validation;

namespace FiveDSudoku.Tests;

/// <summary>
/// Plays a level by taking random legal actions, checking every invariant after
/// each one.
///
/// The walk is bounded rather than exhaustive. Placements are enumerated because
/// whether any exists decides when the walk is over; branches are sampled with a
/// small number of tries instead, because enumerating every source time, cell and
/// value on a 9x9 grid would cost far more than it tells us.
///
/// Nothing here reaches past the public API: every action is one a player could
/// take, which is the only reason a green run means anything.
/// </summary>
internal sealed class RandomPlaySession
{
    /// <summary>How often to sample before concluding no branch is available.</summary>
    private const int BranchSampleAttempts = 40;

    private readonly Random _random;
    private readonly List<string> _log = new List<string>();
    private readonly List<InvariantViolation> _violations = new List<InvariantViolation>();

    internal RandomPlaySession(LevelDefinition level, Random random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        State = GameState.Start(level);
        _violations.AddRange(GameInvariants.Check(State));
        _log.Add($"start {level.Id}");
    }

    /// <summary>The state the walk has reached.</summary>
    internal GameState State { get; private set; }

    /// <summary>Everything the invariants complained about along the way.</summary>
    internal IReadOnlyList<InvariantViolation> Violations => _violations;

    /// <summary>What the walk did, in order. Only interesting when something failed.</summary>
    internal IReadOnlyList<string> Log => _log;

    /// <summary>How many state-producing actions were taken.</summary>
    internal int ActionsTaken { get; private set; }

    /// <summary>How many branches the walk made.</summary>
    internal int BranchesMade { get; private set; }

    /// <summary>How many parked timelines it brought back into play.</summary>
    internal int Activations { get; private set; }

    /// <summary>How many timelines it parked.</summary>
    internal int Deactivations { get; private set; }

    /// <summary>
    /// Takes up to <paramref name="maxActions"/> actions, stopping early if the run
    /// is won or nothing is left that could change the state.
    /// </summary>
    internal void RunFor(int maxActions)
    {
        for (int step = 0; step < maxActions; step++)
        {
            if (State.Outcome == GameOutcome.Won)
            {
                _log.Add("won");
                return;
            }

            if (!TakeOneAction())
            {
                _log.Add($"stuck ({State.Outcome})");
                return;
            }
        }

        _log.Add($"budget of {maxActions} actions used up");
    }

    /// <summary>A compact, order-independent description, for comparing two runs.</summary>
    internal string Describe()
    {
        StringBuilder builder = new StringBuilder();

        builder.Append("present=").Append(State.Present is null ? "none" : State.Present.Value.ToString())
            .Append(" outcome=").Append(State.Outcome)
            .Append(" budget=").Append(State.RemainingTemporalBudget)
            .Append(" selected=").Append(State.SelectedTimelineId)
            .Append('\n');

        foreach (Timeline timeline in State.Timelines)
        {
            builder.Append("L").Append(timeline.Id)
                .Append(" parent=").Append(timeline.ParentId?.ToString() ?? "-")
                .Append(" branch=").Append(timeline.BranchTime?.ToString() ?? "-")
                .Append(" first=").Append(timeline.FirstStateTime)
                .Append(" frontier=").Append(timeline.FrontierTime)
                .Append(' ').Append(timeline.Status)
                .Append(timeline.OccupiesActiveSlot ? " slot" : " noslot")
                .Append('\n');

            for (int time = timeline.FirstStateTime; time <= timeline.FrontierTime; time++)
            {
                builder.Append("  T").Append(time).Append(' ')
                    .Append(Flatten(timeline.StateAt(time))).Append('\n');
            }
        }

        return builder.ToString();
    }

    private bool TakeOneAction()
    {
        // Placements dominate on purpose: a walk that mostly switches and parks
        // never gets deep enough into a run to stress anything.
        List<Func<bool>> weighted = new List<Func<bool>>();

        AddIfAvailable(weighted, TryPlace, 6);
        AddIfAvailable(weighted, TryBranch, 2);
        AddIfAvailable(weighted, TryActivate, 1);
        AddIfAvailable(weighted, TryDeactivate, 1);

        while (weighted.Count > 0)
        {
            int pick = _random.Next(weighted.Count);

            if (weighted[pick]())
            {
                MaybeSwitchTimeline();
                return true;
            }

            weighted.RemoveAt(pick);
        }

        return false;
    }

    private static void AddIfAvailable(List<Func<bool>> weighted, Func<bool> action, int weight)
    {
        for (int index = 0; index < weight; index++)
        {
            weighted.Add(action);
        }
    }

    /// <summary>Switching is a view change, so it is folded in rather than counted as an action.</summary>
    private void MaybeSwitchTimeline()
    {
        if (State.Timelines.Count < 2 || _random.Next(4) != 0)
        {
            return;
        }

        int target = State.Timelines[_random.Next(State.Timelines.Count)].Id;
        GameState before = State;
        GameState after = before.SelectTimeline(target);

        Record($"switch to L{target}", before, after);
    }

    private bool TryPlace()
    {
        List<(int Timeline, int Row, int Column, int Value)> options =
            new List<(int, int, int, int)>();

        BoardSize size = State.Level.Size;

        foreach (Timeline timeline in State.Timelines)
        {
            if (timeline.Status != TimelineStatus.Active ||
                !timeline.OccupiesActiveSlot ||
                State.Present is null ||
                timeline.FrontierTime != State.Present.Value)
            {
                continue;
            }

            SudokuBoard board = timeline.Frontier;

            for (int row = 0; row < size.Side; row++)
            {
                for (int column = 0; column < size.Side; column++)
                {
                    for (int value = size.MinValue; value <= size.MaxValue; value++)
                    {
                        if (board.IsPlacementLegal(row, column, value))
                        {
                            options.Add((timeline.Id, row, column, value));
                        }
                    }
                }
            }
        }

        if (options.Count == 0)
        {
            return false;
        }

        (int timelineId, int pickedRow, int pickedColumn, int pickedValue) = options[_random.Next(options.Count)];

        GameState before = State.SelectTimeline(timelineId);
        MoveResult result = before.PlaceValue(pickedRow, pickedColumn, pickedValue);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"A placement believed legal was refused: {result.Rejection}.");
        }

        Record($"place {pickedValue} at r{pickedRow}c{pickedColumn} on L{timelineId}", before, result.State);
        ActionsTaken++;

        return true;
    }

    private bool TryBranch()
    {
        if (State.RemainingTemporalBudget <= 0)
        {
            return false;
        }

        BoardSize size = State.Level.Size;

        for (int attempt = 0; attempt < BranchSampleAttempts; attempt++)
        {
            Timeline source = State.Timelines[_random.Next(State.Timelines.Count)];

            if (source.StateCount < 2)
            {
                continue;
            }

            int sourceTime = source.FirstStateTime + _random.Next(source.StateCount - 1);
            int row = _random.Next(size.Side);
            int column = _random.Next(size.Side);
            int value = size.MinValue + _random.Next(size.Side);

            if (State.ValidateTemporalMove(source.Id, sourceTime, row, column, value)
                != TemporalMoveRejection.None)
            {
                continue;
            }

            GameState before = State;
            TemporalMoveResult result = before.PerformTemporalMove(source.Id, sourceTime, row, column, value);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"A branch believed legal was refused: {result.Rejection}.");
            }

            Record(
                $"branch off L{source.Id} at T{sourceTime} with {value} at r{row}c{column} " +
                $"-> L{result.NewTimelineId}",
                before,
                result.State);
            ActionsTaken++;
            BranchesMade++;

            return true;
        }

        return false;
    }

    private bool TryActivate()
    {
        if (State.FreeActiveSlots <= 0)
        {
            return false;
        }

        List<int> parked = new List<int>();

        foreach (Timeline timeline in State.Timelines)
        {
            if (State.ValidateActivation(timeline.Id) == TimelineSlotChangeRejection.None)
            {
                parked.Add(timeline.Id);
            }
        }

        if (parked.Count == 0)
        {
            return false;
        }

        int target = parked[_random.Next(parked.Count)];
        GameState before = State;
        TimelineSlotChangeResult result = before.ActivateTimeline(target);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"An activation believed legal was refused: {result.Rejection}.");
        }

        Record($"activate L{target}", before, result.State);
        ActionsTaken++;
        Activations++;

        return true;
    }

    private bool TryDeactivate()
    {
        List<int> inPlay = new List<int>();

        foreach (Timeline timeline in State.Timelines)
        {
            if (State.ValidateDeactivation(timeline.Id) == TimelineSlotChangeRejection.None)
            {
                inPlay.Add(timeline.Id);
            }
        }

        // Parking the only timeline in play is legal, but a walk that keeps doing
        // it never gets anywhere. Leave one behind.
        if (inPlay.Count < 2)
        {
            return false;
        }

        int target = inPlay[_random.Next(inPlay.Count)];
        GameState before = State;
        TimelineSlotChangeResult result = before.DeactivateTimeline(target);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"A deactivation believed legal was refused: {result.Rejection}.");
        }

        Record($"park L{target}", before, result.State);
        ActionsTaken++;
        Deactivations++;

        return true;
    }

    private void Record(string description, GameState before, GameState after)
    {
        _violations.AddRange(GameInvariants.CheckTransition(before, after));
        _log.Add(description);
        State = after;
    }

    private static string Flatten(SudokuBoard board)
    {
        StringBuilder builder = new StringBuilder();
        int side = board.Size.Side;

        for (int row = 0; row < side; row++)
        {
            for (int column = 0; column < side; column++)
            {
                int value = board[row, column];
                builder.Append(value == BoardSize.EmptyCell ? "." : value.ToString());
            }
        }

        return builder.ToString();
    }
}
