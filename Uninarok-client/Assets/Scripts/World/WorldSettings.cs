using UnityEngine;

namespace Uninarok.World
{
    /// <summary>
    /// The server's and the client's rule values of the world (PLAN-0010 3, 4.2; SPEC-0010 §6): Hercules's defaults, one setting
    /// each. <c>Settings/World/WorldSettings.asset</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Uninarok/World Settings", fileName = "WorldSettings")]
    public sealed class WorldSettings : ScriptableObject
    {
        [Header("Server rules")]
        [Tooltip("Longest walk the server accepts, in cells (max_walk_path, conf/map/battle/client.conf).")]
        [Min(1)] public int MaxWalkPath = 17;
        [Tooltip("Longest walk when no straight line exists (OFFICIAL_WALKPATH).")]
        [Min(1)] public int MaxWalkPathObstructed = 14;
        [Tooltip("Units in a cell that refuse a one-cell walk onto it (official_cell_stack_limit, conf/map/battle/misc.conf); 0 is off.")]
        [Min(0)] public int CellStackLimit = 1;
        [Tooltip("The server keeps the facing through a warp (player_warp_keep_direction, conf/map/battle/player.conf); off: it faces north.")]
        public bool WarpKeepsDirection;

        [Header("Client rules")]
        [Tooltip("How often a held walk button asks again, in ms.")]
        [Min(50)] public int HoldRepeatMs = 500;
        [Tooltip("How long a dead monster stays before it is removed, in ms (PLAN-0010 D8).")]
        [Min(0)] public int DeadMonsterLingerMs = 5000;
        [Tooltip("The walk speed (ms per cell) at which the walk animation plays at its own speed.")]
        [Min(1)] public int WalkAnimationSpeed = 150;

        /// <summary>The limits of a walk request (PLAN-0010 4.4).</summary>
        public WalkRules WalkRules => new WalkRules(MaxWalkPath, MaxWalkPathObstructed);
    }

    /// <summary>The length limits of <c>unit_walktoxy</c> (PLAN-0010 4.4).</summary>
    public readonly struct WalkRules
    {
        public readonly int MaxWalkPath, MaxWalkPathObstructed;

        public WalkRules(int maxWalkPath, int maxWalkPathObstructed)
        {
            MaxWalkPath = maxWalkPath;
            MaxWalkPathObstructed = maxWalkPathObstructed;
        }

        /// <summary>No limits: units the server does not limit (NPCs).</summary>
        public static WalkRules None => new WalkRules(int.MaxValue, int.MaxValue);
    }
}
