using System;
using Uninarok.Map;
using Uninarok.Networking.Client;
using Uninarok.Networking.Core.Dispatch;
using Uninarok.Networking.Data;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Packets.In.Map;

namespace Uninarok.World
{
    /// <summary>
    /// Packets to the world model (SPEC-0010 FR-3 to FR-8, FR-15, FR-16; PLAN-0010 4.6): one handler per packet, each one model
    /// call. It owns the model of the current map and feeds the server clock. Handlers are subscribed while the flow is on a map
    /// server and do nothing while there is no model. No allocation in a handler. Main thread.
    /// </summary>
    public sealed class WorldSession : IDisposable
    {
        const byte ActSit = 2, ActStand = 3;
        const ushort VarSpeed = 0; // SP_SPEED
        const int DefaultSpeed = 150; // DEFAULT_WALK_SPEED

        readonly NetworkFlow _flow;
        readonly ServerClock _clock;
        readonly WorldSettings _settings;
        readonly Subscriptions _subscriptions;
        bool _disposed;

        // The own character between maps: what the server last said of it. Nothing before the first entry of a character.
        bool _entered;
        EntityLook _ownLook;
        UninarokDirection _ownFacing;
        short _ownSpeed;

        readonly PacketHandler<UNIT_STAND_ENTRY> _onStand;
        readonly PacketHandler<UNIT_NEW_ENTRY> _onNew;
        readonly PacketHandler<UNIT_MOVE_ENTRY> _onMoveEntry;
        readonly PacketHandler<UNIT_MOVE> _onMove;
        readonly PacketHandler<PLAYER_MOVE> _onOwnMove;
        readonly PacketHandler<UNIT_STOP_MOVE> _onStop;
        readonly PacketHandler<UNIT_SLIDE> _onSlide;
        readonly PacketHandler<UNIT_VANISH> _onVanish;
        readonly PacketHandler<UNIT_DIRECTION> _onDirection;
        readonly PacketHandler<UNIT_LOOK_CHANGE> _onLook;
        readonly PacketHandler<UNIT_ACT> _onAct;
        readonly PacketHandler<ITEM_ENTRY> _onItem;
        readonly PacketHandler<ITEM_FALL_ENTRY> _onItemFall;
        readonly PacketHandler<ITEM_DISAPPEAR> _onItemGone;
        readonly PacketHandler<PARAMETER_CHANGE> _onParameter;
        // SPEC-0011
        bool _statusKnown;
        readonly PacketHandler<DAMAGE> _onDamage;
        readonly PacketHandler<LONG_PARAMETER_CHANGE> _onLongParameter;
        readonly PacketHandler<EXP_GAIN> _onExpGain;
        readonly PacketHandler<ATTACK_RANGE> _onAttackRange;
        readonly PacketHandler<ATTACK_TOO_FAR> _onTooFar;
        readonly PacketHandler<RESURRECTION> _onResurrection;
        readonly PacketHandler<ACTION_FAILURE> _onActionFailure;
        readonly PacketHandler<ITEM_PICKUP_ACK> _onPickupAnswer;

        public WorldSession(INetworkClient client, NetworkFlow flow, ServerClock clock, WorldSettings settings)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            _subscriptions = new Subscriptions(client);

            _onStand = OnStand;
            _onNew = OnNew;
            _onMoveEntry = OnMoveEntry;
            _onMove = OnMove;
            _onOwnMove = OnOwnMove;
            _onStop = OnStop;
            _onSlide = OnSlide;
            _onVanish = OnVanish;
            _onDirection = OnDirection;
            _onLook = OnLook;
            _onAct = OnAct;
            _onItem = OnItem;
            _onItemFall = OnItemFall;
            _onItemGone = OnItemGone;
            _onParameter = OnParameter;
            _onDamage = OnDamage;
            _onLongParameter = OnLongParameter;
            _onExpGain = OnExpGain;
            _onAttackRange = OnAttackRange;
            _onTooFar = OnTooFar;
            _onResurrection = OnResurrection;
            _onActionFailure = OnActionFailure;
            _onPickupAnswer = OnPickupAnswer;
            Own = new OwnCharacter(this, client, _flow);
            Combat = new OwnCombat(this, client, _flow);

