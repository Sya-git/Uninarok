using System;

namespace Uninarok.Networking.Client
{
    /// <summary>Timeouts and keepalive intervals of the networking layer (SPEC-0001 FR-10, FR-14; plan 4.7). Seconds.</summary>
    [Serializable]
    public sealed class NetworkSettings
    {
        /// <summary>Longest wait for a connection to be established.</summary>
        public float ConnectSeconds = 15f;

        /// <summary>Longest wait for a server reply once connected.</summary>
        public float ReplySeconds = 15f;

        /// <summary>Longest wait for the map to finish loading after the server accepted the entry.</summary>
        public float MapLoadSeconds = 60f;

        /// <summary>Map keepalive interval (HEARTBEAT_REQUEST). Hercules closes idle sockets after 60 s.</summary>
        public float MapKeepaliveSeconds = 10f;

        /// <summary>Character-server keepalive interval (CHARACTER_SERVER_PING).</summary>
        public float CharacterKeepaliveSeconds = 12f;
    }
}
