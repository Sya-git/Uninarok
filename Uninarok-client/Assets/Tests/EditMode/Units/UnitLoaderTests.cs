using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.AddressableAssets;
using Uninarok.Catalog;
using Uninarok.Networking.Data;
using Uninarok.Sprites;
using Uninarok.Units.Editor;

namespace Uninarok.Units.Tests
{
    // TASKS-0009 T4.1, T4.2 (FR-1, FR-2; PLAN-0009 4.2) on the imported catalog.
    [Category("NeedsData")]
    public class UnitLoaderTests
    {
        AssetDatabaseUnitAssetSource _source;
        UnitLoader _loader;

        [SetUp]
        public void SetUp()
        {
            _source = UnitTestData.CatalogOrIgnore();
            _loader = new UnitLoader(_source);
        }

        (UnitParts parts, LoadReport report) Load(in UnitLook look, UnitParts parts = null)
        {
            parts = parts ?? new UnitParts();
            LoadReport report = null;
            int calls = 0;
            _loader.Load(look, parts, (_, r) =>
            {
                report = r;
                calls++;
            });
            Assert.That(calls, Is.EqualTo(1), "the editor source answers at once, once");
            return (parts, report);
        }

        [Test]
        public void AssetSource_CountsTwoUsers_BackToZero()
        {
            Assert.That(_source.Catalog.TryGetUnit(1002, out var poring), Is.True);
            AssetReference reference = poring.Sprite.Animation;
            SpriteAnimation a = null, b = null;
            _source.Acquire<SpriteAnimation>(reference, x => a = x);
            _source.Acquire<SpriteAnimation>(reference, x => b = x);
            Assert.That(a, Is.Not.Null);
            Assert.That(a, Is.SameAs(b));
            Assert.That(_source.CountOf(reference), Is.EqualTo(2));
            _source.Release(reference);
            Assert.That(_source.CountOf(reference), Is.EqualTo(1));
            _source.Release(reference);
            Assert.That(_source.HeldCount, Is.EqualTo(0));
            Assert.That(() => _source.Release(reference), Throws.InvalidOperationException);
        }

        [Test]
        public void UnitLook_ForJob_PlayerClassOrUnit()
        {
            Assert.That(UnitLook.ForJob(0).Kind, Is.EqualTo(UnitKind.Player));
            Assert.That(UnitLook.ForJob(4218).Kind, Is.EqualTo(UnitKind.Player));
            Assert.That(UnitLook.ForJob(44).Kind, Is.EqualTo(UnitKind.Player));
            Assert.That(UnitLook.ForJob(45).Kind, Is.EqualTo(UnitKind.Unit));
            Assert.That(UnitLook.ForJob(117).Kind, Is.EqualTo(UnitKind.Unit));
            Assert.That(UnitLook.ForJob(1002).Kind, Is.EqualTo(UnitKind.Unit));
            Assert.That(UnitLook.ForJob(6001).Kind, Is.EqualTo(UnitKind.Unit));
            Assert.That(UnitLook.ForJob(20395).Kind, Is.EqualTo(UnitKind.Unit));
            Assert.That(UnitLook.ForJob(1002) == UnitLook.ForJob(1002), Is.True);
            Assert.That(UnitLook.ForJob(1002) != UnitLook.ForJob(1003), Is.True);
        }

        [Test]
        public void UnitLoader_ResolvesReferenceSet()
        {
            foreach (var (name, look, slots) in UnitTestData.ReferenceSet())
            {
                var (parts, report) = Load(look);
                Assert.That(report.MissingCount, Is.EqualTo(0), name + "\n" + report);
                foreach (var slot in slots) Assert.That(parts.Has(slot), Is.True, $"{name}: {slot}\n{report}");
                foreach (var line in report.Lines) Assert.That(line.Text, Is.Not.Empty);
                _loader.Release(parts);
                Assert.That(_source.HeldCount, Is.EqualTo(0), name);
            }
        }

        [Test]
        public void UnitLoader_Swordsman_PalettesRaceAndTrail()
        {
            var (parts, report) = Load(UnitTestData.Swordsman());
            Assert.That(parts.Palettes[(int)PartSlot.Head], Is.Not.Null, report.ToString());
            Assert.That(parts.Race, Is.EqualTo(SpriteRace.Human));
            Assert.That(parts.Riding, Is.False);
            Assert.That(parts.HasShadow && parts.ShadowScale == 1f, Is.True);
            Assert.That(report.TryGet(PartSlot.Weapon, out var line) && line.Text.Contains("view id 2"), Is.True, report.ToString());

            var knight = Load(UnitTestData.KnightOnPeco()).parts;
            Assert.That(knight.Riding, Is.True);
            Assert.That(knight.ShadowScale, Is.EqualTo(1.5f));
            var summoner = Load(UnitTestData.Summoner()).parts;
            Assert.That(summoner.Race, Is.EqualTo(SpriteRace.Doram));
        }

