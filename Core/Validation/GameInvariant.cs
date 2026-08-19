namespace FiveDSudoku.Core.Validation;

/// <summary>
/// Properties that must hold of a run at every moment, whatever actions got it
/// there. These are the claims the rules make about themselves.
/// </summary>
public enum GameInvariant
{
    /// <summary>
    /// History is only ever added to. A state that was played stays exactly as it
    /// was, a timeline never loses states or changes where its time axis starts,
    /// and a timeline never disappears from the run.
    /// </summary>
    ImmutableHistory = 1,

    /// <summary>
    /// Remaining temporal budget stays between zero and what the level granted,
    /// and never goes back up.
    /// </summary>
    TemporalBudgetWithinBounds = 2,

    /// <summary>
    /// The present is the earliest frontier among the timelines that are active
    /// and hold a slot — and absent exactly when there are none.
    /// </summary>
    PresentMatchesTheMinimumActiveFrontier = 3,
}
