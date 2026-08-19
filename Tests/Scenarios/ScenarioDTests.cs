using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Level;
using FiveDSudoku.Core.Solver;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;
using FiveDSudoku.Core.Validation;

namespace FiveDSudoku.Tests;

/// <summary>
/// Scenario D — the acceptance test for "time travel is optional".
///
/// The same level is played twice. The first run wins without a single Temporal
/// Move, which is the whole claim. The second run takes a branch anyway, to show
/// that doing so is an addition on top of a solve path that never needed it —
/// the original timeline is untouched and still wins.
/// </summary>
[TestFixture]
public sealed class ScenarioDTests
{
    [Test]
    public void TheLevelIsWinnableWithNoTemporalMovesAtAllAndABranchIsOnlyAnExtra()
    {
        LevelDefinition level = Levels.SolvableFourByFour(temporalBudget: 3, temporalWindow: 4);
        SudokuBoard startingBoard = level.StartingBoard;
        SudokuBoard solution = SudokuSolver.FindFirstSolution(startingBoard)!;

        // --- run one: no time travel whatsoever --------------------------
        GameState plain = Levels.SolveSelectedTimeline(GameState.Start(level));

        Assert.That(plain.Outcome, Is.EqualTo(GameOutcome.Won));
        Assert.That(plain.Timelines, Has.Count.EqualTo(1), "not one branch was made");
        Assert.That(
            plain.RemainingTemporalBudget,
            Is.EqualTo(level.TemporalBudget),
            "not one unit of budget was spent");
        Assert.That(plain.SelectedTimeline.Frontier, Is.EqualTo(solution));

        // --- run two: the same level, with an optional detour ------------
        GameState explored = GameState.Start(level);
        explored = explored.PlaceValue(0, 1, solution[0, 1]).State;

        // A branch that explores a different cell, keeping the puzzle winnable.
        TemporalMoveResult detour = explored.PerformTemporalMove(
            sourceTimelineId: GameState.RootTimelineId,
            sourceTime: 0,
            row: 0,
            column: 2,
            value: solution[0, 2]);

        Assert.That(detour.Succeeded, Is.True, detour.Rejection.ToString());

        GameState withBranch = detour.State;
        Timeline branch = withBranch.GetTimeline(detour.NewTimelineId!.Value);
        Timeline root = withBranch.GetTimeline(GameState.RootTimelineId);

        Assert.That(branch.Status, Is.EqualTo(TimelineStatus.Active), "the detour goes somewhere winnable");
        Assert.That(branch.StateAt(0), Is.EqualTo(startingBoard));
        Assert.That(root.StateAt(0), Is.EqualTo(startingBoard), "the original is untouched");
        Assert.That(root.StateAt(1), Is.EqualTo(startingBoard.WithValue(0, 1, solution[0, 1])));
        Assert.That(withBranch.RemainingTemporalBudget, Is.EqualTo(level.TemporalBudget - 1));

        // --- the original timeline still wins, exactly as before ---------
        Assert.That(
            SudokuSolver.HasUniqueSolution(root.Frontier),
            Is.True,
            "exploring cost the original nothing but a unit of budget");

        // Both frontiers sit at T1, so the root may move once — and then it is the
        // one that is ahead, and has to wait for the detour to catch up.
        Assert.That(withBranch.Present, Is.EqualTo(1));
        Assert.That(branch.FrontierTime, Is.EqualTo(1));

        GameState oneAhead = withBranch.PlaceValue(0, 2, solution[0, 2]).State;

        Assert.That(oneAhead.Present, Is.EqualTo(1), "the detour is now the earliest active frontier");
        Assert.That(
            oneAhead.PlaceValue(0, 3, solution[0, 3]).Rejection,
            Is.EqualTo(MoveRejection.TimelineNotAtPresent));

        // Set the detour aside and the root is free to run to the end on its own.
        GameState parked = oneAhead.DeactivateTimeline(branch.Id).State;

        Assert.That(parked.Present, Is.EqualTo(2), "the root's own frontier is the present again");

        GameState finished = Levels.SolveSelectedTimeline(parked.SelectTimeline(GameState.RootTimelineId));

        Assert.That(finished.Outcome, Is.EqualTo(GameOutcome.Won));
        Assert.That(
            finished.GetTimeline(GameState.RootTimelineId).Frontier,
            Is.EqualTo(solution),
            "the original timeline reached the very same solution");

        // --- and the branch was genuinely optional -----------------------
        Assert.That(
            plain.Outcome,
            Is.EqualTo(finished.Outcome),
            "both runs of the same level are won; the branch changed nothing that mattered");
        Assert.That(plain.RemainingTemporalBudget, Is.GreaterThan(finished.RemainingTemporalBudget));
    }

    [Test]
    public void EveryReleaseCandidateFixtureIsCertifiedSolvableWithoutTemporalMoves()
    {
        // The standing guarantee, asserted for every level meant to ship.
        LevelDefinition[] releaseCandidates =
        {
            Levels.SolvableFourByFour(temporalBudget: 3, temporalWindow: 4),
            Levels.FromText("9x9-unique", BoardSize.NineByNine, Puzzles.Unique9, temporalBudget: 2, temporalWindow: 4),
        };

        foreach (LevelDefinition level in releaseCandidates)
        {
            LevelValidationReport report = LevelValidator.Validate(level);

            Assert.That(
                report.Passed(LevelValidationCheck.SolvableWithoutTemporalMoves),
                Is.True,
                $"{level.Id}: {report[LevelValidationCheck.SolvableWithoutTemporalMoves].Detail}");
            Assert.That(report.IsReleaseCandidate, Is.True, report.ToString());
        }
    }
}
