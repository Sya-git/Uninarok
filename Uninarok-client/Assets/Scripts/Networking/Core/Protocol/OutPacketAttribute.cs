using System;

namespace Uninarok.Networking.Core.Protocol
{
    /// <summary>
    /// Marks an outgoing packet struct (SPEC-0001 FR-18; plan 4.6). The struct must also implement
    /// <c>IOutgoingPacket</c>. <see cref="Size"/> follows the length contract: -1 variable, otherwise the fixed total length.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class OutPacketAttribute : Attribute
    {
        public OutPacketAttribute(ushort id, int size)
        {
            Id = id;
            Size = size;
        }

        public ushort Id { get; }
        public int Size { get; }
    }
}