            _flow.StateChanged += OnStateChanged;
            _flow.MapLoadRequested += OnMapLoadRequested;
            _flow.Zone.HeartbeatAnswered += OnHeartbeatAnswered;
            OnStateChanged(_flow.State);
        }

        /// <summary>The world of the current map; null until <see cref="BeginMap"/> and after <see cref="EndMap"/>.</summary>
        public WorldModel Model { get; private set; }

        /// <summary>The rules of the own character.</summary>
        public OwnCharacter Own { get; }

        /// <summary>The own character's attacks and pickups (SPEC-0011 FR-2, FR-3, FR-10).</summary>
        public OwnCombat Combat { get; }

        public ServerClock Clock => _clock;
        public WorldSettings Settings => _settings;

        /// <summary>The own character's HP, SP, levels, EXP and range, kept across maps (SPEC-0011 FR-1).</summary>
        public OwnStatus Status { get; } = new OwnStatus();

        public delegate void BlowHandler(in Blow blow);

        /// <summary>Every blow in view, as it is handled (SPEC-0011 FR-11: the dev HUD).</summary>
        public event BlowHandler BlowReceived;

        /// <summary>EXP gained by the own character: variable (1 base, 2 job) and amount (SPEC-0011 FR-11).</summary>
        public event Action<int, long> ExpGained;

        void Raise(in Blow blow) => BlowReceived?.Invoke(blow);

        void RaiseExp(int varId, long amount) => ExpGained?.Invoke(varId, amount);

        /// <summary>The map is loaded: a new world on its cells; without data (null) the world is tracked with nothing walkable (FR-1).</summary>
        public void BeginMap(MapData map) => BeginMap(map != null ? new MapDataWalkGrid(map) : new UnknownWalkGrid());

        internal void BeginMap(IWalkGrid grid)
        {
            EndMap();
            Model = new WorldModel(grid, _settings);
            AddOwn();
        }

        // FR-1: the selected character on the cell and facing the server gave. The character server's data are from before the first
        // map: after that the look and the speed are what the server last said, and nobody enters a map dead.
        void AddOwn()
        {
            var context = _flow.Context;
            if (context.AccountId == 0) return;
            var entry = context.Entry;
            var facing = entry.Position.Direction;
            var state = EntityState.Standing;
            if (!_entered)
            {
                _entered = true;
                var character = Selected(context);
                _ownLook = LookOf(character);
                if (_ownSpeed == 0) _ownSpeed = character.DefaultWalkSpeed > 0 ? character.DefaultWalkSpeed : (short)DefaultSpeed;
                if (character.CharId != 0 && character.Hp == 0) state = EntityState.Dead;
            }
            else if (entry.StartTick == 0 && _settings.WarpKeepsDirection) facing = _ownFacing; // D14
            Model.Upsert(context.AccountId, EntityKind.Player, _ownLook, _ownSpeed, entry.Position.X, entry.Position.Y, facing, HeadDirection.Straight, state);
            Model.OwnId = context.AccountId;
            SetOwnHp();
        }

        // The own entity has the HP of the status (SPEC-0011 FR-1, PLAN-0011 4.2).
        void SetOwnHp() => Model?.SetHp(Model.OwnId, (int)Math.Min(Status.Hp, int.MaxValue), (int)Math.Min(Status.MaxHp, int.MaxValue));

        // The character server lists them; the selected slot, else the selected id.
        static CharInfo Selected(SessionContext context)
        {
            foreach (var c in context.Characters)
                if (c.CharId == context.CharacterId) return c;
            foreach (var c in context.Characters)
                if (c.Slot == context.SelectedSlot) return c;
            return default;
        }

