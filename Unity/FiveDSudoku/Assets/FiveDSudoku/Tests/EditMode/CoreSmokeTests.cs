using System;
using NUnit.Framework;
using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Level;
using FiveDSudoku.Core.Persistence;
using FiveDSudoku.Core.Solver;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;

// Block-scoped namespace, no raw string literals, nothing newer than
// netstandard2.1: Unity's compiler is C# 9, so this file is written to the
// language and API level Unity actually has. See UNITY-PHASES.md.
namespace FiveDSudoku.Unity.Tests
{
    /// <summary>
    /// Proves the core behaves the same inside Unity's runtime as it does under
    /// dotnet — under Mono in the editor, and under IL2CPP once player tests run.
    ///
    /// This is deliberately a smoke subset, not a copy of the real suite. The 337
    /// tests under Tests/ are what prove the rules, and they run through
    /// `dotnet test`. What cannot be proven there is that the same assembly loads
    /// and behaves identically once Unity is hosting it, which is this file's
    /// only job.
    /// </summary>
    public sealed class CoreSmokeTests
    {
        private const string Puzzle4 = "1... ..1. .2.. ...3";

        private const string Puzzle9 =
            "53..7.... 6..195... .98....6. 8...6...3 4..8.3..1 7...2...6 .6....28. ...419..5 ....8..79";

        private static LevelDefinition Level4(
            int temporalBudget = 3,
            int temporalWindow = 4,
            int maxActiveTimelines = 2)
        {
            return new LevelDefinition(
                "smoke-4x4",
                SudokuBoard.Parse(BoardSize.FourByFour, Puzzle4),
                temporalBudget,
                temporalWindow,
                maxActiveTimelines,
                12);
        }

        /// <summary>Plays the solution into every empty cell, using ordinary moves only.</summary>
        private static GameState SolveByOrdinaryPlay(GameState game)
        {
            SudokuBoard start = game.SelectedTimeline.Frontier;
            SudokuBoard solution = SudokuSolver.FindFirstSolution(start);

            Assert.That(solution, Is.Not.Null, "the fixture must be solvable");

            BoardSize size = start.Size;

            for (int row = 0; row < size.Side; row++)
            {
                for (int column = 0; column < size.Side; column++)
                {
                    if (!start.IsEmpty(row, column))
                    {
                        continue;
                    }

                    MoveResult result = game.PlaceValue(row, column, solution[row, column]);

                    Assert.That(result.Succeeded, Is.True, "r" + row + "c" + column + ": " + result.Rejection);

                    game = result.State;
                }
            }

            return game;
        }

        // ---- the assembly itself -------------------------------------------

