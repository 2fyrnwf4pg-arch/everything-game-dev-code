using System;

namespace FiveDSudoku.Core.Game;

/// <summary>
/// Outcome of attempting to bring an inactive timeline into play.
///
/// Activation costs no temporal budget: it is a management action, not a move.
/// A refused activation throws nothing and returns the unchanged state.
/// </summary>
public sealed class TimelineActivationResult
{
    private TimelineActivationResult(
        bool succeeded,
        TimelineActivationRejection rejection,
        GameState state)
    {
        Succeeded = succeeded;
        Rejection = rejection;
        State = state;
    }

    /// <summary>True when the timeline was brought into play.</summary>
    public bool Succeeded { get; }

    /// <summary>Why the activation was refused, or <see cref="TimelineActivationRejection.None"/>.</summary>
    public TimelineActivationRejection Rejection { get; }

    /// <summary>
    /// The resulting game state on success, or the unchanged state the activation
    /// was attempted against on failure.
    /// </summary>
    public GameState State { get; }

    internal static TimelineActivationResult Activated(GameState state) =>
        new TimelineActivationResult(
            true,
            TimelineActivationRejection.None,
            state ?? throw new ArgumentNullException(nameof(state)));

    internal static TimelineActivationResult Refused(TimelineActivationRejection rejection, GameState state)
    {
        if (rejection == TimelineActivationRejection.None)
        {
            throw new ArgumentException("A refused activation needs a reason.", nameof(rejection));
        }

        return new TimelineActivationResult(
            false,
            rejection,
            state ?? throw new ArgumentNullException(nameof(state)));
    }

    public override string ToString() => Succeeded ? "activated" : $"refused: {Rejection}";
}
