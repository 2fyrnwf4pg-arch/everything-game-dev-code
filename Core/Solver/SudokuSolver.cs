using System;
using FiveDSudoku.Core.Sudoku;

namespace FiveDSudoku.Core.Solver;

/// <summary>
/// Deterministic Sudoku solver. Works on any <see cref="BoardSize"/> — nothing
/// here is specialised for 4x4 or 9x9.
///
/// Every entry point is a pure function of its arguments: the same board always
/// produces the same answer, and the same first solution. Search order is fixed
/// (fewest candidates first, ties broken by row-major order, candidate values
/// tried ascending), so results never depend on timing or memory layout.
///
/// Solution counting is always bounded by an explicit limit. There is no
/// unbounded overload on purpose: an empty 9x9 grid has more solutions than
/// anything could enumerate, and callers only ever need to distinguish
/// "none" / "exactly one" / "more than one".
/// </summary>
public static class SudokuSolver
{
    /// <summary>True when the board violates no row, column, or box constraint.</summary>
    public static bool IsValid(SudokuBoard board)
    {
        if (board is null)
        {
            throw new ArgumentNullException(nameof(board));
        }

        return board.IsValid();
    }

    /// <summary>
    /// True when <paramref name="value"/> may legally be placed at the given
    /// coordinates on <paramref name="board"/>.
    /// </summary>
    public static bool IsPlacementLegal(SudokuBoard board, int row, int column, int value)
    {
        if (board is null)
        {
            throw new ArgumentNullException(nameof(board));
        }

        return board.IsPlacementLegal(row, column, value);
    }

    /// <summary>True when the board is a complete, violation-free Sudoku solution.</summary>
    public static bool IsSolved(SudokuBoard board)
    {
        if (board is null)
        {
            throw new ArgumentNullException(nameof(board));
        }

        return board.IsSolved();
    }

    /// <summary>
    /// Counts how many complete solutions <paramref name="board"/> has, stopping
    /// once <paramref name="limit"/> solutions have been found. The result is
    /// therefore <c>min(actual solutions, limit)</c>. A board that already
    /// violates a constraint has zero solutions.
    /// </summary>
    public static int CountSolutions(SudokuBoard board, int limit)
    {
        if (board is null)
        {
            throw new ArgumentNullException(nameof(board));
        }

        if (limit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be at least 1.");
        }

        Search search = new Search(board, limit, captureFirstSolution: false);

        return search.Run();
    }

    /// <summary>True when the board can be completed at least once.</summary>
    public static bool HasSolution(SudokuBoard board) => CountSolutions(board, 1) == 1;

    /// <summary>True when the board can be completed in exactly one way.</summary>
    public static bool HasUniqueSolution(SudokuBoard board) => CountSolutions(board, 2) == 1;

    /// <summary>
    /// Returns the first solution in the solver's fixed search order, or
    /// <c>null</c> when the board cannot be completed.
    /// </summary>
    public static SudokuBoard? FindFirstSolution(SudokuBoard board)
    {
        if (board is null)
        {
            throw new ArgumentNullException(nameof(board));
        }

        Search search = new Search(board, limit: 1, captureFirstSolution: true);
        search.Run();

        return search.FirstSolution;
    }

    /// <summary>
    /// Backtracking search over a flat cell array with row/column/box candidate
    /// bitmasks. Kept private so the mutable working state can never escape:
    /// callers only ever see immutable <see cref="SudokuBoard"/> instances.
    /// </summary>
    private sealed class Search
    {
        private readonly BoardSize _size;
        private readonly int[] _cells;
        private readonly int[] _rowMask;
        private readonly int[] _columnMask;
        private readonly int[] _boxMask;
        private readonly int _fullMask;
        private readonly int _limit;
        private readonly bool _captureFirstSolution;
        private readonly bool _startsValid;

        private int _found;

        internal Search(SudokuBoard board, int limit, bool captureFirstSolution)
        {
            _size = board.Size;
            _cells = board.ToArray();
            _limit = limit;
            _captureFirstSolution = captureFirstSolution;

            int side = _size.Side;
            _rowMask = new int[side];
            _columnMask = new int[side];
            _boxMask = new int[side];
            _fullMask = (1 << side) - 1;

            // The board owns the constraint rules; the search does not re-derive
            // them. This both seeds the search and tells us whether the board is
            // already in violation — in which case it has no solutions and the
            // search must not run at all.
            _startsValid = board.TryBuildConstraintMasks(_rowMask, _columnMask, _boxMask);
        }

        internal SudokuBoard? FirstSolution { get; private set; }

        internal int Run()
        {
            if (!_startsValid)
            {
                return 0;
            }

            Descend();

            return _found;
        }

        private void Descend()
        {
            if (!TrySelectCell(out int row, out int column, out int candidates))
            {
                RecordSolution();
                return;
            }

            while (candidates != 0)
            {
                int bit = candidates & -candidates;
                candidates &= candidates - 1;

                Place(row, column, bit);
                Descend();
                Remove(row, column, bit);

                if (_found >= _limit)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Picks the empty cell with the fewest candidates, breaking ties by
        /// row-major order. Returns false when every cell is filled. A selected
        /// cell with zero candidates is a dead end and ends this branch.
        /// </summary>
        private bool TrySelectCell(out int bestRow, out int bestColumn, out int bestCandidates)
        {
            bestRow = -1;
            bestColumn = -1;
            bestCandidates = 0;

            int bestCount = int.MaxValue;
            int side = _size.Side;

            for (int row = 0; row < side; row++)
            {
                for (int column = 0; column < side; column++)
                {
                    if (_cells[_size.CellIndex(row, column)] != BoardSize.EmptyCell)
                    {
                        continue;
                    }

                    int candidates = CandidatesAt(row, column);
                    int count = PopCount(candidates);

                    if (count >= bestCount)
                    {
                        continue;
                    }

                    bestRow = row;
                    bestColumn = column;
                    bestCandidates = candidates;
                    bestCount = count;

                    if (count == 0)
                    {
                        return true;
                    }
                }
            }

            return bestRow >= 0;
        }

        private int CandidatesAt(int row, int column)
        {
            int used = _rowMask[row] | _columnMask[column] | _boxMask[_size.BoxIndex(row, column)];

            return _fullMask & ~used;
        }

        private void Place(int row, int column, int bit)
        {
            _cells[_size.CellIndex(row, column)] = TrailingZeroCount(bit) + 1;
            _rowMask[row] |= bit;
            _columnMask[column] |= bit;
            _boxMask[_size.BoxIndex(row, column)] |= bit;
        }

        private void Remove(int row, int column, int bit)
        {
            _cells[_size.CellIndex(row, column)] = BoardSize.EmptyCell;
            _rowMask[row] &= ~bit;
            _columnMask[column] &= ~bit;
            _boxMask[_size.BoxIndex(row, column)] &= ~bit;
        }

        private void RecordSolution()
        {
            _found++;

            if (_captureFirstSolution && FirstSolution is null)
            {
                FirstSolution = SudokuBoard.Create(_size, _cells);
            }
        }

        /// <summary>
        /// Hand-rolled because <c>System.Numerics.BitOperations</c> is not available
        /// on netstandard2.1.
        /// </summary>
        private static int PopCount(int mask)
        {
            int count = 0;

            while (mask != 0)
            {
                mask &= mask - 1;
                count++;
            }

            return count;
        }

        private static int TrailingZeroCount(int singleBit)
        {
            int index = 0;

            while ((singleBit & 1) == 0)
            {
                singleBit >>= 1;
                index++;
            }

            return index;
        }
    }
}
