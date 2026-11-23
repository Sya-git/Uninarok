using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Uninarok.World.View;

namespace Uninarok.World.Dev
{
    /// <summary>
    /// What the client knows of the fight (SPEC-0011 FR-11; PLAN-0011 4.12): F10 shows the own HP, SP, levels, EXP and attack
    /// range, the combat state with its target, the last 20 blows as they arrived, and the last too-far replies, pickup answers and
    /// EXP gains. The text is made anew four times a second, from rings that hold what the session raised. Dev only.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class CombatDebugView : MonoBehaviour
    {
        const float RefreshSeconds = 0.25f;
        internal const int BlowCount = 20;
        internal const int NoteCount = 8;

        enum NoteKind : byte { TooFar, Pickup, Exp }

        struct Note
        {
            public NoteKind Kind;
            public uint At;
            public uint Id;
            public long Value;
        }

        readonly StringBuilder _builder = new StringBuilder(4096);
        readonly Blow[] _blows = new Blow[BlowCount];
        readonly Note[] _notes = new Note[NoteCount];
        int _blowTotal, _noteTotal;

        GameObject _canvas;
        Text _label;
        WorldBootstrap _world;
        WorldSession _fixed, _bound;
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

        /// <summary>Blows received since the session was bound.</summary>
        internal int BlowTotal => _blowTotal;

        internal void Use(WorldBootstrap world) => _world = world;

        /// <summary>A session of its own, no bootstrap (tests).</summary>
        internal void Use(WorldSession session) => _fixed = session;

        void Update()
        {
            Bind(_fixed != null ? _fixed : _world != null ? _world.Session : null);
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f10Key.wasPressedThisFrame) Shown = !Shown;
            if (!Shown || Time.unscaledTime < _refreshAt) return;
            _refreshAt = Time.unscaledTime + RefreshSeconds;
            Refresh();
        }

        // The rings fill whether the HUD is shown or not, so a fight is there when it is opened.
        internal void Bind(WorldSession session)
        {
            if (session == _bound) return;
            if (_bound != null)
            {
                _bound.BlowReceived -= OnBlow;
                _bound.ExpGained -= OnExp;
                _bound.PickupAnswered -= OnPickup;
                _bound.TooFarAnswered -= OnTooFar;
            }
            _bound = session;
            _blowTotal = _noteTotal = 0;
            if (session == null) return;
            session.BlowReceived += OnBlow;
            session.ExpGained += OnExp;
            session.PickupAnswered += OnPickup;
            session.TooFarAnswered += OnTooFar;
        }

        void OnBlow(in Blow blow) => _blows[_blowTotal++ % BlowCount] = blow;

        void OnExp(int varId, long amount) => AddNote(NoteKind.Exp, (uint)varId, amount);

        void OnPickup(uint itemId, byte result) => AddNote(NoteKind.Pickup, itemId, result);

        void OnTooFar(uint targetId) => AddNote(NoteKind.TooFar, targetId, 0);

        void AddNote(NoteKind kind, uint id, long value)
        {
            _notes[_noteTotal++ % NoteCount] = new Note { Kind = kind, At = _bound != null ? _bound.Clock.Now : 0, Id = id, Value = value };
        }

        internal void Refresh()
        {
            string text = Text();
            EnsureCanvas();
            _label.text = text;
        }

        /// <summary>The whole readout.</summary>
        internal string Text()
        {
            _builder.Clear();
            if (_bound == null) _builder.Append("no session");
            else Write(_builder, _bound);
            return _builder.ToString();
        }

        void EnsureCanvas()
        {
            if (_canvas != null) return;
            _canvas = new GameObject("Combat Debug View");
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
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-10f, -10f);
            var text = new GameObject("Text");
            text.transform.SetParent(panel.transform, false);
            _label = text.AddComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 14;
            _label.color = Color.white;
            _label.raycastTarget = false;
            _canvas.SetActive(_shown);
        }

        void Write(StringBuilder b, WorldSession session)
        {
            var s = session.Status;
            b.Append("HP ").Append(s.Hp).Append('/').Append(s.MaxHp).Append("  SP ").Append(s.Sp).Append('/').Append(s.MaxSp)
             .Append("  base lv ").Append(s.BaseLevel).Append("  job lv ").Append(s.JobLevel).Append("  range ").Append(s.AttackRange).Append('\n');
            b.Append("EXP base ").Append(s.BaseExp).Append('/').Append(s.NextBaseExp).Append("  job ").Append(s.JobExp).Append('/').Append(s.NextJobExp).Append('\n');

            var combat = session.Combat;
            b.Append("state ").Append(combat.State);
            if (combat.Continuous) b.Append(" continuous");
            uint target = combat.TargetId;
            if (target != 0)
            {
                b.Append("  target ").Append(target);
                var model = session.Model;
                if (model != null && model.TryGetSlot(target, out int slot))
                {
                    ref readonly var e = ref model[slot];
                    b.Append(" hp ");
                    if (e.Hp < 0) b.Append('?');
                    else b.Append(e.Hp).Append('/').Append(e.MaxHp);
                }
            }
            b.Append('\n');

            b.Append("blows ").Append(_blowTotal).Append(" (last ").Append(Mathf.Min(_blowTotal, BlowCount)).Append(")\n");
            int shown = Mathf.Min(_blowTotal, BlowCount);
            for (int i = 0; i < shown; i++)
            {
                ref readonly var w = ref _blows[(_blowTotal - 1 - i) % BlowCount];
                b.Append(w.At).Append("  ").Append(w.AttackerId).Append(" > ").Append(w.TargetId).Append("  dmg ").Append(w.Damage);
                if (w.LeftDamage != 0) b.Append(" + ").Append(w.LeftDamage);
                b.Append("  ").Append(w.Kind);
                if (w.Endured) b.Append(" endured");
                b.Append(" (type ").Append(w.WireType).Append(")  hits ").Append(w.Hits).Append("  attackMT ").Append(w.AttackMs).Append("  attackedMT ").Append(w.HurtMs).Append('\n');
            }

            int notes = Mathf.Min(_noteTotal, NoteCount);
            for (int i = 0; i < notes; i++)
            {
                ref readonly var n = ref _notes[(_noteTotal - 1 - i) % NoteCount];
                b.Append(n.At).Append("  ");
                switch (n.Kind)
                {
                    case NoteKind.TooFar: b.Append("too far from ").Append(n.Id); break;
                    case NoteKind.Pickup: b.Append("pickup of ").Append(n.Id).Append(" answered ").Append(n.Value); break;
                    default: b.Append("exp ").Append(n.Id == 1u ? "base" : n.Id == 2u ? "job" : "var " + n.Id).Append(" +").Append(n.Value); break;
                }
                b.Append('\n');
            }
        }

        void OnDestroy() => Bind(null);
    }
}
