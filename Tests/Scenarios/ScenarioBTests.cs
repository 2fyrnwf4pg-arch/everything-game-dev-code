using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Level;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;

namespace FiveDSudoku.Tests;

/// <summary>
/// Scenario B — the acceptance test for branching into history.
///
/// Make progress on the root, branch off a state that is already behind it, and
/// confirm that the parent's history is exactly as it was, that the present has
/// moved backwards to the branch, and that playing the branch leaves two
/// genuinely separate timelines with their own untouched histories.
/// </summary>
[TestFixture]
public sealed class ScenarioBTests
{
    [Test]
    public void BranchingIntoHistoryLeavesTheParentIntactAndPullsThePresentBack()
    {
        // --- arrange: a 4x4 with two solutions, so the branch stays playable ---
        LevelDefinition level = Levels.TwoSolutionFourByFour(temporalBudget: 2, temporalWindow: 4);
        SudokuBoard startingBoard = level.StartingBoard;

        GameState game = GameState.Start(level);

        // --- act: make progress on the root ------------------------------
        game = game.PlaceValue(0, 0, 1).State;
        SudokuBoard rootAtT1 = game.SelectedTimeline.Frontier;

        game = game.PlaceValue(0, 1, 2).State;
        SudokuBoard rootAtT2 = game.SelectedTimeline.Frontier;

        Assert.That(game.Present, Is.EqualTo(2));

        GameState beforeBranch = game;
        Timeline rootBefore = beforeBranch.GetTimeline(GameState.RootTimelineId);

        // --- act: branch off T0, which is already history ----------------
        TemporalMoveResult branched = game.PerformTemporalMove(
            sourceTimelineId: GameState.RootTimelineId,
            sourceTime: 0,
            row: 0,
            column: 0,
            value: 2);

        Assert.That(branched.Succeeded, Is.True, branched.Rejection.ToString());

        GameState afterBranch = branched.State;
        Timeline root = afterBranch.GetTimeline(GameState.RootTimelineId);
        Timeline child = afterBranch.GetTimeline(branched.NewTimelineId!.Value);

        // --- assert: the parent's history is untouched -------------------
        Assert.That(root, Is.SameAs(rootBefore));
        Assert.That(root.StateCount, Is.EqualTo(3));
        Assert.That(root.FrontierTime, Is.EqualTo(2));
        Assert.That(root.StateAt(0), Is.EqualTo(startingBoard));
        Assert.That(root.StateAt(1), Is.EqualTo(rootAtT1));
        Assert.That(root.StateAt(2), Is.EqualTo(rootAtT2));
        Assert.That(root.Status, Is.EqualTo(TimelineStatus.Active));
        Assert.That(beforeBranch.Timelines, Has.Count.EqualTo(1), "the earlier state never gained a timeline");

        // --- assert: the present moved backwards -------------------------
        Assert.That(child.FirstStateTime, Is.EqualTo(0));
        Assert.That(child.FrontierTime, Is.EqualTo(1));
        Assert.That(afterBranch.Present, Is.EqualTo(1), "the child's earlier frontier is now the present");
        Assert.That(
            afterBranch.PlaceValue(2, 0, 2).Rejection,
            Is.EqualTo(MoveRejection.TimelineNotAtPresent),
            "the root has to wait for the child to catch up");

        // --- act: play the child -----------------------------------------
        GameState onChild = afterBranch.SelectTimeline(child.Id);
        MoveResult childMove = onChild.PlaceValue(0, 1, 1);

        Assert.That(childMove.Succeeded, Is.True, childMove.Rejection.ToString());

        GameState played = childMove.State;
        Timeline childAfter = played.GetTimeline(child.Id);
        Timeline rootAfter = played.GetTimeline(GameState.RootTimelineId);

        Assert.That(childAfter.FrontierTime, Is.EqualTo(2));
        Assert.That(played.Present, Is.EqualTo(2), "both frontiers are level again");

        // --- assert: two separate timelines, each with its own history ---
        Assert.That(rootAfter, Is.Not.SameAs(childAfter));
        Assert.That(rootAfter.Id, Is.Not.EqualTo(childAfter.Id));
        Assert.That(childAfter.ParentId, Is.EqualTo(rootAfter.Id));
        Assert.That(rootAfter.ParentId, Is.Null);

        Assert.That(rootAfter.StateAt(0), Is.EqualTo(childAfter.StateAt(0)), "they share where they came from");
        Assert.That(rootAfter.StateAt(1), Is.Not.EqualTo(childAfter.StateAt(1)), "and diverge from there");
        Assert.That(rootAfter.StateAt(2), Is.Not.EqualTo(childAfter.StateAt(2)));

        Assert.That(rootAfter.StateAt(1), Is.EqualTo(rootAtT1), "the parent's history never moved");
        Assert.That(rootAfter.StateAt(2), Is.EqualTo(rootAtT2));

        // --- assert: one branch, one unit of budget ----------------------
        Assert.That(played.Timelines, Has.Count.EqualTo(2));
        Assert.That(played.RemainingTemporalBudget, Is.EqualTo(1));
        Assert.That(played.Outcome, Is.EqualTo(GameOutcome.InProgress));
    }
}
