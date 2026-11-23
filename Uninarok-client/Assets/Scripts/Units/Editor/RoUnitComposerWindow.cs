using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using Uninarok.Catalog;
using Uninarok.Networking.Data;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Editor.Catalog;
using Object = UnityEngine.Object;

namespace Uninarok.Units.Editor
{
    /// <summary>
    /// <c>Tools/Ragnarok/Unit Composer</c> (PLAN-0009 4.9; SPEC-0009 FR-15): builds one unit from ids exactly as the game does
    /// and shows it from a turnable camera: playback, the 8 directions side by side, the parts with their frames, order and
    /// how each was found, and evidence images.
    /// </summary>
    public sealed class RoUnitComposerWindow : EditorWindow
    {
        const string EvidenceFolder = "../docs/specs/0009-unit-display/evidence";

        enum IdMode
        {
            ViewId,
            ItemId,
        }

        [Serializable]
        struct Equipment
        {
            public IdMode Mode;
            public int Id;

            public Equipment(IdMode mode, int id)
            {
                Mode = mode;
                Id = id;
            }
        }

        [SerializeField] UnitKind _kind = UnitKind.Player;
        [SerializeField] int _unitJob = 1002;
        [SerializeField] PlayerJob _class = PlayerJob.JOB_SWORDMAN;
        [SerializeField] AccountSex _sex = AccountSex.SEX_MALE;
        [SerializeField] int _bodyStyle, _hairStyle = 2, _hairColour = 3, _clothColour;
        [SerializeField] Equipment _top = new Equipment(IdMode.ItemId, 2220), _mid, _low = new Equipment(IdMode.ItemId, 2218);
        [SerializeField] Equipment _garment = new Equipment(IdMode.ViewId, 1);
        // The hands as typed (D35): the value is a view id or an item id by its size; the mode is not used.
        [SerializeField, FormerlySerializedAs("_weapon")] Equipment _right = new Equipment(IdMode.ItemId, 1101);
        [SerializeField, FormerlySerializedAs("_shield")] Equipment _left = new Equipment(IdMode.ItemId, 2101);

        [SerializeField] int _animation;
        [SerializeField] PlayMode _mode = PlayMode.Loop;
        [SerializeField] UninarokDirection _facing = UninarokDirection.South;
        [SerializeField] HeadFacing _head;
        [SerializeField] bool _playing = true;
        [SerializeField] float _speed = 1f;
        [SerializeField] bool _grid, _ground = true, _shadow = true, _ridingEdits;
        [SerializeField] float _yaw, _pitch = 50f, _distance = 12f;

        UnitPreviewStage _stage;
        ItemSpriteTable _items;
        string _problem;
        bool _ended;
        double _lastTime;
        Vector2 _leftScroll, _rightScroll;
        LoadReport _handsReport;
        string _handsText = "";
        Texture2D _gridImage;
        int _gridKey;

        /// <summary>World facings as a compass seen from above, north at the top.</summary>
        static UninarokDirection[,] Compass => new[,]
        {
            { UninarokDirection.NorthWest, UninarokDirection.North, UninarokDirection.NorthEast },
            { UninarokDirection.West, (UninarokDirection)255, UninarokDirection.East },
            { UninarokDirection.SouthWest, UninarokDirection.South, UninarokDirection.SouthEast },
        };

        [MenuItem("Tools/Ragnarok/Unit Composer", priority = 41)]
        public static void Open() => GetWindow<RoUnitComposerWindow>("Unit Composer").Show();

        void OnEnable()
        {
            _lastTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnUpdate;
        }

        void OnDisable()
        {
            EditorApplication.update -= OnUpdate;
            _stage?.Dispose();
            _stage = null;
            if (_gridImage != null) DestroyImmediate(_gridImage);
        }

        bool EnsureStage()
        {
            if (_stage != null) return true;
            var source = AssetDatabaseUnitAssetSource.Create();
            if (source == null)
            {
                _problem = "The catalog is not imported: run Tools/Ragnarok/Import Catalog… first.";
                return false;
            }
            _problem = source.Tables == null ? "The composition tables are not imported (Tools/Ragnarok/Catalog/Composition): default rules are used." : null;
            _items = AssetDatabase.LoadAssetAtPath<ItemSpriteTable>(RoCatalogOutputPaths.Items(new RoOutputRoots()));
            _stage = new UnitPreviewStage(source);
            _stage.View.AnimationEnded += _ => _ended = true;
            ApplyLook();
            PlayAnimation();
            return true;
        }

