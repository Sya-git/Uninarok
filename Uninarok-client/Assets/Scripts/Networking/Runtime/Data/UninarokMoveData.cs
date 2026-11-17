using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Data
{
    /// <summary>Walk segment (source, destination, sub-tile offset) packed in 6 bytes (Hercules WBUFPOS2/RBUFPOS2). See docs/data/UninarokMoveData.md.</summary>
    public readonly struct UninarokMoveData
    {
        public const int WireSize = 6;

        public readonly ushort SourceX, SourceY, DestX, DestY;
        public readonly byte SubX, SubY;

        public UninarokMoveData(ushort sourceX, ushort sourceY, ushort destX, ushort destY, byte subX, byte subY)
        {
            SourceX = sourceX;
            SourceY = sourceY;
            DestX = destX;
            DestY = destY;
            SubX = subX;
            SubY = subY;
        }

        /// <summary>Consumes exactly 6 bytes. Every bit pattern is valid.</summary>
        public static UninarokMoveData Read(ref PacketReader r)
        {
            byte p0 = r.ReadByte(), p1 = r.ReadByte(), p2 = r.ReadByte(), p3 = r.ReadByte(), p4 = r.ReadByte(), p5 = r.ReadByte();
            return new UninarokMoveData(
                (ushort)((p0 << 2) | (p1 >> 6)),
                (ushort)(((p1 & 0x3F) << 4) | (p2 >> 4)),
                (ushort)(((p2 & 0x0F) << 6) | (p3 >> 2)),
                (ushort)(((p3 & 0x03) << 8) | p4),
                (byte)(p5 >> 4),
                (byte)(p5 & 0x0F));
        }

        /// <summary>Writes exactly 6 bytes; coordinates are masked to 10 bits, offsets to 4 bits.</summary>
        public void Write(ref PacketWriter w)
        {
            int sx = SourceX & 0x3FF, sy = SourceY & 0x3FF, dx = DestX & 0x3FF, dy = DestY & 0x3FF;
            w.WriteByte((byte)(sx >> 2));
            w.WriteByte((byte)(((sx & 0x03) << 6) | ((sy >> 4) & 0x3F)));
            w.WriteByte((byte)(((sy & 0x0F) << 4) | ((dx >> 6) & 0x0F)));
            w.WriteByte((byte)(((dx & 0x3F) << 2) | ((dy >> 8) & 0x03)));
            w.WriteByte((byte)(dy & 0xFF));
            w.WriteByte((byte)(((SubX & 0x0F) << 4) | (SubY & 0x0F)));
        }
    }
}
