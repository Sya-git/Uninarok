using UnityEngine;
using Uninarok.Catalog;
using Uninarok.Networking.Data;

namespace Uninarok.Units
{
    /// <summary>The parts a unit can have (PLAN-0009 4.4).</summary>
    public enum PartSlot : byte
    {
        Shadow,
        Body,
        Head,
        HeadBottom,
        HeadMid,
        HeadTop,
        Garment,
        Weapon,
        WeaponTrail,
        Shield,
    }

    /// <summary>One part of a composed unit: its place in the back-to-front order and its offset from the unit's origin in pixels, Y down.</summary>
    public struct PartPlacement
    {
        public PartSlot Slot;
        /// <summary>0 is drawn first (behind).</summary>
        public int Order;
        public Vector2 OffsetPx;
        public Color32 Tint;
        public bool Visible;
    }

    /// <summary>A part that is attached by anchor (head and headgears): what the composer needs of its frame.</summary>
    public struct AttachedPart
    {
        public bool Present;
        public int ViewId;
        public bool HasAnchor;
        /// <summary>Anchor 0 of the part's frame, in pixels, Y down.</summary>
        public Vector2Int Anchor;
    }

    /// <summary>What <see cref="UnitComposer.Compose"/> composes: the frame shown of every part and the look's ids (PLAN-0009 4.5).</summary>
    public struct ComposeInput
    {
        public bool Player;
        public int Job;
        public AccountSex Sex;
        public SpriteRace Race;
        /// <summary>The body is a mount (PLAN-0009 4.5 rule 2).</summary>
        public bool Riding;
        /// <summary>Apply the riding and body edits of the tables; off by default (D17).</summary>
        public bool RidingEdits;

        public int Animation;
        /// <summary>The shown direction, 0 = front.</summary>
        public int Direction;
        /// <summary>The body's frame.</summary>
        public int Frame;

        public bool Body;
        public bool HasBodyAnchor;
        public Vector2Int BodyAnchor;

        public AttachedPart Head, HeadBottom, HeadMid, HeadTop;
        public bool Garment;
        public int GarmentId;
        public bool Weapon, WeaponTrail, Shield, Shadow;

        public int FileAction => Animation * 8 + Direction;
    }

    /// <summary>
    /// Draw order and attachment of a unit's parts (PLAN-0009 4.5, D3, D16; SPEC-0009 FR-3, FR-4). Pure: the rules and their
    /// evidence are in composition-reference.md. Every rule whose table is empty is skipped; the default order alone is sane.
    /// </summary>
    public static class UnitComposer
    {
        public const int MaxParts = 10;

        // Back to front. Headgears and garments take their priority above the head.
        const int KeyShadow = -100000, KeyShieldBehind = -3000, KeyGarmentBehind = -2000, KeyHeadgearBehind = -1000;
        const int KeyBody = 0, KeyGarmentUnderHead = 10, KeyHead = 20, KeyPriority = 1000;
        const int KeyGarmentTop = 100000, KeyWeapon = 200000, KeyTrail = 200001, KeyShield = 200002;

        /// <summary>Directions that draw the shield behind the body (composition-reference.md 3).</summary>
        public static bool ShieldBehind(int direction) => direction >= 2 && direction <= 5;

        /// <summary>Directions that show the unit from behind: the garment is in front of the body (default, no table).</summary>
        public static bool FacesAway(int direction) => direction >= 3 && direction <= 5;

