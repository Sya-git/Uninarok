using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.Out.Map
{
    /// <summary>
    /// Client to map server, 0x00F3 (CZ_REQUEST_CHAT), variable. A public chat line, or a command of the server: the text is
    /// <c>name : text</c> with the own name. Cold packet: it holds its bytes.
    /// </summary>
    [OutPacket(PacketIds.GLOBAL_CHAT, Size)]
    public readonly struct GLOBAL_CHAT : IOutgoingPacket
    {
        public const int Size = -1;
        /// <summary>The longest text Hercules takes (CHAT_SIZE_MAX, 255 with the NUL).</summary>
        public const int MaxText = 254;

        readonly string _text;

        /// <param name="text">Written as ASCII: other characters become '?'; cut at <see cref="MaxText"/>.</param>
        public GLOBAL_CHAT(string name, string text)
        {
            text = name + " : " + text;
            _text = text.Length > MaxText ? text.Substring(0, MaxText) : text;
        }

        public string Text => _text;

        public int WireSize => 4 + _text.Length + 1;

        public void Serialize(Span<byte> destination)
        {
            var w = new PacketWriter(destination);
            w.WriteUInt16(PacketIds.GLOBAL_CHAT);
            w.WriteUInt16((ushort)WireSize);
            foreach (char c in _text) w.WriteByte(c >= 0x20 && c < 0x7F ? (byte)c : (byte)'?');
            w.WriteByte(0);
        }
    }
}
