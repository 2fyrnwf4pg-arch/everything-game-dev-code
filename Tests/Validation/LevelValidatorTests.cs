using System;
using System.Linq;
using FiveDSudoku.Core.Level;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Validation;

namespace FiveDSudoku.Tests;

/// <summary>
/// The validation layer: every check with a level that passes it and, where the
/// check can be made to fail at all, a level that fails it.
/// </summary>
[TestFixture]
public sealed class LevelValidatorTests
{
    private static LevelValidationReport ValidateGoodFourByFour() =>
        LevelValidator.Validate(Levels.SolvableFourByFour(temporalBudget: 3, temporalWindow: 4));

    // ---- the level that should sail through -------------------------------

    [Test]
    public void AGenuinelyValidLevelPassesEveryCheck()
    {
        LevelValidationReport report = ValidateGoodFourByFour();

        Assert.That(report.IsReleaseCandidate, Is.True, report.ToString());
        Assert.That(report.Failures, Is.Empty);
        Assert.That(
            report.Findings.Select(finding => finding.Check),
            Is.EquivalentTo(Enum.GetValues<LevelValidationCheck>()),
            "every check must be reported on, not just the ones that failed");
    }

    [Test]
    public void AValidNineByNineLevelPassesEveryCheck()
    {
        LevelValidationReport report = LevelValidator.Validate(
            Levels.FromText("9x9-unique", BoardSize.NineByNine, Puzzles.Unique9, temporalBudget: 2, temporalWindow: 4));

        Assert.That(report.IsReleaseCandidate, Is.True, report.ToString());
    }

    [Test]
    public void ValidationIsDeterministic()
    {
        LevelDefinition level = Levels.SolvableFourByFour();

        Assert.That(
            LevelValidator.Validate(level).ToString(),
            Is.EqualTo(LevelValidator.Validate(level).ToString()));
    }

    [Test]
    public void ValidationNeedsALevel()
    {
        Assert.That(() => LevelValidator.Validate(null!), Throws.ArgumentNullException);
    }

    // ---- check 1: the puzzle is valid -------------------------------------

    [Test]
    public void Check1RejectsAPuzzleThatRepeatsAValue()
    {
        LevelValidationReport report = LevelValidator.Validate(
            Levels.FromText("broken", BoardSize.FourByFour, Puzzles.Invalid4));

        Assert.That(report.Passed(LevelValidationCheck.StartingPuzzleIsValid), Is.False);
        Assert.That(report.IsReleaseCandidate, Is.False);
    }

    [Test]
    public void Check1AcceptsAValidPuzzle()
    {
        Assert.That(ValidateGoodFourByFour().Passed(LevelValidationCheck.StartingPuzzleIsValid), Is.True);
    }

    // ---- check 2: the puzzle has a solution -------------------------------

    [Test]
    public void Check2RejectsAPuzzleWithNoCompletion()
    {
        LevelValidationReport report = LevelValidator.Validate(
            Levels.FromText("no-solution", BoardSize.FourByFour, Puzzles.Zero4));

        Assert.That(report.Passed(LevelValidationCheck.StartingPuzzleIsValid), Is.True, "it breaks no rule");
        Assert.That(report.Passed(LevelValidationCheck.StartingPuzzleHasASolution), Is.False);
    }

    [Test]
    public void Check2AcceptsASolvablePuzzle()
    {
        Assert.That(ValidateGoodFourByFour().Passed(LevelValidationCheck.StartingPuzzleHasASolution), Is.True);
    }

    // ---- check 3: exactly one solution ------------------------------------

    [Test]
    public void Check3RejectsAPuzzleWithMoreThanOneSolution()
    {
        LevelValidationReport report = LevelValidator.Validate(Levels.TwoSolutionFourByFour());

        Assert.That(report.Passed(LevelValidationCheck.StartingPuzzleHasASolution), Is.True);
        Assert.That(report.Passed(LevelValidationCheck.StartingPuzzleHasExactlyOneSolution), Is.False);
        Assert.That(report.IsReleaseCandidate, Is.False, "playable, but not shippable");
        Assert.That(
            report.Passed(LevelValidationCheck.SolvableWithoutTemporalMoves),
            Is.True,
            "it is still solvable without time travel");
    }

    [Test]
    public void Check3AcceptsAUniquelySolvablePuzzle()
    {
        Assert.That(
            ValidateGoodFourByFour().Passed(LevelValidationCheck.StartingPuzzleHasExactlyOneSolution),
            Is.True);
    }

    // ---- check 4: solvable with zero Temporal Moves ------------------------

    [Test]
    public void Check4RejectsALevelThereIsNoWayToWin()
    {
        LevelValidationReport report = LevelValidator.Validate(
            Levels.FromText("stuck", BoardSize.FourByFour, Puzzles.Blocked4));

        Assert.That(report.Passed(LevelValidationCheck.SolvableWithoutTemporalMoves), Is.False);
    }

    [Test]
    public void Check4AcceptsALevelThatCanBeWonWithoutTimeTravel()
    {
        LevelValidationReport report = ValidateGoodFourByFour();

        Assert.That(report.Passed(LevelValidationCheck.SolvableWithoutTemporalMoves), Is.True);
        Assert.That(
            report[LevelValidationCheck.SolvableWithoutTemporalMoves].Detail,
            Does.Contain("untouched"));
    }

