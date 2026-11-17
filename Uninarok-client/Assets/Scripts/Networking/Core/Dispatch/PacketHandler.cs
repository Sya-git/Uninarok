namespace Uninarok.Networking.Core.Dispatch
{
    /// <summary>Handler of one decoded packet. The packet is passed by readonly reference: no copy, no boxing.</summary>
    public delegate void PacketHandler<T>(in T packet) where T : struct;
}
