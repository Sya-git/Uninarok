using System;
using System.Buffers.Binary;

namespace Uninarok.Networking.Core.Wire
{
    /// <summary>
    /// Bounds-checked little-endian writer into a caller-provided buffer (SPEC-0001 FR-6, FR-18; plan 4.1).
    /// The happy path allocates nothing; overruns throw <see cref="PacketFormatException"/>.
    /// </summary>
    public ref struct PacketWriter
    {
        private readonly Span<byte> _dst;
        private int _pos;

        public PacketWriter(Span<byte> destination)
        {
            _dst = destination;
            _pos = 0;
        }

        public int Position => _pos;

        public void WriteByte(byte v)
        {
            Require(1);
            _dst[_pos++] = v;
        }

        public void WriteUInt16(ushort v)
        {
            Require(2);
            BinaryPrimitives.WriteUInt16LittleEndian(_dst.Slice(_pos, 2), v);
            _pos += 2;
        }

        public void WriteInt16(short v)
        {
            Require(2);
            BinaryPrimitives.WriteInt16LittleEndian(_dst.Slice(_pos, 2), v);
            _pos += 2;
        }

        public void WriteUInt32(uint v)
        {
            Require(4);
            BinaryPrimitives.WriteUInt32LittleEndian(_dst.Slice(_pos, 4), v);
            _pos += 4;
        }

        public void WriteInt32(int v)
        {
            Require(4);
            BinaryPrimitives.WriteInt32LittleEndian(_dst.Slice(_pos, 4), v);
            _pos += 4;
        }

        public void WriteBytes(ReadOnlySpan<byte> v)
        {
            Require(v.Length);
            v.CopyTo(_dst.Slice(_pos, v.Length));
            _pos += v.Length;
        }

        /// <summary>Writes <paramref name="count"/> zero bytes.</summary>
        public void Skip(int count)
        {
            Require(count);
            _dst.Slice(_pos, count).Clear();
            _pos += count;
        }

        private void Require(int count)
        {
            if (count < 0 || count > _dst.Length - _pos)
                Throw(count);
        }

        private void Throw(int count)
        {
            throw new PacketFormatException(
                $"Write of {count} byte(s) at offset {_pos} overruns buffer of {_dst.Length} byte(s)");
        }
    }
}