        internal static EntityLook LookOf(in CharInfo c) => new EntityLook
        {
            Job = c.Class,
            HairStyle = unchecked((ushort)c.Hair),
            HairColour = unchecked((ushort)c.HairColor),
            ClothColour = unchecked((ushort)c.ClothesColor),
            BodyStyle = unchecked((ushort)c.Body),
            Weapon = c.Weapon,
            Shield = c.LookShield,
            HeadTop = unchecked((ushort)c.LookHeadTop),
            HeadMid = unchecked((ushort)c.LookHeadMid),
            HeadBottom = unchecked((ushort)c.LookHeadBottom),
            Robe = unchecked((ushort)c.LookRobe),
            Sex = (byte)c.Sex,
        };

        /// <summary>Empties the world: map change, disconnect (FR-3). The handlers stay.</summary>
        public void EndMap()
        {
            Combat.Cancel();
            var model = Model;
            if (model == null) return;
            if (model.TryGetSlot(model.OwnId, out int slot))
            {
                _ownLook = model[slot].Look;
                _ownFacing = model[slot].Facing;
            }
            Model = null;
            model.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _flow.StateChanged -= OnStateChanged;
            _flow.MapLoadRequested -= OnMapLoadRequested;
            _flow.Zone.HeartbeatAnswered -= OnHeartbeatAnswered;
            _subscriptions.Clear();
            EndMap();
        }

        // ---- flow and clock ----

        void OnStateChanged(FlowState state)
        {
            if (state == FlowState.LoadingMap || state == FlowState.InGame) Subscribe();
            else
            {
                _subscriptions.Clear();
                EndMap(); // left the map server, or moves to another one
                // Another character or login: nothing of the old one counts. A move to another map server keeps it.
                if (state != FlowState.ConnectingToMap)
                {
                    _entered = false;
                    _ownSpeed = 0;
                    _statusKnown = false;
                    Status.Reset();
                }
            }
        }

        // Entry or map change: the old world is over before the new map loads.
        void OnMapLoadRequested()
        {
            EndMap();
            var entry = _flow.Context.Entry;
            if (entry.StartTick != 0) _clock.Start(entry.StartTick); // a map change carries no tick (MapEntry)
        }

        void OnHeartbeatAnswered(uint serverTick, long sentMs, long receivedMs) => _clock.Sample(serverTick, sentMs, receivedMs);

        void Subscribe()
        {
            // The character server's data, before the map server's first status packet (they can come while the map loads).
            if (!_statusKnown && _flow.Context.AccountId != 0)
            {
                _statusKnown = true;
                Status.From(Selected(_flow.Context));
            }
            _subscriptions.Add(_onDamage);
            _subscriptions.Add(_onLongParameter);
            _subscriptions.Add(_onExpGain);
            _subscriptions.Add(_onAttackRange);
            _subscriptions.Add(_onTooFar);
            _subscriptions.Add(_onResurrection);
            _subscriptions.Add(_onActionFailure);
            _subscriptions.Add(_onPickupAnswer);
            _subscriptions.Add(_onStand);
            _subscriptions.Add(_onNew);
            _subscriptions.Add(_onMoveEntry);
            _subscriptions.Add(_onMove);
            _subscriptions.Add(_onOwnMove);
            _subscriptions.Add(_onStop);
            _subscriptions.Add(_onSlide);
            _subscriptions.Add(_onVanish);
            _subscriptions.Add(_onDirection);
            _subscriptions.Add(_onLook);
            _subscriptions.Add(_onAct);
            _subscriptions.Add(_onItem);
            _subscriptions.Add(_onItemFall);
            _subscriptions.Add(_onItemGone);
            _subscriptions.Add(_onParameter);
        }

        // ---- handlers ----

