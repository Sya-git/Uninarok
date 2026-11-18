using UnityEditor;
using UnityEngine;
using Uninarok.Map;

namespace Uninarok.RoImport.Editor.Gat.Gizmos
{
    /// <summary>
    /// The inspector of a map data asset (PLAN-0005 4.6, FR-7): size, geometry and the legend with the count of cells per
    /// type. The raw arrays are not shown (160,000 cells would stall the inspector).
    /// </summary>
    [CustomEditor(typeof(MapData))]
    public sealed class MapDataInspector : UnityEditor.Editor
    {
        int[] _counts;

        void OnEnable() => _counts = RoMapDataLegend.Count(target as MapData);

        public override void OnInspectorGUI()
        {
            var data = (MapData)target;
            EditorGUILayout.LabelField("Map", data.MapName);
            EditorGUILayout.LabelField("Cells", $"{data.CellsX} x {data.CellsZ} ({data.CellsX * data.CellsZ:N0})");
            EditorGUILayout.LabelField("Cell size", $"{data.CellSize} units");
            EditorGUILayout.LabelField("Origin", data.Origin.ToString());
            EditorGUILayout.LabelField("Row sign", data.RowSign.ToString());
            EditorGUILayout.LabelField("Diagonal", data.DiagonalFlip ? "H00 to H11" : "H10 to H01");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cell types", EditorStyles.boldLabel);
            RoMapDataLegend.Draw(_counts);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Select the map's MapRoot in a scene to see the cells as gizmos (overlay \"Ragnarok Map Data\").", MessageType.None);
        }
    }
}
