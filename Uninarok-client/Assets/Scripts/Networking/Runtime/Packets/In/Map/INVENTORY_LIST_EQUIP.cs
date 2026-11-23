using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>
    /// Map server to client, 0x0B39 (<c>packet_itemlist_equip</c>), variable. The items of an inventory, storage or cart that do
    /// not stack: 68-byte records. Cold packet: it allocates the record array while decoding.
    /// </summary>
    [InPacket(PacketIds.INVENTORY_LIST_EQUIP, Size)]
    public readonly struct INVENTORY_LIST_EQUIP
    {
        public const int Size = -1;
        public const int RecordSize = 68;

        public readonly struct Item
        {
            /// <summary>The slot plus 2.</summary>
            public readonly ushort Index;
            public readonly uint ItemId;
            public readonly byte ItemType;
            /// <summary>Where it can be worn, and is (0: not worn).</summary>
            public readonly uint Location, WornAt;

            internal Item(ref PacketReader r)
            {
                Index = r.ReadUInt16();
                ItemId = r.ReadUInt32();
                ItemType = r.ReadByte();
                Location = r.ReadUInt32();
                WornAt = r.ReadUInt32();
                r.Skip(16 + 4 + 2 + 2 + 1 + 25 + 1 + 1 + 1); // cards, expiry, bind, look, options, refine, grade, flags
            }
        }

        /// <summary>As in <see cref="INVENTORY_LIST_NORMAL"/>.</summary>
        public readonly byte ListType;
        public readonly Item[] Items;

        public INVENTORY_LIST_EQUIP(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            ListType = r.ReadByte();
            if (r.Remaining % RecordSize != 0)
                throw new PacketFormatException($"INVENTORY_LIST_EQUIP record list of {r.Remaining} byte(s) is not a multiple of {RecordSize}");
            Items = new Item[r.Remaining / RecordSize];
            for (int i = 0; i < Items.Length; i++) Items[i] = new Item(ref r);
            r.EnsureConsumed();
        }
    }
}
