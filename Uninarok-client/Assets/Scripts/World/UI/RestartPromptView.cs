using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Uninarok.World.View;

namespace Uninarok.World.UI
{
    /// <summary>
    /// The death prompt on the screen (SPEC-0011 FR-8; PLAN-0011 4.11, D8): a UI Toolkit window (<c>RestartPrompt.uxml</c>) with
    /// two buttons, "return to save point" and close, shown while <see cref="RestartPrompt.IsOpen"/>. Esc toggles it while dead,
    /// except while something else has the keyboard (the dev command line). In every build.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class RestartPromptView : MonoBehaviour
    {
        public WorldBootstrap World;
        /// <summary>Its <c>WorldInputActions</c> give Esc (<c>World/Menu</c>); while its input is off Esc is ignored.</summary>
        public OwnInput Input;

        UIDocument _document;
        VisualElement _window;
        Button _savePoint, _close;
        InputAction _menu;
        bool _shown;

        internal Func<RestartPrompt> Prompt { get; set; }

        internal Button SavePoint => _savePoint;
        internal Button CloseButton => _close;
        internal VisualElement Window => _window;

        /// <summary>Whether the window is shown.</summary>
        public bool Shown => _shown;

        void OnEnable()
        {
            Prompt ??= () => World != null && World.Session != null ? World.Session.Restart : null;
            _document = GetComponent<UIDocument>();
            var root = _document.rootVisualElement;
            _window = root?.Q<VisualElement>("restart-prompt");
            _savePoint = root?.Q<Button>("save-point");
            _close = root?.Q<Button>("close");
            if (_savePoint != null) _savePoint.clicked += OnSavePoint;
            if (_close != null) _close.clicked += OnClose;
            _menu = Input != null && Input.Actions != null ? Input.Actions.FindAction("World/Menu") : null;
            _shown = true;
            Show(false);
        }

        void OnDisable()
        {
            if (_savePoint != null) _savePoint.clicked -= OnSavePoint;
            if (_close != null) _close.clicked -= OnClose;
        }

        void OnSavePoint() => Prompt?.Invoke()?.ReturnToSavePoint();

        void OnClose() => Prompt?.Invoke()?.Close();

        void Update()
        {
            var prompt = Prompt?.Invoke();
            if (prompt != null && _menu != null && _menu.WasPressedThisFrame() && (Input == null || Input.InputEnabled)) prompt.ToggleByEscape();
            Show(prompt != null && prompt.IsOpen);
        }

        void Show(bool open)
        {
            if (open == _shown || _window == null) return;
            _shown = open;
            _window.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
