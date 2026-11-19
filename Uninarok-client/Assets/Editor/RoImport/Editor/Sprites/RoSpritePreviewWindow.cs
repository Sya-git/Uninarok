using System;
using UnityEditor;
using UnityEngine;
using Uninarok.Sprites;
using Object = UnityEngine.Object;

namespace Uninarok.RoImport.Editor.Sprites
{
    /// <summary>
    /// Plays a <see cref="SpriteAnimation"/> in a preview scene (PLAN-0007 4.8, SPEC-0007 FR-10): animation number,
    /// direction picker (8 buttons as a compass, direction 0 at the bottom), play / pause / step, speed, missing-action policy,
    /// a palette slot, the origin (red) and the anchors (blue). Editor only; it advances its own actor with
    /// <see cref="SpriteActor.Step"/> (D15), so nothing in the scene moves.
    /// </summary>
    public sealed class RoSpritePreviewWindow : EditorWindow
    {
        [SerializeField] SpriteAnimation _animation;
        [SerializeField] int _anim;
        [SerializeField] int _direction;
        [SerializeField] float _speed = 1f;
        [SerializeField] bool _playing = true;
        [SerializeField] SpriteMissingAction _missing = SpriteMissingAction.DrawNothing;
        [SerializeField] Texture2D _palette;
        [SerializeField] float _zoom = 1f;

        PreviewRenderUtility _preview;
        GameObject _root;
        SpriteActor _actor;
        Material _material;
        double _lastTime;

        /// <summary>Direction buttons laid out as a compass: direction 0 faces the viewer (south), counter-clockwise from there.</summary>
        static readonly int[,] Compass = { { 3, 4, 5 }, { 2, -1, 6 }, { 1, 0, 7 } };

        public static RoSpritePreviewWindow Open(SpriteAnimation animation)
        {
            var w = GetWindow<RoSpritePreviewWindow>("Sprite Preview");
            w.SetAnimation(animation);
            w.Show();
            return w;
        }

        [MenuItem("Tools/Ragnarok/Sprite Preview", priority = 40)]
        static void OpenEmpty() => Open(Selection.activeObject as SpriteAnimation);

        public void SetAnimation(SpriteAnimation animation)
        {
            _animation = animation;
            _anim = 0;
            Rebuild();
        }

        void OnEnable()
        {
            _lastTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnUpdate;
            Rebuild();
        }

        void OnDisable()
        {
            EditorApplication.update -= OnUpdate;
            Cleanup();
        }

        void Cleanup()
        {
            if (_root != null) DestroyImmediate(_root);
            if (_material != null) DestroyImmediate(_material);
            _preview?.Cleanup();
            _preview = null;
            _root = null;
            _actor = null;
        }

        void Rebuild()
        {
            Cleanup();
            if (_animation == null) return;

            _preview = new PreviewRenderUtility();
            var cam = _preview.camera;
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.36f, 0.42f, 0.36f, 1f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100f;

            var shader = Shader.Find(RoSpriteConverter.ShaderName);
            if (shader != null) _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            _root = new GameObject("Sprite Preview") { hideFlags = HideFlags.HideAndDontSave };
            _root.SetActive(false);
            _actor = _root.AddComponent<SpriteActor>();
            _actor.Data = _animation;
            _actor.Material = _material;
            _actor.Billboard = false;
            _root.SetActive(true);
            _preview.AddSingleGO(_root);
            Play(restart: true);
        }

        void Play(bool restart)
        {
            if (_actor == null) return;
            _actor.Missing = _missing;
            _actor.Speed = _speed > 0.01f ? 1f / _speed : 100f;   // the window's speed is playback speed; the actor's multiplies frame time
            _actor.Play(_anim, _direction, restart);
            SpritePalette.Apply(_actor, _palette);
            _actor.Step(0f);
            Repaint();
        }

        void OnUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = (float)(now - _lastTime);
            _lastTime = now;
            if (_actor == null || !_playing) return;
            int before = _actor.CurrentFrame;
            _actor.Step(delta);
            if (_actor.CurrentFrame != before) Repaint();
        }

