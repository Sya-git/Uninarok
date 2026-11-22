using System;
using UnityEngine;
using Uninarok.Catalog;
using Uninarok.Networking.Data;

namespace Uninarok.Units
{
    /// <summary>A headgear's draw priority (<c>tb_layer_priority</c>, composition-reference.md 2.1).</summary>
    [Serializable]
    public struct HeadgearPriority
    {
        public int ViewId;
        public int Default;
        /// <summary>The item has no <c>Default</c>: only its directions, then the slot default.</summary>
        public bool NoDefault;
        /// <summary>The priority is not used while riding.</summary>
        public bool IgnoreRiding;
        /// <summary>First of 8 values in <see cref="CompositionTables.DirectionPriorities"/>, or −1.</summary>
        public int FirstDirection;
    }

    /// <summary>The frames of one (sex, job, file action) where the garment is behind the body (composition-reference.md 2.3).</summary>
    [Serializable]
    public struct LayerSwap
    {
        public int Key;
        /// <summary>Bit n: frame n.</summary>
        public ulong Behind;

        public static int KeyOf(int job, AccountSex sex, int fileAction) => (job << 8) | ((fileAction & 0x7F) << 1) | ((int)sex & 1);
    }

    /// <summary>The frames of one (sex, job, garment, file action) where the garment is under the head (composition-reference.md 2.4).</summary>
    [Serializable]
    public struct HeadToBodyRow
    {
        public long Key;
        /// <summary>Bit n: frame n.</summary>
        public ulong Frames;

        public static long KeyOf(int job, AccountSex sex, int garment, int fileAction) =>
            ((long)job << 28) | ((long)(garment & 0xFFFF) << 8) | (long)((fileAction & 0x7F) << 1) | ((long)sex & 1);
    }

    /// <summary>A riding or body correction (composition-reference.md 2.5), in pixels, Y down.</summary>
    [Serializable]
    public struct SprEdit
    {
        public int Job;
        public int FirstAction, LastAction;
        public int FirstFrame, LastFrame;
        public int Dx, Dy;
    }

    /// <summary>A headgear's offset by direction (composition-reference.md 2.6), in pixels, Y down.</summary>
    [Serializable]
    public struct ItemOffset
    {
        public int ViewId;
        public int Direction;
        public SpriteRace Race;
        /// <summary>0 both, 1 male, 2 female (the client's values).</summary>
        public int Sex;
        public int Dx, Dy;
    }

    /// <summary>A transparent garment (composition-reference.md 2.7).</summary>
    [Serializable]
    public struct ItemTint
    {
        public int ViewId;
        public Color32 Tint;
    }

    /// <summary>
    /// The client's composition tables (PLAN-0009 4.1; SPEC-0009 FR-3, FR-4, FR-11) in flat, sorted arrays read with binary
    /// search (D5): no allocation, no exception. Written by the Composition import, read-only at run time. An empty table
    /// means "no rule": the composer's defaults apply.
    /// </summary>
    public sealed class CompositionTables : ScriptableObject
    {
        public const int NoPriority = int.MinValue;

        public int DefaultTop = 200, DefaultMid = 100, DefaultBottom = 300, DefaultRobe = 400;
        /// <summary>Sorted by view id.</summary>
        public HeadgearPriority[] Priorities = Array.Empty<HeadgearPriority>();
        /// <summary>8 values per item that has directions; <see cref="NoPriority"/> where the direction has none.</summary>
        public int[] DirectionPriorities = Array.Empty<int>();
        /// <summary>Garment view ids of the top layer, sorted.</summary>
        public int[] RobeTopLayer = Array.Empty<int>();
        /// <summary>Sorted by key.</summary>
        public LayerSwap[] LayerSwaps = Array.Empty<LayerSwap>();
        /// <summary><c>job &lt;&lt; 1 | sex</c> of every job that has a layer table, sorted.</summary>
        public int[] LayerSwapJobs = Array.Empty<int>();
        /// <summary>Job inheritance of the layer tables: parallel, sorted by the first.</summary>
        public int[] LayerJobs = Array.Empty<int>();
        public int[] LayerJobBases = Array.Empty<int>();
        /// <summary>Sorted by key.</summary>
        public HeadToBodyRow[] HeadToBody = Array.Empty<HeadToBodyRow>();
        /// <summary>Sorted by job, then file order (a later call wins).</summary>
        public SprEdit[] HeadEdits = Array.Empty<SprEdit>();
        public SprEdit[] BodyEdits = Array.Empty<SprEdit>();
        /// <summary>Sorted by view id, then file order (the first match wins).</summary>
        public ItemOffset[] ItemOffsets = Array.Empty<ItemOffset>();
        /// <summary>Garments, sorted by view id.</summary>
        public ItemTint[] Transparency = Array.Empty<ItemTint>();
        /// <summary>Parallel, sorted by job id; a job id not listed has scale 1.</summary>
        public int[] ShadowJobIds = Array.Empty<int>();
        public float[] ShadowScales = Array.Empty<float>();
        /// <summary>Job ids the client draws as nothing, sorted (FR-2).</summary>
        public int[] HiddenJobIds = Array.Empty<int>();
        /// <summary>The ground shadow sprite (PLAN-0009 D20).</summary>
        public SpriteRef Shadow;

        /// <summary>The shadow scale of a job id; 1 when the table does not list it, 0 for no shadow (FR-11).</summary>
        public float ShadowScaleOf(int jobId)
        {
            int i = Find(ShadowJobIds, jobId);
            return i >= 0 && i < ShadowScales.Length ? ShadowScales[i] : 1f;
        }

