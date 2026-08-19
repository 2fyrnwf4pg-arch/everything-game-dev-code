using System;

namespace FiveDSudoku.Core.Game;

/// <summary>
/// Outcome of moving a timeline into or out of an active slot.
///
/// Neither direction costs temporal budget: these are management actions, not
/// moves. A refused change throws nothing and returns the unchanged state.
/// </summary>
public sealed class TimelineSlotChangeResult
{
    private TimelineSlotChangeResult(
        bool succeeded,
        TimelineSlotChangeRejection rejection,
        GameState state)
    {
        Succeeded = succeeded;
        Rejection = rejection;
        State = state;
    }

    /// <summary>True when the timeline changed slot.</summary>
    public bool Succeeded { get; }

    /// <summary>Why the change was refused, or <see cref="TimelineSlotChangeRejection.None"/>.</summary>
    public TimelineSlotChangeRejection Rejection { get; }

    /// <summary>
    /// The resulting game state on success, or the unchanged state the change was
    /// attempted against on failure.
    /// </summary>
    public GameState State { get; }

    internal static TimelineSlotChangeResult Applied(GameState state) =>
        new TimelineSlotChangeResult(
            true,
            TimelineSlotChangeRejection.None,
            state ?? throw new ArgumentNullException(nameof(state)));

    internal static TimelineSlotChangeResult Refused(TimelineSlotChangeRejection rejection, GameState state)
    {
        if (rejection == TimelineSlotChangeRejection.None)
        {
            throw new ArgumentException("A refused slot change needs a reason.", nameof(rejection));
        }

        return new TimelineSlotChangeResult(
            false,
            rejection,
            state ?? throw new ArgumentNullException(nameof(state)));
    }

    public override string ToString() => Succeeded ? "applied" : $"refused: {Rejection}";
}
