using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Editor.Map;

namespace Uninarok.RoImport.Tests.Rsw
{
    // TASKS-0006 T5.2 (FR-5, FR-11): the names of a map scene are a stable contract with the additive scene.
    public class RoMapNamesTests
    {
        [Test]
        public void TheNames_ArePinned()
        {
            Assert.AreEqual("Map_prontera", RoMapNames.Root("prontera"));
            Assert.AreEqual("Ground", RoMapNames.Ground);
            Assert.AreEqual("Sun", RoMapNames.Sun);
            Assert.AreEqual(new[] { "Models", "Lights", "Sounds", "Effects" },
                new[] { RoMapNames.Group(RoMapGroup.Models), RoMapNames.Group(RoMapGroup.Lights), RoMapNames.Group(RoMapGroup.Sounds), RoMapNames.Group(RoMapGroup.Effects) });
            Assert.AreEqual("Models (1303)", RoMapNames.GroupLabel(RoMapGroup.Models, 1303));
        }

        [Test]
        public void IsGroup_MatchesTheBaseName_WithAnyCount()
        {
            Assert.IsTrue(RoMapNames.IsGroup("Models", RoMapGroup.Models));
            Assert.IsTrue(RoMapNames.IsGroup("Models (0)", RoMapGroup.Models));
            Assert.IsTrue(RoMapNames.IsGroup("Models (1303)", RoMapGroup.Models));
            Assert.IsFalse(RoMapNames.IsGroup("ModelsExtra", RoMapGroup.Models));
            Assert.IsFalse(RoMapNames.IsGroup("Models (", RoMapGroup.Models));
            Assert.IsFalse(RoMapNames.IsGroup("Lights (5)", RoMapGroup.Models));
            Assert.IsFalse(RoMapNames.IsGroup(null, RoMapGroup.Models));
        }

        [Test]
        public void FindGroup_FindsTheGroupByItsBaseName_WhateverTheCount()
        {
            var root = new GameObject(RoMapNames.Root("m"));
            try
            {
                new GameObject("Ground").transform.SetParent(root.transform);
                var models = new GameObject(RoMapNames.GroupLabel(RoMapGroup.Models, 3));
                models.transform.SetParent(root.transform);
                var sounds = new GameObject(RoMapNames.GroupLabel(RoMapGroup.Sounds, 0));
                sounds.transform.SetParent(root.transform);

                Assert.AreSame(models.transform, RoMapNames.FindGroup(root.transform, RoMapGroup.Models));
                Assert.AreSame(sounds.transform, RoMapNames.FindGroup(root.transform, RoMapGroup.Sounds));
                Assert.IsNull(RoMapNames.FindGroup(root.transform, RoMapGroup.Lights));
                Assert.IsNull(RoMapNames.FindGroup(null, RoMapGroup.Models));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OutputPaths_AreTheScenesBesideEachOther_AndTheSourcesBesideTheRsw()
        {
            var paths = RoMapOutputPaths.For(new RoOutputRoots(), RoPath.Parse("prontera.rsw"));

            Assert.AreEqual("prontera", paths.MapName);
            Assert.AreEqual("Assets/Scenes/Maps/prontera.unity", paths.Scene);
            Assert.AreEqual("Assets/Scenes/Maps/prontera.additive.unity", paths.AdditiveScene);
            Assert.AreEqual("prontera.gnd", paths.Gnd.Canonical);
            Assert.AreEqual("prontera.gat", paths.Gat.Canonical);
        }
    }
}
