using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Uninarok.Catalog;
using Uninarok.Map;
using Uninarok.Networking.Data;
using Uninarok.Units;
using Uninarok.World.View;

namespace Uninarok.World.PlayTests
{
    // SPEC-0010 FR-3 to FR-8, FR-16, FR-17 (PLAN-0010 4.9): the views follow the model and never write it.
    public class WorldPresenterPlayTests
    {
        MapData _map;
        WorldSettings _settings;
        WorldModel _model;
        AddressablesUnitAssetSource _source;
        WorldPresenter _presenter;
        GameObject _root;

        // A flat map: 100 x 100 cells of 0.5, everything walkable.
        static MapData FlatMap()
        {
            const int n = 100;
            var map = ScriptableObject.CreateInstance<MapData>();
            var flags = new byte[n * n];
            for (int i = 0; i < flags.Length; i++) flags[i] = (byte)MapCellFlags.Walkable;
            var heights = new float[n * n * 4];
            for (int i = 0; i < heights.Length; i++) heights[i] = 2f;
            map.Assign("flat", n, n, 0.5f, new Vector2(-10f, -20f), 1f, false, heights, new byte[n * n], flags, new byte[n * n]);
            return map;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            if (UnityEditor.AssetDatabase.LoadMainAssetAtPath(SpriteCatalogKeys.Index(SpriteCatalogCategory.Bodies)) == null)
                Assert.Ignore("the catalog is not imported yet (Tools/Ragnarok/Import Catalog…)");
#endif
            _map = FlatMap();
            _settings = ScriptableObject.CreateInstance<WorldSettings>();
            _model = new WorldModel(new MapDataWalkGrid(_map), _settings);
            _root = new GameObject("Root");
            bool done = false;
            AddressablesUnitAssetSource.LoadAsync((s, e) =>
            {
                _source = s;
                done = true;
            }, SpriteCatalogCategory.GroundItems);
            for (int i = 0; i < 1800 && !done; i++) yield return null;
            Assert.IsNotNull(_source);
            _presenter = new WorldPresenter(new UnitLoader(_source), null, _root.transform, _settings) { Map = _map };
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _presenter?.Dispose();
            yield return null;
            if (_root != null) UnityEngine.Object.Destroy(_root);
            yield return null;
            _source?.Dispose();
            UnityEngine.Object.Destroy(_map);
            UnityEngine.Object.Destroy(_settings);
        }

        void Poring(uint id, int x, int y) =>
            _model.Upsert(id, EntityKind.Monster, new EntityLook { Job = 1002 }, 200, x, y, UninarokDirection.West, HeadDirection.Straight, EntityState.Standing);

        void Novice(uint id, int x, int y, EntityState state = EntityState.Standing) =>
            _model.Upsert(id, EntityKind.Player, new EntityLook { Job = 0, HairStyle = 1, Sex = 1 }, 150, x, y, UninarokDirection.South, HeadDirection.Left, state);

        IEnumerator Loaded(uint id)
        {
            for (int i = 0; i < 600 && !(_presenter.ViewOf(id) != null && _presenter.ViewOf(id).Loaded); i++) yield return null;
            Assert.IsTrue(_presenter.ViewOf(id).Loaded);
        }

