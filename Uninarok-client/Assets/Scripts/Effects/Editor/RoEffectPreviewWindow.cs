using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Uninarok.Effects.Editor
{
    /// <summary>
    /// Effect preview window (SPEC-0012 FR-11, plan 4.11), editor only, works with no scene open. Browse the effect keys (flat
    /// list or folder tree, text filter, virtualised) and play one in an <see cref="EffectPreviewStage"/>: play, pause, stop,
    /// loop, step by one frame, scrubber, speed. The play state is <see cref="EffectPreviewController"/>, the list logic
    /// <see cref="EffectBrowse"/> and the arithmetic <see cref="EffectPreviewMath"/>. Ticked from
    /// <see cref="EditorApplication.update"/>; the stage and the browse source are disposed when the window closes.
    /// </summary>
    public sealed class RoEffectPreviewWindow : EditorWindow
    {
        const float RowHeight = 18f;
        const float ListWidth = 330f;
        const float MaxFrameStep = 0.1f;

        EffectBrowseSource source;
        EffectCatalog catalog;
        EffectPreviewStage stage;
        EffectPreviewController player;

        string filter = "";
        bool treeView;
        readonly HashSet<string> collapsed = new HashSet<string>();
        List<BrowseRow> rows = new List<BrowseRow>();
        bool rowsDirty = true;
        Vector2 listScroll;
        int selectedKey = -1;
        double lastTime;
        Vector2 inspectScroll;
        Uninarok.RoImport.Core.Effects.RoEffectDataReport checkReport;
        readonly Dictionary<string, List<string>> runtimeWarnings = new Dictionary<string, List<string>>();

        [MenuItem("Tools/Ragnarok/Effect Preview")]
        static void Open()
        {
            var window = GetWindow<RoEffectPreviewWindow>();
            window.titleContent = new GUIContent("Effect Preview");
            window.minSize = new Vector2(760f, 420f);
            window.Show();
        }

        /// <summary>Selects and plays a key (for callers such as an inspector button).</summary>
        public void Select(string key)
        {
            if (source == null) return;
            for (int i = 0; i < source.Keys.Count; i++)
            {
                if (source.Keys[i] != key) continue;
                selectedKey = i;
                player.Play(key);
                Repaint();
                return;
            }
        }

        void OnEnable()
        {
            titleContent = new GUIContent("Effect Preview");
            Build();
            lastTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnUpdate;
        }

        void OnDisable()
        {
            EditorApplication.update -= OnUpdate;
            Teardown();
        }

        void Build()
        {
            Teardown();
            source = EffectBrowseSource.Load();
            catalog = source.CreateCatalog();
            stage = new EffectPreviewStage(catalog);
            player = new EffectPreviewController(stage);
            player.SetSpeed(1f);
            stage.Runtime.Warned += OnRuntimeWarned;
            runtimeWarnings.Clear();
            selectedKey = -1;
            rowsDirty = true;
        }

        void Teardown()
        {
            if (stage != null) stage.Runtime.Warned -= OnRuntimeWarned;
            stage?.Dispose();
            stage = null;
            player = null;
            catalog?.Release();
            catalog = null;
            source?.Dispose();
            source = null;
        }

        void OnUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min((float)(now - lastTime), MaxFrameStep);
            lastTime = now;
            if (player == null || !player.HasEffect) return;
            player.Tick(dt);
            Repaint();
        }

        void OnGUI()
        {
            if (stage == null) return;
            DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            DrawList();
            EditorGUILayout.BeginVertical();
            DrawView();
            DrawTransport();
            DrawInfo();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        // ---- browse ----

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            bool tree = GUILayout.Toggle(treeView, "Tree", EditorStyles.toolbarButton, GUILayout.Width(44f));
            if (tree != treeView) { treeView = tree; rowsDirty = true; }
            GUI.SetNextControlName("EffectFilter");
            string text = GUILayout.TextField(filter, EditorStyles.toolbarSearchField, GUILayout.Width(ListWidth - 120f));
            if (text != filter) { filter = text; rowsDirty = true; listScroll = Vector2.zero; }
            if (GUILayout.Button("Rescan", EditorStyles.toolbarButton, GUILayout.Width(52f))) Build();
            if (GUILayout.Button("Save check report", EditorStyles.toolbarButton, GUILayout.Width(110f))) SaveCheckReport();
            GUILayout.FlexibleSpace();
            GUILayout.Label((source.FromIndex ? "index" : "scan") + ", " + source.Keys.Count + " keys", EditorStyles.miniLabel);
            var bg = (EffectPreviewStage.BackgroundKind)EditorGUILayout.EnumPopup(stage.Background, EditorStyles.toolbarPopup, GUILayout.Width(70f));
            if (bg != stage.Background) stage.Background = bg;
            EditorGUILayout.EndHorizontal();
        }

        void RebuildRows()
        {
            var keys = source.Keys;
            var matches = EffectBrowse.Filter(keys, filter);
            bool filtering = !string.IsNullOrWhiteSpace(filter);
            rows = treeView ? EffectBrowse.TreeRows(keys, matches, filtering ? null : collapsed) : EffectBrowse.FlatRows(keys, matches);
            rowsDirty = false;
        }

        void DrawList()
        {
            if (rowsDirty) RebuildRows();
            Rect area = GUILayoutUtility.GetRect(ListWidth, ListWidth, 0f, 100000f, GUILayout.Width(ListWidth), GUILayout.ExpandHeight(true));
            GUI.Box(area, GUIContent.none);
            var content = new Rect(0f, 0f, area.width - 16f, rows.Count * RowHeight);
            listScroll = GUI.BeginScrollView(area, listScroll, content);

            EffectPreviewMath.VisibleRange(rows.Count, RowHeight, listScroll.y, area.height, 2, out int first, out int end);
            for (int r = first; r < end; r++)
            {
                var row = rows[r];
                var rect = new Rect(0f, r * RowHeight, content.width, RowHeight);
                if (row.IsFolder) DrawFolderRow(rect, row);
                else DrawKeyRow(rect, row);
            }
            GUI.EndScrollView();
        }

        void DrawFolderRow(Rect rect, BrowseRow row)
        {
            bool open = !collapsed.Contains(row.Folder);
            rect.xMin += row.Depth * 12f;
            bool now = EditorGUI.Foldout(rect, open, row.Label, true);
            if (now == open) return;
            if (now) collapsed.Remove(row.Folder); else collapsed.Add(row.Folder);
            rowsDirty = true;
        }

        void DrawKeyRow(Rect rect, BrowseRow row)
        {
            bool selected = row.KeyIndex == selectedKey;
            bool missing = source.IsSourceMissing(row.KeyIndex);
            if (selected) EditorGUI.DrawRect(rect, new Color(0.24f, 0.37f, 0.58f, 0.8f));
            var label = new Rect(rect.x + (treeView ? row.Depth * 12f + 14f : 2f), rect.y, rect.width, rect.height);
            string text = missing ? "! " + row.Label : row.Label;
            var content = new GUIContent(text, missing ? "source file is missing: " + source.Keys[row.KeyIndex] : source.Keys[row.KeyIndex]);
            var style = missing ? MissingStyle() : EditorStyles.label;
            if (GUI.Button(label, content, style))
            {
                selectedKey = row.KeyIndex;
                player.Play(source.Keys[row.KeyIndex]);
            }
        }

        GUIStyle missingStyle;
        GUIStyle MissingStyle()
        {
            if (missingStyle != null) return missingStyle;
            missingStyle = new GUIStyle(EditorStyles.label);
            missingStyle.normal.textColor = new Color(0.95f, 0.45f, 0.35f);
            return missingStyle;
        }

        // ---- view and transport ----

        void DrawView()
        {
            Rect rect = GUILayoutUtility.GetRect(100f, 100000f, 120f, 100000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            var e = Event.current;
            if (rect.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDrag && (e.button == 0 || e.button == 1))
                {
                    stage.Yaw += e.delta.x * 0.5f;
                    stage.Pitch = Mathf.Clamp(stage.Pitch + e.delta.y * 0.5f, -89f, 89f);
                    e.Use();
                    Repaint();
                }
                else if (e.type == EventType.ScrollWheel)
                {
                    stage.Distance = Mathf.Clamp(stage.Distance * (1f + e.delta.y * 0.05f), 2f, 80f);
                    e.Use();
                    Repaint();
                }
            }
            if (e.type == EventType.Repaint && rect.width > 1f && rect.height > 1f)
                GUI.DrawTexture(rect, stage.Render(rect), ScaleMode.StretchToFill, false);
        }

        void DrawTransport()
        {
            bool has = player.Key != null;
            EditorGUI.BeginDisabledGroup(!has);
            EditorGUILayout.BeginHorizontal();
            bool playing = has && !player.Paused && !player.Ended;
            if (GUILayout.Button(playing ? "Pause" : "Play", GUILayout.Width(56f))) player.TogglePause();
            if (GUILayout.Button("Stop", GUILayout.Width(44f))) player.Stop();
            if (GUILayout.Button("<|", GUILayout.Width(30f))) player.Step(-1);
            if (GUILayout.Button("|>", GUILayout.Width(30f))) player.Step(1);
            bool loop = GUILayout.Toggle(player.Loop, "Loop", GUILayout.Width(48f));
            if (loop != player.Loop) player.SetLoop(loop);
            GUILayout.Space(8f);
            GUILayout.Label("Speed", GUILayout.Width(40f));
            float speed = EditorGUILayout.DelayedFloatField(player.Speed, GUILayout.Width(48f));
            if (!Mathf.Approximately(speed, player.Speed)) player.SetSpeed(speed);
            if (GUILayout.Button("Save image", GUILayout.Width(80f))) SaveImage();
            EditorGUILayout.EndHorizontal();

            float length = Mathf.Max(player.Length, 0.0001f);
            EditorGUILayout.BeginHorizontal();
            float seconds = GUILayout.HorizontalSlider(player.Seconds, 0f, length, GUILayout.ExpandWidth(true));
            GUILayout.Label($"{EffectPreviewMath.FrameLabel(player.ShownFrame, player.Length, player.FrameRate)}  {player.Seconds:0.00}/{player.Length:0.00} s", GUILayout.Width(210f));
            EditorGUILayout.EndHorizontal();
            if (has && GUI.changed && !Mathf.Approximately(seconds, player.Seconds))
            {
                player.Pause();
                player.SeekSeconds(seconds);
            }
            EditorGUI.EndDisabledGroup();
        }

        void DrawInfo()
        {
            if (player.Key == null) { EditorGUILayout.LabelField("Select an effect on the left."); return; }
            if (!player.HasEffect) EditorGUILayout.LabelField("Nothing is playing for this key (unknown to the catalog or it failed to load).", EditorStyles.miniLabel);
            if (!catalog.TryResolve(player.Key, out var entry) || !catalog.TryGetAsset(entry, out var asset)) return;

            var input = MakeInput(asset);
            input.Speed = player.Speed;
            input.CheckLines = EffectInspectorModel.CheckLinesFor(checkReport, player.Key);
            if (runtimeWarnings.TryGetValue(player.Key, out var lines)) input.RuntimeLines = lines;
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Run check", GUILayout.Width(80f))) RunCheck();
            GUILayout.Label(checkReport == null ? "check not run" : "check run", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            inspectScroll = EditorGUILayout.BeginScrollView(inspectScroll, GUILayout.MaxHeight(260f));
            EffectInspectorPanel.Draw(EffectInspectorModel.Build(asset, input));
            EditorGUILayout.EndScrollView();
        }

        void RunCheck()
        {
            try { checkReport = Uninarok.RoImport.Editor.Effects.RoEffectMenu.RunEffectDataCheck(); }
            catch (System.Exception e) { Debug.LogWarning("[Effects] Effect data check failed: " + e.Message); }
        }

        /// <summary>Renders the stage at the window size and writes it to the evidence folder as <c>&lt;key&gt;-f&lt;frame&gt;.png</c> (T8.4).</summary>
        void SaveImage()
        {
            if (stage == null || player.Key == null) return;
            byte[] png = EffectEvidence.CapturePng(stage, (int)position.width, (int)position.height);
            string path = EffectEvidence.WriteImage(png, EffectEvidence.DefaultFolder, player.Key, player.ShownFrame);
            Debug.Log("[Effects] Saved image " + path);
        }

        /// <summary>Runs the same check as the "Check Effect Data" menu item and writes its <c>ToText()</c> to the evidence folder (T8.4).</summary>
        void SaveCheckReport()
        {
            Uninarok.RoImport.Core.Effects.RoEffectDataReport report;
            try { report = Uninarok.RoImport.Editor.Effects.RoEffectMenu.RunEffectDataCheck(); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Effects] Effect data check failed: " + e.Message);
                return;
            }
            checkReport = report;
            string path = EffectEvidence.WriteCheckReport(report.ToText(), EffectEvidence.DefaultFolder);
            Debug.Log("[Effects] Saved check report " + path);
        }

        void OnRuntimeWarned(string key, string message)
        {
            if (key == null) return;
            if (!runtimeWarnings.TryGetValue(key, out var list)) runtimeWarnings[key] = list = new List<string>();
            list.Add(message);
        }

        /// <summary>The inspector input from the project's effect settings and importer userData (no scene needed).</summary>
        public static EffectInspectorInput MakeInput(EffectAsset asset)
        {
            var settings = AssetDatabase.LoadAssetAtPath<EffectSettings>(EffectPreviewStage.SettingsPath);
            var input = new EffectInspectorInput
            {
                UserDataOf = t =>
                {
                    string path = AssetDatabase.GetAssetPath(t);
                    return string.IsNullOrEmpty(path) ? null : AssetImporter.GetAtPath(path)?.userData;
                },
            };
            if (settings != null) { input.AuthoredFrameRate = settings.AuthoredFrameRate; input.Placeholder = settings.PlaceholderTexture; }
            return input;
        }

        /// <summary>Opens the window and plays the key (used by the "Open preview" button of <see cref="EffectAssetEditor"/>).</summary>
        public static void OpenFor(string key)
        {
            Open();
            GetWindow<RoEffectPreviewWindow>().Select(key);
        }
    }
}
