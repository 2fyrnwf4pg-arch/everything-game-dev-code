namespace FiveDSudoku.Core.Validation;

/// <summary>
/// The checks a level has to survive before it is fit to ship.
///
/// The first four are about the puzzle itself. The rest are about what the rules
/// do with it: they are established by actually playing the level, because a
/// claim like "no action can corrupt history" cannot be read off a board.
/// </summary>
public enum LevelValidationCheck
{
    /// <summary>The starting puzzle breaks no row, column, or box rule.</summary>
    StartingPuzzleIsValid = 1,

    /// <summary>The starting puzzle can be completed at least one way.</summary>
    StartingPuzzleHasASolution = 2,

    /// <summary>
    /// The starting puzzle can be completed exactly one way. A puzzle with several
    /// solutions still plays, but it is not what a release candidate should be.
    /// </summary>
    StartingPuzzleHasExactlyOneSolution = 3,

    /// <summary>
    /// The level can be played to a win using ordinary placements only, spending no
    /// temporal budget. This is the hard design requirement: time travel may help,
    /// never gate.
    /// </summary>
    SolvableWithoutTemporalMoves = 4,

    /// <summary>
    /// Taking an optional Temporal Move is legal and leaves the timeline it branched
    /// from exactly as it was.
    /// </summary>
    OptionalBranchesLeaveTheOriginalIntact = 5,

    /// <summary>
    /// A branch that is legal Sudoku but has no completion is reported dead the
    /// moment it is made, never left looking playable.
    /// </summary>
    ImpossibleBranchesDieImmediately = 6,

    /// <summary>
    /// The level offers at least one branch that is worth taking — one that leads
    /// somewhere still winnable — on top of a solve path that never needed it.
    /// </summary>
    AUsefulButOptionalBranchExists = 7,

    /// <summary>No action taken while validating changed or discarded any history.</summary>
    ImmutableHistoryHolds = 8,

    /// <summary>No action taken while validating pushed the temporal budget out of bounds.</summary>
    TemporalBudgetNeverGoesNegative = 9,

    /// <summary>The present matched the earliest active frontier after every action.</summary>
    PresentMatchesTheMinimumActiveFrontier = 10,
}
