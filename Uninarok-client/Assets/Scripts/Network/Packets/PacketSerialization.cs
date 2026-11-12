using System;
using System.Buffers.Binary;
using Unity.Collections;

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

    public ref struct PacketReader
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

        public void Read(out byte value)
        {
            value = ReadByte();
        }

        public ushort ReadUshort()
        {
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.Slice(Offset, sizeof(ushort)));
            Offset += sizeof(ushort);
            return value;
        }

        public void Read(out ushort value)
        {
            value = ReadUshort();
        }

        public short ReadShort()
        {
            short value = BinaryPrimitives.ReadInt16LittleEndian(_buffer.Slice(Offset, sizeof(short)));
            Offset += sizeof(short);
            return value;
        }

        public void Read(out short value)
        {
            value = ReadShort();
        }

        public int ReadInt()
        {
            int value = BinaryPrimitives.ReadInt32LittleEndian(_buffer.Slice(Offset, sizeof(int)));
            Offset += sizeof(int);
            return value;
        }

        public void Read(out int value)
        {
            value = ReadInt();
        }

        public uint ReadUint()
        {
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(_buffer.Slice(Offset, sizeof(uint)));
            Offset += sizeof(uint);
            return value;
        }

        public void Read(out uint value)
        {
            value = ReadUint();
        }

        public FixedString32Bytes ReadChars(int amount)
        {
            var bytes = _buffer.Slice(Offset, amount);
            var text = new FixedString32Bytes();

            for(var i=0;i<bytes.Length;++i)
            {
                if (bytes[i] == 0) continue;

                text.Append((char)bytes[i]);
            }
            // Copia o bloco de memória inteiro de uma vez
            //unsafe
            //{
            //    fixed (byte* ptr = bytes)
            //    {
            //        if (ptr != 0)
            //        { 
            //            text.Append(ptr, bytes.Length); 
            //        }
            //    }
            //}
            Offset += amount;
            return text;
        }

        public void Read(out FixedString32Bytes value, int amount)
        {
            value = ReadChars(amount);
        }

        public ReadOnlySpan<byte> ReadBytes(int count)
        {
            ReadOnlySpan<byte> value = _buffer.Slice(Offset, count);
            Offset += count;
            return value;
        }

        public void ReadBytes(out ReadOnlySpan<byte> value, int amount)
        {
            value = ReadBytes(amount);
        }

        public void Skip(int count)
        {
            Offset += count;
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

        public void Write(byte value)
        {
            _buffer[Offset] = value;
            Offset++;
        }

        public void Write(ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(_buffer.Slice(Offset, sizeof(ushort)), value);
            Offset += sizeof(ushort);
        }

        public void Write(int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(_buffer.Slice(Offset, sizeof(int)), value);
            Offset += sizeof(int);
        }

        public void Write(uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(_buffer.Slice(Offset, sizeof(uint)), value);
            Offset += sizeof(uint);
        }

        public void Write(long value)
        {
            BinaryPrimitives.WriteInt64LittleEndian(_buffer.Slice(Offset, sizeof(long)), value);
            Offset += sizeof(long);
        }

        public void Write(ulong value)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(_buffer.Slice(Offset, sizeof(long)), value);
            Offset += sizeof(ulong);
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
                Write(value[i] <= 0x7F ? (byte)value[i] : (byte)'?');
            for (int i = count; i < width; i++)
                Write((byte)'\0');
        }
    }
}
