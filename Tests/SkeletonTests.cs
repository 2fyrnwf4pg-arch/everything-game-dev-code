using System;
using System.Linq;
using System.Reflection;

namespace FiveDSudoku.Tests;

/// <summary>
/// Phase 0 placeholder tests. These prove the build/test pipeline works end to
/// end and guard the two structural constraints from CLAUDE.md that are already
/// meaningful with an empty solution. No game rules are asserted here — those
/// arrive with Phase 1.
/// </summary>
public sealed class SkeletonTests
{
    private const string CoreAssemblyName = "FiveDSudoku.Core";

    [Fact]
    public void TestPipelineRuns()
    {
        Assert.True(true);
    }

    [Fact]
    public void CoreAssemblyLoadsFromTheTestHost()
    {
        Assembly core = LoadCore();

        Assert.Equal(CoreAssemblyName, core.GetName().Name);
    }

    [Fact]
    public void CoreHasNoUnityDependency()
    {
        string[] unityReferences = LoadCore()
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(IsUnityAssembly)
            .ToArray();

        Assert.Empty(unityReferences);
    }

    private static Assembly LoadCore() => Assembly.Load(new AssemblyName(CoreAssemblyName));

    private static bool IsUnityAssembly(string name) =>
        name.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("UnityEditor", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Unity.", StringComparison.OrdinalIgnoreCase);
}
