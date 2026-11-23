using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>
    /// Map server to client, 0x0B09 (<c>packet_itemlist_normal</c>), variable. The stackable items of an inventory, storage or
    /// cart: 34-byte records. Cold packet: it allocates the record array while decoding.
    /// </summary>
    [InPacket(PacketIds.INVENTORY_LIST_NORMAL, Size)]
    public readonly struct INVENTORY_LIST_NORMAL
    {
        public const int Size = -1;
        public const int RecordSize = 34;
        /// <summary><see cref="ListType"/> of the own inventory.</summary>
        public const byte Inventory = 0;

        public readonly struct Item
        {
            /// <summary>The slot plus 2.</summary>
            public readonly ushort Index;
            public readonly uint ItemId;
            public readonly byte ItemType;
            public readonly ushort Amount;

            internal Item(ref PacketReader r)
            {
                Index = r.ReadUInt16();
                ItemId = r.ReadUInt32();
                ItemType = r.ReadByte();
                Amount = r.ReadUInt16();
                r.Skip(4 + 16 + 4 + 1); // location, cards, expiry, flags
            }
        }

        public readonly byte ListType;
        public readonly Item[] Items;

        public INVENTORY_LIST_NORMAL(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            ListType = r.ReadByte();
            if (r.Remaining % RecordSize != 0)
                throw new PacketFormatException($"INVENTORY_LIST_NORMAL record list of {r.Remaining} byte(s) is not a multiple of {RecordSize}");
            Items = new Item[r.Remaining / RecordSize];
            for (int i = 0; i < Items.Length; i++) Items[i] = new Item(ref r);
            r.EnsureConsumed();
        }
    }
}