        [Test]
        public void UnitLoader_MissingHeadgear_ReportsReason()
        {
            var look = UnitTestData.Swordsman();
            look.HeadTop = 65000;
            var (parts, report) = Load(look);
            Assert.That(parts.Has(PartSlot.HeadTop), Is.False);
            Assert.That(report.MissingCount, Is.EqualTo(1));
            Assert.That(report.TryGet(PartSlot.HeadTop, out var line) && !line.Found && line.Text.Contains("65000"), Is.True, report.ToString());
            Assert.That(parts.Has(PartSlot.Body) && parts.Has(PartSlot.Head) && parts.Has(PartSlot.HeadBottom), Is.True, "the rest shows");
        }

        [Test]
        public void UnitLoader_ByItemId_WrongKindIsMissing()
        {
            var look = UnitTestData.Swordsman();
            look.Weapon = 0;
            look.WeaponItem = 1101;
            var (parts, report) = Load(look);
            Assert.That(parts.Has(PartSlot.Weapon), Is.True, report.ToString());
            look.WeaponItem = 2220;
            (_, report) = Load(look);
            Assert.That(report.TryGet(PartSlot.Weapon, out var line) && !line.Found && line.Text.Contains("Headgear"), Is.True, report.ToString());
        }

        [Test]
        public void UnitLoader_PlayerJobOnNpc_ShowsAsPlayer()
        {
            var (parts, report) = Load(UnitLook.ForJob((int)PlayerJob.JOB_KNIGHT));
            Assert.That(parts.Has(PartSlot.Body) && parts.Has(PartSlot.Head), Is.True, report.ToString());
        }

        [Test]
        public void UnitLoader_BabyJobs_AreTheirAdultClassAt08()
        {
            foreach (PlayerJob job in System.Enum.GetValues(typeof(PlayerJob)))
            {
                bool baby = job.ToString().Contains("BABY");
                Assert.That(UnitLook.IsBabyJob((int)job), Is.EqualTo(baby), job.ToString());
                if (!baby) continue;
                bool female = job == PlayerJob.JOB_BABY_DANCER || job == PlayerJob.JOB_BABY_WANDERER || job == PlayerJob.JOB_BABY_OBORO;
                var (parts, report) = Load(UnitLook.ForJob((int)job, female ? AccountSex.SEX_FEMALE : AccountSex.SEX_MALE));
                Assert.That(parts.Has(PartSlot.Body) && parts.Has(PartSlot.Head), Is.True, $"{job}\n{report}");
                Assert.That(parts.Scale, Is.EqualTo(0.8f), job.ToString());
                _loader.Release(parts);
            }
        }

        [Test]
        public void UnitLoader_BabySwordsman_HasTheSwordsmansParts()
        {
            var look = UnitTestData.Swordsman();
            var adult = Load(look).parts;
            look.JobId = (int)PlayerJob.JOB_BABY_SWORDMAN;
            var (baby, report) = Load(look);
            Assert.That(report.MissingCount, Is.EqualTo(0), report.ToString());
            Assert.That(baby.Animations, Is.EqualTo(adult.Animations));
            Assert.That(baby.ShadowScale, Is.EqualTo(0.75f));
            Assert.That(adult.Scale, Is.EqualTo(1f));

            Assert.That(Load(UnitLook.ForJob((int)PlayerJob.JOB_BABY_KNIGHT2)).parts.Riding, Is.True);
            Assert.That(Load(UnitLook.ForJob((int)PlayerJob.JOB_BABY_SUMMONER)).parts.Race, Is.EqualTo(SpriteRace.Doram));
        }

