using System;
using Uninarok.Networking.Client;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Packets.Out.Map;

namespace Uninarok.World
{
    /// <summary>What became of a walk request (PLAN-0010 4.6); only <see cref="Sent"/> sent a packet.</summary>
    public enum WalkRequestResult : byte
    {
        Sent,
        Dead,
        Sitting,
        /// <summary>The cell is not walkable.</summary>
        Blocked,
        NoPath,
        TooLong,
        /// <summary>A one-cell walk onto a unit's cell, which the server ignores.</summary>
        Occupied,
        SameCell,
        NotInGame,
    }

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
        WalkPath _path;

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

        // ---- walk (FR-9) ----

        /// <summary>Asks the server to walk to a cell when it would accept; the walk starts when it answers.</summary>
        public WalkRequestResult RequestWalk(int x, int y)
        {
            var result = CheckWalk(x, y);
            if (result != WalkRequestResult.Sent) return result;
            return _client.Send(new REQUEST_MOVE((ushort)x, (ushort)y)) ? WalkRequestResult.Sent : WalkRequestResult.NotInGame;
        }

        /// <summary>What <see cref="RequestWalk"/> would do, without sending: <see cref="WalkRequestResult.Sent"/> when it would send.</summary>
        public WalkRequestResult CheckWalk(int x, int y)
        {
            if (!TryGet(out var model, out int slot)) return WalkRequestResult.NotInGame;
            ref readonly var own = ref model[slot];
            if (own.State == EntityState.Dead) return WalkRequestResult.Dead;
            if (own.State == EntityState.Sitting) return WalkRequestResult.Sitting;

            // The server's path starts on its cell of the character, also in the middle of a walk.
            switch (model.Pathfinder.SearchWalk(own.CellX, own.CellY, x, y, ref _path, _session.Settings.WalkRules))
            {
                case PathResult.SameCell: return WalkRequestResult.SameCell;
                case PathResult.Blocked: return WalkRequestResult.Blocked;
                case PathResult.NoPath: return WalkRequestResult.NoPath;
                case PathResult.TooLong: return WalkRequestResult.TooLong;
            }

            int limit = _session.Settings.CellStackLimit;
            if (limit > 0 && Math.Abs(x - own.CellX) <= 1 && Math.Abs(y - own.CellY) <= 1 && CountOnCell(model, x, y) >= limit)
                return WalkRequestResult.Occupied;
            return WalkRequestResult.Sent;
        }

        // map_count_oncell(BL_CHAR | BL_NPC, 0x1 | 0x2): who stands on the cell. Pets and items do not count, nor hidden warps.
        static int CountOnCell(WorldModel model, int x, int y)
        {
            const short FakeNpc = -1, HiddenWarp = 139;
            int count = 0;
            for (int i = 0; i < model.SlotLimit; i++)
            {
                ref readonly var e = ref model[i];
                if (e.Id == 0 || e.CellX != x || e.CellY != y || e.Walking) continue;
                switch (e.Kind)
                {
                    case EntityKind.Player:
                    case EntityKind.Homunculus:
                    case EntityKind.Mercenary:
                    case EntityKind.Elemental:
                        count++;
                        break;
                    case EntityKind.Monster:
                        if (e.State != EntityState.Dead) count++; // a dead one is off the server's map
                        break;
                    case EntityKind.Npc:
                        if (e.Look.Job != FakeNpc && e.Look.Job != HiddenWarp) count++;
                        break;
                }
            }
            return count;
        }
    }
}
