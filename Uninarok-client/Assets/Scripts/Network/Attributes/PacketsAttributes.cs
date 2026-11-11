using System;

namespace Uninarok.Networking.Packets.Attributes
{
    [AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    public sealed class InPacketAttribute : Attribute
    {
        public ushort Id { get; }
        public short Size { get; } // short para aceitar -1 (variável) ou o tamanho exato em bytes

        public InPacketAttribute(PacketIds id, short size)
        {
            Id = (ushort)id;
            Size = size;
        }
    }

    [AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    public sealed class OutPacketAttribute : Attribute
    {
        public ushort Id { get; }
        public short Size { get; }
        
        public OutPacketAttribute(PacketIds id, short size)
        {
            Id = (ushort)id;
            Size = size;
        }
    }
}
