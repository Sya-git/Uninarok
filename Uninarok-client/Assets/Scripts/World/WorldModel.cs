using System;
using System.Collections.Generic;
using Uninarok.Networking.Client;
using Uninarok.Networking.Data;

namespace Uninarok.World
{
    /// <summary>
    /// Every known entity of one map session, by server id (SPEC-0010 FR-3; PLAN-0010 4.5, D1): the only place their state lives.
    /// <c>WorldSession</c> writes it, views read it each frame by version numbers; it works with nothing drawn. All storage is
    /// allocated once: no operation and no <see cref="Advance"/> allocates. Main thread.
    /// </summary>
    public sealed class WorldModel
    {
        /// <summary>A unit shown farther than this from where a new walk has it is placed there (PLAN-0010 D7, D36), in cells.</summary>
        const float MaxOffset = 1.5f;

        readonly Entity[] _entities;
        readonly WalkPath[] _paths;
        readonly int[] _free;
        readonly Dictionary<uint, int> _slots;
        readonly HerculesPathfinder _pathfinder;
        readonly WorldSettings _settings;
        int _freeCount;
        bool _fullReported;

        public WorldModel(IWalkGrid grid, WorldSettings settings, int capacity = 2048)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _pathfinder = new HerculesPathfinder(grid);
            _entities = new Entity[capacity];
            _paths = new WalkPath[capacity];
            _free = new int[capacity];
            _slots = new Dictionary<uint, int>(capacity);
            for (int i = 0; i < capacity; i++) _free[i] = capacity - 1 - i; // low slots first
            _freeCount = capacity;
        }

        public IWalkGrid Grid { get; }
        public HerculesPathfinder Pathfinder => _pathfinder;

        /// <summary>The own character's id; 0 when it is not in the world.</summary>
        public uint OwnId { get; internal set; }

        /// <summary>Slots there are.</summary>
        public int Capacity => _entities.Length;

        /// <summary>Entities in the world.</summary>
        public int Count => _slots.Count;

        /// <summary>Every used slot is below this.</summary>
        public int SlotLimit { get; private set; }

        public bool IsUsed(int slot) => _entities[slot].Id != 0;

        public ref readonly Entity this[int slot] => ref _entities[slot];

        public bool TryGetSlot(uint id, out int slot) => _slots.TryGetValue(id, out slot);

        /// <summary>The path of a walking entity.</summary>
        public ref readonly WalkPath PathOf(int slot) => ref _paths[slot];

        /// <summary>A unit disappeared, with its reason and place; raised once per vanish, before the removal (FR-5).</summary>
        public event Action<VanishInfo> Vanished;

        // ---- operations ----

        /// <summary>
        /// A unit stands on a cell: creates it, or updates it in place when the id is known; never two slots for one id (FR-4).
        /// Returns its slot, −1 when the model is full.
        /// </summary>
        public int Upsert(uint id, EntityKind kind, in EntityLook look, short speed, int x, int y,
                          UninarokDirection dir, HeadDirection head, EntityState state)
        {
            int slot = Update(id, kind, look, speed, head, state, out _);
            if (slot < 0) return -1;
            ref var e = ref _entities[slot];
            EndWalk(ref e);
            e.Facing = Wrap(dir);
            Place(ref e, x, y);
            return slot;
        }

        /// <summary>
        /// A unit that walks comes into view, or a known one gets another speed: as <see cref="Upsert"/> and <see cref="Walk"/>, but a
        /// known unit goes on from where it is shown.
        /// </summary>
        public int UpsertWalking(uint id, EntityKind kind, in EntityLook look, short speed, int sx, int sy, int dx, int dy,
                                 HeadDirection head, uint startTick, uint now)
        {
            int slot = Update(id, kind, look, speed, head, EntityState.Standing, out bool created);
            if (slot < 0) return -1;
            if (created) Place(ref _entities[slot], sx, sy);
            Walk(slot, sx, sy, dx, dy, startTick, now, created);
            return slot;
        }

        /// <summary>A unit walks from a cell to another, along the client's path, since a server tick (FR-6); also in the past (FR-4).</summary>
        public void Walk(uint id, int sx, int sy, int dx, int dy, uint startTick, uint now)
        {
            if (_slots.TryGetValue(id, out int slot)) Walk(slot, sx, sy, dx, dy, startTick, now, false);
            else NetLog.Trace("walk of unknown unit " + id);
        }

