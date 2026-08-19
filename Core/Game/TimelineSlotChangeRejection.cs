namespace FiveDSudoku.Core.Game;

/// <summary>
/// Why moving a timeline into or out of an active slot was refused.
/// <see cref="None"/> means the change is allowed.
/// </summary>
public enum TimelineSlotChangeRejection
{
    /// <summary>The change is allowed.</summary>
    None = 0,

    /// <summary>The run is already won, so its shape no longer matters.</summary>
    RunAlreadyWon = 1,

    /// <summary>No timeline with the given id exists.</summary>
    TimelineNotFound = 2,

    /// <summary>
    /// Activation only: the timeline is not inactive. It is either already in play
    /// or finished, and neither is something to bring back.
    /// </summary>
    TimelineNotInactive = 3,

    /// <summary>Activation only: every active slot the level grants is currently taken.</summary>
    NoFreeActiveSlot = 4,

    /// <summary>
    /// Deactivation only: the timeline is not active. A finished timeline holds no
    /// slot to give up, and an inactive one has already given it up.
    /// </summary>
    TimelineNotActive = 5,
}
