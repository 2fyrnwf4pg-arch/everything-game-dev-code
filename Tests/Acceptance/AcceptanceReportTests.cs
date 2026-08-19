using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Level;
using FiveDSudoku.Core.Solver;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Validation;

namespace FiveDSudoku.Tests;

/// <summary>
/// Prints the final report for the core prototype.
///
/// Every number in it is measured while the report is being written, not copied
/// from a previous run, so the report cannot drift away from what the code
/// actually does. Run it on its own to read it:
/// <c>dotnet test --filter Name=PrintTheFinalReport --logger "console;verbosity=detailed"</c>
/// </summary>
[TestFixture]
public sealed class AcceptanceReportTests
{
    private const int ReportSessions = 25;
    private const int ReportActions = 60;

    [Test]
    public void PrintTheFinalReport()
    {
        StringBuilder report = new StringBuilder();

        report.Append('\n').Append("5D SUDOKU — CORE PROTOTYPE, FINAL REPORT").Append('\n');
        report.Append(new string('=', 60)).Append('\n').Append('\n');

        AppendTestCounts(report);
        AppendRandomizedRun(report);
        AppendFourByFour(report);
        AppendNineByNine(report);
        AppendPerformanceNotes(report);
        AppendDesignAssumptions(report);

        TestContext.Out.WriteLine(report.ToString());

        // The report is only worth printing if what it reports on is sound.
        Assert.That(
            LevelValidator.Validate(Levels.SolvableFourByFour(temporalBudget: 3, temporalWindow: 4))
                .IsReleaseCandidate,
            Is.True);
    }

    private static void AppendTestCounts(StringBuilder report)
    {
        Assembly tests = typeof(AcceptanceReportTests).Assembly;
        int fixtures = 0;
        int cases = 0;

        foreach (Type type in tests.GetTypes())
        {
            if (type.GetCustomAttributes().All(attribute => attribute.GetType().Name != "TestFixtureAttribute"))
            {
                continue;
            }

            fixtures++;

            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                object[] attributes = method.GetCustomAttributes(inherit: false);
                int testCases = attributes.Count(attribute => attribute.GetType().Name == "TestCaseAttribute");
                int sources = attributes.Count(attribute => attribute.GetType().Name == "TestCaseSourceAttribute");
                bool plain = attributes.Any(attribute => attribute.GetType().Name == "TestAttribute");

                // Cases fed from a source are counted as one here; the runner's own
                // total is the authority, this is only the shape of the suite.
                cases += testCases + sources + (plain ? 1 : 0);
            }
        }

