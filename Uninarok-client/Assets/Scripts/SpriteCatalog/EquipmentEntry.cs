using System;
using System.Collections.Generic;
using UnityEngine;
using Uninarok.Networking.Data;

namespace Uninarok.Catalog
{
    /// <summary>
    /// One headgear, garment, weapon or shield (SPEC-0008 FR-3, FR-4; PLAN-0008 4.4, D9, D16): its animation per class and
    /// sex, already paired with the right sheet at import. Keyed by view id, or by item id when the client names the file by
    /// item. Written by the importer, read-only at run time.
    /// </summary>
    public sealed class EquipmentEntry : ScriptableObject
    {
        public EquipmentKind Kind;
        /// <summary>-1 when keyed by item id only.</summary>
        public int ViewId = -1;
        /// <summary>0 when keyed by view id.</summary>
        public int ItemId;
        public string Name;
        public EquipmentSlot[] Slots = Array.Empty<EquipmentSlot>();
        /// <summary>Weapons only, parallel to <see cref="Slots"/>; empty for other kinds. A slot without a trail has an empty reference.</summary>
        public SpriteAnimationReference[] Trails = Array.Empty<SpriteAnimationReference>();

        /// <summary>The import's numbers for this entry (PLAN-0008 4.7, D12): stored actions of its animations, distinct sheets.</summary>
        public int StoredActions;
        public int SheetCount;

        /// <summary>How many times the lookup dictionaries were built (once per loaded entry; tests).</summary>
        internal int DictionaryBuilds { get; private set; }

        /// <summary>
        /// The animation for <paramref name="job"/> and <paramref name="sex"/> (FR-4, D16): the slot for exactly (job, sex); else
        /// the any-class slot for (the job's race in <paramref name="classes"/>, sex); else the any-class human slot for the sex.
        /// <paramref name="trail"/> is the weapon trail of that slot, null when it has none. False when no slot applies: the
        /// equipment draws nothing. O(1) after the first call.
        /// </summary>
        public bool TryResolve(PlayerJob job, AccountSex sex, ClassTable classes, out SpriteAnimationReference animation, out SpriteAnimationReference trail)
        {
            if (_byJob == null) BuildDictionaries();
            SpriteRace race = classes != null ? classes.RaceOf(job) : SpriteRace.Human;
            if (_byJob.TryGetValue(JobKey(job, sex), out int slot)
                || _anyClass.TryGetValue(RaceKey(race, sex), out slot)
                || (race != SpriteRace.Human && _anyClass.TryGetValue(RaceKey(SpriteRace.Human, sex), out slot)))
            {
                animation = Slots[slot].Animation;
                trail = TrailOf(slot);
                return true;
            }
            animation = null;
            trail = null;
            return false;
        }

        // Built on the first lookup and kept for the life of the loaded entry (D16); not serialized.
        [NonSerialized] Dictionary<int, int> _byJob, _anyClass;

        static int JobKey(PlayerJob job, AccountSex sex) => ((ushort)job << 2) | (byte)sex;
        static int RaceKey(SpriteRace race, AccountSex sex) => ((byte)race << 2) | (byte)sex;

        void BuildDictionaries()
        {
            var slots = Slots ?? Array.Empty<EquipmentSlot>();
            var byJob = new Dictionary<int, int>(slots.Length);
            var anyClass = new Dictionary<int, int>(4);
            for (int i = 0; i < slots.Length; i++)
            {
                // The first slot of a key wins; the importer writes one slot per key.
                if (slots[i].AnyClass) anyClass.TryAdd(RaceKey(slots[i].Race, slots[i].Sex), i);
                else byJob.TryAdd(JobKey(slots[i].Job, slots[i].Sex), i);
            }
            _byJob = byJob;
            _anyClass = anyClass;
            DictionaryBuilds++;
        }

        SpriteAnimationReference TrailOf(int slot)
        {
            if (Kind != EquipmentKind.Weapon || Trails == null || slot >= Trails.Length) return null;
            var trail = Trails[slot];
            return trail != null && trail.RuntimeKeyIsValid() ? trail : null;
        }

        // An edit in the editor (the importer rewriting the entry) invalidates the dictionaries.
        void OnValidate()
        {
            _byJob = null;
            _anyClass = null;
        }

        [Serializable]
        public struct EquipmentSlot
        {
            /// <summary>The slot is for every class of <see cref="Race"/> and <see cref="Sex"/>; a flag, because <c>JOB_NOVICE</c> is 0.</summary>
            public bool AnyClass;
            public PlayerJob Job;
            public AccountSex Sex;
            public SpriteRace Race;
            /// <summary>The view id this slot stands in for, -1 when none (D9).</summary>
            public int FallbackOf;
            /// <summary>Every equipment sprite is animated, so no still reference is stored (D16).</summary>
            public SpriteAnimationReference Animation;
        }
    }
}
