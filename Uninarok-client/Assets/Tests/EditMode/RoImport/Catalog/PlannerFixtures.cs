using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Uninarok.RoImport.Core.Catalog.Planning;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Source;

namespace Uninarok.RoImport.Tests.Catalog
{
    /// <summary>Synthetic tables and file lists for the planner tests (PLAN-0008 §6): nothing is read from disk.</summary>
    static class PlannerFixtures
    {
        /// <summary>A source that only lists paths: the planners never open a file.</summary>
        public sealed class ListSource : IRoSource
        {
            readonly List<RoPath> _paths;

            public ListSource(IEnumerable<string> paths) => _paths = paths.Select(RoPath.Parse).Distinct().OrderBy(p => p).ToList();

            public bool TryGetFile(RoPath path, out RoSourceFile file)
            {
                file = default;
                return false;
            }

            public IEnumerable<RoPath> Enumerate(string folderPrefix)
            {
                string prefix = RoFileNames.Prefix(folderPrefix);
                return _paths.Where(p => p.Canonical.StartsWith(prefix, StringComparison.Ordinal));
            }
        }

        /// <summary>The ACT and SPR of each stem (<c>sprite/몬스터/poring</c>).</summary>
        public static IEnumerable<string> Pairs(params string[] stems) => stems.SelectMany(s => new[] { s + ".act", s + ".spr" });

        public static RoCatalogPlan Plan(IRoCatalogPlanner planner, RoCatalogTables tables, IEnumerable<string> files)
        {
            var b = new RoCatalogPlan.Builder();
            planner.Plan(new RoCatalogContext(new ListSource(files), tables), b);
            return b.Build();
        }

        public static RoJobTables Jobs(string npcConstants, string playerConstants, string names)
        {
            Assert.That(RoJobTables.TryRead($"jobtbl = {{ {npcConstants} }}", $"JTtbl = {{ {playerConstants} }}", $"JobNameTable = {{ {names} }}",
                out var jobs, out string reason), Is.True, reason);
            return jobs;
        }

        public static RoMonsterScaleTable Scales(string body)
        {
            Assert.That(RoMonsterScaleTable.TryRead($"tbl = {{ {body} }}", out var t, out string reason), Is.True, reason);
            return t;
        }

        public static RoItemTable Items(string body)
        {
            Assert.That(RoItemTable.TryRead($"tbl = {{ {body} }}", "iteminfo_new.lub", out var t, out string reason), Is.True, reason);
            return t;
        }

        public static RoSkillTable Skills(string body)
        {
            Assert.That(RoSkillTable.TryRead($"SKID = {{ {body} }}", out var t, out string reason), Is.True, reason);
            return t;
        }

        /// <summary>The class table of the body, garment and weapon tests.</summary>
        public const string ClassBodies =
            "BodyName,Job,Race,Kind,BaseJob\n" +
            "검사,JOB_SWORDMAN,Human,Base,JOB_SWORDMAN\n" +
            "기사,JOB_KNIGHT,Human,Base,JOB_KNIGHT\n" +
            "페코페코_기사,JOB_KNIGHT2,Human,Mount,JOB_KNIGHT\n" +
            "기사_h,JOB_KNIGHT,Human,Outfit,JOB_KNIGHT\n" +
            "summoner,JOB_SUMMONER,Doram,Base,JOB_SUMMONER\n" +
            "결혼,JOB_WEDDING,Human,Base,JOB_WEDDING\n" +
            "결혼,JOB_WEDDING,Doram,Base,JOB_WEDDING\n";

        public static RoOwnTables Own(string shields = "ViewId,Name\n1,가드\n2,버클러\n")
        {
            Assert.That(RoOwnTables.TryRead(ClassBodies, shields, out var t, out string reason), Is.True, reason);
            return t;
        }

        public static RoViewIdTables ViewIds(string accIds = "", string accNames = "", string robeIds = "", string robeNames = "",
                                             string weaponIds = "", string weaponNames = "", string expansion = "")
        {
            Assert.That(RoViewIdTables.TryRead($"ACCESSORY_IDs = {{ {accIds} }}", $"AccNameTable = {{ {accNames} }}",
                $"SPRITE_ROBE_IDs = {{ {robeIds} }}", $"RobeNameTable = {{ {robeNames} }}",
                $"Weapon_IDs = {{ {weaponIds} }}\nWeaponNameTable = {{ {weaponNames} }}\nExpansion_Weapon_IDs = {{ {expansion} }}",
                out var t, out string reason), Is.True, reason);
            return t;
        }

        public static RoEmoteTable Emotes(string lua)
        {
            Assert.That(RoEmoteTable.TryRead(lua, out var t, out string reason), Is.True, reason);
            return t;
        }
    }
}
