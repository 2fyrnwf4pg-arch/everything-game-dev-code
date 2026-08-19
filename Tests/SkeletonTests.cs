using System;
using System.Linq;
using System.Reflection;

namespace FiveDSudoku.Tests;

/// <summary>
/// Structural tests introduced in Phase 0. They prove the build/test pipeline
/// works end to end and guard the constraints from CLAUDE.md that hold for the
/// whole project, independently of any game rule.
/// </summary>
[TestFixture]
public sealed class SkeletonTests
{
    private const string CoreAssemblyName = "FiveDSudoku.Core";

    [Test]
    public void TestPipelineRuns()
    {
        Assert.Pass();
    }

    [Test]
    public void CoreAssemblyLoadsFromTheTestHost()
    {
        Assembly core = LoadCore();

        Assert.That(core.GetName().Name, Is.EqualTo(CoreAssemblyName));
    }

    [Test]
    public void CoreHasNoUnityDependency()
    {
        string[] unityReferences = LoadCore()
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(IsUnityAssembly)
            .ToArray();

        Assert.That(unityReferences, Is.Empty);
    }

    [Test]
    public void CoreTargetsNetStandard21()
    {
        string? targetFramework = LoadCore()
            .GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()
            ?.FrameworkName;

        Assert.That(targetFramework, Is.EqualTo(".NETStandard,Version=v2.1"));
    }

    private static Assembly LoadCore() => Assembly.Load(new AssemblyName(CoreAssemblyName));

    private static bool IsUnityAssembly(string name) =>
        name.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("UnityEditor", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Unity.", StringComparison.OrdinalIgnoreCase);
}
