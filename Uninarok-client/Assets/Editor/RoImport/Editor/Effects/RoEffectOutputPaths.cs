using System;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Editor.Effects
{
    /// <summary>
    /// Where effect outputs go (PLAN-0012 §3): under <c>Art/Imported/effect/</c>, mirroring the canonical Ragnarok path with
    /// its extension kept and <c>.asset</c> appended, so <c>sprite/이팩트/a.act</c> gives <c>…/effect/sprite/이팩트/a.act.asset</c>.
    /// </summary>
    public static class RoEffectOutputPaths
    {
        public const string Folder = "effect";

        /// <summary>The generated effect folder: <c>Art/effect</c> (<c>Assets/Art/Imported/effect</c> by default).</summary>
        public static string Root(RoOutputRoots roots)
        {
            if (roots == null) throw new ArgumentNullException(nameof(roots));
            return roots.Art.TrimEnd('/') + "/" + Folder;
        }

        /// <summary>The catalog index: <c>effect/_index.asset</c>.</summary>
        public static string Index(RoOutputRoots roots) => Root(roots) + "/_index.asset";

        /// <summary>The hand-made effect assets: <c>Data/Effects/Authored</c>; an entry sits at <c>&lt;this&gt;/&lt;key&gt;.asset</c>.</summary>
        public static string AuthoredRoot(RoOutputRoots roots)
        {
            if (roots == null) throw new ArgumentNullException(nameof(roots));
            return roots.Data.TrimEnd('/') + "/Effects/Authored";
        }

        /// <summary>The asset of an effect source (an STR or an ACT): <c>effect/&lt;canonical path&gt;.asset</c>.</summary>
        public static string Asset(RoOutputRoots roots, RoPath source)
        {
            if (source.IsEmpty) throw new ArgumentException("An effect output needs a source path.", nameof(source));
            return Root(roots) + "/" + source.Canonical + ".asset";
        }
    }
}
