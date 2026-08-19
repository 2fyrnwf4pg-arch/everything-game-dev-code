namespace FiveDSudoku.Core.Game;

/// <summary>
/// Why a Temporal Move was refused. <see cref="None"/> means the branch is legal.
///
/// The values follow the branch conditions one for one, and are checked in this
/// order, so the reason a caller gets back is always the first condition that
/// actually failed.
/// </summary>
public enum TemporalMoveRejection
{
    /// <summary>The branch is legal.</summary>
    None = 0,

    /// <summary>The run is already won or already over.</summary>
    GameAlreadyFinished = 1,

    /// <summary>No temporal budget is left.</summary>
    NoTemporalBudget = 2,

    /// <summary>No timeline with the given id exists.</summary>
    SourceTimelineNotFound = 3,

    /// <summary>The source timeline holds no state at the requested time.</summary>
    SourceTimeNotInTimeline = 4,

    /// <summary>
    /// The requested time is the source timeline's frontier rather than a state
    /// behind it. Branching is for revisiting history, not for playing the present.
    /// </summary>
    SourceTimeIsNotHistorical = 5,

    /// <summary>The requested time is not strictly before the global present.</summary>
    SourceTimeNotBeforePresent = 6,

    /// <summary>The requested time lies further back than the level's temporal window allows.</summary>
    OutsideTemporalWindow = 7,

    /// <summary>The alternative placement is not a legal Sudoku placement on the source board.</summary>
    PlacementNotLegal = 8,

    /// <summary>The branch would leave the source board unchanged.</summary>
    BranchDoesNotChangeTheBoard = 9,
}
