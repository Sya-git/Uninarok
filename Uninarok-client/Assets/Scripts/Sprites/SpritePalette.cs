using UnityEngine;

namespace Uninarok.Sprites
{
    /// <summary>
    /// Shows an indexed sprite with another palette without new images (PLAN-0007 4.7, SPEC-0007 FR-7). The palette (a
    /// 256 × 1 texture) goes to every layer renderer of the actor through one cached property block; the sheet's index
    /// texture and the shader <c>Uninarok/RoSprite</c> do the lookup. No texture or frame is created.
    /// </summary>
    public static class SpritePalette
    {
        public static readonly int PaletteTexId = Shader.PropertyToID("_PaletteTex");
        public static readonly int PaletteOnId = Shader.PropertyToID("_PaletteOn");

        /// <summary>Uses <paramref name="palette"/> for <paramref name="actor"/>'s indexed pixels; null is the same as <see cref="Clear"/>.</summary>
        public static void Apply(SpriteActor actor, Texture2D palette)
        {
            if (actor == null) return;
            actor.Palette = palette;
            Write(actor);
        }

        /// <summary>Back to the sprite's own colours.</summary>
        public static void Clear(SpriteActor actor) => Apply(actor, null);

        /// <summary>Writes the actor's palette to its renderers (also called when it creates renderers later).</summary>
        internal static void Write(SpriteActor actor)
        {
            var renderers = actor.LayerRenderers;
            if (actor.Palette == null)
            {
                for (int i = 0; i < renderers.Count; i++) renderers[i].SetPropertyBlock(null);
                return;
            }

            var block = actor.PaletteBlock ??= new MaterialPropertyBlock();
            for (int i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i];
                r.GetPropertyBlock(block);
                block.SetTexture(PaletteTexId, actor.Palette);
                block.SetFloat(PaletteOnId, 1f);
                r.SetPropertyBlock(block);
            }
        }
    }
}
