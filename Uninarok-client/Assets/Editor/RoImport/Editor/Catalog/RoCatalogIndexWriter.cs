using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Uninarok.Catalog;
using Uninarok.Networking.Data;
using Uninarok.RoImport.Core.Catalog.Planning;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.RoImport.Core.Output;

namespace Uninarok.RoImport.Editor.Catalog
{
    /// <summary>
    /// Builds the index of a category, the item table and the class table (SPEC-0008 FR-1 to FR-8; PLAN-0008 4.5, 4.7) from the
    /// run's plan, the outputs its records produced and the tables. References come from output paths (GUIDs, no asset
    /// loaded); arrays are sorted so the file is deterministic and an unchanged index is not rewritten.
    /// </summary>
    internal static class RoCatalogIndexWriter
    {
        /// <summary>The records of a category that produced outputs in this run (a failed record has none).</summary>
        public static List<(RoCatalogRecord record, RoCatalogOutput[] outputs)> Converted(RoCatalogRun run, SpriteCatalogCategory category) =>
            run.Plan.RecordsOf(category)
                .Select(r => (r, run.TryGetOutputs(RoCatalogRun.JobPath(category, r.Key), out var o) ? o : null))
                .Where(x => x.Item2 != null).ToList();

        public static ScriptableObject Build(RoCatalogRun run, SpriteCatalogCategory category, RoOutputRoots roots)
        {
            var records = Converted(run, category);
            ScriptableObject index;
            switch (category)
            {
                case SpriteCatalogCategory.Monsters:
                case SpriteCatalogCategory.Npcs:
                case SpriteCatalogCategory.Homunculi: index = Units(category, records); break;
                case SpriteCatalogCategory.GroundItems: index = Ground(records); break;
                case SpriteCatalogCategory.SkillIcons: index = Skills(records); break;
                case SpriteCatalogCategory.Effects: index = Effects(records); break;
                case SpriteCatalogCategory.Bodies: index = Bodies(records, roots); break;
                case SpriteCatalogCategory.Heads: index = Heads(records, roots); break;
                case SpriteCatalogCategory.Headgears:
                case SpriteCatalogCategory.Garments:
                case SpriteCatalogCategory.Weapons:
                case SpriteCatalogCategory.Shields: index = Equipment(category, records, roots); break;
                default: throw new ArgumentException($"{category} has no index", nameof(category));
            }
            index.name = RoCatalogOutputPaths.IndexName;
            return index;
        }

        static UnitIndex Units(SpriteCatalogCategory category, List<(RoCatalogRecord record, RoCatalogOutput[] outputs)> records)
        {
            var rows = records.Select(x => new UnitRecord
                {
                    JobId = Int(x.record.Value("jobId") ?? x.record.Key),
                    Name = x.record.Value("name") ?? string.Empty,
                    Sprite = SpriteOf(x.outputs.FirstOrDefault()),
                    Scale = float.TryParse(x.record.Value("scale"), NumberStyles.Float, CultureInfo.InvariantCulture, out float s) ? s : 1f,
                })
                .OrderBy(r => r.JobId).ToArray();
            var i = ScriptableObject.CreateInstance<UnitIndex>();
            i.Category = category;
            i.JobIds = rows.Select(r => r.JobId).ToArray();
            i.Records = rows;
            return i;
        }

        static GroundItemIndex Ground(List<(RoCatalogRecord record, RoCatalogOutput[] outputs)> records)
        {
            var rows = records.OrderBy(x => x.record.Key, StringComparer.Ordinal).ToArray();
            var i = ScriptableObject.CreateInstance<GroundItemIndex>();
            i.ResourceNames = rows.Select(x => x.record.Key).ToArray();
            i.Resources = rows.Select(x => SpriteOf(x.outputs.FirstOrDefault())).ToArray();
            return i;
        }

        static SkillIconIndex Skills(List<(RoCatalogRecord record, RoCatalogOutput[] outputs)> records)
        {
            var rows = records.Select(x => (id: Int(x.record.Key), sprite: SpriteOf(x.outputs.FirstOrDefault()))).OrderBy(x => x.id).ToArray();
            var i = ScriptableObject.CreateInstance<SkillIconIndex>();
            i.SkillIds = rows.Select(x => x.id).ToArray();
            i.Icons = rows.Select(x => x.sprite).ToArray();
            return i;
        }

        static EffectIndex Effects(List<(RoCatalogRecord record, RoCatalogOutput[] outputs)> records)
        {
            var rows = records.OrderBy(x => x.record.Key, StringComparer.Ordinal).ToArray();
            var i = ScriptableObject.CreateInstance<EffectIndex>();
            i.Names = rows.Select(x => x.record.Key).ToArray();
            i.Effects = rows.Select(x => SpriteOf(x.outputs.FirstOrDefault())).ToArray();
            var set = rows.FirstOrDefault(x => x.record.Key == RoEffectPlanner.EmoteSet);
            if (set.record != null)
            {
                i.EmoteSet = SpriteOf(set.outputs.FirstOrDefault());
                // Action = emote id (Candidate, sprite-catalog.md §3.10).
                var emotes = set.record.Values.Where(v => v.Name.StartsWith(RoEffectPlanner.EmoteValuePrefix, StringComparison.Ordinal))
                    .Select(v => Int(v.Text)).Distinct().OrderBy(id => id).ToArray();
                i.EmoteIds = emotes;
                i.EmoteActions = emotes.ToArray();
            }
            return i;
        }

