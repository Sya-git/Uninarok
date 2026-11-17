using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-1, FR-17 (plan 6, CaptureReplayTests): every recorded session replays identically however it is chunked.
    // Real captures (T9.1) are *.bin files under Captures/ with a reviewed *.expected.txt each; this suite picks them up.
    public class CaptureReplayTests
    {
        static readonly int[] ChunkSizes = { 1, 2, 3, 7, 64, 0 };           // 0 = the whole buffer

        static string CapturesDir => Path.Combine(Application.dataPath, "Tests/EditMode/Networking/Captures");

        const string NoCaptures = "(no captures recorded yet)";

        public static IEnumerable<string> Captures()
        {
            var files = Directory.Exists(CapturesDir)
                ? Directory.GetFiles(CapturesDir, "*.bin").OrderBy(p => p).Select(Path.GetFileName).ToList()
                : new List<string>();
            if (files.Count == 0) files.Add(NoCaptures);            // keeps the test visible: ignored until T9.1 records real sessions
            return files;
        }

        [TestCaseSource(nameof(Captures))]
        public void EveryCapture_ReplaysIdentically_ForEveryChunkSize(string captureFile)
        {
            if (captureFile == NoCaptures) Assert.Ignore("No real captures yet: T9.1 records them against a local Hercules.");
            string binPath = Path.Combine(CapturesDir, captureFile);
            string expectedPath = Path.ChangeExtension(binPath, ".expected.txt");
            Assert.IsTrue(File.Exists(expectedPath), captureFile + " has no reviewed .expected.txt (run Uninarok/Networking/Write Capture Expectations)");

            var data = File.ReadAllBytes(binPath);
            var (skip, expected) = CaptureReplay.ParseExpected(File.ReadAllText(expectedPath));
            Assert.Greater(expected.Count, 0, "an expectation file without packets proves nothing");

            foreach (int chunk in ChunkSizes)
            {
                var run = CaptureReplay.Replay(data, skip, chunk);
                string label = captureFile + " chunk=" + (chunk == 0 ? "whole" : chunk.ToString());
                Assert.IsFalse(run.Faulted, label + " faulted: " + run.FaultReason);
                Assert.AreEqual(0, run.PendingBytes, label + ": every byte after the skipped prefix is consumed (file length minus skip)");
                CollectionAssert.AreEqual(expected, run.Lines, label);
            }
        }

        // ---- the harness itself, on a generated stream ----

        static byte[] SyntheticSession() => Wire.Concat(
            new byte[] { 0x81, 0x84, 0x1E, 0x00 },                                        // the 4-byte echo of the character server
            Wire.Var(0x006B, new byte[23]),                                                // CHARACTER_DATA without records
            Wire.Fixed(0x006C, 0),                                                         // 3 bytes
            Wire.Fixed(0x0066, 1),                                                         // known but unhandled by the client
            Wire.Var(0x0AC4, new byte[60]));                                               // ACCEPT_LOGIN without servers

        [Test]
        public void Harness_ProducesTheSameLines_ForEveryChunkSize()
        {
            var data = SyntheticSession();
            var whole = CaptureReplay.Replay(data, 4, 0);
            Assert.IsFalse(whole.Faulted, whole.FaultReason);
            Assert.AreEqual(4, whole.Lines.Count);
            Assert.AreEqual(0, whole.PendingBytes);

            foreach (int chunk in ChunkSizes)
            {
                var run = CaptureReplay.Replay(data, 4, chunk);
                CollectionAssert.AreEqual(whole.Lines, run.Lines, "chunk " + chunk);
                Assert.IsFalse(run.Faulted);
            }
        }

        [Test]
        public void Harness_WithoutTheSkip_FaultsOnTheEcho_SoTheSkipHeaderMatters()
        {
            var run = CaptureReplay.Replay(SyntheticSession(), 0, 0);
            Assert.IsTrue(run.Faulted);
        }

        [Test]
        public void Harness_LinesDescribeIdLengthAndCrc()
        {
            var run = CaptureReplay.Replay(Wire.Fixed(0x006C, 7), 0, 0);
            Assert.AreEqual(1, run.Lines.Count);
            var parts = run.Lines[0].Split(',');
            Assert.AreEqual("006C", parts[0]);
            Assert.AreEqual("1", parts[1]);
            Assert.AreEqual(CaptureReplay.Crc32(new byte[] { 7 }).ToString("X8"), parts[2]);
        }

        [Test]
        public void Crc32_MatchesTheStandardCheckValue()
        {
            Assert.AreEqual(0xCBF43926u, CaptureReplay.Crc32(System.Text.Encoding.ASCII.GetBytes("123456789")));
            Assert.AreEqual(0u, CaptureReplay.Crc32(ReadOnlySpan<byte>.Empty));
        }

        [Test]
        public void ExpectedText_RoundTrips()
        {
            var lines = new List<string> { "006C,1,AABBCCDD", "0AC4,60,00000001" };
            var (skip, parsed) = CaptureReplay.ParseExpected(CaptureReplay.ExpectedText(4, lines));
            Assert.AreEqual(4, skip);
            CollectionAssert.AreEqual(lines, parsed);
        }

        [Test]
        public void ParseExpected_RejectsAFileWithoutTheSkipHeader()
        {
            Assert.Throws<InvalidDataException>(() => CaptureReplay.ParseExpected("006C,1,AABBCCDD\n"));
        }

        [TestCase("Character-20261117-101530.bin", 4)]
        [TestCase("Login-20261117-101530.bin", 0)]
        [TestCase("Map-20261117-101530.bin", 0)]
        public void DefaultSkip_IsFourOnlyForTheCharacterServer(string file, int expected)
        {
            Assert.AreEqual(expected, CaptureReplay.DefaultSkipFor(file));
        }
    }
}
