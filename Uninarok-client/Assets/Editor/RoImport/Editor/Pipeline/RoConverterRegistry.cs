using System;
using System.Collections.Generic;

namespace Uninarok.RoImport.Editor.Pipeline
{
    /// <summary>
    /// The converter of each job kind (plan 4.10): one hand-written list, so the menu, the window and the code path all see
    /// the same set. PLAN-0003 to 0007 add their converters here. Converters are stateless, so the list is built per call
    /// and the class holds no static state.
    /// </summary>
    public static class RoConverterRegistry
    {
        public static IReadOnlyList<IRoConverter> All => new IRoConverter[]
        {
            new RoTextureConverter(),
            new Rsm.RoRsmConverter(),
            new Gnd.RoGndConverter(),
            new Gat.RoMapDataConverter(),
            new Map.RoMapConverter(),
            new Sprites.RoSpriteConverter(),
            new Sprites.RoPaletteConverter(),
            new Catalog.RoCatalogEntryConverter(),
            new Catalog.RoCatalogIndexConverter(),
        };

        public static IRoConverter For(RoJobKind kind)
        {
            foreach (var converter in All)
                if (converter.Kind == kind) return converter;
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "No converter is registered for this kind.");
        }
    }
}
