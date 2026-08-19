using System;
using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Level;
using FiveDSudoku.Core.Sudoku;

namespace FiveDSudoku.Tools.ScenarioRunner;

/// <summary>
/// A small fixed run used to exercise the diagnostics: one root that has made
/// progress, one branch that died on arrival, and one that is still playable.
///
/// It is deliberately hand-written and takes no random input, so its output is
/// the same on every machine and can be pinned down by a test.
/// </summary>
public static class DemoScenario
{
    /// <summary>The 4x4 puzzle the demo is built on. Exactly one solution.</summary>
    public const string Puzzle = "1... ..1. .2.. ...3";

    /// <summary>Builds the demo run.</summary>
    public static GameState Build()
    {
        LevelDefinition level = new LevelDefinition(
            "demo-4x4",
            SudokuBoard.Parse(BoardSize.FourByFour, Puzzle),
            temporalBudget: 3,
            temporalWindow: 4,
            maxActiveTimelines: 3,
            finalDepth: 12);

        GameState game = GameState.Start(level);

        // Two ordinary placements on the root.
        game = Play(game, 0, 1, 4);
        game = Play(game, 0, 2, 3);

        // A branch that breaks no rule and still cannot be completed.
        game = Branch(game, sourceTime: 0, row: 0, column: 1, value: 3);

        // And a real alternative: a different cell from the one the root went on
        // to fill, leading somewhere still winnable.
        game = Branch(game, sourceTime: 1, row: 1, column: 0, value: 2);

        return game;
    }

    private static GameState Play(GameState game, int row, int column, int value)
    {
        MoveResult result = game.PlaceValue(row, column, value);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"The demo expected r{row}c{column}={value} to be legal, but it was {result.Rejection}.");
        }

        return result.State;
    }

    private static GameState Branch(GameState game, int sourceTime, int row, int column, int value)
    {
        TemporalMoveResult result = game.PerformTemporalMove(
            GameState.RootTimelineId, sourceTime, row, column, value);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"The demo expected a branch off T{sourceTime} to be legal, but it was {result.Rejection}.");
        }

        return result.State;
    }
}
