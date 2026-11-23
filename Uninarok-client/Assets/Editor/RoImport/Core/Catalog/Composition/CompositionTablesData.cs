using System;
using Uninarok.Units;

namespace Uninarok.RoImport.Core.Catalog.Composition
{
    /// <summary>The composition tables as plain data (PLAN-0009 4.1): what <see cref="RoCompositionTablesBuilder"/> makes and the converter writes.</summary>
    public sealed class CompositionTablesData
    {
        public int DefaultTop = 200, DefaultMid = 100, DefaultBottom = 300, DefaultRobe = 400;
        public HeadgearPriority[] Priorities = Array.Empty<HeadgearPriority>();
        public int[] DirectionPriorities = Array.Empty<int>();
        public int[] RobeTopLayer = Array.Empty<int>();
        public LayerSwap[] LayerSwaps = Array.Empty<LayerSwap>();
        public int[] LayerSwapJobs = Array.Empty<int>();
        public int[] LayerJobs = Array.Empty<int>();
        public int[] LayerJobBases = Array.Empty<int>();
        public HeadToBodyRow[] HeadToBody = Array.Empty<HeadToBodyRow>();
        public SprEdit[] HeadEdits = Array.Empty<SprEdit>();
        public SprEdit[] BodyEdits = Array.Empty<SprEdit>();
        public ItemOffset[] ItemOffsets = Array.Empty<ItemOffset>();
        public ItemTint[] Transparency = Array.Empty<ItemTint>();
        public int[] ShadowJobIds = Array.Empty<int>();
        public float[] ShadowScales = Array.Empty<float>();
        public int[] HiddenJobIds = Array.Empty<int>();
        public int[] WeaponViewIds = Array.Empty<int>();
        public int[] WeaponBaseTypes = Array.Empty<int>();

        public void ApplyTo(CompositionTables t)
        {
            t.WeaponViewIds = WeaponViewIds;
            t.WeaponBaseTypes = WeaponBaseTypes;
            t.DefaultTop = DefaultTop;
            t.DefaultMid = DefaultMid;
            t.DefaultBottom = DefaultBottom;
            t.DefaultRobe = DefaultRobe;
            t.Priorities = Priorities;
            t.DirectionPriorities = DirectionPriorities;
            t.RobeTopLayer = RobeTopLayer;
            t.LayerSwaps = LayerSwaps;
            t.LayerSwapJobs = LayerSwapJobs;
            t.LayerJobs = LayerJobs;
            t.LayerJobBases = LayerJobBases;
            t.HeadToBody = HeadToBody;
            t.HeadEdits = HeadEdits;
            t.BodyEdits = BodyEdits;
            t.ItemOffsets = ItemOffsets;
            t.Transparency = Transparency;
            t.ShadowJobIds = ShadowJobIds;
            t.ShadowScales = ShadowScales;
            t.HiddenJobIds = HiddenJobIds;
        }
    }

    /// <summary>One problem of one table: the table stays empty or partial and the import reports one warning (PLAN-0009 4.1).</summary>
    public readonly struct RoCompositionGap
    {
        public RoCompositionGap(string file, bool missing, string message)
        {
            File = file;
            Missing = missing;
            Message = message;
        }

        /// <summary>The file, relative to <c>luafiles514/lua files/</c>.</summary>
        public string File { get; }
        /// <summary>The file is not there (else it could not be read, or rows were left out).</summary>
        public bool Missing { get; }
        public string Message { get; }
    }
}
