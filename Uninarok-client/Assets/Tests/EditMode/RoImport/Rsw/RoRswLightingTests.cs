using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Rsw;

namespace Uninarok.RoImport.Tests.Rsw
{
    // TASKS-0006 T3.1 / T3.2 (FR-8): the scene light from the RSW's global light (PLAN-0006 4.4, verified by T0.3).
    public class RoRswLightingTests
    {
        const float Eps = 1e-4f;

        static RoRswLight Light(int longitude, int latitude, float opacity = 0.5f) =>
            new RoRswLight(longitude, latitude, new Color(0.99f, 0.97f, 1f), new Color(0.55f, 0.5f, 0.5f), opacity);

        static void AssertNear(Vector3 expected, Vector3 actual, string what)
        {
            Assert.AreEqual(expected.x, actual.x, Eps, what + " x");
            Assert.AreEqual(expected.y, actual.y, Eps, what + " y");
            Assert.AreEqual(expected.z, actual.z, Eps, what + " z");
        }

        [Test]
        public void Colours_AreTheRswColours_WithNoFactor()
        {
            var light = Light(45, 45);
            Assert.AreEqual(light.Ambient, RoRswLighting.Ambient(light));
            Assert.AreEqual(light.Diffuse, RoRswLighting.Sun(light));
        }

        [TestCase(0.5f, 0.5f)]
        [TestCase(0.6f, 0.4f)]
        [TestCase(0f, 1f)]
        [TestCase(1f, 0f)]
        public void ShadowStrength_IsOneMinusTheOpacity(float opacity, float strength)
        {
            Assert.AreEqual(strength, RoRswLighting.ShadowStrength(Light(45, 45, opacity)), Eps);
        }

        // longitude, latitude (from the zenith), expected Sun.forward in Unity
        [TestCase(45, 45, 0.5f, -0.70710678f, -0.5f)]     // prontera and prt_fild08: from the north-west, 45° up
        [TestCase(0, 0, 0f, -1f, 0f)]                     // straight down
        [TestCase(0, 90, 1f, 0f, 0f)]                     // on the horizon, from -X
        [TestCase(90, 90, 0f, 0f, -1f)]                   // on the horizon, from +Z
        [TestCase(70, 20, 0.11697778f, -0.93969262f, -0.32139380f)]   // izlude
        public void SunRotation_FollowsLongitudeAndLatitude(int longitude, int latitude, float x, float y, float z)
        {
            var forward = RoRswLighting.SunRotation(Light(longitude, latitude)) * Vector3.forward;
            AssertNear(new Vector3(x, y, z), forward, $"lon {longitude} lat {latitude}");
        }
    }
}
