using Uninarok.Networking.Client;

namespace Uninarok.Networking.Tests
{
    /// <summary>A clock the test controls: time only moves when the test says so.</summary>
    public sealed class FakeClock : IClock
    {
        public long NowMs { get; set; }

        public void AdvanceMs(long ms) => NowMs += ms;
        public void AdvanceSeconds(double seconds) => NowMs += (long)(seconds * 1000.0);
    }
}
