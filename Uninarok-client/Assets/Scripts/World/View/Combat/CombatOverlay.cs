using System;
using UnityEngine;
using UnityEngine.Rendering;
using Uninarok.Map;
using Uninarok.Sprites;

namespace Uninarok.World.View
{
    /// <summary>
    /// Draws the combat numbers and messages (SPEC-0011 FR-6, FR-12; PLAN-0011 4.8, D6) from the session's timeline, after the camera
    /// and the units, with the final camera: one dynamic mesh for the digits and one for the message pictures, drawn with
    /// <c>Uninarok/Overlay</c>. Nothing per number is a GameObject; nothing allocates per frame.
    /// </summary>
    [DefaultExecutionOrder(950)]
    [DisallowMultipleComponent]
    public sealed class CombatOverlay : MonoBehaviour
    {
        // msg sprites (PLAN-0011 §1).
        const int Miss = 0, CriticalBack = 3, LuckyBack = 4, Lucky = 5;

        WorldSession _session;
        // Without a session (tests): these.
        WorldModel _model;
        OwnStatus _status;
        CombatTimeline _timeline;
        WorldPresenter _presenter;
        CombatSettings _settings;
        QuadMesh _digits, _messages, _bars;
        Material _digitMaterial, _messageMaterial, _barMaterial;
        readonly Rect[] _digitUv = new Rect[10];
        readonly Vector2[] _digitPx = new Vector2[10];
        readonly Rect[] _messageUv = new Rect[6];
        readonly Vector2[] _messagePx = new Vector2[6];
        float _digitPpu = 1f, _messagePpu = 1f;
        int[] _order = Array.Empty<int>();
        bool _ready;
        ProjectileView[] _arrows = Array.Empty<ProjectileView>();

        /// <summary>The material of the arrow's sprite (<c>sprite_unit.mat</c>, as the units).</summary>
        public Material SpriteMaterial { get; set; }

        /// <summary>Arrows shown in the last frame.</summary>
        internal int DrawnArrows { get; private set; }

        /// <summary>Numbers drawn in the last frame (tests, dev HUD).</summary>
        internal int DrawnNumbers { get; private set; }

        /// <summary>Bars drawn in the last frame.</summary>
        internal int DrawnBars { get; private set; }

        internal QuadMesh BarMesh => _bars;

        CombatTimeline Timeline => _session != null ? _session.Timeline : _timeline;
        WorldModel Model => _session != null ? _session.Model : _model;
        OwnStatus Status => _session != null ? _session.Status : _status;

        /// <summary>Without a session: a model, a status and a timeline of their own (tests).</summary>
        internal void Use(WorldModel model, OwnStatus status, CombatTimeline timeline, WorldPresenter presenter, CombatSettings settings)
        {
            Use(null, presenter, settings);
            _model = model;
            _status = status;
            _timeline = timeline;
        }

        public void Use(WorldSession session, WorldPresenter presenter, CombatSettings settings)
        {
            Clear();
            _session = session;
            _presenter = presenter;
            _settings = settings;
            _ready = false;
            if (settings == null || settings.OverlayShader == null || settings.Digits == null || settings.Messages == null)
            {
                Debug.LogWarning("[Combat] the overlay has no shader or sprites in its settings: no numbers are drawn");
                return;
            }
            _digits ??= new QuadMesh("Combat Digits", settings.NumberCapacity * settings.MaxDigits);
            _messages ??= new QuadMesh("Combat Messages", settings.NumberCapacity * 2);
            _bars ??= new QuadMesh("Combat Bars", settings.BarCapacity * 5);
            if (_barMaterial == null)
                _barMaterial = new Material(settings.OverlayShader) { name = "Combat Bars", mainTexture = Texture2D.whiteTexture, hideFlags = HideFlags.DontSave };
            _digitMaterial = MakeMaterial(settings.OverlayShader, settings.Digits);
            _messageMaterial = MakeMaterial(settings.OverlayShader, settings.Messages);
            for (int d = 0; d < 10; d++) Read(settings.Digits, DigitSlot(settings.Digits, d), out _digitUv[d], out _digitPx[d]);
            for (int m = 0; m < 6; m++) Read(settings.Messages, m, out _messageUv[m], out _messagePx[m]);
            _digitPpu = settings.Digits.PixelsPerUnit > 0f ? settings.Digits.PixelsPerUnit : 1f;
            _messagePpu = settings.Messages.PixelsPerUnit > 0f ? settings.Messages.PixelsPerUnit : 1f;
            if (_order.Length < settings.NumberCapacity) _order = new int[settings.NumberCapacity];
            // FR-14: arrows made in advance.
            if (_arrows.Length == 0 && settings.Arrow != null)
            {
                _arrows = new ProjectileView[Math.Max(1, settings.ProjectilePool)];
                for (int i = 0; i < _arrows.Length; i++) _arrows[i] = ProjectileView.Create(transform, settings.Arrow, SpriteMaterial);
            }
            _ready = true;
        }

