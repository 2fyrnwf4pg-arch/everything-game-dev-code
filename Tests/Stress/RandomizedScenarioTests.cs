using System;
using System.Collections.Generic;
using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Level;
using FiveDSudoku.Core.Solver;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;
using FiveDSudoku.Core.Validation;

namespace FiveDSudoku.Tests;

/// <summary>
/// Scenarios A to E again, but on randomly generated levels rather than the one
/// hand-picked puzzle each was written against.
///
/// The hand-written versions say the rules work on that board. These say they
/// work on boards nobody chose — which is the only way to find out whether the
/// fixed cases were doing the work.
/// </summary>
[TestFixture]
public sealed class RandomizedScenarioTests
{
    private const int Repetitions = 12;

    private static LevelDefinition UniqueLevel(
        Random random,
        int temporalBudget = 3,
        int temporalWindow = 16,
        int maxActiveTimelines = 2) =>
        new LevelDefinition(
            "randomized",
            PuzzleGenerator.RandomPuzzleWithUniqueSolution(BoardSize.FourByFour, random),
            temporalBudget,
            temporalWindow,
            maxActiveTimelines,
            finalDepth: 12);

    /// <summary>The empty cells of a board, in a random order.</summary>
    private static List<(int Row, int Column)> EmptyCellsShuffled(SudokuBoard board, Random random)
    {
        List<(int Row, int Column)> cells = new List<(int, int)>();

        for (int row = 0; row < board.Size.Side; row++)
        {
            for (int column = 0; column < board.Size.Side; column++)
            {
                if (board.IsEmpty(row, column))
                {
                    cells.Add((row, column));
                }
            }
        }

        for (int index = cells.Count - 1; index > 0; index--)
        {
            int swap = random.Next(index + 1);
            (cells[index], cells[swap]) = (cells[swap], cells[index]);
        }

        return cells;
    }

    // ---- Scenario A --------------------------------------------------------

    [Test]
    public void ScenarioA_AnyUniquelySolvableLevelIsWonInAnyOrderWithoutSpendingBudget()
    {
        Random random = StressSeeds.For(nameof(ScenarioA_AnyUniquelySolvableLevelIsWonInAnyOrderWithoutSpendingBudget));

        for (int repetition = 0; repetition < Repetitions; repetition++)
        {
            LevelDefinition level = UniqueLevel(random);
            SudokuBoard solution = SudokuSolver.FindFirstSolution(level.StartingBoard)!;
            GameState game = GameState.Start(level);

            // A different, random filling order every time — the rules must not
            // care which cell a player reaches for first.
            foreach ((int row, int column) in EmptyCellsShuffled(level.StartingBoard, random))
            {
                MoveResult result = game.PlaceValue(row, column, solution[row, column]);

                Assert.That(result.Succeeded, Is.True, $"r{row}c{column}: {result.Rejection}");

                game = result.State;
            }

            Assert.That(game.Outcome, Is.EqualTo(GameOutcome.Won));
            Assert.That(game.RemainingTemporalBudget, Is.EqualTo(level.TemporalBudget));
            Assert.That(game.Timelines, Has.Count.EqualTo(1));
        }
    }

    // ---- Scenario B --------------------------------------------------------

