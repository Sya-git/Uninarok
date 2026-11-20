using System;
using UnityEngine;
using Uninarok.Networking.Data;

namespace Uninarok.Catalog
{
    /// <summary>Every job of every body variant → its class entry (SPEC-0008 FR-2; PLAN-0008 4.5). Sorted by job.</summary>
    public sealed class BodyIndex : ScriptableObject
    {
        public PlayerJob[] Jobs = Array.Empty<PlayerJob>();
        public BodyEntryReference[] Entries = Array.Empty<BodyEntryReference>();
    }
}