        void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            var picked = (SpriteAnimation)EditorGUILayout.ObjectField("Animation", _animation, typeof(SpriteAnimation), false);
            if (EditorGUI.EndChangeCheck()) SetAnimation(picked);
            if (_animation == null)
            {
                EditorGUILayout.HelpBox("Pick a converted sprite (a SpriteAnimation asset).", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    int max = Math.Max(0, _animation.AnimationCount - 1);
                    _anim = EditorGUILayout.IntField($"Animation (0–{max})", _anim);
                    _speed = EditorGUILayout.Slider("Speed", _speed, 0.1f, 4f);
                    _missing = (SpriteMissingAction)EditorGUILayout.EnumPopup("Missing action", _missing);
                    _palette = (Texture2D)EditorGUILayout.ObjectField("Palette", _palette, typeof(Texture2D), false);
                    _zoom = EditorGUILayout.Slider("Zoom", _zoom, 0.25f, 8f);
                    bool restart = EditorGUI.EndChangeCheck();

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(_playing ? "Pause" : "Play")) _playing = !_playing;
                        if (GUILayout.Button("Step"))
                        {
                            _playing = false;
                            var a = _animation.Actions[Math.Max(0, _actor.UniqueAction)];
                            _actor.Step(a.FrameMs * _actor.Speed / 1000f);
                            Repaint();
                        }
                        if (GUILayout.Button("Restart")) Play(true);
                    }
                    if (restart) Play(true);

                    EditorGUILayout.LabelField("Frame", _actor == null || _actor.CurrentFrame < 0 ? "–" :
                        $"{_actor.CurrentFrame + 1} / {_animation.Actions[_actor.UniqueAction].FrameCount}  ({_animation.Actions[_actor.UniqueAction].FrameMs:0.#} ms)");
                    EditorGUILayout.LabelField("Action", _actor == null || !_actor.HasAction ? "missing: draws nothing" : $"unique {_actor.UniqueAction}");
                }

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(110)))
                {
                    EditorGUILayout.LabelField("Direction", EditorStyles.boldLabel);
                    for (int row = 0; row < 3; row++)
                        using (new EditorGUILayout.HorizontalScope())
                            for (int col = 0; col < 3; col++)
                            {
                                int d = Compass[row, col];
                                if (d < 0)
                                {
                                    GUILayout.Space(34);
                                    continue;
                                }
                                var style = d == _direction ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
                                if (GUILayout.Toggle(d == _direction, d.ToString(), style, GUILayout.Width(30)) && d != _direction)
                                {
                                    _direction = d;
                                    Play(restart: false);   // a direction change keeps the frame (plan 4.6)
                                }
                            }
                }
            }

            var rect = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (Event.current.type == EventType.Repaint && _preview != null)
            {
                var texture = Render(rect);
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
                DrawMarkers(rect);
            }
        }

        /// <summary>World units visible vertically at zoom 1: about 4 GAT cells.</summary>
        float OrthoSize => 1f / _zoom;

        /// <summary>The origin sits a quarter from the bottom, so a unit standing on it is centred.</summary>
        Vector3 CameraOffset => new Vector3(0f, OrthoSize * 0.5f, -10f);

        /// <summary>Renders the actor into a texture of <paramref name="rect"/>'s size (also used to save evidence images).</summary>
        internal Texture Render(Rect rect)
        {
            _preview.camera.orthographicSize = OrthoSize;
            _preview.camera.transform.position = CameraOffset;
            _preview.BeginPreview(rect, GUIStyle.none);
            _preview.camera.Render();
            return _preview.EndPreview();
        }

        void DrawMarkers(Rect rect)
        {
            Mark(rect, Vector3.zero, Color.red);
            if (_actor == null) return;
            for (int i = 0; _actor.TryGetAnchor(i, out var p); i++) Mark(rect, p, new Color(0f, 0.6f, 1f));
        }

        void Mark(Rect rect, Vector3 local, Color color)
        {
            float pixelsPerUnit = rect.height / (2f * OrthoSize);
            var offset = CameraOffset;
            var centre = rect.center + new Vector2((local.x - offset.x) * pixelsPerUnit, -(local.y - offset.y) * pixelsPerUnit);
            EditorGUI.DrawRect(new Rect(centre.x - 4, centre.y - 0.5f, 9, 1), color);
            EditorGUI.DrawRect(new Rect(centre.x - 0.5f, centre.y - 4, 1, 9), color);
        }

        /// <summary>Test and evidence hook: shows <paramref name="animation"/> at a frame and saves the preview as a PNG.</summary>
        internal void SaveFrame(string pngPath, int animation, int direction, float seconds, int size = 256)
        {
            _anim = animation;
            _direction = direction;
            _playing = false;
            Play(restart: true);
            _actor.Step(seconds);
            var rt = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32);
            var cam = _preview.camera;
            cam.orthographicSize = OrthoSize;
            cam.transform.position = CameraOffset;
            var previousTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = previousTarget;
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var read = new Texture2D(size, size, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            read.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            System.IO.File.WriteAllBytes(pngPath, read.EncodeToPNG());
            Object.DestroyImmediate(read);
        }
    }
}
