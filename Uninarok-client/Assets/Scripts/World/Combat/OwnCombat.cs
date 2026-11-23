using System;
using Uninarok.Networking.Client;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Packets.In.Map;
using Uninarok.Networking.Packets.Out.Map;

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
        /// <summary>At most one walk request towards the target this often (PLAN-0011 4.5, lead).</summary>
        public int ApproachRepeatMs { get; set; } = 200;

        readonly WorldSession _session;
        readonly INetworkClient _client;
        readonly NetworkFlow _flow;
        WalkPath _path;

        CombatState _state;
        uint _target;
        bool _continuous;
        // An attack request was sent for this target (a ground click stops it first).
        bool _requested;
        // One cell stricter per too-far reply, back to 0 at the next own blow (D4).
        int _caution;
        // The last walk request towards the target: when, and the target's cell then.
        uint _askedAt;
        int _askedX, _askedY;

        internal OwnCombat(WorldSession session, INetworkClient client, NetworkFlow flow)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
        }

        public CombatState State => _state;

        /// <summary>The monster attacked or approached; 0 while idle or fetching.</summary>
        public uint TargetId => _state == CombatState.Approaching || _state == CombatState.Attacking ? _target : 0;

        /// <summary>The attack goes on after each blow.</summary>
        public bool Continuous => _continuous;

        /// <summary><c>/noctrl</c>: plain clicks on a monster attack continuously.</summary>
        public bool NoCtrl { get; set; }

        /// <summary>An attack request was sent for the current target.</summary>
        internal bool AttackRequested => _requested;

        /// <summary>
        /// A click on a unit: attack it once, or continuously (FR-2). In range the request is sent now, else the own character walks
        /// into range first (FR-3).
        /// </summary>
        public AttackResult Attack(uint id, bool continuous)
        {
            continuous |= NoCtrl;
            if (_flow.State != FlowState.InGame || !_session.Own.TryGet(out var model, out int own)) return AttackResult.NotInGame;
            if (model[own].State == EntityState.Dead) return AttackResult.Dead;
            if (model[own].State == EntityState.Sitting) return AttackResult.Sitting;
            if (!IsAttackable(model, id, out _)) return AttackResult.NotAttackable;

            if (_state == CombatState.Attacking && _target == id && _continuous == continuous) return AttackResult.Sent;
            if (_target != id || _state == CombatState.Idle || _state == CombatState.Fetching)
            {
                _caution = 0;
                _requested = false;
            }
            _target = id;
            _continuous = continuous;
            uint now = _session.Clock.Now;
            if (InRange(model, own))
            {
                SendAttack();
                return AttackResult.Sent;
            }
            if (Approach(model, own, now))
            {
                _state = CombatState.Approaching;
                return AttackResult.Approaching;
            }
            Idle();
            return AttackResult.NoPath;
        }

        /// <summary>A click on an item on the ground; false when nothing was asked for.</summary>
        public bool PickUp(uint itemId) => throw new NotImplementedException("T3.5");

        /// <summary>Sit, map change, death: forgets the target or the item, sends nothing.</summary>
        public void Cancel() => throw new NotImplementedException("T3.4");

        /// <summary>Once per frame after the model's <c>Advance</c>, with the server time.</summary>
        public void Tick(uint now)
        {
            if (_state == CombatState.Idle || _state == CombatState.Fetching) return;
            if (!_session.Own.TryGet(out var model, out int own) || model[own].State == EntityState.Dead || !IsAttackable(model, _target, out int target))
            {
                Idle();
                return;
            }
            if (_state != CombatState.Approaching) return;

            // H4: the server keeps a request made while walking, so it is asked for as soon as the own server cell is in range.
            if (InRange(model, own))
            {
                SendAttack();
                return;
            }
            ref readonly var t = ref model[target];
            bool moved = t.CellX != _askedX || t.CellY != _askedY;
            if ((moved || !model[own].Walking) && ServerClock.Diff(now, _askedAt) >= ApproachRepeatMs && !Approach(model, own, now)) Idle();
        }

        internal void OnBlow(in Blow blow, uint now)
        {
            if (_state != CombatState.Approaching && _state != CombatState.Attacking) return;
            var model = _session.Model;
            if (model == null || blow.AttackerId != model.OwnId || blow.TargetId != _target) return;
            _caution = 0;
            if (!_continuous) Idle();
            else _state = CombatState.Attacking; // also when the server ran a request it kept from a walk (H4)
        }

        internal void OnTooFar(in ATTACK_TOO_FAR p, uint now) => throw new NotImplementedException("T3.4");
        internal void OnActionFailure(int type) => throw new NotImplementedException("T3.4");
        internal void OnPickupAnswer(int result, uint now) => throw new NotImplementedException("T3.5");

        // ---- rules ----

        // FR-2: a living monster that is not the own character.
        static bool IsAttackable(WorldModel model, uint id, out int slot)
        {
            slot = -1;
            if (id == 0 || id == model.OwnId || !model.TryGetSlot(id, out slot)) return false;
            ref readonly var e = ref model[slot];
            return e.Kind == EntityKind.Monster && e.State != EntityState.Dead;
        }

        int Range => Math.Max(0, _session.Status.AttackRange - _caution);

        bool InRange(WorldModel model, int own)
        {
            if (!model.TryGetSlot(_target, out int target)) return false;
            ref readonly var o = ref model[own];
            ref readonly var t = ref model[target];
            return AttackRange.CanStrike(model.Pathfinder, o.CellX, o.CellY, t.CellX, t.CellY, Range, t.Walking);
        }

        void SendAttack()
        {
            if (!_client.Send(new REQUEST_ACT(_target, _continuous ? REQUEST_ACT.AttackRepeat : REQUEST_ACT.Attack))) return;
            _requested = true;
            _state = CombatState.Attacking;
        }

        // A* to the target's cell; the walk goes to the first cell of that path from which a standing target is in range. An
        // occupied cell: the next one; a walk too long for the server: the farthest cell before it that the server accepts.
        bool Approach(WorldModel model, int own, uint now)
        {
            if (!model.TryGetSlot(_target, out int target)) return false;
            ref readonly var o = ref model[own];
            ref readonly var t = ref model[target];
            _askedAt = now;
            _askedX = t.CellX;
            _askedY = t.CellY;
            if (model.Pathfinder.Search(o.CellX, o.CellY, t.CellX, t.CellY, ref _path) != PathResult.Found) return false;

            int first = -1, x, y;
            for (int i = 0; i < _path.Length && first < 0; i++)
            {
                CellAt(i, out x, out y);
                if (AttackRange.CanStrike(model.Pathfinder, x, y, t.CellX, t.CellY, Range, false)) first = i;
            }
            if (first < 0) return false;
            for (int i = first; i < _path.Length; i++)
            {
                CellAt(i, out x, out y);
                var result = _session.Own.WalkFor(x, y);
                if (result == WalkRequestResult.Sent) return true;
                if (result != WalkRequestResult.Occupied) break;
            }
            for (int i = first - 1; i >= 0; i--)
            {
                CellAt(i, out x, out y);
                if (_session.Own.WalkFor(x, y) == WalkRequestResult.Sent) return true;
            }
            return false;
        }

        // The cell after step 'index' of the path.
        void CellAt(int index, out int x, out int y)
        {
            x = _path.StartX;
            y = _path.StartY;
            for (int i = 0; i <= index; i++)
            {
                x += HerculesPathfinder.Dx(_path.Step(i));
                y += HerculesPathfinder.Dy(_path.Step(i));
            }
        }

        void Idle()
        {
            _state = CombatState.Idle;
            _target = 0;
            _requested = false;
            _caution = 0;
        }
    }
}
