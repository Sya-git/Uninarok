using System;
using System.Buffers.Binary;

namespace Uninarok.Networking.Core.Wire
{
    /// <summary>
    /// Bounds-checked little-endian reader over a packet payload (SPEC-0001 FR-6, FR-18; plan 4.1).
    /// The happy path allocates nothing; overruns throw <see cref="PacketFormatException"/>.
    /// </summary>
    public ref struct PacketReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _pos;

        public PacketReader(ReadOnlySpan<byte> payload)
        {
            _data = payload;
            _pos = 0;
        }

        public int Remaining => _data.Length - _pos;

        public byte ReadByte()
        {
            Require(1);
            return _data[_pos++];
        }

        public ushort ReadUInt16()
        {
            Require(2);
            ushort v = BinaryPrimitives.ReadUInt16LittleEndian(_data.Slice(_pos, 2));
            _pos += 2;
            return v;
        }

        public short ReadInt16()
        {
            Require(2);
            short v = BinaryPrimitives.ReadInt16LittleEndian(_data.Slice(_pos, 2));
            _pos += 2;
            return v;
        }

        public uint ReadUInt32()
        {
            Require(4);
            uint v = BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(_pos, 4));
            _pos += 4;
            return v;
        }

        public int ReadInt32()
        {
            Require(4);
            int v = BinaryPrimitives.ReadInt32LittleEndian(_data.Slice(_pos, 4));
            _pos += 4;
            return v;
        }

        public void ReadBytes(Span<byte> destination)
        {
            Require(destination.Length);
            _data.Slice(_pos, destination.Length).CopyTo(destination);
            _pos += destination.Length;
        }

        /// <summary>A view over the next <paramref name="count"/> bytes, valid only while the payload is.</summary>
        public ReadOnlySpan<byte> ReadSpan(int count)
        {
            Require(count);
            ReadOnlySpan<byte> s = _data.Slice(_pos, count);
            _pos += count;
            return s;
        }

        public void Skip(int count)
        {
            Require(count);
            _pos += count;
        }

        /// <summary>
        /// Throws <see cref="PacketFormatException"/> unless the whole payload was read. Incoming packet constructors call
        /// this last, so a payload with unread bytes is reported instead of silently misparsed (docs: Packet-Implementation).
        /// </summary>
        public void EnsureConsumed()
        {
            if (_pos != _data.Length)
                throw new PacketFormatException($"{_data.Length - _pos} unread byte(s) left in payload of {_data.Length} byte(s)");
        }

        private void Require(int count)
        {
            if (count < 0 || count > _data.Length - _pos)
                Throw(count);
        }

        private void Throw(int count)
        {
            throw new PacketFormatException(
                $"Read of {count} byte(s) at offset {_pos} overruns payload of {_data.Length} byte(s)");
        }
    }
}
