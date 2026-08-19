using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;

namespace FiveDSudoku.Tests;

/// <summary>
/// The immediate classification rule, tested directly on boards.
///
/// Testing the classifier as a unit matters because not every verdict is
/// reachable through play. Finishing a board wins the run outright, so a branch
/// can never be born <see cref="TimelineStatus.Solved"/> — but the rule says what
/// a complete, valid board means, and that has to hold wherever it is applied.
/// </summary>
[TestFixture]
public sealed class TimelineClassifierTests
{
    [Test]
    public void ABoardWithAtLeastOneCompletionIsActive()
    {
        Assert.That(
            TimelineClassifier.ClassifyBoard(SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Unique4)),
            Is.EqualTo(TimelineStatus.Active));
        Assert.That(
            TimelineClassifier.ClassifyBoard(SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Multi4)),
            Is.EqualTo(TimelineStatus.Active));
        Assert.That(
            TimelineClassifier.ClassifyBoard(SudokuBoard.Parse(BoardSize.NineByNine, Puzzles.Unique9)),
            Is.EqualTo(TimelineStatus.Active));
    }

    [Test]
    public void ACompleteAndValidBoardIsSolvedRatherThanMerelyActive()
    {
        // A finished board technically has one completion — itself — so the solved
        // check has to come first or it would be reported as still playable.
        Assert.That(
            TimelineClassifier.ClassifyBoard(SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Solved4)),
            Is.EqualTo(TimelineStatus.Solved));
        Assert.That(
            TimelineClassifier.ClassifyBoard(SudokuBoard.Parse(BoardSize.NineByNine, Puzzles.Unique9Solution)),
            Is.EqualTo(TimelineStatus.Solved));
    }

    [Test]
    public void ALocallyValidBoardWithNoCompletionIsDead()
    {
        SudokuBoard zero4 = SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Zero4);
        SudokuBoard zero9 = SudokuBoard.Parse(BoardSize.NineByNine, Puzzles.Zero9);

        Assert.That(zero4.IsValid(), Is.True, "the fixture must break no constraint");
        Assert.That(zero9.IsValid(), Is.True, "the fixture must break no constraint");

        Assert.That(TimelineClassifier.ClassifyBoard(zero4), Is.EqualTo(TimelineStatus.Dead));
        Assert.That(TimelineClassifier.ClassifyBoard(zero9), Is.EqualTo(TimelineStatus.Dead));
    }

    [Test]
    public void ABoardThatBreaksAConstraintIsDead()
    {
        Assert.That(
            TimelineClassifier.ClassifyBoard(SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Invalid4)),
            Is.EqualTo(TimelineStatus.Dead));
    }

    [Test]
    public void AStuckButIncompleteBoardIsDead()
    {
        Assert.That(
            TimelineClassifier.ClassifyBoard(SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Blocked4)),
            Is.EqualTo(TimelineStatus.Dead));
    }

    [Test]
    public void ClassifyingNeedsABoard()
    {
        Assert.That(() => TimelineClassifier.ClassifyBoard(null!), Throws.ArgumentNullException);
    }

    // ---- classification after an ordinary placement ----------------------

    [Test]
    public void AfterAPlacementACompleteAndValidBoardIsSolved()
    {
        Assert.That(
            TimelineClassifier.ClassifyAfterPlacement(SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Solved4)),
            Is.EqualTo(TimelineStatus.Solved));
    }

    [Test]
    public void AfterAPlacementABoardWithNoLegalMoveLeftIsDead()
    {
        Assert.That(
            TimelineClassifier.ClassifyAfterPlacement(SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Blocked4)),
            Is.EqualTo(TimelineStatus.Dead));
    }

    [Test]
    public void AfterAPlacementABoardThatStillOffersMovesIsActive()
    {
        Assert.That(
            TimelineClassifier.ClassifyAfterPlacement(SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Unique4)),
            Is.EqualTo(TimelineStatus.Active));
    }

    [Test]
    public void OrdinaryPlayAsksALocalQuestionWhereBranchingAsksAGlobalOne()
    {
        // Zero4 offers no completion at all, but it does offer legal placements.
        // Branching on such a board produces a dead timeline immediately; reaching
        // it through ordinary play leaves the timeline playable, so the player
        // discovers the mistake by playing rather than by being told.
        SudokuBoard board = SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Zero4);

        Assert.That(board.HasAnyLegalPlacement(), Is.True);
        Assert.That(TimelineClassifier.ClassifyBoard(board), Is.EqualTo(TimelineStatus.Dead));
        Assert.That(TimelineClassifier.ClassifyAfterPlacement(board), Is.EqualTo(TimelineStatus.Active));
    }

    [Test]
    public void NeitherClassificationLeavesAnUnplayableBoardActive()
    {
        SudokuBoard blocked = SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Blocked4);

        Assert.That(TimelineClassifier.ClassifyBoard(blocked), Is.Not.EqualTo(TimelineStatus.Active));
        Assert.That(TimelineClassifier.ClassifyAfterPlacement(blocked), Is.Not.EqualTo(TimelineStatus.Active));
    }

    [Test]
    public void ClassifyingAfterAPlacementNeedsABoard()
    {
        Assert.That(() => TimelineClassifier.ClassifyAfterPlacement(null!), Throws.ArgumentNullException);
    }

    [Test]
    public void ClassificationNeverReportsAnImpossibleBoardAsActive()
    {
        // The rule that actually matters: whatever else it decides, a board with no
        // completion must never come back playable.
        foreach (string text in new[] { Puzzles.Zero4, Puzzles.Invalid4, Puzzles.Blocked4 })
        {
            Assert.That(
                TimelineClassifier.ClassifyBoard(SudokuBoard.Parse(BoardSize.FourByFour, text)),
                Is.Not.EqualTo(TimelineStatus.Active),
                text);
        }
    }
}
