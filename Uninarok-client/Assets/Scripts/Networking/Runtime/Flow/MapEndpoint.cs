using Unity.Collections;

namespace Uninarok.Networking.Flow
{
    /// <summary>Where to connect to enter the map server, from MAP_ENTER_REQUEST_ACCEPTED (plan 4.9). The port is a ushort: 0 to 65535.</summary>
    public readonly struct MapEndpoint
    {
        public readonly string Host;
        public readonly ushort Port;
        public readonly FixedString32Bytes MapName;
        public readonly FixedString512Bytes DnsHost;

        public MapEndpoint(string host, ushort port, in FixedString32Bytes mapName, in FixedString512Bytes dnsHost)
        {
            Host = host;
            Port = port;
            MapName = mapName;
            DnsHost = dnsHost;
        }
    }
}
