using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Level;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;

namespace FiveDSudoku.Tests;

/// <summary>
/// Temporal Moves: the branch conditions, the budget, the window, and the
/// immediate classification of a new timeline.
/// </summary>
[TestFixture]
public sealed class TemporalMoveTests
{
    private static readonly SudokuBoard Solution =
        SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Unique4Solution);

    /// <summary>
    /// The empty cells of the uniquely solvable 4x4, in the order the tests play
    /// them, so a run can be advanced to a known depth.
    /// </summary>
    private static readonly (int Row, int Column)[] Unique4PlayOrder =
    {
        (0, 1), (0, 2), (0, 3), (1, 0), (1, 1), (1, 3),
        (2, 0), (2, 2), (2, 3), (3, 0), (3, 1), (3, 2),
    };

    /// <summary>Starts the uniquely solvable 4x4 and plays <paramref name="moves"/> ordinary moves.</summary>
    private static GameState PlayedTo(int moves, LevelDefinition? level = null)
    {
        GameState game = GameState.Start(level ?? Levels.SolvableFourByFour());

        for (int index = 0; index < moves; index++)
        {
            (int row, int column) = Unique4PlayOrder[index];
            MoveResult result = game.PlaceValue(row, column, Solution[row, column]);

            Assert.That(result.Succeeded, Is.True, $"setup move r{row}c{column} was refused: {result.Rejection}");

            game = result.State;
        }

        return game;
    }

    // ---- budget ----------------------------------------------------------

    [Test]
    public void ABranchCostsExactlyOneUnitOfBudget()
    {
        GameState game = PlayedTo(1, Levels.SolvableFourByFour(temporalBudget: 3));

        Assert.That(game.RemainingTemporalBudget, Is.EqualTo(3), "ordinary play must not cost budget");

        TemporalMoveResult first = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3);

        Assert.That(first.Succeeded, Is.True, first.Rejection.ToString());
        Assert.That(first.State.RemainingTemporalBudget, Is.EqualTo(2));

        TemporalMoveResult second = first.State.PerformTemporalMove(GameState.RootTimelineId, 0, 1, 0, 3);

        Assert.That(second.Succeeded, Is.True, second.Rejection.ToString());
        Assert.That(second.State.RemainingTemporalBudget, Is.EqualTo(1));
    }

    [Test]
    public void AnExhaustedBudgetRejectsFurtherBranches()
    {
        GameState game = PlayedTo(1, Levels.SolvableFourByFour(temporalBudget: 1));

        TemporalMoveResult first = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3);

        Assert.That(first.Succeeded, Is.True, first.Rejection.ToString());
        Assert.That(first.State.RemainingTemporalBudget, Is.EqualTo(0));

        TemporalMoveResult second = first.State.PerformTemporalMove(GameState.RootTimelineId, 0, 1, 0, 3);

        Assert.That(second.Succeeded, Is.False);
        Assert.That(second.Rejection, Is.EqualTo(TemporalMoveRejection.NoTemporalBudget));
        Assert.That(second.State.Timelines, Has.Count.EqualTo(2), "no timeline may have been created");
    }

    [Test]
    public void ALevelWithoutBudgetAllowsNoBranchingAtAll()
    {
        GameState game = PlayedTo(2, Levels.SolvableFourByFour(temporalBudget: 0));

        Assert.That(
            game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3).Rejection,
            Is.EqualTo(TemporalMoveRejection.NoTemporalBudget));
    }

    [Test]
    public void ARefusedBranchSpendsNothingAndChangesNothing()
    {
        GameState game = PlayedTo(1);

        // Illegal placement: column 1 already holds a 2.
        TemporalMoveResult result = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 2);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.NewTimelineId, Is.Null);
        Assert.That(result.State, Is.SameAs(game));
        Assert.That(game.RemainingTemporalBudget, Is.EqualTo(game.Level.TemporalBudget));
        Assert.That(game.Timelines, Has.Count.EqualTo(1));
    }

    // ---- window ----------------------------------------------------------

    [Test]
    public void TheTemporalWindowBoundsHowFarBackABranchMayReach()
    {
        GameState game = PlayedTo(3, Levels.SolvableFourByFour(temporalWindow: 1));

        Assert.That(game.Present, Is.EqualTo(3));

        // present - 2 = 1, inside a window of 1
        Assert.That(
            game.ValidateTemporalMove(GameState.RootTimelineId, 2, 1, 3, 2),
            Is.EqualTo(TemporalMoveRejection.None));

        // present - 1 = 2 and present - 0 = 3, both beyond it
        Assert.That(
            game.ValidateTemporalMove(GameState.RootTimelineId, 1, 1, 3, 2),
            Is.EqualTo(TemporalMoveRejection.OutsideTemporalWindow));
        Assert.That(
            game.ValidateTemporalMove(GameState.RootTimelineId, 0, 1, 3, 2),
            Is.EqualTo(TemporalMoveRejection.OutsideTemporalWindow));
    }

    [Test]
    public void AZeroWindowRulesOutBranchingEntirely()
    {
        GameState game = PlayedTo(3, Levels.SolvableFourByFour(temporalWindow: 0));

        for (int sourceTime = 0; sourceTime < 3; sourceTime++)
        {
            Assert.That(
                game.ValidateTemporalMove(GameState.RootTimelineId, sourceTime, 1, 3, 2),
                Is.EqualTo(TemporalMoveRejection.OutsideTemporalWindow),
                $"T{sourceTime}");
        }
    }

    [Test]
    public void AWideWindowStillNeverReachesTheWholeOfHistoryByAccident()
    {
        GameState game = PlayedTo(3, Levels.SolvableFourByFour(temporalWindow: 3));

        // The window admits T0, but the other conditions still apply to every time.
        Assert.That(
            game.ValidateTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3),
            Is.EqualTo(TemporalMoveRejection.None));
        Assert.That(
            game.ValidateTemporalMove(GameState.RootTimelineId, 3, 0, 1, 3),
            Is.EqualTo(TemporalMoveRejection.SourceTimeIsNotHistorical));
    }

    // ---- which states may be branched from -------------------------------

    [TestCase(4)]
    [TestCase(5)]
    [TestCase(99)]
    public void BranchingFromAStateTheTimelineDoesNotHaveIsRejected(int sourceTime)
    {
        GameState game = PlayedTo(3);

        Assert.That(
            game.PerformTemporalMove(GameState.RootTimelineId, sourceTime, 1, 3, 2).Rejection,
            Is.EqualTo(TemporalMoveRejection.SourceTimeNotInTimeline));
    }

    [Test]
    public void BranchingFromTheCurrentFrontierIsRejected()
    {
        GameState game = PlayedTo(3);

        Assert.That(game.SelectedTimeline.FrontierTime, Is.EqualTo(3));
        Assert.That(
            game.PerformTemporalMove(GameState.RootTimelineId, 3, 1, 3, 2).Rejection,
            Is.EqualTo(TemporalMoveRejection.SourceTimeIsNotHistorical));
    }

    [Test]
    public void BranchingFromATimelineThatDoesNotExistIsRejected()
    {
        GameState game = PlayedTo(2);

        Assert.That(
            game.PerformTemporalMove(42, 0, 0, 1, 3).Rejection,
            Is.EqualTo(TemporalMoveRejection.SourceTimelineNotFound));
    }

    [Test]
    public void BranchingFromAStateThatIsNoLongerBeforeThePresentIsRejected()
    {
        // A branch pulls the present back, which can leave the parent's own history
        // sitting at or after it. Those states are then out of reach.
        GameState game = GameState.Start(Levels.TwoSolutionFourByFour());
        game = game.PlaceValue(0, 0, 1).State;
        game = game.PlaceValue(0, 1, 2).State;

        Assert.That(game.Present, Is.EqualTo(2));

        GameState branched = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 0, 2).State;

        Assert.That(branched.Present, Is.EqualTo(1), "the branch must pull the present back");
        Assert.That(
            branched.ValidateTemporalMove(GameState.RootTimelineId, 1, 2, 0, 1),
            Is.EqualTo(TemporalMoveRejection.SourceTimeNotBeforePresent));
    }

    // ---- placement legality ----------------------------------------------

    [TestCase(0, 1, 2, TestName = "value already in the column")]
    [TestCase(0, 0, 3, TestName = "cell already holds a value")]
    [TestCase(0, 1, 5, TestName = "value outside the grid's range")]
    [TestCase(0, 1, 0, TestName = "empty is not a placeable value")]
    [TestCase(4, 0, 1, TestName = "row outside the grid")]
    [TestCase(0, 4, 1, TestName = "column outside the grid")]
    public void ABranchPlacementThatIsNotLegalSudokuIsRejected(int row, int column, int value)
    {
        GameState game = PlayedTo(1);

        Assert.That(
            game.PerformTemporalMove(GameState.RootTimelineId, 0, row, column, value).Rejection,
            Is.EqualTo(TemporalMoveRejection.PlacementNotLegal));
    }

    // ---- immediate classification ----------------------------------------

    [Test]
    public void ALocallyLegalButImpossibleBranchIsDeadImmediately()
    {
        GameState game = PlayedTo(1);
        SudokuBoard atT0 = game.SelectedTimeline.StateAt(0);

        Assert.That(atT0.IsPlacementLegal(0, 1, 3), Is.True, "the alternative must be legal Sudoku");

        TemporalMoveResult result = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3);

        Assert.That(result.Succeeded, Is.True, result.Rejection.ToString());

        Timeline branch = result.State.GetTimeline(result.NewTimelineId!.Value);

        Assert.That(branch.Status, Is.EqualTo(TimelineStatus.Dead));
        Assert.That(branch.Frontier.IsValid(), Is.True, "the board itself breaks no constraint");
        Assert.That(branch.IsCapableOfFurtherPlay, Is.False);
    }

    [Test]
    public void ABranchThatCanStillBeCompletedIsActive()
    {
        GameState game = GameState.Start(Levels.TwoSolutionFourByFour());
        game = game.PlaceValue(0, 0, 1).State;

        TemporalMoveResult result = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 0, 2);

        Assert.That(result.Succeeded, Is.True, result.Rejection.ToString());

        Timeline branch = result.State.GetTimeline(result.NewTimelineId!.Value);

        Assert.That(branch.Status, Is.EqualTo(TimelineStatus.Active));
        Assert.That(branch.IsCapableOfFurtherPlay, Is.True);
    }

    // ---- what a branch is made of ----------------------------------------

    [Test]
    public void ABranchCarriesTheSourceStateOverAndAddsTheAlternative()
    {
        GameState game = PlayedTo(2);
        SudokuBoard sourceState = game.SelectedTimeline.StateAt(1);

        TemporalMoveResult result = game.PerformTemporalMove(GameState.RootTimelineId, 1, 1, 3, 2);

        Assert.That(result.Succeeded, Is.True, result.Rejection.ToString());

        Timeline branch = result.State.GetTimeline(result.NewTimelineId!.Value);

        Assert.That(branch.ParentId, Is.EqualTo(GameState.RootTimelineId));
        Assert.That(branch.BranchTime, Is.EqualTo(1));
        Assert.That(branch.FirstStateTime, Is.EqualTo(1));
        Assert.That(branch.FrontierTime, Is.EqualTo(2));
        Assert.That(branch.StateCount, Is.EqualTo(2));
        Assert.That(branch.StateAt(1), Is.EqualTo(sourceState), "the source state is carried over unchanged");
        Assert.That(branch.Frontier, Is.EqualTo(sourceState.WithValue(1, 3, 2)));

        // A slot was free when this branch was made, but the branch turned out to
        // be impossible and a dead timeline holds no slot.
        Assert.That(branch.Status, Is.EqualTo(TimelineStatus.Dead));
        Assert.That(branch.OccupiesActiveSlot, Is.False);
    }

    [Test]
    public void ABranchNeverTouchesTheTimelineItCameFrom()
    {
        GameState before = PlayedTo(2);
        Timeline rootBefore = before.SelectedTimeline;
        SudokuBoard[] statesBefore = { rootBefore.StateAt(0), rootBefore.StateAt(1), rootBefore.StateAt(2) };

        GameState after = before.PerformTemporalMove(GameState.RootTimelineId, 1, 1, 3, 2).State;
        Timeline rootAfter = after.GetTimeline(GameState.RootTimelineId);

        Assert.That(rootAfter, Is.SameAs(rootBefore), "the parent object is reused, not rebuilt");
        Assert.That(rootAfter.StateCount, Is.EqualTo(3));
        Assert.That(rootAfter.FrontierTime, Is.EqualTo(2));
        Assert.That(rootAfter.Status, Is.EqualTo(TimelineStatus.Active));
        Assert.That(rootAfter.ParentId, Is.Null);

        for (int time = 0; time <= 2; time++)
        {
            Assert.That(rootAfter.StateAt(time), Is.EqualTo(statesBefore[time]), $"T{time}");
        }

        Assert.That(before.Timelines, Has.Count.EqualTo(1), "the earlier game state must not gain a timeline");
        Assert.That(before.RemainingTemporalBudget, Is.EqualTo(before.Level.TemporalBudget));
    }

    [Test]
    public void TimelineIdsAreHandedOutInAscendingOrder()
    {
        GameState game = PlayedTo(1);

        TemporalMoveResult first = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3);
        TemporalMoveResult second = first.State.PerformTemporalMove(GameState.RootTimelineId, 0, 1, 0, 3);

        Assert.That(first.NewTimelineId, Is.EqualTo(1));
        Assert.That(second.NewTimelineId, Is.EqualTo(2));
        Assert.That(second.State.Timelines, Has.Count.EqualTo(3));
    }

    [Test]
    public void BranchingDoesNotChangeWhichTimelineIsSelected()
    {
        GameState game = PlayedTo(1);

        GameState after = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3).State;

        Assert.That(after.SelectedTimelineId, Is.EqualTo(GameState.RootTimelineId));
    }

    // ---- validation mirrors application ----------------------------------

    [Test]
    public void ValidateTemporalMoveAgreesWithPerformTemporalMove()
    {
        GameState game = PlayedTo(2);
        BoardSize size = game.Level.Size;

        for (int sourceTime = -1; sourceTime <= 3; sourceTime++)
        {
            for (int row = 0; row < size.Side; row++)
            {
                for (int column = 0; column < size.Side; column++)
                {
                    for (int value = 0; value <= size.Side; value++)
                    {
                        TemporalMoveRejection predicted =
                            game.ValidateTemporalMove(GameState.RootTimelineId, sourceTime, row, column, value);
                        TemporalMoveResult actual =
                            game.PerformTemporalMove(GameState.RootTimelineId, sourceTime, row, column, value);

                        Assert.That(
                            actual.Rejection,
                            Is.EqualTo(predicted),
                            $"T{sourceTime} r{row}c{column} value {value}");
                    }
                }
            }
        }
    }

    // ---- playing on a branch ---------------------------------------------

    [Test]
    public void ABranchCanBePlayedOnceItHoldsThePresent()
    {
        GameState game = GameState.Start(Levels.TwoSolutionFourByFour());
        game = game.PlaceValue(0, 0, 1).State;
        game = game.PlaceValue(0, 1, 2).State;

        TemporalMoveResult branched = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 0, 2);
        GameState afterBranch = branched.State.SelectTimeline(branched.NewTimelineId!.Value);

        Assert.That(afterBranch.Present, Is.EqualTo(1));
        Assert.That(afterBranch.SelectedTimeline.FrontierTime, Is.EqualTo(1));

        MoveResult played = afterBranch.PlaceValue(0, 1, 1);

        Assert.That(played.Succeeded, Is.True, played.Rejection.ToString());
        Assert.That(played.State.SelectedTimeline.FrontierTime, Is.EqualTo(2));
        Assert.That(played.State.RemainingTemporalBudget, Is.EqualTo(afterBranch.RemainingTemporalBudget));
    }

    [Test]
    public void ATimelineAheadOfThePresentCannotBePlayedOn()
    {
        GameState game = GameState.Start(Levels.TwoSolutionFourByFour());
        game = game.PlaceValue(0, 0, 1).State;
        game = game.PlaceValue(0, 1, 2).State;

        GameState afterBranch = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 0, 2).State;

        Assert.That(afterBranch.SelectedTimelineId, Is.EqualTo(GameState.RootTimelineId));
        Assert.That(afterBranch.SelectedTimeline.FrontierTime, Is.EqualTo(2));
        Assert.That(afterBranch.Present, Is.EqualTo(1));
        Assert.That(
            afterBranch.PlaceValue(2, 0, 2).Rejection,
            Is.EqualTo(MoveRejection.TimelineNotAtPresent));
    }

    [Test]
    public void PresentIsTheEarliestFrontierAcrossTimelinesNotTheLatest()
    {
        GameState game = GameState.Start(Levels.TwoSolutionFourByFour());
        game = game.PlaceValue(0, 0, 1).State;
        game = game.PlaceValue(0, 1, 2).State;

        GameState afterBranch = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 0, 2).State;

        Timeline root = afterBranch.GetTimeline(GameState.RootTimelineId);
        Timeline branch = afterBranch.GetTimeline(1);

        Assert.That(root.FrontierTime, Is.EqualTo(2));
        Assert.That(branch.FrontierTime, Is.EqualTo(1));
        Assert.That(afterBranch.Present, Is.EqualTo(1));
    }

    [Test]
    public void ADeadBranchDoesNotInfluenceThePresent()
    {
        GameState game = PlayedTo(2);

        GameState afterBranch = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3).State;
        Timeline branch = afterBranch.GetTimeline(1);

        Assert.That(branch.Status, Is.EqualTo(TimelineStatus.Dead));
        Assert.That(branch.FrontierTime, Is.EqualTo(1));
        Assert.That(afterBranch.Present, Is.EqualTo(2), "the dead branch's earlier frontier must not count");
    }

    // ---- interaction with a finished run ---------------------------------

    [Test]
    public void ALostRunStillAllowsTemporalMoves()
    {
        // Ordinary play is over, but the run is not: budget and a reachable
        // historical state are still there, so a branch may be attempted.
        GameState game = GameState.Start(
            Levels.FromText("almost-stuck", BoardSize.FourByFour, Puzzles.AlmostBlocked4, temporalBudget: 3));

        game = game.PlaceValue(0, 0, 4).State;

        Assert.That(game.Outcome, Is.EqualTo(GameOutcome.GameOver));
        Assert.That(game.PlaceValue(1, 1, 2).Rejection, Is.EqualTo(MoveRejection.GameAlreadyFinished));

        TemporalMoveResult result = game.PerformTemporalMove(GameState.RootTimelineId, 0, 3, 0, 4);

        Assert.That(result.Succeeded, Is.True, result.Rejection.ToString());
        Assert.That(result.State.RemainingTemporalBudget, Is.EqualTo(2));
    }

    [Test]
    public void ATemporalMoveCanBringALostRunBack()
    {
        // The bad historical choice this recovers from: 3 at r0c1 is legal Sudoku
        // but wrong, and ordinary play then runs out of legal placements nine moves
        // later without ever completing the grid.
        GameState game = GameState.Start(Levels.SolvableFourByFour(temporalBudget: 2));
        SudokuBoard start = game.SelectedTimeline.StateAt(0);

        game = game.PlaceValue(0, 1, 3).State;

        while (game.Outcome == GameOutcome.InProgress)
        {
            game = Levels.PlayAnyLegalMove(game);
        }

        Assert.That(game.Outcome, Is.EqualTo(GameOutcome.GameOver));
        Assert.That(game.SelectedTimeline.Frontier.IsComplete, Is.False);
        Assert.That(game.SelectedTimeline.Status, Is.EqualTo(TimelineStatus.Dead));
        Assert.That(game.Present, Is.Null, "nothing is active, so there is no present to reach back from");

        // Reach back past the mistake and take the other legal value instead.
        TemporalMoveResult rescue = game.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 4);

        Assert.That(rescue.Succeeded, Is.True, rescue.Rejection.ToString());

        GameState recovered = rescue.State;
        Timeline branch = recovered.GetTimeline(rescue.NewTimelineId!.Value);

        Assert.That(branch.Status, Is.EqualTo(TimelineStatus.Active));
        Assert.That(branch.StateAt(0), Is.EqualTo(start), "the branch starts from the untouched original");
        Assert.That(recovered.Outcome, Is.EqualTo(GameOutcome.InProgress), "the run is playable again");
        Assert.That(recovered.Present, Is.EqualTo(1), "the branch pulls the present back to its own frontier");

        // And it really is winnable from here.
        GameState won = Levels.SolveSelectedTimeline(recovered.SelectTimeline(branch.Id));

        Assert.That(won.Outcome, Is.EqualTo(GameOutcome.Won));
    }

    [Test]
    public void WithNoPresentTheWindowIsMeasuredFromTheSourceTimelinesOwnFrontier()
    {
        // AlmostBlocked4 dies one move in, so the run has no present at all. The
        // source timeline's frontier stands in, and the window still bites.
        GameState game = GameState.Start(
            Levels.FromText(
                "almost-stuck", BoardSize.FourByFour, Puzzles.AlmostBlocked4, temporalBudget: 3, temporalWindow: 0));

        game = game.PlaceValue(0, 0, 4).State;

        Assert.That(game.Present, Is.Null);
        Assert.That(game.SelectedTimeline.FrontierTime, Is.EqualTo(1));

        // reach = 1 - 0 = 1, beyond a window of 0
        Assert.That(
            game.ValidateTemporalMove(GameState.RootTimelineId, 0, 3, 0, 4),
            Is.EqualTo(TemporalMoveRejection.OutsideTemporalWindow));
    }

    [Test]
    public void ATimelineThatIsDoomedButStillPlayableStaysActive()
    {
        // Ordinary play does not run the solver. A wrong-but-legal value leaves a
        // grid with no completion, and the timeline stays playable until it
        // actually runs out of moves.
        GameState game = GameState.Start(Levels.SolvableFourByFour());

        game = game.PlaceValue(0, 1, 3).State;

        Assert.That(game.SelectedTimeline.Status, Is.EqualTo(TimelineStatus.Active));
        Assert.That(game.SelectedTimeline.Frontier.HasAnyLegalPlacement(), Is.True);
        Assert.That(TimelineClassifier.ClassifyBoard(game.SelectedTimeline.Frontier), Is.EqualTo(TimelineStatus.Dead));
        Assert.That(game.Outcome, Is.EqualTo(GameOutcome.InProgress));
    }

    [Test]
    public void AWonRunRefusesTemporalMoves()
    {
        GameState won = Levels.SolveSelectedTimeline(GameState.Start(Levels.SolvableFourByFour()));

        Assert.That(
            won.PerformTemporalMove(GameState.RootTimelineId, 0, 0, 1, 3).Rejection,
            Is.EqualTo(TemporalMoveRejection.RunAlreadyWon));
    }

}
