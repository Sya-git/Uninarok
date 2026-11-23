using NUnit.Framework;
using UnityEngine;
using Uninarok.Map;
using Uninarok.Networking.Data;
using Uninarok.Sprites;
using Uninarok.Units.Editor;

namespace Uninarok.Units.Tests
{
    // TASKS-0009 T10.5 (FR-17, FR-1, FR-9; PLAN-0009 4.11) on the imported catalog.
    [Category("NeedsData")]
    public class UnitLoaderHandsTests
    {
        const string DaggerPair = "단검_단검";

        AssetDatabaseUnitAssetSource _source;
        UnitLoader _loader;

        [SetUp]
        public void SetUp()
        {
            _source = UnitTestData.CatalogOrIgnore();
            _loader = new UnitLoader(_source);
        }

        static UnitLook Assassin(int rightItem, int leftItem, AccountSex sex = AccountSex.SEX_MALE, int job = (int)PlayerJob.JOB_ASSASSIN) => new UnitLook
        {
            Kind = UnitKind.Player, JobId = job, Sex = sex, HairStyle = 1, WeaponItem = rightItem, ShieldItem = leftItem,
        };

        (UnitParts parts, LoadReport report) Load(in UnitLook look)
        {
            var parts = new UnitParts();
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
        public void UnitLoaderHands_AssassinTwoKnives_CombinedSprite()
        {
            var (parts, report) = Load(Assassin(1201, 1201));
            Assert.That(report.MissingCount, Is.EqualTo(0), report.ToString());
            Assert.That(parts.Has(PartSlot.Weapon), Is.True, report.ToString());
            Assert.That(parts.Has(PartSlot.Shield), Is.False, "the left knife is not a shield");
            Assert.That(report.TryGet(PartSlot.Weapon, out var line) && line.Found && line.Text.Contains(DaggerPair), Is.True, report.ToString());
            Assert.That(report.Hands.WeaponType, Is.EqualTo(25));
            Assert.That(report.Hands.Weapon, Is.EqualTo(WeaponSource.Both));
        }

        [Test]
        public void UnitLoaderHands_KnifeLeftOnly_DrawnAsTheWeapon()
        {
            var (parts, report) = Load(Assassin(0, 1201));
            Assert.That(report.MissingCount, Is.EqualTo(0), report.ToString());
            Assert.That(parts.Has(PartSlot.Weapon), Is.True, report.ToString());
            Assert.That(parts.Has(PartSlot.Shield), Is.False);
            Assert.That(report.TryGet(PartSlot.Weapon, out var line) && !line.Text.Contains(DaggerPair), Is.True, "a single knife, not the pair\n" + report);
            Assert.That(report.Hands.Weapon, Is.EqualTo(WeaponSource.Left));
        }

        [Test]
        public void UnitLoaderHands_KnifeAndShield_Both()
        {
            var (parts, report) = Load(Assassin(1201, 2101));
            Assert.That(report.MissingCount, Is.EqualTo(0), report.ToString());
            Assert.That(parts.Has(PartSlot.Weapon) && parts.Has(PartSlot.Shield), Is.True, report.ToString());
            Assert.That(report.Hands.Weapon, Is.EqualTo(WeaponSource.Right));
            Assert.That(report.Hands.DrawShield, Is.True);
        }

        [Test]
        public void UnitLoaderHands_Katar_Alone()
        {
            var look = Assassin(0, 0);
            look.Weapon = WeaponHands.Katar;   // a view id: the katar's own
            look.Shield = 1;                   // anything in the left hand
            var (parts, report) = Load(look);
            Assert.That(parts.Has(PartSlot.Weapon), Is.True, report.ToString());
            Assert.That(parts.Has(PartSlot.Shield), Is.False, "no left drawn with a katar");
            Assert.That(report.Hands.WeaponType, Is.EqualTo(WeaponHands.Katar));
            Assert.That(report.Hands.DrawShield, Is.False);
        }

        [Test]
        public void UnitLoaderHands_ShieldOnly()
        {
            var (parts, report) = Load(Assassin(0, 2101, job: (int)PlayerJob.JOB_SWORDMAN));
            Assert.That(report.MissingCount, Is.EqualTo(0), report.ToString());
            Assert.That(parts.Has(PartSlot.Shield), Is.True, report.ToString());
            Assert.That(parts.Has(PartSlot.Weapon), Is.False);
            Assert.That(report.Hands.Weapon, Is.EqualTo(WeaponSource.None));
        }

        [Test]
        public void UnitLoaderHands_SmallLeftValue_IsAShield()
        {
            var look = UnitTestData.Swordsman();
            look.Weapon = 0;
            look.WeaponItem = 0;
            look.ShieldItem = 0;
            look.Shield = 1;   // a view id: ambiguous, counts as a shield (FR-17)
            var (parts, report) = Load(look);
            Assert.That(parts.Has(PartSlot.Shield), Is.True, report.ToString());
            Assert.That(parts.Has(PartSlot.Weapon), Is.False);
            Assert.That(report.Hands.Left.Kind, Is.EqualTo(HandKind.Shield));
        }

        [Test]
        public void UnitLoaderHands_SwordsmanTwoWeapons_RightOnly_NotMissing()
        {
            var look = UnitTestData.Swordsman();
            look.Weapon = 0;
            look.Shield = 0;
            look.WeaponItem = 1101;
            look.ShieldItem = 1101;
            var (parts, report) = Load(look);
            Assert.That(report.MissingCount, Is.EqualTo(0), "not a warning\n" + report);
            Assert.That(parts.Has(PartSlot.Weapon), Is.True);
            Assert.That(parts.Has(PartSlot.Shield), Is.False);
            Assert.That(report.TryGet(PartSlot.Weapon, out var line) && line.Found && line.Text.Contains("left weapon not drawn"), Is.True, report.ToString());
            Assert.That(report.Hands.LeftNotDrawn, Is.True);
        }

        [Test]
        public void UnitLoaderHands_ReportNamesTypeAndHands()
        {
            var (_, report) = Load(Assassin(1201, 1201));
            Assert.That(report.TryGet(PartSlot.Weapon, out var line), Is.True, report.ToString());
            Assert.That(line.Text, Does.Contain("right").And.Contain("left").And.Contain("1201").And.Contain("type 25"));
        }

        [Test]
        public void UnitLoaderHands_UnknownLeftItem_MissingWithReason()
        {
            var (parts, report) = Load(Assassin(1201, 2220));
            Assert.That(report.MissingCount, Is.GreaterThanOrEqualTo(1), report.ToString());
            bool named = false;
            foreach (var l in report.Lines)
                if (!l.Found && l.Text.Contains("2220") && l.Text.Contains("Headgear")) named = true;
            Assert.That(named, Is.True, report.ToString());
            Assert.That(parts.Has(PartSlot.Weapon), Is.True, "the right knife still shows");
            Assert.That(report.Hands.Left.Kind, Is.EqualTo(HandKind.Unknown));
        }

        // R15: the combined sprite may be missing for some job or sex of the family.
        [Test]
        public void UnitLoaderHands_AssassinFamilySweep_CombinedOrMissingWithReason()
        {
            var missing = new System.Text.StringBuilder();
            foreach (PlayerJob job in System.Enum.GetValues(typeof(PlayerJob)))
            {
                if (!JobFamilies.IsAssassinFamily((int)job)) continue;
                foreach (var sex in new[] { AccountSex.SEX_MALE, AccountSex.SEX_FEMALE })
                {
                    var (parts, report) = Load(Assassin(1201, 1201, sex, (int)job));
                    string who = $"{job} {sex}";
                    Assert.That(report.TryGet(PartSlot.Weapon, out var line), Is.True, who + "\n" + report);
                    if (line.Found)
                    {
                        Assert.That(parts.Has(PartSlot.Weapon), Is.True, who);
                        Assert.That(line.Text, Does.Contain(DaggerPair), who);
                    }
                    else
                    {
                        Assert.That(line.Text, Is.Not.Empty, who);
                        missing.AppendLine($"{who}: {line.Text}");
                    }
                    _loader.Release(parts);
                }
            }
            Assert.That(_source.HeldCount, Is.EqualTo(0));
            if (missing.Length > 0) Debug.Log("assassin family without the combined sprite:\n" + missing);
        }
    }

