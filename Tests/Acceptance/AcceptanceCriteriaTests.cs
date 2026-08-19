using System;
using System.Linq;
using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Level;
using FiveDSudoku.Core.Persistence;
using FiveDSudoku.Core.Solver;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;
using FiveDSudoku.Core.Validation;

namespace FiveDSudoku.Tests;

/// <summary>
/// The acceptance criteria for the core prototype, one test each.
///
/// Every claim here has its own dedicated tests elsewhere; what this fixture adds
/// is that the criteria are stated in one place and checked mechanically, so a
/// criterion cannot quietly stop being true while the suite stays green. The
/// tests are thin on purpose — they exercise the real API and assert the claim,
/// they do not re-implement the behaviour behind it.
/// </summary>
[TestFixture]
public sealed class AcceptanceCriteriaTests
{
    // ---- randomized invariant tests pass -----------------------------------

    [Test]
    public void RandomizedInvariantTestsPass()
    {
        Random random = StressSeeds.For(nameof(RandomizedInvariantTestsPass));
        int actions = 0;

        for (int session = 0; session < 15; session++)
        {
            LevelDefinition level = new LevelDefinition(
                "acceptance",
                PuzzleGenerator.RandomSolvablePuzzle(BoardSize.FourByFour, random, cellsToClear: 10),
                temporalBudget: 3,
                temporalWindow: 4,
                maxActiveTimelines: 2,
                finalDepth: 12);

            RandomPlaySession walk = new RandomPlaySession(level, random);
            walk.RunFor(60);

            Assert.That(
                walk.Violations.Select(violation => violation.ToString()),
                Is.Empty,
                $"seed {StressSeeds.Seed}, session {session}");

            actions += walk.ActionsTaken;
        }

        Assert.That(actions, Is.GreaterThan(50));
    }

    // ---- a complete 4x4 level solved with zero Temporal Moves --------------

    [Test]
    public void AtLeastOneComplete4x4LevelIsSolvedWithZeroTemporalMoves()
    {
        LevelDefinition level = Levels.SolvableFourByFour(temporalBudget: 3, temporalWindow: 4);

        GameState won = Levels.SolveSelectedTimeline(GameState.Start(level));

        Assert.That(won.Outcome, Is.EqualTo(GameOutcome.Won));
        Assert.That(won.Timelines, Has.Count.EqualTo(1), "no branch was ever made");
        Assert.That(won.RemainingTemporalBudget, Is.EqualTo(level.TemporalBudget), "no budget was ever spent");
        Assert.That(won.SelectedTimeline.Frontier.IsSolved(), Is.True);
    }

    // ---- a 4x4 scenario demonstrating an optional, useful branch -----------

    [Test]
    public void AtLeastOne4x4ScenarioDemonstratesAnOptionalUsefulBranch()
    {
        LevelValidationReport report = LevelValidator.Validate(
            Levels.SolvableFourByFour(temporalBudget: 3, temporalWindow: 4));

        Assert.That(
            report.Passed(LevelValidationCheck.SolvableWithoutTemporalMoves),
            Is.True,
            "the branch has to be optional");
        Assert.That(
            report.Passed(LevelValidationCheck.AUsefulButOptionalBranchExists),
            Is.True,
            "and there has to be one worth taking");
        Assert.That(
            report.Passed(LevelValidationCheck.OptionalBranchesLeaveTheOriginalIntact),
            Is.True,
            "and taking it must cost the original nothing");
    }

    // ---- an impossible branch classified DEAD immediately ------------------

    [Test]
    public void AtLeastOneLocallyLegalButGloballyImpossibleBranchIsDeadImmediately()
    {
        GameState game = GameState.Start(Levels.SolvableFourByFour(temporalBudget: 2, temporalWindow: 4));
        SudokuBoard start = game.SelectedTimeline.StateAt(0);

        game = game.PlaceValue(0, 1, 4).State;

        Assert.That(start.IsPlacementLegal(0, 1, 3), Is.True, "legal by the local rules");
        Assert.That(SudokuSolver.HasSolution(start.WithValue(0, 1, 3)), Is.False, "and impossible globally");

        TemporalMoveResult trap = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3);

