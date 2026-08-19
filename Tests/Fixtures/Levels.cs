using System;
using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Level;
using FiveDSudoku.Core.Solver;
using FiveDSudoku.Core.Sudoku;

namespace FiveDSudoku.Tests;

/// <summary>
/// Level fixtures and small play helpers shared by the game tests.
///
/// The helpers only ever go through the public game API, so a test using them
/// still proves the real rules rather than a shortcut around them.
/// </summary>
internal static class Levels
{
    internal const int DefaultTemporalBudget = 3;

    /// <summary>Wide enough that the window never gets in the way unless a test asks it to.</summary>
    internal const int DefaultTemporalWindow = 16;

    internal const int DefaultActiveSlots = 2;
    internal const int DefaultFinalDepth = 12;

    /// <summary>A solvable 4x4 level with exactly one solution.</summary>
    internal static LevelDefinition SolvableFourByFour(
        int temporalBudget = DefaultTemporalBudget,
        int temporalWindow = DefaultTemporalWindow,
        int maxActiveTimelines = DefaultActiveSlots,
        int finalDepth = DefaultFinalDepth) =>
        FromText(
            "4x4-unique",
            BoardSize.FourByFour,
            Puzzles.Unique4,
            temporalBudget,
            temporalWindow,
            maxActiveTimelines,
            finalDepth);

    /// <summary>
    /// A 4x4 level with exactly two solutions. Useful whenever a branch has to end
    /// up playable: on a uniquely solvable puzzle every alternative to the solution
    /// is by definition a dead end.
    /// </summary>
    internal static LevelDefinition TwoSolutionFourByFour(
        int temporalBudget = DefaultTemporalBudget,
        int temporalWindow = DefaultTemporalWindow,
        int maxActiveTimelines = DefaultActiveSlots,
        int finalDepth = DefaultFinalDepth) =>
        FromText(
            "4x4-two-solutions",
            BoardSize.FourByFour,
            Puzzles.Multi4,
            temporalBudget,
            temporalWindow,
            maxActiveTimelines,
            finalDepth);

    /// <summary>Builds a level from a board written as text.</summary>
    internal static LevelDefinition FromText(
        string id,
        BoardSize size,
        string boardText,
        int temporalBudget = DefaultTemporalBudget,
        int temporalWindow = DefaultTemporalWindow,
        int maxActiveTimelines = DefaultActiveSlots,
        int finalDepth = DefaultFinalDepth) =>
        new LevelDefinition(
            id,
            SudokuBoard.Parse(size, boardText),
            temporalBudget,
            temporalWindow,
            maxActiveTimelines,
            finalDepth);

    /// <summary>
    /// Plays the first legal placement in row-major, ascending-value order on the
    /// selected timeline. Deterministic, so a run driven by it is reproducible.
    /// </summary>
    internal static GameState PlayAnyLegalMove(GameState game)
    {
        BoardSize size = game.Level.Size;

        for (int row = 0; row < size.Side; row++)
        {
            for (int column = 0; column < size.Side; column++)
            {
                for (int value = size.MinValue; value <= size.MaxValue; value++)
                {
                    MoveResult result = game.PlaceValue(row, column, value);

                    if (result.Succeeded)
                    {
                        return result.State;
                    }
                }
            }
        }

        throw new InvalidOperationException("No legal placement was available on the selected timeline.");
    }

    /// <summary>
    /// Plays the selected timeline to a complete solution using normal placements
    /// only, in row-major order. Throws if any placement is refused, so a caller
    /// never mistakes a stalled run for a solved one.
    /// </summary>
    internal static GameState SolveSelectedTimeline(GameState game)
    {
        SudokuBoard start = game.SelectedTimeline.Frontier;
        SudokuBoard solution = SudokuSolver.FindFirstSolution(start)
            ?? throw new InvalidOperationException("The selected timeline's frontier cannot be completed.");

        BoardSize size = start.Size;

        for (int row = 0; row < size.Side; row++)
        {
            for (int column = 0; column < size.Side; column++)
            {
                if (!start.IsEmpty(row, column))
                {
                    continue;
                }

                MoveResult result = game.PlaceValue(row, column, solution[row, column]);

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Placing {solution[row, column]} at r{row}c{column} was refused: {result.Rejection}.");
                }

                game = result.State;
            }
        }

        return game;
    }
}
