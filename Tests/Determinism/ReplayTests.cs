using System;
using System.Collections.Generic;
using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Level;
using FiveDSudoku.Core.Persistence;
using FiveDSudoku.Core.Solver;
using FiveDSudoku.Core.Sudoku;

namespace FiveDSudoku.Tests;

/// <summary>
/// The determinism audit, run across everything built so far.
///
/// The claim is that the same level plus the same starting state plus the same
/// action sequence always produces the same result. Two states are compared by
/// their save text, which carries every timeline, every state of every timeline
/// and every derived value — so "the same" here means genuinely the same run,
/// not the same summary of one.
/// </summary>
[TestFixture]
public sealed class ReplayTests
{
    /// <summary>One recorded action. Enough kinds to cover every way a run changes.</summary>
    private abstract class Action
    {
        internal abstract GameState ApplyTo(GameState state);
    }

    private sealed class Place : Action
    {
        internal Place(int row, int column, int value)
        {
            Row = row;
            Column = column;
            Value = value;
        }

        internal int Row { get; }

        internal int Column { get; }

        internal int Value { get; }

        internal override GameState ApplyTo(GameState state)
        {
            MoveResult result = state.PlaceValue(Row, Column, Value);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"r{Row}c{Column}={Value} refused: {result.Rejection}");
            }

            return result.State;
        }
    }

    private sealed class Branch : Action
    {
        internal Branch(int sourceTimelineId, int sourceTime, int row, int column, int value)
        {
            SourceTimelineId = sourceTimelineId;
            SourceTime = sourceTime;
            Row = row;
            Column = column;
            Value = value;
        }

        internal int SourceTimelineId { get; }

        internal int SourceTime { get; }

        internal int Row { get; }

        internal int Column { get; }

        internal int Value { get; }

        internal override GameState ApplyTo(GameState state)
        {
            TemporalMoveResult result = state.PerformTemporalMove(
                SourceTimelineId, SourceTime, Row, Column, Value);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"branch refused: {result.Rejection}");
            }

            return result.State;
        }
    }

    private sealed class Select : Action
    {
        internal Select(int timelineId) => TimelineId = timelineId;

        internal int TimelineId { get; }

        internal override GameState ApplyTo(GameState state) => state.SelectTimeline(TimelineId);
    }

    private sealed class Park : Action
    {
        internal Park(int timelineId) => TimelineId = timelineId;

        internal int TimelineId { get; }

        internal override GameState ApplyTo(GameState state)
        {
            TimelineSlotChangeResult result = state.DeactivateTimeline(TimelineId);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"parking L{TimelineId} refused: {result.Rejection}");
            }

            return result.State;
        }
    }

    private sealed class Revive : Action
    {
        internal Revive(int timelineId) => TimelineId = timelineId;

        internal int TimelineId { get; }

        internal override GameState ApplyTo(GameState state)
        {
            TimelineSlotChangeResult result = state.ActivateTimeline(TimelineId);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"activating L{TimelineId} refused: {result.Rejection}");
            }

            return result.State;
        }
    }

    private static LevelDefinition Level() =>
        Levels.SolvableFourByFour(temporalBudget: 3, temporalWindow: 4, maxActiveTimelines: 2);

    /// <summary>
    /// A sequence touching every action the engine has: ordinary play, a branch
    /// that dies, a branch that lives, switching, parking and bringing back.
    /// </summary>
    private static IReadOnlyList<Action> Sequence() => new Action[]
    {
        new Place(0, 1, 4),
        new Place(0, 2, 3),
        new Branch(GameState.RootTimelineId, 0, 0, 1, 3),
        new Select(1),
        new Select(GameState.RootTimelineId),
        new Branch(GameState.RootTimelineId, 1, 0, 2, 3),
        new Select(2),
        new Park(GameState.RootTimelineId),
        new Place(0, 3, 2),
        new Revive(GameState.RootTimelineId),
    };

    private static GameState Replay(LevelDefinition level, IReadOnlyList<Action> actions)
    {
        GameState state = GameState.Start(level);

        foreach (Action action in actions)
        {
            state = action.ApplyTo(state);
        }

        return state;
    }

    // ---- the audit ---------------------------------------------------------

    [Test]
    public void TheSameLevelAndActionSequenceProduceTheSameRunTwice()
    {
        string first = GameSaveFormat.Write(Replay(Level(), Sequence()));
        string second = GameSaveFormat.Write(Replay(Level(), Sequence()));

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void TheSameSequenceIsStillTheSameRunAfterATripThroughASaveFile()
    {
        // Saving in the middle of a run must not change where it ends up.
        IReadOnlyList<Action> actions = Sequence();
        LevelDefinition level = Level();

        GameState straightThrough = Replay(level, actions);

        GameState viaSave = GameState.Start(level);

        foreach (Action action in actions)
        {
            viaSave = GameSaveFormat.Read(GameSaveFormat.Write(action.ApplyTo(viaSave)));
        }

        Assert.That(GameSaveFormat.Write(viaSave), Is.EqualTo(GameSaveFormat.Write(straightThrough)));
    }

    [Test]
    public void TheSolverGivesTheSameAnswerEveryTime()
    {
        // Determinism has to hold below the game rules too: the solver decides
        // whether a branch lives or dies, so a solver that wandered would make the
        // whole run wander with it.
        SudokuBoard puzzle = SudokuBoard.Parse(BoardSize.NineByNine, Puzzles.Unique9);

        SudokuBoard? first = SudokuSolver.FindFirstSolution(puzzle);

        for (int repetition = 0; repetition < 5; repetition++)
        {
            Assert.That(SudokuSolver.FindFirstSolution(puzzle), Is.EqualTo(first));
            Assert.That(SudokuSolver.CountSolutions(puzzle, 5), Is.EqualTo(1));
        }
    }

    [Test]
    public void TimelineIdsAreHandedOutTheSameWayOnEveryRun()
    {
        GameState first = Replay(Level(), Sequence());
        GameState second = Replay(Level(), Sequence());

        Assert.That(
            CollectIds(second),
            Is.EqualTo(CollectIds(first)),
            "a run that renumbered its timelines would not replay");
    }

    [Test]
    public void ADifferentSequenceReachesADifferentRun()
    {
        // The comparison is only worth anything if it can tell runs apart.
        List<Action> shorter = new List<Action>(Sequence());
        shorter.RemoveAt(shorter.Count - 1);

        Assert.That(
            GameSaveFormat.Write(Replay(Level(), shorter)),
            Is.Not.EqualTo(GameSaveFormat.Write(Replay(Level(), Sequence()))));
    }

    [Test]
    public void ARandomWalkReplaysExactlyWhenItsSeedIsReused()
    {
        LevelDefinition level = Level();

        RandomPlaySession first = new RandomPlaySession(level, new Random(20260819));
        first.RunFor(60);

        RandomPlaySession second = new RandomPlaySession(level, new Random(20260819));
        second.RunFor(60);

        Assert.That(second.Log, Is.EqualTo(first.Log));
        Assert.That(
            GameSaveFormat.Write(second.State),
            Is.EqualTo(GameSaveFormat.Write(first.State)));
    }

    private static List<int> CollectIds(GameState state)
    {
        List<int> ids = new List<int>();

        foreach (var timeline in state.Timelines)
        {
            ids.Add(timeline.Id);
        }

        return ids;
    }
}
