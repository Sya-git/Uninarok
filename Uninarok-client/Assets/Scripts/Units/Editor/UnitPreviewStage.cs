using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Uninarok.Networking.Data;
using Uninarok.Sprites;
using Object = UnityEngine.Object;

namespace Uninarok.Units.Editor
{
    /// <summary>
    /// A preview scene with one unit and a camera (PLAN-0009 4.9; SPEC-0009 FR-15): the unit is the game's
    /// <see cref="UnitView"/>, loaded by the game's <see cref="UnitLoader"/> and stepped through
    /// <see cref="UnitSystem.StepOne"/>; only the asset source differs. The camera is the game's 15° perspective on the same
    /// sphere as the rig's orbit. Dispose it to release everything.
    /// </summary>
    public sealed class UnitPreviewStage : IDisposable
    {
        public const string UnitMaterialPath = "Assets/Art/Materials/sprite_unit.mat";
        const float FieldOfView = 15f;

        /// <summary>One line of the parts list.</summary>
        public struct PartRow
        {
            public PartSlot Slot;
            public bool Found;
            /// <summary>How it was found, or why it is missing.</summary>
            public string Resolution;
            public bool Hidden;
            /// <summary>The part's unique action, −1 when the part has no such animation.</summary>
            public int Action;
            /// <summary>The frame shown and the frames the part's own action has.</summary>
            public int Frame, FrameCount;
            /// <summary>The part's own frame time, which the unit ignores (FR-5).</summary>
            public float OwnFrameMs;
            /// <summary>Back to front in the current frame, −1 when it is not drawn.</summary>
            public int Order;
            public Vector2 OffsetPx;
        }

        readonly PreviewRenderUtility _preview;
        readonly UnitView _view;
        readonly GameObject _ground;
        readonly Material _groundMaterial;
        readonly List<PartRow> _rows = new List<PartRow>();

        public UnitPreviewStage(AssetDatabaseUnitAssetSource source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            _preview = new PreviewRenderUtility();
            var cam = _preview.camera;
            cam.orthographic = false;
            cam.fieldOfView = FieldOfView;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 150f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.36f, 0.42f, 0.36f, 1f);

            _ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _ground.name = "Ground";
            _ground.hideFlags = HideFlags.HideAndDontSave;
            Object.DestroyImmediate(_ground.GetComponent<Collider>());
            _groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.HideAndDontSave, color = new Color(0.45f, 0.52f, 0.43f) };
            _ground.GetComponent<Renderer>().sharedMaterial = _groundMaterial;
            _preview.AddSingleGO(_ground);

