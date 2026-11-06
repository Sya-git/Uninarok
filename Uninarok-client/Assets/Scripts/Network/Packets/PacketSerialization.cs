using System;
using System.Buffers.Binary;

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

    internal ref struct PacketReader
    {
        private readonly ReadOnlySpan<byte> _buffer;

        public int Offset { get; private set; }

        public PacketReader(ReadOnlySpan<byte> buffer)
        {
            _buffer = buffer;
            Offset = 0;
        }

        public byte ReadByte()
        {
            byte value = _buffer[Offset];
            Offset++;
            return value;
        }

        public ushort ReadUInt16()
        {
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.Slice(Offset, sizeof(ushort)));
            Offset += sizeof(ushort);
            return value;
        }

        public int ReadInt32()
        {
            int value = BinaryPrimitives.ReadInt32LittleEndian(_buffer.Slice(Offset, sizeof(int)));
            Offset += sizeof(int);
            return value;
        }

        public uint ReadUInt32()
        {
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(_buffer.Slice(Offset, sizeof(uint)));
            Offset += sizeof(uint);
            return value;
        }

        public ReadOnlySpan<byte> ReadBytes(int count)
        {
            ReadOnlySpan<byte> value = _buffer.Slice(Offset, count);
            Offset += count;
            return value;
        }

        public void Skip(int count)
        {
            _ = ReadBytes(count);
        }
    }

    internal ref struct PacketWriter
    {
        private readonly Span<byte> _buffer;

        public int Offset { get; private set; }

        public PacketWriter(Span<byte> buffer)
        {
            _buffer = buffer;
            Offset = 0;
        }

        public void WriteByte(byte value)
        {
            _buffer[Offset] = value;
            Offset++;
        }

        public void WriteUInt16(ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(_buffer.Slice(Offset, sizeof(ushort)), value);
            Offset += sizeof(ushort);
        }

        public void WriteInt32(int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(_buffer.Slice(Offset, sizeof(int)), value);
            Offset += sizeof(int);
        }

        public void WriteUInt32(uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(_buffer.Slice(Offset, sizeof(uint)), value);
            Offset += sizeof(uint);
        }

        public void WriteBytes(ReadOnlySpan<byte> value)
        {
            value.CopyTo(_buffer.Slice(Offset, value.Length));
            Offset += value.Length;
        }

        public void Skip(int count)
        {
            _ = _buffer.Slice(Offset, count);
            Offset += count;
        }

        public void WriteAscii(string value, int width)
        {
            int count = Math.Min(value == null ? 0 : value.Length, width);
            for (int i = 0; i < count; i++)
                WriteByte(value[i] <= 0x7F ? (byte)value[i] : (byte)'?');
            for (int i = count; i < width; i++)
                WriteByte(0);
        }
    }
}
