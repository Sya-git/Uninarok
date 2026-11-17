using System;
using Unity.Collections;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Data;
using Uninarok.Networking.Text;

namespace Uninarok.Networking.Packets.Out.Character
{
    /// <summary>Client to character server, 0x0A39, 36 bytes. Requests creation of a character.</summary>
    [OutPacket(PacketIds.CREATE_CHARACTER, Size)]
    public readonly struct CREATE_CHARACTER : IOutgoingPacket
    {
        public const int Size = 36;
        private const int NameWireBytes = 24;

        /// <summary>Character name. Written as ASCII: other characters become '?'.</summary>
        public readonly FixedString32Bytes Name;
        public readonly byte Slot;
        public readonly ushort HairColor;
        public readonly ushort HairStyle;

        /// <summary>Starting job class ID. Four bytes on the wire (Hercules reads it with RFIFOL).</summary>
        public readonly uint JobClass;
        public readonly AccountSex Sex;

        public CREATE_CHARACTER(in FixedString32Bytes name, byte slot, ushort hairColor, ushort hairStyle, uint jobClass, AccountSex sex)
        {
            Name = name;
            Slot = slot;
            HairColor = hairColor;
            HairStyle = hairStyle;
            JobClass = jobClass;
            Sex = sex;
        }

        public int WireSize => Size;

        public void Serialize(Span<byte> destination)
        {
            var w = new PacketWriter(destination);
            w.WriteUInt16(PacketIds.CREATE_CHARACTER);
            WireText.WriteAscii(ref w, NameWireBytes, in Name);
            w.WriteByte(Slot);
            w.WriteUInt16(HairColor);
            w.WriteUInt16(HairStyle);
            w.WriteUInt32(JobClass);
            w.WriteByte((byte)Sex);
        }
    }
}
