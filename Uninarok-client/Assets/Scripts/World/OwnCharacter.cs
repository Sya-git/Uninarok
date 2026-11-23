using System;
using Uninarok.Networking.Client;
using Uninarok.Networking.Data;
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
        /// <remarks>
        /// The player's walk ends an attack or a pickup (SPEC-0011 FR-2, PLAN-0011 4.5): a sent attack request is stopped first, and
        /// the server runs the walk when its walk delay ends (H5). A click that cannot walk changes nothing.
        /// </remarks>
        public WalkRequestResult RequestWalk(int x, int y)
        {
            var result = CheckWalk(x, y);
            if (result != WalkRequestResult.Sent) return result;
            var combat = _session.Combat;
            if (combat.State != CombatState.Idle && combat.AttackRequested) _client.Send(new STOP_ATTACK());
            combat.Cancel();
            return _client.Send(new REQUEST_MOVE((ushort)x, (ushort)y)) ? WalkRequestResult.Sent : WalkRequestResult.NotInGame;
        }

        /// <summary>A walk asked for by the combat rules (SPEC-0011 FR-3, FR-10): it does not end the attack.</summary>
        internal WalkRequestResult WalkFor(int x, int y)
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

        /// <summary>It walks, and to this cell.</summary>
        public bool IsWalkingTo(int x, int y)
        {
            if (!TryGet(out var model, out int slot) || !model[slot].Walking) return false;
            ref readonly var path = ref model.PathOf(slot);
            int endX = path.StartX, endY = path.StartY;
            for (int i = 0; i < path.Length; i++)
            {
                endX += HerculesPathfinder.Dx(path.Step(i));
                endY += HerculesPathfinder.Dy(path.Step(i));
            }
            return x == endX && y == endY;
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

        // ---- turn (FR-13): the server does not echo a turn to who turned, so it is applied when it was sent (D9) ----

        /// <summary>
        /// Shift + click: the head ends in the direction of the cell. One step beside the body's, the head turns alone, or the body
        /// when the head is turned there already; two or three steps, the body stops one step short of it (PLAN-0010 D31); behind,
        /// the body turns with the head straight. False when nothing was sent: not able to act, walking, or the own cell.
        /// </summary>
        public bool TurnTowards(int x, int y)
        {
            if (!TryTurning(out var model, out int slot)) return false;
            ref readonly var own = ref model[slot];
            if (x == own.CellX && y == own.CellY) return false;
            int target = DirectionTo(own.CellX, own.CellY, x, y), body = (int)own.Facing;
            int step = (target - body) & 7; // counter-clockwise
            if (step == 0 || step == 4) return Turn(model, (UninarokDirection)target, HeadDirection.Straight);
            var head = step < 4 ? HeadDirection.Left : HeadDirection.Right;
            if ((step == 1 || step == 7) && own.Head == head) return Turn(model, (UninarokDirection)target, HeadDirection.Straight);
            return Turn(model, (UninarokDirection)((target + (step < 4 ? 7 : 1)) & 7), head);
        }

        /// <summary><c>/doridori</c>: the head to the left, or to the right when it is on the left (D32); it stays there.</summary>
        public bool Doridori() =>
            TryTurning(out var model, out int slot) && Turn(model, model[slot].Facing, model[slot].Head == HeadDirection.Left ? HeadDirection.Right : HeadDirection.Left);

        /// <summary><c>/bangbang</c>: the body one step clockwise, the head straight.</summary>
        public bool Bangbang() =>
            TryTurning(out var model, out int slot) && Turn(model, (UninarokDirection)(((int)model[slot].Facing + 7) & 7), HeadDirection.Straight);

        bool TryTurning(out WorldModel model, out int slot) =>
            TryGet(out model, out slot) && model[slot].State != EntityState.Dead && !model[slot].Walking;

        bool Turn(WorldModel model, UninarokDirection body, HeadDirection head)
        {
            if (!_client.Send(new CHANGE_DIRECTION((ushort)head, body))) return false;
            model.Turn(model.OwnId, body, head);
            return true;
        }

        /// <summary>The direction of a cell seen from another (<c>map_calc_dir</c>); the cells differ.</summary>
        internal static int DirectionTo(int x0, int y0, int x1, int y1)
        {
            int dx = x1 - x0, dy = y1 - y0;
            if (dx >= 0 && dy >= 0)
            {
                if (dx * 2 < dy || dx == 0) return 0;
                return dx > dy * 2 + 1 || dy == 0 ? 6 : 7;
            }
            if (dx >= 0 && dy <= 0)
            {
                if (dx * 2 < -dy || dx == 0) return 4;
                return dx > -dy * 2 + 1 || dy == 0 ? 6 : 5;
            }
            if (dx <= 0 && dy <= 0)
            {
                if (dx * 2 > dy || dx == 0) return 4;
                return dx < dy * 2 + 1 || dy == 0 ? 2 : 3;
            }
            if (-dx * 2 < dy || dx == 0) return 0;
            return -dx > dy * 2 + 1 || dy == 0 ? 2 : 1;
        }

        // ---- drop (FR-20): the server puts the item on the ground and takes it out of the inventory, or ignores it ----

        /// <summary>Asks to drop items of an inventory slot (the server's index, slot + 2). False when nothing was sent.</summary>
        public bool DropItem(ushort inventoryIndex, ushort amount) =>
            amount > 0 && CanAct && _client.Send(new DROP_ITEM(inventoryIndex, amount));

        /// <summary>Sends a command of the server (<c>@item 501</c>) as the chat line it is; the line sent, null when it was not.</summary>
        public string Command(string command)
        {
            if (!TryGet(out _, out _)) return null;
            foreach (var c in _flow.Context.Characters)
            {
                if (c.CharId != _flow.Context.CharacterId) continue;
                var chat = new GLOBAL_CHAT(c.Name.ToString(), command);
                return _client.Send(chat) ? chat.Text : null;
            }
            return null;
        }

        // ---- sit (FR-19): the state changes on the server's answer, which a refusal never gives (D9) ----

        /// <summary>Asks to stand when sitting, else to sit. False when nothing was sent.</summary>
        public bool ToggleSit() => TryGet(out var model, out int slot) && (model[slot].State == EntityState.Sitting ? Stand() : Sit());

        /// <summary>Asks to sit; nothing while sitting already.</summary>
        public bool Sit()
        {
            if (!Act(EntityState.Standing, REQUEST_ACT.Sit)) return false;
            _session.Combat.Cancel(); // the server stops the attack (clif_parse_ActionRequest)
            return true;
        }

        /// <summary>Asks to stand up; nothing while standing already.</summary>
        public bool Stand() => Act(EntityState.Sitting, REQUEST_ACT.Stand);

        bool Act(EntityState from, byte action) =>
            TryGet(out var model, out int slot) && model[slot].State == from && _client.Send(new REQUEST_ACT(0, action));
    }
}
