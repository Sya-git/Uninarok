using System;
using Uninarok.Networking.Client;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Packets.Out.Map;

namespace Uninarok.World
{
    /// <summary>
    /// The own character's death prompt, without its view (SPEC-0011 FR-8; PLAN-0011 4.11): open while the own character is dead
    /// (not feigning) and its death is shown, unless the player closed it; Esc toggles it while dead. "Return to save point"
    /// sends <c>RESTART(0)</c> once, then the prompt waits hidden; with neither a map change nor a resurrection within
    /// <see cref="TimeoutMs"/> it opens again. It closes when the character stands or the game is left.
    /// </summary>
    public sealed class RestartPrompt
    {
        readonly WorldSession _session;
        readonly INetworkClient _client;
        readonly NetworkFlow _flow;

        bool _open, _dead, _held, _closed, _awaiting;
        uint _now, _askedAt;

        internal RestartPrompt(WorldSession session, INetworkClient client, NetworkFlow flow)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
        }

        /// <summary>How long an answer to "return to save point" is awaited before the prompt opens again (<c>RestartTimeoutMs</c>).</summary>
        public int TimeoutMs { get; set; } = 3000;

        /// <summary>Own character dead (not feigning) and its death shown, not closed, no answer awaited.</summary>
        public bool IsOpen => _open;

        /// <summary>When <see cref="IsOpen"/> changes.</summary>
        public event Action Changed;

        /// <summary>Each frame, from the model's own state and the timeline's death hold.</summary>
        public void Update(uint now)
        {
            _now = now;
            var model = _session.Model;
            int slot = -1;
            _dead = _flow.State == FlowState.InGame && model != null && model.TryGetSlot(model.OwnId, out slot)
                    && model[slot].State == EntityState.Dead && !model[slot].Feigning;
            if (!_dead)
            {
                // Standing again (resurrection, respawn on the new map) or out of the game: a new death starts over.
                _closed = _awaiting = false;
                _held = false;
                Set(false);
                return;
            }
            if (_awaiting)
            {
                if (ServerClock.Diff(now, _askedAt) < TimeoutMs)
                {
                    Set(false);
                    return;
                }
                _awaiting = false;
            }
            _held = _session.Timeline != null && _session.Timeline.HoldsDeath(slot, now);
            Set(!_closed && !_held);
        }

        /// <summary>Esc: closes an open prompt, opens a closed one; only while dead and no answer is awaited.</summary>
        public void ToggleByEscape()
        {
            if (!_dead || _awaiting) return;
            if (_open) Close();
            else if (_closed)
            {
                _closed = false;
                Set(!_held);
            }
        }

        /// <summary>The close button: hidden until Esc or the next death.</summary>
        public void Close()
        {
            if (!_open) return;
            _closed = true;
            Set(false);
        }

        /// <summary>Sends <c>RESTART(0)</c> once and waits hidden; false when the prompt is not open or nothing could be sent.</summary>
        public bool ReturnToSavePoint()
        {
            if (!_open || !_client.Send(new RESTART(RESTART.SavePoint))) return false;
            _awaiting = true;
            _askedAt = _now;
            Set(false);
            return true;
        }

        void Set(bool open)
        {
            if (_open == open) return;
            _open = open;
            Changed?.Invoke();
        }
    }
}
