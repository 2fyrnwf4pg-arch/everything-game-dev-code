using System;

namespace FiveDSudoku.Core.Validation;

/// <summary>One way in which a run failed to hold up a rule about itself.</summary>
public sealed class InvariantViolation
{
    public InvariantViolation(GameInvariant invariant, string detail)
    {
        Invariant = invariant;
        Detail = detail ?? throw new ArgumentNullException(nameof(detail));
    }

    /// <summary>Which invariant was broken.</summary>
    public GameInvariant Invariant { get; }

    /// <summary>What exactly went wrong, in terms a reader can act on.</summary>
    public string Detail { get; }

    public override string ToString() => $"{Invariant}: {Detail}";
}
