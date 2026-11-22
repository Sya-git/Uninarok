using System;
using UnityEngine;
using Uninarok.CameraRig;
using Uninarok.Networking.Data;

namespace Uninarok.Units.Dev
{
    /// <summary>
    /// The sandbox's panel (PLAN-0009 4.8; SPEC-0009 FR-13), IMGUI: place units by look on the clicked cell, select one, change
    /// its facing, head, animation and parts live, turn the camera by the FR-6 steps, start the stress mode and read its
    /// numbers. With the panel closed only one cached line is drawn, so it does not add to the measured garbage.
    /// </summary>
    [RequireComponent(typeof(EntitySandbox))]
    public sealed class EntitySandboxPanel : MonoBehaviour
    {
        const float Width = 330f;

        EntitySandbox _sandbox;
        EntitySandboxStress _stress;
        bool _open = true;
        bool _place = true;
        Vector2 _scroll;
        UnitLook _look = new UnitLook
        {
            Kind = UnitKind.Player, JobId = (int)PlayerJob.JOB_SWORDMAN, Sex = AccountSex.SEX_MALE, HairStyle = 2, HairColour = 3,
            HeadTop = 16, HeadBottom = 8, Garment = 1, Weapon = 2, WeaponItem = 1101, Shield = 1, ShieldItem = 2101,
        };
        PlayMode _mode = PlayMode.Loop;
        string _line = string.Empty;
        float _nextLine;

        void Awake()
        {
            _sandbox = GetComponent<EntitySandbox>();
            _stress = GetComponent<EntitySandboxStress>();
            useGUILayout = true;
        }

        void Start()
        {
            var input = _sandbox.Rig != null ? _sandbox.Rig.GetComponent<RoCameraInput>() : null;
            if (input != null) input.Blocked = p => _open && p.x < Width;
        }

        void Update()
        {
            if (Time.unscaledTime < _nextLine) return;
            _nextLine = Time.unscaledTime + 0.5f;
            _line = _stress != null && _stress.Running
                ? $"{UnitSystem.Count} units   update {_stress.MeanMs:0.00} ms (max {_stress.MaxMs:0.00}, budget {EntitySandboxStress.BudgetMs:0.0})   GC {_stress.GarbageLast} B (mean {_stress.GarbageMean})   {1f / Mathf.Max(Time.smoothDeltaTime, 1e-4f):0} fps"
                : $"{UnitSystem.Count} units   {_sandbox.Status}";
        }

        void OnGUI()
        {
            if (!_open)
            {
                if (GUI.Button(new Rect(4, 4, 24, 20), ">")) _open = true;
                GUI.Label(new Rect(34, 4, 900, 20), _line);
                return;
            }
            GUILayout.BeginArea(new Rect(0, 0, Width, Screen.height), GUI.skin.box);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("<", GUILayout.Width(24))) _open = false;
                GUILayout.Label(_line);
            }
            if (_sandbox.Ready)
            {
                _scroll = GUILayout.BeginScrollView(_scroll);
                Panel();
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
            if (_sandbox.Ready) Click();
        }