        void Walk(int slot, int sx, int sy, int dx, int dy, uint startTick, uint now, bool unseen)
        {
            ref var e = ref _entities[slot];
            ref var path = ref _paths[slot];
            bool walked = e.Walking;
            if (walked)
            {
                // Where it would be shown now; the last frame's place would hold it for a frame. The new walk goes on with the old one's
                // animation, also when that was just over.
                int animation = e.AnimationVersion;
                Step(slot, now);
                e.Walking = true;
                e.AnimationVersion = animation;
            }
            float shownX = e.PosX, shownY = e.PosY;
            e.Version++;
            if (e.Kind == EntityKind.Player) e.Head = HeadDirection.Straight; // as the server (unit_walk_toxy_sub)
            if (e.State != EntityState.Standing) SetState(ref e, EntityState.Standing);

            var result = _pathfinder.SearchForUnit(e.Kind, sx, sy, dx, dy, ref path);
            if (result != PathResult.Found)
            {
                // The server got there, so the client must too.
                if (result != PathResult.SameCell) NetLog.Trace($"no path for unit {e.Id} from {sx},{sy} to {dx},{dy}: {result}");
                EndWalk(ref e);
                Place(ref e, dx, dy);
                return;
            }

            e.RunsIn = walked;
            e.OffX = e.OffY = 0f;
            e.CellX = sx;
            e.CellY = sy;
            e.StepIndex = 0;
            e.StepStarts = startTick;
            e.StepEnds = unchecked(startTick + (uint)StepMs(e, path.Step(0)));
            e.Facing = (UninarokDirection)path.Step(0);
            if (!e.Walking)
            {
                e.Walking = true;
                e.AnimationVersion++;
            }
            Step(slot, now);

            // It goes on from where it is shown and gets to where the walk has it little by little (D36), unless that is a real
            // desync (D7). The server's steps take longer than the walk speed says, so the client is ahead at each new order.
            if (unseen || !e.Walking) return;
            float offX = shownX - e.PosX, offY = shownY - e.PosY;
            if (Math.Abs(offX) > MaxOffset || Math.Abs(offY) > MaxOffset) return;
            uint ends = e.StepEnds;
            for (int i = e.StepIndex + 1; i < path.Length; i++) ends = unchecked(ends + (uint)StepMs(e, path.Step(i)));
            int left = Math.Min(_settings.WalkCorrectionMs, ServerClock.Diff(ends, now));
            if (left <= 0) return;
            e.OffX = offX;
            e.OffY = offY;
            e.OffStarts = now;
            e.OffEnds = unchecked(now + (uint)left);
            e.PosX = shownX;
            e.PosY = shownY;
        }

        /// <summary>A unit stops on a cell (FR-7).</summary>
        public void Stop(uint id, int x, int y)
        {
            if (!_slots.TryGetValue(id, out int slot)) return;
            ref var e = ref _entities[slot];
            EndWalk(ref e);
            Place(ref e, x, y);
            e.Version++;
        }

        /// <summary>A unit moves to a cell at once, without walking, and keeps its facing (FR-7).</summary>
        public void Slide(uint id, int x, int y) => Stop(id, x, y);

        /// <summary>A unit turned its body and head (FR-8).</summary>
        public void Turn(uint id, UninarokDirection body, HeadDirection head)
        {
            if (!_slots.TryGetValue(id, out int slot)) return;
            ref var e = ref _entities[slot];
            body = Wrap(body);
            head = Valid(head);
            if (e.Facing == body && e.Head == head) return;
            e.Facing = body;
            e.Head = head;
            e.Version++;
        }

        /// <summary>One part of a look changed (FR-8); unknown types are ignored.</summary>
        public void SetLook(uint id, LookType type, int value, int value2)
        {
            if (!_slots.TryGetValue(id, out int slot)) return;
            ref var e = ref _entities[slot];
            var look = e.Look;
            switch (type)
            {
                case LookType.Base: look.Job = unchecked((short)value); break;
                case LookType.Hair: look.HairStyle = unchecked((ushort)value); break;
                case LookType.Weapon:
                    look.Weapon = value;
                    look.Shield = value2;
                    break;
                case LookType.HeadBottom: look.HeadBottom = unchecked((ushort)value); break;
                case LookType.HeadTop: look.HeadTop = unchecked((ushort)value); break;
                case LookType.HeadMid: look.HeadMid = unchecked((ushort)value); break;
                case LookType.HairColour: look.HairColour = unchecked((ushort)value); break;
                case LookType.ClothColour: look.ClothColour = unchecked((ushort)value); break;
                case LookType.Shield: look.Shield = value; break;
                case LookType.Robe: look.Robe = unchecked((ushort)value); break;
                case LookType.BodyStyle: look.BodyStyle = unchecked((ushort)value); break;
                default:
                    NetLog.Trace($"look change of unknown type {(int)type} for unit {id}");
                    return;
            }
            if (look.Equals(e.Look)) return;
            e.Look = look;
            e.Version++;
        }

