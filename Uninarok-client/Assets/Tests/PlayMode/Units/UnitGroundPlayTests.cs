using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Uninarok.Map;

namespace Uninarok.Units.PlayTests
{
    // TASKS-0009 T5.3 (FR-10; PLAN-0009 4.6, D7): units on a slope of prt_fild08, at the lowest and highest camera tilt.
    [Category("NeedsData")]
    public class UnitGroundPlayTests
    {
        const string ScenePath = "Assets/Scenes/Maps/prt_fild08.unity";
        const string MaterialPath = "Assets/Art/Materials/sprite_unit.mat";
        const int Size = 384;

        [UnityTest, Timeout(180000)]
        public IEnumerator UnitView_FeetOnGround_SlopeOfPrtFild08_MinMaxTilt()
        {
            if (!File.Exists(ScenePath)) Assert.Ignore("prt_fild08 is not imported");
            AddressablesUnitAssetSource source = null;
            yield return UnitPlayData.LoadSource(s => source = s);
            var load = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            while (!load.isDone) yield return null;
            var root = Object.FindAnyObjectByType<MapRoot>();
            Assert.That(root, Is.Not.Null, "the map scene has its MapRoot");
            var map = root.Data;

            var material = new Material(UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(MaterialPath));
            Assert.That(material.GetFloat("_DepthBias"), Is.EqualTo(0.5f));
            var loader = new UnitLoader(source);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.fieldOfView = 15f;
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 150f;
            UnitSystem.Camera = camera;
            var target = new RenderTexture(Size, Size, 24);
            camera.targetTexture = target;
            camera.enabled = false;

            Vector3 slope = FindSlope(map);
            foreach (var (name, look) in new[] { ("poring", UnitLook.ForJob(1002)), ("swordsman", UnitPlayData.Swordsman()) })
            {
                var unit = UnitView.Create(loader, material);
                unit.SetLook(look);
                unit.PlaceOn(map, slope);
                unit.ShadowEnabled = false;
                for (int i = 0; i < 600 && !unit.Loaded; i++) yield return null;
                unit.Play(PcAnimation.Idle);
                foreach (float tilt in new[] { 50f, 65f })
                {
                    camera.transform.rotation = Quaternion.Euler(tilt, 0f, 0f);
                    camera.transform.position = unit.transform.position - camera.transform.forward * 12f;
                    UnitSystem.Tick(0f);

                    // The feet are exactly on the ground point: it is the pivot's origin.
                    Assert.That(Vector3.Distance(unit.Pivot.position, unit.transform.position), Is.LessThan(1e-4f));
                    Assert.That(map.TryGetHeight(unit.transform.position, out float y) && Mathf.Abs(y - unit.transform.position.y) < 1e-4f, Is.True, "on the ground, not floating");
                    // Not squashed: the sprite plane is parallel to the screen.
                    Assert.That(Vector3.Angle(unit.Pivot.forward, camera.transform.forward), Is.LessThan(0.01f));

                    unit.gameObject.SetActive(false);
                    var empty = Shot(camera, target);
                    unit.gameObject.SetActive(true);
                    UnitSystem.Tick(0f);
                    material.SetFloat("_DepthBias", 0.5f);
                    var shown = Shot(camera, target);
                    Save(shown, $"t5.3-{name}-tilt{tilt}");
                    material.SetFloat("_DepthBias", 8f);   // never cut by the ground
                    var whole = Shot(camera, target);
                    material.SetFloat("_DepthBias", 0.5f);

                    int visible = Changed(empty, shown), all = Changed(empty, whole);
                    Assert.That(all, Is.GreaterThan(300), "the unit is on screen");
                    Assert.That(visible, Is.EqualTo(all), $"{name} at {tilt}°: nothing is cut by the ground");
                    Object.Destroy(empty);
                    Object.Destroy(shown);
                    Object.Destroy(whole);
                }
                Object.Destroy(unit.gameObject);
                yield return null;
            }
            UnitSystem.Camera = null;
            camera.targetTexture = null;
            Object.Destroy(camera.gameObject);
            Object.Destroy(target);
            Object.Destroy(material);
            Assert.That(source.HeldCount, Is.EqualTo(0));
            source.Dispose();
            yield return SceneManager.UnloadSceneAsync(Path.GetFileNameWithoutExtension(ScenePath));
        }

        // A walkable cell in the middle of the map that rises toward the camera's back (its south edge is lower) by a clear step.
        static Vector3 FindSlope(MapData map)
        {
            float best = 0f;
            Vector3 at = map.CellCenter(map.CellsX / 2, map.CellsZ / 2);
            for (int z = 40; z < map.CellsZ - 40; z++)
                for (int x = 40; x < map.CellsX - 40; x++)
                {
                    if (!map.IsWalkable(x, z)) continue;
                    var h = map.CornerHeights(x, z);
                    float rise = Mathf.Abs((h.z + h.w - h.x - h.y) * 0.5f);
                    if (rise <= best || rise > 0.3f) continue; // steeper than 31° is a cliff edge
                    best = rise;
                    at = map.CellCenter(x, z);
                }
            Assert.That(best, Is.GreaterThan(0.05f), "the map has a slope");
            return at;
        }

        static Texture2D Shot(Camera camera, RenderTexture target)
        {
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var t = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            t.Apply();
            RenderTexture.active = previous;
            return t;
        }

        static int Changed(Texture2D a, Texture2D b)
        {
            var pa = a.GetPixels32();
            var pb = b.GetPixels32();
            int n = 0;
            for (int i = 0; i < pa.Length; i++)
                if (Mathf.Abs(pa[i].r - pb[i].r) + Mathf.Abs(pa[i].g - pb[i].g) + Mathf.Abs(pa[i].b - pb[i].b) > 12) n++;
            return n;
        }

        static void Save(Texture2D t, string name)
        {
            string folder = Path.Combine(Path.GetTempPath(), "Uninarok-0009");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), t.EncodeToPNG());
        }
    }
}
