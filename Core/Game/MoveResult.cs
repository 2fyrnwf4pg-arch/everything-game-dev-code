using System;

namespace FiveDSudoku.Core.Game;

/// <summary>
/// Outcome of attempting a placement.
///
/// A refused move is not an error and does not throw: it comes back with the
/// reason and with the game state unchanged, so a caller can offer a move,
/// discover it is illegal, and carry on from exactly where it was.
/// </summary>
public sealed class MoveResult
{
    private MoveResult(bool succeeded, MoveRejection rejection, GameState state)
    {
        Succeeded = succeeded;
        Rejection = rejection;
        State = state;
    }

    /// <summary>True when the placement was applied.</summary>
    public bool Succeeded { get; }

    /// <summary>Why the placement was refused, or <see cref="MoveRejection.None"/>.</summary>
    public MoveRejection Rejection { get; }

    /// <summary>
    /// The resulting game state on success, or the unchanged state the move was
    /// attempted against on failure.
    /// </summary>
    public GameState State { get; }

    internal static MoveResult Applied(GameState state) =>
        new MoveResult(true, MoveRejection.None, state ?? throw new ArgumentNullException(nameof(state)));

    internal static MoveResult Refused(MoveRejection rejection, GameState state)
    {
        if (rejection == MoveRejection.None)
        {
            throw new ArgumentException("A refused move needs a reason.", nameof(rejection));
        }

        return new MoveResult(false, rejection, state ?? throw new ArgumentNullException(nameof(state)));
    }

    public override string ToString() => Succeeded ? "applied" : $"refused: {Rejection}";
}