        /// <summary>A unit sits, stands or lies dead (FR-8, FR-19); it stops walking unless it stands.</summary>
        public void SetState(uint id, EntityState state)
        {
            if (!_slots.TryGetValue(id, out int slot)) return;
            ref var e = ref _entities[slot];
            if (state != EntityState.Standing) EndWalk(ref e);
            SetState(ref e, state);
        }

        /// <summary>A walk speed in ms per straight cell; it counts from the next step, as on the server.</summary>
        public void SetSpeed(uint id, short speed)
        {
            if (!_slots.TryGetValue(id, out int slot) || _entities[slot].Speed == speed) return;
            _entities[slot].Speed = speed;
            _entities[slot].Version++;
        }

        // ---- combat (SPEC-0011, PLAN-0011 4.2) ----

        /// <summary>HP and max HP from an entry or a status packet; a negative value: not known. Never kills (FR-1).</summary>
        public void SetHp(uint id, int hp, int maxHp)
        {
            if (!_slots.TryGetValue(id, out int slot)) return;
            ref var e = ref _entities[slot];
            if (hp < 0 || maxHp < 0) hp = maxHp = -1;
            if (e.Hp == hp && e.MaxHp == maxHp) return;
            e.Hp = hp;
            e.MaxHp = maxHp;
            e.Version++;
        }

        /// <summary>A blow: the attacker's target, last blow and facing (FR-4, FR-9). It moves nobody and stops no walk.</summary>
        public void Strike(in Blow blow)
        {
            if (!_slots.TryGetValue(blow.AttackerId, out int slot)) return; // nothing of the target changes in the model
            ref var e = ref _entities[slot];
            e.TargetId = blow.TargetId;
            e.LastBlow = blow;
            if (blow.TargetId != blow.AttackerId && _slots.TryGetValue(blow.TargetId, out int target))
            {
                ref readonly var t = ref _entities[target];
                if (t.CellX != e.CellX || t.CellY != e.CellY)
                {
                    e.Facing = (UninarokDirection)OwnCharacter.DirectionTo(e.CellX, e.CellY, t.CellX, t.CellY);
                    e.Head = HeadDirection.Straight;
                }
            }
            e.Version++;
        }

        /// <summary>Everyone who attacks this unit attacks nobody now (FR-9).</summary>
        public void ClearTargets(uint targetId)
        {
            if (targetId == 0) return;
            for (int i = 0; i < SlotLimit; i++)
            {
                ref var e = ref _entities[i];
                if (e.Id == 0 || e.TargetId != targetId) continue;
                e.TargetId = 0;
                e.Version++;
            }
        }

        /// <summary>An item on the ground, at its cell and sub-cell (FR-16).</summary>
        /// <param name="falls">It was dropped now: it goes up a little and falls to the ground (D37).</param>
        public void AddItem(uint id, int itemId, bool identified, int x, int y, byte subX, byte subY, int amount, bool falls = false, uint now = 0)
        {
            int slot = Update(id, EntityKind.Item, default, 0, HeadDirection.Straight, EntityState.Standing, out _);
            if (slot < 0) return;
            ref var e = ref _entities[slot];
            e.Walking = false;
            e.ItemId = itemId;
            e.ItemAmount = amount;
            e.Identified = identified;
            Place(ref e, x, y);
            // Twelfths from the cell's south-west corner (ITEM_FALL_ENTRY.md); the centre is the whole number.
            e.PosX = x - 0.5f + subX / 12f;
            e.PosY = y - 0.5f + subY / 12f;
            e.Falling = falls && _settings.ItemFallHeight > 0f;
            e.FallStarts = now;
            e.Height = e.Falling ? _settings.ItemFallHeight : 0f;
        }

        /// <summary>An item leaves the ground (FR-5). An unknown id is ignored.</summary>
        public void RemoveItem(uint id)
        {
            if (_slots.TryGetValue(id, out int slot) && _entities[slot].IsItem) Remove(slot);
        }

