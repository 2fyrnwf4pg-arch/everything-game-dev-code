using System;
using FiveDSudoku.Core.Solver;
using FiveDSudoku.Core.Sudoku;

namespace FiveDSudoku.Tests;

/// <summary>
/// Random puzzles for the stress suite, built candidate-first rather than by
/// brute force.
///
/// A complete solution is produced by shuffling a known one through the
/// transformations that a Sudoku grid is invariant under — relabelling values,
/// permuting rows inside a band, columns inside a stack, and the bands and stacks
/// themselves. Every one of those preserves validity, so a solution comes out
/// without a single solver call.
///
/// Puzzles are then made by removing cells from that solution. Any subset of a
/// solution is completable by construction, so "solvable" costs nothing to
/// guarantee; only uniqueness has to be checked, and it is checked one removal at
/// a time with a bounded solver call.
/// </summary>
internal static class PuzzleGenerator
{
    private const string CanonicalFourByFour = "1234 3412 2143 4321";

    private const string CanonicalNineByNine =
        "534678912 672195348 198342567 859761423 426853791 713924856 961537284 287419635 345286179";

    /// <summary>A random complete, valid solution of the given size.</summary>
    internal static SudokuBoard RandomSolution(BoardSize size, Random random)
    {
        SudokuBoard canonical = SudokuBoard.Parse(size, CanonicalFor(size));
        int side = size.Side;

        int[] values = Shuffled(Identity(side), random);
        int[] rows = ShuffledBlocks(side, size.BoxHeight, random);
        int[] columns = ShuffledBlocks(side, size.BoxWidth, random);
        bool transpose = size.BoxWidth == size.BoxHeight && random.Next(2) == 0;

        int[] cells = new int[size.CellCount];

        for (int row = 0; row < side; row++)
        {
            for (int column = 0; column < side; column++)
            {
                int sourceRow = rows[row];
                int sourceColumn = columns[column];
                int value = transpose ? canonical[sourceColumn, sourceRow] : canonical[sourceRow, sourceColumn];

                cells[size.CellIndex(row, column)] = values[value - 1];
            }
        }

        SudokuBoard solution = SudokuBoard.Create(size, cells);

        if (!solution.IsSolved())
        {
            throw new InvalidOperationException("Shuffling produced a board that is not a solution.");
        }

        return solution;
    }

    /// <summary>
    /// A puzzle with at least one solution, made by clearing
    /// <paramref name="cellsToClear"/> cells of a random solution.
    /// </summary>
    internal static SudokuBoard RandomSolvablePuzzle(BoardSize size, Random random, int cellsToClear)
    {
        SudokuBoard puzzle = RandomSolution(size, random);
        int[] order = Shuffled(Identity(size.CellCount), random);
        int cleared = 0;

        for (int index = 0; index < order.Length && cleared < cellsToClear; index++)
        {
            int cell = order[index] - 1;

            puzzle = puzzle.WithValue(cell / size.Side, cell % size.Side, BoardSize.EmptyCell);
            cleared++;
        }

        return puzzle;
    }

    /// <summary>
    /// A puzzle with exactly one solution: cells are cleared one at a time and a
    /// removal is kept only while the grid still has a single completion.
    /// </summary>
    internal static SudokuBoard RandomPuzzleWithUniqueSolution(BoardSize size, Random random)
    {
        SudokuBoard puzzle = RandomSolution(size, random);
        int[] order = Shuffled(Identity(size.CellCount), random);

        foreach (int index in order)
        {
            int cell = index - 1;
            int row = cell / size.Side;
            int column = cell % size.Side;

            SudokuBoard candidate = puzzle.WithValue(row, column, BoardSize.EmptyCell);

            if (SudokuSolver.HasUniqueSolution(candidate))
            {
                puzzle = candidate;
            }
        }

        return puzzle;
    }

    private static string CanonicalFor(BoardSize size)
    {
        if (size.Equals(BoardSize.FourByFour))
        {
            return CanonicalFourByFour;
        }

        if (size.Equals(BoardSize.NineByNine))
        {
            return CanonicalNineByNine;
        }

        throw new ArgumentOutOfRangeException(nameof(size), size, "No canonical solution for that size.");
    }

    private static int[] Identity(int count)
    {
        int[] values = new int[count];

        for (int index = 0; index < count; index++)
        {
            values[index] = index + 1;
        }

        return values;
    }

    /// <summary>Fisher-Yates, so the shuffle is uniform and driven only by the seed.</summary>
    private static int[] Shuffled(int[] values, Random random)
    {
        for (int index = values.Length - 1; index > 0; index--)
        {
            int swap = random.Next(index + 1);
            (values[index], values[swap]) = (values[swap], values[index]);
        }

        return values;
    }

    /// <summary>
    /// A permutation of 0..side-1 that shuffles whole blocks of
    /// <paramref name="blockSize"/> and the entries inside each block, which is
    /// exactly what a Sudoku grid stays valid under.
    /// </summary>
    private static int[] ShuffledBlocks(int side, int blockSize, Random random)
    {
        int blockCount = side / blockSize;
        int[] blockOrder = Shuffled(Identity(blockCount), random);
        int[] result = new int[side];
        int next = 0;

        foreach (int block in blockOrder)
        {
            int[] within = Shuffled(Identity(blockSize), random);

            foreach (int offset in within)
            {
                result[next++] = ((block - 1) * blockSize) + (offset - 1);
            }
        }

        return result;
    }
}
