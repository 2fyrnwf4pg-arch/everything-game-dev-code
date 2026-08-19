using System;
using System.Linq;
using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Level;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;
using FiveDSudoku.Core.Validation;

namespace FiveDSudoku.Tests;

/// <summary>
/// Random walks through whole runs, checking every invariant after every action.
///
/// Bounded and seeded rather than exhaustive: a fixed default seed keeps the
/// suite meaningful to run on every change, and <c>FIVED_SUDOKU_SEED</c> sweeps
/// further when there is time for it.
/// </summary>
[TestFixture]
public sealed class StressTests
{
    private const int FourByFourSessions = 40;
    private const int FourByFourActions = 60;
    private const int NineByNineSessions = 3;
    private const int NineByNineActions = 45;

    private static LevelDefinition RandomFourByFourLevel(Random random) =>
        new LevelDefinition(
            $"stress-4x4",
            PuzzleGenerator.RandomSolvablePuzzle(BoardSize.FourByFour, random, cellsToClear: 8 + random.Next(6)),
            temporalBudget: random.Next(4),
            temporalWindow: random.Next(5),
            maxActiveTimelines: 1 + random.Next(3),
            finalDepth: 12);

    private static void AssertNoViolations(RandomPlaySession session)
    {
        if (session.Violations.Count == 0)
        {
            return;
        }

        Assert.Fail(
            $"seed {StressSeeds.Seed}: {session.Violations.Count} invariant violation(s).\n" +
            string.Join("\n", session.Violations.Select(violation => "  " + violation)) +
            "\nactions:\n  " + string.Join("\n  ", session.Log));
    }

    // ---- the walks ---------------------------------------------------------

    [Test]
    public void FourByFourRunsHoldEveryInvariantUnderRandomPlay()
    {
        Random random = StressSeeds.For(nameof(FourByFourRunsHoldEveryInvariantUnderRandomPlay));
        int totalActions = 0;
        int totalBranches = 0;
        int totalActivations = 0;
        int totalDeactivations = 0;

        for (int session = 0; session < FourByFourSessions; session++)
        {
            RandomPlaySession walk = new RandomPlaySession(RandomFourByFourLevel(random), random);
            walk.RunFor(FourByFourActions);

            AssertNoViolations(walk);

            totalActions += walk.ActionsTaken;
            totalBranches += walk.BranchesMade;
            totalActivations += walk.Activations;
            totalDeactivations += walk.Deactivations;
        }

        Assert.That(totalActions, Is.GreaterThan(200), "the walks must actually get somewhere");
        Assert.That(totalBranches, Is.GreaterThan(0), "at least some walks must have branched");
        Assert.That(totalDeactivations, Is.GreaterThan(0), "at least some walks must have parked a timeline");
        Assert.That(totalActivations, Is.GreaterThan(0), "and some must have brought one back");

        TestContext.Out.WriteLine(
            $"seed {StressSeeds.Seed}: {FourByFourSessions} sessions, {totalActions} actions, " +
            $"{totalBranches} branches, {totalDeactivations} parked, {totalActivations} reactivated, " +
            "0 violations.");
    }

    [Test]
    public void NineByNineRunsHoldEveryInvariantUnderRandomPlay()
    {
        // Deliberately fewer and shorter: every branch and every activation asks
        // the solver, and that costs far more at 9x9 than at 4x4.
        Random random = StressSeeds.For(nameof(NineByNineRunsHoldEveryInvariantUnderRandomPlay));
        int totalActions = 0;

        for (int session = 0; session < NineByNineSessions; session++)
        {
            LevelDefinition level = new LevelDefinition(
                "stress-9x9",
                PuzzleGenerator.RandomSolvablePuzzle(BoardSize.NineByNine, random, cellsToClear: 20),
                temporalBudget: 2,
                temporalWindow: 4,
                maxActiveTimelines: 2,
                finalDepth: 60);

            RandomPlaySession walk = new RandomPlaySession(level, random);
            walk.RunFor(NineByNineActions);

            AssertNoViolations(walk);

            totalActions += walk.ActionsTaken;
        }

        Assert.That(totalActions, Is.GreaterThan(50));

        TestContext.Out.WriteLine(
            $"seed {StressSeeds.Seed}: {NineByNineSessions} 9x9 sessions, {totalActions} actions, 0 violations.");
    }

    [Test]
    public void ARunThatIsPlayedToTheEndEitherWinsOrRunsOutOfMoves()
    {
        Random random = StressSeeds.For(nameof(ARunThatIsPlayedToTheEndEitherWinsOrRunsOutOfMoves));

        for (int session = 0; session < 20; session++)
        {
            RandomPlaySession walk = new RandomPlaySession(RandomFourByFourLevel(random), random);
            walk.RunFor(200);

            AssertNoViolations(walk);

            // A walk that stopped short of its budget stopped for a reason the
            // rules gave it, never because it quietly gave up mid-run.
            if (walk.State.Outcome == GameOutcome.InProgress)
            {
                bool anythingLeft = walk.State.Timelines.Any(timeline => timeline.IsCapableOfFurtherPlay);

                Assert.That(anythingLeft, Is.True, "an in-progress run must have somewhere left to go");
            }
        }
    }

