using System;
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

        public bool TryGet(PlayerJob job, out SpriteRace race, out PlayerJob baseJob) => throw new NotImplementedException("pending T3.4");

        /// <summary>The race of <paramref name="job"/>; <see cref="SpriteRace.Human"/> for a job the table does not list.</summary>
        public SpriteRace RaceOf(PlayerJob job) => throw new NotImplementedException("pending T3.4");
    }
}
