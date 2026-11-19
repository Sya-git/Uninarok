using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Uninarok.Map;
using Uninarok.RoImport.Core.Gnd;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Rsw;
using Uninarok.RoImport.Editor.Map;

namespace Uninarok.RoImport.Tests.Rsw
{
    // TASKS-0006 T5.1 / T5.3 (FR-1, FR-2, FR-5, FR-6, FR-8, FR-9): a synthetic map converts to one scene with the map root,
    // the ground, the four groups and the sun; models are prefab instances placed by the rules of T3.2; the other objects are
    // markers; static flags are set where baking and batching can use them.
    [Ignore("pending T5.3")]
    public class RoMapSceneTests
    {
        const float Eps = 1e-4f;
        RoMapTestBed _bed;

        [SetUp]
        public void SetUp() => _bed = new RoMapTestBed();

        [TearDown]
        public void TearDown() => _bed.Dispose();

        GameObject ConvertAndOpen()
        {
            var result = _bed.Convert();
            Assert.AreEqual(RoStatus.Created, result.Status, result.Failure?.Message);
            Assert.AreEqual(_bed.Paths.Scene, AssetDatabase.GetAssetPath(result.Value));
            return _bed.OpenScene();
        }

        static Transform Group(GameObject root, RoMapGroup group)
        {
            var t = RoMapNames.FindGroup(root.transform, group);
            Assert.IsNotNull(t, group.ToString());
            return t;
        }

        static void AssertNear(Vector3 expected, Vector3 actual, string what)
        {
            Assert.AreEqual(expected.x, actual.x, Eps, what + " x");
            Assert.AreEqual(expected.y, actual.y, Eps, what + " y");
            Assert.AreEqual(expected.z, actual.z, Eps, what + " z");
        }

        [Test]
        public void TheScene_HasTheMapRoot_WithItsChildrenInOrder()
        {
            var root = ConvertAndOpen();

            Assert.AreEqual("Map_sample", root.name);
            var children = Enumerable.Range(0, root.transform.childCount).Select(i => root.transform.GetChild(i).name).ToArray();
            CollectionAssert.AreEqual(new[] { "Ground", "Models (7)", "Lights (1)", "Sounds (1)", "Effects (1)", "Sun" }, children);
        }

        [Test]
        public void TheMapRoot_LinksTheMapData()
        {
            var root = ConvertAndOpen();
            var mapRoot = root.GetComponent<MapRoot>();

            Assert.IsNotNull(mapRoot);
            Assert.AreEqual("sample", mapRoot.MapName);
            Assert.IsNotNull(mapRoot.Data);
            Assert.AreEqual(_bed.Roots.Data + "/map/sample.asset", AssetDatabase.GetAssetPath(mapRoot.Data));
            Assert.IsTrue(mapRoot.Data.HasSettings, "the map data was converted with the RSW's settings");
        }

        [Test]
        public void TheGround_IsAnInstanceOfTheGroundPrefab_AtTheOrigin()
        {
            var ground = ConvertAndOpen().transform.Find(RoMapNames.Ground);

            Assert.IsTrue(PrefabUtility.IsOutermostPrefabInstanceRoot(ground.gameObject));
            Assert.AreEqual(_bed.Roots.Art + "/map/sample/sample_ground.prefab",
                AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(ground.gameObject)));
            Assert.AreEqual(Vector3.zero, ground.position);
            Assert.AreEqual(Quaternion.identity, ground.rotation);
        }

