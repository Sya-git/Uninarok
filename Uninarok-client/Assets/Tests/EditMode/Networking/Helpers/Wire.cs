using System;
using System.Collections.Generic;

namespace Uninarok.Networking.Tests
{
    /// <summary>Builds raw packet bytes for framer tests.</summary>
    public static class Wire
    {
        /// <summary>Fixed packet: ID (LE) then payload. Total length = 2 + payload.Length.</summary>
        public static byte[] Fixed(ushort id, params byte[] payload)
        {
            var b = new byte[2 + payload.Length];
            b[0] = (byte)id; b[1] = (byte)(id >> 8);
            Array.Copy(payload, 0, b, 2, payload.Length);
            return b;
        }

        /// <summary>Variable packet: ID, u16 total length (including the 4-byte header), payload.</summary>
        public static byte[] Var(ushort id, params byte[] payload)
        {
            int total = 4 + payload.Length;
            var b = new byte[total];
            b[0] = (byte)id; b[1] = (byte)(id >> 8);
            b[2] = (byte)total; b[3] = (byte)(total >> 8);
            Array.Copy(payload, 0, b, 4, payload.Length);
            return b;
        }

        public static byte[] Concat(params byte[][] parts)
        {
            var all = new List<byte>();
            foreach (var p in parts) all.AddRange(p);
            return all.ToArray();
        }

        public static byte[] Repeat(byte[] packet, int count)
        {
            var all = new byte[packet.Length * count];
            for (int i = 0; i < count; i++) Array.Copy(packet, 0, all, i * packet.Length, packet.Length);
            return all;
        }
    }
}
