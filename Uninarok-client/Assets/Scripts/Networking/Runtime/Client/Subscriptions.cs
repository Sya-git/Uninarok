using System.Collections.Generic;
using Uninarok.Networking.Core.Dispatch;

namespace Uninarok.Networking.Client
{
    /// <summary>
    /// Remembers the packet handlers an owner subscribed and releases them all at once (SPEC-0001 FR-9; plan 4.7).
    /// <see cref="Clear"/> unsubscribes but keeps the entries, so adding the same handler again (a manager's next
    /// <c>Enter</c>) reuses its entry: after the first cycle this class allocates nothing.
    /// </summary>
    public sealed class Subscriptions
    {
        private interface IEntry
        {
            bool Active { get; }
            void Release(INetworkClient client);
        }

        private sealed class Entry<T> : IEntry where T : struct
        {
            public readonly PacketHandler<T> Handler;
            public bool Active { get; set; }

            public Entry(PacketHandler<T> handler) { Handler = handler; }

            public void Release(INetworkClient client)
            {
                client.Unsubscribe(Handler);
                Active = false;
            }
        }

        private readonly INetworkClient _client;
        private readonly List<IEntry> _entries = new List<IEntry>();

        public Subscriptions(INetworkClient client)
        {
            _client = client ?? throw new System.ArgumentNullException(nameof(client));
        }

        /// <summary>Number of handlers currently subscribed through this object.</summary>
        public int Count
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _entries.Count; i++)
                    if (_entries[i].Active) n++;
                return n;
            }
        }

        /// <summary>Subscribes now and remembers the handler for <see cref="Clear"/>. Adding the same handler twice is harmless.</summary>
        public void Add<T>(PacketHandler<T> handler) where T : struct
        {
            if (handler == null) throw new System.ArgumentNullException(nameof(handler));

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i] is Entry<T> existing && existing.Handler == handler)
                {
                    if (!existing.Active)
                    {
                        _client.Subscribe(handler);
                        existing.Active = true;
                    }
                    return;
                }
            }

            _client.Subscribe(handler);
            _entries.Add(new Entry<T>(handler) { Active = true });
        }

        /// <summary>Unsubscribes everything added. Safe to call repeatedly.</summary>
        public void Clear()
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Active) _entries[i].Release(_client);
        }
    }
}
