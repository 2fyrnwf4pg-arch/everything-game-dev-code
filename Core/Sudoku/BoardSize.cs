using System;

namespace FiveDSudoku.Core.Sudoku;

/// <summary>
/// Describes the geometry of a Sudoku grid: how wide and how tall a single box
/// is, and everything derived from that.
///
/// The engine is never hard-coded to one grid size. A 4x4 board is
/// <c>2x2</c> boxes, a 9x9 board is <c>3x3</c> boxes, and non-square box shapes
/// (for example 3x2 boxes giving a 6x6 grid) fall out of the same arithmetic.
/// </summary>
public sealed class BoardSize : IEquatable<BoardSize>
{
    /// <summary>Cell value used to mean "this cell has no value yet".</summary>
    public const int EmptyCell = 0;

    /// <summary>
    /// Largest grid side the engine accepts. Candidate sets are tracked as bits
    /// of an <see cref="int"/>, so a value of <c>Side</c> must fit in bit
    /// <c>Side - 1</c>.
    /// </summary>
    public const int MaxSupportedSide = 31;

    /// <summary>A 4x4 grid built from 2x2 boxes.</summary>
    public static readonly BoardSize FourByFour = new BoardSize(2, 2);

    /// <summary>A 9x9 grid built from 3x3 boxes.</summary>
    public static readonly BoardSize NineByNine = new BoardSize(3, 3);

    private BoardSize(int boxWidth, int boxHeight)
    {
        BoxWidth = boxWidth;
        BoxHeight = boxHeight;
        Side = boxWidth * boxHeight;
        CellCount = Side * Side;
        BoxesPerBand = Side / boxWidth;
    }

    /// <summary>Number of columns spanned by one box.</summary>
    public int BoxWidth { get; }

    /// <summary>Number of rows spanned by one box.</summary>
    public int BoxHeight { get; }

    /// <summary>Number of rows, of columns, and of distinct values in the grid.</summary>
    public int Side { get; }

    /// <summary>Total number of cells in the grid.</summary>
    public int CellCount { get; }

    /// <summary>Lowest legal cell value.</summary>
    public int MinValue => 1;

    /// <summary>Highest legal cell value.</summary>
    public int MaxValue => Side;

    /// <summary>Number of boxes sitting side by side in one horizontal band.</summary>
    private int BoxesPerBand { get; }

    /// <summary>
    /// Creates a board size from a box shape. <paramref name="boxWidth"/> is the
    /// number of columns per box, <paramref name="boxHeight"/> the number of rows.
    /// </summary>
    public static BoardSize FromBoxShape(int boxWidth, int boxHeight)
    {
        if (boxWidth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(boxWidth), boxWidth, "Box width must be at least 1.");
        }

        if (boxHeight < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(boxHeight), boxHeight, "Box height must be at least 1.");
        }

        int side = boxWidth * boxHeight;

        if (side < 2)
        {
            throw new ArgumentException("A board must have a side of at least 2.", nameof(boxWidth));
        }

        if (side > MaxSupportedSide)
        {
            throw new ArgumentException(
                $"A board side of {side} exceeds the supported maximum of {MaxSupportedSide}.",
                nameof(boxWidth));
        }

        return new BoardSize(boxWidth, boxHeight);
    }

    /// <summary>True when <paramref name="index"/> is a valid row or column index.</summary>
    public bool IsIndexInRange(int index) => index >= 0 && index < Side;

    /// <summary>True when <paramref name="value"/> is a placeable Sudoku value (empty does not count).</summary>
    public bool IsValueInRange(int value) => value >= MinValue && value <= MaxValue;

    /// <summary>True when <paramref name="value"/> may be stored in a cell, including <see cref="EmptyCell"/>.</summary>
    public bool IsStorableCellValue(int value) => value == EmptyCell || IsValueInRange(value);

    /// <summary>Row-major index of the cell at <paramref name="row"/>/<paramref name="column"/>.</summary>
    public int CellIndex(int row, int column) => (row * Side) + column;

    /// <summary>Index of the box containing the cell at <paramref name="row"/>/<paramref name="column"/>.</summary>
    public int BoxIndex(int row, int column) => ((row / BoxHeight) * BoxesPerBand) + (column / BoxWidth);

    /// <summary>Index of the first row of the box containing <paramref name="row"/>.</summary>
    public int BoxStartRow(int row) => (row / BoxHeight) * BoxHeight;

    /// <summary>Index of the first column of the box containing <paramref name="column"/>.</summary>
    public int BoxStartColumn(int column) => (column / BoxWidth) * BoxWidth;

    /// <summary>
    /// Throws when <paramref name="row"/>/<paramref name="column"/> is outside the grid.
    /// </summary>
    public void ValidateCoordinates(int row, int column)
    {
        if (!IsIndexInRange(row))
        {
            throw new ArgumentOutOfRangeException(nameof(row), row, $"Row must be in [0, {Side - 1}].");
        }

        if (!IsIndexInRange(column))
        {
            throw new ArgumentOutOfRangeException(nameof(column), column, $"Column must be in [0, {Side - 1}].");
        }
    }

    public bool Equals(BoardSize? other)
    {
        if (other is null)
        {
            return false;
        }

        return ReferenceEquals(this, other) || (BoxWidth == other.BoxWidth && BoxHeight == other.BoxHeight);
    }

    public override bool Equals(object? obj) => Equals(obj as BoardSize);

    public override int GetHashCode() => (BoxWidth * 397) ^ BoxHeight;

    public override string ToString() => $"{Side}x{Side} ({BoxWidth}x{BoxHeight} boxes)";
}
