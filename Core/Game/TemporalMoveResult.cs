using System;

namespace FiveDSudoku.Core.Game;

/// <summary>
/// Outcome of attempting a Temporal Move.
///
/// Like a refused placement, a refused branch is not an error: it comes back with
/// the reason, with the game state unchanged, and with the temporal budget
/// untouched.
/// </summary>
public sealed class TemporalMoveResult
{
    private TemporalMoveResult(
        bool succeeded,
        TemporalMoveRejection rejection,
        GameState state,
        int? newTimelineId)
    {
        Succeeded = succeeded;
        Rejection = rejection;
        State = state;
        NewTimelineId = newTimelineId;
    }

    /// <summary>True when the branch was created.</summary>
    public bool Succeeded { get; }

    /// <summary>Why the branch was refused, or <see cref="TemporalMoveRejection.None"/>.</summary>
    public TemporalMoveRejection Rejection { get; }

    /// <summary>
    /// The resulting game state on success, or the unchanged state the branch was
    /// attempted against on failure.
    /// </summary>
    public GameState State { get; }

    /// <summary>Id of the timeline that was created, or <c>null</c> when none was.</summary>
    public int? NewTimelineId { get; }

    internal static TemporalMoveResult Created(GameState state, int newTimelineId) =>
        new TemporalMoveResult(
            true,
            TemporalMoveRejection.None,
            state ?? throw new ArgumentNullException(nameof(state)),
            newTimelineId);

    internal static TemporalMoveResult Refused(TemporalMoveRejection rejection, GameState state)
    {
        if (rejection == TemporalMoveRejection.None)
        {
            throw new ArgumentException("A refused Temporal Move needs a reason.", nameof(rejection));
        }

        return new TemporalMoveResult(
            false,
            rejection,
            state ?? throw new ArgumentNullException(nameof(state)),
            newTimelineId: null);
    }

    public override string ToString() =>
        Succeeded ? $"branched into L{NewTimelineId}" : $"refused: {Rejection}";
}
