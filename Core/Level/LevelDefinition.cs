using System;
using FiveDSudoku.Core.Sudoku;

namespace FiveDSudoku.Core.Level;

/// <summary>
/// Everything a level needs in order to be played: the starting puzzle and the
/// tuning numbers that bound a run.
///
/// A level definition is inert data. It is deliberately not a quality gate: a
/// definition may carry a puzzle that has no solution, or several. Deciding
/// whether a puzzle is fit for release is the job of the validation layer, and
/// that layer needs to be able to build a broken level in order to reject it.
/// </summary>
public sealed class LevelDefinition
{
    /// <param name="id">Stable identifier used for saves, replays and diagnostics.</param>
    /// <param name="startingBoard">The puzzle as the player first sees it.</param>
    /// <param name="temporalBudget">How many Temporal Moves the level grants in total.</param>
    /// <param name="temporalWindow">
    /// How far back a Temporal Move may reach from the present. A window of 0
    /// grants no reach at all and so rules out branching entirely.
    /// </param>
    /// <param name="maxActiveTimelines">
    /// How many timelines may hold an active slot at the same time. The root
    /// timeline occupies one of them, so this is at least 1.
    /// </param>
    /// <param name="finalDepth">
    /// Pacing and balance target for how deep a run is expected to go. It is not a
    /// win condition: a complete, valid solution always wins regardless of depth.
    /// </param>
    public LevelDefinition(
        string id,
        SudokuBoard startingBoard,
        int temporalBudget,
        int temporalWindow,
        int maxActiveTimelines,
        int finalDepth)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A level needs a non-empty id.", nameof(id));
        }

        if (startingBoard is null)
        {
            throw new ArgumentNullException(nameof(startingBoard));
        }

        if (temporalBudget < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(temporalBudget), temporalBudget, "Temporal budget cannot be negative.");
        }

        if (temporalWindow < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(temporalWindow), temporalWindow, "Temporal window cannot be negative.");
        }

        if (maxActiveTimelines < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxActiveTimelines),
                maxActiveTimelines,
                "At least one active timeline slot is needed for the root timeline.");
        }

        if (finalDepth < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(finalDepth), finalDepth, "Final depth cannot be negative.");
        }

        Id = id;
        StartingBoard = startingBoard;
        TemporalBudget = temporalBudget;
        TemporalWindow = temporalWindow;
        MaxActiveTimelines = maxActiveTimelines;
        FinalDepth = finalDepth;
    }

    /// <summary>Stable identifier for this level.</summary>
    public string Id { get; }

    /// <summary>The puzzle the root timeline starts from.</summary>
    public SudokuBoard StartingBoard { get; }

    /// <summary>Total number of Temporal Moves granted by this level.</summary>
    public int TemporalBudget { get; }

    /// <summary>
    /// How far back a Temporal Move may reach from the present. The engine never
    /// permits an unbounded jump into history: a source time is only reachable when
    /// <c>present - sourceTime</c> falls within this window.
    /// </summary>
    public int TemporalWindow { get; }

    /// <summary>Maximum number of timelines that may hold an active slot at once.</summary>
    public int MaxActiveTimelines { get; }

    /// <summary>Pacing target for run depth. Never a win or loss condition.</summary>
    public int FinalDepth { get; }

    /// <summary>Geometry of this level's grid.</summary>
    public BoardSize Size => StartingBoard.Size;

    public override string ToString() =>
        $"{Id} ({Size}, budget {TemporalBudget}, window {TemporalWindow}, " +
        $"{MaxActiveTimelines} active slots, final depth {FinalDepth})";
}