        Assert.That(trap.Succeeded, Is.True, trap.Rejection.ToString());
        Assert.That(
            trap.State.GetTimeline(trap.NewTimelineId!.Value).Status,
            Is.EqualTo(TimelineStatus.Dead),
            "dead on arrival, not later");
    }

    // ---- 9x9 solving on a known unique-solution puzzle ---------------------

    [Test]
    public void NineByNineSolvingWorksOnAKnownUniqueSolutionPuzzle()
    {
        LevelDefinition level = Levels.FromText(
            "9x9-unique", BoardSize.NineByNine, Puzzles.Unique9, temporalBudget: 2, temporalWindow: 4);

        Assert.That(SudokuSolver.HasUniqueSolution(level.StartingBoard), Is.True);

        GameState won = Levels.SolveSelectedTimeline(GameState.Start(level));

        Assert.That(won.Outcome, Is.EqualTo(GameOutcome.Won));
        Assert.That(
            won.SelectedTimeline.Frontier,
            Is.EqualTo(SudokuBoard.Parse(BoardSize.NineByNine, Puzzles.Unique9Solution)));
    }

    // ---- immutable history verified automatically --------------------------

    [Test]
    public void ImmutableHistoryIsVerifiedAutomatically()
    {
        GameState before = GameState.Start(Levels.SolvableFourByFour(temporalBudget: 2, temporalWindow: 4));
        GameState after = before.PlaceValue(0, 1, 4).State;
        after = after.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3).State;

        // It holds forwards...
        Assert.That(GameInvariants.CheckTransition(before, after), Is.Empty);

        // ...and the check that says so is not a rubber stamp.
        Assert.That(
            GameInvariants.CheckTransition(after, before).Select(violation => violation.Invariant),
            Does.Contain(GameInvariant.ImmutableHistory));
    }

    // ---- PRESENT invariants verified automatically -------------------------

    [Test]
    public void PresentInvariantsAreVerifiedAutomatically()
    {
        GameState game = GameState.Start(Levels.TwoSolutionFourByFour(temporalBudget: 2, temporalWindow: 4));
        game = game.PlaceValue(0, 0, 1).State;
        game = game.PlaceValue(0, 1, 2).State;
        game = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 0, 2).State;

        Assert.That(game.Timelines.Count, Is.EqualTo(2), "two frontiers, so the minimum means something");
        Assert.That(game.Present, Is.EqualTo(1));
        Assert.That(GameInvariants.Check(game), Is.Empty);
        Assert.That(
            LevelValidator.Validate(Levels.SolvableFourByFour(temporalBudget: 3, temporalWindow: 4))
                .Passed(LevelValidationCheck.PresentMatchesTheMinimumActiveFrontier),
            Is.True);
    }

    // ---- temporal budget and window rules verified automatically -----------

    [Test]
    public void TemporalBudgetAndWindowRulesAreVerifiedAutomatically()
    {
        GameState oneUnit = GameState.Start(Levels.SolvableFourByFour(temporalBudget: 1, temporalWindow: 16))
            .PlaceValue(0, 1, 4).State;

        TemporalMoveResult spent = oneUnit.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3);

        Assert.That(spent.State.RemainingTemporalBudget, Is.EqualTo(0), "exactly one unit per branch");
        Assert.That(
            spent.State.PerformTemporalMove(GameState.RootTimelineId, 0, 1, 0, 3).Rejection,
            Is.EqualTo(TemporalMoveRejection.NoTemporalBudget));

        GameState narrow = GameState.Start(Levels.SolvableFourByFour(temporalBudget: 3, temporalWindow: 1));
        narrow = narrow.PlaceValue(0, 1, 4).State;
        narrow = narrow.PlaceValue(0, 2, 3).State;
        narrow = narrow.PlaceValue(0, 3, 2).State;

        Assert.That(
            narrow.ValidateTemporalMove(GameState.RootTimelineId, 0, 1, 0, 3),
            Is.EqualTo(TemporalMoveRejection.OutsideTemporalWindow),
            "the window is never silently widened");

        Assert.That(
            LevelValidator.Validate(Levels.SolvableFourByFour(temporalBudget: 3, temporalWindow: 4))
                .Passed(LevelValidationCheck.TemporalBudgetNeverGoesNegative),
            Is.True);
    }

    // ---- no normal Undo ----------------------------------------------------

    [Test]
    public void NoNormalUndoExistsInGameplayLogic()
    {
        // The only way back to an earlier decision costs a unit of budget and
        // leaves the original where it was.
        GameState before = GameState.Start(Levels.SolvableFourByFour(temporalBudget: 2, temporalWindow: 4));
        GameState after = before.PlaceValue(0, 1, 4).State;

        Assert.That(after.SelectedTimeline.StateCount, Is.EqualTo(2));
        Assert.That(before.SelectedTimeline.StateCount, Is.EqualTo(1), "the earlier state still exists...");

        TemporalMoveResult back = after.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 2, 3);

        Assert.That(back.Succeeded, Is.True, back.Rejection.ToString());
        Assert.That(back.State.RemainingTemporalBudget, Is.EqualTo(1), "...and revisiting it costs");
        Assert.That(
            back.State.GetTimeline(GameState.RootTimelineId).StateCount,
            Is.EqualTo(2),
            "...and takes nothing away from the original");
    }

    // ---- the save carries everything needed to reconstruct a run -----------

    [Test]
    public void ARunSurvivesBeingWrittenDownAndReadBack()
    {
        GameState game = GameState.Start(Levels.SolvableFourByFour(temporalBudget: 3, temporalWindow: 4));
        game = game.PlaceValue(0, 1, 4).State;
        game = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3).State;

        Assert.That(
            GameSaveFormat.Write(GameSaveFormat.Read(GameSaveFormat.Write(game))),
            Is.EqualTo(GameSaveFormat.Write(game)));
    }
}