        void OnStand(in UNIT_STAND_ENTRY p)
        {
            if (Model == null) return;
            var look = Look(p.Job, p.HairStyle, p.Weapon, p.Shield, p.HeadBottom, p.HeadTop, p.HeadMid, p.HairColour, p.ClothColour, p.Robe, p.Sex, p.BodyStyle);
            Model.Upsert(p.UnitId, KindOf(p.ObjectType, p.CharacterId), look, p.Speed, p.Position.X, p.Position.Y, p.Position.Direction, (HeadDirection)p.HeadDirection, StateOf(p.State));
            Model.SetHp(p.UnitId, p.Hp, p.MaxHp);
        }

        void OnNew(in UNIT_NEW_ENTRY p)
        {
            if (Model == null) return;
            var look = Look(p.Job, p.HairStyle, p.Weapon, p.Shield, p.HeadBottom, p.HeadTop, p.HeadMid, p.HairColour, p.ClothColour, p.Robe, p.Sex, p.BodyStyle);
            Model.Upsert(p.UnitId, KindOf(p.ObjectType, p.CharacterId), look, p.Speed, p.Position.X, p.Position.Y, p.Position.Direction, (HeadDirection)p.HeadDirection, EntityState.Standing);
            Model.SetHp(p.UnitId, p.Hp, p.MaxHp);
        }

        void OnMoveEntry(in UNIT_MOVE_ENTRY p)
        {
            if (Model == null) return;
            var look = Look(p.Job, p.HairStyle, p.Weapon, p.Shield, p.HeadBottom, p.HeadTop, p.HeadMid, p.HairColour, p.ClothColour, p.Robe, p.Sex, p.BodyStyle);
            Model.UpsertWalking(p.UnitId, KindOf(p.ObjectType, p.CharacterId), look, p.Speed, p.Move.SourceX, p.Move.SourceY, p.Move.DestX, p.Move.DestY,
                                (HeadDirection)p.HeadDirection, p.MoveStartTime, _clock.Now);
            // From PACKETVER 20131223 a monster's HP comes in this entry, re-sent after each change (SPEC-0011 H10).
            Model.SetHp(p.UnitId, p.Hp, p.MaxHp);
        }

        void OnMove(in UNIT_MOVE p) => Model?.Walk(p.UnitId, p.Move.SourceX, p.Move.SourceY, p.Move.DestX, p.Move.DestY, p.StartTick, _clock.Now);

        void OnOwnMove(in PLAYER_MOVE p) => Model?.Walk(Model.OwnId, p.Move.SourceX, p.Move.SourceY, p.Move.DestX, p.Move.DestY, p.StartTick, _clock.Now);

        void OnStop(in UNIT_STOP_MOVE p) => Model?.Stop(p.UnitId, p.X, p.Y);

        void OnSlide(in UNIT_SLIDE p) => Model?.Slide(p.UnitId, p.X, p.Y);

        void OnVanish(in UNIT_VANISH p)
        {
            if (Model == null) return;
            var reason = p.Type <= (byte)VanishReason.FeignedDeath ? (VanishReason)p.Type : VanishReason.OutOfSight;
            if (p.UnitId == Model.OwnId && reason == VanishReason.Died) Combat.Cancel(); // SPEC-0011 FR-8
            Model.Vanish(p.UnitId, reason, _clock.Now);
        }

        void OnDirection(in UNIT_DIRECTION p) =>
            Model?.Turn(p.UnitId, (UninarokDirection)p.Direction, p.HeadDirection <= 2 ? (HeadDirection)p.HeadDirection : HeadDirection.Straight);

        void OnLook(in UNIT_LOOK_CHANGE p) => Model?.SetLook(p.UnitId, (LookType)p.Type, unchecked((int)p.Value), unchecked((int)p.Value2));

        // Sitting and standing; the other actions are the combat spec's.
        void OnAct(in UNIT_ACT p)
        {
            if (p.Action == ActSit) Model?.SetState(p.UnitId, EntityState.Sitting);
            else if (p.Action == ActStand) Model?.SetState(p.UnitId, EntityState.Standing);
        }

