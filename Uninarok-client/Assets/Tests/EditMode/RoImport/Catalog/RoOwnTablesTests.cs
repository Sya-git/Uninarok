using System.IO;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Networking.Data;
using Uninarok.RoImport.Core.Catalog.Tables;

namespace Uninarok.RoImport.Tests.Catalog
{
    // TASKS-0008 T2.8 (FR-2, FR-4; OQ-1; PLAN-0008 4.2): the owner's class-body and shield-name tables.
    public class RoOwnTablesTests
    {
        const string Shields = "# comment\nViewId,Name\n1,가드\n2,버클러\n";
        const string Bodies =
            "# comment\nBodyName,Job,Race,Kind,BaseJob\n" +
            "기사,JOB_KNIGHT,Human,Base,JOB_KNIGHT\n" +
            "# review: a note\n" +
            "페코페코_기사,JOB_KNIGHT2,Human,Mount,JOB_KNIGHT\r\n" +
            "summoner,JOB_SUMMONER,Doram,Base,JOB_SUMMONER\n" +
            "결혼,JOB_WEDDING,Human,Base,JOB_WEDDING\n" +
            "결혼,JOB_WEDDING,Doram,Base,JOB_WEDDING\n";

        static string Rejected(string bodies, string shields = Shields)
        {
            Assert.That(RoOwnTables.TryRead(bodies, shields, out _, out string reason), Is.False);
            return reason;
        }

        [Test]
        public void ReadsBothTables()
        {
            Assert.That(RoOwnTables.TryRead(Bodies, Shields, out var t, out string reason), Is.True, reason);
            Assert.That(t.Bodies.Count, Is.EqualTo(5));
            Assert.That(t.TryGetBody("페코페코_기사", false, out var peco), Is.True);
            Assert.That(peco.Job, Is.EqualTo(PlayerJob.JOB_KNIGHT2));
            Assert.That(peco.BaseJob, Is.EqualTo(PlayerJob.JOB_KNIGHT));
            Assert.That(peco.Kind, Is.EqualTo("Mount"));
            Assert.That(t.TryGetBody("summoner", true, out var summoner) && summoner.Doram, Is.True);
            Assert.That(t.TryGetBody("summoner", false, out _), Is.False, "the race is part of the key");
            Assert.That(t.TryGetBody("결혼", true, out _) && t.TryGetBody("결혼", false, out _), Is.True);
            Assert.That(t.ShieldNames[2], Is.EqualTo("버클러"));
        }

        [Test]
        public void RejectsBadRowsWithTheirLine()
        {
            const string head = "BodyName,Job,Race,Kind,BaseJob\n";
            Assert.That(Rejected(head + "기사,JOB_NOPE,Human,Base,JOB_KNIGHT\n"), Does.Contain("line 2").And.Contain("JOB_NOPE"));
            Assert.That(Rejected(head + "기사,7,Human,Base,JOB_KNIGHT\n"), Does.Contain("PlayerJob"), "a number is not a job name");
            Assert.That(Rejected(head + "기사,JOB_KNIGHT,Elf,Base,JOB_KNIGHT\n"), Does.Contain("Race"));
            Assert.That(Rejected(head + "기사,JOB_KNIGHT,Human,Riding,JOB_KNIGHT\n"), Does.Contain("Kind"));
            Assert.That(Rejected(head + "기사,JOB_KNIGHT,Human,Base\n"), Does.Contain("columns"));
            Assert.That(Rejected(head + "기사,JOB_KNIGHT,Human,Base,JOB_KNIGHT\n기사,JOB_KNIGHT,Human,Base,JOB_KNIGHT\n"), Does.Contain("twice"));
            Assert.That(Rejected("Name,Job\n"), Does.Contain("header"));
            Assert.That(Rejected(Bodies, "ViewId,Name\nx,가드\n"), Does.Contain(RoOwnTables.ShieldNamesFile));
        }

        [Test]
        public void ReadsTheProjectTables()
        {
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", RoOwnTables.Folder);
            string bodies = File.ReadAllText(Path.Combine(folder, RoOwnTables.ClassBodiesFile));
            string shields = File.ReadAllText(Path.Combine(folder, RoOwnTables.ShieldNamesFile));
            Assert.That(RoOwnTables.TryRead(bodies, shields, out var t, out string reason), Is.True, reason);
            Assert.That(t.TryGetBody("초보자", false, out var novice) && novice.Job == PlayerJob.JOB_NOVICE, Is.True);
            Assert.That(t.TryGetBody("페코페코_기사", false, out var peco) && peco.Job == PlayerJob.JOB_KNIGHT2, Is.True, "knight on peco");
            Assert.That(t.TryGetBody("summoner", true, out var summoner) && summoner.Job == PlayerJob.JOB_SUMMONER, Is.True);
            Assert.That(t.ShieldNames.Count, Is.EqualTo(4));
        }
    }
}
