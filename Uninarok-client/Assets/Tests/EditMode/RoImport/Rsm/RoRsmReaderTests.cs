using System;
using System.Linq;
using NUnit.Framework;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Rsm;

namespace Uninarok.RoImport.Tests.Rsm
{
    // TASKS-0003 T1.2 (FR-8, FR-2). The expected values come from a raw read of the files that does not use GRF
    // (Fixtures~/reference-nodes.csv, reference-models.md §7), so these tests check the adapter, not GRF against itself.
    [Category(RsmTestData.Category)]
    public class RoRsmReaderTests
    {
        static RoRsm Read(string model)
        {
            byte[] bytes = RsmTestData.ModelBytes(model);
            bool ok = RoRsmReader.TryRead(RoPath.Parse("model/" + model), bytes, out var rsm, out var reason);
            Assert.IsTrue(ok, $"{model}: {reason}");
            Assert.IsNotNull(rsm);
            return rsm;
        }

        static readonly string[] ReferenceModels = RsmTestData.ReferenceModels().ToArray();

        [TestCaseSource(nameof(ReferenceModels))]
        public void ReferenceModel_ReadsWithTheExpectedNodes(string model)
        {
            var rsm = Read(model);
            var expected = RsmTestData.ReferenceNodes().Where(n => n.Model == model).ToList();

            Assert.AreEqual(1, rsm.VersionMajor, "major version (T0.3: the data is all 1.4)");
            Assert.AreEqual(4, rsm.VersionMinor, "minor version");
            Assert.IsTrue(rsm.HasShadeType);
            CollectionAssert.AreEqual(expected.Select(n => n.Name), rsm.Nodes.Select(n => n.Name), "node names in file order");
            CollectionAssert.AreEqual(expected.Select(n => n.Parent), rsm.Nodes.Select(n => n.ParentName ?? string.Empty), "parent names");
            for (int i = 0; i < expected.Count; i++)
            {
                var e = expected[i];
                var n = rsm.Nodes[i];
                Assert.AreEqual(e.Vertices, n.Vertices.Length, $"{e.Name} vertices");
                Assert.AreEqual(e.TexCoords, n.TexCoords.Length, $"{e.Name} texture coordinates");
                Assert.AreEqual(e.Faces, n.Faces.Length, $"{e.Name} faces");
                Assert.AreEqual(e.PosKeys, n.PosKeys.Length, $"{e.Name} position keys");
                Assert.AreEqual(e.RotKeys, n.RotKeys.Length, $"{e.Name} rotation keys");
                Assert.AreEqual(0, n.ScaleKeys.Length, $"{e.Name} scale keys (1.x has none)");
                Assert.AreEqual(9, n.OffsetMatrix.Length, $"{e.Name} offset matrix");
            }
        }

        [Test]
        public void SingleNode_HasTheFileValues()
        {
            // 나무잡초꽃/버섯02, raw values recorded by T0.2 (PLAN-0003 4.1).
            var rsm = Read("나무잡초꽃/버섯02.rsm");
            Assert.AreEqual(RsmShadeType.Smooth, rsm.ShadeType);
            Assert.AreEqual(16000, rsm.AnimationLength);
            var n = rsm.Nodes.Single();
            CollectionAssert.AreEqual(new[] { 1f, 0f, 0f, 0f, 0f, 1f, 0f, -1f, 0f }, n.OffsetMatrix);
            Assert.AreEqual(0f, n.Offset.sqrMagnitude);
            Assert.AreEqual(-3.4286f, n.Position.x, 1e-6f);
            Assert.AreEqual(1.6567f, n.Position.y, 1e-6f);
            Assert.AreEqual(22.7747f, n.Position.z, 1e-6f);
            Assert.AreEqual(-3.927f, n.RotationAngle, 1e-6f);
            Assert.AreEqual(new UnityEngine.Vector3(0f, 1f, 0f), n.RotationAxis);
            Assert.AreEqual(0.0901f, n.Scale.x, 1e-6f);
            Assert.AreEqual(13.9166f, n.Vertices[0].x, 1e-4f);
            Assert.AreEqual(-1.3885f, n.Vertices[0].y, 1e-4f);
            CollectionAssert.AreEqual(new[] { 0 }, n.TextureSlots);
            var f = n.Faces[0];
            Assert.AreEqual((0, 5, 4, 12, 13, 14, 0, false, 4), (f.V0, f.V1, f.V2, f.T0, f.T1, f.T2, f.TextureSlot, f.TwoSided, f.SmoothGroup));
        }

