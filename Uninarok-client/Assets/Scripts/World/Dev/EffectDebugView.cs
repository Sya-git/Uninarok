using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Uninarok.Effects;

namespace Uninarok.World.Dev
{
    /// <summary>
    /// The effects playing now (SPEC-0012 FR-8; PLAN-0012 4.8): F11 lists every active effect with its key, place (point, cell or
    /// unit), part count and age. The text is made anew four times a second from <see cref="EffectSystem.FillDebug"/>. Dev only.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class EffectDebugView : MonoBehaviour
    {
        const float RefreshSeconds = 0.25f;
        internal const int MaxLines = 40;

        readonly StringBuilder _builder = new StringBuilder(4096);
        readonly List<EffectDebugInfo> _infos = new List<EffectDebugInfo>(64);
        GameObject _canvas;
        Text _label;
        float _refreshAt;
        bool _shown;

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

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f11Key.wasPressedThisFrame) Shown = !Shown;
            if (!Shown || Time.unscaledTime < _refreshAt) return;
            _refreshAt = Time.unscaledTime + RefreshSeconds;
            Refresh();
        }

        internal void Refresh()
        {
            EffectSystem.FillDebug(_infos);
            _builder.Clear();
            Write(_builder, _infos);
            EnsureCanvas();
            _label.text = _builder.ToString();
        }

        /// <summary>The whole readout of a list of active effects.</summary>
        internal static void Write(StringBuilder b, List<EffectDebugInfo> infos)
        {
            if (infos.Count == 0)
            {
                b.Append("no effects");
                return;
            }
            b.Append("effects ").Append(infos.Count).Append('\n');
            for (int i = 0; i < infos.Count; i++)
            {
                if (i >= MaxLines)
                {
                    b.Append("...\n");
                    break;
                }
                var e = infos[i];
                b.Append(e.Key).Append("  ");
                switch (e.Place)
                {
                    case EffectPlace.Cell: b.Append("cell ").Append(e.CellX).Append(' ').Append(e.CellY); break;
                    case EffectPlace.Unit: b.Append("unit ").Append(e.UnitName); break;
                    default:
                        b.Append("point ").Append(e.Position.x.ToString("0.0", CultureInfo.InvariantCulture)).Append(' ')
                         .Append(e.Position.y.ToString("0.0", CultureInfo.InvariantCulture)).Append(' ')
                         .Append(e.Position.z.ToString("0.0", CultureInfo.InvariantCulture));
                        break;
                }
                b.Append("  parts ").Append(e.Parts).Append("  age ").Append(e.Age.ToString("0.00", CultureInfo.InvariantCulture)).Append(" s");
                if (e.Loop) b.Append(" loop");
                if (e.Waiting) b.Append(" loading");
                b.Append('\n');
            }
        }

        void EnsureCanvas()
        {
            if (_canvas != null) return;
            _canvas = new GameObject("Effect Debug View");
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
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);   // bottom left: F9 is top left, F10 top right
            rect.anchoredPosition = new Vector2(10f, 10f);
            var text = new GameObject("Text");
            text.transform.SetParent(panel.transform, false);
            _label = text.AddComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 14;
            _label.color = Color.white;
            _label.raycastTarget = false;
            _canvas.SetActive(_shown);
        }
    }
}
