using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;

namespace FiveDSudoku.Tests;

/// <summary>
/// A whole multi-timeline run through the public API: branch, switch, let a
/// timeline die, bring the parked one back, and win on it.
/// </summary>
[TestFixture]
public sealed class MultiTimelineSessionTests
{
    [Test]
    public void ARunCanBeLostOnTheRootAndWonOnABranchThatWasParkedAllAlong()
    {
        // One slot only, so the root holds it and any branch has to wait.
        GameState game = GameState.Start(
            Levels.SolvableFourByFour(temporalBudget: 2, temporalWindow: 16, maxActiveTimelines: 1));
        SudokuBoard startingBoard = game.SelectedTimeline.StateAt(0);

        // 1. A legal but wrong value on the root.
        game = game.PlaceValue(0, 1, 3).State;

        Assert.That(game.Outcome, Is.EqualTo(GameOutcome.InProgress));
        Assert.That(game.SelectedTimeline.Status, Is.EqualTo(TimelineStatus.Active));

        // 2. Branch back to before the mistake. No slot is free, so it is parked.
        TemporalMoveResult branched = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 4);

        Assert.That(branched.Succeeded, Is.True, branched.Rejection.ToString());

        int branchId = branched.NewTimelineId!.Value;
        game = branched.State;

        Assert.That(game.GetTimeline(branchId).Status, Is.EqualTo(TimelineStatus.Inactive));
        Assert.That(game.RemainingTemporalBudget, Is.EqualTo(1));

        // 3. Switching to it is free and shows its history without letting it play.
        game = game.SelectTimeline(branchId);

        Assert.That(game.SelectedTimeline.StateAt(0), Is.EqualTo(startingBoard));
        Assert.That(game.RemainingTemporalBudget, Is.EqualTo(1), "switching costs nothing");
        Assert.That(game.PlaceValue(0, 2, 3).Rejection, Is.EqualTo(MoveRejection.TimelineNotActive));

        // 4. Play the root out. Its mistake has no completion, so it eventually
        //    runs out of legal placements and dies, giving up its slot.
        game = game.SelectTimeline(GameState.RootTimelineId);

        while (game.Outcome == GameOutcome.InProgress)
        {
            game = Levels.PlayAnyLegalMove(game);
        }

        Assert.That(game.GetTimeline(GameState.RootTimelineId).Status, Is.EqualTo(TimelineStatus.Dead));
        Assert.That(game.Outcome, Is.EqualTo(GameOutcome.GameOver));
        Assert.That(game.Present, Is.Null);
        Assert.That(game.FreeActiveSlots, Is.EqualTo(1), "the dead root released its slot");

        // A parked timeline never counts as a way to keep playing.
        Assert.That(game.GetTimeline(branchId).IsCapableOfFurtherPlay, Is.False);

        // 5. Now the parked branch can be brought in.
        TimelineSlotChangeResult activated = game.ActivateTimeline(branchId);

        Assert.That(activated.Succeeded, Is.True, activated.Rejection.ToString());

        game = activated.State;

        Assert.That(game.GetTimeline(branchId).Status, Is.EqualTo(TimelineStatus.Active));
        Assert.That(game.GetTimeline(branchId).OccupiesActiveSlot, Is.True);
        Assert.That(game.Outcome, Is.EqualTo(GameOutcome.InProgress), "the run is back on");
        Assert.That(game.Present, Is.EqualTo(1), "the revived branch defines the present on its own");

        // 6. Win on it. The root's history is still there, untouched.
        game = Levels.SolveSelectedTimeline(game.SelectTimeline(branchId));

        Assert.That(game.Outcome, Is.EqualTo(GameOutcome.Won));
        Assert.That(game.GetTimeline(branchId).Status, Is.EqualTo(TimelineStatus.Solved));
        Assert.That(game.GetTimeline(branchId).Frontier.IsSolved(), Is.True);
        Assert.That(game.GetTimeline(GameState.RootTimelineId).Status, Is.EqualTo(TimelineStatus.Dead));
        Assert.That(game.GetTimeline(GameState.RootTimelineId).StateAt(0), Is.EqualTo(startingBoard));
        Assert.That(game.GetTimeline(GameState.RootTimelineId).StateAt(1)[0, 1], Is.EqualTo(3), "the mistake is still on record");
        Assert.That(game.RemainingTemporalBudget, Is.EqualTo(1), "one branch, one unit");
    }

    [Test]
    public void OneBranchDyingDoesNotEndARunThatAnotherTimelineCanStillWin()
    {
        GameState game = GameState.Start(Levels.SolvableFourByFour(temporalBudget: 2, maxActiveTimelines: 3));

        game = game.PlaceValue(0, 1, 4).State;

        TemporalMoveResult doomed = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3);

        Assert.That(doomed.Succeeded, Is.True, doomed.Rejection.ToString());
        Assert.That(
            doomed.State.GetTimeline(doomed.NewTimelineId!.Value).Status,
            Is.EqualTo(TimelineStatus.Dead));
        Assert.That(doomed.State.Outcome, Is.EqualTo(GameOutcome.InProgress));

        GameState won = Levels.SolveSelectedTimeline(doomed.State);

        Assert.That(won.Outcome, Is.EqualTo(GameOutcome.Won));
        Assert.That(won.GetTimeline(doomed.NewTimelineId!.Value).Status, Is.EqualTo(TimelineStatus.Dead));
    }
}