        [UnityTest, Category("NeedsData")]
        public IEnumerator Views_AppearAtTheirPlace_WithLookFacingAndPosture_AndLeave()
        {
            Poring(1, 10, 20);
            Novice(2, 12, 20, EntityState.Sitting);
            _presenter.Sync(_model);
            yield return Loaded(1);
            yield return Loaded(2);
            var poring = _presenter.ViewOf(1);
            Assert.AreEqual(new Vector3(-10f + 10.5f * 0.5f, 2f, -20f + 20.5f * 0.5f), poring.transform.position, "the centre of cell 10,20, on the ground");
            Assert.AreEqual((1002, UninarokDirection.West), (poring.Look.JobId, poring.Facing));
            var novice = _presenter.ViewOf(2);
            Assert.AreEqual((UnitKind.Player, HeadFacing.Left, (int)PcAnimation.Sit), (novice.Look.Kind, novice.HeadFacing, novice.AnimationNumber));

            // a look change in place
            _model.SetLook(2, LookType.HeadTop, 16, 0);
            _presenter.Sync(_model);
            Assert.AreEqual(16, novice.Look.HeadTop);
            Assert.AreSame(novice, _presenter.ViewOf(2));

            _model.Vanish(1, VanishReason.OutOfSight, 0);
            _presenter.Sync(_model);
            Assert.IsNull(_presenter.ViewOf(1));
            Assert.IsFalse(poring.gameObject.activeSelf);
            Assert.AreEqual(1, _presenter.ShownCount);
        }

        [UnityTest, Category("NeedsData")]
        public IEnumerator AKnifeSentAsItsItemId_IsFound()
        {
            // a novice's Knife on the local server: 1201 is an item id, its sprite is the dagger's
            _model.Upsert(2, EntityKind.Player, new EntityLook { Job = 0, HairStyle = 1, Sex = 1, Weapon = 1201 }, 150, 12, 20, UninarokDirection.South, HeadDirection.Straight, EntityState.Standing);
            _presenter.Sync(_model);
            yield return Loaded(2);
            Assert.IsTrue(_presenter.ViewOf(2).Report.TryGet(PartSlot.Weapon, out var line) && line.Found, _presenter.ViewOf(2).Report.ToString());
        }

        [UnityTest, Category("NeedsData")]
        public IEnumerator AWalk_MovesTheView_AndPlaysWalkAtItsSpeed()
        {
            Poring(1, 10, 20);
            _presenter.Sync(_model);
            yield return Loaded(1);
            var view = _presenter.ViewOf(1);
            _model.Walk(1, 10, 20, 15, 20, 0, 0);
            _model.Advance(500);
            _presenter.Sync(_model);
            Assert.AreEqual(-10f + 13f * 0.5f, view.transform.position.x, 1e-4f, "2.5 cells on");
            Assert.AreEqual(((int)MonsterAnimation.Walk, 200f / 150f, UninarokDirection.East), (view.AnimationNumber, view.Speed, view.Facing));
            _model.Advance(1000);
            _presenter.Sync(_model);
            Assert.AreEqual(((int)MonsterAnimation.Idle, 1f), (view.AnimationNumber, view.Speed));
        }

        [UnityTest, Category("NeedsData")]
        public IEnumerator ADeadUnit_Falls_AndOneThatComesIntoViewDead_Lies()
        {
            Poring(1, 10, 20);
            _model.Upsert(2, EntityKind.Monster, new EntityLook { Job = 1002 }, 200, 12, 20, UninarokDirection.West, HeadDirection.Straight, EntityState.Dead);
            _presenter.Sync(_model);
            yield return Loaded(1);
            yield return Loaded(2);
            _model.Vanish(1, VanishReason.Died, 0);
            _presenter.Sync(_model);
            UnitSystem.Tick(0.001f);
            Assert.AreEqual((int)MonsterAnimation.Die, _presenter.ViewOf(1).AnimationNumber);
            Assert.AreEqual((int)MonsterAnimation.Die, _presenter.ViewOf(2).AnimationNumber);
            Assert.AreEqual(0, _presenter.ViewOf(1).Frame);
            Assert.Greater(_presenter.ViewOf(2).Frame, 0, "its last frame");
        }

