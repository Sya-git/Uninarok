using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Catalog;
using Uninarok.Networking.Data;
using Uninarok.RoImport.Core.Catalog.Composition;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.Units;
using B = Uninarok.RoImport.Core.Catalog.Composition.RoCompositionTablesBuilder;

namespace Uninarok.RoImport.Tests.Catalog.Composition
{
    // TASKS-0009 T1.3 (FR-3, FR-4, FR-11; PLAN-0009 4.1): fixtures copied from the decompiled tables, cut short.
    public class RoCompositionTablesBuilderTests
    {
        Dictionary<string, string> _files;
        RoCatalogTables _tables;

        [SetUp]
        public void SetUp()
        {
            Assert.That(RoJobTables.TryRead("jobtbl = { JT_WARPNPC = 45, JT_HIDDEN_NPC = 111, JT_PORING = 1002, JT_HIDDEN_MOB = 2337, JT_EDDGA3 = 21653 }",
                "JTtbl = { JT_NOVICE = 0, JT_FABRE = 1007, JT_NOVICE_B = 4023 }",
                "JobNameTable = { [jobtbl.JT_PORING] = \"PORING\", [jobtbl.JT_HIDDEN_MOB] = \"invisible\", [jobtbl.JT_HIDDEN_NPC] = \"1_ETC_01\" }", out var jobs, out string reason), Is.True, reason);
            Assert.That(RoViewIdTables.TryRead("ACCESSORY_IDs = { ACCESSORY_HAT = 16, ACCESSORY_BACK = 20, ACCESSORY_FOX = 30 }", "AccNameTable = { }",
                "SPRITE_ROBE_IDs = { ROBE_WINGS = 1, ROBE_BAG = 2 }", "RobeNameTable = { }", "Weapon_IDs = { }\nWeaponNameTable = { }\nExpansion_Weapon_IDs = { }", out var views, out reason), Is.True, reason);
            _tables = RoCatalogTables.Create(jobs, views);
            _files = new Dictionary<string, string>
            {
                [B.PriorityFile] = "TB_Layer_Priority = {\n Default_Mid = 100,\n Default_Top = 200,\n Default_Bottom = 300,\n Default_Robe = 400,\n Items_List = {\n"
                    + "[ACCESSORY_IDs.ACCESSORY_FOX] = { Default = 404, isIgnoreRiding = true },\n[ACCESSORY_IDs.ACCESSORY_HAT] = { Default = 143, Direction = {} },\n"
                    + "[ACCESSORY_IDs.ACCESSORY_BACK] = { Default = -300, Direction = { [2] = -100, [1] = -100, [0] = -100 } }\n}\n}\nGetPriority = function(a, layer)\n if layer == 2 then\n return 1\n end\nend",
                [B.RobeNameFile] = "RobeNameTable = { [SPRITE_ROBE_IDs.ROBE_WINGS] = \"x\" }\nRobeTopLayer = {\n SPRITE_ROBE_IDs.ROBE_BAG\n}",
                [B.JobInheritFile] = "JOBID = { JT_NOVICE = 0, JT_NOVICE_H = 4001, JT_STAR = 4047 }",
                [B.PcIdentityFile] = "pcJobTbl = { JT_NOVICE = 0, JT_STAR = 4047 }",
                [B.LayerFunctionsFile] = "SEX_FEMALE = 0\nLAYER_ALL_TABLE_LIST = { [JOBID.JT_NOVICE_H] = JOBID.JT_NOVICE, [JOBID.JT_STAR] = JOBID.JT_STAR }\n_New_DrawOnTop = function(robeID)\n return true\nend",
                [B.LayerMaleFile] = "_New_BigLayerDir_M = {\n [0] = {\n  [0] = { 0, 1, 2 },\n  [42] = { 1, 2 }\n }\n}",
                [B.LayerFemaleFile] = "_New_BigLayerDir_F = {\n [7] = {\n  [1] = { 0 }\n }\n}",
                [B.HeadToBodyFile] = "_2dlayertoHead_list = {\n { 0, 4218, 23, 3, 0 },\n { 0, 4218, 23, 3, 2 },\n { 1, 0, 1, 4, 1 },\n { 10, 0, 0, 0, 0 }\n}\nGet = function(i)\n return i\nend",
                [B.RidingFile] = "SetHeadSprEditInfo(pcJobTbl.JT_STAR, 99, 99, 1, 1, -6, 15)\nSetHeadSprEditInfo(pcJobTbl.JT_NOVICE, 4, 4, 0, 2, 0, 35)\nSetBodySprEditInfo_THIEFS()\nSetBodySprEditInfo(4186, 12, 12, 0, 7, 0, 5)",
                [B.OffsetFunctionsFile] = "OffsetItemPos = {}\nOffsetItemPos.Doram = {}\nOffsetItemPos.AddOffsetForDoram = function(a)\n local t = { ClassNum = a }\nend",
                [B.OffsetFile] = "OffsetItemPos.Direction = { FRONT = 0, FRONTLEFT = 1 }\nOffsetItemPos.Sex = { MALE = 1, FEMALE = 2 }\nOffsetItemPos.AddOffsetForDoram(1717, OffsetItemPos.Direction.FRONT, {\n y = -15\n})\n"
                    + "OffsetItemPos.AddOffsetForDoram(54, OffsetItemPos.Direction.FRONTLEFT, {\n x = -3,\n y = 1\n}, OffsetItemPos.Sex.FEMALE)\nOffsetItemPos.AddUppingBottomItem(1081)",
                [B.TransparentFile] = "transparentItemlist = {\n { 21, 255, 255, 2000 },\n { 36, 100, 200, 25500 },\n { 0, 0, 0, 0 }\n}",
                [B.ShadowFile] = "ShadowFactorTable = { [jobtbl.JT_WARPNPC] = 0, [JTtbl.JT_FABRE] = 0.5, [JTtbl.JT_NOVICE_B] = 0.75, [JTtbl.JT_EDDGA3] = 1.2 }",
            };
        }