    [Test]
    public void ScenarioB_BranchingIntoHistoryLeavesTheParentIntactAndPullsThePresentBack()
    {
        Random random = StressSeeds.For(nameof(ScenarioB_BranchingIntoHistoryLeavesTheParentIntactAndPullsThePresentBack));
        int demonstrated = 0;

        for (int repetition = 0; repetition < Repetitions; repetition++)
        {
            LevelDefinition level = UniqueLevel(random);
            SudokuBoard solution = SudokuSolver.FindFirstSolution(level.StartingBoard)!;
            List<(int Row, int Column)> order = EmptyCellsShuffled(level.StartingBoard, random);

            if (order.Count < 3)
            {
                continue;
            }

            // Get the root far enough ahead that branching at T0 really is going back.
            GameState game = GameState.Start(level);

            for (int move = 0; move < 2; move++)
            {
                game = game.PlaceValue(order[move].Row, order[move].Column, solution[order[move].Row, order[move].Column]).State;
            }

            Assert.That(game.Present, Is.EqualTo(2));

            GameState beforeBranch = game;
            Timeline rootBefore = beforeBranch.GetTimeline(GameState.RootTimelineId);
            SudokuBoard[] historyBefore =
            {
                rootBefore.StateAt(0), rootBefore.StateAt(1), rootBefore.StateAt(2),
            };

            // Branch on a cell the root has not touched, taking the value that keeps
            // the puzzle winnable, so the child is playable rather than a dead end.
            (int row, int column) = order[2];
            TemporalMoveResult branched = game.PerformTemporalMove(
                GameState.RootTimelineId, 0, row, column, solution[row, column]);

            Assert.That(branched.Succeeded, Is.True, branched.Rejection.ToString());

            GameState afterBranch = branched.State;
            Timeline child = afterBranch.GetTimeline(branched.NewTimelineId!.Value);
            Timeline rootAfter = afterBranch.GetTimeline(GameState.RootTimelineId);

            Assert.That(child.Status, Is.EqualTo(TimelineStatus.Active));
            Assert.That(child.ParentId, Is.EqualTo(GameState.RootTimelineId));
            Assert.That(child.BranchTime, Is.EqualTo(0));
            Assert.That(child.FirstStateTime, Is.EqualTo(0));
            Assert.That(child.FrontierTime, Is.EqualTo(1));
            Assert.That(child.StateAt(0), Is.EqualTo(level.StartingBoard));

            Assert.That(rootAfter.StateCount, Is.EqualTo(3), "the parent did not grow or shrink");

            for (int time = 0; time <= 2; time++)
            {
                Assert.That(rootAfter.StateAt(time), Is.EqualTo(historyBefore[time]), $"root T{time}");
            }

            Assert.That(afterBranch.Present, Is.EqualTo(1), "the present went backwards");
            Assert.That(
                afterBranch.PlaceValue(row, column, solution[row, column]).Rejection,
                Is.EqualTo(MoveRejection.TimelineNotAtPresent),
                "the root has to wait for the child");

            // Play the child, and the two stay separate objects with separate pasts.
            GameState played = afterBranch.SelectTimeline(child.Id);
            played = Levels.PlayAnyLegalMove(played);

            Assert.That(played.GetTimeline(child.Id).FrontierTime, Is.EqualTo(2));
            Assert.That(
                played.GetTimeline(child.Id).StateAt(1),
                Is.Not.EqualTo(played.GetTimeline(GameState.RootTimelineId).StateAt(1)));
            Assert.That(GameInvariants.CheckTransition(beforeBranch, played), Is.Empty);

            demonstrated++;
        }

        Assert.That(demonstrated, Is.GreaterThan(0), "no level was big enough to demonstrate the scenario");
    }

    // ---- Scenario C --------------------------------------------------------

    [Test]
    public void ScenarioC_ALocallyLegalButImpossibleBranchIsAlwaysDeadOnArrival()
    {
        Random random = StressSeeds.For(nameof(ScenarioC_ALocallyLegalButImpossibleBranchIsAlwaysDeadOnArrival));
        int demonstrated = 0;

        for (int repetition = 0; repetition < Repetitions; repetition++)
        {
            LevelDefinition level = UniqueLevel(random);
            SudokuBoard start = level.StartingBoard;
            SudokuBoard solution = SudokuSolver.FindFirstSolution(start)!;
            List<(int Row, int Column)> order = EmptyCellsShuffled(start, random);

            GameState game = GameState.Start(level);
            game = game.PlaceValue(order[0].Row, order[0].Column, solution[order[0].Row, order[0].Column]).State;

            // On a uniquely solvable puzzle every legal value that is not the
            // solution's is a trap: locally fine, globally hopeless.
            foreach ((int row, int column) in order)
            {
                for (int value = 1; value <= start.Size.Side; value++)
                {
                    if (value == solution[row, column] || !start.IsPlacementLegal(row, column, value))
                    {
                        continue;
                    }

                    if (game.ValidateTemporalMove(GameState.RootTimelineId, 0, row, column, value)
                        != TemporalMoveRejection.None)
                    {
                        continue;
                    }

                    TemporalMoveResult trap = game.PerformTemporalMove(
                        GameState.RootTimelineId, 0, row, column, value);

                    Assert.That(trap.Succeeded, Is.True, trap.Rejection.ToString());

                    Timeline branch = trap.State.GetTimeline(trap.NewTimelineId!.Value);

                    Assert.That(branch.Frontier.IsValid(), Is.True, "the trap breaks no Sudoku rule");
                    Assert.That(SudokuSolver.HasSolution(branch.Frontier), Is.False, "and cannot be completed");
                    Assert.That(branch.Status, Is.EqualTo(TimelineStatus.Dead), "so it must arrive dead");
                    Assert.That(branch.OccupiesActiveSlot, Is.False);

                    Timeline parent = trap.State.GetTimeline(GameState.RootTimelineId);

                    Assert.That(parent.Status, Is.EqualTo(TimelineStatus.Active));
                    Assert.That(SudokuSolver.HasUniqueSolution(parent.Frontier), Is.True);
                    Assert.That(trap.State.Outcome, Is.EqualTo(GameOutcome.InProgress));

                    demonstrated++;

                    goto nextLevel;
                }
            }

            nextLevel: ;
        }

        Assert.That(demonstrated, Is.GreaterThan(0), "no trap was found on any generated level");
    }

