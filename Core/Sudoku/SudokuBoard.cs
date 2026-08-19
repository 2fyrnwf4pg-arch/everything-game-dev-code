using System;
using System.Collections.Generic;
using System.Text;

namespace FiveDSudoku.Core.Sudoku;

/// <summary>
/// An immutable Sudoku grid.
///
/// A board is a value object: it is never mutated in place. <see cref="WithValue"/>
/// returns a new board and leaves this one untouched, which is what lets later
/// phases keep whole histories of states around and inspect them safely.
///
/// The board owns the Sudoku constraint rules (row, column, box). Those rules are
/// always local to this single grid — they know nothing about time or timelines.
/// Gameplay rules layered on top (which cells a player may touch, and when) live
/// elsewhere; the board only answers what Sudoku itself permits.
/// </summary>
public sealed class SudokuBoard : IEquatable<SudokuBoard>
{
    private const string IgnoredParseCharacters = " \t\r\n|-+_/";

    private readonly int[] _cells;

    /// <summary>Takes ownership of <paramref name="cells"/>; callers must not keep a reference.</summary>
    private SudokuBoard(BoardSize size, int[] cells)
    {
        Size = size;
        _cells = cells;
    }

    /// <summary>Geometry of this grid.</summary>
    public BoardSize Size { get; }

    /// <summary>Value at the given coordinates, or <see cref="BoardSize.EmptyCell"/>.</summary>
    public int this[int row, int column]
    {
        get
        {
            Size.ValidateCoordinates(row, column);
            return _cells[Size.CellIndex(row, column)];
        }
    }

    /// <summary>Number of cells that currently hold a value.</summary>
    public int FilledCellCount
    {
        get
        {
            int filled = 0;

            for (int index = 0; index < _cells.Length; index++)
            {
                if (_cells[index] != BoardSize.EmptyCell)
                {
                    filled++;
                }
            }

            return filled;
        }
    }

    /// <summary>True when every cell holds a value. Says nothing about correctness.</summary>
    public bool IsComplete => FilledCellCount == Size.CellCount;

    /// <summary>An all-empty board of the given size.</summary>
    public static SudokuBoard Empty(BoardSize size)
    {
        if (size is null)
        {
            throw new ArgumentNullException(nameof(size));
        }

        return new SudokuBoard(size, new int[size.CellCount]);
    }

    /// <summary>
    /// Creates a board from row-major cell values. Values must be in range or
    /// <see cref="BoardSize.EmptyCell"/>; the input is copied.
    /// </summary>
    public static SudokuBoard Create(BoardSize size, IReadOnlyList<int> cells)
    {
        if (size is null)
        {
            throw new ArgumentNullException(nameof(size));
        }

        if (cells is null)
        {
            throw new ArgumentNullException(nameof(cells));
        }

        if (cells.Count != size.CellCount)
        {
            throw new ArgumentException(
                $"Expected {size.CellCount} cells for a {size} board but got {cells.Count}.",
                nameof(cells));
        }

        int[] copy = new int[size.CellCount];

        for (int index = 0; index < copy.Length; index++)
        {
            int value = cells[index];

            if (!size.IsStorableCellValue(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cells),
                    value,
                    $"Cell value must be {BoardSize.EmptyCell} (empty) or in [{size.MinValue}, {size.MaxValue}].");
            }

            copy[index] = value;
        }

