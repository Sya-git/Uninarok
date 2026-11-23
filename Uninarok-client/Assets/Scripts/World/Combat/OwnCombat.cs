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

        /// <summary>While attacking, no own blow for this long (at least; 2 × the last attack motion + 500) asks again (H1, H6).</summary>
        public int AttackSilenceMs { get; set; } = 1500;

        /// <summary>Requests in a row without an own blow before the attack is given up.</summary>
        public int AttackRetries { get; set; } = 3;

        /// <summary>A refused pickup is asked for once more after this (a step of lag).</summary>
        public int PickupRetryMs { get; set; } = 400;

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
        // The watchdog: since when the server is silent, the last own attack motion, the requests without a blow.
        uint _silentSince;
        int _lastAttackMs;
        int _retries;
        // The item asked for, whether a request waits for its answer, the failures, a retry due.
        uint _item;
        bool _pickUpSent;
        int _pickupFailures;
        bool _retryPickUp;
        uint _retryAt;

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
                _lastAttackMs = 0;
            }
            _retries = 0;
            _target = id;
            _continuous = continuous;
            uint now = _session.Clock.Now;
            if (InRange(model, own))
            {
                SendAttack(now);
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

        /// <summary>
        /// A click on an item on the ground (FR-10): within two cells and standing, it is asked for now; else the own character
        /// walks to the first cell of the path within two cells and asks when the walk ends. False when nothing was asked for.
        /// </summary>
        public bool PickUp(uint itemId)
        {
            if (!_session.Own.TryGet(out var model, out int own) || model[own].State != EntityState.Standing) return false;
            if (!model.TryGetSlot(itemId, out int item) || !model[item].IsItem) return false;
            Idle();
            _item = itemId;
            _pickupFailures = 0;
            uint now = _session.Clock.Now;
            ref readonly var o = ref model[own];
            if (!o.Walking && AttackRange.CanPickUp(o.CellX, o.CellY, model[item].CellX, model[item].CellY)) return SendPickUp();
            if (!Fetch(model, own, item, now))
            {
                _item = 0;
                return false;
            }
            _state = CombatState.Fetching;
            return true;
        }

        /// <summary>Sit, map change, death: forgets the target or the item, sends nothing.</summary>
        public void Cancel()
        {
            Idle();
            _item = 0;
            _pickUpSent = false;
            _retryPickUp = false;
        }

        /// <summary>Once per frame after the model's <c>Advance</c>, with the server time.</summary>
        public void Tick(uint now)
        {
            if (_retryPickUp && ServerClock.Diff(now, _retryAt) >= 0) RetryPickUp();
            if (_state == CombatState.Fetching) TickFetch(now);
            if (_state == CombatState.Idle || _state == CombatState.Fetching) return;
            if (!_session.Own.TryGet(out var model, out int own) || model[own].State == EntityState.Dead || !IsAttackable(model, _target, out int target))
            {
                Idle();
                return;
            }
            if (_state == CombatState.Attacking)
            {
                Watch(model, own, now);
                return;
            }

            // H4: the server keeps a request made while walking, so it is asked for as soon as the own server cell is in range.
            if (InRange(model, own))
            {
                SendAttack(now);
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
            _retries = 0;
            _silentSince = now;
            _lastAttackMs = blow.AttackMs;
            if (!_continuous) Idle();
            else _state = CombatState.Attacking; // also when the server ran a request it kept from a walk (H4)
        }

        // 0x0139 for the current target: closer by one cell for it, at most range − 1 (D4); a single blow is asked for again too.
        internal void OnTooFar(in ATTACK_TOO_FAR p, uint now)
        {
            if ((_state != CombatState.Approaching && _state != CombatState.Attacking) || p.TargetId != _target) return;
            if (!_session.Own.TryGet(out var model, out int own)) return;
            _caution = Math.Min(_caution + 1, Math.Max(0, _session.Status.AttackRange - 1));
            Resume(model, own, now);
        }

        // 0x013B: type 0, no arrows; the server will not attack (H6, D9).
        internal void OnActionFailure(int type)
        {
            if (type != ACTION_FAILURE.NoArrows || (_state != CombatState.Approaching && _state != CombatState.Attacking)) return;
            Idle();
            NetLog.Info("attack stopped: no arrows (ZC_ACTION_FAILURE 0)");
        }
        // ZC_ITEM_PICKUP_ACK to the request sent: a failure while the item is there is asked for once more after a step of lag,
        // then reported with its code as received (D13). Success needs nothing: the server removes the item.
        internal void OnPickupAnswer(int result, uint now)
        {
            if (!_pickUpSent) return;
            _pickUpSent = false;
            if (result == 0)
            {
                _item = 0;
                return;
            }
            if (++_pickupFailures == 1 && ItemThere(out _, out _))
            {
                _retryPickUp = true;
                _retryAt = unchecked(now + (uint)Math.Max(0, PickupRetryMs));
                return;
            }
            NetLog.Info($"pickup failed: ZC_ITEM_PICKUP_ACK result {result}");
            _item = 0;
        }

        // ---- pickup ----

        bool ItemThere(out WorldModel model, out int slot)
        {
            slot = -1;
            model = _session.Model;
            return _item != 0 && model != null && model.TryGetSlot(_item, out slot) && model[slot].IsItem;
        }

        bool SendPickUp()
        {
            if (!_client.Send(new PICKUP_ITEM(_item))) return false;
            _pickUpSent = true;
            return true;
        }

        void RetryPickUp()
        {
            _retryPickUp = false;
            if (_state != CombatState.Idle || !ItemThere(out _, out _) || !_session.Own.CanAct)
            {
                _item = 0;
                return;
            }
            SendPickUp();
        }

        // The server tests its own cell once and keeps nothing (unlike attacks, H4): asked for only when the walk has ended.
        void TickFetch(uint now)
        {
            if (!ItemThere(out var model, out int item) || !_session.Own.TryGet(out _, out int own) || model[own].State != EntityState.Standing)
            {
                Idle();
                return;
            }
            ref readonly var o = ref model[own];
            if (o.Walking) return;
            if (AttackRange.CanPickUp(o.CellX, o.CellY, model[item].CellX, model[item].CellY))
            {
                _state = CombatState.Idle;
                SendPickUp();
                return;
            }
            if (ServerClock.Diff(now, _askedAt) >= ApproachRepeatMs && !Fetch(model, own, item, now)) Idle();
        }

        // The first cell of the path to the item within two cells; an occupied one: the next.
        bool Fetch(WorldModel model, int own, int item, uint now)
        {
            ref readonly var o = ref model[own];
            ref readonly var i = ref model[item];
            _askedAt = now;
            if (model.Pathfinder.Search(o.CellX, o.CellY, i.CellX, i.CellY, ref _path) != PathResult.Found) return false;
            for (int step = 0; step < _path.Length; step++)
            {
                CellAt(step, out int x, out int y);
                if (!AttackRange.CanPickUp(x, y, i.CellX, i.CellY)) continue;
                var result = _session.Own.WalkFor(x, y);
                if (result == WalkRequestResult.Sent) return true;
                if (result != WalkRequestResult.Occupied) return false;
            }
            return false;
        }

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

        void SendAttack(uint now)
        {
            if (!_client.Send(new REQUEST_ACT(_target, _continuous ? REQUEST_ACT.AttackRepeat : REQUEST_ACT.Attack))) return;
            _requested = true;
            _state = CombatState.Attacking;
            _silentSince = now;
        }

        // In range: ask now; else walk; no way there: idle.
        void Resume(WorldModel model, int own, uint now)
        {
            if (InRange(model, own)) SendAttack(now);
            else if (Approach(model, own, now)) _state = CombatState.Approaching;
            else Idle();
        }

        // H1, H6: the server drops an attack without a word (no line of sight, weight, a status). The client reacts to the silence,
        // not to its own idea of the target's cell (D5).
        void Watch(WorldModel model, int own, uint now)
        {
            int limit = Math.Max(AttackSilenceMs, 2 * _lastAttackMs + 500);
            if (ServerClock.Diff(now, _silentSince) < limit) return;
            if (++_retries > AttackRetries)
            {
                Idle();
                NetLog.Info($"attack given up: no blow after {AttackRetries} more requests");
                return;
            }
            _silentSince = now;
            Resume(model, own, now);
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
            _retries = 0;
        }
    }
}
