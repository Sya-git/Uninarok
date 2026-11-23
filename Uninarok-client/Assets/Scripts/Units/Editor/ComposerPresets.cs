using System;
using System.Collections.Generic;
using Uninarok.Networking.Data;

namespace Uninarok.Units.Editor
{
    /// <summary>
    /// The look part of the composer window as plain values (PLAN-0009 4.12, FR-18): what a preset holds and nothing else.
    /// Camera, frame, play state, head facing, speed and view toggles are not here, so a preset can never touch them.
    /// </summary>
    public struct ComposerLookFields
    {
        public UnitKind Kind;
        public int UnitJob;
        public int Class;
        public AccountSex Sex;
        public int BodyStyle, HairStyle, HairColour, ClothColour;
        public PresetHand Top, Mid, Low, Garment, Right, Left;
        public int Animation;
        public PlayMode Mode;
        public UninarokDirection Facing;
    }

    /// <summary>Preset to window fields and back, without any window or dialog, so it can be tested (T11.4).</summary>
    public static class ComposerPresets
    {
        /// <summary>The preset for the fields as they are now; the animation is stored by name (D39).</summary>
        public static UnitComposerPreset Build(string name, in ComposerLookFields f)
        {
            return new UnitComposerPreset
            {
                Name = name,
                Kind = f.Kind, UnitJob = f.UnitJob, Class = f.Class, Sex = f.Sex,
                BodyStyle = f.BodyStyle, HairStyle = f.HairStyle, HairColour = f.HairColour, ClothColour = f.ClothColour,
                Top = f.Top, Mid = f.Mid, Low = f.Low, Garment = f.Garment, Right = f.Right, Left = f.Left,
                Animation = AnimationName(f.Kind, f.Animation),
                Mode = f.Mode, Facing = f.Facing,
            };
        }

        /// <summary>
        /// The fields a preset sets. A bad animation name, play mode or facing falls back to Idle, Loop and South and is added to
        /// <paramref name="problems"/> (D39); a class that is not a <see cref="PlayerJob"/> value is kept and listed.
        /// </summary>
        public static ComposerLookFields ToFields(UnitComposerPreset p, List<string> problems)
        {
            var f = new ComposerLookFields
            {
                Kind = p.Kind, UnitJob = p.UnitJob, Class = p.Class, Sex = p.Sex,
                BodyStyle = p.BodyStyle, HairStyle = p.HairStyle, HairColour = p.HairColour, ClothColour = p.ClothColour,
                Top = p.Top, Mid = p.Mid, Low = p.Low, Garment = p.Garment, Right = p.Right, Left = p.Left,
                Mode = p.Mode, Facing = p.Facing,
            };
            if (!Enum.IsDefined(typeof(UnitKind), p.Kind))
            {
                problems?.Add($"unit kind {(int)p.Kind} is not known (Player used)");
                f.Kind = UnitKind.Player;
            }
            if (f.Kind == UnitKind.Player && !(p.Class >= short.MinValue && p.Class <= short.MaxValue && Enum.IsDefined(typeof(PlayerJob), (short)p.Class))) problems?.Add($"class {p.Class} is not a player class");
            if (!Enum.IsDefined(typeof(PlayMode), p.Mode))
            {
                problems?.Add($"play mode {(int)p.Mode} is out of range (Loop used)");
                f.Mode = PlayMode.Loop;
            }
            if (!Enum.IsDefined(typeof(UninarokDirection), p.Facing) || (int)p.Facing > 7)
            {
                problems?.Add($"facing {(int)p.Facing} is out of range (South used)");
                f.Facing = UninarokDirection.South;
            }
            f.Animation = AnimationOf(f.Kind, p.Animation, problems);
            return f;
        }

        static string AnimationName(UnitKind kind, int value) =>
            kind == UnitKind.Unit ? ((MonsterAnimation)value).ToString() : ((PcAnimation)value).ToString();

        static int AnimationOf(UnitKind kind, string name, List<string> problems)
        {
            if (!string.IsNullOrEmpty(name))
            {
                if (kind == UnitKind.Unit)
                {
                    if (Enum.TryParse(name, false, out MonsterAnimation m) && Enum.IsDefined(typeof(MonsterAnimation), m)) return (int)m;
                }
                else if (Enum.TryParse(name, false, out PcAnimation a) && Enum.IsDefined(typeof(PcAnimation), a)) return (int)a;
            }
            problems?.Add($"animation '{name}' does not exist (Idle used)");
            return 0;
        }
    }
}
