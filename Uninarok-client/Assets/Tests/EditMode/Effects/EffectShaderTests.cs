using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Uninarok.Effects.Tests
{
    public sealed class EffectShaderTests
    {
        const string SettingsPath = "Assets/Settings/Effects/EffectSettings.asset";

        static Shader Load() => AssetDatabase.LoadAssetAtPath<EffectSettings>(SettingsPath).Shader;

        [Test] public void Settings_shader_is_RoEffect_and_supported() // FR-3
        {
            Shader shader = Load();
            Assert.IsNotNull(shader);
            Assert.AreEqual("Uninarok/RoEffect", shader.name);
            Assert.IsTrue(shader.isSupported);
            Assert.AreEqual(0, ShaderUtil.GetShaderMessageCount(shader), "shader compile messages");
        }

        [Test] public void Shader_has_the_two_keywords() // FR-4
        {
            string[] names = Load().keywordSpace.keywordNames.ToArray();
            CollectionAssert.Contains(names, "_FACING_CAMERA");
            CollectionAssert.Contains(names, "_UV_REPEAT");
        }

        [Test] public void Shader_has_blend_and_depth_bias_properties() // FR-3, FR-9
        {
            Shader shader = Load();
            Assert.GreaterOrEqual(shader.FindPropertyIndex("_SrcBlend"), 0);
            Assert.GreaterOrEqual(shader.FindPropertyIndex("_DstBlend"), 0);
            Assert.GreaterOrEqual(shader.FindPropertyIndex("_DepthBias"), 0);
        }
    }
}
