using System;
using Uninarok.Networking.Client;
using Uninarok.Networking.Flow;

namespace Uninarok.World
{
    /// <summary>
    /// What the player's own character may ask for, and how (SPEC-0010 FR-9, FR-13, FR-19; PLAN-0010 4.6): it checks with the
    /// model and the pathfinder as the server will, then sends. Walking and sitting change the model only when the server answers
    /// (D9). Plain rules: no input, no scene.
    /// </summary>
    public sealed class OwnCharacter
    {
        readonly WorldSession _session;
        readonly INetworkClient _client;
        readonly NetworkFlow _flow;

        internal OwnCharacter(WorldSession session, INetworkClient client, NetworkFlow flow)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
        }

        /// <summary>Alive, in game, on a map.</summary>
        public bool CanAct => TryGet(out var model, out int slot) && model[slot].State != EntityState.Dead;

        /// <summary>The own entity; false when it is not in the world or the flow is not in game.</summary>
        public bool TryGet(out WorldModel model, out int slot)
        {
            model = _session.Model;
            slot = -1;
            return model != null && _flow.State == FlowState.InGame && model.TryGetSlot(model.OwnId, out slot);
        }
    }
}
