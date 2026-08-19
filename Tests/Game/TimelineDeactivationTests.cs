using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;

namespace FiveDSudoku.Tests;

/// <summary>
/// Deactivation: parking an active timeline so its slot goes to another one.
/// </summary>
[TestFixture]
public sealed class TimelineDeactivationTests
{
    /// <summary>A 4x4 with a single given, so branches stay playable.</summary>
    private const string SparseBoard = """
        1.|..
        ..|..
        --+--
        ..|..
        ..|..
        """;

    private static GameState StartSparse(int maxActiveTimelines, int temporalBudget = 4) =>
        GameState.Start(
            Levels.FromText(
                "sparse",
                BoardSize.FourByFour,
                SparseBoard,
                temporalBudget: temporalBudget,
                maxActiveTimelines: maxActiveTimelines));

    // ---- the basic move --------------------------------------------------

    [Test]
    public void ParkingAnActiveTimelineGivesUpItsSlot()
    {
        GameState game = Levels.PlayAnyLegalMove(StartSparse(maxActiveTimelines: 1));

        Assert.That(game.FreeActiveSlots, Is.EqualTo(0));

        TimelineSlotChangeResult result = game.DeactivateTimeline(GameState.RootTimelineId);

        Assert.That(result.Succeeded, Is.True, result.Rejection.ToString());

        Timeline parked = result.State.GetTimeline(GameState.RootTimelineId);

        Assert.That(parked.Status, Is.EqualTo(TimelineStatus.Inactive));
        Assert.That(parked.OccupiesActiveSlot, Is.False);
        Assert.That(result.State.FreeActiveSlots, Is.EqualTo(1));
    }

    [Test]
    public void ParkingKeepsTheWholeHistoryAndCostsNoBudget()
    {
        GameState game = Levels.PlayAnyLegalMove(Levels.PlayAnyLegalMove(StartSparse(maxActiveTimelines: 1)));
        Timeline before = game.SelectedTimeline;

        GameState parked = game.DeactivateTimeline(GameState.RootTimelineId).State;
        Timeline after = parked.GetTimeline(GameState.RootTimelineId);

        Assert.That(after.StateCount, Is.EqualTo(before.StateCount));
        Assert.That(after.FirstStateTime, Is.EqualTo(before.FirstStateTime));
        Assert.That(after.FrontierTime, Is.EqualTo(before.FrontierTime));

        for (int time = after.FirstStateTime; time <= after.FrontierTime; time++)
        {
            Assert.That(after.StateAt(time), Is.EqualTo(before.StateAt(time)), $"T{time}");
        }

        Assert.That(parked.RemainingTemporalBudget, Is.EqualTo(game.RemainingTemporalBudget));
    }

    [Test]
    public void AParkedTimelineStopsCountingTowardsThePresentAndCannotBePlayed()
    {
        GameState game = StartSparse(maxActiveTimelines: 2);
        game = Levels.PlayAnyLegalMove(game);
        game = Levels.PlayAnyLegalMove(game);
        game = Levels.PlayAnyLegalMove(game);

        TemporalMoveResult branched = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3);
        int branchId = branched.NewTimelineId!.Value;
        GameState withBranch = branched.State;

        Assert.That(withBranch.Present, Is.EqualTo(1), "the branch is the earliest active frontier");

        GameState parked = withBranch.DeactivateTimeline(branchId).State;

