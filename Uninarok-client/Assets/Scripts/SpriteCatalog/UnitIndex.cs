using System;
using UnityEngine;

namespace Uninarok.Catalog
{
    /// <summary>The monsters, NPCs or homunculi of one category (SPEC-0008 FR-1; PLAN-0008 4.5, D5): records sorted by job id.</summary>
    public sealed class UnitIndex : ScriptableObject
    {
        public SpriteCatalogCategory Category;
        public int[] JobIds = Array.Empty<int>();
        public UnitRecord[] Records = Array.Empty<UnitRecord>();

        /// <summary>Binary search over <see cref="JobIds"/>; the facade uses its own dictionary.</summary>
        public bool TryGet(int jobId, out UnitRecord record)
        {
            int i = JobIds == null ? -1 : Array.BinarySearch(JobIds, jobId);
            if (i >= 0 && Records != null && i < Records.Length)
            {
                record = Records[i];
                return true;
            }
            record = default;
            return false;
        }
    }
}
