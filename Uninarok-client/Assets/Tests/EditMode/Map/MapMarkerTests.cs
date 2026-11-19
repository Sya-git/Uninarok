using NUnit.Framework;
using UnityEngine;

namespace Uninarok.Map.Tests
{
    // TASKS-0006 T4.1 (FR-9, FR-10): each marker can be added to an object and holds its parameters.
    public class MapMarkerTests
    {
        GameObject _go;

        [SetUp]
        public void SetUp() => _go = new GameObject("marker");

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void LightMarker_HoldsColourAndRange()
        {
            var m = _go.AddComponent<MapLightMarker>();
            m.color = new Color(0.2f, 0.05f, 0f);
            m.range = 1.5f;
            Assert.AreEqual((new Color(0.2f, 0.05f, 0f), 1.5f), (m.color, m.range));
        }

        [Test]
        public void SoundMarker_HoldsItsParameters()
        {
            var m = _go.AddComponent<MapSoundMarker>();
            m.file = "se_prtbird_01.wav";
            m.volume = 0.8f;
            m.range = 10f;
            m.interval = 4f;
            m.width = 10;
            m.height = 12;
            Assert.AreEqual(("se_prtbird_01.wav", 0.8f, 10f, 4f, 10, 12), (m.file, m.volume, m.range, m.interval, m.width, m.height));
        }

        [Test]
        public void EffectMarker_HoldsItsParameters()
        {
            var m = _go.AddComponent<MapEffectMarker>();
            m.preset = 44;
            m.delay = 40f;
            m.a = 35f;
            m.b = 35f;
            Assert.AreEqual((44, 40f, 35f, 35f, 0f, 0f), (m.preset, m.delay, m.a, m.b, m.c, m.d));
        }

        [Test]
        public void MissingModelMarker_HoldsTheModelFile()
        {
            var m = _go.AddComponent<MapMissingModelMarker>();
            m.modelFile = "없는모델\\missing.rsm";
            Assert.AreEqual("없는모델\\missing.rsm", m.modelFile);
        }
    }
}
