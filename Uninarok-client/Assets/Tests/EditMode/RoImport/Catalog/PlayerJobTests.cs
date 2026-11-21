using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Networking.Data;
using Uninarok.RoImport.Core.Catalog.Tables;

namespace Uninarok.RoImport.Tests.Catalog
{
    // TASKS-0008 T2.9 (FR-2; PLAN-0008 4.4.1, D14, D18): PlayerJob against the client's JTtbl, the class table and its doc.
    public class PlayerJobTests
    {
        // PlayerJob ids the client's JTtbl does not define (T2.9): rebellion and summoner (only their baby forms are there),
        // Hercules' DUMMY placeholders and BABY_STAR_GLADIATOR2. Any other difference fails.
        static readonly short[] NotInJtTbl = { 4215, 4218, 4219, 4221, 4230, 4231, 4232, 4233, 4234, 4235, 4236, 4237, 4238 };

        [Test, Category(CatalogTestFiles.Category)]
        public void EveryMemberIsInTheClientJobTable()
        {
            Assert.That(RoJobTables.TryRead(CatalogTestFiles.TableLua("datainfo/npcidentity.lub"), CatalogTestFiles.TableLua("datainfo/jobidentity.lub"),
                CatalogTestFiles.TableLua("datainfo/jobname.lub"), out var jobs, out string reason), Is.True, reason);
            var clientIds = jobs.PlayerConstants.Values.ToHashSet();
            var missing = Enum.GetValues(typeof(PlayerJob)).Cast<PlayerJob>().Select(j => (short)j).Where(id => !clientIds.Contains(id)).OrderBy(id => id).ToArray();
            CollectionAssert.AreEqual(NotInJtTbl, missing);
        }

        [Test]
        public void EveryJobOfTheClassTableParses()
        {
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", RoOwnTables.Folder);
            bool ok = RoOwnTables.TryRead(File.ReadAllText(Path.Combine(folder, RoOwnTables.ClassBodiesFile)),
                File.ReadAllText(Path.Combine(folder, RoOwnTables.ShieldNamesFile)), out var own, out string reason);
            Assert.That(ok, Is.True, reason);
            Assert.That(own.Bodies.All(b => Enum.IsDefined(typeof(PlayerJob), b.Job) && Enum.IsDefined(typeof(PlayerJob), b.BaseJob)), Is.True);
        }

        [Test]
        public void TheDocListsEveryMember()
        {
            string doc = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Application.dataPath)) ?? ".", "docs", "data", "PlayerJob.md");
            if (!File.Exists(doc)) Assert.Ignore($"doc not found: {doc}");
            var rows = Regex.Matches(File.ReadAllText(doc), @"^\| `(JOB_\w+)` \| `(-?\d+)` \|", RegexOptions.Multiline)
                .Cast<Match>().ToDictionary(m => m.Groups[1].Value, m => short.Parse(m.Groups[2].Value));
            var members = Enum.GetValues(typeof(PlayerJob)).Cast<PlayerJob>().ToDictionary(j => j.ToString(), j => (short)j);
            CollectionAssert.AreEquivalent(members, rows);
        }
    }
}
