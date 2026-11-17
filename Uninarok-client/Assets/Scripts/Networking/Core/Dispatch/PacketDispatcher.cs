using System;
using System.Threading;

namespace Uninarok.Networking.Core.Dispatch
{
    /// <summary>
    /// Delivers decoded packets to subscribed handlers (SPEC-0001 FR-4, FR-6, FR-9; plan 4.5, D5).
    /// Handler lists live per instance in an array indexed by a per-type slot: dispatch is one array index plus a type
    /// test, with no boxing, dictionary or allocation. Lists are copy-on-write: <see cref="Subscribe{T}"/> and
    /// <see cref="Unsubscribe{T}"/> allocate a new array (setup time), so a handler may subscribe or unsubscribe during a
    /// dispatch without skipping or repeating a callback; the change applies from the next packet.
    /// Main thread only.
    /// </summary>
    public sealed class PacketDispatcher
    {
        private static int s_nextSlot;

        private static class Slot<T> where T : struct
        {
            internal static readonly int Index = Interlocked.Increment(ref s_nextSlot) - 1;
        }

        private readonly Action<Exception> _onHandlerError;
        private object[] _lists = new object[32];

        public PacketDispatcher(Action<Exception> onHandlerError)
        {
            _onHandlerError = onHandlerError ?? throw new ArgumentNullException(nameof(onHandlerError));
        }

        /// <summary>Adds a handler. A handler already subscribed (same delegate) is ignored.</summary>
        public void Subscribe<T>(PacketHandler<T> handler) where T : struct
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            int slot = Slot<T>.Index;
            if (slot >= _lists.Length) Array.Resize(ref _lists, Math.Max(slot + 1, _lists.Length * 2));

            var current = _lists[slot] as PacketHandler<T>[];
            if (current == null)
            {
                _lists[slot] = new[] { handler };
                return;
            }
            if (Array.IndexOf(current, handler) >= 0) return;

            var next = new PacketHandler<T>[current.Length + 1];
            Array.Copy(current, next, current.Length);
            next[current.Length] = handler;
            _lists[slot] = next;
        }

        /// <summary>Removes a handler. A handler that is not subscribed is ignored.</summary>
        public void Unsubscribe<T>(PacketHandler<T> handler) where T : struct
        {
            if (handler == null) return;
            int slot = Slot<T>.Index;
            if (slot >= _lists.Length) return;

            var current = _lists[slot] as PacketHandler<T>[];
            if (current == null) return;
            int at = Array.IndexOf(current, handler);
            if (at < 0) return;

            if (current.Length == 1)
            {
                _lists[slot] = null;
                return;
            }
            var next = new PacketHandler<T>[current.Length - 1];
            Array.Copy(current, 0, next, 0, at);
            Array.Copy(current, at + 1, next, at, current.Length - at - 1);
            _lists[slot] = next;
        }

        /// <summary>
        /// Calls every handler of <typeparamref name="T"/>, each in its own try/catch: an exception goes to the error
        /// callback and the next handler still runs (FR-4). Allocates nothing.
        /// </summary>
        public void Dispatch<T>(in T packet) where T : struct
        {
            int slot = Slot<T>.Index;
            if (slot >= _lists.Length) return;
            var handlers = _lists[slot] as PacketHandler<T>[];   // snapshot: later changes do not affect this dispatch
            if (handlers == null) return;

            for (int i = 0; i < handlers.Length; i++)
            {
                try
                {
                    handlers[i](in packet);
                }
                catch (Exception e)
                {
                    _onHandlerError(e);
                }
            }
        }

        /// <summary>Number of handlers subscribed for <typeparamref name="T"/> (tests).</summary>
        public int HandlerCount<T>() where T : struct
        {
            int slot = Slot<T>.Index;
            if (slot >= _lists.Length) return 0;
            var handlers = _lists[slot] as PacketHandler<T>[];
            return handlers == null ? 0 : handlers.Length;
        }
    }
}
