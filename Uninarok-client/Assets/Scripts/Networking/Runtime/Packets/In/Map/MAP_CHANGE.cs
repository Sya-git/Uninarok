using System;
using Unity.Collections;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Text;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>Map server to client, 0x0091 (ZC_NPCACK_MAPMOVE), 22 bytes. The player moves to a map (or cell) of the same map server. Cold.</summary>
    [InPacket(PacketIds.MAP_CHANGE, Size)]
    public readonly struct MAP_CHANGE
    {
        public const int Size = 22;

        /// <summary>With the <c>.gat</c> suffix.</summary>
        public readonly FixedString32Bytes MapName;
        public readonly ushort X;
        public readonly ushort Y;

        public MAP_CHANGE(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            MapName = default;
            WireText.Read(ref r, 16, ref MapName);
            X = r.ReadUInt16();
            Y = r.ReadUInt16();
            r.EnsureConsumed();
        }
    }
}
