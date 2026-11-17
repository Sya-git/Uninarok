using UnityEngine;

namespace Uninarok.Networking.Client
{
    /// <summary>Real-time clock: milliseconds since startup. Unaffected by <c>Time.timeScale</c>. Main thread only.</summary>
    public sealed class UnityClock : IClock
    {
        public long NowMs => (long)(Time.realtimeSinceStartupAsDouble * 1000.0);
    }
}