        [Test]
        public void ShadeTypes_AreRead()
        {
            Assert.AreEqual(RsmShadeType.Flat, Read("나무잡초꽃/넝쿨01.rsm").ShadeType);
            Assert.AreEqual(RsmShadeType.None, Read("izlude/iz_enchant.rsm").ShadeType);
            Assert.AreEqual(RsmShadeType.Smooth, Read("프론테라/시계탑.rsm").ShadeType);
        }

        [Test]
        public void OffsetVector_IsRead()
        {
            // 외부소품/벤치01 is in the "node offset non-zero" samples of reference-models.md §3.
            var rsm = Read("외부소품/벤치01.rsm");
            Assert.Greater(rsm.Nodes.Max(n => n.Offset.sqrMagnitude), 0f);
        }

        [Test]
        public void RotationKeys_AreReadInFileUnits()
        {
            // 프론테라/시계탑 Box05: 49 keys 16000 apart, the second a quarter turn about +Z (T0.4).
            var hand = Read("프론테라/시계탑.rsm").Nodes.Single(n => n.Name == "Box05");
            Assert.AreEqual(49, hand.RotKeys.Length);
            Assert.AreEqual(0, hand.RotKeys[0].Time);
            Assert.AreEqual(16000, hand.RotKeys[1].Time);
            Assert.AreEqual(768000, hand.RotKeys[48].Time);
            Assert.AreEqual(0.7071081f, hand.RotKeys[1].Rotation.z, 1e-6f);
            Assert.AreEqual(0.7071055f, hand.RotKeys[1].Rotation.w, 1e-6f);
        }

        [Test]
        public void KoreanTextureNames_AreDecodedAndExist()
        {
            // PLAN-0003 4.1 "Text encoding": without the Korean encoding these names are lost.
            var rsm = Read("프론테라/시계탑.rsm");
            CollectionAssert.AreEqual(
                new[] { "프론테라\\pron-ctwr1.bmp", "프론테라\\pron-ctwr2.bmp", "프론테라\\pron-ctwr3.bmp" }, rsm.Textures);
            foreach (var t in rsm.Textures) Assert.IsTrue(RsmTestData.TextureExists(t), t);
        }

        [Test]
        public void Reading_LeavesGrfsDisplayEncodingAsItWas()
        {
            var before = Utilities.Services.EncodingService.DisplayEncoding;
            Read("프론테라/시계탑.rsm");
            Assert.AreSame(before, Utilities.Services.EncodingService.DisplayEncoding);
        }

        [Test]
        public void TruncatedFile_FailsWithAReason_AndDoesNotThrow()
        {
            byte[] bytes = RsmTestData.ModelBytes("프론테라/시계탑.rsm");
            foreach (int length in new[] { 3, 6, 40, bytes.Length / 2, bytes.Length - 30 })
            {
                var cut = new byte[length];
                Array.Copy(bytes, cut, length);
                bool ok = true; RoRsm rsm = null; string reason = null;
                Assert.DoesNotThrow(() => ok = RoRsmReader.TryRead(RoPath.Parse("model/cut.rsm"), cut, out rsm, out reason));
                Assert.IsFalse(ok, $"length {length}");
                Assert.IsNull(rsm);
                Assert.IsFalse(string.IsNullOrEmpty(reason));
            }
        }

        [Test]
        public void EmptyAndNull_FailWithAReason()
        {
            foreach (var bytes in new[] { new byte[0], null })
            {
                Assert.IsFalse(RoRsmReader.TryRead(RoPath.Parse("model/empty.rsm"), bytes, out var rsm, out var reason));
                Assert.IsNull(rsm);
                Assert.IsFalse(string.IsNullOrEmpty(reason));
            }
        }

        [Test]
        public void NotAnRsm_FailsWithAReason()
        {
            var bytes = System.Text.Encoding.ASCII.GetBytes("GRAT this is not a model, just some bytes that are long enough");
            Assert.IsFalse(RoRsmReader.TryRead(RoPath.Parse("model/x.rsm"), bytes, out var rsm, out var reason));
            Assert.IsNull(rsm);
            Assert.IsFalse(string.IsNullOrEmpty(reason));
        }
    }
}