        Assert.That(parked.GetTimeline(branchId).FrontierTime, Is.EqualTo(1));
        Assert.That(parked.Present, Is.EqualTo(3), "and yet the present is back at the root's frontier");
        Assert.That(
            parked.SelectTimeline(branchId).PlaceValue(1, 0, 2).Rejection,
            Is.EqualTo(MoveRejection.TimelineNotActive));
    }

    [Test]
    public void ParkingAndActivatingSwapWhichTimelineIsInPlay()
    {
        GameState game = Levels.PlayAnyLegalMove(StartSparse(maxActiveTimelines: 1));

        TemporalMoveResult branched = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3);
        int branchId = branched.NewTimelineId!.Value;
        GameState withParkedBranch = branched.State;

        Assert.That(withParkedBranch.GetTimeline(branchId).Status, Is.EqualTo(TimelineStatus.Inactive));
        Assert.That(
            withParkedBranch.ActivateTimeline(branchId).Rejection,
            Is.EqualTo(TimelineSlotChangeRejection.NoFreeActiveSlot));

        // Park the root; the branch can now take the slot.
        GameState swapped = withParkedBranch.DeactivateTimeline(GameState.RootTimelineId).State;
        TimelineSlotChangeResult activated = swapped.ActivateTimeline(branchId);

        Assert.That(activated.Succeeded, Is.True, activated.Rejection.ToString());
        Assert.That(activated.State.GetTimeline(branchId).Status, Is.EqualTo(TimelineStatus.Active));
        Assert.That(
            activated.State.GetTimeline(GameState.RootTimelineId).Status,
            Is.EqualTo(TimelineStatus.Inactive));
        Assert.That(activated.State.FreeActiveSlots, Is.EqualTo(0));
        Assert.That(activated.State.RemainingTemporalBudget, Is.EqualTo(withParkedBranch.RemainingTemporalBudget));
    }

    // ---- what parking costs you -----------------------------------------

    [Test]
    public void ParkingAndBringingBackATimelineRevealsThatItWasDoomed()
    {
        // Ordinary play never asks the solver, so a timeline that has taken a
        // legal but unrecoverable value stays active. Bringing it back into play
        // does ask, and it comes back dead.
        GameState game = GameState.Start(Levels.SolvableFourByFour(maxActiveTimelines: 1));

        game = game.PlaceValue(0, 1, 3).State;

        Assert.That(game.SelectedTimeline.Status, Is.EqualTo(TimelineStatus.Active));
        Assert.That(
            TimelineClassifier.ClassifyBoard(game.SelectedTimeline.Frontier),
            Is.EqualTo(TimelineStatus.Dead),
            "it was already beyond saving");

        GameState parked = game.DeactivateTimeline(GameState.RootTimelineId).State;
        GameState back = parked.ActivateTimeline(GameState.RootTimelineId).State;

        Assert.That(back.GetTimeline(GameState.RootTimelineId).Status, Is.EqualTo(TimelineStatus.Dead));
        Assert.That(back.GetTimeline(GameState.RootTimelineId).OccupiesActiveSlot, Is.False);
        Assert.That(back.Outcome, Is.EqualTo(GameOutcome.GameOver));
    }

    [Test]
    public void ParkingTheLastPlayableTimelineEndsTheRunButNotTerminally()
    {
        GameState game = Levels.PlayAnyLegalMove(StartSparse(maxActiveTimelines: 1));

        GameState parked = game.DeactivateTimeline(GameState.RootTimelineId).State;

        Assert.That(parked.Present, Is.Null);
        Assert.That(parked.Outcome, Is.EqualTo(GameOutcome.GameOver));

        GameState back = parked.ActivateTimeline(GameState.RootTimelineId).State;

        Assert.That(back.Outcome, Is.EqualTo(GameOutcome.InProgress));
        Assert.That(back.Present, Is.EqualTo(1));
    }

    // ---- refusals --------------------------------------------------------

    [Test]
    public void ParkingATimelineThatDoesNotExistIsRefused()
    {
        Assert.That(
            StartSparse(maxActiveTimelines: 2).DeactivateTimeline(88).Rejection,
            Is.EqualTo(TimelineSlotChangeRejection.TimelineNotFound));
    }

    [Test]
    public void ParkingAnAlreadyParkedTimelineIsRefused()
    {
        GameState game = Levels.PlayAnyLegalMove(StartSparse(maxActiveTimelines: 1));
        GameState parked = game.DeactivateTimeline(GameState.RootTimelineId).State;

        Assert.That(
            parked.DeactivateTimeline(GameState.RootTimelineId).Rejection,
            Is.EqualTo(TimelineSlotChangeRejection.TimelineNotActive));
    }

    [Test]
    public void ParkingADeadTimelineIsRefused()
    {
        GameState game = GameState.Start(
            Levels.FromText("almost-stuck", BoardSize.FourByFour, Puzzles.AlmostBlocked4));

        game = game.PlaceValue(0, 0, 4).State;

        Assert.That(game.SelectedTimeline.Status, Is.EqualTo(TimelineStatus.Dead));
        Assert.That(
            game.DeactivateTimeline(GameState.RootTimelineId).Rejection,
            Is.EqualTo(TimelineSlotChangeRejection.TimelineNotActive));
    }

    [Test]
    public void ParkingIsRefusedOnAWonRunSoAWinCannotBeUndone()
    {
        GameState won = Levels.SolveSelectedTimeline(GameState.Start(Levels.SolvableFourByFour()));

        Assert.That(won.Outcome, Is.EqualTo(GameOutcome.Won));
        Assert.That(
            won.DeactivateTimeline(GameState.RootTimelineId).Rejection,
            Is.EqualTo(TimelineSlotChangeRejection.RunAlreadyWon));
        Assert.That(won.ValidateDeactivation(GameState.RootTimelineId),
            Is.EqualTo(TimelineSlotChangeRejection.RunAlreadyWon));
    }

    [Test]
    public void ARefusedParkingChangesNothing()
    {
        GameState game = StartSparse(maxActiveTimelines: 2);

        TimelineSlotChangeResult result = game.DeactivateTimeline(88);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.State, Is.SameAs(game));
    }

    [Test]
    public void ValidateDeactivationAgreesWithDeactivateTimeline()
    {
        GameState game = Levels.PlayAnyLegalMove(StartSparse(maxActiveTimelines: 1));
        game = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3).State;

        for (int timelineId = -1; timelineId <= 3; timelineId++)
        {
            Assert.That(
                game.DeactivateTimeline(timelineId).Rejection,
                Is.EqualTo(game.ValidateDeactivation(timelineId)),
                $"L{timelineId}");
        }
    }
}
