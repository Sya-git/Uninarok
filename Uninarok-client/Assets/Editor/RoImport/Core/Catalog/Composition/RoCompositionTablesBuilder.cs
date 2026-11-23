using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Uninarok.Catalog;
using Uninarok.Networking.Data;
using Uninarok.RoImport.Core.Catalog.Lua;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.Units;

namespace Uninarok.RoImport.Core.Catalog.Composition
{
    /// <summary>
    /// Builds the composition tables (PLAN-0009 4.1, D1; SPEC-0009 FR-3, FR-4, FR-8, FR-11) from the decompiled texts and the
    /// catalog tables already read. Pure. Each table is independent: a missing or unreadable file gives one gap and leaves
    /// its table empty. Columns and meaning: composition-reference.md.
    /// </summary>
    public static class RoCompositionTablesBuilder
    {
        public const string PriorityFile = "datainfo/tb_layer_priority.lub";
        public const string RobeNameFile = "datainfo/spriterobename.lub";
        public const string JobInheritFile = "skillinfoz/jobinheritlist.lub";
        public const string PcIdentityFile = "admin/pcidentity.lub";
        public const string LayerFunctionsFile = "spreditinfo/_new_2dlayerdir_f.lub";
        public const string LayerMaleFile = "spreditinfo/_new_biglayerdir_male.lub";
        public const string LayerFemaleFile = "spreditinfo/_new_biglayerdir_female.lub";
        public const string HeadToBodyFile = "spreditinfo/_2dlayer_headtobody.lub";
        public const string RidingFile = "ridingspreditinfo/ridingspreditinfo.lub";
        public const string OffsetFunctionsFile = "offsetitempos/offsetitempos_f.lub";
        public const string OffsetFile = "offsetitempos/offsetitempos.lub";
        public const string TransparentFile = "transparentitem/transparentitem.lub";
        public const string ShadowFile = "datainfo/shadowtable.lub";

        /// <summary>Every file the builder asks for, for the import's ledger.</summary>
        public static IReadOnlyList<string> Files => new[]
        {
            PriorityFile, RobeNameFile, JobInheritFile, PcIdentityFile, LayerFunctionsFile, LayerMaleFile, LayerFemaleFile, HeadToBodyFile,
            RidingFile, OffsetFunctionsFile, OffsetFile, TransparentFile, ShadowFile,
        };

        /// <summary>The sprite name of job ids that draw nothing (composition-reference.md 6).</summary>
        const string InvisibleName = "INVISIBLE";
        static string[] HiddenConstants => new[] { "JT_WARPNPC", "JT_HIDDEN_NPC", "JT_HIDDEN_WARP_NPC" };

        /// <param name="textOf">The text of a file relative to <c>luafiles514/lua files/</c>; null when it is missing or could not be decompiled.</param>
        public static (CompositionTablesData data, IReadOnlyList<RoCompositionGap> gaps) Build(RoCatalogTables tables, Func<string, string> textOf)
        {
            var b = new Builder(tables, textOf ?? (_ => null));
            b.Run();
            return (b.Data, b.Gaps);
        }

        sealed class Builder
        {
            readonly RoCatalogTables _tables;
            readonly Func<string, string> _textOf;
            public readonly CompositionTablesData Data = new CompositionTablesData();
            public readonly List<RoCompositionGap> Gaps = new List<RoCompositionGap>();

            public Builder(RoCatalogTables tables, Func<string, string> textOf)
            {
                _tables = tables;
                _textOf = textOf;
            }

            public void Run()
            {
                Priorities();
                TopLayer();
                LayerJobs();
                LayerSwaps();
                HeadToBody();
                Riding();
                Offsets();
                Transparency();
                Shadows();
                Hidden();
                WeaponTypes();
            }

            // ---- reading ----

            bool TryChunk(string file, IReadOnlyDictionary<string, RoLuaValue> imports, out RoLuaChunk chunk, string before = null)
            {
                chunk = null;
                string text = _textOf(file);
                if (text == null)
                {
                    Gaps.Add(new RoCompositionGap(file, true, "the table file is missing or could not be decompiled; its rules are left out"));
                    return false;
                }
                if (RoLuaReader.TryRead(before == null ? text : before + "\n" + text, null, imports, out chunk, out string reason)) return true;
                Gaps.Add(new RoCompositionGap(file, false, reason));
                return false;
            }