        void Click()
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0 || e.mousePosition.x < Width) return;
            var camera = _sandbox.Rig != null ? _sandbox.Rig.OutputCamera : Camera.main;
            if (camera == null) return;
            var ray = camera.ScreenPointToRay(new Vector3(e.mousePosition.x, Screen.height - e.mousePosition.y, 0f));
            if (!MapPicker.TryPick(_sandbox.Map, ray, 200f, out int x, out int z)) return;
            if (_place) _sandbox.Selected = _sandbox.Spawn(_look, x, z);
            else if (_sandbox.UnitNear(x, z) is UnitView near)
            {
                _sandbox.Selected = near;
                _look = near.Look;
            }
            e.Use();
        }

        void Panel()
        {
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Toggle(_place, "Place on click", GUI.skin.button)) _place = true;
                if (GUILayout.Toggle(!_place, "Select on click", GUI.skin.button)) _place = false;
            }
            Look();
            Selected();
            CameraButtons();
            Stress();
        }

        void Look()
        {
            GUILayout.Label("Look", GUI.skin.box);
            var before = _look;
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Toggle(_look.Kind == UnitKind.Player, "Player", GUI.skin.button)) _look.Kind = UnitKind.Player;
                if (GUILayout.Toggle(_look.Kind == UnitKind.Unit, "Unit (job id)", GUI.skin.button)) _look.Kind = UnitKind.Unit;
            }
            _look.JobId = Number("Job id", _look.JobId);
            if (_look.Kind == UnitKind.Player)
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label(((PlayerJob)_look.JobId).ToString());
                    if (GUILayout.Button(_look.Sex == AccountSex.SEX_MALE ? "male" : "female", GUILayout.Width(70)))
                        _look.Sex = _look.Sex == AccountSex.SEX_MALE ? AccountSex.SEX_FEMALE : AccountSex.SEX_MALE;
                }
                _look.BodyStyle = Number("Body style", _look.BodyStyle);
                _look.HairStyle = Number("Hair style", _look.HairStyle);
                _look.HairColour = Number("Hair colour", _look.HairColour);
                _look.ClothColour = Number("Cloth colour", _look.ClothColour);
                _look.HeadTop = Number("Top headgear", _look.HeadTop);
                _look.HeadMid = Number("Mid headgear", _look.HeadMid);
                _look.HeadBottom = Number("Low headgear", _look.HeadBottom);
                _look.Garment = Number("Garment", _look.Garment);
                _look.Weapon = Number("Weapon view", _look.Weapon);
                _look.WeaponItem = Number("Weapon item", _look.WeaponItem);
                _look.Shield = Number("Shield view", _look.Shield);
                _look.ShieldItem = Number("Shield item", _look.ShieldItem);
            }
            using (new GUILayout.HorizontalScope())
                foreach (var (name, job) in Presets)
                    if (GUILayout.Button(name)) _look = UnitLook.ForJob(job);
            // Live: every change goes to the selected unit (FR-9).
            if (_sandbox.Selected != null && before != _look) _sandbox.Selected.SetLook(_look);
        }

        static (string, int)[] Presets => new[] { ("Novice", 0), ("Poring", 1002), ("Lunatic", 1063), ("Kafra", 117), ("Warp", 45) };

        static int Number(string label, int value)
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label(label, GUILayout.Width(110));
                if (GUILayout.Button("-", GUILayout.Width(24))) value = Math.Max(0, value - 1);
                string text = GUILayout.TextField(value.ToString(), GUILayout.Width(80));
                if (int.TryParse(text, out int typed)) value = typed;
                if (GUILayout.Button("+", GUILayout.Width(24))) value++;
            }
            return value;
        }

        void Selected()
        {
            GUILayout.Label(_sandbox.Selected != null ? "Selected: " + _sandbox.Selected.name : "Nothing selected", GUI.skin.box);
            var u = _sandbox.Selected;
            if (u == null) return;
            using (new GUILayout.HorizontalScope())
                for (int d = 0; d < 8; d++)
                    if (GUILayout.Toggle((int)u.Facing == d, Short((UninarokDirection)d), GUI.skin.button)) u.Facing = (UninarokDirection)d;
            using (new GUILayout.HorizontalScope())
                foreach (HeadFacing h in new[] { HeadFacing.Left, HeadFacing.Straight, HeadFacing.Right })
                    if (GUILayout.Toggle(u.HeadFacing == h, "head " + h, GUI.skin.button)) u.HeadFacing = h;
            using (new GUILayout.HorizontalScope())
                foreach (PlayMode m in new[] { PlayMode.Loop, PlayMode.OnceHold, PlayMode.OnceEnd })
                    if (GUILayout.Toggle(_mode == m, m.ToString(), GUI.skin.button)) _mode = m;
            for (int row = 0; row < 13; row += 4)
                using (new GUILayout.HorizontalScope())
                    for (int a = row; a < Math.Min(row + 4, 13); a++)
                        if (GUILayout.Button(((PcAnimation)a).ToString())) u.Play((PcAnimation)a, _mode);
            using (new GUILayout.HorizontalScope())
                for (int a = 5; a <= 8; a++)
                    if (GUILayout.Button("unit " + a)) u.Play((MonsterAnimation)a, _mode);
            GUILayout.Label($"animation {u.AnimationNumber}  frame {u.Frame}  shown direction {u.ShownDirection}");
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("doridori")) _sandbox.Doridori(u);
                if (GUILayout.Button("bangbang")) _sandbox.Bangbang(u);
                if (GUILayout.Button("remove")) _sandbox.Remove(u);
            }
            _sandbox.CameraFollowsSelected = GUILayout.Toggle(_sandbox.CameraFollowsSelected, "camera follows");
        }

        static string Short(UninarokDirection d)
        {
            switch (d)
            {
                case UninarokDirection.North: return "N";
                case UninarokDirection.NorthWest: return "NW";
                case UninarokDirection.West: return "W";
                case UninarokDirection.SouthWest: return "SW";
                case UninarokDirection.South: return "S";
                case UninarokDirection.SouthEast: return "SE";
                case UninarokDirection.East: return "E";
                default: return "NE";
            }
        }

        void CameraButtons()
        {
            var rig = _sandbox.Rig;
            if (rig == null) return;
            GUILayout.Label($"Camera: turn {rig.Yaw:0.0}°  tilt {rig.Pitch:0.0}°  distance {rig.Distance:0.0}  sector {rig.Sector}", GUI.skin.box);
            using (new GUILayout.HorizontalScope())
            {
                foreach (float degrees in new[] { 22f, 23f, 90f, 180f })
                    if (GUILayout.Button($"to {degrees}°")) rig.Turn(Mathf.DeltaAngle(rig.WantedYaw, degrees));
                if (GUILayout.Button("reset"))
                {
                    rig.ResetTurn();
                    rig.ResetTilt();
                    rig.ResetZoom();
                }
            }
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("lowest tilt")) rig.Tilt(-90f);
                if (GUILayout.Button("highest tilt")) rig.Tilt(90f);
                if (GUILayout.Button("nearest")) rig.Zoom(-1000f);
                if (GUILayout.Button("farthest")) rig.Zoom(1000f);
            }
        }

        void Stress()
        {
            if (_stress == null) return;
            GUILayout.Label("Stress", GUI.skin.box);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"{EntitySandboxStress.Players} players + {EntitySandboxStress.Monsters} monsters")) _stress.Begin();
                if (GUILayout.Button("clear", GUILayout.Width(60))) _stress.End();
            }
        }
    }
}