        /// <summary>Draws nothing until the next frame with numbers (map change).</summary>
        public void Clear()
        {
            _digits?.Begin();
            _digits?.End();
            _messages?.Begin();
            _messages?.End();
            _bars?.Begin();
            _bars?.End();
            DrawnNumbers = DrawnBars = 0;
            for (int i = 0; i < _arrows.Length; i++) if (_arrows[i] != null) _arrows[i].Hide();
            DrawnArrows = 0;
        }

        static Material MakeMaterial(Shader shader, SpriteAnimation sprites)
        {
            var texture = sprites.Sprites.Length > 0 && sprites.Sprites[0] != null ? sprites.Sprites[0].texture : null;
            return new Material(shader) { name = "Combat " + sprites.name, mainTexture = texture, hideFlags = HideFlags.DontSave };
        }

        // Frame d of the first action is the digit d.
        static int DigitSlot(SpriteAnimation a, int digit)
        {
            if (a.Actions.Length == 0 || digit >= a.Actions[0].FrameCount) return digit;
            var frame = a.Frames[a.Actions[0].FirstFrame + digit];
            return frame.LayerCount > 0 ? a.Layers[frame.FirstLayer].Slot : digit;
        }

        static void Read(SpriteAnimation a, int slot, out Rect uv, out Vector2 px)
        {
            uv = default;
            px = default;
            if (slot < 0 || slot >= a.Sprites.Length || a.Sprites[slot] == null) return;
            var sprite = a.Sprites[slot];
            var r = sprite.textureRect;
            var t = sprite.texture;
            uv = new Rect(r.x / t.width, r.y / t.height, r.width / t.width, r.height / t.height);
            px = r.size;
        }

        void LateUpdate()
        {
            if (!_ready) return;
            Draw(UnitSystemCamera());
            var p = new RenderParams(_barMaterial) { layer = gameObject.layer, shadowCastingMode = ShadowCastingMode.Off };
            if (_bars.Count > 0) Graphics.RenderMesh(p, _bars.Mesh, 0, Matrix4x4.identity);
            p.material = _digitMaterial;
            if (_digits.Count > 0) Graphics.RenderMesh(p, _digits.Mesh, 0, Matrix4x4.identity);
            p.material = _messageMaterial;
            if (_messages.Count > 0) Graphics.RenderMesh(p, _messages.Mesh, 0, Matrix4x4.identity);
        }

        /// <summary>Writes the meshes for this camera (each frame; tests call it directly).</summary>
        internal void Draw(Camera camera)
        {
            if (!_ready) return;
            var timeline = Timeline;
            var map = _presenter != null ? _presenter.Map : null;
            _digits.Begin();
            _messages.Begin();
            _bars.Begin();
            DrawnNumbers = DrawnBars = 0;
            int arrows = 0;
            if (map != null && camera != null)
            {
                DrawBars(map, camera);
                if (timeline != null)
                {
                    DrawNumbers(timeline, map, camera.transform);
                    arrows = PlaceArrows(timeline, map, camera);
                }
            }
            for (int i = arrows; i < _arrows.Length; i++) _arrows[i].Hide();
            DrawnArrows = arrows;
            _digits.End();
            _messages.End();
            _bars.End();
        }

        // ---- projectiles (FR-14) ----

        // Arrows from their launch to their arrival, straight between the shown positions, ProjectileHeight above the ground.
        int PlaceArrows(CombatTimeline timeline, MapData map, Camera camera)
        {
            var projectiles = timeline.Projectiles;
            uint now = timeline.Now;
            int used = 0;
            var rotation = camera.transform.rotation;
            for (int i = 0; i < projectiles.Length && used < _arrows.Length; i++)
            {
                ref readonly var p = ref projectiles[i];
                if (p.Kind != ProjectileKind.Arrow || ServerClock.Diff(now, p.Launches) < 0 || ServerClock.Diff(now, p.Arrives) >= 0) continue;
                int flight = Math.Max(1, ServerClock.Diff(p.Arrives, p.Launches));
                float t = ServerClock.Diff(now, p.Launches) / (float)flight;
                var from = Lifted(map, p.FromX, p.FromY);
                var to = Lifted(map, p.ToX, p.ToY);
                var at = Vector3.Lerp(from, to, t);
                var a = camera.WorldToScreenPoint(from);
                var b = camera.WorldToScreenPoint(to);
                float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                _arrows[used++].Place(at, rotation, angle);
            }
            return used;
        }