        /// <summary>A unit disappears, by reason (FR-5). An unknown id is ignored.</summary>
        public void Vanish(uint id, VanishReason reason, uint now)
        {
            if (!_slots.TryGetValue(id, out int slot))
            {
                NetLog.Trace("vanish of unknown unit " + id);
                return;
            }
            bool dies = reason == VanishReason.Died, feigns = reason == VanishReason.FeignedDeath;
            if (id == OwnId && !dies && !feigns) return; // a map change moves the own character, nothing removes it

            ref var e = ref _entities[slot];
            if (dies || feigns)
            {
                EndWalk(ref e);
                SetState(ref e, EntityState.Dead);
                e.Feigning = feigns;
                if (e.TargetId != 0)
                {
                    e.TargetId = 0; // the dead attack nobody (SPEC-0011 FR-9)
                    e.Version++;
                }
                // A player lies until the server says otherwise; anything else is removed after a while (D8).
                if (dies && e.Kind != EntityKind.Player && id != OwnId)
                {
                    e.Removing = true;
                    e.RemoveAt = unchecked(now + (uint)Math.Max(0, _settings.DeadMonsterLingerMs));
                }
            }
            ClearTargets(id); // nobody attacks a unit that died or left (SPEC-0011 FR-9)
            Vanished?.Invoke(new VanishInfo(id, e.Kind, reason, e.CellX, e.CellY));

            // A listener may have changed the model: look the unit up again.
            if (!dies && !feigns && _slots.TryGetValue(id, out slot)) Remove(slot);
        }

        /// <summary>Empties the world: map change, disconnect (FR-3).</summary>
        public void Clear()
        {
            for (int i = SlotLimit - 1; i >= 0; i--)
                if (_entities[i].Id != 0) Remove(i);
            OwnId = 0;
        }

        /// <summary>Moves every walking unit to where it is at this server tick and removes dead units whose time is over.</summary>
        public void Advance(uint now)
        {
            for (int i = 0; i < SlotLimit; i++)
            {
                ref var e = ref _entities[i];
                if (e.Walking) Step(i, now);
                else if (e.Falling) Fall(ref e, now);
                else if (e.Removing && ServerClock.Diff(now, e.RemoveAt) >= 0) Remove(i);
            }
        }

        // ---- the rules ----

        void Fall(ref Entity e, uint now)
        {
            // Thrown up a little from above the ground, then it falls.
            float t = Math.Max(0, ServerClock.Diff(now, e.FallStarts)) / 1000f;
            float height = _settings.ItemFallHeight + (_settings.ItemRiseSpeed - 0.5f * Math.Max(0.1f, _settings.ItemFallGravity) * t) * t;
            e.Falling = height > 0f;
            e.Height = e.Falling ? height : 0f;
        }

        // Moves a walker to where it is at `now`: ends the steps that are over, then places it inside the current one.
        void Step(int slot, uint now)
        {
            ref var e = ref _entities[slot];
            ref var path = ref _paths[slot];
            while (ServerClock.Diff(now, e.StepEnds) >= 0)
            {
                int done = path.Step(e.StepIndex);
                e.CellX += HerculesPathfinder.Dx(done);
                e.CellY += HerculesPathfinder.Dy(done);
                if (++e.StepIndex >= path.Length)
                {
                    e.Walking = false;
                    e.PosX = e.CellX;
                    e.PosY = e.CellY;
                    e.Version++;
                    e.AnimationVersion++;
                    return;
                }
                byte next = path.Step(e.StepIndex);
                e.Facing = (UninarokDirection)next;
                e.StepStarts = e.StepEnds;
                e.StepEnds = unchecked(e.StepEnds + (uint)StepMs(e, next));
            }

            int duration = ServerClock.Diff(e.StepEnds, e.StepStarts);
            int passed = ServerClock.Diff(now, e.StepStarts);
            float t = duration <= 0 ? 0f : Math.Max((float)passed / duration, e.RunsIn && e.StepIndex == 0 ? -1f : 0f);
            int direction = path.Step(e.StepIndex);
            e.PosX = e.CellX + HerculesPathfinder.Dx(direction) * t;
            e.PosY = e.CellY + HerculesPathfinder.Dy(direction) * t;
            if (e.OffX == 0f && e.OffY == 0f) return;
            int rest = ServerClock.Diff(e.OffEnds, now);
            if (rest <= 0)
            {
                e.OffX = e.OffY = 0f;
                return;
            }
            float part = Math.Min(1f, (float)rest / ServerClock.Diff(e.OffEnds, e.OffStarts));
            e.PosX += e.OffX * part;
            e.PosY += e.OffY * part;
        }

        static int StepMs(in Entity e, int direction) => Math.Max(1, WalkTiming.StepMs(e.Speed, WalkTiming.IsDiagonal(direction)));