        (CompositionTablesData data, IReadOnlyList<RoCompositionGap> gaps) Build() => B.Build(_tables, f => _files.TryGetValue(f, out string t) ? t : null);

        [Test]
        public void RoCompositionTablesBuilder_EveryTable_NoGaps()
        {
            var (_, gaps) = Build();
            Assert.That(gaps.Select(g => g.File + ": " + g.Message), Is.Empty);
        }

        [Test]
        public void RoCompositionTablesBuilder_Priorities_TopLayer()
        {
            var (d, _) = Build();
            Assert.That((d.DefaultTop, d.DefaultMid, d.DefaultBottom, d.DefaultRobe), Is.EqualTo((200, 100, 300, 400)));
            CollectionAssert.AreEqual(new[] { 16, 20, 30 }, d.Priorities.Select(p => p.ViewId), "sorted");
            Assert.That(d.Priorities[0].FirstDirection, Is.EqualTo(-1), "an empty Direction table");
            Assert.That(d.Priorities[1].FirstDirection, Is.EqualTo(0));
            Assert.That(d.DirectionPriorities[2], Is.EqualTo(-100));
            Assert.That(d.DirectionPriorities[4], Is.EqualTo(CompositionTables.NoPriority));
            Assert.That(d.Priorities[2].IgnoreRiding, Is.True);
            CollectionAssert.AreEqual(new[] { 2 }, d.RobeTopLayer);
        }

        [Test]
        public void RoCompositionTablesBuilder_LayerTables()
        {
            var (d, _) = Build();
            var t = ScriptableObject.CreateInstance<CompositionTables>();
            d.ApplyTo(t);
            Assert.That(t.TryGetLayerSwap(4001, AccountSex.SEX_MALE, 42, 1, out bool behind) && behind, Is.True, "inherited job");
            Assert.That(t.TryGetLayerSwap(0, AccountSex.SEX_MALE, 42, 0, out behind) && !behind, Is.True);
            Assert.That(t.TryGetLayerSwap(7, AccountSex.SEX_FEMALE, 1, 0, out behind) && behind, Is.True);
            Assert.That(t.TryGetLayerSwap(7, AccountSex.SEX_MALE, 1, 0, out _), Is.False);
            CollectionAssert.AreEqual(new[] { 4001 }, d.LayerJobs, "a job that inherits itself is left out");
            Assert.That(t.IsGarmentUnderHead(4218, AccountSex.SEX_FEMALE, 23, 3, 2), Is.True);
            Assert.That(t.IsGarmentUnderHead(4218, AccountSex.SEX_FEMALE, 23, 3, 1), Is.False);
            Assert.That(t.IsGarmentUnderHead(0, AccountSex.SEX_MALE, 1, 4, 1), Is.True);
            Assert.That(d.HeadToBody.Length, Is.EqualTo(2), "the terminator is not a row");
            Object.DestroyImmediate(t);
        }

