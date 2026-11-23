using System;
using Uninarok.Networking.Client;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Packets.In.Map;

namespace Uninarok.World
{
    /// <summary>What the own character is doing in combat (PLAN-0011 4.5).</summary>
    public enum CombatState : byte
    {
        Idle,
        /// <summary>Walking towards a target to attack it.</summary>
        Approaching,
        /// <summary>An attack request was sent for the target.</summary>
        Attacking,
        /// <summary>Walking towards an item to pick it up.</summary>
        Fetching,
    }

    /// <summary>What became of an attack request; only <see cref="Sent"/> sent a request now.</summary>
    public enum AttackResult : byte
    {
        Sent,
        /// <summary>Out of range: a walk towards the target was asked for.</summary>
        Approaching,
        NotInGame,
        Dead,
        Sitting,
        /// <summary>Not a living monster: players, NPCs, items, the own character, unknown ids (SPEC-0011 FR-2).</summary>
        NotAttackable,
        /// <summary>No cell to attack from can be walked to.</summary>
        NoPath,
    }

    /// <summary>
    /// The own character's attacks and pickups (SPEC-0011 FR-2, FR-3, FR-10; PLAN-0011 4.5): it asks the server only for what the
    /// server would accept, walks into range first, and reacts to the server's too-far replies and to its silence. Plain rules on
    /// the model, no scene. Main thread.
    /// </summary>
    public sealed class OwnCombat
    {
        readonly WorldSession _session;
        readonly INetworkClient _client;
        readonly NetworkFlow _flow;

        internal OwnCombat(WorldSession session, INetworkClient client, NetworkFlow flow)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
        }

        public CombatState State => throw new NotImplementedException("T3.3");
        public uint TargetId => throw new NotImplementedException("T3.3");
        public bool Continuous => throw new NotImplementedException("T3.3");

        /// <summary><c>/noctrl</c>: plain clicks on a monster attack continuously.</summary>
        public bool NoCtrl { get; set; }

        /// <summary>A click on a unit: attack it once, or continuously.</summary>
        public AttackResult Attack(uint id, bool continuous) => throw new NotImplementedException("T3.3");

        /// <summary>A click on an item on the ground; false when nothing was asked for.</summary>
        public bool PickUp(uint itemId) => throw new NotImplementedException("T3.5");

        /// <summary>Sit, map change, death: forgets the target or the item, sends nothing.</summary>
        public void Cancel() => throw new NotImplementedException("T3.4");

        /// <summary>Once per frame after the model's <c>Advance</c>, with the server time.</summary>
        public void Tick(uint now) => throw new NotImplementedException("T3.3");

        internal void OnBlow(in Blow blow, uint now) => throw new NotImplementedException("T3.3");
        internal void OnTooFar(in ATTACK_TOO_FAR p, uint now) => throw new NotImplementedException("T3.4");
        internal void OnActionFailure(int type) => throw new NotImplementedException("T3.4");
        internal void OnPickupAnswer(int result, uint now) => throw new NotImplementedException("T3.5");
    }
}
