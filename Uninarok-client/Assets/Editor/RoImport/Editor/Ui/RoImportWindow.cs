using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Pipeline;
using RoImportApi = Uninarok.RoImport.Editor.Pipeline.RoImport;   // the class name equals our root namespace

namespace Uninarok.RoImport.Editor.Ui
{
    /// <summary>
    /// The import window (plan 4.11, D13; SPEC-0002 FR-2): source root with Browse, a kind, a list of Ragnarok paths or
    /// folder prefixes, Scan, Force and Convert. It only builds jobs and calls <see cref="RoImportApi.Run"/>, so the window
    /// and the code path cannot differ.
    /// </summary>
    public sealed class RoImportWindow : EditorWindow
    {
        const string PathsPref = "Uninarok.RoImport.Window.Paths";

        [SerializeField] int _kind;
        [SerializeField] bool _force;
        [SerializeField] bool _gatImage;
        [SerializeField] bool _spritePalettes;
        [SerializeField] string _paths = "texture/";
        [SerializeField] Vector2 _scroll;

        string _scan;
        string _summary;

        public static void Open() => GetWindow<RoImportWindow>("Ragnarok Import").Show();

        void OnEnable() => _paths = EditorPrefs.GetString(PathsPref, _paths);

        void OnGUI()
        {
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                string root = RoSourceLocation.Resolve();
                EditorGUILayout.SelectableLabel(root, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("Browse", GUILayout.Width(70)))
                {
                    string chosen = EditorUtility.OpenFolderPanel("Ragnarok data folder", Directory.Exists(root) ? root : "", "");
                    if (!string.IsNullOrEmpty(chosen)) RoSourceLocation.Set(chosen);
                }
                using (new EditorGUI.DisabledScope(!RoSourceLocation.IsOverridden))
                    if (GUILayout.Button("Default", GUILayout.Width(70))) RoSourceLocation.Clear();
            }
            if (!Directory.Exists(RoSourceLocation.Resolve()))
                EditorGUILayout.HelpBox("The source folder does not exist.", MessageType.Warning);

            EditorGUILayout.Space();
            var kinds = RoConverterRegistry.All.Select(c => c.Kind).Where(k => k != RoJobKind.CatalogEntry && k != RoJobKind.CatalogIndex).ToArray();   // the catalog has its own window (PLAN-0008 4.7)
            _kind = Mathf.Clamp(EditorGUILayout.Popup("Kind", _kind, kinds.Select(k => k.ToString()).ToArray()), 0, kinds.Length - 1);
            _force = EditorGUILayout.Toggle(new GUIContent("Force", "Convert even items the ledger says are current"), _force);
            if (kinds[_kind] == RoJobKind.MapData)
                _gatImage = EditorGUILayout.Toggle(new GUIContent("With Image", "Also write the cell type image of each map (PLAN-0005 4.5)"), _gatImage);
            if (kinds[_kind] == RoJobKind.Sprite)
                _spritePalettes = EditorGUILayout.Toggle(new GUIContent("With Palettes", "Also write the index texture, so palettes can recolour the sprites (PLAN-0007 4.4)"), _spritePalettes);

            EditorGUILayout.LabelField("Ragnarok paths or folders (end a folder with /; / alone is the data root), one per line");
            EditorGUI.BeginChangeCheck();
            _paths = EditorGUILayout.TextArea(_paths, GUILayout.MinHeight(80));
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetString(PathsPref, _paths);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan")) _scan = Scan(kinds[_kind]);
                if (GUILayout.Button("Convert")) Convert(kinds[_kind]);
            }

            if (!string.IsNullOrEmpty(_scan) || !string.IsNullOrEmpty(_summary))
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                if (!string.IsNullOrEmpty(_summary)) EditorGUILayout.HelpBox(_summary, MessageType.Info);
                if (!string.IsNullOrEmpty(_scan)) EditorGUILayout.HelpBox(_scan, MessageType.None);
                EditorGUILayout.EndScrollView();
            }
        }

        RoImportJobs.Expansion Expand(RoJobKind kind) =>
            RoImportJobs.Expand(_paths, kind, new RoFolderSource(RoSourceLocation.Resolve()));

        string Scan(RoJobKind kind)
        {
            var expansion = Expand(kind);
            const int shown = 40;
            string list = string.Join("\n", expansion.Jobs.Take(shown).Select(j => j.Path.Canonical));
            if (expansion.Jobs.Count > shown) list += $"\n... and {expansion.Jobs.Count - shown} more";
            string invalid = expansion.Invalid.Count == 0 ? "" : "\n\nIgnored:\n" + string.Join("\n", expansion.Invalid);
            return $"{expansion.Jobs.Count} item(s):\n{list}{invalid}";
        }

        void Convert(RoJobKind kind)
        {
            var expansion = Expand(kind);
            if (expansion.Jobs.Count == 0)
            {
                _summary = "Nothing to convert.";
                return;
            }
            RoReport report = RoImportApi.Run(expansion.Jobs, new RoImportOptions { Force = _force, GatImage = _gatImage, SpritePalettes = _spritePalettes });   // logs the summary once
            _summary = report.Summarize();
            _scan = null;
        }
    }
}
