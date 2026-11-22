using System;
using System.Text;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>
    /// Map server to client, 0x008E (ZC_NOTIFY_PLAYERCHAT), variable. A line for the own chat window: the server's answer to a
    /// command, a notice, or the own chat line sent back. Cold packet: it allocates its text.
    /// </summary>
    [InPacket(PacketIds.OWN_MESSAGE, Size)]
    public readonly struct OWN_MESSAGE
    {
        public const int Size = -1;

        public readonly string Text;

        public OWN_MESSAGE(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            var bytes = r.ReadSpan(r.Remaining);
            int end = bytes.IndexOf((byte)0);
            Text = Encoding.ASCII.GetString(end < 0 ? bytes : bytes.Slice(0, end));
            r.EnsureConsumed();
        }
    }
}