        void OnItem(in ITEM_ENTRY p) => Model?.AddItem(p.ItemObjectId, (int)p.ItemId, p.Identified, p.X, p.Y, p.SubX, p.SubY, p.Amount);

        void OnItemFall(in ITEM_FALL_ENTRY p) => Model?.AddItem(p.ItemObjectId, (int)p.ItemId, p.Identified, p.X, p.Y, p.SubX, p.SubY, p.Amount, true, _clock.Now);

        void OnItemGone(in ITEM_DISAPPEAR p) => Model?.RemoveItem(p.ItemObjectId);

        void OnParameter(in PARAMETER_CHANGE p)
        {
            if (p.VarId == VarSpeed)
            {
                _ownSpeed = (short)Math.Clamp(p.Value, 1, short.MaxValue); // also before the map is loaded
                Model?.SetSpeed(Model.OwnId, _ownSpeed);
                return;
            }
            // HP while dead is the restart HP (display_fake_hp_when_dead): it never stands the character up (SPEC-0011 FR-1).
            if (Status.SetParameter(p.VarId, p.Value) && (p.VarId == OwnStatus.VarHp || p.VarId == OwnStatus.VarMaxHp)) SetOwnHp();
        }

        // ---- combat (SPEC-0011, PLAN-0011 4.3) ----

        void OnDamage(in DAMAGE p)
        {
            if (Model == null) return;
            var blow = Blow.From(p, _clock.Now);
            Model.Strike(blow);
            Combat.OnBlow(blow, blow.At);
            Raise(blow);
        }

        void OnLongParameter(in LONG_PARAMETER_CHANGE p) => Status.SetLongParameter(p.VarId, p.Value);

        void OnExpGain(in EXP_GAIN p) => RaiseExp(p.VarId, p.Amount);

        void OnAttackRange(in ATTACK_RANGE p) => Status.SetAttackRange(p.Range);

        void OnTooFar(in ATTACK_TOO_FAR p)
        {
            Status.SetAttackRange(p.Range);
            Combat.OnTooFar(p, _clock.Now);
        }

        void OnActionFailure(in ACTION_FAILURE p) => Combat.OnActionFailure(p.Type);

        void OnPickupAnswer(in ITEM_PICKUP_ACK p) => Combat.OnPickupAnswer(p.Result, _clock.Now);

        void OnResurrection(in RESURRECTION p) => Model?.SetState(p.UnitId, EntityState.Standing);

        // ---- wire to model ----

        // UNIT_MOVE_ENTRY.md: a monster or pet that looks like a player class is sent as a player without a character id.
        internal static EntityKind KindOf(byte objectType, uint characterId)
        {
            switch (objectType)
            {
                case 0: return characterId != 0 ? EntityKind.Player : EntityKind.Monster;
                case 1: return EntityKind.Npc;
                case 5: return EntityKind.Monster;
                case 6: return EntityKind.Npc;
                case 7: return EntityKind.Pet;
                case 8: return EntityKind.Homunculus;
                case 9: return EntityKind.Mercenary;
                case 10: return EntityKind.Elemental;
                default: return EntityKind.Other;
            }
        }

        static EntityState StateOf(byte state) => state == 1 ? EntityState.Dead : state == 2 ? EntityState.Sitting : EntityState.Standing;

        static EntityLook Look(short job, ushort hair, uint weapon, uint shield, ushort bottom, ushort top, ushort mid,
                               short hairColour, short clothColour, ushort robe, byte sex, ushort body) => new EntityLook
        {
            Job = job,
            HairStyle = hair,
            Weapon = unchecked((int)weapon),
            Shield = unchecked((int)shield),
            HeadBottom = bottom,
            HeadTop = top,
            HeadMid = mid,
            HairColour = unchecked((ushort)hairColour),
            ClothColour = unchecked((ushort)clothColour),
            Robe = robe,
            Sex = sex,
            BodyStyle = body,
        };
    }
}