    // ---- determinism -------------------------------------------------------

    [Test]
    public void TheSameSeedAndTheSameLevelProduceTheSameRunTwice()
    {
        LevelDefinition level = new LevelDefinition(
            "replay",
            PuzzleGenerator.RandomSolvablePuzzle(BoardSize.FourByFour, new Random(4242), cellsToClear: 10),
            temporalBudget: 3,
            temporalWindow: 4,
            maxActiveTimelines: 2,
            finalDepth: 12);

        RandomPlaySession first = new RandomPlaySession(level, new Random(1234));
        first.RunFor(80);

        RandomPlaySession second = new RandomPlaySession(level, new Random(1234));
        second.RunFor(80);

        Assert.That(second.Log, Is.EqualTo(first.Log), "the same seed must take the same actions");
        Assert.That(second.Describe(), Is.EqualTo(first.Describe()), "and reach the same state");
        Assert.That(first.Violations, Is.Empty);
        Assert.That(second.Violations, Is.Empty);
    }

    [Test]
    public void DifferentSeedsExploreDifferentRuns()
    {
        LevelDefinition level = new LevelDefinition(
            "variety",
            PuzzleGenerator.RandomSolvablePuzzle(BoardSize.FourByFour, new Random(99), cellsToClear: 10),
            temporalBudget: 3,
            temporalWindow: 4,
            maxActiveTimelines: 2,
            finalDepth: 12);

        RandomPlaySession first = new RandomPlaySession(level, new Random(1));
        RandomPlaySession second = new RandomPlaySession(level, new Random(2));

        first.RunFor(60);
        second.RunFor(60);

        Assert.That(second.Log, Is.Not.EqualTo(first.Log));
    }

    [Test]
    public void TheSeedIsFixedByDefaultAndOverridableFromTheEnvironment()
    {
        Assert.That(StressSeeds.SeedVariable, Is.EqualTo("FIVED_SUDOKU_SEED"));
        Assert.That(
            Environment.GetEnvironmentVariable(StressSeeds.SeedVariable) is null
                ? StressSeeds.Seed
                : StressSeeds.DefaultSeed,
            Is.EqualTo(StressSeeds.DefaultSeed));
    }

    [Test]
    public void EachTestDrawsFromItsOwnStreamSoOrderDoesNotMatter()
    {
        int firstDrawFromA = StressSeeds.For("a").Next();
        int secondDrawFromA = StressSeeds.For("a").Next();
        int firstDrawFromB = StressSeeds.For("b").Next();

        Assert.That(secondDrawFromA, Is.EqualTo(firstDrawFromA));
        Assert.That(firstDrawFromB, Is.Not.EqualTo(firstDrawFromA));
    }

    // ---- the generator itself ---------------------------------------------

    [Test]
    public void GeneratedSolutionsAreRealSolutions()
    {
        Random random = StressSeeds.For(nameof(GeneratedSolutionsAreRealSolutions));

        for (int index = 0; index < 25; index++)
        {
            Assert.That(PuzzleGenerator.RandomSolution(BoardSize.FourByFour, random).IsSolved(), Is.True);
        }

        for (int index = 0; index < 5; index++)
        {
            Assert.That(PuzzleGenerator.RandomSolution(BoardSize.NineByNine, random).IsSolved(), Is.True);
        }
    }

    [Test]
    public void GeneratedPuzzlesAreAlwaysCompletable()
    {
        Random random = StressSeeds.For(nameof(GeneratedPuzzlesAreAlwaysCompletable));

        for (int index = 0; index < 25; index++)
        {
            SudokuBoard puzzle = PuzzleGenerator.RandomSolvablePuzzle(
                BoardSize.FourByFour, random, cellsToClear: 12);

            Assert.That(puzzle.IsValid(), Is.True);
            Assert.That(TimelineClassifier.ClassifyBoard(puzzle), Is.Not.EqualTo(TimelineStatus.Dead));
        }
    }

    [Test]
    public void GeneratedUniquePuzzlesReallyHaveOneSolution()
    {
        Random random = StressSeeds.For(nameof(GeneratedUniquePuzzlesReallyHaveOneSolution));

        for (int index = 0; index < 10; index++)
        {
            SudokuBoard puzzle = PuzzleGenerator.RandomPuzzleWithUniqueSolution(BoardSize.FourByFour, random);
            LevelValidationReport report = LevelValidator.Validate(
                new LevelDefinition($"generated-{index}", puzzle, 3, 4, 2, 12));

            Assert.That(
                report.Passed(LevelValidationCheck.StartingPuzzleHasExactlyOneSolution),
                Is.True,
                report.ToString());
            Assert.That(
                report.Passed(LevelValidationCheck.SolvableWithoutTemporalMoves),
                Is.True,
                report.ToString());
        }
    }
}
