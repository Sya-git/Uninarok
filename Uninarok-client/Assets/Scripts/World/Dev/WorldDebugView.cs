using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Uninarok.Map;
using Uninarok.Networking.Client;
using Uninarok.World.View;

namespace Uninarok.World.Dev
{
    /// <summary>
    /// What the client knows of the world (SPEC-0010 FR-14; PLAN-0010 4.12): F9 shows every entity's id, kind, cell and state, the
    /// server time estimate and its round trip, and draws the own path and the server's cell of the own character on the map.
    /// Drawing allocates nothing; the text is made anew a few times a second. Dev only.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class WorldDebugView : MonoBehaviour
    {
        const float RefreshSeconds = 0.25f;
        const int MaxLines = 60;
        const float Lift = 0.05f;

        static readonly Color s_path = new Color(1f, 0.9f, 0.1f, 1f);
        static readonly Color s_cell = new Color(0.2f, 0.8f, 1f, 1f);

        readonly StringBuilder _builder = new StringBuilder(8192);
        string _text = string.Empty;
        // A canvas is drawn again only when its text changes; IMGUI allocates every frame.
        GameObject _canvas;
        Text _label;
        Material _lines;
        WorldBootstrap _world;
        float _refreshAt;

        public bool Shown
        {
            get => _shown;
            set
            {
                _shown = value;
                _refreshAt = 0f;
                if (_canvas != null) _canvas.SetActive(value);
            }
        }
        bool _shown;

        internal void Use(WorldBootstrap world) => _world = world;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f9Key.wasPressedThisFrame) Shown = !Shown;
            if (!Shown || Time.unscaledTime < _refreshAt) return;
            _refreshAt = Time.unscaledTime + RefreshSeconds;
            Refresh();
        }

        internal void Refresh()
        {
            _builder.Clear();
            var session = _world != null ? _world.Session : null;
            if (session == null) _builder.Append("no session");
            else Write(_builder, session, _world.MapName);
            _text = _builder.ToString();
            EnsureCanvas();
            _label.text = _text;
        }

        void EnsureCanvas()
        {
            if (_canvas != null) return;
            _canvas = new GameObject("World Debug View");
            _canvas.transform.SetParent(transform, false);
            var canvas = _canvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_canvas.transform, false);
            var back = panel.AddComponent<Image>();
            back.color = new Color(0f, 0f, 0f, 0.6f);
            back.raycastTarget = false;
            var group = panel.AddComponent<VerticalLayoutGroup>();
            group.padding = new RectOffset(8, 8, 6, 6);
            var fit = panel.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(10f, -10f);
            var text = new GameObject("Text");
            text.transform.SetParent(panel.transform, false);
            _label = text.AddComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 14;
            _label.color = Color.white;
            _label.raycastTarget = false;
        }

        /// <summary>The whole readout of a session.</summary>
        internal static void Write(StringBuilder b, WorldSession session, string mapName)
        {
            var clock = session.Clock;
            b.Append("server time ").Append(clock.Now).Append("  round trip ").Append(clock.LastRoundTripMs).Append(" ms\n");
            var model = session.Model;
            if (model == null)
            {
                b.Append("no world");
                return;
            }
            b.Append("map ").Append(mapName).Append("  entities ").Append(model.Count).Append('\n');
            if (model.TryGetSlot(model.OwnId, out int own))
            {
                ref readonly var e = ref model[own];
                b.Append("own cell ").Append(e.CellX).Append(' ').Append(e.CellY).Append("  shown ").Append(e.PosX.ToString("0.00")).Append(' ').Append(e.PosY.ToString("0.00"))
                 .Append("  facing ").Append(e.Facing).Append("  head ").Append(e.Head).Append("  speed ").Append(e.Speed).Append('\n');
            }
            int lines = 0;
            for (int i = 0; i < model.SlotLimit; i++)
            {
                if (!model.IsUsed(i)) continue;
                if (++lines > MaxLines)
                {
                    b.Append("...\n");
                    break;
                }
                ref readonly var e = ref model[i];
                b.Append(e.Id).Append("  ").Append(e.Kind).Append(' ').Append(e.IsItem ? e.ItemId : e.Look.Job).Append("  ").Append(e.CellX).Append(' ').Append(e.CellY);
                if (!e.IsItem) b.Append("  ").Append(e.State);
                if (e.Walking) b.Append(" walking");
                b.Append('\n');
            }
        }

        // ---- on the map ----

        void OnRenderObject()
        {
            if (!Shown || _world == null || _world.Map == null) return;
            var model = _world.Session != null ? _world.Session.Model : null;
            if (model == null || !model.TryGetSlot(model.OwnId, out int slot)) return;
            if (_lines == null)
            {
                _lines = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                _lines.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                _lines.SetInt("_ZWrite", 0);
            }
            _lines.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);
            DrawPath(_world.Map, model, slot);
            DrawCell(_world.Map, model[slot].CellX, model[slot].CellY);
            GL.End();
            GL.PopMatrix();
        }

        static void DrawPath(MapData map, WorldModel model, int slot)
        {
            ref readonly var e = ref model[slot];
            if (!e.Walking) return;
            ref readonly var path = ref model.PathOf(slot);
            GL.Color(s_path);
            var from = Ground(map, e.PosX, e.PosY);
            int x = e.CellX, y = e.CellY;
            for (int i = e.StepIndex; i < path.Length; i++)
            {
                x += HerculesPathfinder.Dx(path.Step(i));
                y += HerculesPathfinder.Dy(path.Step(i));
                var to = Ground(map, x, y);
                GL.Vertex(from);
                GL.Vertex(to);
                from = to;
            }
        }

        static void DrawCell(MapData map, int x, int y)
        {
            GL.Color(s_cell);
            var a = Ground(map, x - 0.49f, y - 0.49f);
            var b = Ground(map, x + 0.49f, y - 0.49f);
            var c = Ground(map, x + 0.49f, y + 0.49f);
            var d = Ground(map, x - 0.49f, y + 0.49f);
            GL.Vertex(a); GL.Vertex(b);
            GL.Vertex(b); GL.Vertex(c);
            GL.Vertex(c); GL.Vertex(d);
            GL.Vertex(d); GL.Vertex(a);
        }

        static Vector3 Ground(MapData map, float x, float y)
        {
            var p = WorldPresenter.Position(map, x, y);
            map.TryGetHeight(p, out p.y);
            p.y += Lift;
            return p;
        }

        void OnDestroy()
        {
            if (_lines != null) Destroy(_lines);
        }
    }

    /// <summary>
    /// Puts the dev tools on the world at start, so no scene holds a component that a release build lacks (BACKLOG, SPEC-0001).
    /// </summary>
    static class WorldDevTools
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var world = Object.FindAnyObjectByType<WorldBootstrap>();
            if (world != null) Attach(world);
        }

        internal static void Attach(WorldBootstrap world)
        {
            var go = world.gameObject;
            if (!go.TryGetComponent<DevCommandLine>(out var line)) line = go.AddComponent<DevCommandLine>();
            line.Use(world, go.GetComponent<OwnInput>());
            // Before the connection: the items are listed at entry.
            var client = NetworkClient.Instance != null ? NetworkClient.Instance : Object.FindAnyObjectByType<NetworkClient>();
            if (client != null) line.UseInventory(new DevInventory(client));
            if (!go.TryGetComponent<WorldDebugView>(out var view)) view = go.AddComponent<WorldDebugView>();
            view.Use(world);
        }
    }
}