            bool TryTable(string file, string name, IReadOnlyDictionary<string, RoLuaValue> imports, out RoLuaTable table)
            {
                table = null;
                if (!TryChunk(file, imports, out var chunk)) return false;
                if (chunk.TryGetTable(name, out table)) return true;
                Gaps.Add(new RoCompositionGap(file, false, $"no table '{name}'"));
                return false;
            }

            static RoLuaTable Constants(params IReadOnlyDictionary<string, int>[] maps)
            {
                var t = new RoLuaTable();
                foreach (var map in maps)
                    if (map != null)
                        foreach (var kv in map)
                            if (!t.TryGet(kv.Key, out _)) t.Set(kv.Key, new RoLuaNumber(kv.Value));
                return t;
            }

            // jobtbl and JTtbl as the client has them: JTtbl also answers the NPC constants (shadowtable uses JTtbl.JT_EDDGA3).
            Dictionary<string, RoLuaValue> JobImports()
            {
                var npc = _tables?.Jobs?.NpcConstants;
                var player = _tables?.Jobs?.PlayerConstants;
                return new Dictionary<string, RoLuaValue>(StringComparer.Ordinal) { ["jobtbl"] = Constants(npc), ["JTtbl"] = Constants(player, npc) };
            }

            static bool Int(RoLuaValue v, out int value)
            {
                value = 0;
                if (v == null || !v.TryGetInteger(out long n) || n < int.MinValue || n > int.MaxValue) return false;
                value = (int)n;
                return true;
            }

            void LeftOut(string file, int rows)
            {
                if (rows > 0) Gaps.Add(new RoCompositionGap(file, false, $"{rows} rows have another shape and are left out"));
            }

            // ---- tables ----

            void Priorities()
            {
                var imports = new Dictionary<string, RoLuaValue>(StringComparer.Ordinal) { ["ACCESSORY_IDs"] = Constants(_tables?.ViewIds?.HeadgearConstants) };
                if (!TryTable(PriorityFile, "TB_Layer_Priority", imports, out var t)) return;
                if (t.TryGet("Default_Top", out var v) && Int(v, out int n)) Data.DefaultTop = n;
                if (t.TryGet("Default_Mid", out v) && Int(v, out n)) Data.DefaultMid = n;
                if (t.TryGet("Default_Bottom", out v) && Int(v, out n)) Data.DefaultBottom = n;
                if (t.TryGet("Default_Robe", out v) && Int(v, out n)) Data.DefaultRobe = n;
                if (!t.TryGet("Items_List", out v) || !(v is RoLuaTable items)) return;

                var rows = new List<HeadgearPriority>();
                var directions = new List<int>();
                int leftOut = 0;
                foreach (var kv in items.Indexed.OrderBy(k => k.Key))
                {
                    if (!(kv.Value is RoLuaTable item)) { leftOut++; continue; }
                    var p = new HeadgearPriority { ViewId = (int)kv.Key, FirstDirection = -1 };
                    if (item.TryGet("Default", out v) && Int(v, out n)) p.Default = n;
                    else p.NoDefault = true;
                    p.IgnoreRiding = item.TryGet("isIgnoreRiding", out v) && v is RoLuaBoolean ride && ride.Value;
                    if (item.TryGet("Direction", out v) && v is RoLuaTable d)
                    {
                        var values = new int[8];
                        bool any = false;
                        for (int i = 0; i < 8; i++)
                        {
                            values[i] = CompositionTables.NoPriority;
                            if (!d.Indexed.TryGetValue(i, out var dv) || !Int(dv, out n)) continue;
                            values[i] = n;
                            any = true;
                        }
                        if (any)
                        {
                            p.FirstDirection = directions.Count;
                            directions.AddRange(values);
                        }
                    }
                    rows.Add(p);
                }
                LeftOut(PriorityFile, leftOut);
                Data.Priorities = rows.ToArray();
                Data.DirectionPriorities = directions.ToArray();
            }

            void TopLayer()
            {
                var imports = new Dictionary<string, RoLuaValue>(StringComparer.Ordinal) { ["SPRITE_ROBE_IDs"] = Constants(_tables?.ViewIds?.GarmentConstants) };
                if (!TryTable(RobeNameFile, "RobeTopLayer", imports, out var t)) return;
                Data.RobeTopLayer = t.Items.Concat(t.Indexed.Values).Where(x => Int(x, out _)).Select(x => { Int(x, out int id); return id; }).Distinct().OrderBy(id => id).ToArray();
            }

