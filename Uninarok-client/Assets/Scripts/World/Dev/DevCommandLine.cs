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

        readonly Dictionary<string, Func<string>> _commands = new Dictionary<string, Func<string>>(StringComparer.OrdinalIgnoreCase);
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
            Register(() => _world != null && _world.Session != null ? _world.Session.Own : null);
        }

        /// <summary>The commands of SPEC-0010, on the own character.</summary>
        internal void Register(Func<OwnCharacter> own)
        {
            Add("/doridori", () => Ask(own, c => c.Doridori()));
            Add("/bangbang", () => Ask(own, c => c.Bangbang()));
            Add("/sit", () => Ask(own, c => c.Sit()));
            Add("/stand", () => Ask(own, c => c.Stand()));
        }

        static string Ask(Func<OwnCharacter> own, Func<OwnCharacter, bool> command)
        {
            var c = own();
            return c == null ? "no character" : command(c) ? null : "not now";
        }

        /// <summary>Adds a command; what it returns is shown, null shows nothing.</summary>
        public void Add(string name, Func<string> run) => _commands[name ?? throw new ArgumentNullException(nameof(name))] = run ?? throw new ArgumentNullException(nameof(run));

        /// <summary>Runs a line; returns the message to show, null when there is none.</summary>
        public string Execute(string line)
        {
            line = (line ?? string.Empty).Trim();
            if (line.Length == 0) return null;
            return _commands.TryGetValue(line, out var run) ? run() : $"unknown command: {line}";
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
            _message = Execute(_text);
            _messageUntil = Time.unscaledTime + MessageSeconds;
            Close();
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
