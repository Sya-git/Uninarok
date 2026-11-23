using UnityEngine;
using Uninarok.Sprites;

namespace Uninarok.World
{
    /// <summary>
    /// The combat rule and display values (SPEC-0011 FR-2 to FR-10, FR-14; PLAN-0011 4.5 to 4.11), one setting each.
    /// <c>Settings/World/CombatSettings.asset</c>. Each value is a row of <c>docs/specs/0011-combat/combat-reference.md</c>, with
    /// its source; the leads are replaced by what the original client's footage shows (T0.3, T9.3).
    /// </summary>
    [CreateAssetMenu(menuName = "Uninarok/Combat Settings", fileName = "CombatSettings")]
    public sealed class CombatSettings : ScriptableObject
    {
        [Header("Sprites and shader (4.8; PLAN-0010 D29)")]
        [Tooltip("sprite/이팩트/숫자: frames 0 to 9 are the digits.")]
        public SpriteAnimation Digits;
        [Tooltip("sprite/이팩트/msg: 0 miss, 1 guard, 2 critical, 3 critical background, 4 lucky background, 5 lucky.")]
        public SpriteAnimation Messages;
        [Tooltip("sprite/npc/skel_archer_arrow (T6.4).")]
        public SpriteAnimation Arrow;
        [Tooltip("The target mark: sprite/cursors frame 6 (action 3 of cursors.act), a white faceted diamond pointing down (T11.2).")]
        public Sprite TargetMark;
        [Tooltip("Uninarok/Overlay.")]
        public Shader OverlayShader;

        [Header("Own combat (4.5)")]
        [Tooltip("At most one walk request towards the target this often, in ms.")]
        [Min(50)] public int ApproachRepeatMs = 200;
        [Tooltip("While attacking, no own blow for this long asks again, in ms (at least; see the two values below).")]
        [Min(100)] public int AttackSilenceMs = 1500;
        [Tooltip("The silence is at least this many times the last own attack motion ...")]
        [Min(0)] public int AttackSilenceMotions = 2;
        [Tooltip("... plus this, in ms.")]
        [Min(0)] public int AttackSilencePadMs = 500;
        [Tooltip("Requests in a row without an own blow before the attack is given up.")]
        [Min(0)] public int AttackRetries = 3;
        [Tooltip("A refused pickup is asked for once more after this, in ms.")]
        [Min(0)] public int PickupRetryMs = 400;
        [Tooltip("The target mark of a single attack goes this long after its blow's hit moment, in ms (FR-16).")]
        public int TargetMarkSingleMs = 0;

        [Header("Timeline (4.6)")]
        [Tooltip("A player stands in Standby after an attack or a hurt for this long, in ms; −1: until it does something else; 0: off.")]
        [Min(-1)] public int StandbyMs = -1;
        [Tooltip("The i-th hit of a multi-hit WITH a second hand is shown i times this after the hit moment, in ms (footage + roBrowserLegacy: 100, D36).")]
        [Min(0)] public int MultiHitMs = 100;
        [Tooltip("The same for a multi-hit WITHOUT a second hand, in ms (roBrowserLegacy Entity.js: 200, no footage sample, D36).")]
        [Min(0)] public int MultiHitNoLeftMs = 200;
        [Tooltip("The second hand's number is shown this after the hit moment (the first part), in ms.")]
        [Min(0)] public int LeftHandMs = 350;
        [Tooltip("A monster's death is shown at least this after its last hit moment, in ms (H9).")]
        [Min(0)] public int HurtBeforeDeathMs = 250;
        [Tooltip("The same for a player, in ms.")]
        [Min(0)] public int PlayerHurtBeforeDeathMs = 0;

        [Header("Projectiles (4.7)")]
        [Tooltip("A projectile's flight over FlightFullCells or more, in ms.")]
        [Min(1)] public int FlightMs = 192;
        [Tooltip("The distance from which a flight takes the full FlightMs, in cells; shorter ones are shorter in proportion.")]
        [Min(0.1f)] public float FlightFullCells = 8f;
        [Tooltip("How high above the ground a projectile flies, in cells.")]
        [Min(0f)] public float ProjectileHeight = 1f;
        [Tooltip("Projectile objects made in advance.")]
        [Min(0)] public int ProjectilePool = 32;