    // ---- Scenario D --------------------------------------------------------

    [Test]
    public void ScenarioD_EveryGeneratedLevelIsCertifiedWinnableWithoutTimeTravel()
    {
        Random random = StressSeeds.For(nameof(ScenarioD_EveryGeneratedLevelIsCertifiedWinnableWithoutTimeTravel));

        for (int repetition = 0; repetition < Repetitions; repetition++)
        {
            LevelDefinition level = UniqueLevel(random, temporalBudget: 3, temporalWindow: 4);
            LevelValidationReport report = LevelValidator.Validate(level);

            Assert.That(
                report.Passed(LevelValidationCheck.SolvableWithoutTemporalMoves),
                Is.True,
                report.ToString());
            Assert.That(
                report.Passed(LevelValidationCheck.AUsefulButOptionalBranchExists),
                Is.True,
                "the branch is an extra, and there is one to take");
            Assert.That(report.IsReleaseCandidate, Is.True, report.ToString());
        }
    }

    // ---- Scenario E --------------------------------------------------------

    [Test]
    public void ScenarioE_BranchesBeyondTheSlotsArePackedAwayAndLeaveThePresentAlone()
    {
        Random random = StressSeeds.For(nameof(ScenarioE_BranchesBeyondTheSlotsArePackedAwayAndLeaveThePresentAlone));
        int demonstrated = 0;

        for (int repetition = 0; repetition < Repetitions; repetition++)
        {
            // A single slot, which the root takes, so every branch has to wait.
            LevelDefinition level = UniqueLevel(random, temporalBudget: 3, maxActiveTimelines: 1);
            SudokuBoard start = level.StartingBoard;
            SudokuBoard solution = SudokuSolver.FindFirstSolution(start)!;
            List<(int Row, int Column)> order = EmptyCellsShuffled(start, random);

            GameState game = GameState.Start(level);

            for (int move = 0; move < 2 && move < order.Count; move++)
            {
                game = game.PlaceValue(order[move].Row, order[move].Column, solution[order[move].Row, order[move].Column]).State;
            }

            int presentBefore = game.Present!.Value;
            List<int> parked = new List<int>();

            foreach ((int row, int column) in order)
            {
                if (parked.Count == 2)
                {
                    break;
                }

                for (int value = 1; value <= start.Size.Side && parked.Count < 2; value++)
                {
                    if (game.ValidateTemporalMove(GameState.RootTimelineId, 0, row, column, value)
                        != TemporalMoveRejection.None)
                    {
                        continue;
                    }

                    TemporalMoveResult extra = game.PerformTemporalMove(
                        GameState.RootTimelineId, 0, row, column, value);

                    Assert.That(extra.Succeeded, Is.True, extra.Rejection.ToString());

                    game = extra.State;
                    parked.Add(extra.NewTimelineId!.Value);
                }
            }

            if (parked.Count < 2)
            {
                continue;
            }

            foreach (int id in parked)
            {
                Timeline timeline = game.GetTimeline(id);

                Assert.That(timeline.Status, Is.EqualTo(TimelineStatus.Inactive), $"L{id}");
                Assert.That(timeline.OccupiesActiveSlot, Is.False, $"L{id}");
                Assert.That(timeline.FrontierTime, Is.LessThan(presentBefore), $"L{id}");
                Assert.That(
                    game.ActivateTimeline(id).Rejection,
                    Is.EqualTo(TimelineSlotChangeRejection.NoFreeActiveSlot),
                    $"L{id}");
            }

            Assert.That(
                game.Present,
                Is.EqualTo(presentBefore),
                "the parked branches sit behind the present and must not drag it back");
            Assert.That(game.FreeActiveSlots, Is.EqualTo(0));

            demonstrated++;
        }

        Assert.That(demonstrated, Is.GreaterThan(0), "no level offered two parked branches");
    }
}
