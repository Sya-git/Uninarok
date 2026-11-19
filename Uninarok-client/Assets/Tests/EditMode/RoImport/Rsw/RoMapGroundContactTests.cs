using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Gnd;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Rsw;
using Uninarok.RoImport.Tests.Gnd;
using Object = UnityEngine.Object;

namespace Uninarok.RoImport.Tests.Rsw
{
    // TASKS-0006 T7.1 (FR-4; closes SPEC-0003 FR-3): the sample models of ground-contact.json (recorded by T0.4 against the
    // reference viewer) sit at the recorded vertical distance from the ground. The instances are placed exactly as the
    // scene builder places them (RoRswPlacement) using the project's converted model and ground prefabs.
    [Category(GndTestData.Category)]
    public class RoMapGroundContactTests
    {
        const string ArtRoot = "Assets/Art/Imported";

        [Serializable]
        sealed class Sample
        {
            public string map;
            public int instance;
            public string model;
            public float expectedDistance;
            public float tolerance;
        }

        [Serializable]
        sealed class SampleFile
        {
            public Sample[] samples;
        }

        [TearDown]
        public void TearDown() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        static SampleFile Samples()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "Tests/EditMode/RoImport/Rsw/ground-contact.json"));
            return JsonUtility.FromJson<SampleFile>(json);
        }

        [Test]
        public void TheSamples_AreAtLeastTwenty_OverTheThreeReferenceMaps()
        {
            var file = Samples();
            Assert.GreaterOrEqual(file.samples.Length, 20);
            CollectionAssert.IsSubsetOf(new[] { "prt_fild08", "prontera", "izlude" }, Array.ConvertAll(file.samples, s => s.map));
        }

        [TestCase("prt_fild08")]
        [TestCase("prontera")]
        [TestCase("izlude")]
        public void EverySampleOfTheMap_SitsAtItsRecordedDistanceFromTheGround(string map)
        {
            var samples = Array.FindAll(Samples().samples, s => s.map == map);
            Assert.IsTrue(RoRswReader.TryRead(RoPath.Parse(map + ".rsw"), RswTestData.MapBytes(map), out var rsw, out var reason), reason);
            Assert.IsTrue(RoGndReader.TryRead(RoPath.Parse(map + ".gnd"), GndTestData.MapBytes(map), null, out var gnd, out reason), reason);
            var space = new RoMapSpace(gnd.Width, gnd.Height, gnd.Zoom);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var groundPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ArtRoot}/map/{map}/{map}_ground.prefab");
            if (groundPrefab == null) Assert.Ignore($"the ground of {map} is not converted in this project");
            var ground = (GameObject)PrefabUtility.InstantiatePrefab(groundPrefab);
            foreach (var mf in ground.GetComponentsInChildren<MeshFilter>()) mf.gameObject.AddComponent<MeshCollider>();
            Physics.SyncTransforms();

            var failures = new List<string>();
            foreach (var sample in samples)
            {
                var m = rsw.Models[sample.instance];
                Assert.AreEqual(sample.model, m.File, $"{map} #{sample.instance}: the sample names its model");
                string path = $"{ArtRoot}/model/{m.File.Replace('\\', '/')}";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path.Substring(0, path.Length - 4) + ".prefab");
                Assert.IsNotNull(prefab, path);

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                RoRswPlacement.ToUnity(RoRswPlacement.ModelMatrixRo(m, space, RoRswRules.V1), out var p, out var r, out var s);
                go.transform.SetPositionAndRotation(p, r);
                go.transform.localScale = s;

                var low = Lowest(go);
                Assert.IsTrue(Physics.Raycast(new Vector3(low.x, 1000f, low.z), Vector3.down, out var hit, 5000f), $"{map} #{sample.instance}: ground under it");
                float distance = low.y - hit.point.y;
                if (Mathf.Abs(distance - sample.expectedDistance) > sample.tolerance)
                    failures.Add($"#{sample.instance} {m.File}: {distance:F3} (expected {sample.expectedDistance:F3} ± {sample.tolerance})");
                TestContext.Out.WriteLine($"{map} #{sample.instance} {m.File}: {distance:F3} (expected {sample.expectedDistance:F3})");
                Object.DestroyImmediate(go);
            }
            CollectionAssert.IsEmpty(failures);
        }

        static Vector3 Lowest(GameObject go)
        {
            var low = new Vector3(0f, float.MaxValue, 0f);
            void Scan(Mesh mesh, Matrix4x4 toWorld)
            {
                if (mesh == null) return;
                foreach (var v in mesh.vertices)
                {
                    var w = toWorld.MultiplyPoint3x4(v);
                    if (w.y < low.y) low = w;
                }
            }
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) Scan(mf.sharedMesh, mf.transform.localToWorldMatrix);
            foreach (var sm in go.GetComponentsInChildren<SkinnedMeshRenderer>()) Scan(sm.sharedMesh, sm.transform.localToWorldMatrix);
            return low;
        }
    }
}