        [Header("Numbers (4.8)")]
        [Tooltip("Life of a normal or critical number, in ms.")]
        [Min(1)] public int NumberMs = 1500;
        [Tooltip("Scale of a normal or critical number at its start and after its shrink (it then stays there).")]
        public float NumberScaleStart = 4f, NumberScaleEnd = 1f;
        [Tooltip("A number shrinks from NumberScaleStart to NumberScaleEnd over this, smooth-step, in ms (footage: about 0.4 s).")]
        [Min(0)] public int NumberShrinkMs = 400;
        [Tooltip("How far a number drifts over its life, in cells: x decelerating (sin(π t / 2)), y linear (+x, then −y).")]
        public float NumberDriftX = 3.1f, NumberDriftY = 4f;
        [Tooltip("Height of a number: base + amplitude · sin(−π/2 + π(0.5 + 1.5 t)), in cells.")]
        public float NumberHeightBase = 2f, NumberHeightAmplitude = 5f;
        [Tooltip("Life of a miss picture, in ms (shown over the unit that missed, D31).")]
        [Min(1)] public int MessageMs = 1500;
        [Tooltip("Scale of a miss or lucky picture.")]
        public float MessageScale = 0.5f;
        [Tooltip("Height of a miss picture at its start and at its end, in cells (a constant rise).")]
        public float MessageHeightStart = 0.4f, MessageHeightEnd = 3.5f;
        [Tooltip("Life of a lucky picture, in ms.")]
        [Min(1)] public int LuckyMs = 800;
        [Tooltip("Height of a lucky picture at its start and at its end, in cells.")]
        public float LuckyHeightStart = 3.5f, LuckyHeightEnd = 7f;
        [Tooltip("Life of a multi-hit's total, in ms.")]
        [Min(1)] public int TotalMs = 2500;
        [Tooltip("Height and scale of a multi-hit's total.")]
        public float TotalHeight = 7f, TotalScale = 1.5f;
        [Tooltip("A total (each update of it) grows from TotalScaleStart to TotalScale over TotalGrowMs, in ms.")]
        public float TotalScaleStart = 0.6f;
        [Min(0)] public int TotalGrowMs = 100;
        [Tooltip("Space between digits, in pixels of the digit sprites.")]
        [Min(0)] public float DigitSpacingPx = 2f;
        public Color NumberOnMonster = Color.white;
        public Color NumberOnPlayer = Color.red;
        public Color NumberCritical = new Color(0.9f, 0.9f, 0.15f);
        [Tooltip("A multi-hit's total (D28).")]
        public Color NumberTotal = new Color(0.9f, 0.9f, 0.15f);
        [Tooltip("Numbers on screen at most, and digits of one number.")]
        [Min(1)] public int NumberCapacity = 512;
        [Min(1)] public int MaxDigits = 10;

        [Header("Bars (4.8)")]
        [Tooltip("HP bar size in screen pixels.")]
        public Vector2Int HpBarPx = new Vector2Int(60, 5);
        [Tooltip("The own HP and SP bar size in screen pixels.")]
        public Vector2Int HpSpBarPx = new Vector2Int(60, 9);
        [Tooltip("How far under the feet the bar is, in cells.")]
        public float BarBelowFeet = 0.5f;
        [Tooltip("Below this share of HP a bar shows its low colour.")]
        [Range(0f, 1f)] public float BarLowShare = 0.25f;
        public Color BarFrame = new Color32(0x10, 0x18, 0x9c, 0xff);
        public Color BarEmpty = new Color32(0x42, 0x42, 0x42, 0xff);
        public Color OwnHp = new Color32(0x10, 0xef, 0x21, 0xff);
        public Color OwnHpLow = new Color32(0xff, 0x00, 0x00, 0xff);
        public Color OwnSp = new Color32(0x18, 0x63, 0xde, 0xff);
        public Color MonsterHp = new Color32(0xff, 0x00, 0xe7, 0xff);
        public Color MonsterHpLow = new Color32(0xff, 0xff, 0x00, 0xff);
        [Min(1)] public int BarCapacity = 512;

        [Header("Target mark (4.8, FR-16)")]
        [Tooltip("Size of the mark on the screen, in pixels (the footage shows the 14 x 10 sprite at about 22 x 15).")]
        public Vector2Int TargetMarkSizePx = new Vector2Int(22, 15);
        [Tooltip("The mark's centre is this many screen pixels above the top of the unit's standing sprite.")]
        public int TargetMarkHeight = 66;
        [Tooltip("How far down the mark floats at the lowest, in screen pixels (cursors.act action 3: 10 sprite px, shown 1.5 times larger = 15; the footage: 514 to 529).")]
        [Min(0)] public int TargetMarkBobPixels = 15;
        [Tooltip("One float, high to low to high, takes this long, in ms (cursors.act action 3: 21 frames of 100 ms).")]
        [Min(1)] public int TargetMarkBobMs = 2100;
        [Tooltip("The float frame by frame, evenly spaced over TargetMarkBobMs: how far below the top, in sprite px (cursors.act action 3, offset y + 80). The largest value is TargetMarkBobPixels.")]
        public int[] TargetMarkBobFrames = { 0, 1, 3, 6, 9, 10, 10, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0, 0, 0, 0 };

        [Header("Input and restart (4.10, 4.11)")]
        [Tooltip("A plain press on a monster still held after this attacks continuously, in ms.")]
        [Min(0)] public int HoldAttackMs = 300;
        [Tooltip("A unit's pick rectangle is at least this wide and high, in screen pixels.")]
        [Min(0)] public float PickMinPx = 24f;
        [Tooltip("After Return to save point, with neither a map change nor a resurrection within this, the prompt opens again, in ms.")]
        [Min(0)] public int RestartTimeoutMs = 3000;
    }
}
