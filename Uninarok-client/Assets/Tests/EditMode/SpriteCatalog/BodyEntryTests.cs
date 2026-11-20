using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Networking.Data;
using static Uninarok.Catalog.Tests.CatalogFixtures;

namespace Uninarok.Catalog.Tests
{
    // TASKS-0008 T3.2 (FR-2; PLAN-0008 4.4): the variants of a class body.
    public class BodyEntryTests
    {
        readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void Destroy() => DestroyAll(_created);

        static BodyEntry.BodyVariant Variant(string name, PlayerJob job, BodyVariantKind kind, AccountSex sex, int animation) =>
            new BodyEntry.BodyVariant { Name = name, Job = job, Kind = kind, Sex = sex, Race = SpriteRace.Human, Sprite = AnimationRef(animation) };

        BodyEntry Knight()
        {
            var e = Make<BodyEntry>(_created);
            e.BaseJob = PlayerJob.JOB_KNIGHT;
            e.Variants = new[]
            {
                Variant("기사", PlayerJob.JOB_KNIGHT, BodyVariantKind.Base, AccountSex.SEX_MALE, 1),
                Variant("기사", PlayerJob.JOB_KNIGHT, BodyVariantKind.Base, AccountSex.SEX_FEMALE, 2),
                Variant("페코페코_기사", PlayerJob.JOB_KNIGHT2, BodyVariantKind.Mount, AccountSex.SEX_FEMALE, 3),
            };
            return e;
        }

        [Test]
        public void TheJobsOwnVariant_IsChosen()
        {
            Assert.That(Knight().TryGetVariant(PlayerJob.JOB_KNIGHT2, AccountSex.SEX_FEMALE, out var v), Is.True);
            Assert.That(v.Kind, Is.EqualTo(BodyVariantKind.Mount));
            Assert.That(v.Sprite.Animation.AssetGUID, Is.EqualTo(Guid(3)));
        }

        [Test]
        public void WithoutAVariantOfItsOwn_TheBaseVariantOfTheSex()
        {
            var knight = Knight();

            Assert.That(knight.TryGetVariant(PlayerJob.JOB_KNIGHT, AccountSex.SEX_MALE, out var v), Is.True);
            Assert.That(v.Sprite.Animation.AssetGUID, Is.EqualTo(Guid(1)));
            Assert.That(knight.TryGetVariant(PlayerJob.JOB_KNIGHT2, AccountSex.SEX_MALE, out var mounted), Is.True, "no male peco body: the base one");
            Assert.That(mounted.Sprite.Animation.AssetGUID, Is.EqualTo(Guid(1)));
        }

        [Test]
        public void NoVariantForTheSex_IsAMiss()
        {
            var e = Make<BodyEntry>(_created);
            e.BaseJob = PlayerJob.JOB_DANCER;
            e.Variants = new[] { Variant("무희", PlayerJob.JOB_DANCER, BodyVariantKind.Base, AccountSex.SEX_FEMALE, 1) };

            Assert.That(e.TryGetVariant(PlayerJob.JOB_DANCER, AccountSex.SEX_MALE, out _), Is.False);
        }
    }

    // TASKS-0008 T3.2 (FR-2; PLAN-0008 4.4): the variants of a head style.
    public class HeadEntryTests
    {
        readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void Destroy() => DestroyAll(_created);

        [Test]
        public void TheVariantIsBySexAndRace()
        {
            var e = Make<HeadEntry>(_created);
            e.Style = 1;
            e.Variants = new[]
            {
                new HeadEntry.HeadVariant { Sex = AccountSex.SEX_MALE, Race = SpriteRace.Human, Sprite = AnimationRef(1) },
                new HeadEntry.HeadVariant { Sex = AccountSex.SEX_MALE, Race = SpriteRace.Doram, Sprite = AnimationRef(2) },
                new HeadEntry.HeadVariant { Sex = AccountSex.SEX_FEMALE, Race = SpriteRace.Human, Sprite = AnimationRef(3) },
            };

            Assert.That(e.TryGetVariant(AccountSex.SEX_MALE, SpriteRace.Doram, out var doram) ? doram.Sprite.Animation.AssetGUID : null, Is.EqualTo(Guid(2)));
            Assert.That(e.TryGetVariant(AccountSex.SEX_FEMALE, SpriteRace.Human, out var female) ? female.Sprite.Animation.AssetGUID : null, Is.EqualTo(Guid(3)));
            Assert.That(e.TryGetVariant(AccountSex.SEX_FEMALE, SpriteRace.Doram, out _), Is.False);
        }
    }

    // TASKS-0008 T3.2 (FR-2, FR-3; PLAN-0008 4.4): palettes by colour number.
    public class PaletteSetTests
    {
        [Test]
        public void APaletteIsFoundByColour_AndAMissIsFalse()
        {
            var set = new PaletteSet
            {
                Colours = new[]
                {
                    new PaletteRef { Colour = 1, Palette = new PaletteReference(Guid(1)) },
                    new PaletteRef { Colour = 4, Palette = new PaletteReference(Guid(4)) },
                    new PaletteRef { Colour = 9, Palette = new PaletteReference(Guid(9)) },
                },
            };

            Assert.That(set.TryGet(4, out var p) ? p.AssetGUID : null, Is.EqualTo(Guid(4)));
            Assert.That(set.TryGet(9, out var last) ? last.AssetGUID : null, Is.EqualTo(Guid(9)));
            Assert.That(set.TryGet(2, out _), Is.False);
            Assert.That(default(PaletteSet).TryGet(1, out _), Is.False, "a set with no colours");
        }
    }
}
