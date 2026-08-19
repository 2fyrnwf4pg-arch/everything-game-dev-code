using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using FiveDSudoku.Core.Level;

namespace FiveDSudoku.Core.Validation;

/// <summary>The verdict on one check.</summary>
public sealed class LevelValidationFinding
{
    public LevelValidationFinding(LevelValidationCheck check, bool passed, string detail)
    {
        Check = check;
        Passed = passed;
        Detail = detail ?? throw new ArgumentNullException(nameof(detail));
    }

    /// <summary>Which check this is about.</summary>
    public LevelValidationCheck Check { get; }

    /// <summary>Whether the level survived it.</summary>
    public bool Passed { get; }

    /// <summary>What was found, in terms a level designer can act on.</summary>
    public string Detail { get; }

    public override string ToString() => $"{(Passed ? "PASS" : "FAIL")} {Check}: {Detail}";
}

/// <summary>
/// What validation found out about a level: one finding per check, in check
/// order, each saying what it saw rather than only whether it liked it.
/// </summary>
public sealed class LevelValidationReport
{
    private readonly ReadOnlyCollection<LevelValidationFinding> _findings;

    internal LevelValidationReport(LevelDefinition level, LevelValidationFinding[] findings)
    {
        Level = level;
        _findings = new ReadOnlyCollection<LevelValidationFinding>(findings);
    }

    /// <summary>The level that was checked.</summary>
    public LevelDefinition Level { get; }

    /// <summary>Every finding, in check order.</summary>
    public IReadOnlyList<LevelValidationFinding> Findings => _findings;

    /// <summary>True when every check passed, which is what a release candidate needs.</summary>
    public bool IsReleaseCandidate
    {
        get
        {
            for (int index = 0; index < _findings.Count; index++)
            {
                if (!_findings[index].Passed)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>The finding for one check.</summary>
    public LevelValidationFinding this[LevelValidationCheck check]
    {
        get
        {
            for (int index = 0; index < _findings.Count; index++)
            {
                if (_findings[index].Check == check)
                {
                    return _findings[index];
                }
            }

            throw new ArgumentOutOfRangeException(nameof(check), check, "No finding for that check.");
        }
    }

    /// <summary>Whether one check passed.</summary>
    public bool Passed(LevelValidationCheck check) => this[check].Passed;

    /// <summary>Every check the level failed.</summary>
    public IReadOnlyList<LevelValidationFinding> Failures
    {
        get
        {
            List<LevelValidationFinding> failures = new List<LevelValidationFinding>();

            for (int index = 0; index < _findings.Count; index++)
            {
                if (!_findings[index].Passed)
                {
                    failures.Add(_findings[index]);
                }
            }

            return failures;
        }
    }

    public override string ToString()
    {
        StringBuilder builder = new StringBuilder();

        builder.Append(Level.Id).Append(": ")
            .Append(IsReleaseCandidate ? "release candidate" : $"{Failures.Count} check(s) failed")
            .Append('\n');

        for (int index = 0; index < _findings.Count; index++)
        {
            builder.Append("  ").Append(_findings[index]).Append('\n');
        }

        return builder.ToString();
    }
}
