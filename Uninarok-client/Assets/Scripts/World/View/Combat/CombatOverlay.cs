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
        WorldPresenter _presenter;
        CombatSettings _settings;
        QuadMesh _digits, _messages;
        Material _digitMaterial, _messageMaterial;
        readonly Rect[] _digitUv = new Rect[10];
        readonly Vector2[] _digitPx = new Vector2[10];
        readonly Rect[] _messageUv = new Rect[6];
        readonly Vector2[] _messagePx = new Vector2[6];
        float _digitPpu = 1f, _messagePpu = 1f;
        int[] _order = Array.Empty<int>();
        bool _ready;

        /// <summary>Numbers drawn in the last frame (tests, dev HUD).</summary>
        internal int DrawnNumbers { get; private set; }

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
            _digitMaterial = MakeMaterial(settings.OverlayShader, settings.Digits);
            _messageMaterial = MakeMaterial(settings.OverlayShader, settings.Messages);
            for (int d = 0; d < 10; d++) Read(settings.Digits, DigitSlot(settings.Digits, d), out _digitUv[d], out _digitPx[d]);
            for (int m = 0; m < 6; m++) Read(settings.Messages, m, out _messageUv[m], out _messagePx[m]);
            _digitPpu = settings.Digits.PixelsPerUnit > 0f ? settings.Digits.PixelsPerUnit : 1f;
            _messagePpu = settings.Messages.PixelsPerUnit > 0f ? settings.Messages.PixelsPerUnit : 1f;
            if (_order.Length < settings.NumberCapacity) _order = new int[settings.NumberCapacity];
            _ready = true;
        }

        /// <summary>Draws nothing until the next frame with numbers (map change).</summary>
        public void Clear()
        {
            _digits?.Begin();
            _digits?.End();
            _messages?.Begin();
            _messages?.End();
            DrawnNumbers = 0;
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
            var timeline = _session?.Timeline;
            var map = _presenter?.Map;
            var camera = UnitSystemCamera();
            _digits.Begin();
            _messages.Begin();
            DrawnNumbers = 0;
            if (timeline != null && map != null && camera != null) DrawNumbers(timeline, map, camera.transform);
            _digits.End();
            _messages.End();
            if (_digits.Count > 0) Graphics.RenderMesh(new RenderParams(_digitMaterial) { layer = gameObject.layer, shadowCastingMode = ShadowCastingMode.Off }, _digits.Mesh, 0, Matrix4x4.identity);
            if (_messages.Count > 0) Graphics.RenderMesh(new RenderParams(_messageMaterial) { layer = gameObject.layer, shadowCastingMode = ShadowCastingMode.Off }, _messages.Mesh, 0, Matrix4x4.identity);
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
                var colour = n.OnPlayer ? _settings.NumberOnPlayer : _settings.NumberOnMonster;
                if (n.Kind == NumberKind.Critical) colour = _settings.NumberCritical;
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
            if (_digitMaterial != null) Destroy(_digitMaterial);
            if (_messageMaterial != null) Destroy(_messageMaterial);
        }
    }
}