        [Test]
        public void Models_ArePrefabInstances_InFileOrder_WithTheFileNames()
        {
            var models = Group(ConvertAndOpen(), RoMapGroup.Models);

            var names = Enumerable.Range(0, models.childCount).Select(i => models.GetChild(i).name).ToArray();
            CollectionAssert.AreEqual(RoMapTestBed.Sample().Models.Select(m => m.Name).ToArray(), names);

            var box = models.GetChild(0).gameObject;
            Assert.IsTrue(PrefabUtility.IsOutermostPrefabInstanceRoot(box));
            Assert.AreEqual(_bed.Roots.Art + "/model/외부소품/box.prefab",
                AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(box)));
        }

        [Test]
        public void Instances_ShareThePrefabMaterial_WithNoRendererOverride()
        {
            // FR-2: changing the model's material changes every instance, because no instance overrides it.
            var models = Group(ConvertAndOpen(), RoMapGroup.Models);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_bed.Roots.Art + "/model/외부소품/box.prefab");
            var material = prefab.GetComponentInChildren<Renderer>().sharedMaterial;

            foreach (int i in new[] { 0, 1, 6 })
            {
                var instance = models.GetChild(i).gameObject;
                Assert.AreSame(material, instance.GetComponentInChildren<Renderer>().sharedMaterial, instance.name);
                var overrides = PrefabUtility.GetObjectOverrides(instance).Select(o => o.instanceObject).ToArray();
                Assert.IsFalse(overrides.Any(o => o is Renderer || o is MeshFilter), instance.name + ": no renderer override");
            }
        }

        [Test]
        public void Instances_ArePlacedByTheRules()
        {
            var models = Group(ConvertAndOpen(), RoMapGroup.Models);
            var space = new RoMapSpace(RoMapTestBed.Width, RoMapTestBed.Height, 10f);

            foreach (int i in new[] { 0, 1, 6 })
            {
                var model = RoMapTestBed.Sample().Models[i];
                RoRswPlacement.ToUnity(RoRswPlacement.ModelMatrixRo(model, space, RoRswRules.V1), out var p, out var r, out var s);
                var t = models.GetChild(i);
                AssertNear(p, t.position, model.Name + " position");
                Assert.AreEqual(1f, Mathf.Abs(Quaternion.Dot(r, t.rotation)), Eps, model.Name + " rotation");
                AssertNear(s, t.localScale, model.Name + " scale");
            }
            AssertNear(new Vector3(2f, 0f, 1.5f), models.GetChild(0).position, "the centre of a 4 x 3 map");
        }

        [Test]
        public void AnimatedInstances_FollowTheAnimationTypeAndSpeed()
        {
            var models = Group(ConvertAndOpen(), RoMapGroup.Models);

            var moving = models.GetChild(2).GetComponentInChildren<Animator>();
            Assert.IsTrue(moving.enabled, "type 2 keeps the animator");
            Assert.AreEqual(0.5f, moving.speed, Eps, "the RSW speed (D14)");
            Assert.IsFalse(models.GetChild(3).GetComponentInChildren<Animator>().enabled, "type 0 is a static prop");
        }

        [Test]
        public void LightsSoundsAndEffects_AreMarkersWithTheirParameters()
        {
            var root = ConvertAndOpen();
            var rsw = RoMapTestBed.Sample();

            var light = Group(root, RoMapGroup.Lights).GetChild(0);
            Assert.AreEqual("torch", light.name);
            var lightMarker = light.GetComponent<MapLightMarker>();
            Assert.AreEqual(rsw.Lights[0].Color, lightMarker.color);
            Assert.AreEqual(1.5f, lightMarker.range, Eps, "range in Unity units");
            Assert.IsNull(light.GetComponent<Light>(), "a placeholder, not a scene light");

            var sound = Group(root, RoMapGroup.Sounds).GetChild(0).GetComponent<MapSoundMarker>();
            Assert.AreEqual(("se_prtbird_01.wav", 0.8f, 10f, 4f, 10, 10), (sound.file, sound.volume, sound.range, sound.interval, sound.width, sound.height));

            var effect = Group(root, RoMapGroup.Effects).GetChild(0).GetComponent<MapEffectMarker>();
            Assert.AreEqual((44, 40f, 35f, 35f, 0f, 0f), (effect.preset, effect.delay, effect.a, effect.b, effect.c, effect.d));

            var space = new RoMapSpace(RoMapTestBed.Width, RoMapTestBed.Height, 10f);
            AssertNear(Core.Space.RoSpace.Point(space.FromRswRo(rsw.Lights[0].Position)), light.position, "light position");
        }

        [Test]
        public void StaticFlags_AreOnTheGroundAndStillInstances_NotOnMovingOnesMarkersOrGroups()
        {
            var root = ConvertAndOpen();
            const StaticEditorFlags full = StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic
                                           | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
            var models = Group(root, RoMapGroup.Models);

            foreach (var chunk in root.transform.Find(RoMapNames.Ground).GetComponentsInChildren<MeshRenderer>())
                Assert.AreEqual(full, GameObjectUtility.GetStaticEditorFlags(chunk.gameObject) & full, chunk.name);
            foreach (int i in new[] { 0, 1, 3, 6 })
                foreach (var r in models.GetChild(i).GetComponentsInChildren<Renderer>())
                    Assert.AreEqual(full, GameObjectUtility.GetStaticEditorFlags(r.gameObject) & full, models.GetChild(i).name);

            var moving = GameObjectUtility.GetStaticEditorFlags(models.GetChild(2).GetComponentInChildren<Renderer>().gameObject);
            Assert.AreEqual((StaticEditorFlags)0, moving & (StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic), "an animated instance");

            foreach (var group in new[] { RoMapGroup.Models, RoMapGroup.Lights, RoMapGroup.Sounds, RoMapGroup.Effects })
                Assert.AreEqual((StaticEditorFlags)0, GameObjectUtility.GetStaticEditorFlags(Group(root, group).gameObject), group.ToString());
            Assert.AreEqual((StaticEditorFlags)0, GameObjectUtility.GetStaticEditorFlags(Group(root, RoMapGroup.Lights).GetChild(0).gameObject), "a marker");
        }

        [Test]
        public void TheSunAndAmbient_FollowTheRswLight()
        {
            var root = ConvertAndOpen();
            var rsw = RoMapTestBed.Sample();

            var sun = root.transform.Find(RoMapNames.Sun).GetComponent<Light>();
            Assert.AreEqual(LightType.Directional, sun.type);
            Assert.AreEqual(LightShadows.Soft, sun.shadows);
            Assert.AreEqual(RoRswLighting.Sun(rsw.Light), sun.color);
            Assert.AreEqual(RoRswLighting.ShadowStrength(rsw.Light), sun.shadowStrength, Eps);
            Assert.AreEqual(1f, Mathf.Abs(Quaternion.Dot(RoRswLighting.SunRotation(rsw.Light), sun.transform.rotation)), Eps);
            Assert.AreEqual(UnityEngine.Rendering.AmbientMode.Flat, RenderSettings.ambientMode);
            Assert.AreEqual(RoRswLighting.Ambient(rsw.Light), RenderSettings.ambientLight);
        }
    }
}
