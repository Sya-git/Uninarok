using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Uninarok.Sprites.Tests
{
    // TASKS-0007 T2.4 (FR-7, FR-1, plan 4.7): one sprite rendered with two palettes, from one sheet, through the shader
    // Uninarok/RoSprite. The sprite is 2 × 1 pixels: the left one indexed (index 1, stored red), the right one true colour
    // (index 0, cyan). Rendered by a camera into a 2 × 1 texture and read back.
    public class SpritePaletteTests
    {
        const string ShaderName = "Uninarok/RoSprite";
        const int Layer = 31;

        readonly List<Object> _owned = new List<Object>();
        SpriteActor _actor;
        Camera _camera;
        RenderTexture _target;

        static readonly Color32 Red = new Color32(255, 0, 0, 255);
        static readonly Color32 Cyan = new Color32(0, 200, 250, 255);

        [SetUp]
        public void SetUp()
        {
            SpriteSystem.ResetForTests();
            var shader = Shader.Find(ShaderName);
            Assert.IsNotNull(shader, ShaderName);

            var main = Own(new Texture2D(2, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point });
            main.SetPixels32(new[] { Red, Cyan });
            main.Apply();
            var index = Own(new Texture2D(2, 1, TextureFormat.R8, false, true) { filterMode = FilterMode.Point });
            index.SetPixelData(new byte[] { 1, 0 }, 0);
            index.Apply();

            var sprite = Own(Sprite.Create(main, new Rect(0, 0, 2, 1), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect,
                Vector4.zero, false, new[] { new SecondarySpriteTexture { name = "_IndexTex", texture = index } }));

            var anim = Own(ScriptableObject.CreateInstance<SpriteAnimation>());
            anim.Sprites = new[] { sprite };
            anim.PixelsPerUnit = 1f;
            anim.FileActionCount = 1;
            anim.ActionMap = new short[] { 0 };
            anim.Actions = new[] { new SpriteAnimation.ActionData { FirstFrame = 0, FrameCount = 1, FrameMs = 100f } };
            anim.Frames = new[] { new SpriteAnimation.FrameData { FirstLayer = 0, LayerCount = 1, EventId = -1 } };
            anim.Layers = new[] { new SpriteAnimation.LayerData { Slot = 0, ScaleX = 1f, ScaleY = 1f, Tint = new Color32(255, 255, 255, 255) } };
            anim.MaxLayers = 1;

            var go = Own(new GameObject("palette actor"));
            go.SetActive(false);
            _actor = go.AddComponent<SpriteActor>();
            _actor.Data = anim;
            _actor.Material = Own(new Material(shader));
            _actor.Billboard = false;
            go.SetActive(true);
            foreach (var r in _actor.LayerRenderers) r.gameObject.layer = Layer;
            _actor.Play(0, 0);
            SpriteSystem.Tick(0f);

            var cam = Own(new GameObject("palette camera"));
            _camera = cam.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 0.5f;
            _camera.transform.position = new Vector3(0f, 0f, -10f);
            _camera.cullingMask = 1 << Layer;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _camera.enabled = false;
            _target = new RenderTexture(2, 1, 24, RenderTextureFormat.ARGB32);
            _camera.targetTexture = _target;
            _camera.aspect = 2f;
        }

        [TearDown]
        public void TearDown()
        {
            if (_camera != null) _camera.targetTexture = null;
            if (_target != null) { _target.Release(); Object.DestroyImmediate(_target); }
            for (int i = _owned.Count - 1; i >= 0; i--)
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
            SpriteSystem.ResetForTests();
        }

        T Own<T>(T o) where T : Object
        {
            _owned.Add(o);
            return o;
        }

        Texture2D Palette(Color32 entry1)
        {
            var t = Own(new Texture2D(256, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp });
            var colors = new Color32[256];
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color32(255, 0, 255, 255);
            colors[1] = entry1;
            t.SetPixels32(colors);
            t.Apply();
            return t;
        }

        Color32[] Render()
        {
            _camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = _target;
            var read = new Texture2D(2, 1, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, 2, 1), 0, 0);
            read.Apply();
            RenderTexture.active = previous;
            var pixels = read.GetPixels32();
            Object.DestroyImmediate(read);
            return pixels;
        }

        static void AssertColor(Color32 expected, Color32 actual, string what) =>
            Assert.AreEqual((expected.r, expected.g, expected.b), (actual.r, actual.g, actual.b), what);

        [Test]
        public void TheShader_CompilesWithoutMessages()
        {
            var shader = Shader.Find(ShaderName);

            Assert.IsTrue(shader.isSupported);
            Assert.AreEqual(0, ShaderUtil.GetShaderMessageCount(shader), "no error or warning");
        }

        [Test]
        public void WithoutAPalette_ItRendersLikeTheMainTexture()
        {
            var p = Render();

            AssertColor(Red, p[0], "indexed pixel");
            AssertColor(Cyan, p[1], "true-colour pixel");
        }

        [Test]
        public void TwoPalettes_GiveTwoColours_FromOneSheet_AndCreateNoTexture()
        {
            var green = new Color32(10, 220, 30, 255);
            var blue = new Color32(40, 60, 230, 255);
            var a = Palette(green);
            var b = Palette(blue);
            int textures = Resources.FindObjectsOfTypeAll<Texture>().Length;

            SpritePalette.Apply(_actor, a);
            var withA = Render();
            SpritePalette.Apply(_actor, b);
            var withB = Render();

            Assert.AreEqual(textures, Resources.FindObjectsOfTypeAll<Texture>().Length, "no texture created by a palette swap");
            AssertColor(green, withA[0], "palette A");
            AssertColor(blue, withB[0], "palette B");
            AssertColor(Cyan, withA[1], "true colour unchanged with A");
            AssertColor(Cyan, withB[1], "true colour unchanged with B");
            Assert.AreSame(_actor.LayerRenderers[0].sprite.texture, ((Sprite)_owned.Find(o => o is Sprite)).texture, "same sheet");
        }

        [Test]
        public void Clear_GoesBackToTheSpritesOwnColours()
        {
            SpritePalette.Apply(_actor, Palette(new Color32(1, 2, 3, 255)));
            SpritePalette.Clear(_actor);

            AssertColor(Red, Render()[0], "own colour");
        }
    }
}