        Vector3 Lifted(MapData map, float x, float y)
        {
            var p = WorldPresenter.Position(map, x, y);
            if (map.TryGetHeight(p, out float ground)) p.y = ground;
            p.y += _settings.ProjectileHeight * map.CellSize;
            return p;
        }

        // ---- bars (FR-7) ----

        // The own character always, from the status; a monster that is shown, not dead, with 0 < HP < max HP.
        void DrawBars(MapData map, Camera camera)
        {
            var model = Model;
            if (model == null) return;
            for (int slot = 0; slot < model.SlotLimit; slot++)
            {
                ref readonly var e = ref model[slot];
                if (e.Id == 0) continue;
                bool own = e.Id == model.OwnId;
                if (!own && (e.Kind != EntityKind.Monster || e.State == EntityState.Dead || e.Hp <= 0 || e.MaxHp <= 0 || e.Hp >= e.MaxHp)) continue;
                if (own && e.State == EntityState.Dead) continue;
                var view = _presenter.ViewOf(e.Id);
                if (view == null || !view.gameObject.activeInHierarchy) continue;
                if (!BarTop(camera, view.transform.position, map.CellSize, out var top)) continue;
                if (own)
                {
                    var status = Status;
                    if (status == null) continue;
                    float hp = Share(status.Hp, status.MaxHp);
                    Bar(camera, top, _settings.HpSpBarPx, hp, hp < _settings.BarLowShare ? _settings.OwnHpLow : _settings.OwnHp, true,
                        Share(status.Sp, status.MaxSp), _settings.OwnSp);
                }
                else
                {
                    float hp = Share(e.Hp, e.MaxHp);
                    Bar(camera, top, _settings.HpBarPx, hp, hp < _settings.BarLowShare ? _settings.MonsterHpLow : _settings.MonsterHp, false, 0f, default);
                }
                DrawnBars++;
            }
        }

        static float Share(long value, long max) => max > 0 ? Mathf.Clamp01(value / (float)max) : 0f;

        // The bar's top centre on the screen, on whole pixels: BarBelowFeet cells under the feet.
        bool BarTop(Camera camera, Vector3 feet, float cellSize, out Vector2 top)
        {
            var p = camera.WorldToScreenPoint(feet - Vector3.up * (_settings.BarBelowFeet * cellSize));
            top = new Vector2(Mathf.Round(p.x), Mathf.Round(p.y));
            return p.z > camera.nearClipPlane;
        }

        // A frame of 1 px around the HP row (and the SP row under it, 1 px apart), each row its filled part then its empty part.
        void Bar(Camera camera, Vector2 top, Vector2Int size, float hp, Color hpColour, bool withSp, float sp, Color spColour)
        {
            float left = top.x - Mathf.Floor(size.x * 0.5f), y = top.y;
            ScreenRect(camera, left, y - size.y, size.x, size.y, _settings.BarFrame);
            float inner = size.x - 2f;
            float row = withSp ? Mathf.Floor((size.y - 3f) * 0.5f) : size.y - 2f;
            Row(camera, left + 1f, y - 1f - row, inner, row, hp, hpColour);
            if (withSp) Row(camera, left + 1f, y - 2f - 2f * row, inner, row, sp, spColour);
        }

        void Row(Camera camera, float x, float y, float width, float height, float share, Color colour)
        {
            float filled = Mathf.Round(width * share);
            if (filled > 0f) ScreenRect(camera, x, y, filled, height, colour);
            if (filled < width) ScreenRect(camera, x + filled, y, width - filled, height, _settings.BarEmpty);
        }

        // A rectangle in screen pixels (bottom-left x, y), just in front of the near plane.
        void ScreenRect(Camera camera, float x, float y, float width, float height, Color colour)
        {
            float z = camera.nearClipPlane + 0.01f;
            var a = camera.ScreenToWorldPoint(new Vector3(x, y, z));
            var b = camera.ScreenToWorldPoint(new Vector3(x + width, y, z));
            var c = camera.ScreenToWorldPoint(new Vector3(x + width, y + height, z));
            var d = camera.ScreenToWorldPoint(new Vector3(x, y + height, z));
            _bars.Quad(a, b, c, d, new Rect(0f, 0f, 1f, 1f), colour);
        }