            void LayerJobs()
            {
                // JOBID is defined by the skill inheritance file.
                if (!TryChunk(JobInheritFile, null, out var ids)) return;
                var imports = JobImports();
                foreach (var kv in ids.Globals) imports[kv.Key] = kv.Value;
                if (!TryTable(LayerFunctionsFile, "LAYER_ALL_TABLE_LIST", imports, out var t)) return;
                var map = new SortedDictionary<int, int>();
                foreach (var kv in t.Indexed)
                    if (Int(kv.Value, out int to) && to != kv.Key) map[(int)kv.Key] = to;
                Data.LayerJobs = map.Keys.ToArray();
                Data.LayerJobBases = map.Values.ToArray();
            }

            void LayerSwaps()
            {
                var rows = new SortedDictionary<int, ulong>();
                var jobs = new SortedSet<int>();
                Read(LayerFemaleFile, "_New_BigLayerDir_F", AccountSex.SEX_FEMALE);
                Read(LayerMaleFile, "_New_BigLayerDir_M", AccountSex.SEX_MALE);
                Data.LayerSwaps = rows.Select(r => new LayerSwap { Key = r.Key, Behind = r.Value }).ToArray();
                Data.LayerSwapJobs = jobs.ToArray();

                void Read(string file, string name, AccountSex sex)
                {
                    if (!TryTable(file, name, null, out var t)) return;
                    int leftOut = 0;
                    foreach (var job in t.Indexed)
                    {
                        if (!(job.Value is RoLuaTable actions)) { leftOut++; continue; }
                        jobs.Add(((int)job.Key << 1) | (int)sex);
                        foreach (var action in actions.Indexed)
                        {
                            if (!(action.Value is RoLuaTable frames) || action.Key < 0 || action.Key > 127) { leftOut++; continue; }
                            ulong mask = 0;
                            foreach (var f in frames.Items)
                                if (Int(f, out int frame) && frame >= 0 && frame < 64) mask |= 1UL << frame;
                                else leftOut++;
                            rows[LayerSwap.KeyOf((int)job.Key, sex, (int)action.Key)] = mask;
                        }
                    }
                    LeftOut(file, leftOut);
                }
            }

            void HeadToBody()
            {
                if (!TryTable(HeadToBodyFile, "_2dlayertoHead_list", null, out var t)) return;
                var rows = new SortedDictionary<long, ulong>();
                int leftOut = 0;
                foreach (var item in t.Items)
                {
                    if (!(item is RoLuaTable r) || r.Items.Count < 5 || !Int(r.Items[0], out int sex) || !Int(r.Items[1], out int job)
                        || !Int(r.Items[2], out int garment) || !Int(r.Items[3], out int action) || !Int(r.Items[4], out int frame)) { leftOut++; continue; }
                    if (sex != 0 && sex != 1) continue; // the terminator row {10, 0, 0, 0, 0}
                    if (frame < 0 || frame > 63 || action < 0 || action > 127) { leftOut++; continue; }
                    long key = HeadToBodyRow.KeyOf(job, (AccountSex)sex, garment, action);
                    rows.TryGetValue(key, out ulong mask);
                    rows[key] = mask | (1UL << frame);
                }
                LeftOut(HeadToBodyFile, leftOut);
                Data.HeadToBody = rows.Select(r => new HeadToBodyRow { Key = r.Key, Frames = r.Value }).ToArray();
            }

            void Riding()
            {
                if (!TryChunk(PcIdentityFile, null, out var ids)) return;
                if (!TryChunk(RidingFile, ids.Globals, out var chunk)) return;
                Data.HeadEdits = Edits("SetHeadSprEditInfo");
                Data.BodyEdits = Edits("SetBodySprEditInfo");

                SprEdit[] Edits(string name)
                {
                    var list = new List<SprEdit>();
                    foreach (var c in chunk.Calls)
                        if (c.Name == name && c.TryGetInteger(0, out int job) && c.TryGetInteger(1, out int a0) && c.TryGetInteger(2, out int a1)
                            && c.TryGetInteger(3, out int f0) && c.TryGetInteger(4, out int f1) && c.TryGetInteger(5, out int x) && c.TryGetInteger(6, out int y))
                            list.Add(new SprEdit { Job = job, FirstAction = a0, LastAction = a1, FirstFrame = f0, LastFrame = f1, Dx = x, Dy = y });
                    return list.OrderBy(e => e.Job).ToArray(); // stable: file order inside a job
                }
            }