        /// <summary>
        /// Writes the placements of the present parts into <paramref name="into"/> (at least <see cref="MaxParts"/>), sorted back
        /// to front, and returns how many. No allocation.
        /// </summary>
        public static int Compose(in ComposeInput input, CompositionTables tables, PartPlacement[] into)
        {
            int count = 0;
            int direction = input.Direction & 7;
            var white = new Color32(255, 255, 255, 255);

            if (input.Shadow) Add(into, ref count, PartSlot.Shadow, KeyShadow, Vector2.zero, white);
            if (!input.Player)
            {
                if (input.Body) Add(into, ref count, PartSlot.Body, KeyBody, Vector2.zero, white);
                Finish(into, count);
                return count;
            }

            // ---- attachment ----
            Vector2Int bodyOffset = default, headEdit = default;
            if (tables != null && input.RidingEdits && input.Riding)
            {
                tables.TryGetBodyEdit(input.Job, input.FileAction, input.Frame, out bodyOffset);
                tables.TryGetHeadEdit(input.Job, input.FileAction, input.Frame, out headEdit);
            }
            var neck = (input.HasBodyAnchor ? input.BodyAnchor : Vector2Int.zero) + bodyOffset + headEdit;

            // ---- order ----
            if (input.Body) Add(into, ref count, PartSlot.Body, KeyBody, bodyOffset, white);
            if (input.Head.Present) Add(into, ref count, PartSlot.Head, KeyHead, Attach(neck, input.Head), white);

            Headgear(input, input.HeadMid, PartSlot.HeadMid, tables != null ? tables.DefaultMid : 100, tables, neck, direction, into, ref count,
                     input.HeadMid.Present && !(input.HeadBottom.Present && input.HeadBottom.ViewId == input.HeadMid.ViewId));
            // One item can fill several slots: it is drawn once (composition-reference.md 3).
            Headgear(input, input.HeadTop, PartSlot.HeadTop, tables != null ? tables.DefaultTop : 200, tables, neck, direction, into, ref count,
                     input.HeadTop.Present && !(input.HeadBottom.Present && input.HeadBottom.ViewId == input.HeadTop.ViewId)
                                           && !(input.HeadMid.Present && input.HeadMid.ViewId == input.HeadTop.ViewId));
            Headgear(input, input.HeadBottom, PartSlot.HeadBottom, tables != null ? tables.DefaultBottom : 300, tables, neck, direction, into, ref count,
                     input.HeadBottom.Present);

            if (input.Garment)
            {
                var tint = white;
                if (tables != null) tables.TryGetTint(input.GarmentId, out tint);
                Add(into, ref count, PartSlot.Garment, GarmentKey(input, direction, tables), bodyOffset, tint);
            }
            if (input.Weapon) Add(into, ref count, PartSlot.Weapon, KeyWeapon, bodyOffset, white);
            if (input.WeaponTrail) Add(into, ref count, PartSlot.WeaponTrail, KeyTrail, bodyOffset, white);
            if (input.Shield) Add(into, ref count, PartSlot.Shield, ShieldBehind(direction) ? KeyShieldBehind : KeyShield, bodyOffset, white);

            Finish(into, count);
            return count;
        }

        static int GarmentKey(in ComposeInput input, int direction, CompositionTables tables)
        {
            bool behind = !FacesAway(direction);
            if (tables == null) return behind ? KeyGarmentBehind : KeyPriority + 400;
            if (tables.TryGetLayerSwap(input.Job, input.Sex, input.FileAction, input.Frame, out bool listed)) behind = listed;
            if (behind) return KeyGarmentBehind;
            if (tables.IsGarmentUnderHead(input.Job, input.Sex, input.GarmentId, input.FileAction, input.Frame)) return KeyGarmentUnderHead;
            return tables.IsRobeTopLayer(input.GarmentId) ? KeyGarmentTop : KeyPriority + tables.DefaultRobe;
        }

        static void Headgear(in ComposeInput input, in AttachedPart part, PartSlot slot, int defaultPriority, CompositionTables tables,
                             Vector2Int neck, int direction, PartPlacement[] into, ref int count, bool draw)
        {
            if (!draw) return;
            int priority = defaultPriority;
            var offset = Attach(neck, part);
            if (tables != null)
            {
                priority = tables.PriorityOf(part.ViewId, direction, input.Riding, defaultPriority);
                if (tables.TryGetItemOffset(part.ViewId, direction, input.Race, input.Sex, out var correction)) offset += correction;
            }
            // A negative priority is behind the body.
            Add(into, ref count, slot, priority < 0 ? KeyHeadgearBehind + priority : KeyPriority + priority, offset, new Color32(255, 255, 255, 255));
        }

        static Vector2 Attach(Vector2Int neck, in AttachedPart part) => part.HasAnchor ? neck - part.Anchor : Vector2Int.zero;

        // Insertion by key (kept in Order until Finish numbers them); equal keys keep the order they were added in.
        static void Add(PartPlacement[] into, ref int count, PartSlot slot, int key, Vector2 offset, Color32 tint)
        {
            int i = count++;
            while (i > 0 && into[i - 1].Order > key)
            {
                into[i] = into[i - 1];
                i--;
            }
            into[i] = new PartPlacement { Slot = slot, Order = key, OffsetPx = offset, Tint = tint, Visible = true };
        }

        static void Finish(PartPlacement[] into, int count)
        {
            for (int i = 0; i < count; i++) into[i].Order = i;
        }
    }
}
