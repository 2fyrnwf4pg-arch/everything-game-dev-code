using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;

namespace FiveDSudoku.Tests;

/// <summary>
/// The present is the earliest frontier among active timelines holding a slot.
/// With one timeline that degenerates to "the root's frontier", but the general
/// formula is what is implemented and what these tests check.
/// </summary>
[TestFixture]
public sealed class PresentTests
{
    private static readonly SudokuBoard Solution =
        SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Unique4Solution);

    private static GameState StartGame() => GameState.Start(Levels.SolvableFourByFour());

    [Test]
    public void PresentStartsAtTimeZero()
    {
        Assert.That(StartGame().Present, Is.EqualTo(0));
    }

    [Test]
    public void PresentIsTheMinimumFrontierAmongActiveSlotTimelines()
    {
        GameState game = StartGame();
        game = game.PlaceValue(0, 1, Solution[0, 1]).State;
        game = game.PlaceValue(0, 2, Solution[0, 2]).State;

        int expected = int.MaxValue;

        foreach (Timeline timeline in game.Timelines)
        {
            if (timeline.Status == TimelineStatus.Active && timeline.OccupiesActiveSlot)
            {
                expected = timeline.FrontierTime < expected ? timeline.FrontierTime : expected;
            }
        }

        Assert.That(game.Present, Is.EqualTo(expected));
    }

    [Test]
    public void PresentAdvancesWithEveryPlacement()
    {
        GameState game = StartGame();

        Assert.That(game.Present, Is.EqualTo(0));

        game = game.PlaceValue(0, 1, Solution[0, 1]).State;
        Assert.That(game.Present, Is.EqualTo(1));

        game = game.PlaceValue(0, 2, Solution[0, 2]).State;
        Assert.That(game.Present, Is.EqualTo(2));

        game = game.PlaceValue(0, 3, Solution[0, 3]).State;
        Assert.That(game.Present, Is.EqualTo(3));
    }

    [Test]
    public void PresentTracksTheSelectedTimelinesFrontierWhileOnlyOneTimelineExists()
    {
        GameState game = StartGame();

        for (int column = 1; column <= 3; column++)
        {
            game = game.PlaceValue(0, column, Solution[0, column]).State;

            Assert.That(game.Present, Is.EqualTo(game.SelectedTimeline.FrontierTime));
        }
    }

    [Test]
    public void ARefusedPlacementDoesNotMoveThePresent()
    {
        GameState game = StartGame().PlaceValue(0, 1, Solution[0, 1]).State;

        GameState afterRefusal = game.PlaceValue(0, 0, 3).State;

        Assert.That(afterRefusal.Present, Is.EqualTo(game.Present));
    }

    [Test]
    public void PresentAdvancesOnlyWhenTheEarliestActiveFrontierAdvances()
    {
        GameState game = GameState.Start(Levels.TwoSolutionFourByFour());
        game = game.PlaceValue(0, 0, 1).State;
        game = game.PlaceValue(0, 1, 2).State;

        GameState afterBranch = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 0, 2).State;
        int branchId = 1;

        Assert.That(afterBranch.Present, Is.EqualTo(1), "the branch is now the earliest active frontier");
        Assert.That(afterBranch.GetTimeline(GameState.RootTimelineId).FrontierTime, Is.EqualTo(2));

        // Advancing the earliest frontier is what moves the present, and only up
        // to the next-earliest one.
        GameState advanced = afterBranch.SelectTimeline(branchId).PlaceValue(0, 1, 1).State;

        Assert.That(advanced.GetTimeline(branchId).FrontierTime, Is.EqualTo(2));
        Assert.That(advanced.Present, Is.EqualTo(2));
    }

    [Test]
    public void InactiveTimelinesDoNotInfluenceThePresent()
    {
        // One slot, held by the root, so the branch is parked at an early frontier.
        GameState game = GameState.Start(
            Levels.SolvableFourByFour(temporalBudget: 2, maxActiveTimelines: 1));

        game = game.PlaceValue(0, 1, 4).State;
        game = game.PlaceValue(0, 2, 3).State;

        Assert.That(game.Present, Is.EqualTo(2));

        TemporalMoveResult parked = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3);
        Timeline branch = parked.State.GetTimeline(parked.NewTimelineId!.Value);

        Assert.That(branch.Status, Is.EqualTo(TimelineStatus.Inactive));
        Assert.That(branch.FrontierTime, Is.EqualTo(1), "which is earlier than the present");
        Assert.That(parked.State.Present, Is.EqualTo(2), "and yet the present has not moved");
    }

    [Test]
    public void PresentIsAbsentWhenNoTimelineIsActiveAnyMore()
    {
        // Solving the root leaves no active timeline, so there is no present. The
        // run's outcome must be decided independently of that.
        GameState won = Levels.SolveSelectedTimeline(StartGame());

        Assert.That(won.SelectedTimeline.Status, Is.EqualTo(TimelineStatus.Solved));
        Assert.That(won.Present, Is.Null);
        Assert.That(won.Outcome, Is.EqualTo(GameOutcome.Won));
    }

    [Test]
    public void AnAbsentPresentDoesNotByItselfSayHowTheRunEnded()
    {
        // Both of these have no present at all, and they are opposite outcomes.
        // The outcome is computed from the timelines, never read off the present.
        GameState won = Levels.SolveSelectedTimeline(StartGame());
        GameState lost = GameState.Start(Levels.FromText("stuck", BoardSize.FourByFour, Puzzles.Blocked4));

        Assert.That(won.Present, Is.Null);
        Assert.That(lost.Present, Is.Null);
        Assert.That(won.Outcome, Is.EqualTo(GameOutcome.Won));
        Assert.That(lost.Outcome, Is.EqualTo(GameOutcome.GameOver));
    }

    [Test]
    public void ATimelineThatCanNoLongerBePlayedStopsHoldingThePresentBack()
    {
        // A timeline with no legal placement left can never produce another state.
        // If it kept counting towards the present, it would pin the present at its
        // own frontier forever and no other timeline could ever advance past it.
        GameState stuck = GameState.Start(
            Levels.FromText("stuck", BoardSize.FourByFour, Puzzles.Blocked4));

        Assert.That(stuck.SelectedTimeline.Status, Is.EqualTo(TimelineStatus.Dead));
        Assert.That(stuck.Present, Is.Null);
    }
}
