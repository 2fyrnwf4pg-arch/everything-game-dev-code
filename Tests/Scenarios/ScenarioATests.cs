using System.Collections.Generic;
using FiveDSudoku.Core.Game;
using FiveDSudoku.Core.Level;
using FiveDSudoku.Core.Solver;
using FiveDSudoku.Core.Sudoku;
using FiveDSudoku.Core.Timelines;

namespace FiveDSudoku.Tests;

/// <summary>
/// Scenario A — the acceptance test for a single-timeline run.
///
/// Start a solvable 4x4 puzzle, solve it end to end without a single Temporal
/// Move, and confirm the level is won with the temporal budget untouched.
///
/// The scenario is written out rather than delegated to a helper: it is the proof
/// that the phase works through the real public API, so every step it takes is a
/// step a player could take.
/// </summary>
[TestFixture]
public sealed class ScenarioATests
{
    [Test]
    public void SolvingAFourByFourWithoutTimeTravelWinsAndSpendsNoBudget()
    {
        // --- arrange: a real, solvable 4x4 level -------------------------
        LevelDefinition level = Levels.SolvableFourByFour(temporalBudget: 3);
        SudokuBoard startingBoard = level.StartingBoard;

        Assert.That(SudokuSolver.HasUniqueSolution(startingBoard), Is.True, "the level must be genuinely solvable");

        SudokuBoard solution = SudokuSolver.FindFirstSolution(startingBoard)!;

        GameState game = GameState.Start(level);
        int budgetAtStart = game.RemainingTemporalBudget;

        Assert.That(budgetAtStart, Is.EqualTo(3));
        Assert.That(game.Present, Is.EqualTo(0));
        Assert.That(game.Outcome, Is.EqualTo(GameOutcome.InProgress));

        // --- act: fill every empty cell with an ordinary placement --------
        List<SudokuBoard> statesAsPlayed = new List<SudokuBoard> { game.SelectedTimeline.Frontier };
        int movesPlayed = 0;

        for (int row = 0; row < level.Size.Side; row++)
        {
            for (int column = 0; column < level.Size.Side; column++)
            {
                if (!startingBoard.IsEmpty(row, column))
                {
                    continue;
                }

                MoveResult result = game.PlaceValue(row, column, solution[row, column]);

                Assert.That(
                    result.Succeeded,
                    Is.True,
                    $"placing {solution[row, column]} at r{row}c{column} was refused: {result.Rejection}");

                game = result.State;
                movesPlayed++;

                statesAsPlayed.Add(game.SelectedTimeline.Frontier);

                Assert.That(game.SelectedTimeline.FrontierTime, Is.EqualTo(movesPlayed));
                Assert.That(game.RemainingTemporalBudget, Is.EqualTo(budgetAtStart), "no move may cost budget");
            }
        }

        // --- assert: the level is won ------------------------------------
        Assert.That(movesPlayed, Is.EqualTo(12));
        Assert.That(game.Outcome, Is.EqualTo(GameOutcome.Won));
        Assert.That(game.SelectedTimeline.Frontier, Is.EqualTo(solution));
        Assert.That(game.SelectedTimeline.Frontier.IsSolved(), Is.True);
        Assert.That(game.SelectedTimeline.Status, Is.EqualTo(TimelineStatus.Solved));

        // --- assert: no Temporal Move was used ---------------------------
        Assert.That(game.RemainingTemporalBudget, Is.EqualTo(level.TemporalBudget));
        Assert.That(game.Timelines, Has.Count.EqualTo(1), "no branch may have been created");
        Assert.That(game.SelectedTimeline.ParentId, Is.Null);
        Assert.That(game.SelectedTimeline.BranchTime, Is.Null);

        // --- assert: the whole history survived intact --------------------
        Timeline root = game.SelectedTimeline;

        Assert.That(root.FirstStateTime, Is.EqualTo(0));
        Assert.That(root.FrontierTime, Is.EqualTo(12));
        Assert.That(root.StateCount, Is.EqualTo(13));
        Assert.That(root.StateAt(0), Is.EqualTo(startingBoard), "T0 must still be the puzzle as handed out");

        for (int time = 0; time <= root.FrontierTime; time++)
        {
            Assert.That(root.StateAt(time), Is.EqualTo(statesAsPlayed[time]), $"T{time} must be unchanged");
            Assert.That(root.StateAt(time).FilledCellCount, Is.EqualTo(startingBoard.FilledCellCount + time));
        }
    }
}
