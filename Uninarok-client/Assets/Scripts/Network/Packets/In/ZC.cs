using System;
using Uninarok.Networking.Packets.Attributes;

namespace Uninarok.Networking.Packets
{
    public static class ZC
    {
        [InPacket(PacketIds.ACCEPT_ENTER, Size)]
        public struct ACCEPT_ENTER
        {
            // Another int which i don't know what does, maybe should ignore the second int
            public const short Size = 6;

            public int Id;
            //public uint StartTimestamp;
            //public byte[] Transform; // compressed x, y, direction
            //public byte XSize; // visibility?
            //public byte YSize; // visibility?


            public ACCEPT_ENTER(ReadOnlySpan<byte> data)
            {
                var reader = new PacketReader(data);

                reader.Read(out Id);
            }
        }

        [InPacket(PacketIds.INVENTORY_EXPANSION_INFO, Size)]
        public struct INVENTORY_EXPANSION_INFO
        {
            public const short Size = 4;

            public short ExpansionSize;
            public INVENTORY_EXPANSION_INFO(ReadOnlySpan<byte> data)
            {
                var reader = new PacketReader(data);

                reader.Read(out ExpansionSize);
            }
        }

        [InPacket(PacketIds.OVERWEIGHT_PERCENT, Size)]
        public struct OVERWEIGHT_PERCENT
        {
            public const short Size = 6;

            public uint Percentage;

            public OVERWEIGHT_PERCENT(ReadOnlySpan<byte> data)
            {
                var writer = new PacketReader(data);

                writer.Read(out Percentage);
            }
        }

        [InPacket(PacketIds.AUTH_OK, Size)]
        public struct AUTH_OK
        {
            public const short Size = 13;

            public uint StartTimestamp;
            public short PosX, PosY, Direction;
            public byte XSize, YSize; // not used
            public short Font;

            public AUTH_OK(ReadOnlySpan<byte> data)
            {
                var reader = new PacketReader(data);

                reader.Read(out StartTimestamp);
                var compactedPos = reader.ReadBytes(3).ToArray();
                reader.Read(out XSize);
                reader.Read(out YSize);
                reader.Read(out Font);

                var x = ((compactedPos[0] & 0xFF) << 2) | (compactedPos[1] >> 6);
                var y = ((compactedPos[1] & 0x3F) << 4) | (compactedPos[2] >> 4);
                var dir = (compactedPos[2] & 0x0F);

                PosX = (short)x;
                PosY = (short)y;
                Direction = (short)dir;
            }
        }

        [InPacket(PacketIds.REFUSE_ENTER, Size)]
        public struct REFUSE_ENTER
        {
            public const ushort PacketId = 0x0074;
            public const short Size = 3;

            public REFUSE_ENTER(ReadOnlySpan<byte> data)
            {

            }
        }

        [InPacket(PacketIds.ZC_ACK_PING, Size)]
        public struct ZC_ACK_PING
        {
            public const short Size = 6;

            public uint Tick;

            public ZC_ACK_PING(ReadOnlySpan<byte> data)
            {
                var reader = new PacketReader(data);

                reader.Read(out Tick);
            }
        }
    }
}
