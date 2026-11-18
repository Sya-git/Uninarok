using UnityEditor;
using UnityEngine;
using Uninarok.RoImport.Core.Gat;

namespace Uninarok.RoImport.Editor.Gat.Gizmos
{
    /// <summary>
    /// The switches of the map data gizmos (PLAN-0005 4.6, FR-7), kept in <see cref="EditorPrefs"/> so they survive domain
    /// reloads and sessions: layers, visible type groups, radius, cap and the picked region. Every setter repaints the
    /// scene views.
    /// </summary>
    public static class RoMapDataGizmoSettings
    {
        const string Prefix = "Uninarok.RoImport.MapDataGizmos.";

        public const int MinRadius = 1, MaxRadius = 200;
        public const int MinCap = 100, MaxCap = 100000;

        public static int Radius
        {
            get => Mathf.Clamp(EditorPrefs.GetInt(Prefix + "Radius", RoMapDataGizmoSelector.DefaultRadius), MinRadius, MaxRadius);
            set => SetInt("Radius", Mathf.Clamp(value, MinRadius, MaxRadius));
        }

        public static int Cap
        {
            get => Mathf.Clamp(EditorPrefs.GetInt(Prefix + "Cap", RoMapDataGizmoSelector.DefaultCap), MinCap, MaxCap);
            set => SetInt("Cap", Mathf.Clamp(value, MinCap, MaxCap));
        }

        /// <summary>Cells coloured by type.</summary>
        public static bool ShowCells { get => GetBool("ShowCells", true); set => SetBool("ShowCells", value); }

        /// <summary>The cell grid lines.</summary>
        public static bool ShowGrid { get => GetBool("ShowGrid", true); set => SetBool("ShowGrid", value); }

        /// <summary>Corner height labels (costly: drawn only for the cells nearest the focus).</summary>
        public static bool ShowHeights { get => GetBool("ShowHeights", false); set => SetBool("ShowHeights", value); }

        /// <summary>The legend in the overlay.</summary>
        public static bool ShowLegend { get => GetBool("ShowLegend", true); set => SetBool("ShowLegend", value); }

        public static RoMapCellGroups VisibleGroups
        {
            get => (RoMapCellGroups)EditorPrefs.GetInt(Prefix + "Groups", (int)RoMapCellGroups.All) & RoMapCellGroups.All;
            set => SetInt("Groups", (int)(value & RoMapCellGroups.All));
        }

        /// <summary>Draw the picked <see cref="Region"/> instead of the radius around the focus.</summary>
        public static bool UseRegion { get => GetBool("UseRegion", false); set => SetBool("UseRegion", value); }

        /// <summary>The picked rectangle, in cells.</summary>
        public static RectInt Region
        {
            get => new RectInt(EditorPrefs.GetInt(Prefix + "RegionX", 0), EditorPrefs.GetInt(Prefix + "RegionZ", 0),
                EditorPrefs.GetInt(Prefix + "RegionW", 0), EditorPrefs.GetInt(Prefix + "RegionH", 0));
            set
            {
                EditorPrefs.SetInt(Prefix + "RegionX", value.x);
                EditorPrefs.SetInt(Prefix + "RegionZ", value.y);
                EditorPrefs.SetInt(Prefix + "RegionW", Mathf.Max(0, value.width));
                EditorPrefs.SetInt(Prefix + "RegionH", Mathf.Max(0, value.height));
                SceneView.RepaintAll();
            }
        }

        public static bool IsVisible(RoMapCellGroups group) => (VisibleGroups & group) != 0;

        public static void SetVisible(RoMapCellGroups group, bool visible) =>
            VisibleGroups = visible ? VisibleGroups | group : VisibleGroups & ~group;

        /// <summary>Back to the defaults (removes every key).</summary>
        public static void Reset()
        {
            foreach (string key in new[] { "Radius", "Cap", "ShowCells", "ShowGrid", "ShowHeights", "ShowLegend", "Groups", "UseRegion",
                         "RegionX", "RegionZ", "RegionW", "RegionH" })
                EditorPrefs.DeleteKey(Prefix + key);
            SceneView.RepaintAll();
        }

        static bool GetBool(string key, bool fallback) => EditorPrefs.GetBool(Prefix + key, fallback);

        static void SetBool(string key, bool value)
        {
            EditorPrefs.SetBool(Prefix + key, value);
            SceneView.RepaintAll();
        }

        static void SetInt(string key, int value)
        {
            EditorPrefs.SetInt(Prefix + key, value);
            SceneView.RepaintAll();
        }
    }
}
