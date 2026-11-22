using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Uninarok.Networking.Data;
using Uninarok.Units.Dev;
using Object = UnityEngine.Object;

namespace Uninarok.Units.PlayTests
{
    // TASKS-0009 T7.1, T7.2 (FR-13, FR-14; PLAN-0009 4.8): the Entity Sandbox on prontera.
    [Category("NeedsData")]
    public class EntitySandboxPlayTests
    {
        const string ScenePath = "Assets/Scenes/Dev/EntitySandbox.unity";
        const string MapPath = "Assets/Scenes/Maps/prontera.unity";

        EntitySandbox _sandbox;

        IEnumerator Open()
        {
            if (!File.Exists(MapPath)) Assert.Ignore("prontera is not imported");
            var load = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            while (!load.isDone) yield return null;
            _sandbox = Object.FindAnyObjectByType<EntitySandbox>();
            Assert.That(_sandbox, Is.Not.Null);
            for (int i = 0; i < 6000 && !_sandbox.Ready; i++) yield return null;
            Assert.That(_sandbox.Ready, Is.True, _sandbox.Status);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_sandbox != null) _sandbox.Clear();
            yield return null;
            foreach (string path in new[] { ScenePath, MapPath })
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            _sandbox = null;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator EntitySandbox_Loads_SpawnsReferenceSet_OnProntera()
        {
            yield return Open();
            var map = _sandbox.Map;
            Assert.That(map.MapName, Is.EqualTo("prontera"));
            Assert.That(UnitSystem.Camera, Is.SameAs(_sandbox.Rig.OutputCamera));
            int x = _sandbox.StartCell.x - 6;
            foreach (var look in UnitPlayData.ReferenceLooks())
            {
                var u = _sandbox.Spawn(look, x, _sandbox.StartCell.y);
                Assert.That(map.TryGetHeight(u.transform.position, out float y) && Mathf.Abs(y - u.transform.position.y) < 1e-3f, Is.True);
                x += 2;
            }
            for (int i = 0; i < 1200; i++)
            {
                bool all = true;
                foreach (var u in _sandbox.Units) all &= u.Loaded;
                if (all) break;
                yield return null;
            }
            foreach (var u in _sandbox.Units)
            {
                Assert.That(u.Loaded, Is.True, u.name);
                Assert.That(u.Report.MissingCount, Is.EqualTo(0), u.name + "\n" + u.Report);
                Assert.That(u.ShownDirection, Is.EqualTo(0), "facing south at the default camera: the front");
            }

            // FR-6 through the rig.
            _sandbox.Rig.Turn(180f);
            for (float end = Time.time + 3f; Time.time < end;) yield return null;
            foreach (var u in _sandbox.Units) Assert.That(u.ShownDirection, Is.EqualTo(4), "the back after 180°");
            _sandbox.Rig.ResetTurn();

            // The cell picker finds the cell in the middle of the screen: the camera target's.
            for (float end = Time.time + 3f; Time.time < end;) yield return null;
            var camera = _sandbox.Rig.OutputCamera;
            var ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Assert.That(MapPicker.TryPick(map, ray, 200f, out int px, out int pz), Is.True);
            Assert.That(Mathf.Abs(px - _sandbox.StartCell.x) <= 1 && Mathf.Abs(pz - _sandbox.StartCell.y) <= 1, Is.True, $"picked {px}, {pz}");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator UnitSystem_ZeroGcAllocPerFrame_300Units()
        {
            yield return Open();
            foreach (var panel in Object.FindObjectsByType<EntitySandboxPanel>()) panel.enabled = false; // IMGUI allocates
            var stress = _sandbox.GetComponent<EntitySandboxStress>();
            LogAssert.ignoreFailingMessages = true; // random equipment a class has no animation for warns, as it should
            stress.Begin();
            Assert.That(_sandbox.Units.Count, Is.EqualTo(EntitySandboxStress.Players + EntitySandboxStress.Monsters));
            for (int i = 0; i < 6000; i++)
            {
                bool all = true;
                foreach (var u in _sandbox.Units) all &= u.Loaded;
                if (all) break;
                yield return null;
            }
            foreach (var u in _sandbox.Units) Assert.That(u.Loaded, Is.True, u.name);

            // Warm-up: every frame of every animation once.
            for (int i = 0; i < 300; i++) yield return null;

            // Timing: the driver's own tick, once per frame.
            double sum = 0, max = 0;
            const int frames = 300;
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                sum += UnitSystem.LastTickMs;
                max = Math.Max(max, UnitSystem.LastTickMs);
            }
            Assert.That(sum / frames, Is.LessThan(EntitySandboxStress.BudgetMs), "FR-14 budget, in the editor");

            // Garbage: ticks of our own, with nothing else of the frame inside.
            long worst = 0;
            for (int i = 0; i < frames; i++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                UnitSystem.Tick(Time.deltaTime);
                worst = Math.Max(worst, GC.GetAllocatedBytesForCurrentThread() - before);
                yield return null;
            }
            string line = $"300 units, editor, {SystemInfo.processorType}: tick mean {sum / frames:0.000} ms, max {max:0.000} ms, worst GC {worst} B";
            TestContext.WriteLine(line);
            Debug.Log("[FR-14] " + line);
            Assert.That(worst, Is.EqualTo(0), line);
        }

        // Turning the camera recomposes all 300 units at once: still no garbage.
        [UnityTest, Timeout(600000)]
        public IEnumerator UnitSystem_ZeroGcAlloc_WhileTheCameraTurns()
        {
            yield return Open();
            foreach (var panel in Object.FindObjectsByType<EntitySandboxPanel>()) panel.enabled = false;
            var stress = _sandbox.GetComponent<EntitySandboxStress>();
            LogAssert.ignoreFailingMessages = true;
            stress.Begin(30, 60);
            for (int i = 0; i < 3000; i++)
            {
                bool all = true;
                foreach (var u in _sandbox.Units) all &= u.Loaded;
                if (all) break;
                yield return null;
            }
            for (int i = 0; i < 300; i++) yield return null;
            // Every direction once, so every part has seen its widest frame.
            for (int i = 0; i < 16; i++)
            {
                foreach (var u in _sandbox.Units) u.Facing = (UninarokDirection)(i & 7);
                UnitSystem.Tick(0.1f);
                yield return null;
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                foreach (var u in _sandbox.Units) u.Facing = (UninarokDirection)(i & 7);
                UnitSystem.Tick(0.016f);
            }
            long spent = GC.GetAllocatedBytesForCurrentThread() - before;
            // The foreach over the read-only list boxes one enumerator per pass.
            Assert.That(spent, Is.LessThan(64 * 64));
        }
    }
}