        [UnityTest, Category("NeedsData")]
        public IEnumerator AReusedView_NeverShowsTheUnitBefore()
        {
            Poring(1, 10, 20);
            _presenter.Sync(_model);
            yield return Loaded(1);
            var view = _presenter.ViewOf(1);
            UnitSystem.Tick(0.016f);
            Assert.GreaterOrEqual(view.Frame, 0);
            _model.Vanish(1, VanishReason.OutOfSight, 0);
            Novice(2, 12, 20);
            _presenter.Sync(_model);
            var reused = _presenter.ViewOf(2);
            Assert.AreSame(view, reused, "the pool gives the last one back");
            UnitSystem.Tick(0.016f);
            foreach (var r in reused.GetComponentsInChildren<SpriteRenderer>())
                Assert.IsFalse(r.enabled && r.sprite != null && !reused.Loaded, "nothing of the poring while the novice loads");
            yield return Loaded(2);
            Assert.AreEqual(UnitKind.Player, reused.Look.Kind);
        }

        [UnityTest, Category("NeedsData")]
        public IEnumerator TheOwnView_StaysLoaded_ThroughAMapChange()
        {
            Novice(1, 10, 20);
            Poring(2, 11, 20);
            _model.OwnId = 1;
            _presenter.Sync(_model);
            yield return Loaded(1);
            yield return Loaded(2);
            var own = _presenter.ViewOf(1);

            // the session empties the old world first, then the presenter leaves the map
            _model.Clear();
            _presenter.ReleaseAll();
            _presenter.Map = null;
            Assert.IsFalse(own.gameObject.activeSelf);
            var next = new WorldModel(new MapDataWalkGrid(_map), _settings);
            next.Upsert(1, EntityKind.Player, new EntityLook { Job = 0, HairStyle = 1, Sex = 1 }, 150, 40, 41, UninarokDirection.North, HeadDirection.Straight, EntityState.Standing);
            next.OwnId = 1;
            _presenter.Map = _map;
            _presenter.Sync(next);
            Assert.AreSame(own, _presenter.ViewOf(1));
            UnitSystem.Tick(0.016f);
            Assert.IsTrue(own.Loaded && own.Frame >= 0, "shown on the first frame (FR-12)");
            Assert.AreEqual((UninarokDirection.North, true), (own.Facing, own.gameObject.activeSelf));

            _presenter.ForgetOwn();
            for (int i = 0; i < 60; i++) yield return null;
            Assert.AreEqual((0, 0), (_source.HeldCount, _presenter.ShownCount));
        }

        [UnityTest, Category("NeedsData")]
        public IEnumerator EveryHandle_IsReleased_WithTheLastView()
        {
            for (uint i = 1; i <= 20; i++)
            {
                if (i % 2 == 0) Poring(i, (int)i, 20);
                else Novice(i, (int)i, 20);
            }
            _presenter.Sync(_model);
            yield return Loaded(20);
            yield return Loaded(19);
            Assert.Greater(_source.HeldCount, 0);
            _presenter.ReleaseAll();
            for (int i = 0; i < 60; i++) yield return null;
            Assert.AreEqual((0, 0), (_source.HeldCount, _presenter.ShownCount));
        }

        [UnityTest, Category("NeedsData")]
        public IEnumerator AnItem_ShowsItsGroundSprite_OnItsSubCell_AndLeaves()
        {
            _model.AddItem(501, 501, true, 10, 20, 3, 12, 1);      // a red potion
            _presenter.Sync(_model);
            var view = _presenter.ItemViewOf(501);
            for (int i = 0; i < 600 && !view.Loaded; i++) yield return null;
            Assert.IsTrue(view.Loaded);
            Assert.AreEqual(new Vector3(-10f + 10.25f * 0.5f, 2f, -20f + 21f * 0.5f), view.transform.position, "sub / 12 from the cell's corner");
            bool shown = (view.Still != null && view.Still.enabled && view.Still.sprite != null) || (view.Actor != null && view.Actor.CurrentFrame >= 0);
            Assert.IsTrue(shown, "its sprite");
            Assert.AreEqual((1, 0), (_presenter.ItemCount, _presenter.ShownCount));
            Assert.Greater(_source.HeldCount, 0);
            for (int i = 0; i < 600 && view.Shadow == null; i++) yield return null;
            Assert.IsTrue(view.Shadow.Visible && view.Shadow.Renderer.sprite != null, "its shadow");
            Assert.AreEqual((0.4f, 0f), (view.Shadow.Scale, view.Height));

            _model.RemoveItem(501);
            _presenter.Sync(_model);
            Assert.AreEqual((0, 0), (_presenter.ItemCount, _source.HeldCount));
            Assert.IsFalse(view.gameObject.activeSelf);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("item 99999999 has no ground sprite"));
            _model.AddItem(502, 99999999, true, 10, 20, 3, 12, 1);
            _model.AddItem(503, 99999999, true, 10, 20, 3, 12, 1);
            _presenter.Sync(_model);
            Assert.AreEqual(2, _presenter.ItemCount, "one warning per item id, nothing shown");

