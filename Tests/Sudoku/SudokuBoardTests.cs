using System;
using FiveDSudoku.Core.Sudoku;

namespace FiveDSudoku.Tests;

/// <summary>
/// Board construction, immutability, and the row/column/box legality rules.
/// </summary>
[TestFixture]
public sealed class SudokuBoardTests
{
    [Test]
    public void EmptyBoardHasNoFilledCells()
    {
        SudokuBoard board = SudokuBoard.Empty(BoardSize.FourByFour);

        Assert.That(board.FilledCellCount, Is.EqualTo(0));
        Assert.That(board.IsComplete, Is.False);
        Assert.That(board.IsValid(), Is.True);
        Assert.That(board.IsSolved(), Is.False);
    }

    [Test]
    public void ParseReadsAGridWithSeparators()
    {
        SudokuBoard board = SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Solved4);

        Assert.That(board[0, 0], Is.EqualTo(1));
        Assert.That(board[0, 3], Is.EqualTo(4));
        Assert.That(board[3, 0], Is.EqualTo(4));
        Assert.That(board[3, 3], Is.EqualTo(1));
        Assert.That(board.FilledCellCount, Is.EqualTo(16));
    }

    [Test]
    public void ParseTreatsDotAndZeroAsEmpty()
    {
        SudokuBoard board = SudokuBoard.Parse(BoardSize.FourByFour, "1.0. .... .... ....");

        Assert.That(board[0, 0], Is.EqualTo(1));
        Assert.That(board.IsEmpty(0, 1), Is.True);
        Assert.That(board.IsEmpty(0, 2), Is.True);
        Assert.That(board.FilledCellCount, Is.EqualTo(1));
    }

    [TestCase("123")]
    [TestCase("1234 1234 1234 1234 1")]
    [TestCase("1234 1234 1234 12x4")]
    [TestCase("1234 1234 1234 1235")]
    public void ParseRejectsMalformedInput(string text)
    {
        Assert.That(() => SudokuBoard.Parse(BoardSize.FourByFour, text), Throws.InstanceOf<FormatException>());
    }

    [Test]
    public void CreateRejectsTheWrongNumberOfCells()
    {
        Assert.That(
            () => SudokuBoard.Create(BoardSize.FourByFour, new[] { 1, 2, 3 }),
            Throws.ArgumentException);
    }

    [Test]
    public void CreateRejectsOutOfRangeValues()
    {
        int[] cells = new int[16];
        cells[0] = 5;

        Assert.That(
            () => SudokuBoard.Create(BoardSize.FourByFour, cells),
            Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void CreateCopiesTheInputSoLaterMutationCannotLeakIn()
    {
        int[] cells = new int[16];
        cells[0] = 1;

        SudokuBoard board = SudokuBoard.Create(BoardSize.FourByFour, cells);
        cells[0] = 4;

        Assert.That(board[0, 0], Is.EqualTo(1));
    }

    [Test]
    public void ToArrayHandsOutACopy()
    {
        SudokuBoard board = SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Unique4);

        int[] cells = board.ToArray();
        cells[0] = 4;

        Assert.That(board[0, 0], Is.EqualTo(1));
    }

    [Test]
    public void WithValueProducesANewBoardAndLeavesTheOriginalUntouched()
    {
        SudokuBoard original = SudokuBoard.Empty(BoardSize.FourByFour);

        SudokuBoard updated = original.WithValue(1, 2, 3);

        Assert.That(updated, Is.Not.SameAs(original));
        Assert.That(updated[1, 2], Is.EqualTo(3));
        Assert.That(original[1, 2], Is.EqualTo(BoardSize.EmptyCell));
        Assert.That(original.FilledCellCount, Is.EqualTo(0));
        Assert.That(updated.FilledCellCount, Is.EqualTo(1));
    }

    [Test]
    public void WithValueReturnsTheSameInstanceWhenNothingChanges()
    {
        SudokuBoard board = SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Unique4);

        Assert.That(board.WithValue(0, 0, 1), Is.SameAs(board));
    }

    [Test]
    public void WithValueRejectsOutOfRangeCoordinatesAndValues()
    {
        SudokuBoard board = SudokuBoard.Empty(BoardSize.FourByFour);

        Assert.That(() => board.WithValue(4, 0, 1), Throws.InstanceOf<ArgumentOutOfRangeException>());
        Assert.That(() => board.WithValue(0, 0, 5), Throws.InstanceOf<ArgumentOutOfRangeException>());
        Assert.That(() => board.WithValue(0, 0, -1), Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void BoardsWithTheSameCellsAreEqual()
    {
        SudokuBoard first = SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Unique4);
        SudokuBoard second = SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Unique4);

        Assert.That(first, Is.EqualTo(second));
        Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        Assert.That(first, Is.Not.EqualTo(second.WithValue(0, 1, 2)));
    }

    [Test]
    public void BoardsOfDifferentSizesAreNeverEqual()
    {
        Assert.That(
            SudokuBoard.Empty(BoardSize.FourByFour),
            Is.Not.EqualTo(SudokuBoard.Empty(BoardSize.NineByNine)));
    }

    // ---- validity --------------------------------------------------------

    [Test]
    public void CompleteCorrectBoardIsValidAndSolved()
    {
        SudokuBoard board = SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Solved4);

        Assert.That(board.IsValid(), Is.True);
        Assert.That(board.IsComplete, Is.True);
        Assert.That(board.IsSolved(), Is.True);
    }

    [Test]
    public void PartiallyFilledBoardWithoutConflictsIsValidButNotSolved()
    {
        SudokuBoard board = SudokuBoard.Parse(BoardSize.NineByNine, Puzzles.Unique9);

        Assert.That(board.IsValid(), Is.True);
        Assert.That(board.IsComplete, Is.False);
        Assert.That(board.IsSolved(), Is.False);
    }

    // Each case isolates exactly one constraint: the two 1s share only the row,
    // only the column, or only the box. A pair that shares two constraints at
    // once would still be rejected if one of the three checks were missing.
    [TestCase("1.|1.\n..|..\n..|..\n..|..", TestName = "duplicate in row only")]
    [TestCase("1.|..\n..|..\n1.|..\n..|..", TestName = "duplicate in column only")]
    [TestCase("1.|..\n.1|..\n..|..\n..|..", TestName = "duplicate in box only")]
    public void ConflictingBoardsAreInvalid(string text)
    {
        SudokuBoard board = SudokuBoard.Parse(BoardSize.FourByFour, text);

        Assert.That(board.IsValid(), Is.False);
        Assert.That(board.IsSolved(), Is.False);
    }

    [Test]
    public void ConflictingNineByNineBoardIsInvalid()
    {
        SudokuBoard board = SudokuBoard.Parse(BoardSize.NineByNine, Puzzles.Invalid9);

        Assert.That(board.IsValid(), Is.False);
    }

    [Test]
    public void CompleteBoardWithAConflictIsNotSolved()
    {
        SudokuBoard board = SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Solved4)
            .WithValue(0, 0, BoardSize.EmptyCell)
            .WithValue(0, 0, 2);

        Assert.That(board.IsComplete, Is.True);
        Assert.That(board.IsValid(), Is.False);
        Assert.That(board.IsSolved(), Is.False);
    }

    // ---- placement legality ---------------------------------------------

    [Test]
    public void PlacementIsLegalWhenNoPeerHoldsTheValue()
    {
        SudokuBoard board = SudokuBoard.Empty(BoardSize.FourByFour);

        Assert.That(board.IsPlacementLegal(0, 0, 1), Is.True);
    }

    [Test]
    public void PlacementIsIllegalWhenTheRowAlreadyHoldsTheValue()
    {
        SudokuBoard board = SudokuBoard.Empty(BoardSize.FourByFour).WithValue(0, 0, 1);

        Assert.That(board.IsPlacementLegal(0, 3, 1), Is.False);
    }

    [Test]
    public void PlacementIsIllegalWhenTheColumnAlreadyHoldsTheValue()
    {
        SudokuBoard board = SudokuBoard.Empty(BoardSize.FourByFour).WithValue(0, 0, 1);

        Assert.That(board.IsPlacementLegal(3, 0, 1), Is.False);
    }

    [Test]
    public void PlacementIsIllegalWhenTheBoxAlreadyHoldsTheValue()
    {
        SudokuBoard board = SudokuBoard.Empty(BoardSize.FourByFour).WithValue(0, 0, 1);

        Assert.That(board.IsPlacementLegal(1, 1, 1), Is.False);
    }

    [Test]
    public void PlacementIsIllegalOnAnOccupiedCell()
    {
        SudokuBoard board = SudokuBoard.Empty(BoardSize.FourByFour).WithValue(0, 0, 1);

        Assert.That(board.IsPlacementLegal(0, 0, 2), Is.False);
    }

    [TestCase(0)]
    [TestCase(5)]
    [TestCase(-1)]
    public void PlacementIsIllegalForValuesOutsideTheRange(int value)
    {
        SudokuBoard board = SudokuBoard.Empty(BoardSize.FourByFour);

        Assert.That(board.IsPlacementLegal(0, 0, value), Is.False);
    }

    [TestCase(-1, 0)]
    [TestCase(0, 4)]
    public void PlacementIsIllegalOutsideTheGridRatherThanThrowing(int row, int column)
    {
        SudokuBoard board = SudokuBoard.Empty(BoardSize.FourByFour);

        Assert.That(board.IsPlacementLegal(row, column, 1), Is.False);
    }

    [Test]
    public void PlacementLegalityUsesBoxShapeNotJustRowAndColumn()
    {
        // 6x6 boxes are 3 wide and 2 tall. r1c2 shares a box with r0c0 but not
        // its row or column, so a repeat of 1 there must still be rejected.
        BoardSize size = BoardSize.FromBoxShape(boxWidth: 3, boxHeight: 2);
        SudokuBoard board = SudokuBoard.Empty(size).WithValue(0, 0, 1);

        Assert.That(board.IsPlacementLegal(1, 2, 1), Is.False);
        Assert.That(board.IsPlacementLegal(2, 3, 1), Is.True);
    }

    [Test]
    public void NineByNinePlacementRespectsAllThreeConstraints()
    {
        SudokuBoard board = SudokuBoard.Parse(BoardSize.NineByNine, Puzzles.Unique9);

        Assert.That(board.IsPlacementLegal(0, 2, 5), Is.False, "5 already sits in the same row and box");
        Assert.That(board.IsPlacementLegal(0, 2, 6), Is.False, "6 already sits in the same column");
        Assert.That(board.IsPlacementLegal(0, 2, 4), Is.True);
    }

    [Test]
    public void ToStringRendersTheGridWithBoxSeparators()
    {
        SudokuBoard board = SudokuBoard.Parse(BoardSize.FourByFour, Puzzles.Unique4);

        Assert.That(
            board.ToString(),
            Is.EqualTo("1 . | . .\n. . | 1 .\n----+----\n. 2 | . .\n. . | . 3\n"));
    }
}
