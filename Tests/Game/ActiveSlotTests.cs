using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;

namespace FiveDSudoku.Tests;

/// <summary>
/// Active slots: who holds one, what happens to a branch when none is free, and
/// how an inactive timeline is brought back into play.
/// </summary>
[TestFixture]
public sealed class ActiveSlotTests
{
    /// <summary>A 4x4 with a single given, so almost every branch stays playable.</summary>
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

    // ---- who holds a slot ------------------------------------------------

    [Test]
    public void TheRootTimelineTakesOneOfTheLevelsSlots()
    {
        GameState game = StartSparse(maxActiveTimelines: 2);

        Assert.That(game.SelectedTimeline.OccupiesActiveSlot, Is.True);
        Assert.That(game.FreeActiveSlots, Is.EqualTo(1));
    }

    [Test]
    public void APlayableBranchTakesAFreeSlot()
    {
        GameState game = Levels.PlayAnyLegalMove(StartSparse(maxActiveTimelines: 2));

        TemporalMoveResult result = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3);

        Assert.That(result.Succeeded, Is.True, result.Rejection.ToString());

        Timeline branch = result.State.GetTimeline(result.NewTimelineId!.Value);

        Assert.That(branch.Status, Is.EqualTo(TimelineStatus.Active));
        Assert.That(branch.OccupiesActiveSlot, Is.True);
        Assert.That(result.State.FreeActiveSlots, Is.EqualTo(0));
    }

    [Test]
    public void ADeadBranchHoldsNoSlotEvenThoughOneWasFree()
    {
        GameState game = GameState.Start(Levels.SolvableFourByFour(maxActiveTimelines: 2));
        game = game.PlaceValue(0, 1, 4).State;

        Assert.That(game.FreeActiveSlots, Is.EqualTo(1));

        TemporalMoveResult result = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3);
        Timeline branch = result.State.GetTimeline(result.NewTimelineId!.Value);

        Assert.That(branch.Status, Is.EqualTo(TimelineStatus.Dead));
        Assert.That(branch.OccupiesActiveSlot, Is.False);
        Assert.That(result.State.FreeActiveSlots, Is.EqualTo(1), "the slot was never taken");
    }

    [Test]
    public void ATimelineReleasesItsSlotWhenItRunsOutOfMoves()
    {
        GameState game = GameState.Start(
            Levels.FromText("almost-stuck", BoardSize.FourByFour, Puzzles.AlmostBlocked4, maxActiveTimelines: 2));

        Assert.That(game.FreeActiveSlots, Is.EqualTo(1));

        game = game.PlaceValue(0, 0, 4).State;

        Assert.That(game.SelectedTimeline.Status, Is.EqualTo(TimelineStatus.Dead));
        Assert.That(game.SelectedTimeline.OccupiesActiveSlot, Is.False);
        Assert.That(game.FreeActiveSlots, Is.EqualTo(2));
    }

    [Test]
    public void ASolvedTimelineKeepsItsSlot()
    {
        GameState won = Levels.SolveSelectedTimeline(
            GameState.Start(Levels.SolvableFourByFour(maxActiveTimelines: 2)));

        Assert.That(won.SelectedTimeline.Status, Is.EqualTo(TimelineStatus.Solved));
        Assert.That(won.SelectedTimeline.OccupiesActiveSlot, Is.True);
    }

    [Test]
    public void NoDeadOrInactiveTimelineEverHoldsASlot()
    {
        GameState game = Levels.PlayAnyLegalMove(StartSparse(maxActiveTimelines: 1));
        game = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3).State;
        game = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 4).State;

        foreach (Timeline timeline in game.Timelines)
        {
            if (timeline.Status == TimelineStatus.Dead || timeline.Status == TimelineStatus.Inactive)
            {
                Assert.That(timeline.OccupiesActiveSlot, Is.False, timeline.ToString());
            }
        }

        Assert.That(game.FreeActiveSlots, Is.GreaterThanOrEqualTo(0));
    }

    // ---- branching with no slot to spare ---------------------------------

    [Test]
    public void ABranchMadeWithNoFreeSlotIsCreatedInactive()
    {
        GameState game = Levels.PlayAnyLegalMove(StartSparse(maxActiveTimelines: 1));

        TemporalMoveResult result = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3);

        Assert.That(result.Succeeded, Is.True, result.Rejection.ToString());

        Timeline branch = result.State.GetTimeline(result.NewTimelineId!.Value);

        Assert.That(branch.Status, Is.EqualTo(TimelineStatus.Inactive));
        Assert.That(branch.OccupiesActiveSlot, Is.False);
        Assert.That(branch.IsCapableOfFurtherPlay, Is.False);
    }

    [Test]
    public void AnInactiveTimelineKeepsItsHistoryAndStaysInspectable()
    {
        GameState game = Levels.PlayAnyLegalMove(StartSparse(maxActiveTimelines: 1));
        SudokuBoard sourceState = game.SelectedTimeline.StateAt(0);

        TemporalMoveResult result = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3);
        Timeline branch = result.State.GetTimeline(result.NewTimelineId!.Value);

        Assert.That(branch.StateCount, Is.EqualTo(2));
        Assert.That(branch.StateAt(0), Is.EqualTo(sourceState));
        Assert.That(branch.Frontier, Is.EqualTo(sourceState.WithValue(0, 1, 3)));
        Assert.That(branch.ParentId, Is.EqualTo(GameState.RootTimelineId));
        Assert.That(branch.BranchTime, Is.EqualTo(0));
    }

    [Test]
    public void AnInactiveTimelineCannotBePlayedOn()
    {
        GameState game = Levels.PlayAnyLegalMove(StartSparse(maxActiveTimelines: 1));

        TemporalMoveResult result = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3);
        GameState onBranch = result.State.SelectTimeline(result.NewTimelineId!.Value);

        Assert.That(onBranch.PlaceValue(1, 0, 2).Rejection, Is.EqualTo(MoveRejection.TimelineNotActive));
    }

    // ---- activation ------------------------------------------------------

    [Test]
    public void ActivatingIsRefusedWhileEverySlotIsTaken()
    {
        GameState game = Levels.PlayAnyLegalMove(StartSparse(maxActiveTimelines: 1));
        TemporalMoveResult result = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3);
        int branchId = result.NewTimelineId!.Value;

        Assert.That(result.State.FreeActiveSlots, Is.EqualTo(0));
        Assert.That(
            result.State.ActivateTimeline(branchId).Rejection,
            Is.EqualTo(TimelineSlotChangeRejection.NoFreeActiveSlot));
    }

    [Test]
    public void ActivatingATimelineThatIsAlreadyInPlayIsRefused()
    {
        GameState game = StartSparse(maxActiveTimelines: 2);

        Assert.That(
            game.ActivateTimeline(GameState.RootTimelineId).Rejection,
            Is.EqualTo(TimelineSlotChangeRejection.TimelineNotInactive));
    }

    [Test]
    public void ActivatingATimelineThatDoesNotExistIsRefused()
    {
        Assert.That(
            StartSparse(maxActiveTimelines: 2).ActivateTimeline(77).Rejection,
            Is.EqualTo(TimelineSlotChangeRejection.TimelineNotFound));
    }

    [Test]
    public void ARefusedActivationChangesNothing()
    {
        GameState game = StartSparse(maxActiveTimelines: 2);

        TimelineSlotChangeResult result = game.ActivateTimeline(GameState.RootTimelineId);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.State, Is.SameAs(game));
    }

    [Test]
    public void ActivatingSucceedsOnceASlotComesFreeAndCostsNoBudget()
    {
        (GameState game, int branchId) = ParkedBranchAfterTheRootDies();

        Assert.That(game.GetTimeline(GameState.RootTimelineId).Status, Is.EqualTo(TimelineStatus.Dead));
        Assert.That(game.FreeActiveSlots, Is.EqualTo(1), "the dead root gave its slot back");

        int budgetBefore = game.RemainingTemporalBudget;
        TimelineSlotChangeResult activated = game.ActivateTimeline(branchId);

        Assert.That(activated.Succeeded, Is.True, activated.Rejection.ToString());
        Assert.That(activated.State.RemainingTemporalBudget, Is.EqualTo(budgetBefore));
    }

    [Test]
    public void ActivationClassifiesTheTimelineItBringsIntoPlay()
    {
        (GameState game, int branchId) = ParkedBranchAfterTheRootDies();

        Timeline before = game.GetTimeline(branchId);

        Assert.That(before.Status, Is.EqualTo(TimelineStatus.Inactive));

        Timeline after = game.ActivateTimeline(branchId).State.GetTimeline(branchId);

        Assert.That(after.Status, Is.EqualTo(TimelineStatus.Active));
        Assert.That(after.OccupiesActiveSlot, Is.True);
        Assert.That(after.StateCount, Is.EqualTo(before.StateCount), "activation is not a move");
        Assert.That(after.Frontier, Is.EqualTo(before.Frontier));
    }

    [Test]
    public void AnInactiveTimelineThatTurnsOutImpossibleEntersAsDead()
    {
        // A branch is parked without being looked at, so nothing yet knows it can
        // never be completed. Activation is where that gets decided.
        GameState game = GameState.Start(Levels.SolvableFourByFour(temporalBudget: 2, maxActiveTimelines: 1));

        game = game.PlaceValue(0, 1, 4).State;

        // 3 at r0c1 is legal Sudoku and has no completion at all.
        TemporalMoveResult parked = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3);

        Assert.That(parked.Succeeded, Is.True, parked.Rejection.ToString());

        int branchId = parked.NewTimelineId!.Value;
        game = parked.State;

        Assert.That(game.GetTimeline(branchId).Status, Is.EqualTo(TimelineStatus.Inactive));

        // Take the root down a line that cannot be completed either, so it runs
        // out of moves and hands its slot over.
        game = game.PlaceValue(0, 2, 2).State;

        while (game.Outcome == GameOutcome.InProgress)
        {
            game = Levels.PlayAnyLegalMove(game);
        }

        Assert.That(game.GetTimeline(GameState.RootTimelineId).Status, Is.EqualTo(TimelineStatus.Dead));
        Assert.That(game.FreeActiveSlots, Is.EqualTo(1));

        TimelineSlotChangeResult activated = game.ActivateTimeline(branchId);

        Assert.That(activated.Succeeded, Is.True, activated.Rejection.ToString());

        Timeline branch = activated.State.GetTimeline(branchId);

        Assert.That(branch.Status, Is.EqualTo(TimelineStatus.Dead), "activation is what finds this out");
        Assert.That(branch.OccupiesActiveSlot, Is.False, "a dead timeline gives the slot straight back");
        Assert.That(activated.State.Outcome, Is.EqualTo(GameOutcome.GameOver));
    }

    /// <summary>
    /// A run on the uniquely solvable 4x4 with a single active slot: the root takes
    /// a legal but wrong value, a branch back onto the right one is parked as
    /// inactive because no slot is free, and the root then plays itself to a
    /// standstill and gives its slot up.
    /// </summary>
    private static (GameState Game, int BranchId) ParkedBranchAfterTheRootDies()
    {
        GameState game = GameState.Start(Levels.SolvableFourByFour(temporalBudget: 3, maxActiveTimelines: 1));

        game = game.PlaceValue(0, 1, 3).State;

        TemporalMoveResult branched = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 4);

        Assert.That(branched.Succeeded, Is.True, branched.Rejection.ToString());
        Assert.That(
            branched.State.GetTimeline(branched.NewTimelineId!.Value).Status,
            Is.EqualTo(TimelineStatus.Inactive));

        game = branched.State;

        while (game.Outcome == GameOutcome.InProgress)
        {
            game = Levels.PlayAnyLegalMove(game);
        }

        return (game, branched.NewTimelineId!.Value);
    }

    [Test]
    public void ActivatingIsRefusedOnAWonRun()
    {
        GameState won = Levels.SolveSelectedTimeline(GameState.Start(Levels.SolvableFourByFour()));

        Assert.That(
            won.ActivateTimeline(GameState.RootTimelineId).Rejection,
            Is.EqualTo(TimelineSlotChangeRejection.RunAlreadyWon));
    }
}
