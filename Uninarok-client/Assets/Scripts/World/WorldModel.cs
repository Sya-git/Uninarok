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
                          UninarokDirection dir, HeadDirection head, EntityState state) => throw new NotImplementedException();

        /// <summary>
        /// A unit that walks comes into view, or a known one gets another speed: as <see cref="Upsert"/> and <see cref="Walk"/>, but a
        /// known unit goes on from where it is shown.
        /// </summary>
        public int UpsertWalking(uint id, EntityKind kind, in EntityLook look, short speed, int sx, int sy, int dx, int dy,
                                 HeadDirection head, uint startTick, uint now) => throw new NotImplementedException();

        /// <summary>A unit walks from a cell to another, along the client's path, since a server tick (FR-6); also in the past (FR-4).</summary>
        public void Walk(uint id, int sx, int sy, int dx, int dy, uint startTick, uint now) => throw new NotImplementedException();

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
        public void Clear() => throw new NotImplementedException();

        /// <summary>Moves every walking unit to where it is at this server tick and removes dead units whose time is over.</summary>
        public void Advance(uint now) => throw new NotImplementedException();

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