        static BodyIndex Bodies(List<(RoCatalogRecord record, RoCatalogOutput[] outputs)> records, RoOutputRoots roots)
        {
            var jobs = new SortedDictionary<int, BodyEntryReference>();
            foreach (var (record, _) in records)
            {
                string guid = AssetDatabase.AssetPathToGUID(RoCatalogOutputPaths.Entry(roots, record.Category, record.Key));
                foreach (var pair in record.Pairs)
                {
                    var parts = pair.Role.Split(':');
                    if (parts.Length > 1 && Enum.TryParse(parts[1], out PlayerJob job) && !jobs.ContainsKey((int)job))
                        jobs.Add((int)job, new BodyEntryReference(guid));
                }
            }
            var i = ScriptableObject.CreateInstance<BodyIndex>();
            i.Jobs = jobs.Keys.Select(j => (PlayerJob)j).ToArray();
            i.Entries = jobs.Values.ToArray();
            return i;
        }

        static HeadIndex Heads(List<(RoCatalogRecord record, RoCatalogOutput[] outputs)> records, RoOutputRoots roots)
        {
            var rows = records.Select(x => (style: Int(x.record.Value("style")), guid: AssetDatabase.AssetPathToGUID(RoCatalogOutputPaths.Entry(roots, x.record.Category, x.record.Key))))
                .OrderBy(x => x.style).ToArray();
            var i = ScriptableObject.CreateInstance<HeadIndex>();
            i.Styles = rows.Select(x => x.style).ToArray();
            i.Entries = rows.Select(x => new HeadEntryReference(x.guid)).ToArray();
            return i;
        }

        static EquipmentIndex Equipment(SpriteCatalogCategory category, List<(RoCatalogRecord record, RoCatalogOutput[] outputs)> records, RoOutputRoots roots)
        {
            var byView = new SortedDictionary<int, EquipmentEntryReference>();
            var byItem = new SortedDictionary<int, EquipmentEntryReference>();
            foreach (var (record, _) in records)
            {
                var reference = new EquipmentEntryReference(AssetDatabase.AssetPathToGUID(RoCatalogOutputPaths.Entry(roots, category, record.Key)));
                if (record.Value("viewId") is string v) byView[Int(v)] = reference;
                else if (record.Value("itemId") is string item) byItem[Int(item)] = reference;
            }
            var i = ScriptableObject.CreateInstance<EquipmentIndex>();
            i.Kind = RoCatalogEntryWriter.KindOf(category);
            i.ViewIds = byView.Keys.ToArray();
            i.ByView = byView.Values.ToArray();
            i.ItemIds = byItem.Keys.ToArray();
            i.ByItem = byItem.Values.ToArray();
            return i;
        }

        /// <summary>
        /// What each item draws (PLAN-0008 4.5, D21): view id = <c>ClassNum</c>, kind from the server's item database, and the
        /// positions of its identified and unidentified resources in <paramref name="groundNames"/> (the ground item index).
        /// </summary>
        public static ItemSpriteTable Items(RoItemTable items, RoItemDb kinds, IReadOnlyList<string> groundNames)
        {
            var positions = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int p = 0; p < (groundNames?.Count ?? 0); p++) positions[groundNames[p]] = p;
            int Slot(string resource)
            {
                string key = resource == null ? null : RoGroundItemPlanner.KeyOf(resource);
                return key != null && positions.TryGetValue(key, out int p) ? p : -1;
            }
            var ids = items.ItemIds.OrderBy(id => id).ToArray();
            var t = ScriptableObject.CreateInstance<ItemSpriteTable>();
            t.ItemIds = ids;
            t.ViewIds = new int[ids.Length];
            t.Kinds = new byte[ids.Length];
            t.GroundSlot = new int[ids.Length];
            t.UnidentifiedGroundSlot = new int[ids.Length];
            for (int n = 0; n < ids.Length; n++)
            {
                items.TryGet(ids[n], out var info);
                t.ViewIds[n] = info.ClassNum;
                t.Kinds[n] = (byte)(kinds?.KindOf(ids[n]) ?? EquipmentKind.None);
                t.GroundSlot[n] = Slot(info.IdentifiedResource);
                t.UnidentifiedGroundSlot[n] = Slot(info.UnidentifiedResource);
            }
            t.name = RoCatalogOutputPaths.IndexName;
            return t;
        }

        /// <summary>The player classes of <c>class-bodies.csv</c> (PLAN-0008 4.5): one row per job, the table's first, sorted by job.</summary>
        public static ClassTable Classes(RoOwnTables own)
        {
            var rows = new SortedDictionary<int, RoClassBody>();
            foreach (var b in own.Bodies)
                if (!rows.ContainsKey((int)b.Job)) rows.Add((int)b.Job, b);
            var t = ScriptableObject.CreateInstance<ClassTable>();
            t.Jobs = rows.Values.Select(b => b.Job).ToArray();
            t.Races = rows.Values.Select(b => b.Doram ? SpriteRace.Doram : SpriteRace.Human).ToArray();
            t.BaseJobs = rows.Values.Select(b => b.BaseJob).ToArray();
            t.BodyNames = rows.Values.Select(b => b.BodyName).ToArray();
            t.name = RoCatalogOutputPaths.IndexName;
            return t;
        }

        static SpriteRef SpriteOf(RoCatalogOutput o)
        {
            if (o == null || string.IsNullOrEmpty(o.AssetPath)) return default;
            string guid = AssetDatabase.AssetPathToGUID(o.AssetPath);
            return o.IsStill
                ? new SpriteRef { Still = new AssetReferenceSprite(guid) { SubObjectName = o.SubAsset } }
                : new SpriteRef { Animation = new SpriteAnimationReference(guid) };
        }

        static int Int(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
    }
}
