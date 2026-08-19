using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace FiveDSudoku.Unity.Tests
{
    /// <summary>
    /// Guards the project settings the core depends on.
    ///
    /// These are settings a person clicks once and then forgets, which is exactly
    /// the kind of thing that silently changes and costs an afternoon later. The
    /// core is a netstandard2.1 assembly: if the API compatibility level drops to
    /// .NET Standard 2.0 the project stops compiling, and the error will point at
    /// the assembly rather than at the setting that caused it.
    ///
    /// This file is the only one in the smoke suite that touches Unity's own API,
    /// so it is also the only one that cannot be checked outside the editor.
    /// </summary>
    public sealed class ProjectSetupTests
    {
        private const string PluginFolder = "Assets/Plugins/FiveDSudoku";

        [Test]
        public void TheApiCompatibilityLevelIsNetStandard21()
        {
            // NamedBuildTarget is Unity 2021.2+. On an older LTS use the obsolete
            // PlayerSettings.GetApiCompatibilityLevel(BuildTargetGroup) instead.
            NamedBuildTarget target = NamedBuildTarget.FromBuildTargetGroup(
                EditorUserBuildSettings.selectedBuildTargetGroup);

            Assert.That(
                PlayerSettings.GetApiCompatibilityLevel(target),
                Is.EqualTo(ApiCompatibilityLevel.NET_Standard),
                "the core is a netstandard2.1 assembly and needs .NET Standard 2.1 to load");
        }

        [Test]
        public void TheCoreAssemblyIsPresentInPlugins()
        {
            Assert.That(
                File.Exists(Path.Combine(Application.dataPath, "Plugins/FiveDSudoku/FiveDSudoku.Core.dll")),
                Is.True,
                "run Unity/build-core-dll.sh — the core is built from Core/, not compiled by Unity");
        }

        [Test]
        public void TheSourceFingerprintIsRecordedSoStalenessCanBeDetected()
        {
            string fingerprint = Path.Combine(Application.dataPath, "Plugins/FiveDSudoku/core-source.sha256");

            Assert.That(File.Exists(fingerprint), Is.True, "run Unity/build-core-dll.sh");
            Assert.That(
                File.ReadAllText(fingerprint).Trim().Length,
                Is.EqualTo(64),
                "the fingerprint should be a sha256 of the Core sources");
        }

        [Test]
        public void NothingUnderAssetsCompilesTheCoreFromSource()
        {
            // Core is C# 10 and Unity's compiler is C# 9. If someone copies the
            // source in as a shortcut it will not compile, and the reason will not
            // be obvious from the error. Catch the copy, not the symptom.
            string[] strays = Directory.GetFiles(
                Application.dataPath, "GameState.cs", SearchOption.AllDirectories);

            Assert.That(
                strays,
                Is.Empty,
                "the core belongs in " + PluginFolder + " as a built assembly, not as source");
        }
    }
}
