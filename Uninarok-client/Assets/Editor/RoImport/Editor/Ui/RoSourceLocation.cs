using System.IO;
using UnityEditor;
using UnityEngine;

namespace Uninarok.RoImport.Editor.Ui
{
    /// <summary>
    /// Where the source data root is (plan 4.2, SPEC-0002 FR-1, FR-15): the <see cref="PrefKey"/> editor preference when
    /// set, otherwise <see cref="DefaultProjectRelative"/> in this project. The default folder name ends with <c>~</c>, so
    /// Unity neither imports nor builds the source files.
    /// </summary>
    public static class RoSourceLocation
    {
        public const string PrefKey = "Uninarok.RoImport.SourceRoot";
        public const string DefaultProjectRelative = "Assets/Editor/GRF Resources~/data";

        static string ProjectRoot => Path.GetDirectoryName(Application.dataPath) ?? ".";

        /// <summary>The active data root as a full path. A relative preference is resolved against the project root.</summary>
        public static string Resolve()
        {
            string configured = EditorPrefs.GetString(PrefKey, string.Empty).Trim();
            string path = configured.Length > 0 ? configured : DefaultProjectRelative;
            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(ProjectRoot, path));
        }

        /// <summary>True when the preference overrides the default.</summary>
        public static bool IsOverridden => EditorPrefs.GetString(PrefKey, string.Empty).Trim().Length > 0;

        /// <summary>Sets the preference (from the window's Browse button). Null or empty returns to the default.</summary>
        public static void Set(string dataRoot)
        {
            if (string.IsNullOrWhiteSpace(dataRoot)) Clear();
            else EditorPrefs.SetString(PrefKey, dataRoot.Trim());
        }

        public static void Clear() => EditorPrefs.DeleteKey(PrefKey);
    }
}
