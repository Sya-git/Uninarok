using System;
using System.Collections.Generic;
using UnityEngine;
using Uninarok.World.View;

namespace Uninarok.World.Dev
{
    /// <summary>
    /// A one-line text field for commands until a chat spec takes them over (SPEC-0010 FR-13, FR-19; PLAN-0010 4.12): Enter opens
    /// it, Enter runs, Escape closes. While it is open the game takes no input. Dev only; later specs add commands to the table.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class DevCommandLine : MonoBehaviour
    {
        const string FieldName = "Uninarok.DevCommandLine";
        const float MessageSeconds = 4f;

        readonly Dictionary<string, Func<string, string>> _commands = new Dictionary<string, Func<string, string>>(StringComparer.OrdinalIgnoreCase);
        DevInventory _inventory;
        DevMessages _messages;
        Func<OwnCharacter> _own;
        WorldBootstrap _world;
        OwnInput _input;
        string _text = string.Empty;
        string _message;
        float _messageUntil;
        bool _focus;

        public bool IsOpen { get; private set; }

        internal void Use(WorldBootstrap world, OwnInput input)
        {
            _world = world;
            _input = input;
            Register(() => _world != null && _world.Session != null ? _world.Session.Own : null,
                     () => _world != null && _world.Session != null ? _world.Session.Combat : null);
        }

        /// <summary>The commands of SPEC-0010, on the own character; <c>/noctrl</c> of SPEC-0011 on its combat.</summary>
        internal void Register(Func<OwnCharacter> own, Func<OwnCombat> combat = null)
        {
            _own = own;
            Add("/doridori", () => Ask(own, c => c.Doridori()));
            Add("/bangbang", () => Ask(own, c => c.Bangbang()));
            Add("/sit", () => Ask(own, c => c.Sit()));
            Add("/stand", () => Ask(own, c => c.Stand()));
            AddWithArguments("/drop", arguments => Drop(own(), arguments));
            if (combat != null) Add("/noctrl", () => NoCtrl(combat()));
        }

        // SPEC-0011 FR-2 (PLAN-0011 4.10): a plain click attacks without end, as with ctrl held; for the session.
        static string NoCtrl(OwnCombat combat)
        {
            if (combat == null) return "no character";
            combat.NoCtrl = !combat.NoCtrl;
            return combat.NoCtrl ? "/noctrl on: a click attacks without end" : "/noctrl off: ctrl or a held button attacks without end";
        }

        /// <summary>The items <c>/drop</c> knows; replaces (and ends) those of before.</summary>
        internal void UseInventory(DevInventory inventory)
        {
            _inventory?.Dispose();
            _inventory = inventory;
        }

        /// <summary>Where the server's answers come from; replaces (and ends) those of before.</summary>
        internal void UseMessages(DevMessages messages)
        {
            _messages?.Dispose();
            _messages = messages;
        }

        internal void Show(string message)
        {
            _message = message;
            _messageUntil = Time.unscaledTime + MessageSeconds;
        }

        /// <summary>The message shown last.</summary>
        internal string Message => _message;

        // FR-20: a command of the server, "@item 501 5", is a chat line.
        string Command(string line)
        {
            string sent = _own?.Invoke()?.Command(line);
            if (sent == null) return "not in game";
            if (_messages != null) _messages.Sent = sent;
            return null;
        }

        // FR-20: "/drop item [amount]" is dragging an item out of the inventory; alone it lists the items.
        string Drop(OwnCharacter own, string arguments)
        {
            if (own == null || _inventory == null) return "no character";
            var parts = arguments.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return _inventory.Describe();
            ushort amount = 1;
            if (parts.Length > 2 || !uint.TryParse(parts[0], out uint item) || parts.Length == 2 && (!ushort.TryParse(parts[1], out amount) || amount == 0)) return "/drop item [amount]";
            if (!_inventory.TryFind(item, out ushort index, out int held)) return "no item " + item;
            if (amount > held) return $"only {held} of item {item}";
            return own.DropItem(index, amount) ? null : "not now";
        }

        static string Ask(Func<OwnCharacter> own, Func<OwnCharacter, bool> command)
        {
            var c = own();
            return c == null ? "no character" : command(c) ? null : "not now";
        }

        /// <summary>Adds a command; what it returns is shown, null shows nothing.</summary>
        public void Add(string name, Func<string> run)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            AddWithArguments(name, arguments => arguments.Length == 0 ? run() : $"{name} takes nothing");
        }

        /// <summary>Adds a command that gets what follows its name.</summary>
        public void AddWithArguments(string name, Func<string, string> run) => _commands[name ?? throw new ArgumentNullException(nameof(name))] = run ?? throw new ArgumentNullException(nameof(run));

        /// <summary>Runs a line; returns the message to show, null when there is none.</summary>
        public string Execute(string line)
        {
            line = (line ?? string.Empty).Trim();
            if (line.Length == 0) return null;
            if (line[0] == '@' || line[0] == '#') return Command(line);
            int space = line.IndexOf(' ');
            string name = space < 0 ? line : line.Substring(0, space);
            return _commands.TryGetValue(name, out var run) ? run(space < 0 ? string.Empty : line.Substring(space + 1).Trim()) : $"unknown command: {name}";
        }

        internal void Open()
        {
            IsOpen = true;
            _focus = true;
            _text = string.Empty;
            if (_input != null) _input.InputEnabled = false;
        }

        internal void Close()
        {
            IsOpen = false;
            if (_input != null) _input.InputEnabled = true;
        }

        void Submit()
        {
            Show(Execute(_text));
            Close();
        }

        void OnDestroy()
        {
            UseInventory(null);
            UseMessages(null);
        }

        void OnDisable()
        {
            if (IsOpen) Close();
        }

        void OnGUI()
        {
            var e = Event.current;
            bool enter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.character == '\n');
            if (!IsOpen)
            {
                if (enter && e.keyCode != KeyCode.None)
                {
                    Open();
                    e.Use();
                }
                else if (_message != null && Time.unscaledTime < _messageUntil)
                    GUI.Label(new Rect(10f, Screen.height - 52f, Screen.width - 20f, 22f), _message);
                return;
            }

            if (enter)
            {
                if (e.keyCode != KeyCode.None) Submit();
                e.Use();
                return;
            }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                Close();
                e.Use();
                return;
            }
            GUI.SetNextControlName(FieldName);
            _text = GUI.TextField(new Rect(10f, Screen.height - 30f, Mathf.Min(500f, Screen.width - 20f), 22f), _text, 120);
            if (!_focus) return;
            GUI.FocusControl(FieldName);
            if (e.type == EventType.Repaint) _focus = false;
        }
    }
}
