using System;
using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Level;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;

namespace FiveDSudoku.Tests;

/// <summary>
/// Timeline structure, frontier advancement, and the immutability of history.
/// Everything goes through the public game API — a timeline cannot be built or
/// advanced from outside the rules.
/// </summary>
[TestFixture]
public sealed class TimelineTests
{
    private static readonly SudokuBoard Solution =
        SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Unique4Solution);

    private static GameState StartGame() => GameState.Start(Levels.SolvableFourByFour());

    /// <summary>Plays the solution value at a cell and fails loudly if it is refused.</summary>
    private static GameState Play(GameState game, int row, int column)
    {
        MoveResult result = game.PlaceValue(row, column, Solution[row, column]);

        Assert.That(result.Succeeded, Is.True, $"r{row}c{column} was refused: {result.Rejection}");

        return result.State;
    }

    [Test]
    public void RootTimelineStartsAtTimeZeroWithNoParent()
    {
        Timeline root = StartGame().SelectedTimeline;

        Assert.That(root.Id, Is.EqualTo(GameState.RootTimelineId));
        Assert.That(root.ParentId, Is.Null);
        Assert.That(root.BranchTime, Is.Null);
        Assert.That(root.FirstStateTime, Is.EqualTo(0));
        Assert.That(root.FrontierTime, Is.EqualTo(0));
        Assert.That(root.StateCount, Is.EqualTo(1));
        Assert.That(root.Status, Is.EqualTo(TimelineStatus.Active));
        Assert.That(root.OccupiesActiveSlot, Is.True);
    }

    [Test]
    public void RootFrontierIsTheLevelsStartingBoard()
    {
        LevelDefinition level = Levels.SolvableFourByFour();

        Assert.That(GameState.Start(level).SelectedTimeline.Frontier, Is.EqualTo(level.StartingBoard));
    }

    [Test]
    public void EachPlacementAdvancesTheFrontierByOne()
    {
        GameState game = StartGame();

        game = Play(game, 0, 1);
        Assert.That(game.SelectedTimeline.FrontierTime, Is.EqualTo(1));
        Assert.That(game.SelectedTimeline.StateCount, Is.EqualTo(2));

        game = Play(game, 0, 2);
        Assert.That(game.SelectedTimeline.FrontierTime, Is.EqualTo(2));
        Assert.That(game.SelectedTimeline.StateCount, Is.EqualTo(3));

        game = Play(game, 0, 3);
        Assert.That(game.SelectedTimeline.FrontierTime, Is.EqualTo(3));
        Assert.That(game.SelectedTimeline.StateCount, Is.EqualTo(4));
    }

    [Test]
    public void HistoryKeepsEveryStateThatWasEverPlayed()
    {
        GameState game = StartGame();
        SudokuBoard start = game.SelectedTimeline.Frontier;

        game = Play(game, 0, 1);
        SudokuBoard afterFirst = game.SelectedTimeline.Frontier;

        game = Play(game, 0, 2);
        SudokuBoard afterSecond = game.SelectedTimeline.Frontier;

        Timeline timeline = game.SelectedTimeline;

        Assert.That(timeline.StateAt(0), Is.EqualTo(start));
        Assert.That(timeline.StateAt(1), Is.EqualTo(afterFirst));
        Assert.That(timeline.StateAt(2), Is.EqualTo(afterSecond));
        Assert.That(timeline.States, Has.Count.EqualTo(3));
    }

    [Test]
    public void PlayingAMoveDoesNotTouchTheEarlierTimelineObject()
    {
        GameState before = StartGame();
        Timeline rootBefore = before.SelectedTimeline;

        GameState after = Play(before, 0, 1);

        Assert.That(after.SelectedTimeline, Is.Not.SameAs(rootBefore));
        Assert.That(rootBefore.StateCount, Is.EqualTo(1), "the earlier timeline object must not grow");
        Assert.That(rootBefore.FrontierTime, Is.EqualTo(0));
        Assert.That(before.SelectedTimeline.StateCount, Is.EqualTo(1), "the earlier game state must not change");
    }

    [Test]
    public void HistoricalStatesStayIdenticalAsPlayContinues()
    {
        GameState game = StartGame();
        SudokuBoard start = game.SelectedTimeline.StateAt(0);

        game = Play(game, 0, 1);
        game = Play(game, 0, 2);
        game = Play(game, 0, 3);

        Assert.That(game.SelectedTimeline.StateAt(0), Is.EqualTo(start));
        Assert.That(game.SelectedTimeline.StateAt(0).FilledCellCount, Is.EqualTo(start.FilledCellCount));
    }

    [Test]
    public void EachStateDiffersFromItsPredecessorByExactlyOneCell()
    {
        GameState game = StartGame();

        game = Play(game, 0, 1);
        game = Play(game, 0, 2);
        game = Play(game, 0, 3);

        Timeline timeline = game.SelectedTimeline;

        for (int time = 1; time <= timeline.FrontierTime; time++)
        {
            SudokuBoard previous = timeline.StateAt(time - 1);
            SudokuBoard current = timeline.StateAt(time);

            Assert.That(
                current.FilledCellCount,
                Is.EqualTo(previous.FilledCellCount + 1),
                $"state T{time} must add exactly one value");
        }
    }

    [Test]
    public void TimesOutsideTheTimelineAreRejected()
    {
        Timeline root = StartGame().SelectedTimeline;

        Assert.That(root.ContainsTime(0), Is.True);
        Assert.That(root.ContainsTime(1), Is.False);
        Assert.That(root.ContainsTime(-1), Is.False);
        Assert.That(() => root.StateAt(1), Throws.InstanceOf<ArgumentOutOfRangeException>());
        Assert.That(() => root.StateAt(-1), Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void TimelineIsCapableOfFurtherPlayWhileALegalMoveRemains()
    {
        Assert.That(StartGame().SelectedTimeline.IsCapableOfFurtherPlay, Is.True);

        GameState stuck = GameState.Start(Levels.FromText("stuck", BoardSize.FourByFour, Puzzles.Blocked4));

        Assert.That(stuck.SelectedTimeline.IsCapableOfFurtherPlay, Is.False);
    }
}
