using System;
using FiveDSudoku.Core.Solver;
using FiveDSudoku.Core.Sudoku;

namespace FiveDSudoku.Core.Timelines;

/// <summary>
/// Decides what a board means for the timeline holding it.
///
/// A placement can be perfectly legal against its row, column and box and still
/// leave a grid that no longer has any completion at all. Such a timeline must
/// never be left <see cref="TimelineStatus.Active"/>: it would look playable while
/// being mathematically finished, and the player would keep spending moves on it.
/// Classification therefore happens the moment a branch is created, not lazily
/// when someone eventually notices.
/// </summary>
public static class TimelineClassifier
{
    /// <summary>
    /// Classifies a board as <see cref="TimelineStatus.Solved"/> when it is a
    /// complete, violation-free solution; <see cref="TimelineStatus.Active"/> when
    /// it still has at least one completion; and <see cref="TimelineStatus.Dead"/>
    /// when it has none.
    /// </summary>
    public static TimelineStatus ClassifyBoard(SudokuBoard board)
    {
        if (board is null)
        {
            throw new ArgumentNullException(nameof(board));
        }

        // Checked first because a complete, valid board also counts as having a
        // completion — itself — and would otherwise be reported as merely playable.
        if (board.IsSolved())
        {
            return TimelineStatus.Solved;
        }

        return SudokuSolver.HasSolution(board) ? TimelineStatus.Active : TimelineStatus.Dead;
    }

    /// <summary>
    /// Classifies a board that ordinary play has just produced. Solved when it is a
    /// complete, violation-free solution; <see cref="TimelineStatus.Dead"/> when it
    /// offers no legal placement at all; otherwise <see cref="TimelineStatus.Active"/>.
    ///
    /// This asks a local question where <see cref="ClassifyBoard"/> asks a global
    /// one, and the difference is deliberate. A board can still offer placements
    /// while already having no completion; that timeline stays playable, and the
    /// player finds out the way a player should. Running the solver after every
    /// placement would instead end a timeline the instant a legal-looking value
    /// turned out to be wrong.
    ///
    /// What it must never do is leave a timeline active once nothing can be played
    /// on it. Such a timeline can never produce another state, yet its frontier
    /// would keep counting towards the present and pin it there forever — leaving
    /// every other timeline unable to advance past it, in a run that can no longer
    /// end.
    /// </summary>
    public static TimelineStatus ClassifyAfterPlacement(SudokuBoard board)
    {
        if (board is null)
        {
            throw new ArgumentNullException(nameof(board));
        }

        if (board.IsSolved())
        {
            return TimelineStatus.Solved;
        }

        return board.HasAnyLegalPlacement() ? TimelineStatus.Active : TimelineStatus.Dead;
    }
}
