using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Uninarok.Effects.PlayTests
{
    // SPEC-0012 FR-4, FR-7, FR-9 (plan 4.5 "AuthoredPart", D19, D20): quads, cylinders and procedural entries drawn into the
    // instance mesh. Assets are built in code; the runtime is ticked by hand (TimeScale 0 + Seek: the tick draws without
    // moving the clock), so no frame timing is involved.
    public class AuthoredPartPlayTests
    {
        const float Dt = 1f / 60f;
        const int CameraQuad = 0, GroundQuad = 1, Cylinder = 2, Ring = 3, BadSides = 4, Unknown = 5, RandomQuad = 6;

        sealed class FakeSource : IEffectAssetSource
        {
            public readonly Dictionary<string, EffectAsset> Assets = new Dictionary<string, EffectAsset>();
            public void Load(EffectAssetRef r, Action<EffectAsset> done) { Assets.TryGetValue(r.Guid, out var a); done(a); }
            public void Release(EffectAssetRef r) { }
        }

        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        EffectSettings settings;
        GameObject root;
        EffectRuntime runtime;
        EffectCatalog catalog;

        T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            created.Add(o);
            return o;
        }

        EffectAsset Authored(string name, EffectKind kind, AuthoredData data)
        {
            var a = Make<EffectAsset>();
            a.Path = "texture/effect/_authored/" + name; a.Kind = kind; a.Length = 1f; a.Authored = data;
            return a;
        }

        [SetUp]
        public void SetUp()
        {
            settings = Make<EffectSettings>();
            settings.PlaceholderSeconds = 0.25f;
            settings.AuthoredFrameRate = 60f;
            settings.Shader = Shader.Find("Uninarok/RoEffect");

            // quad of 2 cells at the offset (2, 4, 6) cells = (1, 3, 2) units (x, up, ground y), turned 90 degrees
            var cam = new AuthoredData
            {
                Facing = EffectFacing.Camera, SizePixels = 2f * settings.PixelsPerCell, Rotation = AnimationCurve.Constant(0f, 1f, 90f),
                StartMin = new Vector3(2f, 4f, 6f), StartMax = new Vector3(2f, 4f, 6f),
                EndMin = new Vector3(2f, 4f, 6f), EndMax = new Vector3(2f, 4f, 6f),
            };
            var ground = new AuthoredData
            {
                Facing = EffectFacing.Ground, SizePixels = 2f * settings.PixelsPerCell,
                StartMin = new Vector3(2f, 4f, 0f), StartMax = new Vector3(2f, 4f, 0f),
                EndMin = new Vector3(2f, 4f, 0f), EndMax = new Vector3(2f, 4f, 0f),
            };
            // radii 1 and 2 cells, height 4 cells, all scaled by a rising curve (1 at t 0, 2 at t 1); 20 sides
            var cyl = new AuthoredData
            {
                BottomRadius = 1f, TopRadius = 2f, Height = 4f, Sides = 20,
                RadiusScale = AnimationCurve.Linear(0f, 1f, 1f, 2f), HeightScale = AnimationCurve.Linear(0f, 1f, 1f, 2f),
            };
            var ring = new AuthoredData { BottomRadius = 1f, TopRadius = 1f, Height = 1f, Sides = 20, RepeatU = 3f };
            var bad = new AuthoredData { BottomRadius = 1f, TopRadius = 1f, Height = 1f, Sides = 2 };
            // a sparkle: the start and the end are drawn in boxes (D19)
            var random = new AuthoredData
            {
                Facing = EffectFacing.Camera, SizePixels = settings.PixelsPerCell,
                StartMin = new Vector3(-4f, -4f, 0f), StartMax = new Vector3(4f, 4f, 3f),
                EndMin = new Vector3(-4f, -4f, 0f), EndMax = new Vector3(4f, 4f, 3f),
            };

            var assets = new[]
            {
                Authored("cam.quad", EffectKind.Quad, cam), Authored("ground.quad", EffectKind.Quad, ground),
                Authored("cyl.cylinder", EffectKind.Cylinder, cyl), Authored("ring.cylinder", EffectKind.Cylinder, ring),
                Authored("bad.cylinder", EffectKind.Cylinder, bad),
                Authored("unknown.procedural", EffectKind.Procedural, new AuthoredData { Procedure = "nope" }),
                Authored("random.quad", EffectKind.Quad, random),
            };
            var source = new FakeSource();
            var index = Make<EffectCatalogIndex>();
            index.Keys = new string[assets.Length];
            index.Assets = new EffectAssetRef[assets.Length];
            index.SourceMissing = new bool[assets.Length];
            for (int i = 0; i < assets.Length; i++)
            {
                index.Keys[i] = assets[i].Path;
                index.Assets[i] = new EffectAssetRef { Guid = "g" + i };
                source.Assets["g" + i] = assets[i];
            }
            catalog = EffectCatalog.Create(index, source);
            root = new GameObject("AuthoredPartPlayTestsRoot");
            runtime = new EffectRuntime(catalog, settings, root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            runtime?.Dispose();
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            foreach (var o in created) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            created.Clear();
        }

        // Draws entry i looped at an exact Age (TimeScale 0: the tick draws without moving the clock).
        EffectMeshWriter DrawAt(int i, float seconds, out EffectInstance inst, bool loop = true)
        {
            runtime.TimeScale = 0f;
            var h = runtime.Play(new EffectRef(i), Vector3.zero, loop);
            runtime.Seek(h, seconds);
            runtime.Tick(Dt);
            inst = runtime.InstanceOf(h);
            return inst.GetWriter();
        }

        static void AssertNear(Vector3 expected, Vector3 actual, string what) =>
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(1e-5f), what + ": expected " + expected + " got " + actual);

        // ---- Quad (FR-4) ----

        [Test]
        public void CameraQuad_DrawsFourVertices_AtTheExpectedCorners()
        {
            var w = DrawAt(CameraQuad, 0.3f, out var inst);

            Assert.AreEqual(4, w.VertexCount);
            Assert.AreEqual(4, w.Mesh.vertexCount);
            Assert.AreEqual(1, w.SubMeshCount);
            Assert.That(inst.Length, Is.EqualTo(1f), "the part's bind gives the asset Length");
            // half size 0.5 units; 90 degrees: (x, y) -> (-y, x); centre (1, 3) in object x, y (z 0: the shader expands x and y)
            AssertNear(new Vector3(0.5f, 2.5f, 0f), w.Vertices[0].Position, "corner 0");
            AssertNear(new Vector3(0.5f, 3.5f, 0f), w.Vertices[1].Position, "corner 1");
            AssertNear(new Vector3(1.5f, 3.5f, 0f), w.Vertices[2].Position, "corner 2");
            AssertNear(new Vector3(1.5f, 2.5f, 0f), w.Vertices[3].Position, "corner 3");
            Assert.AreEqual(new Vector2(0f, 1f), w.Vertices[0].Uv);
            Assert.AreEqual(new Vector2(1f, 0f), w.Vertices[2].Uv);
            var mat = inst.Object.GetComponent<MeshRenderer>().sharedMaterials[0];
            Assert.IsTrue(mat.IsKeywordEnabled("_FACING_CAMERA"));
        }

        [Test]
        public void GroundQuad_IsFlatOnTheXzPlane_WithAGroundMaterial()
        {
            var w = DrawAt(GroundQuad, 0.3f, out var inst);

            Assert.AreEqual(4, w.VertexCount);
            for (int i = 0; i < 4; i++) Assert.AreEqual(0f, w.Vertices[i].Position.y, "vertex " + i + " is on y = 0");
            // centre (x 2 cells = 1 unit, ground y 4 cells = 2 units), half size 0.5, no rotation: local y is object +z
            AssertNear(new Vector3(0.5f, 0f, 2.5f), w.Vertices[0].Position, "corner 0");
            AssertNear(new Vector3(1.5f, 0f, 2.5f), w.Vertices[1].Position, "corner 1");
            AssertNear(new Vector3(1.5f, 0f, 1.5f), w.Vertices[2].Position, "corner 2");
            AssertNear(new Vector3(0.5f, 0f, 1.5f), w.Vertices[3].Position, "corner 3");
            var mat = inst.Object.GetComponent<MeshRenderer>().sharedMaterials[0];
            Assert.IsFalse(mat.IsKeywordEnabled("_FACING_CAMERA"), "ground facing: object space");
        }

        // ---- Cylinder (FR-4) ----

        [Test]
        public void Cylinder_Sides20_Has42Vertices_WithTheSampledRadiiAndHeight()
        {
            var w = DrawAt(Cylinder, 0.25f, out var inst);

            Assert.AreEqual(42, w.VertexCount);
            Assert.AreEqual(42, w.Mesh.vertexCount, "(Sides + 1) x 2");
            var data = inst.Asset.Authored;
            AuthoredSampler.SampleCylinder(data, settings, 0.25f, out var c);
            // by hand: scale 1.25 at t 0.25 -> bottom 1 x 1.25 x 0.5, top 2 x 1.25 x 0.5, height 4 x 1.25 x 0.5
            Assert.That(c.Bottom, Is.EqualTo(0.625f).Within(1e-5f));
            Assert.That(c.Top, Is.EqualTo(1.25f).Within(1e-5f));
            Assert.That(c.Height, Is.EqualTo(2.5f).Within(1e-5f));
            for (int i = 0; i <= 20; i++)
            {
                var b = w.Vertices[2 * i].Position;
                var t = w.Vertices[2 * i + 1].Position;
                Assert.That(new Vector2(b.x, b.z).magnitude, Is.EqualTo(c.Bottom).Within(1e-4f), "bottom ring " + i);
                Assert.That(new Vector2(t.x, t.z).magnitude, Is.EqualTo(c.Top).Within(1e-4f), "top ring " + i);
                Assert.AreEqual(0f, b.y);
                Assert.That(t.y, Is.EqualTo(c.Height).Within(1e-5f));
            }
            var mat = inst.Object.GetComponent<MeshRenderer>().sharedMaterials[0];
            Assert.IsFalse(mat.IsKeywordEnabled("_FACING_CAMERA"));
            Assert.IsFalse(mat.IsKeywordEnabled("_UV_REPEAT"), "RepeatU 1: clamp");
        }

        [Test]
        public void Cylinder_RepeatUAboveOne_UsesTheRepeatMaterial()
        {
            DrawAt(Ring, 0f, out var inst);
            var mat = inst.Object.GetComponent<MeshRenderer>().sharedMaterials[0];
            Assert.IsTrue(mat.IsKeywordEnabled("_UV_REPEAT"));
            Assert.That(inst.GetWriter().Vertices[40].Uv.x, Is.EqualTo(3f).Within(1e-5f), "u reaches RepeatU at the last point");
        }

        [Test]
        public void Cylinder_WithInvalidSides_DrawsNothing_AndSaysSo()
        {
            var w = DrawAt(BadSides, 0.1f, out var inst);

            var part = inst.Part as AuthoredPart;
            Assert.IsNotNull(part);
            Assert.IsFalse(part.CanDraw);
            StringAssert.Contains("Sides", part.Problem);
            Assert.AreEqual(0, w.VertexCount);
            Assert.AreEqual(0, w.Mesh.vertexCount);
            Assert.IsTrue(runtime.IsActive(inst.Handle), "the instance still keeps time (T5.8 adds the placeholder)");
        }

        // ---- the warp portal's parts: each its own instance ----

        [Test]
        public void TwoCylindersAndAQuad_EachInTheirOwnInstance()
        {
            runtime.TimeScale = 0f;
            var a = runtime.Play(new EffectRef(Cylinder), Vector3.zero, true);
            var b = runtime.Play(new EffectRef(Ring), Vector3.zero, true);
            var c = runtime.Play(new EffectRef(CameraQuad), Vector3.zero, true);
            runtime.Tick(Dt);

            var ia = runtime.InstanceOf(a); var ib = runtime.InstanceOf(b); var ic = runtime.InstanceOf(c);
            Assert.AreEqual(42, ia.GetWriter().Mesh.vertexCount);
            Assert.AreEqual(42, ib.GetWriter().Mesh.vertexCount);
            Assert.AreEqual(4, ic.GetWriter().Mesh.vertexCount);
            Assert.AreNotSame(ia.Object, ib.Object);
            Assert.AreNotSame(ia.GetWriter().Mesh, ib.GetWriter().Mesh);
            Assert.AreEqual(3, runtime.ActiveCount);
        }

        // ---- offset box (FR-7, D19) ----

        [Test]
        public void LoopedQuad_RepeatsTheSameOffsetPath_InTheNextCycle()
        {
            var w = DrawAt(RandomQuad, 0.3f, out var inst);
            var first = new Vector3[4];
            for (int i = 0; i < 4; i++) first[i] = w.Vertices[i].Position;
            Assert.AreEqual(0, inst.Cycle);

            runtime.Seek(inst.Handle, 1.3f);                      // the same cycle position, the next cycle
            runtime.Tick(Dt);

            Assert.AreEqual(1, inst.Cycle);
            for (int i = 0; i < 4; i++) AssertNear(first[i], w.Vertices[i].Position, "corner " + i + " in cycle 1");
            // the box is wide, so the draw is not the box's centre by accident
            Assert.That(Vector3.Distance(first[0], new Vector3(-0.5f, -0.5f, 0f)), Is.GreaterThan(1e-3f));
        }

        // ---- Procedural (FR-4, D20) ----

        [Test]
        public void Procedural_UnknownName_DrawsNothing_AndReports()
        {
            var w = DrawAt(Unknown, 0.1f, out var inst);

            var part = inst.Part as AuthoredPart;
            Assert.IsNotNull(part);
            Assert.IsFalse(part.CanDraw);
            StringAssert.Contains("nope", part.Problem);
            Assert.AreEqual(0, w.VertexCount);
            Assert.AreEqual(0, w.Mesh.vertexCount);
            Assert.IsFalse(EffectProcedures.TryGet("nope", out var p));
            Assert.IsNull(p);
            Assert.IsFalse(EffectProcedures.TryGet(null, out _));
        }
    }
}