        void OnUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = (float)(now - _lastTime);
            _lastTime = now;
            if (_stage == null || !_playing) return;
            int frame = _stage.View.Frame, ticks = _stage.View.Clock.Ticks;
            _stage.Step(delta * _speed);
            if (_stage.View.Frame != frame || _stage.View.Clock.Ticks != ticks) Repaint();
        }

        // ---- look ----

        UnitLook Look()
        {
            if (_kind == UnitKind.Unit) return UnitLook.ForJob(_unitJob);
            var look = new UnitLook
            {
                Kind = UnitKind.Player, JobId = (int)_class, Sex = _sex, BodyStyle = _bodyStyle, HairStyle = _hairStyle, HairColour = _hairColour, ClothColour = _clothColour,
                HeadTop = ViewOf(_top), HeadMid = ViewOf(_mid), HeadBottom = ViewOf(_low), Garment = ViewOf(_garment),
            };
            ComposerHands.Apply(ref look, _right.Id, _left.Id);
            return look;
        }

        // The item table's view id, as the server sends it; an unknown item keeps its number, so the report says it is missing.
        int ViewOf(Equipment e)
        {
            if (e.Mode == IdMode.ViewId || e.Id <= 0) return e.Id;
            if (_items == null) return e.Id;
            int i = Array.IndexOf(_items.ItemIds, e.Id);
            return i >= 0 && _items.ViewIds[i] > 0 ? _items.ViewIds[i] : e.Id;
        }

        void ApplyLook()
        {
            _stage.SetLook(Look());
            _gridKey = 0;
        }

        void PlayAnimation()
        {
            _ended = false;
            if (IsPlayerLook) _stage.View.Play((PcAnimation)_animation, _mode);
            else _stage.View.Play((MonsterAnimation)_animation, _mode);
            _stage.Step(0f);
        }

        bool IsPlayerLook => Look().Kind == UnitKind.Player;

        // ---- GUI ----

        void OnGUI()
        {
            if (!EnsureStage())
            {
                EditorGUILayout.HelpBox(_problem, MessageType.Warning);
                return;
            }
            if (_problem != null) EditorGUILayout.HelpBox(_problem, MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (var scroll = new EditorGUILayout.ScrollViewScope(_leftScroll, GUILayout.Width(330)))
                {
                    _leftScroll = scroll.scrollPosition;
                    Inputs();
                    Playback();
                    CameraInputs();
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    Preview();
                    PartsList();
                }
            }
        }

