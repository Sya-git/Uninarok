using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Uninarok.RoImport.Tests.Rsw
{
    // TASKS-0006 T1.1 (FR-1, FR-3): the synthetic maps hold the objects their builders describe.
    public class RswTestMapsTests
    {
        [Test]
        public void Full_HasItsObjectCounts()
        {
            var map = RswTestMaps.Full();
            Assert.AreEqual((10, 10), (map.Width, map.Height));
            Assert.AreEqual(11, map.Rsw.Models.Count);
            Assert.AreEqual(1, map.Rsw.Lights.Count);
            Assert.AreEqual(1, map.Rsw.Sounds.Count);
            Assert.AreEqual(1, map.Rsw.Effects.Count);
            Assert.AreEqual(1, map.Rsw.Models.Count(m => m.File == RswTestMaps.MissingModelFile));
        }

        [Test]
        public void Full_HasRotatedAndMirroredModels()
        {
            var models = RswTestMaps.Full().Rsw.Models;
            Assert.AreEqual(new Vector3(30, 0, 0), models.Single(m => m.Name == "rot_x").Rotation);
            Assert.AreEqual(new Vector3(-1, 1, 1), models.Single(m => m.Name == "mirror_x").Scale);
            Assert.AreEqual(new Vector3(-1, -1, -1), models.Single(m => m.Name == "mirror_xyz").Scale);
        }

        [Test]
        public void NonSquare_HasFourCornerModels_AndIsWiderThanHigh()
        {
            var map = RswTestMaps.NonSquare();
            Assert.AreEqual((6, 4), (map.Width, map.Height));
            Assert.AreEqual(4, map.Rsw.Models.Count);
            Assert.AreEqual(new Vector3(30, 0, 20), map.Rsw.Models.Single(m => m.Name == "corner_ne").Position);
        }
    }
}