        [Test]
        public void RoCompositionTablesBuilder_Edits_Offsets_Tints()
        {
            var (d, _) = Build();
            CollectionAssert.AreEqual(new[] { 0, 4047 }, d.HeadEdits.Select(e => e.Job), "sorted by job");
            Assert.That((d.HeadEdits[1].Dx, d.HeadEdits[1].Dy), Is.EqualTo((-6, 15)));
            Assert.That(d.BodyEdits.Single().Job, Is.EqualTo(4186));
            CollectionAssert.AreEqual(new[] { 54, 1717 }, d.ItemOffsets.Select(o => o.ViewId));
            Assert.That(d.ItemOffsets[0].Sex, Is.EqualTo(2));
            Assert.That(d.ItemOffsets[1].Race, Is.EqualTo(SpriteRace.Doram));
            Assert.That(d.ItemOffsets[1].Dy, Is.EqualTo(-15));
            Assert.That(d.Transparency.Single().ViewId, Is.EqualTo(36));
            Assert.That(d.Transparency.Single().Tint.a, Is.EqualTo(150));
        }

        [Test]
        public void RoCompositionTablesBuilder_Shadows_Hidden()
        {
            var (d, _) = Build();
            CollectionAssert.AreEqual(new[] { 45, 1007, 4023, 21653 }, d.ShadowJobIds);
            CollectionAssert.AreEqual(new[] { 0f, 0.5f, 0.75f, 1.2f }, d.ShadowScales);
            CollectionAssert.AreEqual(new[] { 45, 111, 2337 }, d.HiddenJobIds);
        }

        // TASKS-0011 T4.6 (SPEC-0011 FR-4, FR-14; PLAN-0011 4.7).
        [Test]
        public void RoCompositionTablesBuilder_WeaponTypes()
        {
            Assert.That(RoViewIdTables.TryRead("ACCESSORY_IDs = { }", "AccNameTable = { }", "SPRITE_ROBE_IDs = { }", "RobeNameTable = { }",
                "Weapon_IDs = { WEAPONTYPE_NONE = 0, WEAPONTYPE_SHORTSWORD = 1, WEAPONTYPE_BOW = 11, WEAPONTYPE_Main_Gauche = 51, WEAPONTYPE_Hunter_Bow = 61 }\n" +
                "WeaponNameTable = { }\n" +
                "Expansion_Weapon_IDs = { [Weapon_IDs.WEAPONTYPE_Main_Gauche] = Weapon_IDs.WEAPONTYPE_SHORTSWORD, [Weapon_IDs.WEAPONTYPE_Hunter_Bow] = Weapon_IDs.WEAPONTYPE_BOW }",
                out var views, out string reason), Is.True, reason);
            var (d, _) = B.Build(RoCatalogTables.Create(null, views), f => _files.TryGetValue(f, out string t) ? t : null);
            CollectionAssert.AreEqual(new[] { 51, 61 }, d.WeaponViewIds);
            CollectionAssert.AreEqual(new[] { 1, 11 }, d.WeaponBaseTypes);

            var tables = ScriptableObject.CreateInstance<CompositionTables>();
            try
            {
                d.ApplyTo(tables);
                Assert.That(tables.WeaponTypeOf(51), Is.EqualTo(1), "Main Gauche is a short sword");
                Assert.That(tables.WeaponTypeOf(61), Is.EqualTo(11), "a hunter bow is a bow");
                Assert.That(tables.WeaponTypeOf(11), Is.EqualTo(11), "a base type is itself");
                Assert.That(tables.WeaponTypeOf(9999), Is.EqualTo(0), "unknown: bare hand");
                Assert.That(tables.WeaponTypeOf(0), Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(tables);
            }
        }

        [Test]
        public void RoCompositionTablesBuilder_MissingFile_OneGap_EmptyTable_OthersLoad()
        {
            _files.Remove(B.ShadowFile);
            var (d, gaps) = Build();
            Assert.That(gaps.Count, Is.EqualTo(1));
            Assert.That(gaps[0].File, Is.EqualTo(B.ShadowFile));
            Assert.That(gaps[0].Missing, Is.True);
            Assert.That(d.ShadowJobIds, Is.Empty);
            Assert.That(d.Priorities, Is.Not.Empty);
        }

        [Test]
        public void RoCompositionTablesBuilder_UnreadableFile_OneGap()
        {
            _files[B.PriorityFile] = "TB_Layer_Priority = { 1 + 2 }";
            var (d, gaps) = Build();
            Assert.That(gaps.Count, Is.EqualTo(1));
            Assert.That(gaps[0].Missing, Is.False);
            Assert.That(d.Priorities, Is.Empty);
            Assert.That(d.DefaultTop, Is.EqualTo(200), "the defaults stay");
        }
    }
}
