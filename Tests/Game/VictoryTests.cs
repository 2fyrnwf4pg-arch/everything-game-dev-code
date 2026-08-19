using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;

namespace FiveDSudoku.Tests;

/// <summary>
/// Victory and game over.
/// </summary>
[TestFixture]
public sealed class VictoryTests
{
    [Test]
    public void SolvingTheRootTimelineWinsTheLevel()
    {
        GameState game = Levels.SolveSelectedTimeline(GameState.Start(Levels.SolvableFourByFour()));

        Assert.That(game.SelectedTimeline.Frontier.IsSolved(), Is.True);
        Assert.That(game.SelectedTimeline.Status, Is.EqualTo(TimelineStatus.Solved));
        Assert.That(game.Outcome, Is.EqualTo(GameOutcome.Won));
        Assert.That(game.IsWon, Is.True);
        Assert.That(game.IsGameOver, Is.False);
    }

    [Test]
    public void TheRunIsNotWonWhileTheBoardIsIncomplete()
    {
        GameState game = GameState.Start(Levels.SolvableFourByFour());
        SudokuBoard solution = SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Unique4Solution);

        game = game.PlaceValue(0, 1, solution[0, 1]).State;

        Assert.That(game.Outcome, Is.EqualTo(GameOutcome.InProgress));
        Assert.That(game.IsWon, Is.False);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(5)]
    [TestCase(12)]
    [TestCase(99)]
    public void FinalDepthNeverBlocksACompleteValidSolutionFromWinning(int finalDepth)
    {
        // FinalDepth is a pacing parameter, not a win condition: solving the puzzle
        // wins whether the run was shorter or longer than the configured depth.
        GameState game = Levels.SolveSelectedTimeline(
            GameState.Start(Levels.SolvableFourByFour(finalDepth: finalDepth)));

        Assert.That(game.Level.FinalDepth, Is.EqualTo(finalDepth));
        Assert.That(game.SelectedTimeline.FrontierTime, Is.EqualTo(12));
        Assert.That(game.Outcome, Is.EqualTo(GameOutcome.Won));
    }

    [Test]
    public void ALevelThatStartsSolvedIsWonImmediately()
    {
        GameState game = GameState.Start(
            Levels.FromText("already-solved", BoardSize.FourByFour, Puzzles.Solved4));

        Assert.That(game.SelectedTimeline.Status, Is.EqualTo(TimelineStatus.Solved));
        Assert.That(game.Outcome, Is.EqualTo(GameOutcome.Won));
    }

    // ---- game over -------------------------------------------------------

    [Test]
    public void ARunWithNoLegalMoveLeftIsOver()
    {
        GameState game = GameState.Start(Levels.FromText("stuck", BoardSize.FourByFour, Puzzles.Blocked4));

        Assert.That(game.SelectedTimeline.Frontier.IsValid(), Is.True);
        Assert.That(game.SelectedTimeline.Frontier.IsComplete, Is.False);
        Assert.That(game.Outcome, Is.EqualTo(GameOutcome.GameOver));
        Assert.That(game.IsGameOver, Is.True);
        Assert.That(game.IsWon, Is.False);
    }

    [Test]
    public void PlayingIntoADeadEndEndsTheRun()
    {
        GameState game = GameState.Start(
            Levels.FromText("almost-stuck", BoardSize.FourByFour, Puzzles.AlmostBlocked4));

        Assert.That(game.Outcome, Is.EqualTo(GameOutcome.InProgress));

        MoveResult result = game.PlaceValue(0, 0, 4);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.State.Outcome, Is.EqualTo(GameOutcome.GameOver));
    }

    [Test]
    public void ARunIsNotOverWhileALegalMoveRemains()
    {
        GameState game = GameState.Start(Levels.SolvableFourByFour());

        Assert.That(game.Outcome, Is.EqualTo(GameOutcome.InProgress));
        Assert.That(game.IsGameOver, Is.False);
    }

    [Test]
    public void AFinishedRunRefusesFurtherMovesRegardlessOfHowItEnded()
    {
        GameState over = GameState.Start(Levels.FromText("stuck", BoardSize.FourByFour, Puzzles.Blocked4));

        Assert.That(over.PlaceValue(1, 1, 2).Rejection, Is.EqualTo(MoveRejection.GameAlreadyFinished));
    }
}
