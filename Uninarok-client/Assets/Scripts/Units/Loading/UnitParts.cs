using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Uninarok.Catalog;
using Uninarok.Sprites;

namespace Uninarok.Units
{
    /// <summary>The loaded parts of one unit (PLAN-0009 4.4): animations, palettes and what the composer needs to know of the look.</summary>
    public sealed class UnitParts
    {
        public const int SlotCount = 10;

        /// <summary>By <see cref="PartSlot"/>; null where the unit has no such part. The shadow has none (it is <see cref="ShadowSprite"/>).</summary>
        public readonly SpriteAnimation[] Animations = new SpriteAnimation[SlotCount];
        public readonly Texture2D[] Palettes = new Texture2D[SlotCount];

        /// <summary>The look these parts belong to.</summary>
        public UnitLook Look;
        public SpriteRace Race;
        public bool Riding;
        /// <summary>The unit's display scale (catalog, 1 for players).</summary>
        public float Scale = 1f;
        /// <summary>A job id the client draws as nothing (FR-2): no parts, no shadow.</summary>
        public bool Hidden;
        public SpriteAnimation ShadowAnimation;
        public Sprite ShadowSprite;
        /// <summary>The shadow table's scale; 0: no shadow (FR-11).</summary>
        public float ShadowScale;
        /// <summary>Changes every time parts are applied.</summary>
        public int Version;

        // What is held of the source, to release when replaced.
        internal List<AssetReference> Held = new List<AssetReference>();
        internal List<AssetReference> Spare = new List<AssetReference>();
        /// <summary>The newest load asked for; older ones still in flight are dropped.</summary>
        internal int Request;

        public SpriteAnimation this[PartSlot slot] => Animations[(int)slot];
        public bool Has(PartSlot slot) => Animations[(int)slot] != null;
        public bool HasShadow => ShadowScale > 0f && (ShadowSprite != null || ShadowAnimation != null);

        internal void Clear()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                Animations[i] = null;
                Palettes[i] = null;
            }
            Race = SpriteRace.Human;
            Riding = false;
            Scale = 1f;
            Hidden = false;
            ShadowAnimation = null;
            ShadowSprite = null;
            ShadowScale = 0f;
        }
    }

    /// <summary>How each part of a look was found, or why it is missing (PLAN-0009 4.2; SPEC-0009 FR-1, FR-15).</summary>
    public sealed class LoadReport
    {
        public readonly struct Line
        {
            public Line(PartSlot slot, bool found, string text)
            {
                Slot = slot;
                Found = found;
                Text = text;
            }

            public PartSlot Slot { get; }
            public bool Found { get; }
            public string Text { get; }

            public override string ToString() => $"{Slot}: {(Found ? "" : "missing: ")}{Text}";
        }

        readonly List<Line> _lines = new List<Line>();

        public IReadOnlyList<Line> Lines => _lines;

        public bool TryGet(PartSlot slot, out Line line)
        {
            foreach (var l in _lines)
                if (l.Slot == slot)
                {
                    line = l;
                    return true;
                }
            line = default;
            return false;
        }

        public int MissingCount
        {
            get
            {
                int n = 0;
                foreach (var l in _lines)
                    if (!l.Found) n++;
                return n;
            }
        }

        internal void Found(PartSlot slot, string text) => _lines.Add(new Line(slot, true, text));
        internal void Missing(PartSlot slot, string text) => _lines.Add(new Line(slot, false, text));

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var l in _lines) sb.Append(l).Append('\n');
            return sb.ToString();
        }
    }
}
