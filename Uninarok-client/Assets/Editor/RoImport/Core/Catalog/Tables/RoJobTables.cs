using System.Collections.Generic;

namespace Uninarok.RoImport.Core.Catalog.Tables
{
    /// <summary>
    /// The job tables (PLAN-0008 4.2; sprite-catalog.md §1): <c>jobtbl</c> (<c>npcidentity.lub</c>, constant → id of NPCs,
    /// monsters, homunculi and mercenaries), <c>JTtbl</c> (<c>jobidentity.lub</c>, constant → player class id) and
    /// <c>JobNameTable</c> (<c>jobname.lub</c>, job id → sprite base name). Read in dependency order: the name table refers
    /// to the constants of the first two (FR-1, FR-2).
    /// </summary>
    public sealed class RoJobTables
    {
        public const string NpcIdentityFile = "npcidentity.lub";
        public const string JobIdentityFile = "jobidentity.lub";
        public const string JobNameFile = "jobname.lub";

        RoJobTables(Dictionary<string, int> npc, Dictionary<string, int> player, Dictionary<int, string> names)
        {
            NpcConstants = npc;
            PlayerConstants = player;
            SpriteNames = names;
        }

        /// <summary><c>jobtbl</c>: <c>JT_PORING</c> → 1002.</summary>
        public IReadOnlyDictionary<string, int> NpcConstants { get; }

        /// <summary><c>JTtbl</c>: <c>JT_KNIGHT</c> → 7.</summary>
        public IReadOnlyDictionary<string, int> PlayerConstants { get; }

        /// <summary><c>JobNameTable</c>: 1002 → <c>PORING</c>.</summary>
        public IReadOnlyDictionary<int, string> SpriteNames { get; }

        public bool TryGetSpriteName(int jobId, out string name) => SpriteNames.TryGetValue(jobId, out name);

        /// <summary>Reads the three decompiled texts in dependency order. False with a reason naming the file on any failure.</summary>
        public static bool TryRead(string npcIdentity, string jobIdentity, string jobName, out RoJobTables tables, out string reason)
        {
            tables = null;
            if (!RoLuaTables.TryRead(npcIdentity, NpcIdentityFile, null, null, out var npc, out reason)) return false;
            if (!RoLuaTables.TryTable(npc, "jobtbl", NpcIdentityFile, out var jobtbl, out reason)) return false;
            if (!RoLuaTables.TryRead(jobIdentity, JobIdentityFile, null, null, out var player, out reason)) return false;
            if (!RoLuaTables.TryTable(player, "JTtbl", JobIdentityFile, out var jttbl, out reason)) return false;
            if (!RoLuaTables.TryRead(jobName, JobNameFile, null, RoLuaTables.Imports(npc, player), out var names, out reason)) return false;
            if (!RoLuaTables.TryTable(names, "JobNameTable", JobNameFile, out var nameTable, out reason)) return false;

            tables = new RoJobTables(RoLuaTables.Constants(jobtbl), RoLuaTables.Constants(jttbl), RoLuaTables.Names(nameTable));
            return true;
        }
    }
}
