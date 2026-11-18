using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Imaging;
using Uninarok.RoImport.Editor.Writing;

namespace Uninarok.RoImport.Tests
{
    // SPEC-0002 FR-8 (textures output as PNG, D7), FR-16 (identical files on rerun). Plan 4.4. TASKS T4.2.
    public class RoPngCodecTests
    {
        static RoImage Sample(bool withAlpha)
        {
            // 3 x 2, odd width; distinct value per pixel so a row flip or channel swap cannot pass.
            var image = new RoImage(3, 2);
            for (int y = 0; y < 2; y++)
            for (int x = 0; x < 3; x++)
                image.SetPixel(x, y, new Color32((byte)(10 + x * 40), (byte)(20 + y * 100), (byte)(200 - x * 30 - y * 7),
                    withAlpha ? (byte)(x * 120 + y * 7) : (byte)255));
            return image;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EncodeThenDecode_ReturnsIdenticalPixels(bool withAlpha)
        {
            var codec = new RoPngCodec();
            var original = Sample(withAlpha);

            byte[] png = codec.Encode(original);
            Assert.IsTrue(codec.TryDecode(png, out var decoded));

            Assert.AreEqual(original.Width, decoded.Width);
            Assert.AreEqual(original.Height, decoded.Height);
            CollectionAssert.AreEqual(original.Rgba32, decoded.Rgba32);
        }

        [Test]
        public void Decode_KeepsTopRowFirst()
        {
            var codec = new RoPngCodec();
            var image = new RoImage(1, 2);
            image.SetPixel(0, 0, new Color32(255, 0, 0, 255));      // top: red
            image.SetPixel(0, 1, new Color32(0, 0, 255, 255));      // bottom: blue

            Assert.IsTrue(codec.TryDecode(codec.Encode(image), out var decoded));

            Assert.AreEqual(new Color32(255, 0, 0, 255), decoded.GetPixel(0, 0));
            Assert.AreEqual(new Color32(0, 0, 255, 255), decoded.GetPixel(0, 1));
        }

        [Test]
        public void Encode_IsDeterministic()
        {
            var codec = new RoPngCodec();

            CollectionAssert.AreEqual(codec.Encode(Sample(true)), codec.Encode(Sample(true)));
        }

        [Test]
        public void InvalidBytes_DoNotDecode()
        {
            var codec = new RoPngCodec();

            Assert.IsFalse(codec.TryDecode(null, out _));
            Assert.IsFalse(codec.TryDecode(new byte[0], out _));
            Assert.IsFalse(codec.TryDecode(new byte[] { 1, 2, 3, 4, 5 }, out var image));
            Assert.IsNull(image);
        }
    }
}
