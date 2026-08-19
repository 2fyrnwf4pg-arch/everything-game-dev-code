namespace FiveDSudoku.Core.Game;

/// <summary>
/// Why bringing an inactive timeline into play was refused.
/// <see cref="None"/> means the activation is allowed.
/// </summary>
public enum TimelineActivationRejection
{
    /// <summary>The activation is allowed.</summary>
    None = 0,

    /// <summary>The run is already won.</summary>
    RunAlreadyWon = 1,

    /// <summary>No timeline with the given id exists.</summary>
    TimelineNotFound = 2,

    /// <summary>
    /// The timeline is not inactive: it is already in play, or it is finished and
    /// there is nothing to bring back.
    /// </summary>
    TimelineNotInactive = 3,

    /// <summary>Every active slot the level grants is currently taken.</summary>
    NoFreeActiveSlot = 4,
}