    [Test]
    public void EveryLevelWithASolutionIsSolvableWithoutTemporalMoves()
    {
        // Not a coincidence but a consequence: any partial assignment that agrees
        // with a complete solution breaks no constraint, so the solution's values
        // can always be placed one by one, in any order, by ordinary play. Time
        // travel therefore cannot be needed to make progress in any level that has
        // a solution at all.
        LevelDefinition[] levels =
        {
            Levels.SolvableFourByFour(),
            Levels.TwoSolutionFourByFour(),
            Levels.FromText("sparse", BoardSize.FourByFour, "1... .... .... ...."),
            Levels.FromText("9x9-unique", BoardSize.NineByNine, Puzzles.Unique9),
        };

        foreach (LevelDefinition level in levels)
        {
            LevelValidationReport report = LevelValidator.Validate(level);

            Assert.That(
                report.Passed(LevelValidationCheck.StartingPuzzleHasASolution),
                Is.True,
                level.Id);
            Assert.That(
                report.Passed(LevelValidationCheck.SolvableWithoutTemporalMoves),
                Is.True,
                $"{level.Id}: {report[LevelValidationCheck.SolvableWithoutTemporalMoves].Detail}");
        }
    }

    // ---- checks 5-7: what branching does -----------------------------------

    [TestCase(0, 4, TestName = "no temporal budget")]
    [TestCase(3, 0, TestName = "no temporal window")]
    public void Checks5To7RejectALevelThatOffersNoBranchAtAll(int temporalBudget, int temporalWindow)
    {
        LevelValidationReport report = LevelValidator.Validate(
            Levels.SolvableFourByFour(temporalBudget: temporalBudget, temporalWindow: temporalWindow));

        Assert.That(report.Passed(LevelValidationCheck.SolvableWithoutTemporalMoves), Is.True);
        Assert.That(report.Passed(LevelValidationCheck.OptionalBranchesLeaveTheOriginalIntact), Is.False);
        Assert.That(report.Passed(LevelValidationCheck.ImpossibleBranchesDieImmediately), Is.False);
        Assert.That(report.Passed(LevelValidationCheck.AUsefulButOptionalBranchExists), Is.False);
    }

    [Test]
    public void Check5AcceptsALevelWhereBranchingLeavesTheOriginalAlone()
    {
        LevelValidationReport report = ValidateGoodFourByFour();

        Assert.That(report.Passed(LevelValidationCheck.OptionalBranchesLeaveTheOriginalIntact), Is.True);
        Assert.That(
            report[LevelValidationCheck.OptionalBranchesLeaveTheOriginalIntact].Detail,
            Does.Contain("untouched"));
    }

    [Test]
    public void Check6AcceptsALevelWhereAnImpossibleBranchDiesAtOnce()
    {
        LevelValidationReport report = ValidateGoodFourByFour();

        Assert.That(report.Passed(LevelValidationCheck.ImpossibleBranchesDieImmediately), Is.True);
        Assert.That(
            report[LevelValidationCheck.ImpossibleBranchesDieImmediately].Detail,
            Does.Contain("dead"));
    }

    [Test]
    public void Check7AcceptsALevelThatOffersAWorthwhileBranch()
    {
        LevelValidationReport report = ValidateGoodFourByFour();

        Assert.That(report.Passed(LevelValidationCheck.AUsefulButOptionalBranchExists), Is.True);
        Assert.That(
            report[LevelValidationCheck.AUsefulButOptionalBranchExists].Detail,
            Does.Contain("winnable"));
    }

    // ---- checks 8-10: invariants along the way -----------------------------

    [Test]
    public void Checks8To10HoldForEveryLevelValidated()
    {
        LevelDefinition[] levels =
        {
            Levels.SolvableFourByFour(),
            Levels.TwoSolutionFourByFour(),
            Levels.FromText("no-solution", BoardSize.FourByFour, Puzzles.Zero4),
            Levels.FromText("stuck", BoardSize.FourByFour, Puzzles.Blocked4),
            Levels.FromText("broken", BoardSize.FourByFour, Puzzles.Invalid4),
            Levels.SolvableFourByFour(temporalBudget: 0),
        };

        foreach (LevelDefinition level in levels)
        {
            LevelValidationReport report = LevelValidator.Validate(level);

            Assert.That(report.Passed(LevelValidationCheck.ImmutableHistoryHolds), Is.True, level.Id);
            Assert.That(report.Passed(LevelValidationCheck.TemporalBudgetNeverGoesNegative), Is.True, level.Id);
            Assert.That(
                report.Passed(LevelValidationCheck.PresentMatchesTheMinimumActiveFrontier),
                Is.True,
                level.Id);
        }
    }

    // ---- the report itself -------------------------------------------------

    [Test]
    public void TheReportNamesTheLevelAndListsEveryCheck()
    {
        LevelValidationReport report = ValidateGoodFourByFour();

        Assert.That(report.Level.Id, Is.EqualTo("4x4-unique"));
        Assert.That(report.Findings, Has.Count.EqualTo(10));
        Assert.That(report.ToString(), Does.Contain("release candidate"));
    }

    [Test]
    public void AskingForACheckThatDoesNotExistIsRejected()
    {
        Assert.That(
            () => ValidateGoodFourByFour()[(LevelValidationCheck)99],
            Throws.InstanceOf<ArgumentOutOfRangeException>());
    }
}
