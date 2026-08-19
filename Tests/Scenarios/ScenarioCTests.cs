using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Level;
using FiveDSudoku.Core.Solver;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;

namespace FiveDSudoku.Tests;

/// <summary>
/// Scenario C — the acceptance test for immediate classification.
///
/// A puzzle offers an alternative value that breaks no row, column or box rule
/// and yet leaves a grid with no completion at all. Branching on it must produce
/// a timeline that is dead the moment it exists, while the timeline it came from
/// carries on untouched and still solvable.
/// </summary>
[TestFixture]
public sealed class ScenarioCTests
{
    [Test]
    public void ALocallyLegalButImpossibleBranchDiesAtBirthAndLeavesTheParentIntact()
    {
        // --- arrange: a uniquely solvable 4x4 ----------------------------
        LevelDefinition level = Levels.SolvableFourByFour(temporalBudget: 2, temporalWindow: 4);
        SudokuBoard startingBoard = level.StartingBoard;

        Assert.That(SudokuSolver.HasUniqueSolution(startingBoard), Is.True);

        // r0c1 admits two values by the local rules. One of them completes the
        // puzzle; the other is a trap that no amount of later play can recover.
        Assert.That(startingBoard.IsPlacementLegal(0, 1, 4), Is.True);
        Assert.That(startingBoard.IsPlacementLegal(0, 1, 3), Is.True);
        Assert.That(SudokuSolver.CountSolutions(startingBoard.WithValue(0, 1, 4), 2), Is.EqualTo(1));
        Assert.That(SudokuSolver.CountSolutions(startingBoard.WithValue(0, 1, 3), 2), Is.EqualTo(0));

        // Play the good value so that T0 becomes a historical state worth revisiting.
        GameState game = GameState.Start(level);
        game = game.PlaceValue(0, 1, 4).State;

        Assert.That(game.Present, Is.EqualTo(1));
        Assert.That(game.RemainingTemporalBudget, Is.EqualTo(2));

        // --- act: branch off T0 with the trap value ----------------------
        TemporalMoveResult result = game.PerformTemporalMove(
            sourceTimelineId: GameState.RootTimelineId,
            sourceTime: 0,
            row: 0,
            column: 1,
            value: 3);

        Assert.That(result.Succeeded, Is.True, result.Rejection.ToString());

        GameState branched = result.State;
        Timeline child = branched.GetTimeline(result.NewTimelineId!.Value);
        Timeline parent = branched.GetTimeline(GameState.RootTimelineId);

        // --- assert: the child is dead immediately -----------------------
        Assert.That(child.Status, Is.EqualTo(TimelineStatus.Dead));
        Assert.That(child.Frontier.IsValid(), Is.True, "the branch breaks no Sudoku constraint");
        Assert.That(child.Frontier.IsComplete, Is.False);
        Assert.That(SudokuSolver.HasSolution(child.Frontier), Is.False, "and yet it can never be completed");
        Assert.That(child.IsCapableOfFurtherPlay, Is.False);
        Assert.That(child.ParentId, Is.EqualTo(GameState.RootTimelineId));
        Assert.That(child.BranchTime, Is.EqualTo(0));
        Assert.That(child.StateAt(0), Is.EqualTo(startingBoard), "T0 was carried over unchanged");

        // --- assert: the parent is untouched and still solvable ----------
        Assert.That(parent.Status, Is.EqualTo(TimelineStatus.Active));
        Assert.That(parent.FrontierTime, Is.EqualTo(1));
        Assert.That(parent.StateCount, Is.EqualTo(2));
        Assert.That(parent.StateAt(0), Is.EqualTo(startingBoard));
        Assert.That(SudokuSolver.HasUniqueSolution(parent.Frontier), Is.True);
        Assert.That(parent.IsCapableOfFurtherPlay, Is.True);

        // --- assert: the run goes on, one unit of budget lighter ---------
        Assert.That(branched.Outcome, Is.EqualTo(GameOutcome.InProgress), "one dead branch does not end the run");
        Assert.That(branched.RemainingTemporalBudget, Is.EqualTo(1));
        Assert.That(branched.Present, Is.EqualTo(1), "a dead branch does not move the present");

        // --- assert: the parent can still be played to victory -----------
        GameState finished = Levels.SolveSelectedTimeline(branched);

        Assert.That(finished.Outcome, Is.EqualTo(GameOutcome.Won));
        Assert.That(finished.GetTimeline(child.Id).Status, Is.EqualTo(TimelineStatus.Dead));
        Assert.That(finished.RemainingTemporalBudget, Is.EqualTo(1), "solving costs no further budget");
    }
}
