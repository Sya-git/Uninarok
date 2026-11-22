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
            if (e.Walking)
            {
                e.Walking = false;
                e.AnimationVersion++;
            }
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
            Walk(slot, sx, sy, dx, dy, startTick, now);
            return slot;
        }

        /// <summary>A unit walks from a cell to another, along the client's path, since a server tick (FR-6); also in the past (FR-4).</summary>
        public void Walk(uint id, int sx, int sy, int dx, int dy, uint startTick, uint now)
        {
            if (_slots.TryGetValue(id, out int slot)) Walk(slot, sx, sy, dx, dy, startTick, now);
            else NetLog.Trace("walk of unknown unit " + id);
        }

        void Walk(int slot, int sx, int sy, int dx, int dy, uint startTick, uint now)
        {
            ref var e = ref _entities[slot];
            ref var path = ref _paths[slot];
            e.Version++;
            if (e.Kind == EntityKind.Player) e.Head = HeadDirection.Straight; // as the server (unit_walk_toxy_sub)
            if (e.State != EntityState.Standing) SetState(ref e, EntityState.Standing);

            var result = _pathfinder.SearchForUnit(e.Kind, sx, sy, dx, dy, ref path);
            if (result != PathResult.Found)
            {
                // The server got there, so the client must too.
                if (result != PathResult.SameCell) NetLog.Trace($"no path for unit {e.Id} from {sx},{sy} to {dx},{dy}: {result}");
                if (e.Walking)
                {
                    e.Walking = false;
                    e.AnimationVersion++;
                }
                Place(ref e, dx, dy);
                return;
            }

            // The first step starts from where the unit is shown, unless that is a real desync (D7).
            bool near = Math.Abs(e.PosX - sx) <= 1f && Math.Abs(e.PosY - sy) <= 1f;
            if (!near)
            {
                e.PosX = sx;
                e.PosY = sy;
            }
            e.FromX = e.PosX;
            e.FromY = e.PosY;
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
        }

        /// <summary>A unit stops on a cell (FR-7).</summary>
        public void Stop(uint id, int x, int y) => throw new NotImplementedException();

        /// <summary>A unit moves to a cell at once, without walking, and keeps its facing (FR-7).</summary>
        public void Slide(uint id, int x, int y) => throw new NotImplementedException();

        public void Turn(uint id, UninarokDirection body, HeadDirection head) => throw new NotImplementedException();

        /// <summary>One part of a look changed (FR-8); unknown types are ignored.</summary>
        public void SetLook(uint id, LookType type, int value, int value2) => throw new NotImplementedException();

        public void SetState(uint id, EntityState state) => throw new NotImplementedException();

        public void SetSpeed(uint id, short speed) => throw new NotImplementedException();

        /// <summary>An item on the ground, at its cell and sub-cell (FR-16).</summary>
        public void AddItem(uint id, int itemId, bool identified, int x, int y, byte subX, byte subY, int amount) => throw new NotImplementedException();

        /// <summary>An item leaves the ground (FR-5). An unknown id is ignored.</summary>
        public void RemoveItem(uint id) => throw new NotImplementedException();

        /// <summary>A unit disappears, by reason (FR-5). An unknown id is ignored.</summary>
        public void Vanish(uint id, VanishReason reason, uint now) => throw new NotImplementedException();

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
                else if (e.Removing && ServerClock.Diff(now, e.RemoveAt) >= 0) Remove(i);
            }
        }

        // ---- the rules ----

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
                e.FromX = e.CellX;
                e.FromY = e.CellY;
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
            float t = passed <= 0 || duration <= 0 ? 0f : (float)passed / duration;
            int direction = path.Step(e.StepIndex);
            e.PosX = e.FromX + (e.CellX + HerculesPathfinder.Dx(direction) - e.FromX) * t;
            e.PosY = e.FromY + (e.CellY + HerculesPathfinder.Dy(direction) - e.FromY) * t;
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
            e = new Entity { Id = id, Generation = generation, Version = version + 1, AnimationVersion = animation + 1 };
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
                }
                return h;
            }
        }
    }
}