        void Inputs()
        {
            EditorGUILayout.LabelField("Look", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            bool wasPlayer = IsPlayerLook;
            _kind = (UnitKind)EditorGUILayout.EnumPopup("Mode", _kind);
            if (_kind == UnitKind.Unit) _unitJob = EditorGUILayout.IntField("Job id", _unitJob);
            else
            {
                _class = (PlayerJob)EditorGUILayout.EnumPopup("Class", _class);
                _sex = (AccountSex)EditorGUILayout.EnumPopup("Sex", _sex);
                EditorGUILayout.LabelField("Race", _stage.Source.Catalog.Classes != null ? _stage.Source.Catalog.Classes.RaceOf(_class).ToString() : "?");
                _bodyStyle = EditorGUILayout.IntField("Body style", _bodyStyle);
                _hairStyle = EditorGUILayout.IntField("Hair style", _hairStyle);
                _hairColour = EditorGUILayout.IntField("Hair colour", _hairColour);
                _clothColour = EditorGUILayout.IntField("Cloth colour", _clothColour);
                _top = Field("Top headgear", _top);
                _mid = Field("Mid headgear", _mid);
                _low = Field("Low headgear", _low);
                _garment = Field("Garment", _garment);
                _right.Id = EditorGUILayout.IntField(new GUIContent("Right hand", "As the server sends it: a view id (below 500) or an item id (500 or more)"), _right.Id);
                _left.Id = EditorGUILayout.IntField(new GUIContent("Left hand", "As the server sends it: a view id (a shield) or an item id"), _left.Id);
            }
            if (!EditorGUI.EndChangeCheck()) return;
            if (IsPlayerLook != wasPlayer) _animation = 0;
            ApplyLook();
            if (IsPlayerLook != wasPlayer) PlayAnimation();
        }

        static Equipment Field(string label, Equipment e)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                e.Id = EditorGUILayout.IntField(label, e.Id);
                e.Mode = (IdMode)EditorGUILayout.EnumPopup(e.Mode, GUILayout.Width(70));
            }
            return e;
        }

        void Playback()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _animation = IsPlayerLook
                ? (int)(PcAnimation)EditorGUILayout.EnumPopup("Animation", (PcAnimation)_animation)
                : (int)(MonsterAnimation)EditorGUILayout.EnumPopup("Animation", (MonsterAnimation)_animation);
            _mode = (PlayMode)EditorGUILayout.EnumPopup("Play mode", _mode);
            if (EditorGUI.EndChangeCheck()) PlayAnimation();

            var view = _stage.View;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(_playing ? "Pause" : "Play")) _playing = !_playing;
                if (GUILayout.Button("Step"))
                {
                    _playing = false;
                    _stage.StepFrame();
                }
                if (GUILayout.Button("Restart")) PlayAnimation();
            }
            var action = _stage.BodyAction;
            EditorGUI.BeginChangeCheck();
            int frame = EditorGUILayout.IntSlider("Frame", Mathf.Max(0, view.Frame), 0, Mathf.Max(0, action.FrameCount - 1));
            if (EditorGUI.EndChangeCheck())
            {
                _playing = false;
                // While idle or sitting the frame is the head facing.
                if (IsPlayerLook && UnitAnimations.IsHeadFrame(_animation)) _head = (HeadFacing)Mathf.Clamp(frame, 0, 2);
                _stage.SetFrame(frame);
            }
            _speed = EditorGUILayout.Slider("Speed", _speed, 0.1f, 4f);
            EditorGUILayout.LabelField("Body frame", view.Frame < 0 ? "nothing is shown"
                : $"{view.Frame + 1} / {action.FrameCount}  ({action.FrameMs:0.#} ms each){(_ended ? "  ended" : view.Clock.Finished ? "  held" : "")}");

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(130)))
                {
                    EditorGUILayout.LabelField("World facing", GUILayout.Width(120));
                    var compass = Compass;
                    for (int row = 0; row < 3; row++)
                        using (new EditorGUILayout.HorizontalScope())
                            for (int col = 0; col < 3; col++)
                            {
                                var d = compass[row, col];
                                if ((int)d > 7)
                                {
                                    GUILayout.Space(40);
                                    continue;
                                }
                                if (GUILayout.Toggle(_facing == d, Short(d), EditorStyles.miniButton, GUILayout.Width(36))) _facing = d;
                            }
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField("Head", GUILayout.Width(80));
                    _head = (HeadFacing)EditorGUILayout.EnumPopup(_head, GUILayout.Width(90));
                    EditorGUILayout.LabelField($"shown direction {view.ShownDirection}", GUILayout.Width(150));
                }
            }
            view.Facing = _facing;
            view.HeadFacing = _head;
        }

        static string Short(UninarokDirection d)
        {
            switch (d)
            {
                case UninarokDirection.North: return "N";
                case UninarokDirection.NorthWest: return "NW";
                case UninarokDirection.West: return "W";
                case UninarokDirection.SouthWest: return "SW";
                case UninarokDirection.South: return "S";
                case UninarokDirection.SouthEast: return "SE";
                case UninarokDirection.East: return "E";
                default: return "NE";
            }
        }

        void CameraInputs()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview camera", EditorStyles.boldLabel);
            _yaw = EditorGUILayout.Slider("Turn", _yaw, -180f, 360f);
            _pitch = EditorGUILayout.Slider("Tilt", _pitch, 0f, 89f);
            _distance = EditorGUILayout.Slider("Distance", _distance, 5f, 40f);
            using (new EditorGUILayout.HorizontalScope())
                foreach (float turn in new[] { 0f, 22f, 23f, 90f, 180f, 270f })
                    if (GUILayout.Button(turn + "°")) _yaw = turn;
            EditorGUILayout.LabelField("Sector", UnitFacing.CameraSector(_yaw).ToString());
            _ground = EditorGUILayout.Toggle("Ground", _ground);
            _shadow = EditorGUILayout.Toggle("Shadow", _shadow);
            _ridingEdits = EditorGUILayout.Toggle(new GUIContent("Riding edits", "Apply ridingspreditinfo (off in the game: PLAN-0009 D17)"), _ridingEdits);
            _grid = EditorGUILayout.Toggle("8 directions", _grid);
            _stage.Yaw = _yaw;
            _stage.Pitch = _pitch;
            _stage.Distance = _distance;
            _stage.ShowGround = _ground;
            _stage.View.ShadowEnabled = _shadow;
            if (_stage.View.RidingEdits != _ridingEdits) _stage.View.RidingEdits = _ridingEdits;
            if (GUILayout.Button("Save image…")) SaveImage();
        }

        void SaveImage()
        {
            string folder = Path.GetFullPath(EvidenceFolder);
            if (!Directory.Exists(folder)) folder = Application.dataPath;
            string name = (IsPlayerLook ? $"{_class}-{_sex}" : "unit-" + _unitJob) + "-" + (IsPlayerLook ? ((PcAnimation)_animation).ToString() : ((MonsterAnimation)_animation).ToString())
                          + (_grid ? "-8dir" : "-" + _facing) + $"-turn{_yaw:0}";
            string path = EditorUtility.SaveFilePanel("Save image", folder, name, "png");
            if (!string.IsNullOrEmpty(path)) _stage.Save(path, _grid, 960, 960);
        }

        void Preview()
        {
            var rect = GUILayoutUtility.GetRect(200, 260, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (Event.current.type == EventType.MouseDrag && rect.Contains(Event.current.mousePosition))
            {
                _yaw = Mathf.Repeat(_yaw + Event.current.delta.x * 0.5f + 180f, 360f) - 180f;
                _pitch = Mathf.Clamp(_pitch + Event.current.delta.y * 0.3f, 0f, 89f);
                Event.current.Use();
                Repaint();
            }
            if (Event.current.type != EventType.Repaint || rect.width < 8f) return;
            if (!_grid)
            {
                GUI.DrawTexture(rect, _stage.Render(rect), ScaleMode.StretchToFill, false);
                return;
            }
            int w = Mathf.Max(32, (int)(rect.width / 4f)), h = Mathf.Max(32, (int)(rect.height / 2f));
            int key = HashCode.Combine(_stage.View.Frame, _stage.View.Clock.Ticks, _yaw, _pitch, _distance, w, HashCode.Combine(h, (int)_head, _ground, _shadow, _ridingEdits, _animation), _gridKey);
            if (_gridImage == null || key != _gridImageKey)
            {
                if (_gridImage != null) DestroyImmediate(_gridImage);
                _gridImage = _stage.CaptureGrid(w, h);
                _gridImageKey = key;
            }
            GUI.DrawTexture(rect, _gridImage, ScaleMode.StretchToFill, false);
        }

        int _gridImageKey;

        // The report's hands line (D35); Describe allocates, so it is rebuilt only when the report is a new one.
        string HandsLine()
        {
            var report = _stage.View.Report;
            if (report != _handsReport)
            {
                _handsReport = report;
                _handsText = report != null && IsPlayerLook ? report.Hands.Describe() : "";
            }
            return _handsText;
        }

        void PartsList()
        {
            EditorGUILayout.LabelField("Hands", HandsLine(), EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Parts (slot order; “order” is back to front in this frame)", EditorStyles.boldLabel);
            using (var scroll = new EditorGUILayout.ScrollViewScope(_rightScroll, GUILayout.Height(210)))
            {
                _rightScroll = scroll.scrollPosition;
                foreach (var row in _stage.Rows())
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(!row.Found))
                        {
                            bool show = EditorGUILayout.ToggleLeft(row.Slot.ToString(), !row.Hidden, GUILayout.Width(105));
                            if (show == row.Hidden)
                            {
                                _stage.View.SetPartHidden(row.Slot, !show);
                                _stage.Step(0f);
                                _gridKey++;
                            }
                        }
                        if (row.Found)
                            EditorGUILayout.LabelField(
                                row.Action < 0 ? "no such animation: draws nothing"
                                    : $"order {row.Order}  frame {row.Frame + 1}/{row.FrameCount}  action {row.Action}  own {row.OwnFrameMs:0.#} ms  offset ({row.OffsetPx.x:0}, {row.OffsetPx.y:0})",
                                GUILayout.Width(330));
                        else EditorGUILayout.LabelField("MISSING", EditorStyles.boldLabel, GUILayout.Width(330));
                        EditorGUILayout.LabelField(row.Resolution, GUILayout.ExpandWidth(true));
                    }
            }
        }
    }
}