        [Test]
        public void TheCoreAssemblyLoadsInsideUnityAndCarriesNoUnityDependency()
        {
            System.Reflection.Assembly core = typeof(GameState).Assembly;

            Assert.That(core.GetName().Name, Is.EqualTo("FiveDSudoku.Core"));

            foreach (System.Reflection.AssemblyName reference in core.GetReferencedAssemblies())
            {
                string name = reference.Name;

                Assert.That(
                    name.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("UnityEditor", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("Unity.", StringComparison.OrdinalIgnoreCase),
                    Is.False,
                    "the core must stay engine-independent, but it references " + name);
            }
        }

        // ---- Sudoku ---------------------------------------------------------

        [Test]
        public void BoardGeometryIsDrivenByTheBoxShapeAtBothSizes()
        {
            Assert.That(BoardSize.FourByFour.Side, Is.EqualTo(4));
            Assert.That(BoardSize.FourByFour.CellCount, Is.EqualTo(16));
            Assert.That(BoardSize.NineByNine.Side, Is.EqualTo(9));
            Assert.That(BoardSize.NineByNine.BoxIndex(4, 4), Is.EqualTo(4));
        }

        [Test]
        public void PlacementLegalityRespectsRowColumnAndBox()
        {
            SudokuBoard board = SudokuBoard.Empty(BoardSize.FourByFour).WithValue(0, 0, 1);

            Assert.That(board.IsPlacementLegal(0, 3, 1), Is.False, "same row");
            Assert.That(board.IsPlacementLegal(3, 0, 1), Is.False, "same column");
            Assert.That(board.IsPlacementLegal(1, 1, 1), Is.False, "same box");
            Assert.That(board.IsPlacementLegal(1, 1, 2), Is.True);
        }

        [Test]
        public void TheSolverSeparatesZeroOneAndManySolutions()
        {
            SudokuBoard unique = SudokuBoard.Parse(BoardSize.FourByFour, Puzzle4);
            SudokuBoard impossible = SudokuBoard.Parse(BoardSize.FourByFour, ".12. .4.. 3... ....");
            SudokuBoard several = SudokuBoard.Parse(BoardSize.FourByFour, "..34 3412 ..43 4321");

            Assert.That(SudokuSolver.CountSolutions(unique, 10), Is.EqualTo(1));
            Assert.That(SudokuSolver.CountSolutions(impossible, 10), Is.EqualTo(0));
            Assert.That(SudokuSolver.CountSolutions(several, 10), Is.EqualTo(2));
        }

        [Test]
        public void TheSolverHandlesNineByNine()
        {
            SudokuBoard puzzle = SudokuBoard.Parse(BoardSize.NineByNine, Puzzle9);

            Assert.That(SudokuSolver.HasUniqueSolution(puzzle), Is.True);
        }

        // ---- playing --------------------------------------------------------

        [Test]
        public void AFourByFourIsWonByOrdinaryPlayWithoutSpendingBudget()
        {
            LevelDefinition level = Level4();
            GameState won = SolveByOrdinaryPlay(GameState.Start(level));

            Assert.That(won.Outcome, Is.EqualTo(GameOutcome.Won));
            Assert.That(won.RemainingTemporalBudget, Is.EqualTo(level.TemporalBudget));
            Assert.That(won.Timelines, Has.Count.EqualTo(1));
        }

        [Test]
        public void ANineByNineIsWonByOrdinaryPlay()
        {
            GameState won = SolveByOrdinaryPlay(GameState.Start(new LevelDefinition(
                "smoke-9x9",
                SudokuBoard.Parse(BoardSize.NineByNine, Puzzle9),
                2,
                4,
                2,
                51)));

            Assert.That(won.Outcome, Is.EqualTo(GameOutcome.Won));
            Assert.That(won.SelectedTimeline.FrontierTime, Is.EqualTo(51));
        }

        [Test]
        public void AnIllegalPlacementIsRefusedWithAReasonAndChangesNothing()
        {
            GameState game = GameState.Start(Level4());

            MoveResult result = game.PlaceValue(0, 1, 2);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Rejection, Is.EqualTo(MoveRejection.ViolatesSudokuConstraint));
            Assert.That(result.State, Is.SameAs(game));
        }

        [Test]
        public void HistoryIsKeptAndNeverRewritten()
        {
            GameState before = GameState.Start(Level4());
            SudokuBoard start = before.SelectedTimeline.StateAt(0);

            GameState after = before.PlaceValue(0, 1, 4).State;

            Assert.That(before.SelectedTimeline.StateCount, Is.EqualTo(1));
            Assert.That(after.SelectedTimeline.StateCount, Is.EqualTo(2));
            Assert.That(after.SelectedTimeline.StateAt(0), Is.EqualTo(start));
        }

        // ---- time travel ----------------------------------------------------

        [Test]
        public void AnImpossibleBranchArrivesDeadAndLeavesTheParentAlone()
        {
            GameState game = GameState.Start(Level4()).PlaceValue(0, 1, 4).State;

            TemporalMoveResult trap = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3);

            Assert.That(trap.Succeeded, Is.True, trap.Rejection.ToString());

            Timeline branch = trap.State.GetTimeline(trap.NewTimelineId.Value);

            Assert.That(branch.Frontier.IsValid(), Is.True, "legal by the local rules");
            Assert.That(branch.Status, Is.EqualTo(TimelineStatus.Dead), "and impossible globally");
            Assert.That(
                trap.State.GetTimeline(GameState.RootTimelineId).Status,
                Is.EqualTo(TimelineStatus.Active));
            Assert.That(trap.State.RemainingTemporalBudget, Is.EqualTo(2), "one branch, one unit");
        }

