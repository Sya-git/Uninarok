using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using Uninarok.Map;
using Uninarok.RoImport.Core.Gat;

namespace Uninarok.RoImport.Editor.Gat.Gizmos
{
    /// <summary>
    /// The "Ragnarok Map Data" scene view overlay (PLAN-0005 4.6, FR-7): layer and type toggles, the legend, radius and cap,
    /// the region tool and the notice when the cap cuts the view. Shown only while a map root is in the selection.
    /// </summary>
    [Overlay(typeof(SceneView), "Ragnarok Map Data")]
    public sealed class RoMapDataOverlay : IMGUIOverlay, ITransientOverlay
    {
        bool _picking;
        Vector2Int _dragStart;

        public bool visible => SelectedRoot() != null;

        public override void OnCreated() => SceneView.duringSceneGui += OnSceneGui;

        public override void OnWillBeDestroyed() => SceneView.duringSceneGui -= OnSceneGui;

        /// <summary>The map root of the selection: on the selected object, an ancestor or a child.</summary>
        static MapRoot SelectedRoot()
        {
            var go = Selection.activeGameObject;
            if (go == null) return null;
            return go.GetComponentInParent<MapRoot>() ?? go.GetComponentInChildren<MapRoot>();
        }

        public override void OnGUI()
        {
            var root = SelectedRoot();
            if (root == null) return;
            if (root.Data == null)
            {
                EditorGUILayout.HelpBox("This map root has no map data.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"{root.Data.name}  {root.Data.CellsX} x {root.Data.CellsZ} cells", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                RoMapDataGizmoSettings.ShowCells = GUILayout.Toggle(RoMapDataGizmoSettings.ShowCells, "Cells", EditorStyles.miniButtonLeft);
                RoMapDataGizmoSettings.ShowGrid = GUILayout.Toggle(RoMapDataGizmoSettings.ShowGrid, "Grid", EditorStyles.miniButtonMid);
                RoMapDataGizmoSettings.ShowHeights = GUILayout.Toggle(RoMapDataGizmoSettings.ShowHeights, "Heights", EditorStyles.miniButtonMid);
                RoMapDataGizmoSettings.ShowLegend = GUILayout.Toggle(RoMapDataGizmoSettings.ShowLegend, "Legend", EditorStyles.miniButtonRight);
            }

            EditorGUILayout.LabelField("Types", EditorStyles.miniBoldLabel);
            Group(RoMapCellGroups.Walkable, "Walkable (0, 2, 4, 6)", MapCellType.Walkable);
            Group(RoMapCellGroups.Water, "Water (3)", MapCellType.Water);
            Group(RoMapCellGroups.Wall, "Wall (1)", MapCellType.Wall);
            Group(RoMapCellGroups.Cliff, "Cliff (5)", MapCellType.Cliff);
            Group(RoMapCellGroups.Other, "Other", MapCellType.Other);

            RoMapDataGizmoSettings.Radius = EditorGUILayout.IntSlider("Radius", RoMapDataGizmoSettings.Radius,
                RoMapDataGizmoSettings.MinRadius, RoMapDataGizmoSettings.MaxRadius);
            RoMapDataGizmoSettings.Cap = EditorGUILayout.IntField("Cap", RoMapDataGizmoSettings.Cap);

            using (new EditorGUILayout.HorizontalScope())
            {
                RoMapDataGizmoSettings.UseRegion = GUILayout.Toggle(RoMapDataGizmoSettings.UseRegion, "Use region", EditorStyles.miniButtonLeft);
                bool picking = GUILayout.Toggle(_picking, "Pick region", EditorStyles.miniButtonMid);
                if (picking != _picking) _picking = picking;
                if (GUILayout.Button("Reset", EditorStyles.miniButtonRight)) RoMapDataGizmoSettings.Reset();
            }
            if (_picking) EditorGUILayout.LabelField("Drag in the scene view to pick the region.", EditorStyles.miniLabel);
            else if (RoMapDataGizmoSettings.UseRegion) EditorGUILayout.LabelField($"Region {RoMapDataGizmoSettings.Region}", EditorStyles.miniLabel);

            var cells = new System.Collections.Generic.List<int>(1024);
            bool capped = RoMapDataGizmos.SelectCells(root, root.Data, cells);   // the same selection the gizmos draw
            EditorGUILayout.LabelField($"{cells.Count:N0} cells drawn", EditorStyles.miniLabel);
            if (capped)
                EditorGUILayout.HelpBox($"The cap of {RoMapDataGizmoSettings.Cap:N0} cells cuts the view; the nearest cells are drawn.", MessageType.None);

            if (RoMapDataGizmoSettings.ShowLegend) RoMapDataLegend.Draw();
        }

        static void Group(RoMapCellGroups group, string label, MapCellType colourOf)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var rect = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12), GUILayout.Height(12));
                EditorGUI.DrawRect(rect, RoGatImage.ColorOf(colourOf));
                bool on = RoMapDataGizmoSettings.IsVisible(group);
                bool now = EditorGUILayout.ToggleLeft(label, on);
                if (now != on) RoMapDataGizmoSettings.SetVisible(group, now);
            }
        }

        /// <summary>The region tool: a left drag on the map's base plane picks a rectangle of cells.</summary>
        void OnSceneGui(SceneView view)
        {
            if (!_picking) return;
            var root = SelectedRoot();
            if (root == null || root.Data == null) return;
            var e = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(control);
            if (e.button != 0 || !TryCellUnderMouse(root, e.mousePosition, out var cell)) return;

            switch (e.type)
            {
                case EventType.MouseDown:
                    _dragStart = cell;
                    GUIUtility.hotControl = control;
                    e.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == control:
                case EventType.MouseUp when GUIUtility.hotControl == control:
                    int x0 = Mathf.Min(_dragStart.x, cell.x), z0 = Mathf.Min(_dragStart.y, cell.y);
                    RoMapDataGizmoSettings.Region = new RectInt(x0, z0, Mathf.Abs(cell.x - _dragStart.x) + 1, Mathf.Abs(cell.y - _dragStart.y) + 1);
                    RoMapDataGizmoSettings.UseRegion = true;
                    if (e.type == EventType.MouseUp)
                    {
                        GUIUtility.hotControl = 0;
                        _picking = false;
                    }
                    e.Use();
                    break;
            }
        }

        static bool TryCellUnderMouse(MapRoot root, Vector2 mouse, out Vector2Int cell)
        {
            cell = default;
            var ray = HandleUtility.GUIPointToWorldRay(mouse);
            var plane = new Plane(root.transform.up, root.transform.position);
            if (!plane.Raycast(ray, out float distance)) return false;
            var local = root.transform.InverseTransformPoint(ray.GetPoint(distance));
            cell = RoMapDataGizmos.CellOf(root.Data, local, clamp: true);
            return true;
        }
    }
}