        report.Append("TESTS").Append('\n');
        report.Append($"  {fixtures} fixtures, {cases} declared tests").Append('\n');
        report.Append("  (the runner's own total is authoritative; parameterised sources expand further)").Append('\n');
        report.Append('\n');
    }

    private static void AppendRandomizedRun(StringBuilder report)
    {
        Random random = StressSeeds.For(nameof(PrintTheFinalReport));
        int actions = 0;
        int branches = 0;
        int parked = 0;
        int revived = 0;
        int violations = 0;
        int won = 0;

        Stopwatch clock = Stopwatch.StartNew();

        for (int session = 0; session < ReportSessions; session++)
        {
            LevelDefinition level = new LevelDefinition(
                "report",
                PuzzleGenerator.RandomSolvablePuzzle(BoardSize.FourByFour, random, cellsToClear: 8 + random.Next(6)),
                temporalBudget: random.Next(4),
                temporalWindow: random.Next(5),
                maxActiveTimelines: 1 + random.Next(3),
                finalDepth: 12);

            RandomPlaySession walk = new RandomPlaySession(level, random);
            walk.RunFor(ReportActions);

            actions += walk.ActionsTaken;
            branches += walk.BranchesMade;
            parked += walk.Deactivations;
            revived += walk.Activations;
            violations += walk.Violations.Count;
            won += walk.State.Outcome == GameOutcome.Won ? 1 : 0;
        }

        clock.Stop();

        report.Append("RANDOMIZED RUNS").Append('\n');
        report.Append($"  seed {StressSeeds.Seed} (override with FIVED_SUDOKU_SEED)").Append('\n');
        report.Append($"  {ReportSessions} sessions, {actions} state-producing actions in {clock.ElapsedMilliseconds} ms")
            .Append('\n');
        report.Append($"  {branches} branches, {parked} timelines parked, {revived} brought back, {won} runs won")
            .Append('\n');
        report.Append($"  invariant violations: {violations}").Append('\n');
        report.Append('\n');

        Assert.That(violations, Is.Zero, "the report must not be printed over a broken run");
    }

    private static void AppendFourByFour(StringBuilder report)
    {
        LevelDefinition level = Levels.SolvableFourByFour(temporalBudget: 3, temporalWindow: 4);

        Stopwatch clock = Stopwatch.StartNew();
        GameState won = Levels.SolveSelectedTimeline(GameState.Start(level));
        clock.Stop();

        LevelValidationReport validation = LevelValidator.Validate(level);

        report.Append("4x4").Append('\n');
        report.Append($"  solved in {won.SelectedTimeline.FrontierTime} ordinary placements, ")
            .Append($"{clock.Elapsed.TotalMilliseconds:F1} ms").Append('\n');
        report.Append($"  outcome {won.Outcome}, budget {won.RemainingTemporalBudget}/{level.TemporalBudget} left")
            .Append('\n');
        report.Append($"  validation: {(validation.IsReleaseCandidate ? "all 10 checks pass" : "FAILED")}")
            .Append('\n');
        report.Append('\n');

        Assert.That(won.Outcome, Is.EqualTo(GameOutcome.Won));
    }

    private static void AppendNineByNine(StringBuilder report)
    {
        LevelDefinition level = Levels.FromText(
            "9x9-unique", BoardSize.NineByNine, Puzzles.Unique9, temporalBudget: 2, temporalWindow: 4);

        Stopwatch solverClock = Stopwatch.StartNew();
        bool unique = SudokuSolver.HasUniqueSolution(level.StartingBoard);
        solverClock.Stop();

        Stopwatch playClock = Stopwatch.StartNew();
        GameState won = Levels.SolveSelectedTimeline(GameState.Start(level));
        playClock.Stop();

        Stopwatch validationClock = Stopwatch.StartNew();
        LevelValidationReport validation = LevelValidator.Validate(level);
        validationClock.Stop();

        report.Append("9x9").Append('\n');
        report.Append($"  uniqueness confirmed in {solverClock.Elapsed.TotalMilliseconds:F1} ms: {unique}")
            .Append('\n');
        report.Append($"  solved in {won.SelectedTimeline.FrontierTime} ordinary placements, ")
            .Append($"{playClock.Elapsed.TotalMilliseconds:F1} ms").Append('\n');
        report.Append($"  validation: {(validation.IsReleaseCandidate ? "all 10 checks pass" : "FAILED")}")
            .Append($" in {validationClock.Elapsed.TotalMilliseconds:F1} ms").Append('\n');
        report.Append('\n');

        Assert.That(won.Outcome, Is.EqualTo(GameOutcome.Won));
        Assert.That(unique, Is.True);
    }

    private static void AppendPerformanceNotes(StringBuilder report)
    {
        report.Append("KNOWN PERFORMANCE LIMITATIONS").Append('\n');
        report.Append("  - Proving a nearly empty grid has no completion is expensive. It only").Append('\n');
        report.Append("    arises for boards that are already in contradiction, which the engine").Append('\n');
        report.Append("    rejects before searching, but a caller reaching past that would feel it.").Append('\n');
        report.Append("  - Solution counting always takes an explicit limit. There is no unbounded").Append('\n');
        report.Append("    overload: an empty 9x9 grid has more completions than anything could").Append('\n');
        report.Append("    enumerate, and callers only ever need none / one / more than one.").Append('\n');
        report.Append("  - Branching and activating ask the solver, so both cost more at 9x9 than").Append('\n');
        report.Append("    at 4x4. The stress and validation passes are bounded for that reason.").Append('\n');
        report.Append("  - Every state of every timeline is kept. That is the point, but memory").Append('\n');
        report.Append("    grows with the length of a run rather than with the size of the board.").Append('\n');
        report.Append('\n');
    }

    private static void AppendDesignAssumptions(StringBuilder report)
    {
        report.Append("REMAINING DESIGN ASSUMPTIONS").Append('\n');
        report.Append("  Recorded in PHASES.md as D1-D4, each with named tests:").Append('\n');
        report.Append("  - D1 only a won run is terminal; a lost one can still be branched out of.").Append('\n');
        report.Append("  - D2 a timeline with no legal placement left is dead. Ordinary play asks").Append('\n');
        report.Append("    that locally and does not run the solver, so a doomed but playable line").Append('\n');
        report.Append("    stays active and the player finds out by playing.").Append('\n');
        report.Append("  - D3 with no present, a branch measures its window from the source").Append('\n');
        report.Append("    timeline's own frontier.").Append('\n');
        report.Append("  - D4 deactivation parks an active timeline; bringing it back classifies it.").Append('\n');
        report.Append('\n');
        report.Append("  Two states the rules describe but play cannot reach:").Append('\n');
        report.Append("  - A branch is never born SOLVED, and an inactive timeline never holds a").Append('\n');
        report.Append("    solved board, because a branch's board can only be complete if the").Append('\n');
        report.Append("    timeline it came from already was — and that one has already won.").Append('\n');
        report.Append("    The rules are implemented and tested directly on boards regardless.").Append('\n');
        report.Append('\n');
    }
}