        /// <summary>True for a job id the client draws as nothing (FR-2).</summary>
        public bool IsHidden(int jobId) => Find(HiddenJobIds, jobId) >= 0;

        /// <summary>The draw priority of a headgear: its direction's, else its own, else <paramref name="defaultForSlot"/>.</summary>
        public int PriorityOf(int viewId, int shownDirection, bool riding, int defaultForSlot)
        {
            int lo = 0, hi = Priorities.Length - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int v = Priorities[mid].ViewId;
                if (v == viewId)
                {
                    var p = Priorities[mid];
                    if (riding && p.IgnoreRiding) return defaultForSlot;
                    if (p.FirstDirection >= 0)
                    {
                        int byDirection = DirectionPriorities[p.FirstDirection + (shownDirection & 7)];
                        if (byDirection != NoPriority) return byDirection;
                    }
                    return p.NoDefault ? defaultForSlot : p.Default;
                }
                if (v < viewId) lo = mid + 1;
                else hi = mid - 1;
            }
            return defaultForSlot;
        }

        public bool IsRobeTopLayer(int garment) => Find(RobeTopLayer, garment) >= 0;

        /// <summary>
        /// Whether the garment is behind the body in this frame. False when the job has no layer table (the caller's default
        /// applies); with a table, <paramref name="behind"/> is true only for a listed frame.
        /// </summary>
        public bool TryGetLayerSwap(int job, AccountSex sex, int fileAction, int frame, out bool behind)
        {
            behind = false;
            int inherit = Find(LayerJobs, job);
            if (inherit >= 0) job = LayerJobBases[inherit];
            if (Find(LayerSwapJobs, (job << 1) | ((int)sex & 1)) < 0) return false;

            int key = LayerSwap.KeyOf(job, sex, fileAction);
            int lo = 0, hi = LayerSwaps.Length - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int k = LayerSwaps[mid].Key;
                if (k == key)
                {
                    behind = Bit(LayerSwaps[mid].Behind, frame);
                    return true;
                }
                if (k < key) lo = mid + 1;
                else hi = mid - 1;
            }
            return true;
        }

        /// <summary>Whether the garment goes under the head in this frame.</summary>
        public bool IsGarmentUnderHead(int job, AccountSex sex, int garment, int fileAction, int frame)
        {
            long key = HeadToBodyRow.KeyOf(job, sex, garment, fileAction);
            int lo = 0, hi = HeadToBody.Length - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                long k = HeadToBody[mid].Key;
                if (k == key) return Bit(HeadToBody[mid].Frames, frame);
                if (k < key) lo = mid + 1;
                else hi = mid - 1;
            }
            return false;
        }

        public bool TryGetHeadEdit(int job, int fileAction, int frame, out Vector2Int offset) => TryGetEdit(HeadEdits, job, fileAction, frame, out offset);

        public bool TryGetBodyEdit(int job, int fileAction, int frame, out Vector2Int offset) => TryGetEdit(BodyEdits, job, fileAction, frame, out offset);

        public bool TryGetItemOffset(int viewId, int shownDirection, SpriteRace race, AccountSex sex, out Vector2Int offset)
        {
            int lo = 0, hi = ItemOffsets.Length;
            while (lo < hi)   // the first row of the view id
            {
                int mid = (lo + hi) >> 1;
                if (ItemOffsets[mid].ViewId < viewId) lo = mid + 1;
                else hi = mid;
            }
            for (int i = lo; i < ItemOffsets.Length && ItemOffsets[i].ViewId == viewId; i++)
            {
                var o = ItemOffsets[i];
                if (o.Direction != shownDirection || o.Race != race) continue;
                if (o.Sex != 0 && o.Sex != (sex == AccountSex.SEX_MALE ? 1 : 2)) continue;
                offset = new Vector2Int(o.Dx, o.Dy);
                return true;
            }
            offset = default;
            return false;
        }

        public bool TryGetTint(int garment, out Color32 tint)
        {
            int lo = 0, hi = Transparency.Length - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int v = Transparency[mid].ViewId;
                if (v == garment)
                {
                    tint = Transparency[mid].Tint;
                    return true;
                }
                if (v < garment) lo = mid + 1;
                else hi = mid - 1;
            }
            tint = new Color32(255, 255, 255, 255);
            return false;
        }

        static bool TryGetEdit(SprEdit[] edits, int job, int fileAction, int frame, out Vector2Int offset)
        {
            int lo = 0, hi = edits.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (edits[mid].Job < job) lo = mid + 1;
                else hi = mid;
            }
            bool found = false;
            offset = default;
            for (int i = lo; i < edits.Length && edits[i].Job == job; i++)
            {
                var e = edits[i];
                if (fileAction < e.FirstAction || fileAction > e.LastAction || frame < e.FirstFrame || frame > e.LastFrame) continue;
                offset = new Vector2Int(e.Dx, e.Dy);
                found = true;
            }
            return found;
        }

        static bool Bit(ulong bits, int frame) => frame >= 0 && frame < 64 && ((bits >> frame) & 1UL) != 0;

        static int Find(int[] sorted, int value)
        {
            int lo = 0, hi = sorted.Length - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int v = sorted[mid];
                if (v == value) return mid;
                if (v < value) lo = mid + 1;
                else hi = mid - 1;
            }
            return -1;
        }
    }
}