            void Offsets()
            {
                string functions = _textOf(OffsetFunctionsFile);
                if (functions == null)
                {
                    Gaps.Add(new RoCompositionGap(OffsetFunctionsFile, true, "the table file is missing or could not be decompiled; its rules are left out"));
                    return;
                }
                // The client loads both files into one state.
                if (!TryChunk(OffsetFile, null, out var chunk, functions)) return;
                var list = new List<ItemOffset>();
                int leftOut = 0;
                foreach (var c in chunk.Calls)
                {
                    if (c.Name != "OffsetItemPos.AddOffsetForDoram") continue;
                    if (!c.TryGetInteger(0, out int view) || !c.TryGetInteger(1, out int direction) || c.Arguments.Count < 3 || !(c.Arguments[2] is RoLuaTable offset)) { leftOut++; continue; }
                    c.TryGetInteger(3, out int sex);
                    list.Add(new ItemOffset
                    {
                        ViewId = view, Direction = direction, Race = SpriteRace.Doram, Sex = sex,
                        Dx = offset.TryGet("x", out var x) && Int(x, out int dx) ? dx : 0,
                        Dy = offset.TryGet("y", out var y) && Int(y, out int dy) ? dy : 0,
                    });
                }
                LeftOut(OffsetFile, leftOut);
                Data.ItemOffsets = list.OrderBy(o => o.ViewId).ToArray();
            }

            void Transparency()
            {
                if (!TryTable(TransparentFile, "transparentItemlist", null, out var t)) return;
                var rows = new SortedDictionary<int, ItemTint>();
                int leftOut = 0;
                foreach (var item in t.Items)
                {
                    if (!(item is RoLuaTable r) || r.Items.Count < 4 || !Int(r.Items[0], out int id) || !Int(r.Items[1], out int a) || !Int(r.Items[2], out int b)) { leftOut++; continue; }
                    if (id <= 0 || (a >= 255 && b >= 255)) continue; // the terminator, and the opaque rows
                    rows[id] = new ItemTint { ViewId = id, Tint = new Color32(255, 255, 255, (byte)Mathf.Clamp((a + b) / 2, 0, 255)) };
                }
                LeftOut(TransparentFile, leftOut);
                Data.Transparency = rows.Values.ToArray();
            }

            void Shadows()
            {
                if (!TryTable(ShadowFile, "ShadowFactorTable", JobImports(), out var t)) return;
                var rows = new SortedDictionary<int, float>();
                foreach (var kv in t.Indexed)
                    if (kv.Value.TryGetNumber(out double scale)) rows[(int)kv.Key] = (float)scale;
                Data.ShadowJobIds = rows.Keys.ToArray();
                Data.ShadowScales = rows.Values.ToArray();
            }

            void Hidden()
            {
                var jobs = _tables?.Jobs;
                if (jobs == null) return;
                var ids = new SortedSet<int>();
                foreach (string c in HiddenConstants)
                    if (jobs.NpcConstants.TryGetValue(c, out int id) || jobs.PlayerConstants.TryGetValue(c, out id)) ids.Add(id);
                foreach (var kv in jobs.SpriteNames)
                    if (string.Equals(kv.Value, InvisibleName, StringComparison.OrdinalIgnoreCase)) ids.Add(kv.Key);
                Data.HiddenJobIds = ids.ToArray();
            }

            // SPEC-0011 FR-4, FR-14 (PLAN-0011 4.7): weapontable.lub's Expansion_Weapon_IDs, read with the catalog's view ids.
            void WeaponTypes()
            {
                var map = _tables?.ViewIds?.WeaponBaseTypes;
                if (map == null) return;
                var rows = new SortedDictionary<int, int>();
                foreach (var kv in map)
                    if (kv.Key != kv.Value) rows[kv.Key] = kv.Value;
                Data.WeaponViewIds = rows.Keys.ToArray();
                Data.WeaponBaseTypes = rows.Values.ToArray();
            }
        }
    }
}
