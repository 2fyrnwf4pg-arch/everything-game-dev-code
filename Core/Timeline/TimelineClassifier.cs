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
}
