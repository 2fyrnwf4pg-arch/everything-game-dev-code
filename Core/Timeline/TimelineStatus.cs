namespace FiveDSudoku.Core.Timelines;

/// <summary>
/// Lifecycle state of a timeline.
///
/// The vocabulary is fixed by the design; not every value is reachable yet. In a
/// single-timeline game only <see cref="Active"/> and <see cref="Solved"/> occur:
/// a timeline is played until its board is complete and correct.
/// </summary>
public enum TimelineStatus
{
    /// <summary>Playable: the frontier of this timeline can still be advanced.</summary>
    Active = 0,

    /// <summary>The frontier is a complete, violation-free Sudoku solution.</summary>
    Solved = 1,

    /// <summary>The frontier can never be completed, so the timeline is finished.</summary>
    Dead = 2,

    /// <summary>
    /// Exists and can be inspected, but holds no active slot: it does not influence
    /// the present and cannot be played until it is activated.
    /// </summary>
    Inactive = 3,
}
