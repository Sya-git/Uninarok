using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Uninarok.Map;
using Uninarok.Sprites;

namespace Uninarok.Units.PlayTests
{
    // TASKS-0009 T9.3 (FR-16; PLAN-0009 4.10): what hides a unit is decided as if its picture stood upright on its ground point.
    [Category("NeedsData")]
    public class UnitDepthPlayTests
    {
        const string MaterialPath = "Assets/Art/Materials/sprite_unit.mat";
        const int Size = 256;
        /// <summary>A bias nothing on the maps hides: the whole picture.</summary>
        const float Unhidden = 18f;
        static readonly float[] Tilts = { 50f, 65f };

        AddressablesUnitAssetSource _source;
        Material _material;
        Camera _camera;
        RenderTexture _target;
        GameObject _root;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return UnitPlayData.LoadSource(s => _source = s);
            _material = new Material(UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(MaterialPath));
            _root = new GameObject("Root");
            _camera = new GameObject("Camera").AddComponent<Camera>();
            _camera.transform.SetParent(_root.transform, false);
            _camera.fieldOfView = 8f;
            _camera.nearClipPlane = 1f;
            _camera.farClipPlane = 150f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.1f, 0.5f, 0.2f);
            _target = new RenderTexture(Size, Size, 24);
            _camera.targetTexture = _target;
            _camera.enabled = false;
            UnitSystem.Camera = _camera;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnitSystem.Camera = null;
            if (_camera != null) _camera.targetTexture = null;
            if (_root != null) Object.Destroy(_root);
            foreach (var unit in Object.FindObjectsByType<UnitView>()) Object.Destroy(unit.gameObject);
            yield return null;
            if (_source != null)
            {
                Assert.That(_source.HeldCount, Is.EqualTo(0));
                _source.Dispose();
            }
            Object.Destroy(_target);
            Object.Destroy(_material);
        }

        [UnityTest, Timeout(180000), Ignore("pending T9.4")]
        public IEnumerator UnitDepth_BackToAWall_IsWhole_4Turns_MinMaxTilt_Prontera() =>
            BackToAWall("prontera", new[] { (221, 221, 0f), (190, 32, 90f), (128, 86, 180f), (262, 99, 270f) });

        [UnityTest, Timeout(180000), Ignore("pending T9.4")]
        public IEnumerator UnitDepth_BackToAWall_IsWhole_4Turns_MinMaxTilt_IzInt() =>
            BackToAWall("iz_int", new[] { (61, 45, 0f), (61, 41, 90f), (55, 10, 180f), (50, 40, 270f) });

        // Cells whose wall, behind the unit at that camera turn, cut the unit when its depth was the billboard's.
        IEnumerator BackToAWall(string scene, (int x, int z, float yaw)[] places)
        {
            string path = "Assets/Scenes/Maps/" + scene + ".unity";
            if (!File.Exists(path)) Assert.Ignore(scene + " is not imported");
            var load = EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            while (!load.isDone) yield return null;
            var map = Object.FindAnyObjectByType<MapRoot>().Data;
            var unit = NewUnit(UnitPlayData.Swordsman());
            yield return Loaded(unit);
            foreach (var (x, z, yaw) in places)
                foreach (float tilt in Tilts)
                {
                    unit.PlaceOn(map, map.CellCenter(x, z));
                    Look(unit, tilt, yaw, 23f);
                    var empty = Without(unit);
                    Heights(unit, false);
                    int flat = Changed(empty, Shot());
                    Heights(unit, true);
                    int shown = Changed(empty, Shot());
                    _material.SetFloat("_DepthBias", Unhidden);
                    int whole = Changed(empty, Shot());
                    _material.SetFloat("_DepthBias", 0.5f);
                    string where = $"{scene} {x},{z} turn {yaw} tilt {tilt}";
                    Assert.That(whole, Is.GreaterThan(1000), where + ": the unit is on screen");
                    Assert.That(flat, Is.LessThan(whole * 0.9f), where + ": the billboard's own depth is cut by the wall");
                    // A mask's two stray pixels lie under the ground, as they did before (FR-10).
                    Assert.That(shown, Is.GreaterThanOrEqualTo(whole - 4), where + ": the unit is whole");
                }
            Object.Destroy(unit.gameObject);
            yield return null;
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest, Timeout(120000), Ignore("pending T9.4")]
        public IEnumerator UnitDepth_BehindABox_IsHiddenAsAnUprightPlane()
        {
            var unit = NewUnit(UnitPlayData.Swordsman());
            yield return Loaded(unit);
            foreach (float tilt in Tilts)
                foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
                {
                    Look(unit, tilt, yaw, 23f);
                    var turn = Quaternion.Euler(0f, yaw, 0f);
                    var background = Without(unit);
                    _material.SetFloat("_DepthBias", Unhidden);
                    var whole = Mask(background, Shot());
                    _material.SetFloat("_DepthBias", 0.5f);

                    // Behind the ground point, as tall as a house: hides nothing.
                    var wall = Box(turn * new Vector3(0f, 3f, 1.1f), turn * new Vector3(6f, 6f, 2f));
                    Count(Without(unit), whole, wall, out int visible, out _, out int all, out _);
                    Assert.That(visible, Is.EqualTo(all).Within(all / 50), $"a wall behind hides nothing, tilt {tilt} turn {yaw}");
                    Object.DestroyImmediate(wall);

                    // In front of the ground point: hides what its outline covers, and no more.
                    // 0.5 u away, and as tall as it takes to reach 0.6 u up the picture.
                    float tall = 0.6f / Mathf.Cos(tilt * Mathf.Deg2Rad) + 0.5f * Mathf.Tan(tilt * Mathf.Deg2Rad);
                    wall = Box(turn * new Vector3(0f, tall * 0.5f, -0.6f), turn * new Vector3(6f, 0f, 0.2f) + new Vector3(0f, tall, 0f));
                    Count(Without(unit), whole, wall, out visible, out int covered, out all, out int coveredVisible);
                    Assert.That(covered, Is.InRange(all / 10, all * 9 / 10), $"the wall in front covers a part, tilt {tilt} turn {yaw}");
                    Assert.That(coveredVisible, Is.LessThanOrEqualTo(all / 50), $"nothing shows through it, tilt {tilt} turn {yaw}");
                    Assert.That(visible, Is.EqualTo(all - covered).Within(all / 50), $"the rest shows, tilt {tilt} turn {yaw}");
                    Object.DestroyImmediate(wall);
                }
        }

        [UnityTest, Timeout(120000), Ignore("pending T9.4")]
        public IEnumerator UnitDepth_TwoUnits_SameOrderAsBefore()
        {
            var a = NewUnit(UnitPlayData.Swordsman());
            var b = NewUnit(UnitLook.ForJob(1002));
            yield return Loaded(a);
            yield return Loaded(b);
            foreach (var offset in new[] { new Vector3(0.2f, 0f, 0.25f), new Vector3(0.2f, 0f, -0.25f), new Vector3(0f, 0.3f, 0.1f) })
                foreach (float tilt in Tilts)
                {
                    b.transform.position = offset;
                    Look(a, tilt, 0f, 23f);
                    var upright = Shot();
                    Heights(a, false);
                    Heights(b, false);
                    var flat = Shot();
                    Heights(a, true);
                    Heights(b, true);
                    Assert.That(Changed(upright, flat), Is.EqualTo(0), $"offset {offset} tilt {tilt}");
                }
        }

        [UnityTest, Timeout(120000), Ignore("pending T9.4")]
        public IEnumerator UnitDepth_TallUnitNearTheCamera_IsNotClipped()
        {
            var unit = NewUnit(UnitPlayData.Swordsman());
            yield return Loaded(unit);
            _camera.fieldOfView = 40f;
            foreach (float tilt in new[] { 20f, 50f, 65f, 80f, 90f })
                foreach (float distance in new[] { 2.5f, 4f, 23f })
                {
                    Look(unit, tilt, 0f, distance);
                    var empty = Without(unit);
                    Heights(unit, false);
                    int flat = Changed(empty, Shot());
                    Heights(unit, true);
                    Assert.That(Changed(empty, Shot()), Is.EqualTo(flat), $"tilt {tilt} distance {distance}");
                }
        }

        [UnityTest, Timeout(120000), Ignore("pending T9.4")]
        public IEnumerator UnitDepth_Shadow_IsUnchanged()
        {
            var unit = NewUnit(UnitLook.ForJob(1002));
            unit.ShadowEnabled = true;
            yield return Loaded(unit);
            unit.SetPartHidden(PartSlot.Body, true);
            foreach (float tilt in Tilts)
            {
                Look(unit, tilt, 0f, 23f);
                var shadow = unit.Shadow.Renderer;
                Assert.That(shadow.enabled, Is.True);
                Vector3 axis = shadow.transform.localToWorldMatrix.GetColumn(2);
                Assert.That(axis.magnitude - SpriteActor.HeightOffset, Is.LessThan(-10f), "the shadow carries no height");
                var wall = Box(new Vector3(0f, 1f, 0.15f), new Vector3(6f, 2f, 0.2f));
                var shown = Shot();
                var scale = shadow.transform.localScale;
                shadow.transform.localScale = new Vector3(scale.x, scale.y, 0.001f);
                Assert.That(Changed(shown, Shot()), Is.EqualTo(0), $"tilt {tilt}");
                shadow.transform.localScale = scale;
                Object.DestroyImmediate(wall);
            }
        }

        // ---- helpers ----

        UnitView NewUnit(in UnitLook look)
        {
            var unit = UnitView.Create(new UnitLoader(_source), _material);
            unit.SetLook(look);
            unit.ShadowEnabled = false;
            unit.Play(PcAnimation.Idle);
            return unit;
        }

        static IEnumerator Loaded(UnitView unit)
        {
            for (int i = 0; i < 600 && !unit.Loaded; i++) yield return null;
            Assert.That(unit.Loaded, Is.True);
        }

        void Look(UnitView unit, float tilt, float yaw, float distance)
        {
            _camera.transform.rotation = Quaternion.Euler(tilt, yaw, 0f);
            _camera.transform.position = unit.transform.position + new Vector3(0f, 0.5f, 0f) - _camera.transform.forward * distance;
            UnitSystem.Tick(0f);
        }

        Color32[] Without(UnitView unit)
        {
            var renderers = unit.GetComponentsInChildren<SpriteRenderer>();
            foreach (var r in renderers) r.forceRenderingOff = true;
            var pixels = Shot();
            foreach (var r in renderers) r.forceRenderingOff = false;
            return pixels;
        }

        // The heights as the unit writes them, or none (the depth of D7 alone).
        static void Heights(UnitView unit, bool on)
        {
            float s = unit.Pivot.localScale.x;
            foreach (var r in unit.Pivot.GetComponentsInChildren<SpriteRenderer>(true))
            {
                var scale = r.transform.localScale;
                float height = Vector3.Dot(r.transform.position - unit.Pivot.position, unit.Pivot.up);
                r.transform.localScale = new Vector3(scale.x, scale.y, on ? SpriteActor.GroundScaleZ(height, s) : 1f);
            }
        }

        GameObject Box(Vector3 centre, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.SetParent(_root.transform, false);
            box.transform.localPosition = centre;
            box.transform.localScale = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            return box;
        }

        // Of the unit's pixels (whole): how many show now, how many the box's outline covers, and how many show though covered.
        void Count(Color32[] without, bool[] whole, GameObject box, out int visible, out int covered, out int all, out int coveredVisible)
        {
            var shown = Mask(without, Shot());
            var bounds = box.GetComponent<Renderer>().bounds;
            visible = covered = all = coveredVisible = 0;
            for (int i = 0; i < whole.Length; i++)
            {
                if (!whole[i]) continue;
                all++;
                if (shown[i]) visible++;
                var ray = _camera.ScreenPointToRay(new Vector3(i % Size + 0.5f, i / Size + 0.5f, 0f));
                if (!bounds.IntersectRay(ray)) continue;
                covered++;
                if (shown[i]) coveredVisible++;
            }
        }

        static bool[] Mask(Color32[] a, Color32[] b)
        {
            var mask = new bool[a.Length];
            for (int i = 0; i < a.Length; i++) mask[i] = Differs(a[i], b[i]);
            return mask;
        }

        static int Changed(Color32[] a, Color32[] b)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i++)
                if (Differs(a[i], b[i])) n++;
            return n;
        }

        static bool Differs(Color32 a, Color32 b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) > 12;

        Color32[] Shot()
        {
            _camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = _target;
            var t = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            RenderTexture.active = previous;
            var pixels = t.GetPixels32();
            Object.DestroyImmediate(t);
            return pixels;
        }
    }
}
