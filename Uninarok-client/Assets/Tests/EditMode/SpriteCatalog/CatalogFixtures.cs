using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Uninarok.Networking.Data;

namespace Uninarok.Catalog.Tests
{
    /// <summary>Hand-built references, entries and tables for the lookup tests (PLAN-0008 §6): no asset is loaded.</summary>
    static class CatalogFixtures
    {
        /// <summary>A 32-digit GUID made from a number, so a test can tell which reference came back.</summary>
        public static string Guid(int n) => n.ToString("x32");

        public static SpriteAnimationReference Animation(int n) => new SpriteAnimationReference(Guid(n));
        public static SpriteRef AnimationRef(int n) => new SpriteRef { Animation = Animation(n) };
        public static SpriteRef StillRef(int n) => new SpriteRef { Still = new AssetReferenceSprite(Guid(n)) { SubObjectName = "f0" } };

        public static EquipmentEntry.EquipmentSlot Slot(PlayerJob job, AccountSex sex, int animation) =>
            new EquipmentEntry.EquipmentSlot { Job = job, Sex = sex, Race = SpriteRace.Human, FallbackOf = -1, Animation = Animation(animation) };

        public static EquipmentEntry.EquipmentSlot AnyClass(SpriteRace race, AccountSex sex, int animation) =>
            new EquipmentEntry.EquipmentSlot { AnyClass = true, Sex = sex, Race = race, FallbackOf = -1, Animation = Animation(animation) };

        public static ClassTable Classes(List<Object> created, params (PlayerJob job, SpriteRace race, PlayerJob baseJob)[] rows)
        {
            var t = Make<ClassTable>(created);
            t.Jobs = new PlayerJob[rows.Length];
            t.Races = new SpriteRace[rows.Length];
            t.BaseJobs = new PlayerJob[rows.Length];
            t.BodyNames = new string[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                t.Jobs[i] = rows[i].job;
                t.Races[i] = rows[i].race;
                t.BaseJobs[i] = rows[i].baseJob;
                t.BodyNames[i] = rows[i].job.ToString();
            }
            return t;
        }

        public static T Make<T>(List<Object> created) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            created.Add(asset);
            return asset;
        }

        public static void DestroyAll(List<Object> created)
        {
            foreach (var o in created)
                if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }
    }
}
