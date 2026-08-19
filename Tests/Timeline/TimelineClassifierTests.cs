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
