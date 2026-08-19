namespace FiveDSudoku.Core.Game;

/// <summary>
/// Why a placement was refused. <see cref="None"/> means the placement is legal.
///
/// The values mirror the conditions a normal move has to satisfy, so a caller can
/// tell a player exactly which one failed rather than only that something did.
/// </summary>
public enum MoveRejection
{
    /// <summary>The placement is legal.</summary>
    None = 0,

    /// <summary>The run is already won or already over.</summary>
    GameAlreadyFinished = 1,

    /// <summary>The selected timeline is not <see cref="Timelines.TimelineStatus.Active"/>.</summary>
    TimelineNotActive = 2,

    /// <summary>The selected timeline's frontier is not the current present.</summary>
    TimelineNotAtPresent = 3,

    /// <summary>Row or column lies outside the grid.</summary>
    CoordinatesOutOfRange = 4,

    /// <summary>The target cell already holds a value.</summary>
    CellNotEmpty = 5,

    /// <summary>The value is not one this grid uses.</summary>
    ValueOutOfRange = 6,

    /// <summary>The value repeats within the target cell's row, column, or box.</summary>
    ViolatesSudokuConstraint = 7,
}