        // The fields every entry sets; a known unit is updated in place and, if it was dying, lives again.
        int Update(uint id, EntityKind kind, in EntityLook look, short speed, HeadDirection head, EntityState state, out bool created)
        {
            created = false;
            if (!_slots.TryGetValue(id, out int slot))
            {
                slot = Take(id);
                if (slot < 0) return -1;
                created = true;
            }
            ref var e = ref _entities[slot];
            e.Kind = kind;
            e.Look = look;
            e.Speed = speed;
            e.Head = Valid(head);
            e.Removing = false;
            SetState(ref e, state);
            e.Version++;
            return slot;
        }

        // A walk ends where the unit is shown; whoever ends it places the unit if the server said where.
        static void EndWalk(ref Entity e)
        {
            if (!e.Walking) return;
            e.Walking = false;
            e.Version++;
            e.AnimationVersion++;
        }

        static void Place(ref Entity e, int x, int y)
        {
            e.CellX = x;
            e.CellY = y;
            e.PosX = x;
            e.PosY = y;
        }

        static void SetState(ref Entity e, EntityState state)
        {
            if (e.State == state) return;
            e.State = state;
            e.Removing = false;
            if (state != EntityState.Dead) e.Feigning = false;
            e.Version++;
            e.AnimationVersion++;
        }

        // 8 to 15 are legal on the wire (UninarokPosition.md).
        static UninarokDirection Wrap(UninarokDirection d) => (UninarokDirection)((int)d & 7);

        static HeadDirection Valid(HeadDirection head) => head <= HeadDirection.Left ? head : HeadDirection.Straight;

        // ---- slots ----

        int Take(uint id)
        {
            if (id == 0) return -1; // no unit has it; it marks a free slot
            if (_freeCount == 0)
            {
                if (!_fullReported)
                {
                    _fullReported = true;
                    UnityEngine.Debug.LogWarning($"[World] the world model is full ({_entities.Length} entities); new ones are ignored");
                }
                return -1;
            }
            int slot = _free[--_freeCount];
            ref var e = ref _entities[slot];
            int generation = e.Generation + 1, version = e.Version, animation = e.AnimationVersion;
            e = new Entity { Id = id, Generation = generation, Version = version + 1, AnimationVersion = animation + 1, Hp = -1, MaxHp = -1 };
            _slots.Add(id, slot);
            if (slot >= SlotLimit) SlotLimit = slot + 1;
            return slot;
        }

        void Remove(int slot)
        {
            ref var e = ref _entities[slot];
            _slots.Remove(e.Id);
            e = new Entity { Generation = e.Generation + 1, Version = e.Version + 1, AnimationVersion = e.AnimationVersion + 1 };
            _free[_freeCount++] = slot;
            while (SlotLimit > 0 && _entities[SlotLimit - 1].Id == 0) SlotLimit--;
        }

        /// <summary>A number that changes with any state of the model (tests: views read only).</summary>
        internal uint Checksum()
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < SlotLimit; i++)
                {
                    ref readonly var e = ref _entities[i];
                    h = (h ^ e.Id) * 16777619u;
                    h = (h ^ (uint)e.Version) * 16777619u;
                    h = (h ^ (uint)e.AnimationVersion) * 16777619u;
                    h = (h ^ (uint)(e.CellX * 1000 + e.CellY)) * 16777619u;
                    h = (h ^ (uint)BitConverter.SingleToInt32Bits(e.PosX)) * 16777619u;
                    h = (h ^ (uint)BitConverter.SingleToInt32Bits(e.PosY)) * 16777619u;
                    h = (h ^ (uint)e.Facing) * 16777619u;
                    h = (h ^ (uint)e.Head) * 16777619u;
                    h = (h ^ (uint)e.State) * 16777619u;
                    h = (h ^ (e.Walking ? 1u : 0u)) * 16777619u;
                    h = (h ^ (uint)e.StepIndex) * 16777619u;
                    h = (h ^ (uint)e.Look.GetHashCode()) * 16777619u;
                    h = (h ^ (uint)e.Hp) * 16777619u;
                    h = (h ^ (uint)e.MaxHp) * 16777619u;
                    h = (h ^ e.TargetId) * 16777619u;
                    h = (h ^ e.LastBlow.At ^ (uint)e.LastBlow.Damage) * 16777619u;
                    h = (h ^ (e.Feigning ? 1u : 0u)) * 16777619u;
                }
                return h;
            }
        }
    }
}
