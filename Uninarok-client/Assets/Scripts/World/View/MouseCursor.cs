using System;
using UnityEngine;
using Uninarok.Sprites;

namespace Uninarok.World.View
{
    /// <summary>The mouse cursors the game shows; the value is the action in <c>cursors.act</c>.</summary>
    public enum MouseCursorKind : byte
    {
        Default = 0,
        /// <summary>The ground under the mouse is not walkable.</summary>
        NoWalk = 13,
    }

    /// <summary>
    /// The client's animated mouse cursor (SPEC-0010 FR-11; PLAN-0010 4.11, D29): the frames of <c>sprite/cursors</c> become
    /// cursor textures once, the first time a kind is shown; the mouse gets another only when the frame changes. Layers keep
    /// their offset, mirror and tint; the cursors shown have no scale or rotation. Unity stretches any hardware cursor to the
    /// system's size, so a cursor that fits is drawn on a square of that size, and a larger one is a software cursor (D34).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MouseCursor : MonoBehaviour
    {
        sealed class Frames
        {
            public Texture2D[] Textures;
            public Vector2 Hotspot;
            public float FrameMs;
            public CursorMode Mode;
        }

        /// <summary>The size Windows gives a hardware cursor.</summary>
        const int HardwareSize = 32;

        /// <summary>The converted <c>sprite/cursors.act</c>; null keeps the system's cursor.</summary>
        public SpriteAnimation Animation;

        readonly Frames[] _frames = new Frames[(int)MouseCursorKind.NoWalk + 1];
        MouseCursorKind _kind;
        double _startedAt = -1.0;
        Texture2D _shown;
        // The sheet's pixels, read once from the GPU: the importer keeps no CPU copy.
        Color32[] _sheet;
        int _sheetWidth;

        internal Action<Texture2D, Vector2, CursorMode> Apply { get; set; } = Cursor.SetCursor;

        public MouseCursorKind Kind
        {
            get => _kind;
            set
            {
                if (_kind == value) return;
                _kind = value;
                _startedAt = -1.0; // its animation starts over
            }
        }

        void Update() => Step(Time.unscaledTimeAsDouble);

        internal void Step(double now)
        {
            if (Animation == null) return;
            var frames = _frames[(int)_kind] ??= Build((int)_kind);
            if (frames.Textures.Length == 0) return;
            if (_startedAt < 0.0) _startedAt = now;
            int frame = frames.FrameMs > 0f ? (int)((now - _startedAt) * 1000.0 / frames.FrameMs) % frames.Textures.Length : 0;
            var texture = frames.Textures[frame];
            if (ReferenceEquals(texture, _shown)) return;
            _shown = texture;
            Apply(texture, frames.Hotspot, frames.Mode);
        }

        void OnDisable()
        {
            if (_shown == null) return;
            _shown = null;
            Apply(null, Vector2.zero, CursorMode.Auto);
        }

        void OnDestroy()
        {
            foreach (var frames in _frames)
                if (frames != null)
                    foreach (var texture in frames.Textures)
                        if (Application.isPlaying) Destroy(texture);
                        else DestroyImmediate(texture);
        }

        Frames Build(int action)
        {
            var built = new Frames { Textures = Array.Empty<Texture2D>() };
            if (!Animation.TryGetAction(action, 0, SpriteMissingAction.DrawNothing, out int unique)) return built;
            var data = Animation.Actions[unique];

            // One size and hotspot (the ACT's origin) for every frame, so the cursor does not jump.
            RectInt bounds = default;
            bool any = false;
            for (int f = 0; f < data.FrameCount; f++)
            {
                var frame = Animation.Frames[data.FirstFrame + f];
                for (int l = 0; l < frame.LayerCount; l++)
                {
                    if (!TryLayer(frame.FirstLayer + l, out _, out var at)) continue;
                    if (!any) bounds = at;
                    else bounds.SetMinMax(Vector2Int.Min(bounds.min, at.min), Vector2Int.Max(bounds.max, at.max));
                    any = true;
                }
            }
            if (!any) return built;

            built.Textures = new Texture2D[data.FrameCount];
            built.Hotspot = new Vector2(Mathf.Clamp(-bounds.xMin, 0, bounds.width - 1), Mathf.Clamp(-bounds.yMin, 0, bounds.height - 1));
            built.FrameMs = data.FrameMs * DelayFactor((MouseCursorKind)action);
            bool fits = bounds.width <= HardwareSize && bounds.height <= HardwareSize;
            built.Mode = fits ? CursorMode.Auto : CursorMode.ForceSoftware;
            bounds.width = bounds.height = fits ? HardwareSize : Mathf.Max(bounds.width, bounds.height);
            for (int f = 0; f < data.FrameCount; f++)
            {
                var pixels = new Color32[bounds.width * bounds.height];
                var frame = Animation.Frames[data.FirstFrame + f];
                for (int l = 0; l < frame.LayerCount; l++)
                    if (TryLayer(frame.FirstLayer + l, out var layer, out var at)) Draw(layer, at, bounds, pixels);
                var texture = new Texture2D(bounds.width, bounds.height, TextureFormat.RGBA32, false) { name = $"cursor {action} {f}", hideFlags = HideFlags.DontSave };
                texture.SetPixels32(pixels);
                texture.Apply(false);
                built.Textures[f] = texture;
            }
            return built;
        }

        // The client plays its default cursor at half the ACT's speed (roBrowserLegacy CursorManager, delayMult).
        static float DelayFactor(MouseCursorKind kind) => kind == MouseCursorKind.Default ? 2f : 1f;

        // Where a layer lies, in pixels from the ACT's origin, y down.
        bool TryLayer(int index, out SpriteAnimation.LayerData layer, out RectInt at)
        {
            layer = Animation.Layers[index];
            at = default;
            if (layer.Slot < 0 || layer.Slot >= Animation.Sprites.Length || Animation.Sprites[layer.Slot] == null) return false;
            var size = Animation.Sprites[layer.Slot].rect.size;
            int w = (int)size.x, h = (int)size.y;
            at = new RectInt(layer.OffsetX - w / 2, layer.OffsetY - h / 2, w, h);
            return true;
        }

        void Draw(in SpriteAnimation.LayerData layer, RectInt at, RectInt bounds, Color32[] into)
        {
            var sprite = Animation.Sprites[layer.Slot];
            if (_sheet == null) ReadSheet(sprite.texture);
            var rect = sprite.rect;
            for (int y = 0; y < at.height; y++)
            {
                // Textures have row 0 at the bottom.
                int row = bounds.height - 1 - (at.yMin - bounds.yMin + y);
                for (int x = 0; x < at.width; x++)
                {
                    int from = layer.Mirror ? at.width - 1 - x : x;
                    var c = _sheet[((int)rect.y + at.height - 1 - y) * _sheetWidth + (int)rect.x + from];
                    c.a = (byte)(c.a * layer.Tint.a / 255);
                    if (c.a == 0) continue;
                    c.r = (byte)(c.r * layer.Tint.r / 255);
                    c.g = (byte)(c.g * layer.Tint.g / 255);
                    c.b = (byte)(c.b * layer.Tint.b / 255);
                    into[row * bounds.width + at.xMin - bounds.xMin + x] = c;
                }
            }
        }

        void ReadSheet(Texture2D sheet)
        {
            var target = RenderTexture.GetTemporary(sheet.width, sheet.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var active = RenderTexture.active;
            Graphics.Blit(sheet, target);
            RenderTexture.active = target;
            var copy = new Texture2D(sheet.width, sheet.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, sheet.width, sheet.height), 0, 0, false);
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
            _sheet = copy.GetPixels32();
            _sheetWidth = sheet.width;
            if (Application.isPlaying) Destroy(copy);
            else DestroyImmediate(copy);
        }
    }
}