        return new SudokuBoard(size, copy);
    }

    /// <summary>
    /// Parses a board from text. Digits <c>1</c>-<c>9</c> and letters <c>A</c>
    /// onwards (for values above 9) are cell values; <c>.</c> and <c>0</c> are
    /// empty cells. Whitespace and the separator characters <c>| - + _ /</c> are
    /// ignored, so fixtures can be written as readable grids.
    /// </summary>
    public static SudokuBoard Parse(BoardSize size, string text)
    {
        if (size is null)
        {
            throw new ArgumentNullException(nameof(size));
        }

        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        int[] cells = new int[size.CellCount];
        int written = 0;

        foreach (char character in text)
        {
            if (IgnoredParseCharacters.IndexOf(character) >= 0)
            {
                continue;
            }

            if (written == size.CellCount)
            {
                throw new FormatException($"Text describes more than {size.CellCount} cells for a {size} board.");
            }

            cells[written++] = ParseCell(size, character);
        }

        if (written != size.CellCount)
        {
            throw new FormatException(
                $"Text describes {written} cells but a {size} board needs {size.CellCount}.");
        }

        return new SudokuBoard(size, cells);
    }

    /// <summary>True when the cell holds no value.</summary>
    public bool IsEmpty(int row, int column) => this[row, column] == BoardSize.EmptyCell;

    /// <summary>
    /// Returns a new board with <paramref name="value"/> at the given coordinates.
    /// This board is not modified. Passing <see cref="BoardSize.EmptyCell"/> clears
    /// the cell — that is a board-construction primitive, not a gameplay undo.
    /// </summary>
    public SudokuBoard WithValue(int row, int column, int value)
    {
        Size.ValidateCoordinates(row, column);

        if (!Size.IsStorableCellValue(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Cell value must be {BoardSize.EmptyCell} (empty) or in [{Size.MinValue}, {Size.MaxValue}].");
        }

        int index = Size.CellIndex(row, column);

        if (_cells[index] == value)
        {
            return this;
        }

        int[] copy = (int[])_cells.Clone();
        copy[index] = value;

        return new SudokuBoard(Size, copy);
    }

    /// <summary>
    /// True when <paramref name="value"/> may be placed at the given coordinates:
    /// the cell must be empty, the value in range, and no cell sharing its row,
    /// column, or box may already hold that value.
    /// </summary>
    public bool IsPlacementLegal(int row, int column, int value)
    {
        if (!Size.IsIndexInRange(row) || !Size.IsIndexInRange(column) || !Size.IsValueInRange(value))
        {
            return false;
        }

        if (_cells[Size.CellIndex(row, column)] != BoardSize.EmptyCell)
        {
            return false;
        }

        return !ConflictsWithPeers(row, column, value);
    }

    /// <summary>
    /// True when no constraint is violated by the values currently on the board.
    /// An incomplete board can be valid; validity says nothing about whether the
    /// board can still be completed.
    /// </summary>
    public bool IsValid()
    {
        int side = Size.Side;

        return TryBuildConstraintMasks(new int[side], new int[side], new int[side]);
    }

    /// <summary>
    /// Fills per-row, per-column, and per-box bitmasks of the values already used,
    /// and reports whether the board is free of violations. Returns false as soon
    /// as a value repeats within a row, column, or box, leaving the masks partially
    /// filled.
    ///
    /// This is the single implementation of the Sudoku constraint rules for whole
    /// boards: <see cref="IsValid"/> and the solver both go through it, so the two
    /// can never drift apart. The masks are exactly what a solver needs to start
    /// searching, which is why they are handed out instead of recomputed.
    /// </summary>
    internal bool TryBuildConstraintMasks(int[] rowMask, int[] columnMask, int[] boxMask)
    {
        int side = Size.Side;

        for (int row = 0; row < side; row++)
        {
            for (int column = 0; column < side; column++)
            {
                int value = _cells[Size.CellIndex(row, column)];

                if (value == BoardSize.EmptyCell)
                {
                    continue;
                }

                int bit = 1 << (value - 1);
                int box = Size.BoxIndex(row, column);

                if ((rowMask[row] & bit) != 0 || (columnMask[column] & bit) != 0 || (boxMask[box] & bit) != 0)
                {
                    return false;
                }

                rowMask[row] |= bit;
                columnMask[column] |= bit;
                boxMask[box] |= bit;
            }
        }

        return true;
    }

    /// <summary>True when the board is both complete and free of violations.</summary>
    public bool IsSolved() => IsComplete && IsValid();

    /// <summary>Row-major copy of the cell values.</summary>
    public int[] ToArray() => (int[])_cells.Clone();

    public bool Equals(SudokuBoard? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (!Size.Equals(other.Size))
        {
            return false;
        }

        for (int index = 0; index < _cells.Length; index++)
        {
            if (_cells[index] != other._cells[index])
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as SudokuBoard);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Size.GetHashCode();

            for (int index = 0; index < _cells.Length; index++)
            {
                hash = (hash * 31) + _cells[index];
            }

            return hash;
        }
    }

    /// <summary>Renders the grid with box separators, using <c>.</c> for empty cells.</summary>
    public override string ToString()
    {
        StringBuilder builder = new StringBuilder();
        int side = Size.Side;

        for (int row = 0; row < side; row++)
        {
            if (row > 0 && row % Size.BoxHeight == 0)
            {
                builder.Append(HorizontalSeparator()).Append('\n');
            }

            for (int column = 0; column < side; column++)
            {
                if (column > 0)
                {
                    builder.Append(column % Size.BoxWidth == 0 ? " | " : " ");
                }

                builder.Append(FormatCell(_cells[Size.CellIndex(row, column)]));
            }

            builder.Append('\n');
        }

        return builder.ToString();
    }

    private static int ParseCell(BoardSize size, char character)
    {
        int value;

        if (character == '.' || character == '0')
        {
            return BoardSize.EmptyCell;
        }

        if (character >= '1' && character <= '9')
        {
            value = character - '0';
        }
        else if (character >= 'A' && character <= 'Z')
        {
            value = character - 'A' + 10;
        }
        else if (character >= 'a' && character <= 'z')
        {
            value = character - 'a' + 10;
        }
        else
        {
            throw new FormatException($"Unexpected character '{character}' in board text.");
        }

        if (!size.IsValueInRange(value))
        {
            throw new FormatException($"Value {value} is out of range for a {size} board.");
        }

        return value;
    }

    private static char FormatCell(int value)
    {
        if (value == BoardSize.EmptyCell)
        {
            return '.';
        }

        return value <= 9 ? (char)('0' + value) : (char)('A' + value - 10);
    }

    private bool ConflictsWithPeers(int row, int column, int value)
    {
        int side = Size.Side;

        for (int index = 0; index < side; index++)
        {
            if (index != column && _cells[Size.CellIndex(row, index)] == value)
            {
                return true;
            }

            if (index != row && _cells[Size.CellIndex(index, column)] == value)
            {
                return true;
            }
        }

        int startRow = Size.BoxStartRow(row);
        int startColumn = Size.BoxStartColumn(column);

        for (int boxRow = startRow; boxRow < startRow + Size.BoxHeight; boxRow++)
        {
            for (int boxColumn = startColumn; boxColumn < startColumn + Size.BoxWidth; boxColumn++)
            {
                if (boxRow == row && boxColumn == column)
                {
                    continue;
                }

                if (_cells[Size.CellIndex(boxRow, boxColumn)] == value)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private string HorizontalSeparator()
    {
        StringBuilder builder = new StringBuilder();
        int side = Size.Side;

        for (int column = 0; column < side; column++)
        {
            if (column > 0)
            {
                builder.Append(column % Size.BoxWidth == 0 ? "-+-" : "-");
            }

            builder.Append('-');
        }

        return builder.ToString();
    }
}