        [Test]
        public void UnitLoader_HighAndTranscendentJobs_UseTheirBaseSprites()
        {
            foreach (var job in new[]
                     {
                         PlayerJob.JOB_NOVICE_HIGH, PlayerJob.JOB_SWORDMAN_HIGH, PlayerJob.JOB_THIEF_HIGH, PlayerJob.JOB_RUNE_KNIGHT_T,
                         PlayerJob.JOB_RUNE_KNIGHT_T2, PlayerJob.JOB_SUPER_NOVICE_E,
                     })
            {
                var (parts, report) = Load(UnitLook.ForJob((int)job));
                Assert.That(parts.Has(PartSlot.Body) && parts.Has(PartSlot.Head), Is.True, $"{job}\n{report}");
                Assert.That(parts.Scale, Is.EqualTo(1f), job.ToString());
            }
        }

        [Test]
        public void UnitLoader_HiddenJob_ShowsNothing()
        {
            foreach (int job in new[] { 45, 111, 139, 2337 })
            {
                var (parts, report) = Load(UnitLook.ForJob(job));
                Assert.That(parts.Hidden, Is.True, job.ToString());
                Assert.That(parts.Has(PartSlot.Body), Is.False);
                Assert.That(parts.HasShadow, Is.False);
                Assert.That(report.MissingCount, Is.EqualTo(0), "not a warning");
            }
        }

        [Test]
        public void UnitLoader_ShadowScales_OfTheAcceptance()
        {
            Assert.That(Load(UnitLook.ForJob(1002)).parts.ShadowScale, Is.EqualTo(1f), "poring");
            Assert.That(Load(UnitTestData.Novice()).parts.ShadowScale, Is.EqualTo(1f), "novice");
            Assert.That(Load(UnitLook.ForJob(1007)).parts.ShadowScale, Is.EqualTo(0.5f), "fabre");
            Assert.That(Load(UnitLook.ForJob(1078)).parts.HasShadow, Is.False, "red plant");
            Assert.That(Load(UnitLook.ForJob(45)).parts.HasShadow, Is.False, "warp NPC");
        }

        [Test]
        public void UnitLoader_UnknownJob_IsMissing_NoShadow()
        {
            var (parts, report) = Load(UnitLook.ForJob(3999));
            Assert.That(report.MissingCount, Is.EqualTo(1));
            Assert.That(parts.HasShadow, Is.False);
            Assert.That(parts.Has(PartSlot.Body), Is.False);
        }

        [Test]
        public void UnitLoader_LookChanges_ReleaseTheOldAssets()
        {
            var parts = new UnitParts();
            var swordsman = UnitTestData.Swordsman();
            Load(swordsman, parts);
            int held = _source.HeldCount;
            var version = parts.Version;
            Load(UnitLook.ForJob(1002), parts);
            Assert.That(parts.Version, Is.GreaterThan(version));
            Load(swordsman, parts);
            Assert.That(_source.HeldCount, Is.EqualTo(held));
            _loader.Release(parts);
            Assert.That(_source.HeldCount, Is.EqualTo(0));
        }

        // A source that answers later: a newer load wins and the older one releases what it got.
        sealed class LateSource : IUnitAssetSource
        {
            readonly AssetDatabaseUnitAssetSource _inner;
            readonly Queue<System.Action> _waiting = new Queue<System.Action>();
            public LateSource(AssetDatabaseUnitAssetSource inner) => _inner = inner;
            public SpriteCatalog Catalog => _inner.Catalog;
            public CompositionTables Tables => _inner.Tables;
            public void Acquire<T>(AssetReference reference, System.Action<T> done) where T : UnityEngine.Object => _waiting.Enqueue(() => _inner.Acquire(reference, done));
            public void Release(AssetReference reference) => _inner.Release(reference);
            public void Answer()
            {
                while (_waiting.Count > 0) _waiting.Dequeue()();
            }
        }

        [Test]
        public void UnitLoader_NewerLoadSupersedesOneInFlight()
        {
            var late = new LateSource(_source);
            var loader = new UnitLoader(late);
            var parts = new UnitParts();
            int first = 0, second = 0;
            loader.Load(UnitTestData.Swordsman(), parts, (_, __) => first++);
            loader.Load(UnitLook.ForJob(1002), parts, (_, __) => second++);
            late.Answer();
            Assert.That((first, second), Is.EqualTo((0, 1)));
            Assert.That(parts.Look.JobId, Is.EqualTo(1002));
            Assert.That(parts.Has(PartSlot.Head), Is.False);

            loader.Load(UnitTestData.Swordsman(), parts, (_, __) => first++);
            loader.Release(parts);   // destroyed while loading
            late.Answer();
            Assert.That(first, Is.EqualTo(0));
            Assert.That(_source.HeldCount, Is.EqualTo(0));
        }
    }
}
