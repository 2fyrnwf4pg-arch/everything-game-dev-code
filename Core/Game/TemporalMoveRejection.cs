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

    /// <summary>
    /// The run is already won, so there is nothing left to explore.
    ///
    /// A lost run is deliberately not covered by this: as long as temporal budget
    /// remains and a historical state is still in reach, a Temporal Move may open
    /// a playable branch and bring the run back. Recovering from a bad historical
    /// choice without an undo is what the mechanic exists for, so a dead end must
    /// not be what puts it out of reach.
    /// </summary>
    RunAlreadyWon = 1,

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
