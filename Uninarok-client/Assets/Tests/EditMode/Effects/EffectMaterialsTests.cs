using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Uninarok.Effects.Tests
{
    /// <summary>EffectMaterials: one material per key, kept, 0 B on repeat lookups (SPEC-0012 FR-3, FR-9).</summary>
    public class EffectMaterialsTests
    {
        const string SettingsPath = "Assets/Settings/Effects/EffectSettings.asset";

        EffectSettings settings;
        EffectMaterials materials;
        Texture2D texA, texB;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<EffectSettings>();
            var real = AssetDatabase.LoadAssetAtPath<EffectSettings>(SettingsPath);
            settings.Shader = real.Shader;
            settings.PlaceholderTexture = real.PlaceholderTexture;
            settings.DepthBias = 0.75f;
            materials = new EffectMaterials(settings);
            texA = new Texture2D(2, 2);
            texB = new Texture2D(2, 2);
        }

        [TearDown]
        public void TearDown()
        {
            materials.Dispose();
            UnityEngine.Object.DestroyImmediate(texA);
            UnityEngine.Object.DestroyImmediate(texB);
            UnityEngine.Object.DestroyImmediate(settings);
        }

        [Test]
        public void SettingsAsset_HasPlaceholderAndShader()
        {
            var real = AssetDatabase.LoadAssetAtPath<EffectSettings>(SettingsPath);
            Assert.IsNotNull(real.PlaceholderTexture);
            Assert.IsNotNull(real.Shader);
        }

        [Test]
        public void SameKey_GivesSameInstance()
        {
            var a = materials.Get(texA, 5, 6, EffectFacing.Camera, false);
            var b = materials.Get(texA, 5, 6, EffectFacing.Camera, false);
            Assert.AreSame(a, b);
            Assert.AreEqual(1, materials.Count);
        }

        [Test]
        public void EachKeyPart_GivesAnotherMaterial()
        {
            var baseMat = materials.Get(texA, 5, 6, EffectFacing.Camera, false);
            Assert.AreNotSame(baseMat, materials.Get(texA, 5, 6, EffectFacing.Camera, true), "wrap");
            Assert.AreNotSame(baseMat, materials.Get(texA, 2, 6, EffectFacing.Camera, false), "src");
            Assert.AreNotSame(baseMat, materials.Get(texA, 5, 2, EffectFacing.Camera, false), "dst");
            Assert.AreNotSame(baseMat, materials.Get(texA, 5, 6, EffectFacing.Ground, false), "facing");
            Assert.AreNotSame(baseMat, materials.Get(texB, 5, 6, EffectFacing.Camera, false), "texture");
            Assert.AreEqual(6, materials.Count);
        }

        [Test]
        public void RepeatedGet_AllocatesNothing()
        {
            materials.Get(texA, 5, 6, EffectFacing.Camera, true);
            materials.Get(texB, 2, 2, EffectFacing.Ground, false);
            for (int i = 0; i < 10; i++) materials.Get(texA, 5, 6, EffectFacing.Camera, true); // JIT warm-up
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                materials.Get(texA, 5, 6, EffectFacing.Camera, true);
                materials.Get(texB, 2, 2, EffectFacing.Ground, false);
            }
            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [Test]
        public void NullTexture_UsesPlaceholder()
        {
            Assert.IsNotNull(settings.PlaceholderTexture);
            var viaNull = materials.Get(null, 5, 6, EffectFacing.Camera, false);
            var viaPlaceholder = materials.Get(settings.PlaceholderTexture, 5, 6, EffectFacing.Camera, false);
            Assert.AreSame(viaNull, viaPlaceholder);
            Assert.AreSame(settings.PlaceholderTexture, viaNull.GetTexture("_BaseMap"));
        }

        [Test]
        public void CreatedMaterial_HasExpectedValues()
        {
            var m = materials.Get(texA, 2, 4, EffectFacing.Camera, true, out bool known);
            Assert.IsTrue(known);
            Assert.AreSame(settings.Shader, m.shader);
            Assert.AreSame(texA, m.GetTexture("_BaseMap"));
            Assert.AreEqual((float)BlendMode.One, m.GetFloat("_SrcBlend"));
            Assert.AreEqual((float)BlendMode.OneMinusSrcColor, m.GetFloat("_DstBlend"));
            Assert.AreEqual(0.75f, m.GetFloat("_DepthBias"));
            Assert.IsTrue(m.IsKeywordEnabled("_FACING_CAMERA"));
            Assert.IsTrue(m.IsKeywordEnabled("_UV_REPEAT"));
            Assert.AreEqual((int)RenderQueue.Transparent, m.renderQueue);

            var g = materials.Get(texA, 2, 4, EffectFacing.Ground, false);
            Assert.IsFalse(g.IsKeywordEnabled("_FACING_CAMERA"));
            Assert.IsFalse(g.IsKeywordEnabled("_UV_REPEAT"));
        }

        [Test]
        public void UnknownBlendCode_FallsBackAndReports()
        {
            var m = materials.Get(texA, 99, 6, EffectFacing.Camera, false, out bool known);
            Assert.IsFalse(known);
            Assert.AreEqual((float)BlendMode.SrcAlpha, m.GetFloat("_SrcBlend"));
            Assert.AreEqual((float)BlendMode.OneMinusSrcAlpha, m.GetFloat("_DstBlend"));
            materials.Get(texA, 99, 6, EffectFacing.Camera, false, out known);
            Assert.IsFalse(known, "still reported on a cached lookup");
        }

        [Test]
        public void KeyedTexelsAreDiscarded_ForPairsThatIgnoreTextureAlpha_FR3_D37()
        {
            // One / Zero (2, 1, angelus.str) and the other non-SrcAlpha sources would draw the RGB under a keyed texel.
            Assert.AreEqual(EffectMaterials.KeyedCutoff, materials.Get(texA, 2, 1, EffectFacing.Camera, false).GetFloat("_Cutoff"));
            Assert.AreEqual(EffectMaterials.KeyedCutoff, materials.Get(texA, 2, 2, EffectFacing.Camera, false).GetFloat("_Cutoff"));
            // SrcAlpha pairs (5 / 7 additive, 5 / 6) already weigh by alpha: no cutoff, soft alpha stays soft.
            Assert.AreEqual(0f, materials.Get(texA, 5, 7, EffectFacing.Camera, false).GetFloat("_Cutoff"));
            Assert.AreEqual(0f, materials.Get(texA, 5, 6, EffectFacing.Camera, false).GetFloat("_Cutoff"));
            // An unknown code falls back to SrcAlpha / OneMinusSrcAlpha.
            Assert.AreEqual(0f, materials.Get(texA, 99, 6, EffectFacing.Camera, false).GetFloat("_Cutoff"));
        }

        [Test]
        public void Clear_DestroysMaterials()
        {
            var m = materials.Get(texA, 5, 6, EffectFacing.Camera, false);
            materials.Clear();
            Assert.IsTrue(m == null);
            Assert.AreEqual(0, materials.Count);
        }
    }
}