        [Test]
        public void ABranchIntoHistoryPullsThePresentBack()
        {
            GameState game = GameState.Start(Level4());
            game = game.PlaceValue(0, 1, 4).State;
            game = game.PlaceValue(0, 2, 3).State;

            Assert.That(game.Present, Is.EqualTo(2));

            TemporalMoveResult detour = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 2, 3);

            Assert.That(detour.Succeeded, Is.True, detour.Rejection.ToString());
            Assert.That(
                detour.State.GetTimeline(detour.NewTimelineId.Value).Status,
                Is.EqualTo(TimelineStatus.Active));
            Assert.That(detour.State.Present, Is.EqualTo(1));
        }

        [Test]
        public void TheTemporalWindowAndBudgetAreEnforced()
        {
            GameState narrow = GameState.Start(Level4(3, 1));
            narrow = narrow.PlaceValue(0, 1, 4).State;
            narrow = narrow.PlaceValue(0, 2, 3).State;
            narrow = narrow.PlaceValue(0, 3, 2).State;

            Assert.That(
                narrow.ValidateTemporalMove(GameState.RootTimelineId, 0, 1, 0, 3),
                Is.EqualTo(TemporalMoveRejection.OutsideTemporalWindow));

            GameState broke = GameState.Start(Level4(0)).PlaceValue(0, 1, 4).State;

            Assert.That(
                broke.ValidateTemporalMove(GameState.RootTimelineId, 0, 0, 2, 3),
                Is.EqualTo(TemporalMoveRejection.NoTemporalBudget));
        }

        [Test]
        public void ABranchWithNoFreeSlotIsParkedUntilItIsActivated()
        {
            GameState game = GameState.Start(Level4(3, 4, 1)).PlaceValue(0, 1, 4).State;

            TemporalMoveResult parked = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 2, 3);

            Assert.That(parked.Succeeded, Is.True, parked.Rejection.ToString());

            int branchId = parked.NewTimelineId.Value;

            Assert.That(parked.State.GetTimeline(branchId).Status, Is.EqualTo(TimelineStatus.Inactive));
            Assert.That(
                parked.State.ActivateTimeline(branchId).Rejection,
                Is.EqualTo(TimelineSlotChangeRejection.NoFreeActiveSlot));

            GameState freed = parked.State.DeactivateTimeline(GameState.RootTimelineId).State;

            Assert.That(freed.ActivateTimeline(branchId).Succeeded, Is.True);
        }

        // ---- persistence and determinism -------------------------------------

        [Test]
        public void ARunSurvivesBeingWrittenDownAndReadBack()
        {
            GameState game = GameState.Start(Level4()).PlaceValue(0, 1, 4).State;
            game = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3).State;

            string save = GameSaveFormat.Write(game);
            GameState loaded = GameSaveFormat.Read(save);

            Assert.That(GameSaveFormat.Write(loaded), Is.EqualTo(save));
            Assert.That(loaded.Timelines, Has.Count.EqualTo(game.Timelines.Count));
            Assert.That(loaded.Present, Is.EqualTo(game.Present));
            Assert.That(loaded.Outcome, Is.EqualTo(game.Outcome));
        }

        [Test]
        public void ASaveThatDoesNotAddUpIsRefusedRatherThanLoaded()
        {
            Assert.That(
                delegate { GameSaveFormat.Read("not a save"); },
                Throws.TypeOf<GameSaveException>());
        }

        [Test]
        public void TheSameActionsProduceTheSameRunEveryTime()
        {
            Assert.That(RunOnce(), Is.EqualTo(RunOnce()));
        }

        private static string RunOnce()
        {
            GameState game = GameState.Start(Level4());
            game = game.PlaceValue(0, 1, 4).State;
            game = game.PlaceValue(0, 2, 3).State;
            game = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3).State;
            game = game.SelectTimeline(GameState.RootTimelineId);

            return GameSaveFormat.Write(game);
        }
    }
}
