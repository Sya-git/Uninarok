using Unity.Collections;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Text;

namespace Uninarok.Networking.Flow
{
    /// <summary>
    /// A character server announced by the login server (plan 4.9). The IPv4 address is converted once to a dotted
    /// <see cref="Host"/> string: a cold, once-per-session allocation. Wire layout: 160 bytes per entry (see ACCEPT_LOGIN).
    /// </summary>
    public readonly struct ServerEndpoint
    {
        public const int WireSize = 160;
        private const int NameWireBytes = 20;
        private const int UnknownWireBytes = 128;

        public readonly string Host;
        public readonly ushort Port;
        public readonly FixedString32Bytes Name;
        public readonly ushort UserCount;
        public readonly ushort State;
        public readonly ushort Property;

        public ServerEndpoint(string host, ushort port, in FixedString32Bytes name, ushort userCount, ushort state, ushort property)
        {
            Host = host;
            Port = port;
            Name = name;
            UserCount = userCount;
            State = state;
            Property = property;
        }

        /// <summary>Consumes exactly <see cref="WireSize"/> bytes. The port is read as <c>ushort</c> (same bits as the wire's int16).</summary>
        public static ServerEndpoint Read(ref PacketReader r)
        {
            byte a = r.ReadByte(), b = r.ReadByte(), c = r.ReadByte(), d = r.ReadByte();   // network byte order: octets as they are
            string host = a + "." + b + "." + c + "." + d;
            ushort port = r.ReadUInt16();
            var name = new FixedString32Bytes();
            WireText.Read(ref r, NameWireBytes, ref name);
            ushort userCount = r.ReadUInt16();
            ushort state = r.ReadUInt16();
            ushort property = r.ReadUInt16();
            r.Skip(UnknownWireBytes);                                                     // opaque region, fixed 128 bytes in Hercules
            return new ServerEndpoint(host, port, in name, userCount, state, property);
        }
    }
}
