using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using Uninarok.Catalog;
using Uninarok.Networking.Data;
using Random = System.Random;

namespace Uninarok.Units.Dev
{
    /// <summary>
    /// The stress mode (PLAN-0009 4.8; SPEC-0009 FR-13, FR-14): fills the walkable cells around the camera target with fully
    /// equipped players and monsters, each playing one animation in place and turning now and then, from a fixed seed so runs
    /// compare. It measures the unit update (<see cref="UnitSystem.LastTickMs"/>) and the garbage of each frame.
    /// </summary>
    [RequireComponent(typeof(EntitySandbox))]
    public sealed class EntitySandboxStress : MonoBehaviour
    {
        public const int Players = 100, Monsters = 200, Window = 120;

        /// <summary>The unit update budget in milliseconds (FR-14).</summary>
        public const double BudgetMs = 2.0;

        struct Turner
        {
            public UnitView View;
            public float Next;
        }

        EntitySandbox _sandbox;
        readonly List<Turner> _turners = new List<Turner>();
        Random _random = new Random(1);
        readonly double[] _ticks = new double[Window];
        readonly long[] _garbage = new long[Window];
        int _samples;
        ProfilerRecorder _gc;

        public bool Running { get; private set; }
        public double MeanMs { get; private set; }
        public double MaxMs { get; private set; }
        /// <summary>GC bytes of the last frame, of the whole player loop (in the editor the editor's own count too).</summary>
        public long GarbageLast { get; private set; }
        public long GarbageMean { get; private set; }
        public bool HasWindow => _samples >= Window;

        static PlayerJob[] Jobs => new[]
        {
            PlayerJob.JOB_NOVICE, PlayerJob.JOB_SWORDMAN, PlayerJob.JOB_MERCHANT, PlayerJob.JOB_THIEF, PlayerJob.JOB_KNIGHT, PlayerJob.JOB_KNIGHT2,
            PlayerJob.JOB_CRUSADER, PlayerJob.JOB_BLACKSMITH,
        };

        /// <summary>The reference monsters (composition-reference.md 7) and a few more.</summary>
        static int[] MonsterJobs => new[] { 1002, 1063, 1007, 1113, 1014, 1031 };

        static PcAnimation[] PlayerAnimations => new[] { PcAnimation.Idle, PcAnimation.Walk, PcAnimation.Attack1, PcAnimation.Attack2, PcAnimation.Hurt, PcAnimation.Dead, PcAnimation.Standby };
        static MonsterAnimation[] MonsterAnimations => new[] { MonsterAnimation.Idle, MonsterAnimation.Walk, MonsterAnimation.Attack, MonsterAnimation.Hurt, MonsterAnimation.Die };

        void Awake() => _sandbox = GetComponent<EntitySandbox>();

        void OnEnable() => _gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

        void OnDisable() => _gc.Dispose();

        /// <summary>Replaces the sandbox's units by the FR-14 load; one action.</summary>
        public void Begin(int players = Players, int monsters = Monsters, int seed = 9)
        {
            End();
            _random = new Random(seed);
            var cells = Cells(players + monsters);
            int n = 0;
            var jobs = Jobs;
            var monsterJobs = MonsterJobs;
            for (int i = 0; i < players && n < cells.Count; i++, n++)
            {
                var view = _sandbox.Spawn(RandomPlayer(jobs[i % jobs.Length]), cells[n].x, cells[n].y, (UninarokDirection)_random.Next(8));
                var animations = PlayerAnimations;
                view.Play(animations[_random.Next(animations.Length)]);
                Add(view);
            }
            for (int i = 0; i < monsters && n < cells.Count; i++, n++)
            {
                var view = _sandbox.Spawn(UnitLook.ForJob(monsterJobs[i % monsterJobs.Length]), cells[n].x, cells[n].y, (UninarokDirection)_random.Next(8));
                var animations = MonsterAnimations;
                view.Play(animations[_random.Next(animations.Length)]);
                Add(view);
            }
            Running = true;
            _samples = 0;
        }

        public void End()
        {
            Running = false;
            _turners.Clear();
            _sandbox.Clear();
            _samples = 0;
            MeanMs = MaxMs = 0;
            GarbageLast = GarbageMean = 0;
        }

        void Add(UnitView view) => _turners.Add(new Turner { View = view, Next = Time.time + NextTurn() });

        float NextTurn() => 2f + (float)_random.NextDouble() * 4f;

        // Every slot filled; ids the catalog has.
        UnitLook RandomPlayer(PlayerJob job) => new UnitLook
        {
            Kind = UnitKind.Player,
            JobId = (int)job,
            Sex = _random.Next(2) == 0 ? AccountSex.SEX_FEMALE : AccountSex.SEX_MALE,
            HairStyle = 1 + _random.Next(20),
            HairColour = _random.Next(6),
            HeadTop = Known(EquipmentKind.Headgear, 1, 120),
            HeadMid = Known(EquipmentKind.Headgear, 1, 120),
            HeadBottom = Known(EquipmentKind.Headgear, 1, 120),
            Garment = Known(EquipmentKind.Garment, 1, 60),
            Weapon = 1,       // a dagger: every one of these classes has it
            WeaponItem = 1201,
            Shield = 1,
            ShieldItem = 2101,
        };

        int Known(EquipmentKind kind, int from, int to)
        {
            for (int tries = 0; tries < 50; tries++)
            {
                int id = _random.Next(from, to + 1);
                if (_sandbox.Loader.Source.Catalog.TryFindEquipment(kind, id, 0, out _)) return id;
            }
            return 0;
        }

        // Walkable cells in growing squares around the camera target.
        List<Vector2Int> Cells(int count)
        {
            var map = _sandbox.Map;
            var cells = new List<Vector2Int>(count);
            var centre = _sandbox.Rig != null ? _sandbox.Rig.Target : map.CellCenter(_sandbox.StartCell.x, _sandbox.StartCell.y);
            if (!map.TryGetCell(centre, out int cx, out int cz))
            {
                cx = _sandbox.StartCell.x;
                cz = _sandbox.StartCell.y;
            }
            for (int r = 0; r < 200 && cells.Count < count; r++)
                for (int z = -r; z <= r && cells.Count < count; z++)
                    for (int x = -r; x <= r && cells.Count < count; x++)
                        if (Math.Max(Math.Abs(x), Math.Abs(z)) == r && map.IsWalkable(cx + x, cz + z)) cells.Add(new Vector2Int(cx + x, cz + z));
            return cells;
        }

        void Update()
        {
            if (!Running) return;
            float now = Time.time;
            for (int i = 0; i < _turners.Count; i++)
            {
                var t = _turners[i];
                if (now < t.Next || t.View == null) continue;
                t.View.Facing = (UninarokDirection)_random.Next(8);
                t.Next = now + NextTurn();
                _turners[i] = t;
            }
            Sample();
        }

        void Sample()
        {
            int i = _samples++ % Window;
            _ticks[i] = UnitSystem.LastTickMs;
            GarbageLast = _gc.Valid ? _gc.LastValue : 0;
            _garbage[i] = GarbageLast;
            int n = Math.Min(_samples, Window);
            double sum = 0, max = 0;
            long garbage = 0;
            for (int k = 0; k < n; k++)
            {
                sum += _ticks[k];
                if (_ticks[k] > max) max = _ticks[k];
                garbage += _garbage[k];
            }
            MeanMs = sum / n;
            MaxMs = max;
            GarbageMean = garbage / n;
        }
    }
}
