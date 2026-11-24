using NUnit.Framework;

namespace Uninarok.Effects.Tests
{
    /// <summary>Pins EffectRandom (SPEC-0012 plan 4.2, D19): a stateless hash of the seed and channel. The hash itself is not pinned.</summary>
    public class EffectRandomTests
    {
        [Test]
        public void SameSeedAndChannel_GiveTheSameValue()
        {
            float a = EffectRandom.Range(1234u, 0, -3f, 3f);
            float b = EffectRandom.Range(1234u, 0, -3f, 3f);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void DifferentSeeds_Differ()
        {
            var first = EffectRandom.Range(1u, 0, 0f, 1f);
            bool anyDiffers = false;
            for (uint s = 2; s <= 16; s++)
                if (EffectRandom.Range(s, 0, 0f, 1f) != first) anyDiffers = true;
            Assert.IsTrue(anyDiffers);
        }

        [Test]
        public void DifferentChannels_Differ()
        {
            var first = EffectRandom.Range(77u, 0, 0f, 1f);
            bool anyDiffers = false;
            for (int c = 1; c <= 16; c++)
                if (EffectRandom.Range(77u, c, 0f, 1f) != first) anyDiffers = true;
            Assert.IsTrue(anyDiffers);
        }

        [Test]
        public void Value_StaysInsideMinMax()
        {
            for (uint s = 0; s < 500; s++)
            {
                float v = EffectRandom.Range(s, (int)(s % 7), -3f, 3f);
                Assert.GreaterOrEqual(v, -3f);
                Assert.LessOrEqual(v, 3f);
            }
        }

        [Test]
        public void EqualMinAndMax_GiveThatValue()
        {
            Assert.AreEqual(2.5f, EffectRandom.Range(9u, 3, 2.5f, 2.5f), 1e-6f);
        }

        [Test]
        public void Range_AllocatesNothing()
        {
            EffectRandom.Range(1u, 0, 0f, 1f);
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            float sum = 0f;
            for (uint i = 0; i < 10000; i++) sum += EffectRandom.Range(i, 1, 0f, 1f);
            long after = System.GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0, after - before, "sum " + sum);
        }
    }
}
