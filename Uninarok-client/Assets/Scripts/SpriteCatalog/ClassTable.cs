using System;
using System.Collections.Generic;
using UnityEngine;
using Uninarok.Networking.Data;

namespace Uninarok.Catalog
{
    /// <summary>
    /// The player classes as the sprites see them (SPEC-0008 FR-2, FR-4; PLAN-0008 4.2, 4.5): for each <see cref="PlayerJob"/>
    /// its race, base job and body name, from <c>class-bodies.csv</c>. Parallel arrays sorted by job.
    /// </summary>
    public sealed class ClassTable : ScriptableObject
    {
        public PlayerJob[] Jobs = Array.Empty<PlayerJob>();
        public SpriteRace[] Races = Array.Empty<SpriteRace>();
        public PlayerJob[] BaseJobs = Array.Empty<PlayerJob>();
        public string[] BodyNames = Array.Empty<string>();

        public bool TryGet(PlayerJob job, out SpriteRace race, out PlayerJob baseJob)
        {
            if (_positions == null) Build();
            if (_positions.TryGetValue((int)job, out int i))
            {
                race = Races[i];
                baseJob = BaseJobs[i];
                return true;
            }
            race = SpriteRace.Human;
            baseJob = job;
            return false;
        }

        /// <summary>The race of <paramref name="job"/>; <see cref="SpriteRace.Human"/> for a job the table does not list.</summary>
        public SpriteRace RaceOf(PlayerJob job) => TryGet(job, out var race, out _) ? race : SpriteRace.Human;

        // Built on the first lookup (D16); not serialized.
        [NonSerialized] Dictionary<int, int> _positions;

        void Build()
        {
            var jobs = Jobs ?? Array.Empty<PlayerJob>();
            int n = Math.Min(jobs.Length, Math.Min(Races?.Length ?? 0, BaseJobs?.Length ?? 0));
            var positions = new Dictionary<int, int>(n);
            for (int i = 0; i < n; i++) positions.TryAdd((int)jobs[i], i);
            _positions = positions;
        }

        void OnValidate() => _positions = null;
    }
}
