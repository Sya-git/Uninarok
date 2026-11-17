using System;
using System.Collections.Generic;

namespace Uninarok.RoImport.Editor.Pipeline
{
    /// <summary>
    /// The converter of each job kind (plan 4.10): one hand-written list, so the menu, the window and the code path all see
    /// the same set. PLAN-0003 to 0007 add their converters here.
    /// </summary>
    public static class RoConverterRegistry
    {
        static readonly IRoConverter[] Converters =
        {
            new RoTextureConverter(),
        };

        public static IReadOnlyList<IRoConverter> All => Converters;

        public static IRoConverter For(RoJobKind kind)
        {
            foreach (var converter in Converters)
                if (converter.Kind == kind) return converter;
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "No converter is registered for this kind.");
        }
    }
}
