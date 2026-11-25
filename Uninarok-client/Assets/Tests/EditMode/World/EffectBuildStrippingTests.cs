using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Uninarok.World.Tests
{
    /// <summary>
    /// FR-8, FR-11: dev-only code (Uninarok.World.Dev, Uninarok.Effects.Editor) is absent from a release player
    /// build, and Uninarok.Effects.Editor is absent from a development build too. Runs two real StandaloneWindows64
    /// player builds into a temp folder outside the project and inspects the assembly file names in the output.
    /// </summary>
    [TestFixture, Category("Slow")]
    public sealed class EffectBuildStrippingTests
    {
        private const string WorldDev = "Uninarok.World.Dev.dll";
        private const string EffectsEditor = "Uninarok.Effects.Editor.dll";

        [Test]
        public void EffectDevTypes_LiveOnlyInWorldDevAssembly()
        {
            // Assembly absence in a build therefore implies the type absence (FR-8).
            var commands = typeof(Uninarok.World.Dev.EffectDebugView).Assembly;
            Assert.AreEqual("Uninarok.World.Dev", commands.GetName().Name);
            Assert.IsNotNull(commands.GetType("Uninarok.World.Dev.EffectDevCommands"));
            Assert.IsNotNull(commands.GetType("Uninarok.World.Dev.EffectDebugView"));
        }

        [Test, Timeout(1800000)] // two real player builds incl. the Addressables content build
        public void PlayerBuilds_StripDevAssemblies()
        {
            var releaseDlls = BuildAndListManagedDlls(BuildOptions.None);
            Assert.IsFalse(releaseDlls.Contains(WorldDev), "release build must not contain " + WorldDev);
            Assert.IsFalse(releaseDlls.Contains(EffectsEditor), "release build must not contain " + EffectsEditor);

            var devDlls = BuildAndListManagedDlls(BuildOptions.Development);
            Assert.IsTrue(devDlls.Contains(WorldDev), "development build must contain " + WorldDev);
            Assert.IsFalse(devDlls.Contains(EffectsEditor), "development build must not contain " + EffectsEditor);
        }

        private static string[] BuildAndListManagedDlls(BuildOptions options)
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "uninarok-strip-" + Guid.NewGuid().ToString("N"));
            var previousTarget = EditorUserBuildSettings.activeBuildTarget;
            var previousGroup = BuildPipeline.GetBuildTargetGroup(previousTarget);
            var previousDevelopment = EditorUserBuildSettings.development;
            try
            {
                Directory.CreateDirectory(tempRoot);
                var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                var exe = Path.Combine(tempRoot, "StripCheck.exe");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = exe,
                    target = BuildTarget.StandaloneWindows64,
                    options = options,
                });
                Assert.AreEqual(BuildResult.Succeeded, report.summary.result,
                    "player build failed (" + options + "): " + report.summary.totalErrors + " errors");

                // The scripting backend decides where assemblies land (Mono: StripCheck_Data/Managed), so search the whole output.
                var dlls = Directory.GetFiles(tempRoot, "*.dll", SearchOption.AllDirectories).Select(Path.GetFileName).ToArray();
                Assert.IsTrue(dlls.Contains("Uninarok.Effects.dll"),
                    "sanity: Uninarok.Effects.dll not found in build output; found: " + string.Join(", ", dlls));
                return dlls;
            }
            finally
            {
                EditorUserBuildSettings.development = previousDevelopment;
                if (EditorUserBuildSettings.activeBuildTarget != previousTarget)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(previousGroup, previousTarget);
                try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); }
                catch (Exception e) { Debug.LogWarning("could not delete " + tempRoot + ": " + e.Message); }
            }
        }
    }
}
