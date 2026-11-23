using System;
using Uninarok.Networking.Client;
using Uninarok.Networking.Packets.In.Map;

namespace Uninarok.World.Dev
{
    /// <summary>
    /// What the server answers to a command of the dev command line (SPEC-0010 FR-20; PLAN-0010 4.12, D38): the lines for the own
    /// chat window. Dev only; the chat spec takes them over.
    /// </summary>
    sealed class DevMessages : IDisposable
    {
        readonly Subscriptions _subscriptions;
        readonly Action<string> _show;

        public DevMessages(INetworkClient client, Action<string> show)
        {
            _show = show ?? throw new ArgumentNullException(nameof(show));
            _subscriptions = new Subscriptions(client);
            _subscriptions.Add<OWN_MESSAGE>(OnMessage);
        }

        /// <summary>The last command sent, as a chat line; null: none.</summary>
        public string Sent { get; set; }

        public void Dispose() => _subscriptions.Clear();

        void OnMessage(in OWN_MESSAGE p)
        {
            if (p.Text.Length == 0) return;
            // A command the account may not use is a chat line for the server, which it sends back.
            if (p.Text == Sent) _show("the server took it as chat: this account has no such command (group of the account)");
            else _show(p.Text);
        }
    }
}
