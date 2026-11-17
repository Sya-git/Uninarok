using System;

namespace Uninarok.Networking.Core.Wire
{
    /// <summary>Thrown when a packet payload cannot be read or written as declared (overrun, bad count).</summary>
    public sealed class PacketFormatException : Exception
    {
        public PacketFormatException(string message) : base(message) { }
    }
}
