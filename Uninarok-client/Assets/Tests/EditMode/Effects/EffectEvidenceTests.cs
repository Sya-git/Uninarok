using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using Uninarok.Effects.Editor;
using Uninarok.RoImport.Core.Effects;

namespace Uninarok.Effects.Tests
{
    /// <summary>SPEC-0012 FR-1, FR-11 (T8.4): file names and the writers behind the "Save image" and "Save check report" buttons.</summary>
    public sealed class EffectEvidenceTests
    {
        string folder;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "uninarok-evidence-" + Guid.NewGuid().ToString("N"), "evidence");
        }

        [TearDown]
        public void TearDown()
        {
            string root = Path.GetDirectoryName(folder);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        [Test]
        public void ImageFileName_ReplacesSlashesAndAppendsFrame()
        {
            Assert.AreEqual("texture_effect_firewall.str-f12.png", EffectEvidence.ImageFileName("texture/effect/firewall.str", 12));
        }

        [Test]
        public void ImageFileName_SingleSegmentKeyAndFrameZero()
        {
            Assert.AreEqual("firewall.str-f0.png", EffectEvidence.ImageFileName("firewall.str", 0));
        }

        [Test]
        public void ImageFileName_EmptyKeyThrows()
        {
            Assert.Throws<ArgumentException>(() => EffectEvidence.ImageFileName("", 0));
        }

        [Test]
        public void WriteImage_CreatesFolderAndWritesTheBytesUnderTheNamedFile()
        {
            byte[] png = { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 };

            string path = EffectEvidence.WriteImage(png, folder, "effect/a/b.str", 7);

            Assert.AreEqual(Path.Combine(folder, "effect_a_b.str-f7.png"), path);
            CollectionAssert.AreEqual(png, File.ReadAllBytes(path));
        }

        [Test]
        public void WriteCheckReport_RoundTripsTheReportTextExactly()
        {
            var report = new RoEffectDataReport
            {
                Str = 3,
                Textures = 5,
                SprAct = 2,
                ToolTables = 1,
                Missing = new[] { ("texture/effect/a.tga", "x.str") },
                WarpPortal = new[] { ("texture/effect/_authored/warp_portal/sparkle.quad", true) },
            };
            string text = report.ToText();

            string path = EffectEvidence.WriteCheckReport(text, folder);

            Assert.AreEqual(Path.Combine(folder, EffectEvidence.CheckReportFileName), path);
            Assert.AreEqual(text, File.ReadAllText(path, Encoding.UTF8));
        }

        [Test]
        public void WriteCheckReport_EmptyTextWritesAnEmptyFile()
        {
            string path = EffectEvidence.WriteCheckReport("", folder);

            Assert.AreEqual(0L, new FileInfo(path).Length);
        }
    }
}
