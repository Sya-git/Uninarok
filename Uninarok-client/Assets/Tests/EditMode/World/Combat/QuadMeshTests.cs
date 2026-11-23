using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Uninarok.World.View;

namespace Uninarok.World.Tests
{
    // SPEC-0011 FR-6, FR-7, FR-12 (PLAN-0011 4.8, D6): one dynamic mesh of quads, written each frame without allocating.
    public class QuadMeshTests
    {
        static void Write(QuadMesh mesh, int quads, float shift)
        {
            mesh.Begin();
            for (int i = 0; i < quads; i++)
            {
                float x = i % 32 + shift, y = i / 32;
                mesh.Quad(new Vector3(x, y, 0f), new Vector3(x + 0.8f, y, 0f), new Vector3(x + 0.8f, y + 0.8f, 0f), new Vector3(x, y + 0.8f, 0f),
                          new Rect(0f, 0f, 1f, 1f), new Color32((byte)i, 255, 0, 255));
            }
            mesh.End();
        }

        [Test]
        public void QuadMesh_512Quads_EachFrame_AllocateNothing()
        {
            using var mesh = new QuadMesh("test", 512);
            Write(mesh, 512, 0f);
            Assert.AreEqual(512, mesh.Count);
            Assert.AreEqual(512 * 6, (int)mesh.Mesh.GetSubMesh(0).indexCount);
            Assert.AreEqual(new Vector3(31f, 15f, 0f), mesh.Mesh.vertices[511 * 4], "the last quad's first corner");

            float shift = 0f;
            Assert.That(() =>
            {
                for (int frame = 0; frame < 60; frame++) Write(mesh, 512, shift += 0.01f);
            }, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());

            Write(mesh, 3, 0f);
            Assert.AreEqual(18, (int)mesh.Mesh.GetSubMesh(0).indexCount, "only the quads written are drawn");
        }

        [Test]
        public void QuadMesh_Full_DropsTheRest()
        {
            using var mesh = new QuadMesh("full", 2);
            mesh.Begin();
            Assert.IsTrue(mesh.Quad(default, default, default, default, default, default));
            Assert.IsTrue(mesh.Quad(default, default, default, default, default, default));
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("is full"));
            Assert.IsFalse(mesh.Quad(default, default, default, default, default, default));
            Assert.IsFalse(mesh.Quad(default, default, default, default, default, default), "one warning only");
            mesh.End();
            Assert.AreEqual(2, mesh.Count);
        }
    }
}
