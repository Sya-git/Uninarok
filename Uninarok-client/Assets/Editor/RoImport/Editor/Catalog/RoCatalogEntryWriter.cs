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

namespace Uninarok.RoImport.Editor.Catalog
{
    /// <summary>
    /// Builds the entry asset of a body, head or equipment record (SPEC-0008 FR-2 to FR-4, FR-8; PLAN-0008 4.4, 4.6) from the
    /// record's roles and values and the outputs its pairs produced. Every reference is a typed Addressables reference made
    /// from an output path's GUID: no asset is loaded. The importer writes the result in place (GUID kept).
    /// </summary>
    internal static class RoCatalogEntryWriter
    {
        public static ScriptableObject Build(RoCatalogRecord record, IReadOnlyList<RoCatalogOutput> outputs)
        {
            var byRole = outputs.Where(o => o.Role != null).GroupBy(o => o.Role, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            ScriptableObject entry;
            switch (record.Category)
            {
                case SpriteCatalogCategory.Bodies: entry = Body(record, byRole); break;
                case SpriteCatalogCategory.Heads: entry = Head(record, byRole); break;
                default: entry = Equipment(record, byRole); break;
            }
            var animated = outputs.Where(o => o.Role != null && !o.Role.StartsWith(RoCatalogRoles.PalettePrefix, StringComparison.Ordinal)).ToList();
            int actions = animated.Sum(o => o.Actions);
            int sheets = animated.Select(o => o.SheetPath).Where(s => s != null).Distinct(StringComparer.Ordinal).Count();
            switch (entry)
            {
                case BodyEntry b: b.StoredActions = actions; b.SheetCount = sheets; break;
                case HeadEntry h: h.StoredActions = actions; h.SheetCount = sheets; break;
                case EquipmentEntry e: e.StoredActions = actions; e.SheetCount = sheets; break;
            }
            entry.name = record.Key;
            return entry;
        }

        static BodyEntry Body(RoCatalogRecord record, Dictionary<string, RoCatalogOutput> outputs)
        {
            var e = ScriptableObject.CreateInstance<BodyEntry>();
            e.BaseJob = Enum.TryParse(record.Value("baseJob"), out PlayerJob baseJob) ? baseJob : PlayerJob.JOB_NOVICE;
            var variants = new List<BodyEntry.BodyVariant>();
            foreach (var pair in record.Pairs)
            {
                var parts = pair.Role.Split(':');
                if (parts.Length != 5 || !Enum.TryParse(parts[1], out PlayerJob job) || !Enum.TryParse(parts[4], out BodyVariantKind kind)) continue;
                variants.Add(new BodyEntry.BodyVariant
                {
                    Name = record.Value(RoCatalogRoles.Name(pair.Role)) ?? string.Empty,
                    Job = job,
                    Sex = SexOf(parts[2]),
                    Race = RaceOf(parts[3]),
                    Kind = kind,
                    Sprite = SpriteOf(outputs, pair.Role),
                    Palettes = PalettesOf(outputs, pair.Role),
                });
            }
            e.Variants = variants.ToArray();
            return e;
        }

        static HeadEntry Head(RoCatalogRecord record, Dictionary<string, RoCatalogOutput> outputs)
        {
            var e = ScriptableObject.CreateInstance<HeadEntry>();
            e.Style = int.TryParse(record.Value("style"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int style) ? style : 0;
            e.Variants = record.Pairs.Select(p => p.Role.Split(':')).Where(parts => parts.Length == 3)
                .Select(parts => string.Join(":", parts))
                .Select(role =>
                {
                    var parts = role.Split(':');
                    return new HeadEntry.HeadVariant { Sex = SexOf(parts[1]), Race = RaceOf(parts[2]), Sprite = SpriteOf(outputs, role), Palettes = PalettesOf(outputs, role) };
                }).ToArray();
            return e;
        }

        static EquipmentEntry Equipment(RoCatalogRecord record, Dictionary<string, RoCatalogOutput> outputs)
        {
            var e = ScriptableObject.CreateInstance<EquipmentEntry>();
            e.Kind = KindOf(record.Category);
            e.ViewId = int.TryParse(record.Value("viewId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int view) ? view : -1;
            e.ItemId = int.TryParse(record.Value("itemId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int item) ? item : 0;
            e.Name = record.Value("name") ?? record.Key;

            var slots = new List<EquipmentEntry.EquipmentSlot>();
            var trails = new List<SpriteAnimationReference>();
            var trailRoles = record.Pairs.Where(p => p.Role.StartsWith(RoCatalogRoles.Trail + ":", StringComparison.Ordinal))
                .ToDictionary(p => p.Role.Substring(RoCatalogRoles.Trail.Length + 1), p => p.Role, StringComparer.Ordinal);
            foreach (var pair in record.Pairs)
            {
                if (!RoCatalogRoles.TryParse(pair.Role, out string kind, out string job, out string sex, out string race, out _) || kind == RoCatalogRoles.Trail) continue;
                if (!outputs.TryGetValue(pair.Role, out var output) || output.IsStill) continue;    // equipment is animated (D16)
                var animation = new SpriteAnimationReference(AssetDatabase.AssetPathToGUID(output.AssetPath));
                int fallbackOf = int.TryParse(record.Value(RoCatalogRoles.Fallback(pair.Role)), NumberStyles.Integer, CultureInfo.InvariantCulture, out int f) ? f : -1;
                bool anyClass = job == RoCatalogRoles.Any;
                string slotKey = job + ":" + sex + ":" + race;
                var trail = trailRoles.TryGetValue(slotKey, out string trailRole) && outputs.TryGetValue(trailRole, out var t) && !t.IsStill
                    ? new SpriteAnimationReference(AssetDatabase.AssetPathToGUID(t.AssetPath))
                    : new SpriteAnimationReference(string.Empty);
                // A root slot for any sex is two slots, one per sex.
                var sexes = sex == RoCatalogRoles.Any ? new[] { AccountSex.SEX_MALE, AccountSex.SEX_FEMALE } : new[] { SexOf(sex) };
                foreach (var s in sexes)
                {
                    slots.Add(new EquipmentEntry.EquipmentSlot
                    {
                        AnyClass = anyClass,
                        Job = anyClass ? default : (Enum.TryParse(job, out PlayerJob j) ? j : default),
                        Sex = s,
                        Race = RaceOf(race),
                        FallbackOf = fallbackOf,
                        Animation = animation,
                    });
                    trails.Add(trail);
                }
            }
            e.Slots = slots.ToArray();
            e.Trails = e.Kind == EquipmentKind.Weapon ? trails.ToArray() : Array.Empty<SpriteAnimationReference>();
            return e;
        }

        static SpriteRef SpriteOf(Dictionary<string, RoCatalogOutput> outputs, string role)
        {
            if (!outputs.TryGetValue(role, out var o)) return default;
            string guid = AssetDatabase.AssetPathToGUID(o.AssetPath);
            return o.IsStill
                ? new SpriteRef { Still = new AssetReferenceSprite(guid) { SubObjectName = o.SubAsset } }
                : new SpriteRef { Animation = new SpriteAnimationReference(guid) };
        }

        /// <summary>The palettes of a role (<c>palette:&lt;role&gt;:&lt;colour&gt;</c>), sorted by colour.</summary>
        static PaletteSet PalettesOf(Dictionary<string, RoCatalogOutput> outputs, string role)
        {
            string prefix = RoCatalogRoles.PalettePrefix + role + ":";
            var colours = outputs.Where(o => o.Key.StartsWith(prefix, StringComparison.Ordinal))
                .Select(o => (ok: int.TryParse(o.Key.Substring(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int c), colour: c, path: o.Value.AssetPath))
                .Where(x => x.ok && !string.IsNullOrEmpty(x.path))
                .OrderBy(x => x.colour)
                .Select(x => new PaletteRef { Colour = x.colour, Palette = new PaletteReference(AssetDatabase.AssetPathToGUID(x.path)) })
                .ToArray();
            return new PaletteSet { Colours = colours };
        }

        public static EquipmentKind KindOf(SpriteCatalogCategory category)
        {
            switch (category)
            {
                case SpriteCatalogCategory.Headgears: return EquipmentKind.Headgear;
                case SpriteCatalogCategory.Garments: return EquipmentKind.Garment;
                case SpriteCatalogCategory.Weapons: return EquipmentKind.Weapon;
                case SpriteCatalogCategory.Shields: return EquipmentKind.Shield;
                default: return EquipmentKind.None;
            }
        }

        static AccountSex SexOf(string sex) => sex == "female" ? AccountSex.SEX_FEMALE : AccountSex.SEX_MALE;
        static SpriteRace RaceOf(string race) => race == "doram" ? SpriteRace.Doram : SpriteRace.Human;
    }
}
