using System;

namespace Uninarok.Networking.Packets
{
    internal static class PacketSerialization
    {
        public static void RequireSize(Span<byte> buffer, int required)
        {
            if (buffer.Length < required)
                throw new ArgumentException($"Buffer size is too small. Expected at least {required} bytes.", nameof(buffer));
        }

        public static void WriteAscii(Span<byte> destination, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;
            int count = Math.Min(value.Length, destination.Length);
            for (int i = 0; i < count; i++)
                destination[i] = value[i] <= 0x7F ? (byte)value[i] : (byte)'?';
        }
    }
}
