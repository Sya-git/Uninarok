using System;
using System.Collections.Generic;
using Uninarok.Networking.Core.Framing;

namespace Uninarok.Networking.Tests
{
    /// <summary>Records every routed packet as (id, payload copy). Handled IDs are configurable; the rest return false.</summary>
    public sealed class RecordingRouter : IPacketRouter
    {
        public readonly List<(ushort Id, byte[] Payload)> Packets = new List<(ushort, byte[])>();
        private readonly HashSet<ushort> _handled = new HashSet<ushort>();
        private bool _handleAll = true;

        /// <summary>Optional hook run for every routed packet (for example to throw or to block).</summary>
        public Action<ushort> OnRoute;

        /// <summary>Only the given IDs are handled; others return false (known but unhandled).</summary>
        public RecordingRouter HandleOnly(params ushort[] ids)
        {
            _handleAll = false;
            _handled.Clear();
            foreach (var id in ids) _handled.Add(id);
            return this;
        }

        public bool Route(ushort packetId, ReadOnlySpan<byte> payload)
        {
            if (!_handleAll && !_handled.Contains(packetId)) return false;
            Packets.Add((packetId, payload.ToArray()));
            OnRoute?.Invoke(packetId);
            return true;
        }
    }
}
