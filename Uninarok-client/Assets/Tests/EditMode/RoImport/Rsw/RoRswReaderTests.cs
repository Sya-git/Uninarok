using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Rsw;
using Uninarok.RoImport.Tests.Gnd;

namespace Uninarok.RoImport.Tests.Rsw
{
    // TASKS-0006 T1.2 / T1.3 (FR-1). Counts and values are GRF's, recorded by T0.2 (plan 4.1, reference-models.md §4).
    [Category(GndTestData.Category)]
    public class RoRswReaderTests
    {
        const float Eps = 1e-3f;

        static RoRsw Read(string map)
        {
            bool ok = RoRswReader.TryRead(RoPath.Parse(map + ".rsw"), RswTestData.MapBytes(map), out var rsw, out var reason);
            Assert.IsTrue(ok, $"{map}: {reason}");
            Assert.IsNotNull(rsw);
            return rsw;
        }

        static void AssertNear(Vector3 expected, Vector3 actual, string what)
        {
            Assert.AreEqual(expected.x, actual.x, Eps, what + " x");
            Assert.AreEqual(expected.y, actual.y, Eps, what + " y");
            Assert.AreEqual(expected.z, actual.z, Eps, what + " z");
        }

        // map, models, distinct model files, lights, sounds, effects
        [TestCase("prt_fild08", 570, 23, 0, 51, 0)]
        [TestCase("prontera", 1303, 62, 0, 25, 152)]
        [TestCase("izlude", 552, 90, 5, 110, 12)]
        public void ReferenceMap_HasItsObjectCounts_AndVersion21(string map, int models, int distinct, int lights, int sounds, int effects)
        {
            var rsw = Read(map);

            Assert.AreEqual((2, 1, 0), (rsw.VersionMajor, rsw.VersionMinor, rsw.Build), "T0.2: every reference map is RSW 2.1");
            Assert.AreEqual(map + ".gnd", rsw.GndFile);
            Assert.AreEqual(map + ".gat", rsw.GatFile);
            Assert.AreEqual(models, rsw.Models.Count, "models");
            Assert.AreEqual(distinct, rsw.Models.Select(m => m.File.ToLowerInvariant()).Distinct().Count(), "distinct model files");
            Assert.AreEqual(lights, rsw.Lights.Count, "lights");
            Assert.AreEqual(sounds, rsw.Sounds.Count, "sounds");
            Assert.AreEqual(effects, rsw.Effects.Count, "effects");
        }

        [Test]
        public void PrtFild08_HeaderWaterLightAndObjects_HaveGrfValues()
        {
            var rsw = Read("prt_fild08");

            Assert.AreEqual(47f, rsw.Water.Level, Eps);
            Assert.AreEqual(1, rsw.Water.Type);
            Assert.AreEqual((0.3f, 3f, 100f), (rsw.Water.WaveHeight, rsw.Water.WaveSpeed, rsw.Water.WavePitch));
            Assert.AreEqual((45, 45), (rsw.Light.Longitude, rsw.Light.Latitude));
            Assert.AreEqual(Color.white, rsw.Light.Diffuse);
            Assert.AreEqual(0.3f, rsw.Light.Ambient.r, Eps);
            Assert.AreEqual(0.5f, rsw.Light.ShadowOpacity, Eps);

            var first = rsw.Models[0];
            Assert.AreEqual("프론테라\\성_성벽1", first.Name, "Korean names decode");
            Assert.AreEqual("프론테라\\성_성벽.rsm", first.File);
            Assert.AreEqual("Box01", first.Node);
            Assert.AreEqual((0, 1f), (first.AnimType, first.AnimSpeed));
            AssertNear(new Vector3(550, 0, 900), first.Position, "first position");

            var last = rsw.Models[rsw.Models.Count - 1];
            Assert.AreEqual("나무잡초꽃\\덤불02.rsm", last.File);
            AssertNear(new Vector3(-11.44696f, -28.51011f, 408.9137f), last.Position, "last position");
            AssertNear(new Vector3(31, 72, 358), last.Rotation, "last rotation");
            AssertNear(Vector3.one, last.Scale, "last scale");

            var sound = rsw.Sounds[0];
            Assert.AreEqual("se_prtbird_02.wav", sound.File);
            Assert.AreEqual((1.2f, 15, 15, 100f, 4f), (sound.Volume, sound.Width, sound.Height, sound.Range, sound.Interval));
        }

        [Test]
        public void Izlude_LightsEffectsAndMirroredModels_HaveGrfValues()
        {
            var rsw = Read("izlude");

            var light = rsw.Lights[0];
            Assert.AreEqual("광원1", light.Name);
            Assert.AreEqual(0.2f, light.Color.r, Eps);
            Assert.AreEqual(0.05f, light.Color.g, Eps);
            Assert.AreEqual(0f, light.Color.b, Eps);
            Assert.AreEqual(15f, light.Range, Eps);

            var effect = rsw.Effects[rsw.Effects.Count - 1];
            Assert.AreEqual((94, 200f, 0f, 0f, 0f, 0f), (effect.Preset, effect.Delay, effect.A, effect.B, effect.C, effect.D));

            var axe = rsw.Models[471];
            Assert.AreEqual("아인브로크\\도끼.rsm", axe.File);
            AssertNear(new Vector3(315, 90, 90), axe.Rotation, "axe rotation");
            AssertNear(new Vector3(0.66f, -0.66f, 0.66f), axe.Scale, "axe scale keeps its sign");
            Assert.AreEqual(5, rsw.Models.Count(m => m.Scale.y < 0), "T0.2: five models mirrored on Y");
        }

        [Test]
        public void Prontera_ClockTowers_HaveTheSlowAnimationSpeed()
        {
            var rsw = Read("prontera");
            var towers = rsw.Models.Where(m => m.File.EndsWith("시계탑.rsm")).ToList();
            Assert.AreEqual(2, towers.Count);
            Assert.IsTrue(towers.All(t => t.AnimType == 2 && Mathf.Abs(t.AnimSpeed - 0.01f) < 1e-5f));
        }

        [Test]
        public void TruncatedEmptyAndForeignFiles_ReturnFalseWithAReason_AndNeverThrow()
        {
            byte[] full = RswTestData.MapBytes("izlude");
            var cases = new[]
            {
                ("empty", new byte[0]),
                ("null", (byte[])null),
                ("header only", Slice(full, 6)),
                ("truncated", Slice(full, 1000)),
                ("half", Slice(full, full.Length / 2)),
                ("not an RSW", new byte[] { (byte)'G', (byte)'R', (byte)'G', (byte)'N', 1, 7, 0, 0, 0, 0 })
            };

            foreach (var (name, bytes) in cases)
            {
                bool ok = true;
                RoRsw rsw = null;
                string reason = null;
                Assert.DoesNotThrow(() => ok = RoRswReader.TryRead(RoPath.Parse("izlude.rsw"), bytes, out rsw, out reason), name);
                Assert.IsFalse(ok, name);
                Assert.IsNull(rsw, name);
                Assert.IsFalse(string.IsNullOrEmpty(reason), name + ": a reason");
            }
        }

        static byte[] Slice(byte[] bytes, int length)
        {
            var copy = new byte[length];
            Array.Copy(bytes, copy, length);
            return copy;
        }
    }
}