    // FR-9: a hand changed while shown reloads and keeps the frame.
    [Category("NeedsData")]
    public class UnitLoaderHandsViewTests
    {
        UnitView _view;

        [SetUp]
        public void SetUp()
        {
            UnitSystem.ResetForTests();
            var source = UnitTestData.CatalogOrIgnore();
            _view = UnitView.Create(new UnitLoader(source), null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_view != null) Object.DestroyImmediate(_view.gameObject);
            UnitSystem.ResetForTests();
        }

        void Step(float seconds) => UnitSystem.StepOne(_view, seconds, Quaternion.Euler(50f, 0f, 0f));

        [Test]
        public void UnitLoaderHands_ChangeLeftHandWhileShown_KeepsFrame()
        {
            var look = UnitTestData.Swordsman();
            look.JobId = (int)PlayerJob.JOB_ASSASSIN;
            look.Weapon = 0;
            look.Shield = 0;
            look.WeaponItem = 1201;
            look.ShieldItem = 2101;
            _view.SetLook(look);
            _view.Play(PcAnimation.Walk);
            Step(0f);
            float ms = _view.ActorOf(PartSlot.Body).Data.Actions[_view.ActorOf(PartSlot.Body).UniqueAction].FrameMs;
            Step(3.5f * ms / 1000f);
            Assert.That(_view.Frame, Is.EqualTo(3));

            look.ShieldItem = 1201;   // the shield becomes a second knife: the combined sprite
            _view.SetLook(look);
            Step(0f);
            Assert.That(_view.Frame, Is.EqualTo(3), "mid-walk, the frame stays");
            Assert.That(_view.Parts.Has(PartSlot.Weapon), Is.True);
            Assert.That(_view.Parts.Has(PartSlot.Shield), Is.False);
            Assert.That(_view.ActorOf(PartSlot.Weapon).CurrentFrame % 8, Is.EqualTo(_view.Frame % 8));
        }
    }
}