        static Camera UnitSystemCamera() => Units.UnitSystem.Camera != null ? Units.UnitSystem.Camera : Camera.main;

        void DrawNumbers(CombatTimeline timeline, MapData map, Transform camera)
        {
            var numbers = timeline.Numbers;
            int count = Math.Min(numbers.Length, _order.Length);
            // Older first, so newer ones are drawn over them (lead, plan 4.8).
            for (int i = 0; i < count; i++)
            {
                int j = i;
                while (j > 0 && ServerClock.Diff(numbers[_order[j - 1]].Starts, numbers[i].Starts) > 0)
                {
                    _order[j] = _order[j - 1];
                    j--;
                }
                _order[j] = i;
            }

            uint now = timeline.Now;
            Vector3 right = camera.right, up = camera.up;
            for (int k = 0; k < count; k++)
            {
                ref readonly var n = ref numbers[_order[k]];
                int life = NumberMotion.LifeMs(n.Kind, _settings);
                float t = life > 0 ? ServerClock.Diff(now, n.Starts) / (float)life : 1f;
                var pose = NumberMotion.At(n.Kind, t, _settings);
                if (pose.Alpha <= 0f || pose.Scale <= 0f) continue;
                var ground = WorldPresenter.Position(map, n.X + pose.X, n.Y + pose.Y);
                if (map.TryGetHeight(ground, out float y)) ground.y = y;
                var at = ground + Vector3.up * (pose.Height * map.CellSize);
                var colour = NumberMotion.ColourOf(n.Kind, n.OnPlayer, _settings);
                colour.a *= pose.Alpha;
                var c32 = (Color32)colour;
                var white = (Color32)new Color(1f, 1f, 1f, pose.Alpha);
                switch (n.Kind)
                {
                    case NumberKind.Miss:
                        Picture(Miss, at, right, up, pose.Scale, c32); // tinted as a number over its target (owner's footage, T0.3)
                        break;
                    case NumberKind.Lucky:
                        Picture(LuckyBack, at, right, up, pose.Scale, white);
                        Picture(Lucky, at, right, up, pose.Scale, white);
                        break;
                    case NumberKind.Critical:
                        Picture(CriticalBack, at, right, up, pose.Scale * 0.5f, white);
                        Digits(n.Value, at, right, up, pose.Scale, c32);
                        break;
                    default:
                        Digits(n.Value, at, right, up, pose.Scale, c32);
                        break;
                }
                DrawnNumbers++;
            }
        }

        void Picture(int index, Vector3 centre, Vector3 right, Vector3 up, float scale, Color32 colour)
        {
            var px = _messagePx[index];
            if (px.x <= 0f) return;
            float w = px.x / _messagePpu * scale * 0.5f, h = px.y / _messagePpu * scale * 0.5f;
            _messages.Quad(centre - right * w - up * h, centre + right * w - up * h, centre + right * w + up * h, centre - right * w + up * h, _messageUv[index], colour);
        }

        void Digits(int value, Vector3 centre, Vector3 right, Vector3 up, float scale, Color32 colour)
        {
            if (value < 0) return;
            float unit = scale / _digitPpu, spacing = _settings.DigitSpacingPx;
            // Width first, then the digits from the left, without strings.
            int digits = 0;
            float width = 0f, height = 0f;
            for (int v = value; ; v /= 10)
            {
                var px = _digitPx[v % 10];
                width += px.x;
                if (px.y > height) height = px.y;
                digits++;
                if (v < 10) break;
            }
            width += spacing * (digits - 1);
            int divisor = 1;
            for (int i = 1; i < digits; i++) divisor *= 10;
            float x = -width * 0.5f;
            for (int i = 0; i < digits; i++, divisor /= 10)
            {
                int d = value / divisor % 10;
                var px = _digitPx[d];
                var left = centre + right * (x * unit);
                var r = right * (px.x * unit);
                var bottom = -up * (px.y * 0.5f * unit);
                var top = up * (px.y * 0.5f * unit);
                _digits.Quad(left + bottom, left + r + bottom, left + r + top, left + top, _digitUv[d], colour);
                x += px.x + spacing;
            }
        }

        void OnDestroy()
        {
            _digits?.Dispose();
            _messages?.Dispose();
            _bars?.Dispose();
            if (_barMaterial != null) Destroy(_barMaterial);
            if (_digitMaterial != null) Destroy(_digitMaterial);
            if (_messageMaterial != null) Destroy(_messageMaterial);
        }
    }
}
