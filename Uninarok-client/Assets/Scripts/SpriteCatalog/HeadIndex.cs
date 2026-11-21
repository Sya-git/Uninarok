using System;
using UnityEngine;

namespace Uninarok.Catalog
{
    /// <summary>Head styles → their entries (SPEC-0008 FR-2; PLAN-0008 4.5). Sorted by style.</summary>
    public sealed class HeadIndex : ScriptableObject
    {
        public int[] Styles = Array.Empty<int>();
        public HeadEntryReference[] Entries = Array.Empty<HeadEntryReference>();
    }
}
