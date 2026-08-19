namespace FiveDSudoku.Core.Game;

/// <summary>How a run currently stands.</summary>
public enum GameOutcome
{
    /// <summary>Still playable.</summary>
    InProgress = 0,

    /// <summary>A timeline reached a complete, valid Sudoku solution.</summary>
    Won = 1,

    /// <summary>
    /// Not won, and no timeline can be played any further.
    ///
    /// This is not necessarily the end of the run. Ordinary placements are out —
    /// that is what the state means — but a Temporal Move can still branch off a
    /// historical state, and a branch that is playable puts the run back
    /// <see cref="InProgress"/>. Only <see cref="Won"/> is terminal.
    /// </summary>
    GameOver = 2,
}
