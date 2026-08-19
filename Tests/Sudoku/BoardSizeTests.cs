using System;
using FiveDSudoku.Core.Sudoku;

namespace FiveDSudoku.Tests;

/// <summary>
/// Geometry is a parameter, never a constant. These tests pin that down for the
/// two required sizes and for a non-square box shape.
/// </summary>
[TestFixture]
public sealed class BoardSizeTests
{
    [Test]
    public void FourByFourIsBuiltFromTwoByTwoBoxes()
    {
        BoardSize size = BoardSize.FourByFour;

        Assert.That(size.BoxWidth, Is.EqualTo(2));
        Assert.That(size.BoxHeight, Is.EqualTo(2));
        Assert.That(size.Side, Is.EqualTo(4));
        Assert.That(size.CellCount, Is.EqualTo(16));
        Assert.That(size.MinValue, Is.EqualTo(1));
        Assert.That(size.MaxValue, Is.EqualTo(4));
    }

    [Test]
    public void NineByNineIsBuiltFromThreeByThreeBoxes()
    {
        BoardSize size = BoardSize.NineByNine;

        Assert.That(size.BoxWidth, Is.EqualTo(3));
        Assert.That(size.BoxHeight, Is.EqualTo(3));
        Assert.That(size.Side, Is.EqualTo(9));
        Assert.That(size.CellCount, Is.EqualTo(81));
        Assert.That(size.MaxValue, Is.EqualTo(9));
    }

    [Test]
    public void NonSquareBoxShapesAreSupported()
    {
        BoardSize size = BoardSize.FromBoxShape(boxWidth: 3, boxHeight: 2);

        Assert.That(size.Side, Is.EqualTo(6));
        Assert.That(size.CellCount, Is.EqualTo(36));
    }

    [TestCase(0, 0, 0)]
    [TestCase(1, 3, 1)]
    [TestCase(2, 0, 2)]
    [TestCase(3, 3, 3)]
    public void BoxIndexFollowsTheBoxGridOnFourByFour(int row, int column, int expectedBox)
    {
        Assert.That(BoardSize.FourByFour.BoxIndex(row, column), Is.EqualTo(expectedBox));
    }

    [TestCase(0, 0, 0)]
    [TestCase(0, 8, 2)]
    [TestCase(4, 4, 4)]
    [TestCase(8, 0, 6)]
    [TestCase(8, 8, 8)]
    public void BoxIndexFollowsTheBoxGridOnNineByNine(int row, int column, int expectedBox)
    {
        Assert.That(BoardSize.NineByNine.BoxIndex(row, column), Is.EqualTo(expectedBox));
    }

    [Test]
    public void BoxIndexRespectsNonSquareBoxes()
    {
        BoardSize size = BoardSize.FromBoxShape(boxWidth: 3, boxHeight: 2);

        // Boxes are 3 wide and 2 tall, so the band containing rows 0-1 holds
        // boxes 0 and 1, and the band containing rows 2-3 holds boxes 2 and 3.
        Assert.That(size.BoxIndex(0, 2), Is.EqualTo(0));
        Assert.That(size.BoxIndex(1, 3), Is.EqualTo(1));
        Assert.That(size.BoxIndex(2, 0), Is.EqualTo(2));
        Assert.That(size.BoxIndex(5, 5), Is.EqualTo(5));
    }

    [Test]
    public void ValueRangeExcludesEmptyAndOutOfRangeValues()
    {
        BoardSize size = BoardSize.FourByFour;

        Assert.That(size.IsValueInRange(BoardSize.EmptyCell), Is.False);
        Assert.That(size.IsValueInRange(1), Is.True);
        Assert.That(size.IsValueInRange(4), Is.True);
        Assert.That(size.IsValueInRange(5), Is.False);
        Assert.That(size.IsStorableCellValue(BoardSize.EmptyCell), Is.True);
        Assert.That(size.IsStorableCellValue(5), Is.False);
    }

    [TestCase(0, 2)]
    [TestCase(2, 0)]
    public void BoxDimensionsBelowOneAreRejected(int boxWidth, int boxHeight)
    {
        Assert.That(
            () => BoardSize.FromBoxShape(boxWidth, boxHeight),
            Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void DegenerateOneByOneBoardIsRejected()
    {
        Assert.That(() => BoardSize.FromBoxShape(1, 1), Throws.ArgumentException);
    }

    [Test]
    public void BoardsLargerThanTheSupportedMaximumAreRejected()
    {
        Assert.That(() => BoardSize.FromBoxShape(8, 8), Throws.ArgumentException);
    }

    [Test]
    public void EqualBoxShapesAreEqualSizes()
    {
        Assert.That(BoardSize.FromBoxShape(3, 3), Is.EqualTo(BoardSize.NineByNine));
        Assert.That(BoardSize.FromBoxShape(3, 3).GetHashCode(), Is.EqualTo(BoardSize.NineByNine.GetHashCode()));
        Assert.That(BoardSize.FourByFour, Is.Not.EqualTo(BoardSize.NineByNine));
    }

    [Test]
    public void CoordinatesOutsideTheGridAreRejected()
    {
        BoardSize size = BoardSize.FourByFour;

        Assert.That(() => size.ValidateCoordinates(-1, 0), Throws.InstanceOf<ArgumentOutOfRangeException>());
        Assert.That(() => size.ValidateCoordinates(0, 4), Throws.InstanceOf<ArgumentOutOfRangeException>());
        Assert.That(() => size.ValidateCoordinates(3, 3), Throws.Nothing);
    }
}
