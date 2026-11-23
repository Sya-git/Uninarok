using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In.Map;
using Uninarok.World.UI;
using Uninarok.World.View;

namespace Uninarok.World.PlayTests
{
    // SPEC-0011 FR-8 (PLAN-0011 4.11, §9): the prompt's window, its two buttons and Esc through simulated devices. It also checks
    // that UI Toolkit gets the Input System's mouse without an EventSystem in the scene.
    public class RestartPromptViewPlayTests : InputTestFixture
    {
        WorldBootstrapPlayTests.Rig _rig;
        GameObject _go;
        InputActionAsset _actions;
        RestartPromptView _view;
        Mouse _mouse;
        Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _rig = new WorldBootstrapPlayTests.Rig();
            _go = new GameObject("Restart Prompt");
            _go.SetActive(false);
            var document = _go.AddComponent<UIDocument>();
            document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/GamePanelSettings.asset");
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/RestartPrompt.uxml");
            _actions = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Scripts/World/View/WorldInputActions.inputactions"));
            var input = _go.AddComponent<OwnInput>();
            input.World = _rig.World;
            input.Actions = _actions;
            _view = _go.AddComponent<RestartPromptView>();
            _view.World = _rig.World;
            _view.Input = input;
            _go.SetActive(true);
        }

        [UnityTearDown]
        public IEnumerator TearDownView()
        {
            Object.Destroy(_go);
            _rig.Dispose();
            yield return null;
            Object.Destroy(_actions);
        }

        IEnumerator Frames(int n = 3)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        // A click in the middle of a button, in screen pixels (y up) from its panel position (y down).
        IEnumerator Click(Button button)
        {
            var centre = button.worldBound.center;
            var panelSize = button.panel.visualTree.layout.size;
            var screen = new Vector2(centre.x * Screen.width / panelSize.x, Screen.height - centre.y * Screen.height / panelSize.y);
            Move(_mouse.position, screen);
            yield return null;
            Press(_mouse.leftButton);
            yield return null;
            Release(_mouse.leftButton);
            yield return Frames();
        }

        [UnityTest]
        public IEnumerator RestartPromptView_TwoButtons()
        {
            LogAssert.Expect(LogType.Warning, new Regex("nowhere.*no converted scene"));
            _rig.Enter("nowhere.gat");
            yield return _rig.WaitInGame();
            yield return Frames();
            Assert.IsFalse(_view.Shown, "alive: hidden");
            Assert.AreEqual(DisplayStyle.None, _view.Window.resolvedStyle.display);

            _rig.Client.Deliver(new UNIT_VANISH(new byte[] { (byte)_rig.OwnId, (byte)(_rig.OwnId >> 8), (byte)(_rig.OwnId >> 16), (byte)(_rig.OwnId >> 24), 1 }));
            yield return Frames();
            Assert.IsTrue(_view.Shown, "dead: shown");
            Assert.AreEqual(DisplayStyle.Flex, _view.Window.resolvedStyle.display);
            var buttons = _view.Window.Query<Button>().ToList();
            Assert.AreEqual(2, buttons.Count, "two choices");
            Assert.AreSame(_view.SavePoint, buttons[0], "return to save point first (provisional until T0.4)");
            Assert.AreSame(_view.CloseButton, buttons[1]);
            Assert.AreEqual(("Return to Save Point", "Cancel"), (buttons[0].text, buttons[1].text), "provisional labels (T0.4)");

            yield return Click(_view.CloseButton);
            Assert.IsFalse(_view.Shown, "the mouse closes it");
            Assert.IsFalse(_rig.World.Session.Restart.IsOpen);

            Press(_keyboard.escapeKey);
            yield return null;
            Release(_keyboard.escapeKey);
            yield return Frames();
            Assert.IsTrue(_view.Shown, "Esc opens it again");

            Press(_keyboard.escapeKey);
            yield return null;
            Release(_keyboard.escapeKey);
            yield return Frames();
            Assert.IsFalse(_view.Shown, "and closes it");
            Press(_keyboard.escapeKey);
            yield return null;
            Release(_keyboard.escapeKey);
            yield return Frames();

            _view.Input.InputEnabled = false; // the dev command line has the keyboard
            Press(_keyboard.escapeKey);
            yield return null;
            Release(_keyboard.escapeKey);
            yield return Frames();
            Assert.IsTrue(_view.Shown, "Esc is ignored while the keyboard is taken");
            _view.Input.InputEnabled = true;

            Assert.AreEqual(0, _rig.Client.SentCount(PacketIds.RESTART));
            yield return Click(_view.SavePoint);
            Assert.AreEqual(1, _rig.Client.SentCount(PacketIds.RESTART), "the mouse returns to the save point");
            Assert.IsFalse(_view.Shown, "hidden while the answer is awaited");
        }
    }
}
