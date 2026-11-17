using System;
using System.IO;
using NUnit.Framework;
using Uninarok.Networking.Client;

namespace Uninarok.Networking.Tests
{
    // T5.7: the capture tee copies received bytes to a file without disturbing the connection.
    public class CaptureTeeTests
    {
        private string _dir;

        [SetUp]
        public void SetUp() => _dir = Path.Combine(Path.GetTempPath(), "uninarok-tee-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void Append_WritesToTheFile_AndForwardsToTheInnerSink()
        {
            var inner = new CollectingSink();
            string path = Path.Combine(_dir, "captures", "Login-test.bin");
            using (var tee = new CaptureTee(inner, path))
            {
                Assert.IsTrue(tee.IsCapturing);
                Assert.IsTrue(tee.Append(new byte[] { 1, 2, 3 }));
                Assert.IsTrue(tee.Append(new byte[] { 4, 5 }));
            }

            Assert.AreEqual(new byte[] { 1, 2, 3, 4, 5 }, File.ReadAllBytes(path));
            Assert.AreEqual(new byte[] { 1, 2, 3, 4, 5 }, inner.Snapshot());
        }

        [Test]
        public void ReturnsWhatTheInnerSinkReturns_SoABufferOverflowStillClosesTheConnection()
        {
            var inner = new CollectingSink { Refuse = true };
            using var tee = new CaptureTee(inner, Path.Combine(_dir, "a.bin"));
            Assert.IsFalse(tee.Append(new byte[] { 1 }));
        }

        [Test]
        public void AnUnwritablePath_DoesNotDisturbTheConnection()
        {
            var inner = new CollectingSink();
            string impossible = Path.Combine(_dir, "file.txt", "x.bin");
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "file.txt"), "a file where a folder would be needed");

            using var tee = new CaptureTee(inner, impossible);
            Assert.IsFalse(tee.IsCapturing);
            Assert.IsTrue(tee.Append(new byte[] { 9 }));
            Assert.AreEqual(new byte[] { 9 }, inner.Snapshot());
        }

        [Test]
        public void AfterDispose_StillForwards_ButNoLongerWrites()
        {
            var inner = new CollectingSink();
            string path = Path.Combine(_dir, "b.bin");
            var tee = new CaptureTee(inner, path);
            tee.Append(new byte[] { 1 });
            tee.Dispose();
            tee.Dispose();
            Assert.IsTrue(tee.Append(new byte[] { 2 }));

            Assert.AreEqual(new byte[] { 1 }, File.ReadAllBytes(path));
            Assert.AreEqual(new byte[] { 1, 2 }, inner.Snapshot());
        }

        [Test]
        public void FileName_FollowsTheDocumentedPattern()
        {
            Assert.AreEqual("Map-20261117-101530.bin", CaptureTee.FileName("Map", new DateTime(2026, 11, 17, 10, 15, 30)));
        }

        [Test]
        public void ConstructorRejectsANullInnerSink()
        {
            Assert.Throws<ArgumentNullException>(() => new CaptureTee(null, Path.Combine(_dir, "c.bin")));
        }
    }
}
