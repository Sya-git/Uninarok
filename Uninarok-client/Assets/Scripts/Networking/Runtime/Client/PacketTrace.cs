using System;
using System.Collections.Generic;
using System.Reflection;
using Uninarok.Networking.Core.Framing;
using Uninarok.Networking.Packets;

namespace Uninarok.Networking.Client
{
    /// <summary>
    /// Per-packet logging switched on at run time with <c>NetworkClient.tracePackets</c> (SPEC-0001 FR-7 logging). Every
    /// received packet is logged as <c>&lt;- 0xID NAME (payload N B)</c> and every sent packet as <c>-&gt; NAME 0xID (N B)</c>.
    /// Strings are built only while the switch is on, so the default path allocates nothing.
    /// </summary>
    internal static class PacketTrace
    {
        private static Dictionary<ushort, string> s_names;

        public static string NameOf(ushort id)
        {
            if (s_names == null)
            {
                s_names = new Dictionary<ushort, string>();
                foreach (FieldInfo f in typeof(PacketIds).GetFields(BindingFlags.Public | BindingFlags.Static))
                    if (f.FieldType == typeof(ushort) && f.IsLiteral)
                        s_names[(ushort)f.GetRawConstantValue()] = f.Name;   // IDs are unique per direction; a shared ID shows the last name
            }
            return s_names.TryGetValue(id, out string name) ? name : "UNKNOWN";
        }

        public static void Received(ushort id, int payloadBytes, bool handled) =>
            NetLog.Packet($"<- 0x{id:X4} {NameOf(id)} (payload {payloadBytes} B){(handled ? "" : " [no handler, skipped]")}");

        public static void Sent(string typeName, int wireSize, bool queued) =>
            NetLog.Packet($"-> {typeName} ({wireSize} B){(queued ? "" : " [NOT SENT: no open connection]")}");
    }

    /// <summary>Logs each packet and forwards it to the real router.</summary>
    internal sealed class TracingRouter : IPacketRouter
    {
        private readonly IPacketRouter _inner;
        public TracingRouter(IPacketRouter inner) => _inner = inner;

        public bool Route(ushort packetId, ReadOnlySpan<byte> payload)
        {
            int length = payload.Length;
            bool handled = _inner.Route(packetId, payload);
            PacketTrace.Received(packetId, length, handled);
            return handled;
        }
    }
}
