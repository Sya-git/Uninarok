using System;
using Uninarok.Networking.Client;

namespace Uninarok.World
{
    /// <summary>
    /// The client's estimate of the server's tick (SPEC-0010 FR-2; PLAN-0010 4.2, D6): started at map entry, corrected by every
    /// heartbeat answer. It keeps the sample with the shortest round trip of the last eight. A correction forward applies at once;
    /// one backward slows the estimate to half speed until it is caught up, so it never runs backwards. No allocation.
    /// </summary>
    public sealed class ServerClock
    {
        public const int SampleCount = 8;
        /// <summary>How much of the passed time a backward correction takes away.</summary>
        const double SlewRate = 0.5;

        readonly IClock _local;
        readonly long[] _offsets = new long[SampleCount];
        readonly long[] _roundTrips = new long[SampleCount];
        int _samples, _next;

        // The offset from local to server time (64 bit: the server's tick unwrapped) goes from _from down to _target, starting at
        // the local time _since; equal when nothing is being corrected.
        double _from;
        long _target, _since;

        public ServerClock(IClock local) => _local = local ?? throw new ArgumentNullException(nameof(local));

        /// <summary>The clock has a server time: <see cref="Start"/> or <see cref="Sample"/> ran.</summary>
        public bool IsSynced { get; private set; }

        /// <summary>Round trip of the last sample in ms, −1 before the first.</summary>
        public int LastRoundTripMs { get; private set; } = -1;

        /// <summary>The estimated server tick in ms; wraps as the server's <c>uint</c>. 0 before <see cref="Start"/>.</summary>
        public uint Now => IsSynced ? unchecked((uint)At(_local.NowMs)) : 0;

        /// <summary>The server accepted the entry: its tick, the one-way delay unknown and taken as 0. A new server starts over.</summary>
        public void Start(uint serverTick, long localReceivedMs)
        {
            _samples = _next = 0;
            LastRoundTripMs = -1;
            _target = serverTick - localReceivedMs;
            _from = _target;
            _since = localReceivedMs;
            IsSynced = true;
        }

        /// <summary><see cref="Start(uint,long)"/> with the tick received now.</summary>
        public void Start(uint serverTick) => Start(serverTick, _local.NowMs);

        /// <summary>A heartbeat answer: the server read its clock half a round trip ago.</summary>
        public void Sample(uint serverTick, long localSentMs, long localReceivedMs)
        {
            if (!IsSynced) Start(serverTick, localReceivedMs);
            long rtt = Math.Max(0, localReceivedMs - localSentMs);
            LastRoundTripMs = (int)Math.Min(rtt, int.MaxValue);

            // The tick is 32 bits: the full value is the one nearest to the estimate.
            long expected = At(localReceivedMs);
            long tick = expected + Diff(serverTick, unchecked((uint)expected));

            _offsets[_next] = tick + rtt / 2 - localReceivedMs;
            _roundTrips[_next] = rtt;
            _next = (_next + 1) % SampleCount;
            if (_samples < SampleCount) _samples++;

            int best = 0;
            for (int i = 1; i < _samples; i++)
                if (_roundTrips[i] < _roundTrips[best]) best = i;

            double current = OffsetAt(localReceivedMs);
            _target = _offsets[best];
            _from = Math.Max(current, _target); // forward at once, backward from where it is
            _since = localReceivedMs;
        }

        /// <summary>a − b with the wrap of the server's tick (<c>DIFF_TICK</c>).</summary>
        public static int Diff(uint a, uint b) => unchecked((int)(a - b));

        double OffsetAt(long localMs)
        {
            double passed = localMs > _since ? localMs - _since : 0;
            return Math.Max(_target, _from - passed * SlewRate);
        }

        long At(long localMs) => (long)Math.Floor(localMs + OffsetAt(localMs));
    }
}
