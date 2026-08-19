using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FiveDSudoku.Tests;

/// <summary>
/// The "do not implement yet" audit, checked against the built Core assembly
/// rather than against anyone's memory of what was written.
///
/// Every entry on the list is something that would be visible in the public API
/// or in what the assembly depends on, so an assembly-level audit is not a
/// stand-in for reading the code — it is the thing that keeps being true after
/// nobody is reading the code any more.
/// </summary>
[TestFixture]
public sealed class ForbiddenContentTests
{
    /// <summary>
    /// One forbidden subject and the words that would give it away. Kept explicit
    /// so a failure names the subject, not just a regular expression.
    /// </summary>
    private static readonly (string Subject, string[] Words)[] NotYet =
    {
        ("multiplayer or online services", new[] { "multiplayer", "netcode", "matchmak", "lobby", "session" }),
        ("achievements", new[] { "achievement", "trophy", "leaderboard" }),
        ("monetization or ads", new[] { "monetiz", "microtransaction", "advertis", "storefront" }),
        ("sound", new[] { "audioclip", "soundeffect", "musictrack", "mixer" }),
        ("animation", new[] { "animation", "animator", "keyframe" }),
        ("final art", new[] { "spriterenderer", "texture", "shader", "material" }),
        ("Steam integration", new[] { "steamworks", "steamapi" }),
        ("platform code", new[] { "androidjava", "uikit", "jnienv" }),
        ("timeline merging", new[] { "merge" }),
        ("causal echoes", new[] { "causal", "echo" }),
        ("artificial paradox rules", new[] { "paradox" }),
        ("gameplay undo", new[] { "undo", "redo", "revert", "rollback", "rewind" }),
    };

    /// <summary>Assemblies a dependency-free core is allowed to lean on.</summary>
    private static readonly string[] AllowedReferencePrefixes = { "netstandard", "System", "mscorlib" };

    private static Assembly Core => Assembly.Load(new AssemblyName("FiveDSudoku.Core"));

    /// <summary>Every public name Core exposes: types, and the members on them.</summary>
    private static IEnumerable<string> PublicNames()
    {
        foreach (Type type in Core.GetExportedTypes())
        {
            yield return type.FullName ?? type.Name;

            foreach (MemberInfo member in type.GetMembers(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                yield return $"{type.FullName}.{member.Name}";
            }
        }
    }

    [TestCaseSource(nameof(NotYet))]
    public void NoneOfTheseHaveCreptIntoTheCore((string Subject, string[] Words) forbidden)
    {
        List<string> offenders = new List<string>();

        foreach (string name in PublicNames())
        {
            string simplified = name.Replace("_", string.Empty).ToLowerInvariant();

            // Only the member's own name matters; the namespace is not a hiding
            // place, but it is also not where a feature would show up.
            string tail = simplified.Substring(simplified.LastIndexOf('.') + 1);

            if (forbidden.Words.Any(word => tail.Contains(word)))
            {
                offenders.Add(name);
            }
        }

        Assert.That(offenders, Is.Empty, $"{forbidden.Subject} must not be part of this milestone");
    }

    [Test]
    public void TheCoreDependsOnNothingButTheBaseLibrary()
    {
        string[] unexpected = Core.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => !AllowedReferencePrefixes.Any(
                prefix => name.Equals(prefix, StringComparison.Ordinal)
                    || name.StartsWith(prefix + ".", StringComparison.Ordinal)))
            .ToArray();

        Assert.That(unexpected, Is.Empty, "the core must stay embeddable, which means depending on nothing");
    }

    [Test]
    public void TheCoreHasNoUnityDependency()
    {
        string[] unity = Core.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name =>
                name.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("UnityEditor", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Unity.", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.That(unity, Is.Empty);
    }

    // ---- no speculative cross-timeline mechanics ---------------------------

    [Test]
    public void SudokuAndSolvingKnowNothingAboutTimelinesOrRuns()
    {
        // The strongest statement available about "no cross-timeline Sudoku
        // constraints": the types that decide what a legal placement is cannot
        // even be handed a timeline, a run or a level to consult.
        string[] localNamespaces = { "FiveDSudoku.Core.Sudoku", "FiveDSudoku.Core.Solver" };
        string[] multiverseNamespaces =
        {
            "FiveDSudoku.Core.Timelines", "FiveDSudoku.Core.Game", "FiveDSudoku.Core.Level",
        };

        List<string> offenders = new List<string>();

        foreach (Type type in Core.GetExportedTypes())
        {
            if (!localNamespaces.Contains(type.Namespace))
            {
                continue;
            }

            foreach (MethodBase method in type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Cast<MethodBase>()
                .Concat(type.GetConstructors()))
            {
                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    if (multiverseNamespaces.Contains(parameter.ParameterType.Namespace))
                    {
                        offenders.Add($"{type.Name}.{method.Name}({parameter.ParameterType.Name})");
                    }
                }
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "row, column and box rules must stay local to one board");
    }

    [Test]
    public void NothingInTheCoreCombinesTwoTimelines()
    {
        // Timeline merging is on the not-yet list, and an operation that merged
        // would have to take two of them.
        Type timeline = Core.GetExportedTypes().Single(type => type.Name == "Timeline");
        List<string> offenders = new List<string>();

        foreach (Type type in Core.GetExportedTypes())
        {
            foreach (MethodBase method in type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Cast<MethodBase>()
                .Concat(type.GetConstructors()))
            {
                int timelineParameters = method.GetParameters()
                    .Count(parameter => parameter.ParameterType == timeline);

                bool alsoTakesOne = timelineParameters > 0 && !method.IsStatic && type == timeline;

                if (timelineParameters + (alsoTakesOne ? 1 : 0) >= 2)
                {
                    offenders.Add($"{type.Name}.{method.Name}");
                }
            }
        }

        Assert.That(offenders, Is.Empty, "no operation may take two timelines and produce one");
    }
}
