using UnityEditor;
using UnityEngine;
using Uninarok.Map;
using Uninarok.RoImport.Core.Gat;

namespace Uninarok.RoImport.Editor.Gat.Gizmos
{
    /// <summary>The legend of the map data colours (PLAN-0005 4.6), shared by the overlay and the inspector.</summary>
    public static class RoMapDataLegend
    {
        static readonly MapCellType[] Types =
        {
            MapCellType.Walkable, MapCellType.Type2, MapCellType.Type4, MapCellType.Type6, MapCellType.Water,
            MapCellType.Wall, MapCellType.Cliff, MapCellType.Other
        };

        /// <summary>One line per type: colour swatch, name, and the count of cells when <paramref name="counts"/> is given.</summary>
        public static void Draw(int[] counts = null)
        {
            foreach (var type in Types)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var rect = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14), GUILayout.Height(14));
                    EditorGUI.DrawRect(rect, RoGatImage.ColorOf(type));
                    string label = Describe(type);
                    if (counts != null) label += $"  ({counts[(int)type]:N0})";
                    EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
                }
            }
        }

        public static string Describe(MapCellType type)
        {
            switch (type)
            {
                case MapCellType.Walkable: return "0 Walkable";
                case MapCellType.Wall: return "1 Wall";
                case MapCellType.Type2: return "2 Type2 (walkable)";
                case MapCellType.Water: return "3 Water (walkable)";
                case MapCellType.Type4: return "4 Type4 (walkable)";
                case MapCellType.Cliff: return "5 Cliff (shootable)";
                case MapCellType.Type6: return "6 Type6 (walkable)";
                default: return "Other (blocked)";
            }
        }

        /// <summary>Cells per type value (index = the byte value of <see cref="MapCellType"/>).</summary>
        public static int[] Count(MapData data)
        {
            var counts = new int[256];
            if (data == null) return counts;
            for (int z = 0; z < data.CellsZ; z++)
            for (int x = 0; x < data.CellsX; x++)
                counts[(int)data.TypeAt(x, z)]++;
            return counts;
        }
    }
}
