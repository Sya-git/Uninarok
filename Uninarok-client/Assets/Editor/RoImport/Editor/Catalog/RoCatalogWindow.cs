using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Uninarok.Catalog;
using Uninarok.RoImport.Editor.Pipeline;
using RoImportApi = Uninarok.RoImport.Editor.Pipeline.RoImport;   // the class name equals our root namespace

namespace Uninarok.RoImport.Editor.Catalog
{
    /// <summary>
    /// <c>Tools/Ragnarok/Import Catalog…</c> (SPEC-0008 FR-9; PLAN-0008 4.7): a checkbox per category, All, Force, Import and
    /// the per-category result table. It calls <see cref="RoImportApi.Catalog"/> only, like the menu items, so the window
    /// and the code path cannot differ (PLAN-0002 4.11).
    /// </summary>
    public sealed class RoCatalogWindow : EditorWindow
    {
        static SpriteCatalogCategory[] AllCategories => (SpriteCatalogCategory[])Enum.GetValues(typeof(SpriteCatalogCategory));

        [SerializeField] List<SpriteCatalogCategory> _chosen = new List<SpriteCatalogCategory>();
        [SerializeField] bool _composition;
        [SerializeField] bool _force;
        RoCatalogReport _last;
        Vector2 _scroll;

        public static void Open() => GetWindow<RoCatalogWindow>("Ragnarok Catalog").Show();

        /// <summary>What the Import button runs (and what the menu items run): the code path itself.</summary>
        public static RoCatalogReport Import(IEnumerable<SpriteCatalogCategory> categories, bool force, RoImportOptions options = null)
        {
            options = (options ?? new RoImportOptions()).Clone();
            options.Force |= force;
            var report = RoImportApi.Catalog(categories.ToArray(), options);
            Debug.Log(report.Summarize());
            return report;
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Categories", EditorStyles.boldLabel);
            bool all = AllCategories.All(_chosen.Contains);
            bool newAll = EditorGUILayout.ToggleLeft("All", all);
            if (newAll != all)
            {
                _chosen.Clear();
                if (newAll) _chosen.AddRange(AllCategories);
                _composition = newAll;
            }
            using (new EditorGUI.IndentLevelScope())
                foreach (var c in AllCategories)
                {
                    bool on = _chosen.Contains(c);
                    if (EditorGUILayout.ToggleLeft(c.ToString(), on) != on)
                    {
                        if (on) _chosen.Remove(c);
                        else _chosen.Add(c);
                    }
                }
            using (new EditorGUI.IndentLevelScope())
                _composition = EditorGUILayout.ToggleLeft(new GUIContent("Composition", "The composition tables of the units (also imported with bodies and equipment)"), _composition);
            _force = EditorGUILayout.Toggle(new GUIContent("Force", "Import even records the ledger says are current"), _force);
            using (new EditorGUI.DisabledScope(_chosen.Count == 0 && !_composition))
                if (GUILayout.Button("Import")) _last = Import(_chosen.OrderBy(c => (int)c), _force, new RoImportOptions { Composition = _composition });

            if (_last == null) return;
            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            using (new EditorGUILayout.HorizontalScope())
                foreach (string h in new[] { "Category", "Entries", "Sheets", "Actions", "Warnings", "Failed", "Time" })
                    EditorGUILayout.LabelField(h, EditorStyles.boldLabel, GUILayout.Width(h == "Category" ? 110 : 70));
            foreach (var c in _last.Categories)
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(c.Category + (c.Skipped ? " (skipped)" : ""), GUILayout.Width(110));
                    foreach (string v in new[] { c.Entries, c.Sheets, c.Actions, c.Warnings, c.Failed }.Select(n => n.ToString()))
                        EditorGUILayout.LabelField(v, GUILayout.Width(70));
                    EditorGUILayout.LabelField($"{c.Time.TotalSeconds:0.0} s", GUILayout.Width(70));
                }
            EditorGUILayout.HelpBox(_last.Report.Summarize(), MessageType.None);
            EditorGUILayout.EndScrollView();
        }
    }

    /// <summary>The catalog menu items (PLAN-0008 4.7): the window, one item per category and All, all through <see cref="RoCatalogWindow.Import"/>.</summary>
    public static class RoCatalogMenu
    {
        const string Root = "Tools/Ragnarok/Catalog/";

        [MenuItem("Tools/Ragnarok/Import Catalog…", priority = 1)]
        static void OpenWindow() => RoCatalogWindow.Open();

        static void Run(params SpriteCatalogCategory[] c) => RoCatalogWindow.Import(c, force: false);

        [MenuItem(Root + "All", priority = 100)] static void All() => Run((SpriteCatalogCategory[])Enum.GetValues(typeof(SpriteCatalogCategory)));
        [MenuItem(Root + "Monsters", priority = 120)] static void Monsters() => Run(SpriteCatalogCategory.Monsters);
        [MenuItem(Root + "NPCs", priority = 121)] static void Npcs() => Run(SpriteCatalogCategory.Npcs);
        [MenuItem(Root + "Homunculi", priority = 122)] static void Homunculi() => Run(SpriteCatalogCategory.Homunculi);
        [MenuItem(Root + "Bodies", priority = 123)] static void Bodies() => Run(SpriteCatalogCategory.Bodies);
        [MenuItem(Root + "Heads", priority = 124)] static void Heads() => Run(SpriteCatalogCategory.Heads);
        [MenuItem(Root + "Headgears", priority = 125)] static void Headgears() => Run(SpriteCatalogCategory.Headgears);
        [MenuItem(Root + "Garments", priority = 126)] static void Garments() => Run(SpriteCatalogCategory.Garments);
        [MenuItem(Root + "Weapons", priority = 127)] static void Weapons() => Run(SpriteCatalogCategory.Weapons);
        [MenuItem(Root + "Shields", priority = 128)] static void Shields() => Run(SpriteCatalogCategory.Shields);
        [MenuItem(Root + "Ground Items", priority = 129)] static void GroundItems() => Run(SpriteCatalogCategory.GroundItems);
        [MenuItem(Root + "Skill Icons", priority = 130)] static void SkillIcons() => Run(SpriteCatalogCategory.SkillIcons);
        [MenuItem(Root + "Effects", priority = 131)] static void Effects() => Run(SpriteCatalogCategory.Effects);
        [MenuItem(Root + "Composition", priority = 133)] static void Composition() => RoCatalogWindow.Import(new SpriteCatalogCategory[0], force: false, new RoImportOptions { Composition = true });
        [MenuItem(Root + "Palettes", priority = 132)] static void Palettes() => Run(SpriteCatalogCategory.Palettes);
    }
}
