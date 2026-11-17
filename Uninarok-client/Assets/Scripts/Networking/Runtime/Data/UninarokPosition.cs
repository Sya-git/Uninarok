using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Data
{
    /// <summary>Tile position plus facing, packed in 3 bytes (Hercules WBUFPOS/RBUFPOS). See docs/data/UninarokPosition.md.</summary>
    public readonly struct UninarokPosition
    {
        public const int WireSize = 3;

        public readonly ushort X;
        public readonly ushort Y;
        public readonly UninarokDirection Direction;

        public UninarokPosition(ushort x, ushort y, UninarokDirection direction)
        {
            X = x;
            Y = y;
            Direction = direction;
        }

        /// <summary>Consumes exactly 3 bytes. Every bit pattern is valid.</summary>
        public static UninarokPosition Read(ref PacketReader r)
        {
            byte p0 = r.ReadByte(), p1 = r.ReadByte(), p2 = r.ReadByte();
            ushort x = (ushort)((p0 << 2) | (p1 >> 6));
            ushort y = (ushort)(((p1 & 0x3F) << 4) | (p2 >> 4));
            return new UninarokPosition(x, y, (UninarokDirection)(p2 & 0x0F));
        }

        /// <summary>Writes exactly 3 bytes; X and Y are masked to 10 bits, direction to 4 bits.</summary>
        public void Write(ref PacketWriter w)
        {
            int x = X & 0x3FF, y = Y & 0x3FF, dir = (byte)Direction & 0x0F;
            w.WriteByte((byte)(x >> 2));
            w.WriteByte((byte)(((x << 6) | ((y >> 4) & 0x3F)) & 0xFF));
            w.WriteByte((byte)(((y << 4) | dir) & 0xFF));
        }
    }
}
