using System;
using Uninarok.RoImport.Core.Materials;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Core.Output
{
    /// <summary>
    /// Where each output goes (plan 4.5, D9; SPEC-0002 FR-4, FR-16). Visual outputs mirror the canonical (lowercase, forward
    /// slash) Ragnarok path under the art root, so two files of one name in different Ragnarok folders never collide and the
    /// path is the same on every machine. Two different sources mapping to one output path is the ledger's collision rule.
    /// </summary>
    public static class RoOutputPaths
    {
        public const string MaterialsFolder = "_materials";
        public const string PlaceholdersFolder = "_placeholders";
        public const string AdditiveSuffix = ".additive";
        public const string GroundSuffix = "__ground";

        /// <summary><c>Art/&lt;canonical folder&gt;/&lt;name&gt;&lt;newExtension&gt;</c>.</summary>
        public static string Mirror(RoOutputRoots r, RoPath source, string newExtension) =>
            Join(r.Art, NotEmpty(source).WithExtension(newExtension).Canonical);

        /// <summary>Every texture becomes a PNG (D7): <c>Art/texture/a/b.png</c>.</summary>
        public static string Texture(RoOutputRoots r, RoPath source) => Mirror(r, source, ".png");

        /// <summary>The ground's Clamp copy of a texture (PLAN-0004 D17): <c>Art/texture/a/b__ground.png</c>.</summary>
        public static string GroundTexture(RoOutputRoots r, RoPath source) =>
            Join(r.Art, NotEmpty(source).WithExtension("").Canonical + GroundSuffix + ".png");

        /// <summary>
        /// <c>Art/_materials/&lt;texture path without extension&gt;__&lt;suffix&gt;.mat</c>. The overload taking an
        /// <c>RoMaterialKey</c> (T5.1) passes the key's texture and suffix.
        /// </summary>
        public static string Material(RoOutputRoots r, RoPath texture, string suffix) =>
            Join(r.Art, MaterialsFolder + "/" + NotEmpty(texture).WithExtension("").Canonical + "__" + Name(suffix, nameof(suffix)) + ".mat");

        /// <summary>The shared material of a key (plan 4.6): <c>Art/_materials/&lt;texture without extension&gt;__&lt;suffix&gt;.mat</c>.</summary>
        public static string Material(RoOutputRoots r, RoMaterialKey key) => Material(r, key.Texture, key.Suffix);

        /// <summary><c>Art/_placeholders/&lt;name&gt;</c>.</summary>
        public static string Placeholder(RoOutputRoots r, string name) =>
            Join(r.Art, PlaceholdersFolder + "/" + Name(name, nameof(name)));

        /// <summary><c>Data/&lt;kind&gt;/&lt;name&gt;&lt;extension&gt;</c>, e.g. <c>Data/Map/prontera.asset</c>.</summary>
        public static string Data(RoOutputRoots r, string kind, string name, string extension) =>
            Join(r.Data, Name(kind, nameof(kind)) + "/" + Name(name, nameof(name)) + Extension(extension));

        /// <summary>The generated map scene, owned by the ledger: <c>Scenes/&lt;map&gt;.unity</c>.</summary>
        public static string Scene(RoOutputRoots r, string mapName) =>
            Join(r.Scenes, Name(mapName, nameof(mapName)) + ".unity");

        /// <summary>The hand-made additive scene beside it, never written by the importer: <c>Scenes/&lt;map&gt;.additive.unity</c>.</summary>
        public static string AdditiveScene(RoOutputRoots r, string mapName) =>
            Join(r.Scenes, Name(mapName, nameof(mapName)) + AdditiveSuffix + ".unity");

        static string Join(string root, string relative) => root.TrimEnd('/') + "/" + relative;

        static RoPath NotEmpty(RoPath path)
        {
            if (path.IsEmpty) throw new ArgumentException("An output needs a source path.", nameof(path));
            return path;
        }

        static string Name(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value) || value.IndexOf('/') >= 0 || value.IndexOf('\\') >= 0 || value == "." || value == "..")
                throw new ArgumentException($"'{value}' is not a single file or folder name.", parameter);
            return value;
        }

        static string Extension(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return string.Empty;
            return extension[0] == '.' ? extension : "." + extension;
        }
    }
}
