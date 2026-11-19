using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Tests.Rsw
{
    // TASKS-0006 T2.1: the synthetic file writer produces files GRF reads back with the same values, so converter tests
    // can use them.
    public class RswTestFilesTests
    {
        [Test]
        public void FullMap_RoundTripsThroughTheReader()
        {
            var source = RswTestMaps.Full().Rsw;
            bool ok = RoRswReader.TryRead(RoPath.Parse("test_full.rsw"), RswTestFiles.Rsw(source), out var rsw, out var reason);

            Assert.IsTrue(ok, reason);
            Assert.AreEqual((2, 1), (rsw.VersionMajor, rsw.VersionMinor));
            Assert.AreEqual(source.GndFile, rsw.GndFile);
            Assert.AreEqual(source.Water.Level, rsw.Water.Level);
            Assert.AreEqual(source.Light.Longitude, rsw.Light.Longitude);
            Assert.AreEqual(source.Light.ShadowOpacity, rsw.Light.ShadowOpacity);
            CollectionAssert.AreEqual(source.Models.Select(m => m.Name), rsw.Models.Select(m => m.Name));
            CollectionAssert.AreEqual(source.Models.Select(m => m.File), rsw.Models.Select(m => m.File));
            CollectionAssert.AreEqual(source.Models.Select(m => m.Scale), rsw.Models.Select(m => m.Scale));
            Assert.AreEqual(source.Lights[0].Range, rsw.Lights[0].Range);
            Assert.AreEqual(source.Sounds[0].File, rsw.Sounds[0].File);
            Assert.AreEqual(source.Effects[0].Preset, rsw.Effects[0].Preset);
            Assert.AreEqual(new Vector3(-50, 0, -50), rsw.Models[1].Position);
        }
    }
}
