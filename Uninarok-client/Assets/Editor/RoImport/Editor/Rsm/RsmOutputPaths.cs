using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Editor.Rsm
{
    /// <summary>
    /// The output paths of one model (PLAN-0003 §3): for <c>model/a/b.rsm</c>, the prefab <c>…/model/a/b.prefab</c> and,
    /// in the folder <c>…/model/a/b/</c>, one mesh per node (<c>&lt;node&gt;.asset</c>), the clips (<c>b.anim</c>,
    /// <c>b_2.anim</c> …) and the controller (<c>b.controller</c>). Node file names are made file-safe and unique
    /// (case-insensitively, as on Windows); Korean characters are kept.
    /// </summary>
    public sealed class RsmOutputPaths
    {
        public string Prefab { get; private set; }
        public string Folder { get; private set; }
        public string Controller { get; private set; }
        /// <summary>One mesh path per node, indexed like the model's nodes.</summary>
        public IReadOnlyList<string> Meshes { get; private set; }

        string _name;

        /// <param name="uniqueNodeNames">The tree's unique node names (<c>RsmTree.UniqueNames</c>).</param>
        public static RsmOutputPaths For(RoOutputRoots roots, RoPath source, IReadOnlyList<string> uniqueNodeNames)
        {
            if (roots == null) throw new ArgumentNullException(nameof(roots));
            if (uniqueNodeNames == null) throw new ArgumentNullException(nameof(uniqueNodeNames));
            string folder = RoOutputPaths.Mirror(roots, source, string.Empty);

            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var meshes = new string[uniqueNodeNames.Count];
            for (int i = 0; i < meshes.Length; i++)
            {
                string safe = FileSafe(uniqueNodeNames[i]);
                string name = safe;
                for (int suffix = 2; !used.Add(name); suffix++) name = $"{safe}_{suffix}";
                meshes[i] = folder + "/" + name + ".asset";
            }

            string modelName = source.FileNameWithoutExtension;
            return new RsmOutputPaths
            {
                Prefab = RoOutputPaths.Mirror(roots, source, ".prefab"),
                Folder = folder,
                Controller = folder + "/" + modelName + ".controller",
                Meshes = meshes,
                _name = modelName
            };
        }

        /// <summary>The clip of a loop group: <c>b.anim</c> for the first, then <c>b_2.anim</c>, <c>b_3.anim</c> (PLAN-0003 §3).</summary>
        public string Clip(int loopIndex)
        {
            if (loopIndex < 0) throw new ArgumentOutOfRangeException(nameof(loopIndex));
            return Folder + "/" + _name + (loopIndex == 0 ? string.Empty : "_" + (loopIndex + 1)) + ".anim";
        }

        /// <summary>Characters invalid in a file name on any of the editor's platforms.</summary>
        static bool IsInvalid(char c, char[] platformInvalid) =>
            char.IsControl(c) || "/\\:*?\"<>|".IndexOf(c) >= 0 || Array.IndexOf(platformInvalid, c) >= 0;

        /// <summary>A name usable as a file name: invalid characters become <c>_</c>, trailing dots and spaces go, empty becomes <c>node</c>.</summary>
        public static string FileSafe(string name)
        {
            var platformInvalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name ?? string.Empty);
            for (int i = 0; i < sb.Length; i++)
                if (IsInvalid(sb[i], platformInvalid)) sb[i] = '_';
            string safe = sb.ToString().Trim().TrimEnd('.');
            return safe.Length == 0 ? "node" : safe;
        }
    }
}
