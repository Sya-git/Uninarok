using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Uninarok.Effects.Tests
{
    /// <summary>EffectMeshWriter: vertex format, submesh layouts, cylinder strip, hidden quads, materials on change, 0 B per frame (SPEC-0012 FR-3, FR-4, FR-9; plan 4.5, D28).</summary>
    public class EffectMeshWriterTests
    {
        const string SettingsPath = "Assets/Settings/Effects/EffectSettings.asset";

        GameObject go;
        EffectMeshWriter writer;
        EffectSettings settings;
        EffectMaterials materials;
        Texture2D texA, texB;
        Bounds bounds;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("EffectMeshWriterTests") { hideFlags = HideFlags.HideAndDontSave };
            writer = new EffectMeshWriter(go.AddComponent<MeshFilter>(), go.AddComponent<MeshRenderer>());
            settings = ScriptableObject.CreateInstance<EffectSettings>();
            var real = AssetDatabase.LoadAssetAtPath<EffectSettings>(SettingsPath);
            settings.Shader = real.Shader;
            settings.PlaceholderTexture = real.PlaceholderTexture;
            materials = new EffectMaterials(settings);
            texA = new Texture2D(2, 2);
            texB = new Texture2D(2, 2);
            bounds = new Bounds(Vector3.zero, new Vector3(4, 4, 4));
        }

        [TearDown]
        public void TearDown()
        {
            writer.Dispose();
            materials.Dispose();
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(texA);
            UnityEngine.Object.DestroyImmediate(texB);
            UnityEngine.Object.DestroyImmediate(settings);
        }

        static CylinderSample Cylinder(float bottom = 1f, float top = 1.5f, float height = 0.55f, float angle = 0f)
            => new CylinderSample { Bottom = bottom, Top = top, Height = height, AngleDegrees = angle, Color = new Color(0.5f, 0.5f, 1f, 0.4f) };

        void WriteLayer(int submesh, float x)
        {
            writer.WriteQuad(submesh, 0,
                new Vector3(x - 1, 1, 0), new Vector3(x + 1, 1, 0), new Vector3(x + 1, -1, 0), new Vector3(x - 1, -1, 0),
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0), new Color32(255, 128, 64, 200));
        }

        EffectVertex[] ReadBack()
        {
            using (var data = Mesh.AcquireReadOnlyMeshData(writer.Mesh))
                return data[0].GetVertexData<EffectVertex>(0).ToArray();
        }

        [Test]
        public void Vertex_Is24BytesInAttributeOrder()
        {
            Assert.AreEqual(24, UnsafeUtility.SizeOf<EffectVertex>());
            Assert.AreEqual(0, (int)Marshal.OffsetOf<EffectVertex>(nameof(EffectVertex.Position)));
            Assert.AreEqual(12, (int)Marshal.OffsetOf<EffectVertex>(nameof(EffectVertex.Color)));
            Assert.AreEqual(16, (int)Marshal.OffsetOf<EffectVertex>(nameof(EffectVertex.Uv)));

            writer.BeginLayout(); writer.AddQuads(); Assert.IsTrue(writer.EndLayout(bounds));
            var m = writer.Mesh;
            Assert.AreEqual(24, m.GetVertexBufferStride(0));
            Assert.AreEqual(0, m.GetVertexAttributeOffset(VertexAttribute.Position));
            Assert.AreEqual(12, m.GetVertexAttributeOffset(VertexAttribute.Color));
            Assert.AreEqual(16, m.GetVertexAttributeOffset(VertexAttribute.TexCoord0));
            Assert.AreEqual(VertexAttributeFormat.UNorm8, m.GetVertexAttributeFormat(VertexAttribute.Color));
            Assert.AreEqual(IndexFormat.UInt16, m.indexFormat);
        }

        [Test]
        public void ThreeLayerStr_Writes12VerticesIn3SubMeshes()
        {
            writer.BeginLayout();
            for (int i = 0; i < 3; i++) Assert.AreEqual(i, writer.AddQuads());
            Assert.IsTrue(writer.EndLayout(bounds));
            for (int i = 0; i < 3; i++) WriteLayer(i, i * 10);
            writer.Upload();

            var m = writer.Mesh;
            Assert.AreEqual(12, m.vertexCount);
            Assert.AreEqual(3, m.subMeshCount);
            for (int s = 0; s < 3; s++)
            {
                var d = m.GetSubMesh(s);
                Assert.AreEqual(6 * s, d.indexStart, "indexStart");
                Assert.AreEqual(6, d.indexCount, "indexCount");
                Assert.AreEqual(4 * s, d.firstVertex, "firstVertex");
                Assert.AreEqual(4, d.vertexCount, "vertexCount");
                Assert.AreEqual(0, d.baseVertex, "baseVertex");
                int v = 4 * s;
                CollectionAssert.AreEqual(new[] { v, v + 1, v + 2, v, v + 2, v + 3 }, m.GetIndices(s));
            }
            Assert.AreEqual(bounds, m.bounds, "bounds as set, not recalculated");

            var vb = ReadBack();
            Assert.AreEqual(new Vector3(21, 1, 0), vb[9].Position, "layer 2, corner 1 (top-right)");
            Assert.AreEqual(new Color32(255, 128, 64, 200), vb[9].Color);
            Assert.AreEqual(new Vector2(1, 1), vb[9].Uv);
        }

        [Test]
        public void Cylinder20_Writes42VerticesBottomTopPairs()
        {
            writer.BeginLayout();
            int s = writer.AddCylinder(20);
            Assert.IsTrue(writer.EndLayout(bounds));
            writer.WriteCylinder(s, Cylinder(), 1f);
            writer.Upload();

            var m = writer.Mesh;
            Assert.AreEqual(42, m.vertexCount);
            Assert.AreEqual(EffectMeshWriter.CylinderVertices(20), 42);
            Assert.AreEqual(120, m.GetSubMesh(0).indexCount);
            var vb = ReadBack();
            for (int i = 0; i <= 20; i++)
            {
                EffectVertex b = vb[2 * i], t = vb[2 * i + 1];
                Assert.AreEqual(i / 20f, b.Uv.x, 1e-6f, "bottom u " + i);
                Assert.AreEqual(i / 20f, t.Uv.x, 1e-6f, "top u " + i);
                Assert.AreEqual(1f, b.Uv.y, "bottom v");
                Assert.AreEqual(0f, t.Uv.y, "top v");
                Assert.AreEqual(0f, b.Position.y, 1e-6f, "bottom ring y 0");
                Assert.AreEqual(0.55f, t.Position.y, 1e-6f, "top ring at Height");
                Assert.AreEqual(1f, new Vector2(b.Position.x, b.Position.z).magnitude, 1e-5f, "bottom radius");
                Assert.AreEqual(1.5f, new Vector2(t.Position.x, t.Position.z).magnitude, 1e-5f, "top radius");
            }
            Assert.AreEqual(vb[0].Position.x, vb[40].Position.x, 1e-5f, "seam closes");
            Assert.AreEqual(vb[0].Position.z, vb[40].Position.z, 1e-5f, "seam closes");
            Assert.AreEqual(new Color32(128, 128, 255, 102), vb[0].Color);

            int[] idx = m.GetIndices(0);
            CollectionAssert.AreEqual(new[] { 0, 1, 3, 0, 3, 2 }, new ArraySegment<int>(idx, 0, 6));
            CollectionAssert.AreEqual(new[] { 38, 39, 41, 38, 41, 40 }, new ArraySegment<int>(idx, 114, 6));
        }

        [Test]
        public void Cylinder_RepeatUAndAngle()
        {
            writer.BeginLayout();
            int s = writer.AddCylinder(4);
            Assert.IsTrue(writer.EndLayout(bounds));
            writer.WriteCylinder(s, Cylinder(angle: 90f), 3f);
            writer.Upload();
            var vb = ReadBack();
            Assert.AreEqual(3f, vb[8].Uv.x, 1e-6f, "last u = RepeatU");
            Assert.AreEqual(0.75f, vb[2].Uv.x, 1e-6f);
            // point 0 at angle 0 is +x; turned like Quaternion.Euler(0, 90, 0).
            Vector3 expected = Quaternion.Euler(0, 90, 0) * new Vector3(1, 0, 0);
            Assert.AreEqual(expected.x, vb[0].Position.x, 1e-5f);
            Assert.AreEqual(expected.z, vb[0].Position.z, 1e-5f);
        }

        [Test]
        public void MixedLayout_OffsetsFollowSubMeshOrder()
        {
            writer.BeginLayout();
            writer.AddQuads(); writer.AddCylinder(20); writer.AddQuads(3);
            Assert.IsTrue(writer.EndLayout(bounds));
            var m = writer.Mesh;
            Assert.AreEqual(4 + 42 + 12, m.vertexCount);
            Assert.AreEqual(3, m.subMeshCount);
            Assert.AreEqual(4, m.GetSubMesh(1).firstVertex);
            Assert.AreEqual(6, m.GetSubMesh(1).indexStart);
            Assert.AreEqual(46, m.GetSubMesh(2).firstVertex);
            Assert.AreEqual(126, m.GetSubMesh(2).indexStart);
            Assert.AreEqual(18, m.GetSubMesh(2).indexCount);
            Assert.AreEqual(4, m.GetIndices(1)[0]);
            Assert.AreEqual(46 + 8 + 3, m.GetIndices(2)[17]);
        }

        [Test]
        public void HiddenLayer_IsZeroAreaQuad()
        {
            writer.BeginLayout(); writer.AddQuads(); writer.AddQuads(); writer.EndLayout(bounds);
            WriteLayer(0, 0); WriteLayer(1, 5);
            writer.HideQuad(1, 0);
            writer.Upload();
            var vb = ReadBack();
            for (int i = 4; i < 8; i++)
            {
                Assert.AreEqual(vb[4].Position, vb[i].Position, "all corners equal");
                Assert.AreEqual(0, vb[i].Color.a, "alpha 0");
            }
            Assert.AreNotEqual(vb[0].Position, vb[2].Position, "the visible layer keeps its area");
            Assert.AreEqual(2, writer.Mesh.subMeshCount, "the hidden layer keeps its submesh");
        }

        [Test]
        public void Rebind_ZeroesVerticesAndSkipsUnchangedLayout()
        {
            writer.BeginLayout(); writer.AddQuads(); writer.AddCylinder(8); writer.EndLayout(bounds);
            WriteLayer(0, 3); writer.WriteCylinder(1, Cylinder(), 1f); writer.Upload();
            Assert.AreEqual(1, writer.LayoutUploads);

            var other = new Bounds(Vector3.one, Vector3.one * 9);
            writer.BeginLayout(); writer.AddQuads(); writer.AddCylinder(8); Assert.IsTrue(writer.EndLayout(other));
            Assert.AreEqual(1, writer.LayoutUploads, "same layout: no index or submesh upload");
            Assert.AreEqual(other, writer.Mesh.bounds, "bounds set at every bind");
            foreach (var v in ReadBack()) Assert.AreEqual(default(EffectVertex), v, "nothing stale from the last play");

            writer.BeginLayout(); writer.AddQuads(); writer.AddCylinder(9); writer.EndLayout(bounds);
            Assert.AreEqual(2, writer.LayoutUploads);
            Assert.AreEqual(4 + 20, writer.Mesh.vertexCount);
        }

        // T8.11 (FR-6, plan D40): HideAll zeroes the uploaded vertices and keeps the layout
        [Test]
        public void HideAll_ZeroesUploadedVerticesAndKeepsLayout_FR6()
        {
            writer.BeginLayout(); writer.AddQuads(); writer.AddCylinder(8); writer.EndLayout(bounds);
            WriteLayer(0, 3); writer.WriteCylinder(1, Cylinder(), 1f); writer.Upload();
            Assert.IsTrue(System.Linq.Enumerable.Any(ReadBack(), v => !v.Equals(default(EffectVertex))), "something was drawn");
            writer.HideAll();
            foreach (var v in ReadBack()) Assert.AreEqual(default(EffectVertex), v, "uploaded vertices zeroed");
            Assert.AreEqual(2, writer.SubMeshCount);
            Assert.AreEqual(1, writer.LayoutUploads);
        }

        [Test]
        public void OverLimitOrBadSides_FailsAndLeavesMeshEmpty()
        {
            writer.BeginLayout(); writer.AddQuads(); writer.EndLayout(bounds);

            writer.BeginLayout();
            Assert.AreEqual(-1, writer.AddCylinder(2));
            Assert.IsFalse(writer.EndLayout(bounds));
            Assert.AreEqual(0, writer.Mesh.vertexCount);
            Assert.AreEqual(0, writer.SubMeshCount);

            writer.BeginLayout();
            Assert.AreEqual(0, writer.AddQuads(EffectMeshWriter.MaxVertices / 4));
            Assert.AreEqual(-1, writer.AddQuads(), "past 65,535 vertices");
            Assert.IsFalse(writer.EndLayout(bounds));
            Assert.AreEqual(0, writer.Mesh.vertexCount);

            writer.BeginLayout();
            Assert.AreEqual(-1, writer.AddCylinder(int.MaxValue), "no overflow on (sides + 1) * 2");
            Assert.AreEqual(-1, writer.AddQuads(int.MaxValue));
            Assert.IsFalse(writer.EndLayout(bounds));

            writer.Upload();     // an empty mesh uploads nothing and does not throw
            Assert.Throws<ArgumentOutOfRangeException>(() => writer.HideQuad(0, 0));
        }

        [Test]
        public void Materials_SetOnlyWhenChanged()
        {
            var r = go.GetComponent<MeshRenderer>();
            writer.BeginLayout(); for (int i = 0; i < 3; i++) writer.AddQuads(); writer.EndLayout(bounds);
            var alpha = materials.Get(texA, 5, 6, EffectFacing.Camera, false);
            var add = materials.Get(texA, 5, 2, EffectFacing.Camera, false);
            var other = materials.Get(texB, 5, 6, EffectFacing.Camera, false);

            writer.SetMaterial(0, alpha); writer.SetMaterial(1, add); writer.SetMaterial(2, other);
            writer.Upload();
            Assert.AreEqual(1, writer.MaterialUploads);
            CollectionAssert.AreEqual(new[] { alpha, add, other }, r.sharedMaterials);

            for (int f = 0; f < 10; f++)
            {
                writer.SetMaterial(0, alpha); writer.SetMaterial(1, add); writer.SetMaterial(2, other);
                writer.SetMaterial(1, materials.Get(texA, 5, 2, EffectFacing.Camera, false));   // same key, same instance
                writer.Upload();
            }
            Assert.AreEqual(1, writer.MaterialUploads, "unchanged keys: no SetSharedMaterials");

            writer.SetMaterial(1, alpha);                                                  // layer 1's key changes blend
            writer.Upload();
            Assert.AreEqual(2, writer.MaterialUploads);
            CollectionAssert.AreEqual(new[] { alpha, alpha, other }, r.sharedMaterials);
        }

        [Test]
        public void ThousandFrames_AllocateNothingAfterWarmUp()
        {
            writer.BeginLayout(); writer.AddQuads(); writer.AddQuads(); writer.AddQuads(); writer.AddCylinder(20); writer.EndLayout(bounds);
            var alpha = materials.Get(texA, 5, 6, EffectFacing.Camera, false);
            var add = materials.Get(texA, 5, 2, EffectFacing.Camera, false);
            var cyl = materials.Get(texB, 5, 2, EffectFacing.Ground, false);

            void Frame(int f)
            {
                WriteLayer(0, f * 0.01f); WriteLayer(1, -f * 0.01f);
                if ((f & 1) == 0) WriteLayer(2, 1f); else writer.HideQuad(2, 0);
                writer.WriteCylinder(3, Cylinder(angle: f), 1f);
                writer.SetMaterial(0, alpha); writer.SetMaterial(1, (f / 100 & 1) == 0 ? alpha : add);
                writer.SetMaterial(2, add); writer.SetMaterial(3, cyl);
                writer.Upload();
            }

            for (int f = 0; f < 210; f++) Frame(f);                       // warm-up, including a material swap
            int uploads = writer.MaterialUploads;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int f = 0; f < 1000; f++) Frame(f);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated, "B over 1,000 frames");
            Assert.Greater(writer.MaterialUploads, uploads, "the swaps happened inside the measured loop");

            // Rebinding layouts already seen allocates nothing either (pool reuse); the larger one is warmed up first.
            writer.BeginLayout(); writer.AddQuads(); writer.AddQuads(); writer.AddQuads(); writer.AddCylinder(21); writer.EndLayout(bounds);
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++)
            {
                writer.BeginLayout(); writer.AddQuads(); writer.AddQuads(); writer.AddQuads(); writer.AddCylinder(20 + (i & 1)); writer.EndLayout(bounds);
            }
            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before, "B over 100 binds");
        }

        [Test]
        public void Dispose_DestroysMeshAndIsIdempotent()
        {
            writer.BeginLayout(); writer.AddCylinder(20); writer.EndLayout(bounds);
            var m = writer.Mesh;
            writer.Dispose();
            Assert.IsTrue(m == null);
            Assert.IsNull(go.GetComponent<MeshFilter>().sharedMesh);
            writer.Dispose();
            Assert.Throws<ObjectDisposedException>(() => writer.Upload());
        }
    }
}
