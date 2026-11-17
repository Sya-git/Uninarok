#if PENDING_T2_8 // T2.2: enabled (guard removed) by T2.8 when PacketFramer.Process exists.
using System;
using System.Collections.Generic;
using Uninarok.Networking.Core.Framing;

namespace Uninarok.Networking.Tests
{
    /// <summary>Feeds a byte stream to a real framer in chunks and records what the router saw.</summary>
    public static class FramerHarness
    {
        public static List<(ushort Id, byte[] Payload)> Run(IPacketLengthTable table, byte[] stream, int chunkSize, int maxPerProcess = int.MaxValue)
        {
            var router = new RecordingRouter();
            var framer = new PacketFramer(table, router);
            int chunk = chunkSize <= 0 ? stream.Length : chunkSize;
            for (int i = 0; i < stream.Length; i += chunk)
            {
                int n = Math.Min(chunk, stream.Length - i);
                Assert_True(framer.Append(new ReadOnlySpan<byte>(stream, i, n)));
                framer.Process(maxPerProcess);
            }
            framer.Process(maxPerProcess);
            return router.Packets;
        }

        private static void Assert_True(bool ok)
        {
            if (!ok) throw new InvalidOperationException("Append rejected data in the harness");
        }
    }
}
#endif
