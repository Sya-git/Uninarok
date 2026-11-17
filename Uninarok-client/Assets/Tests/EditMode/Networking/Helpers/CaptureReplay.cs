using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Uninarok.Networking.Core.Framing;
using Uninarok.Networking.Core.Protocol;

namespace Uninarok.Networking.Tests
{
    /// <summary>
    /// Replays a recorded byte stream through the real framer and the real Hercules length table, and describes what came out
    /// as one line per packet: <c>id,payloadLen,crc32</c> (SPEC-0001 FR-1, T9.2/T9.3). The expected file of a capture is
    /// <c>skip=N</c> followed by these lines, produced from the whole-buffer run and reviewed by hand.
    /// </summary>
    public static class CaptureReplay
    {
        public sealed class Result
        {
            public readonly List<string> Lines = new List<string>();
            public bool Faulted;
            public string FaultReason;
            public int PendingBytes;
        }

        private sealed class DescribingRouter : IPacketRouter
        {
            public readonly List<string> Lines;
            public DescribingRouter(List<string> lines) { Lines = lines; }

            public bool Route(ushort packetId, ReadOnlySpan<byte> payload)
            {
                Lines.Add(packetId.ToString("X4") + "," + payload.Length + "," + Crc32(payload).ToString("X8"));
                return true;
            }
        }

        /// <summary>Feeds <paramref name="data"/> in chunks of <paramref name="chunkSize"/> bytes (0 = the whole buffer), skipping the first <paramref name="skip"/> raw bytes.</summary>
        public static Result Replay(byte[] data, int skip, int chunkSize)
        {
            var result = new Result();
            var framer = new PacketFramer(HerculesPacketTable.Instance, new DescribingRouter(result.Lines));
            framer.SkipNextBytes(skip);

            int chunk = chunkSize <= 0 ? Math.Max(1, data.Length) : chunkSize;
            for (int i = 0; i < data.Length; i += chunk)
            {
                int n = Math.Min(chunk, data.Length - i);
                framer.Append(new ReadOnlySpan<byte>(data, i, n));
                framer.Process(int.MaxValue);
            }
            framer.Process(int.MaxValue);

            result.Faulted = framer.IsFaulted;
            result.FaultReason = framer.FaultReason;
            result.PendingBytes = framer.PendingBytes;
            return result;
        }

        /// <summary>The expected-file text for a replay: <c>skip=N</c>, then one line per packet.</summary>
        public static string ExpectedText(int skip, IEnumerable<string> lines)
        {
            var sb = new StringBuilder();
            sb.Append("skip=").Append(skip).Append('\n');
            foreach (var l in lines) sb.Append(l).Append('\n');
            return sb.ToString();
        }

        /// <summary>Parses an expected file. Returns the skip count and the packet lines.</summary>
        public static (int Skip, List<string> Lines) ParseExpected(string text)
        {
            var lines = new List<string>(text.Replace("\r", "").Split('\n'));
            while (lines.Count > 0 && lines[lines.Count - 1].Length == 0) lines.RemoveAt(lines.Count - 1);
            if (lines.Count == 0 || !lines[0].StartsWith("skip=")) throw new InvalidDataException("expected file must start with 'skip=N'");
            int skip = int.Parse(lines[0].Substring(5));
            lines.RemoveAt(0);
            return (skip, lines);
        }

        /// <summary>Raw bytes to skip at the start of a capture: the character server echoes the 4-byte account ID first (FR-3).</summary>
        public static int DefaultSkipFor(string captureFileName)
            => Path.GetFileName(captureFileName).StartsWith("Character", StringComparison.OrdinalIgnoreCase) ? 4 : 0;

        private static readonly uint[] CrcTable = BuildCrcTable();

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[i] = c;
            }
            return table;
        }

        public static uint Crc32(ReadOnlySpan<byte> data)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < data.Length; i++) crc = CrcTable[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            return ~crc;
        }

#if UNITY_EDITOR
        public const string CapturesFolder = "Assets/Tests/EditMode/Networking/Captures";

        /// <summary>
        /// T9.2: writes <c>*.expected.txt</c> next to every capture that has none, from the whole-buffer run.
        /// Review the files by hand before committing them.
        /// </summary>
        [UnityEditor.MenuItem("Uninarok/Networking/Write Capture Expectations")]
        public static void WriteMissingExpectations()
        {
            string dir = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", CapturesFolder));
            if (!Directory.Exists(dir)) { UnityEngine.Debug.Log("No captures folder: " + dir); return; }

            int written = 0;
            foreach (string bin in Directory.GetFiles(dir, "*.bin"))
            {
                string expected = Path.ChangeExtension(bin, ".expected.txt");
                if (File.Exists(expected)) continue;

                int skip = DefaultSkipFor(bin);
                var run = Replay(File.ReadAllBytes(bin), skip, 0);
                if (run.Faulted) { UnityEngine.Debug.LogError(Path.GetFileName(bin) + " faults the framer: " + run.FaultReason); continue; }
                File.WriteAllText(expected, ExpectedText(skip, run.Lines));
                written++;
            }
            UnityEditor.AssetDatabase.Refresh();
            UnityEngine.Debug.Log("Wrote " + written + " expectation file(s) in " + dir + ". Review them by hand before committing.");
        }
#endif
    }
}