            // the slot of an item taken by a unit
            _model.RemoveItem(502);
            Poring(9, 10, 20);
            _presenter.Sync(_model);
            Assert.IsNotNull(_presenter.ViewOf(9));
            Assert.AreEqual((1, 1), (_presenter.ItemCount, _presenter.ShownCount));
        }

        [UnityTest, Category("NeedsData")]
        public IEnumerator ADroppedItem_FallsToItsShadow()
        {
            _model.AddItem(501, 501, true, 10, 20, 6, 6, 1, true, 1000);
            _presenter.Sync(_model);
            var view = _presenter.ItemViewOf(501);
            for (int i = 0; i < 600 && (!view.Loaded || view.Shadow == null); i++) yield return null;
            Assert.AreEqual(0.975f, view.Height, 1e-4f, "1.95 cells");
            float ground = view.Shadow.Renderer.transform.position.y;
            _model.Advance(1086);
            _presenter.Sync(_model);
            Assert.Greater(view.Height, 1.03f, "up first");
            _model.Advance(1500);
            _presenter.Sync(_model);
            Assert.AreEqual((0f, ground), (view.Height, view.Shadow.Renderer.transform.position.y), "the shadow stays on the ground");
            _model.RemoveItem(501);
            _presenter.Sync(_model);
            Assert.AreEqual(0, _source.HeldCount);
        }

        [UnityTest, Category("NeedsData")]
        public IEnumerator WorldPresenter_ReadsOnly()
        {
            for (uint i = 1; i <= 10; i++) Poring(i, (int)i, 20);
            _model.Walk(3, 3, 20, 8, 22, 0, 0);
            _model.Advance(300);
            uint before = _model.Checksum();
            _presenter.Sync(_model);
            yield return Loaded(10);
            _presenter.Sync(_model);
            _presenter.ReleaseAll();
            _presenter.Sync(_model);
            Assert.AreEqual(before, _model.Checksum());
            // a destroyed display leaves the model alone, and its entity is still updated
            UnityEngine.Object.DestroyImmediate(_presenter.ViewOf(3).gameObject);
            _model.Advance(5000);
            Assert.IsTrue(_model.TryGetSlot(3, out int slot));
            Assert.AreEqual((8, 22), (_model[slot].CellX, _model[slot].CellY));
        }

