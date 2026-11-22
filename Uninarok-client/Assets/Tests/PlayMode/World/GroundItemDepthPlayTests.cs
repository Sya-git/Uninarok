using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Uninarok.Catalog;
using Uninarok.Units;
using Uninarok.World.View;

namespace Uninarok.World.PlayTests
{
    // TASKS-0009 T9.3 (SPEC-0009 FR-16; PLAN-0009 4.10): an item on the ground follows the units' depth rule.
    [Category("NeedsData")]
    public class GroundItemDepthPlayTests
    {
        const int Size = 256;

        [UnityTest, Timeout(120000), Ignore("pending T9.4")]
        public IEnumerator UnitDepth_GroundItemAtAWall_IsWhole()
        {
#if UNITY_EDITOR
            if (UnityEditor.AssetDatabase.LoadMainAssetAtPath(SpriteCatalogKeys.Index(SpriteCatalogCategory.Bodies)) == null)
                Assert.Ignore("the catalog is not imported yet (Tools/Ragnarok/Import Catalog…)");
#endif
            AddressablesUnitAssetSource source = null;
            bool done = false;
            AddressablesUnitAssetSource.LoadAsync((s, e) =>
            {
                source = s;
                done = true;
            }, SpriteCatalogCategory.GroundItems);
            for (int i = 0; i < 1800 && !done; i++) yield return null;
            Assert.IsNotNull(source);

            var material = new Material(UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/sprite_unit.mat"));
            var root = new GameObject("Root");
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            camera.fieldOfView = 8f;
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 150f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.blue;
            camera.enabled = false;
            var target = new RenderTexture(Size, Size, 24);
            camera.targetTexture = target;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(root.transform, false);
            wall.transform.localPosition = new Vector3(0f, 2f, 1.1f);
            wall.transform.localScale = new Vector3(6f, 4f, 2f);

            var item = GroundItemView.Create(source, material, root.transform);
            item.gameObject.SetActive(true);
            Assert.IsTrue(item.Show(501, true)); // a red potion
            for (int i = 0; i < 600 && !item.Loaded; i++) yield return null;
            Assert.IsTrue(item.Loaded);
            var picture = item.Still != null && item.Still.enabled ? item.Still : item.Actor.LayerRenderers[0];
            // Items are small: a taller picture on the same renderer shows the rule clearly.
            var texture = new Texture2D(4, 16);
            var pixels = new Color32[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 0, 0, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            var tall = Sprite.Create(texture, new Rect(0, 0, 4, 16), new Vector2(0.5f, 0f), 10f);
            picture.sprite = tall;

            foreach (float tilt in new[] { 50f, 65f })
            {
                camera.transform.rotation = Quaternion.Euler(tilt, 0f, 0f);
                camera.transform.position = new Vector3(0f, 0.5f, 0f) - camera.transform.forward * 23f;
                item.Face(camera.transform.rotation);
                int shown = Red(camera, target);
                wall.SetActive(false);
                int whole = Red(camera, target);
                wall.SetActive(true);
                Assert.Greater(whole, 500, "the item is on screen");
                Assert.AreEqual(whole, shown, "tilt " + tilt);
            }

            item.Clear();
            camera.targetTexture = null;
            Object.Destroy(root);
            yield return null;
            source.Dispose();
            Object.Destroy(tall);
            Object.Destroy(texture);
            Object.Destroy(target);
            Object.Destroy(material);
        }

        static int Red(Camera camera, RenderTexture target)
        {
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var t = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            RenderTexture.active = previous;
            int n = 0;
            foreach (var p in t.GetPixels32())
                if (p.r > 200 && p.g < 60 && p.b < 60) n++;
            Object.DestroyImmediate(t);
            return n;
        }
    }
}
