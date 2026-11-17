namespace Uninarok.Networking.Client
{
    /// <summary>
    /// Monotonic millisecond clock shared by deadlines, keepalives and the enter-map timestamp (plan 4.7).
    /// Tests substitute a fake so time is controlled.
    /// </summary>
    public interface IClock
    {
        long NowMs { get; }
    }
}