        [UnityTest, Category("NeedsData")]
        public IEnumerator WorldPresenter_ZeroAllocationSteadyState()
        {
            for (uint i = 1; i <= 100; i++)
            {
                if (i % 3 == 0) Novice(i, (int)(10 + i % 60), (int)(10 + i % 50));
                else Poring(i, (int)(10 + i % 60), (int)(10 + i % 50));
            }
            _presenter.Sync(_model);
            yield return Loaded(100);
            yield return Loaded(99);
            uint now = 0;
            void Walk()
            {
                for (uint i = 1; i <= 100; i++)
                {
                    _model.TryGetSlot(i, out int slot);
                    _model.Walk(i, _model[slot].CellX, _model[slot].CellY, 20 + (int)((i + now) % 40), 20 + (int)((i * 7 + now) % 40), now, now);
                }
            }
            Action frames = () =>
            {
                for (int f = 0; f < 100; f++)
                {
                    now += 16;
                    if (f % 50 == 0) Walk();
                    _model.Advance(now);
                    _presenter.Sync(_model);
                }
            };
            frames();
            Assert.That(() => frames(), UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
        }

        // ---- SPEC-0011 T5.3 (FR-4, FR-9; PLAN-0011 4.9): the timeline's motions over the base animation ----

        CombatTimeline Combat(out CombatSettings settings)
        {
            settings = ScriptableObject.CreateInstance<CombatSettings>();
            var timeline = new CombatTimeline(settings, _model.Capacity);
            timeline.Use(_model);
            _presenter.Timeline = timeline;
            return timeline;
        }

        void At(CombatTimeline timeline, uint now)
        {
            _model.Advance(now);
            timeline.Advance(now);
            _presenter.Sync(_model);
        }

        static Blow Hit(uint attacker, uint target, uint at) => new Blow(attacker, target, at, 500, 300, 10, 0, 1, BlowKind.Normal, false, 0);

        [UnityTest, Category("NeedsData")]
        public IEnumerator WorldPresenter_PlaysMotionThenBase()
        {
            Poring(1, 13, 20);
            Novice(2, 12, 20);
            _presenter.Sync(_model);
            yield return Loaded(1);
            yield return Loaded(2);
            var timeline = Combat(out var settings);
            var poring = _presenter.ViewOf(1);
            var novice = _presenter.ViewOf(2);

            timeline.Add(Hit(2, 1, 1000));
            At(timeline, 1000);
            Assert.AreEqual((int)PcAnimation.Attack1, novice.AnimationNumber, "the attack");
            Assert.AreEqual(Uninarok.Units.PlayMode.OnceHold, novice.PlayMode);
            Assert.AreEqual((int)MonsterAnimation.Idle, poring.AnimationNumber);
            At(timeline, 1500);
            Assert.AreEqual((int)UnitAnimations.ToUnit(PcAnimation.Hurt), poring.AnimationNumber, "the hurt at the hit moment");
            Assert.AreEqual((int)PcAnimation.Standby, novice.AnimationNumber, "then the player stands by");
            At(timeline, 1800);
            Assert.AreEqual((int)MonsterAnimation.Idle, poring.AnimationNumber, "back to its base animation");

            _model.Walk(2, 12, 20, 16, 20, 1900, 1900);
            At(timeline, 1950);
            Assert.AreEqual((int)PcAnimation.Walk, novice.AnimationNumber, "a walk ends the standby and shows itself");
            UnityEngine.Object.Destroy(settings);
        }

        [UnityTest, Category("NeedsData")]
        public IEnumerator WorldPresenter_DeathAfterHurt()
        {
            Poring(1, 13, 20);
            Novice(2, 12, 20);
            _presenter.Sync(_model);
            yield return Loaded(1);
            var timeline = Combat(out var settings);
            var poring = _presenter.ViewOf(1);

            timeline.Add(Hit(2, 1, 1000));
            At(timeline, 1000);
            _model.Vanish(1, VanishReason.Died, 1200);
            At(timeline, 1200);
            Assert.AreNotEqual((int)MonsterAnimation.Die, poring.AnimationNumber, "the death waits for its hit");
            At(timeline, 1500);
            Assert.AreEqual((int)UnitAnimations.ToUnit(PcAnimation.Hurt), poring.AnimationNumber);
            At(timeline, 1749);
            Assert.AreEqual((int)UnitAnimations.ToUnit(PcAnimation.Hurt), poring.AnimationNumber, "250 ms of hurt");
            At(timeline, 1750);
            Assert.AreEqual((int)MonsterAnimation.Die, poring.AnimationNumber, "then it dies");
            UnityEngine.Object.Destroy(settings);
        }

        // ---- SPEC-0011 T6.3 (FR-7; PLAN-0011 4.8): the bars under the feet, constant in screen pixels ----

        CombatOverlay Overlay(out Camera camera, out OwnStatus status)
        {
            CombatSettings settings = null;
#if UNITY_EDITOR
            settings = UnityEditor.AssetDatabase.LoadAssetAtPath<CombatSettings>("Assets/Settings/World/CombatSettings.asset");
#endif
            if (settings == null) Assert.Ignore("no combat settings");
            var cameraObject = new GameObject("Bar Camera");
            cameraObject.transform.SetParent(_root.transform, false);
            camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 15f;
            camera.nearClipPlane = 1f;
            camera.targetTexture = new RenderTexture(1280, 720, 24);
            camera.enabled = false;
            status = new OwnStatus { Hp = 50, MaxHp = 100, Sp = 10, MaxSp = 20 };
            var overlay = new GameObject("Overlay").AddComponent<CombatOverlay>();
            overlay.transform.SetParent(_root.transform, false);
            overlay.enabled = false; // drawn by the test
            overlay.Use(_model, status, null, _presenter, settings);
            return overlay;
        }

        static void LookAt(Camera camera, Vector3 target, float yaw)
        {
            camera.transform.rotation = Quaternion.Euler(50f, yaw, 0f);
            camera.transform.position = target - camera.transform.forward * 40f;
        }

        static Rect ScreenRectOf(Camera camera, Mesh mesh, int quad)
        {
            var v = mesh.vertices;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                var s = camera.WorldToScreenPoint(v[quad * 4 + i]);
                minX = Mathf.Min(minX, s.x);
                maxX = Mathf.Max(maxX, s.x);
                minY = Mathf.Min(minY, s.y);
                maxY = Mathf.Max(maxY, s.y);
            }
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        [UnityTest, Category("NeedsData")]
        public IEnumerator CombatOverlay_BarsFollowAtFourTurns()
        {
            Poring(1, 13, 20);
            _model.SetHp(1, 5, 10);
            _presenter.Sync(_model);
            yield return Loaded(1);
            var overlay = Overlay(out var camera, out _);
            var feet = _presenter.ViewOf(1).transform.position;
            foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
            {
                LookAt(camera, feet, yaw);
                overlay.Draw(camera);
                Assert.AreEqual(1, overlay.DrawnBars, $"yaw {yaw}");
                var frame = ScreenRectOf(camera, overlay.BarMesh.Mesh, 0);
                Assert.AreEqual(60f, frame.width, 0.05f, $"yaw {yaw}: 60 px wide at any turn");
                Assert.AreEqual(5f, frame.height, 0.05f, $"yaw {yaw}: 5 px high");
                var under = camera.WorldToScreenPoint(feet - Vector3.up * (0.5f * _map.CellSize));
                Assert.AreEqual(Mathf.Round(under.y), frame.yMax, 0.05f, $"yaw {yaw}: half a cell under the feet");
                Assert.AreEqual(Mathf.Round(under.x), frame.center.x, 1f, $"yaw {yaw}: centred under the feet");
                Assert.AreEqual(Mathf.Round(frame.xMin), frame.xMin, 0.05f, "on whole pixels");
                var fill = ScreenRectOf(camera, overlay.BarMesh.Mesh, 1);
                Assert.AreEqual(29f, fill.width, 0.05f, "half of the 58 px inside");
            }
        }

        [UnityTest, Category("NeedsData")]
        public IEnumerator CombatOverlay_BarHiddenAtFullAndWhenDead()
        {
            Poring(1, 13, 20);
            Poring(3, 15, 20);
            Poring(4, 17, 20);
            Poring(5, 19, 20);
            Novice(2, 12, 22);
            _model.OwnId = 2;
            _model.SetHp(1, 5, 10);
            _model.SetHp(3, 10, 10);  // full: nothing is sent at full, nothing shown
            _model.SetHp(5, 5, 10);
            _model.Vanish(5, VanishReason.Died, 0);
            _presenter.Sync(_model);
            yield return Loaded(1);
            yield return Loaded(2);
            var overlay = Overlay(out var camera, out var status);
            LookAt(camera, _presenter.ViewOf(2).transform.position, 0f);
            overlay.Draw(camera);
            Assert.AreEqual(2, overlay.DrawnBars, "the hurt poring and the own character; not at full, unknown or dead");
            Assert.AreEqual(3 + 5, overlay.BarMesh.Count, "a monster's frame and HP row; the own frame, HP and SP rows");

            status.Hp = 0;
            _model.Vanish(2, VanishReason.Died, 0);
            _presenter.Sync(_model);
            overlay.Draw(camera);
            Assert.AreEqual(1, overlay.DrawnBars, "no bar while the own character is dead");
        }

        [TestCase(0, 1, 0, 2)] // job, sex, server style, head file
        [TestCase(0, 0, 0, 2)]
        [TestCase(0, 1, 2, 1)]
        [TestCase(0, 0, 2, 4)]
        [TestCase(0, 1, 12, 11)]
        [TestCase(0, 0, 12, 8)]
        [TestCase(0, 1, 13, 13)]
        [TestCase(4218, 1, 2, 2)]
        [TestCase(4218, 0, 0, 1)]
        public void LookMapping_HairStyle_IsTheClientsHeadFile(int job, int sex, int style, int head)
        {
            var look = new EntityLook { Job = (short)job, Sex = (byte)sex, HairStyle = (ushort)style };
            Assert.AreEqual(head, EntityLookMapping.ToUnitLook(EntityKind.Player, look).HairStyle);
        }

        [Test]
        public void LookMapping_PlayersByClass_TheRestByJob()
        {
            var look = new EntityLook
            {
                Job = 4008, HairStyle = 5, HairColour = 3, ClothColour = 2, BodyStyle = 1, Weapon = 1201, Shield = 2101,
                HeadTop = 16, HeadMid = 10, HeadBottom = 8, Robe = 1, Sex = 0,
            };
            var u = EntityLookMapping.ToUnitLook(EntityKind.Player, look);
            Assert.AreEqual((UnitKind.Player, 4008, AccountSex.SEX_FEMALE, 5, 3, 2, 1), (u.Kind, u.JobId, u.Sex, u.HairStyle, u.HairColour, u.ClothColour, u.BodyStyle));
            Assert.AreEqual((0, 1201, 0, 2101, 16, 10, 8, 1), (u.Weapon, u.WeaponItem, u.Shield, u.ShieldItem, u.HeadTop, u.HeadMid, u.HeadBottom, u.Garment));
            look.Weapon = 2;
            look.Shield = 1;
            u = EntityLookMapping.ToUnitLook(EntityKind.Player, look);
            Assert.AreEqual((2, 0, 1, 0), (u.Weapon, u.WeaponItem, u.Shield, u.ShieldItem), "view ids");
            Assert.AreEqual(UnitKind.Player, EntityLookMapping.ToUnitLook(EntityKind.Monster, look).Kind, "a monster that looks like a player class");
            Assert.AreEqual((UnitKind.Unit, 117), (EntityLookMapping.ToUnitLook(EntityKind.Npc, new EntityLook { Job = 117 }).Kind, EntityLookMapping.ToUnitLook(EntityKind.Npc, new EntityLook { Job = 117 }).JobId));
            Assert.AreEqual(UnitKind.Unit, EntityLookMapping.ToUnitLook(EntityKind.Player, new EntityLook { Job = 1002 }).Kind, "a disguised player");
            Assert.IsTrue(EntityLookMapping.IsInvisible(new EntityLook { Job = -1 }) && EntityLookMapping.IsInvisible(new EntityLook { Job = 32767 }));
        }
    }
}
