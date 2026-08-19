using System;
using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Sudoku;

namespace FiveDSudoku.Tests;

/// <summary>
/// The normal move rules and timeline switching.
/// </summary>
[TestFixture]
public sealed class GameStateTests
{
    private static readonly SudokuBoard Solution =
        SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Unique4Solution);

    private static GameState StartGame() => GameState.Start(Levels.SolvableFourByFour());

    // ---- starting a run --------------------------------------------------

    [Test]
    public void StartingARunCreatesExactlyOneSelectedRootTimeline()
    {
        GameState game = StartGame();

        Assert.That(game.Timelines, Has.Count.EqualTo(1));
        Assert.That(game.SelectedTimelineId, Is.EqualTo(GameState.RootTimelineId));
        Assert.That(game.Outcome, Is.EqualTo(GameOutcome.InProgress));
    }

    [Test]
    public void StartingARunGrantsTheLevelsFullTemporalBudget()
    {
        Assert.That(
            GameState.Start(Levels.SolvableFourByFour(temporalBudget: 7)).RemainingTemporalBudget,
            Is.EqualTo(7));
    }

    [Test]
    public void StartingARunNeedsALevel()
    {
        Assert.That(() => GameState.Start(null!), Throws.ArgumentNullException);
    }

    // ---- a legal placement -----------------------------------------------

    [Test]
    public void ALegalPlacementProducesTheNextState()
    {
        GameState game = StartGame();

        MoveResult result = game.PlaceValue(0, 1, Solution[0, 1]);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Rejection, Is.EqualTo(MoveRejection.None));
        Assert.That(result.State, Is.Not.SameAs(game));
        Assert.That(result.State.SelectedTimeline.Frontier[0, 1], Is.EqualTo(Solution[0, 1]));
    }

    [Test]
    public void ANormalPlacementSpendsNoTemporalBudget()
    {
        GameState game = StartGame();
        int budgetBefore = game.RemainingTemporalBudget;

        game = game.PlaceValue(0, 1, Solution[0, 1]).State;
        game = game.PlaceValue(0, 2, Solution[0, 2]).State;

        Assert.That(game.RemainingTemporalBudget, Is.EqualTo(budgetBefore));
    }

    // ---- refused placements ----------------------------------------------

    [Test]
    public void PlacingOnAnOccupiedCellIsRefused()
    {
        GameState game = StartGame();

        MoveResult result = game.PlaceValue(0, 0, 2);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Rejection, Is.EqualTo(MoveRejection.CellNotEmpty));
    }

    [TestCase(0)]
    [TestCase(5)]
    [TestCase(-1)]
    public void PlacingAValueOutsideTheGridsRangeIsRefused(int value)
    {
        Assert.That(StartGame().PlaceValue(0, 1, value).Rejection, Is.EqualTo(MoveRejection.ValueOutOfRange));
    }

    [TestCase(-1, 0)]
    [TestCase(0, 4)]
    [TestCase(4, 4)]
    public void PlacingOutsideTheGridIsRefused(int row, int column)
    {
        Assert.That(
            StartGame().PlaceValue(row, column, 1).Rejection,
            Is.EqualTo(MoveRejection.CoordinatesOutOfRange));
    }

    [Test]
    public void PlacingAValueThatBreaksAConstraintIsRefused()
    {
        // Column 1 already holds a 2 at r2, so 2 cannot go at r0c1.
        MoveResult result = StartGame().PlaceValue(0, 1, 2);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Rejection, Is.EqualTo(MoveRejection.ViolatesSudokuConstraint));
    }

    [Test]
    public void ARefusedPlacementLeavesTheStateExactlyAsItWas()
    {
        GameState game = StartGame();

        MoveResult result = game.PlaceValue(0, 1, 2);

        Assert.That(result.State, Is.SameAs(game));
        Assert.That(game.SelectedTimeline.StateCount, Is.EqualTo(1));
        Assert.That(game.SelectedTimeline.FrontierTime, Is.EqualTo(0));
        Assert.That(game.RemainingTemporalBudget, Is.EqualTo(game.Level.TemporalBudget));
    }

    [Test]
    public void PlacingAfterTheRunIsDecidedIsRefused()
    {
        GameState won = Levels.SolveSelectedTimeline(StartGame());

        Assert.That(won.Outcome, Is.EqualTo(GameOutcome.Won));
        Assert.That(won.PlaceValue(0, 0, 1).Rejection, Is.EqualTo(MoveRejection.GameAlreadyFinished));
    }

    // ---- validation mirrors application ----------------------------------

    [Test]
    public void ValidatePlacementAgreesWithPlaceValueForEveryCellAndValue()
    {
        GameState game = StartGame();
        BoardSize size = game.Level.Size;

        for (int row = 0; row < size.Side; row++)
        {
            for (int column = 0; column < size.Side; column++)
            {
                for (int value = 0; value <= size.Side + 1; value++)
                {
                    MoveRejection predicted = game.ValidatePlacement(row, column, value);
                    MoveResult actual = game.PlaceValue(row, column, value);

                    Assert.That(
                        actual.Rejection,
                        Is.EqualTo(predicted),
                        $"r{row}c{column} value {value}");
                    Assert.That(actual.Succeeded, Is.EqualTo(predicted == MoveRejection.None));
                }
            }
        }
    }

    [Test]
    public void ValidatePlacementDoesNotChangeAnything()
    {
        GameState game = StartGame();

        game.ValidatePlacement(0, 1, Solution[0, 1]);

        Assert.That(game.SelectedTimeline.StateCount, Is.EqualTo(1));
        Assert.That(game.Present, Is.EqualTo(0));
    }

    // ---- timeline switching ----------------------------------------------

    [Test]
    public void SwitchingToTheAlreadySelectedTimelineChangesNothing()
    {
        GameState game = StartGame().PlaceValue(0, 1, Solution[0, 1]).State;

        GameState switched = game.SelectTimeline(GameState.RootTimelineId);

        Assert.That(switched, Is.SameAs(game));
    }

    [Test]
    public void SwitchingTimelinesSpendsNoBudgetAndAdvancesNoTime()
    {
        GameState game = StartGame().PlaceValue(0, 1, Solution[0, 1]).State;

        GameState switched = game.SelectTimeline(GameState.RootTimelineId);

        Assert.That(switched.RemainingTemporalBudget, Is.EqualTo(game.RemainingTemporalBudget));
        Assert.That(switched.Present, Is.EqualTo(game.Present));
        Assert.That(switched.SelectedTimeline.FrontierTime, Is.EqualTo(game.SelectedTimeline.FrontierTime));
        Assert.That(switched.SelectedTimeline.StateCount, Is.EqualTo(game.SelectedTimeline.StateCount));
        Assert.That(switched.SelectedTimeline.Frontier, Is.EqualTo(game.SelectedTimeline.Frontier));
        Assert.That(switched.Outcome, Is.EqualTo(game.Outcome));
    }

    [Test]
    public void SwitchingToATimelineThatDoesNotExistIsRejected()
    {
        GameState game = StartGame();

        Assert.That(game.HasTimeline(GameState.RootTimelineId), Is.True);
        Assert.That(game.HasTimeline(99), Is.False);
        Assert.That(() => game.SelectTimeline(99), Throws.InstanceOf<ArgumentOutOfRangeException>());
        Assert.That(() => game.GetTimeline(99), Throws.InstanceOf<ArgumentOutOfRangeException>());
    }
}
