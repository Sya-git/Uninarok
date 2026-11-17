using System;

namespace Uninarok.Networking.Core.Protocol
{
    /// <summary>
    /// Marks an incoming packet struct (SPEC-0001 FR-4, FR-18; plan 4.6). The source generator turns every
    /// <c>[InPacket]</c> struct into a case of the generated router. <see cref="Size"/> follows the length contract:
    /// -1 variable, otherwise the fixed total length including the 2-byte ID. A test checks it against the Hercules table.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class InPacketAttribute : Attribute
    {
        public InPacketAttribute(ushort id, int size)
        {
            Id = id;
            Size = size;
        }

        public ushort Id { get; }
        public int Size { get; }
    }
}