            _view = UnitView.Create(new UnitLoader(source), AssetDatabase.LoadAssetAtPath<Material>(UnitMaterialPath), null, "Unit Preview");
            UnitSystem.Unregister(_view); // only this stage steps it, also while the game plays
            _view.gameObject.hideFlags = HideFlags.HideAndDontSave;
            _preview.AddSingleGO(_view.gameObject);
        }

        public AssetDatabaseUnitAssetSource Source { get; }
        public UnitView View => _view;

        public float Yaw { get; set; }
        public float Pitch { get; set; } = 50f;
        public float Distance { get; set; } = 12f;
        /// <summary>Height above the feet the camera looks at.</summary>
        public float LookHeight { get; set; } = 0.55f;
        public bool ShowGround
        {
            get => _ground.activeSelf;
            set => _ground.SetActive(value);
        }

        public Quaternion CameraRotation => Quaternion.Euler(Pitch, Yaw, 0f);

        /// <summary>The body action's frame time and count for what is played; zeros when nothing is.</summary>
        public SpriteAnimation.ActionData BodyAction
        {
            get
            {
                var body = _view.ActorOf(PartSlot.Body);
                return body != null && body.Data != null && body.UniqueAction >= 0 ? body.Data.Actions[body.UniqueAction] : default;
            }
        }

        public void Dispose()
        {
            if (_view != null) Object.DestroyImmediate(_view.gameObject);
            if (_ground != null) Object.DestroyImmediate(_ground);
            if (_groundMaterial != null) Object.DestroyImmediate(_groundMaterial);
            _preview.Cleanup();
        }

        public void SetLook(in UnitLook look)
        {
            _view.SetLook(look);
            Step(0f);
        }

        /// <summary>One step of the unit under the preview camera.</summary>
        public void Step(float seconds) => UnitSystem.StepOne(_view, seconds, CameraRotation);

        /// <summary>Advances exactly one body frame.</summary>
        public void StepFrame()
        {
            float ms = BodyAction.FrameMs * _view.Speed;
            Step((ms > 0f ? ms : 1f) / 1000f);
        }

        /// <summary>Shows a frame of the animation played.</summary>
        public void SetFrame(int frame)
        {
            _view.SetFrame(frame);
            Step(0f);
        }

        /// <summary>The parts of the look in slot order, with what the current frame shows of each.</summary>
        public IReadOnlyList<PartRow> Rows()
        {
            _rows.Clear();
            var report = _view.Report;
            if (report == null) return _rows;
            for (int s = (int)PartSlot.Body; s <= (int)PartSlot.Shield; s++)
            {
                var slot = (PartSlot)s;
                if (!report.TryGet(slot, out var line)) continue;
                var row = new PartRow { Slot = slot, Found = line.Found, Resolution = line.Text, Action = -1, Frame = -1, Order = -1, Hidden = _view.IsPartHidden(slot) };
                var actor = _view.ActorOf(slot);
                if (line.Found && actor != null && actor.Data != null)
                {
                    row.Action = actor.UniqueAction;
                    row.Frame = actor.CurrentFrame;
                    if (actor.UniqueAction >= 0)
                    {
                        row.FrameCount = actor.Data.Actions[actor.UniqueAction].FrameCount;
                        row.OwnFrameMs = actor.Data.Actions[actor.UniqueAction].FrameMs;
                    }
                    foreach (var p in _view.Placements)
                    {
                        if (p.Slot != slot) continue;
                        row.Order = p.Order;
                        row.OffsetPx = p.OffsetPx;
                    }
                }
                _rows.Add(row);
            }
            return _rows;
        }

        // ---- rendering ----

        void PlaceCamera()
        {
            var cam = _preview.camera;
            cam.transform.rotation = CameraRotation;
            cam.transform.position = new Vector3(0f, LookHeight, 0f) - cam.transform.forward * Distance;
        }

        /// <summary>The preview in a window rectangle.</summary>
        public Texture Render(Rect rect)
        {
            Step(0f);
            PlaceCamera();
            _preview.BeginPreview(rect, GUIStyle.none);
            _preview.camera.Render();
            return _preview.EndPreview();
        }

        /// <summary>The world facings of the 8-direction view, as shown at turn 0: front, front-left, left, back-left; back, back-right, right, front-right.</summary>
        public static UninarokDirection[] GridFacings => new[]
        {
            UninarokDirection.South, UninarokDirection.SouthWest, UninarokDirection.West, UninarokDirection.NorthWest,
            UninarokDirection.North, UninarokDirection.NorthEast, UninarokDirection.East, UninarokDirection.SouthEast,
        };

        /// <summary>The unit in the current view; the caller destroys the texture.</summary>
        public Texture2D Capture(int width, int height)
        {
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            Draw(target, new Rect(0f, 0f, 1f, 1f));
            var image = Read(target, width, height);
            RenderTexture.ReleaseTemporary(target);
            return image;
        }

        /// <summary>The same frame in the 8 world facings, 4 × 2; the caller destroys the texture.</summary>
        public Texture2D CaptureGrid(int cellWidth, int cellHeight)
        {
            var facing = _view.Facing;
            var image = new Texture2D(cellWidth * 4, cellHeight * 2, TextureFormat.RGB24, false);
            var facings = GridFacings;
            for (int i = 0; i < 8; i++)
            {
                _view.Facing = facings[i];
                var cell = Capture(cellWidth, cellHeight);
                image.SetPixels(i % 4 * cellWidth, (1 - i / 4) * cellHeight, cellWidth, cellHeight, cell.GetPixels());
                Object.DestroyImmediate(cell);
            }
            image.Apply();
            _view.Facing = facing;
            Step(0f);
            return image;
        }

        /// <summary>Writes the view, or the 8-direction grid, as a PNG.</summary>
        public void Save(string pngPath, bool grid, int width = 640, int height = 640)
        {
            var image = grid ? CaptureGrid(width / 2, height / 2) : Capture(width, height);
            string folder = Path.GetDirectoryName(pngPath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.WriteAllBytes(pngPath, image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }

        void Draw(RenderTexture target, Rect viewport)
        {
            Step(0f);
            PlaceCamera();
            var cam = _preview.camera;
            var previous = cam.targetTexture;
            cam.targetTexture = target;
            cam.rect = viewport;
            cam.aspect = (float)target.width / target.height;
            cam.Render();
            cam.ResetAspect();
            cam.rect = new Rect(0f, 0f, 1f, 1f);
            cam.targetTexture = previous;
        }

        static Texture2D Read(RenderTexture target, int width, int height)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var t = new Texture2D(width, height, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            t.Apply();
            RenderTexture.active = previous;
            return t;
        }
    }
}
