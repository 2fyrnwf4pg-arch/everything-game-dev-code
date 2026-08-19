using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FiveDSudoku.Tests;

/// <summary>
/// Guards on Core's public API surface.
///
/// "There is no gameplay undo" is a rule about what must not exist, so it cannot
/// be proven by exercising the API — only by looking at the whole of it. This
/// fixture does that, and fails the moment an undo-shaped member appears.
/// </summary>
[TestFixture]
public sealed class ApiSurfaceTests
{
    private static readonly string[] UndoWords =
    {
        "undo", "redo", "revert", "rollback", "rewind", "stepback", "goback", "restoreprevious",
    };

    [Test]
    public void CoreExposesNoGameplayUndo()
    {
        List<string> offenders = new List<string>();

        foreach (Type type in LoadCore().GetExportedTypes())
        {
            foreach (MemberInfo member in type.GetMembers(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                string name = member.Name.Replace("_", string.Empty).ToLowerInvariant();

                if (UndoWords.Any(word => name.Contains(word)))
                {
                    offenders.Add($"{type.FullName}.{member.Name}");
                }
            }
        }

        Assert.That(offenders, Is.Empty, "no gameplay undo may exist anywhere in the core rules");
    }

    [Test]
    public void CoreExposesNoTypeNamedAfterUndo()
    {
        string[] offenders = LoadCore()
            .GetExportedTypes()
            .Select(type => type.Name)
            .Where(name => UndoWords.Any(word => name.ToLowerInvariant().Contains(word)))
            .ToArray();

        Assert.That(offenders, Is.Empty);
    }

    private static Assembly LoadCore() => Assembly.Load(new AssemblyName("FiveDSudoku.Core"));
}
