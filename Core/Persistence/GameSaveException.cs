using System;

namespace FiveDSudoku.Core.Persistence;

/// <summary>Thrown when a save cannot be read back into a run.</summary>
public sealed class GameSaveException : Exception
{
    public GameSaveException(string message)
        : base(message)
    {
    }

    public GameSaveException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
