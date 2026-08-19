namespace FiveDSudoku.Core.Game;

/// <summary>How a run currently stands.</summary>
public enum GameOutcome
{
    /// <summary>Still playable.</summary>
    InProgress = 0,

    /// <summary>A timeline reached a complete, valid Sudoku solution.</summary>
    Won = 1,

    /// <summary>Not won, and no timeline can be played any further.</summary>
    GameOver = 2,
}
