using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Effects;
using Uninarok.RoImport.Core.Effects;

namespace Uninarok.RoImport.Tests.Effects
{
    /// <summary>
    /// SPEC-0012 FR-2, FR-10 (PLAN-0012 4.2, 6): <see cref="RoStrCompiler"/> flattens a <see cref="RoStr"/> without converting
    /// any value. Inputs are synthetic <see cref="RoStr"/> objects built in code (RO files are read only through GRF).
    /// </summary>
    public class RoStrCompilerTests
    {
        // marker spreads distinct values over every field so a dropped or swapped field shows.
        static RoStrKey Key(int frame, int type, int src, int dst, float marker)
        {
            return new RoStrKey(frame, type, new Vector2(marker + 1, marker + 2),
                new Vector2(marker + 3, marker + 4), new Vector2(marker + 5, marker + 6),
                new Vector2(marker + 7, marker + 8), new Vector2(marker + 9, marker + 10),
                new Vector2(marker + 11, marker + 12), new Vector2(marker + 13, marker + 14),
                new Vector2(marker + 15, marker + 16), new Vector2(marker + 17, marker + 18),
                marker + 19, 2, marker + 20, marker + 21, new Vector4(marker + 22, marker + 23, marker + 24, marker + 25),
                src, dst, 7);
        }

        static RoStr Str(params RoStrLayer[] layers) =>
            new RoStr { Version = 0x94, Fps = 60f, FrameCount = 40, Layers = layers };

        static RoStrLayer Layer(string[] textures, params RoStrKey[] keys) =>
            new RoStrLayer { TextureNames = textures, Keys = keys };

        [Test]
        public void Compile_KeepsHeader()
        {
            RoCompiledStr c = RoStrCompiler.Compile(Str(Layer(new[] { "a.bmp" }, Key(0, 0, 5, 6, 0))));
            Assert.AreEqual(60f, c.Fps);
            Assert.AreEqual(40, c.FrameCount);
        }

        [Test]
        public void Compile_KeepsEveryKeyFieldRaw()
        {
            RoStrKey k = Key(3, 1, 5, 6, 100f);
            RoCompiledStr c = RoStrCompiler.Compile(Str(Layer(new[] { "a.bmp" }, k)));
            Assert.AreEqual(1, c.Keys.Length);
            EffectAsset.KeyData d = c.Keys[0];
            Assert.AreEqual(k.Frame, d.Frame);
            Assert.AreEqual(k.Type, d.Type);
            Assert.AreEqual(k.AniType, d.AniType);
            Assert.AreEqual(k.SrcBlend, d.SrcBlend);
            Assert.AreEqual(k.DstBlend, d.DstBlend);
            Assert.AreEqual(k.MtPreset, d.MtPreset);
            Assert.AreEqual(k.Position, d.Position);
            Assert.AreEqual(k.Uv0, d.Uv0);
            Assert.AreEqual(k.Uv1, d.Uv1);
            Assert.AreEqual(k.Uv2, d.Uv2);
            Assert.AreEqual(k.Uv3, d.Uv3);
            Assert.AreEqual(k.Xy0, d.Xy0);
            Assert.AreEqual(k.Xy1, d.Xy1);
            Assert.AreEqual(k.Xy2, d.Xy2);
            Assert.AreEqual(k.Xy3, d.Xy3);
            Assert.AreEqual(k.AniFrame, d.AniFrame);
            Assert.AreEqual(k.Delay, d.Delay);
            Assert.AreEqual(k.Angle, d.Angle);   // raw file units, not degrees
            Assert.AreEqual(k.Color, d.Color);   // raw 0..255, not divided
        }

        [Test]
        public void Compile_KeepsEachKeysOwnSourceAndDestinationBlend()
        {
            // One layer whose keys carry different blend pairs (FR-2: blend per key, not per layer).
            RoCompiledStr c = RoStrCompiler.Compile(Str(Layer(new[] { "a.bmp" },
                Key(0, 0, 5, 6, 0), Key(1, 1, 5, 6, 10), Key(2, 0, 5, 2, 20), Key(3, 1, 2, 1, 30))));
            CollectionAssert.AreEqual(new[] { 5, 5, 5, 2 }, c.Keys.Select(k => (int)k.SrcBlend).ToArray());
            CollectionAssert.AreEqual(new[] { 6, 6, 2, 1 }, c.Keys.Select(k => (int)k.DstBlend).ToArray());
        }

        [Test]
        public void Compile_KeepsFileOrderAcrossLayers()
        {
            RoCompiledStr c = RoStrCompiler.Compile(Str(
                Layer(new[] { "a.bmp" }, Key(0, 0, 5, 6, 0), Key(4, 0, 5, 6, 1)),
                Layer(new[] { "b.bmp" }, Key(1, 0, 5, 6, 2), Key(2, 0, 5, 6, 3), Key(9, 0, 5, 6, 4))));
            Assert.AreEqual(2, c.Layers.Length);
            Assert.AreEqual(5, c.Keys.Length);
            CollectionAssert.AreEqual(new[] { 0, 4, 1, 2, 9 }, c.Keys.Select(k => k.Frame).ToArray());
        }

        [Test]
        public void Compile_KeyRangesPerLayer()
        {
            RoCompiledStr c = RoStrCompiler.Compile(Str(
                Layer(new[] { "a.bmp" }, Key(0, 0, 5, 6, 0), Key(4, 0, 5, 6, 1)),
                Layer(new[] { "b.bmp" }),
                Layer(new[] { "c.bmp" }, Key(1, 0, 5, 6, 2), Key(2, 0, 5, 6, 3), Key(9, 0, 5, 6, 4))));
            Assert.AreEqual(3, c.Layers.Length);
            Assert.AreEqual(0, c.Layers[0].FirstKey);
            Assert.AreEqual(2, c.Layers[0].KeyCount);
            Assert.AreEqual(2, c.Layers[1].FirstKey);
            Assert.AreEqual(0, c.Layers[1].KeyCount);
            Assert.AreEqual(2, c.Layers[2].FirstKey);
            Assert.AreEqual(3, c.Layers[2].KeyCount);
        }

        [Test]
        public void Compile_TextureRangesAndNamesAsTheFileNamesThem()
        {
            RoCompiledStr c = RoStrCompiler.Compile(Str(
                Layer(new string[0], Key(0, 0, 5, 6, 0)),
                Layer(new[] { "Ring.BMP", "ring2.tga" }, Key(0, 0, 5, 6, 1)),
                Layer(new[] { "x.bmp" }, Key(0, 0, 5, 6, 2))));
            CollectionAssert.AreEqual(new[] { "Ring.BMP", "ring2.tga", "x.bmp" }, c.TextureNames);
            Assert.AreEqual(0, c.Layers[0].FirstTexture);
            Assert.AreEqual(0, c.Layers[0].TextureCount);
            Assert.AreEqual(0, c.Layers[1].FirstTexture);
            Assert.AreEqual(2, c.Layers[1].TextureCount);
            Assert.AreEqual(2, c.Layers[2].FirstTexture);
            Assert.AreEqual(1, c.Layers[2].TextureCount);
        }

        [Test]
        public void Compile_SortsKeysByFrame_StableForEqualFrames()
        {
            // Frames out of order; the two frame-3 keys (markers 1 then 2) keep their relative order. Delay = marker + 20.
            RoCompiledStr c = RoStrCompiler.Compile(Str(Layer(new[] { "a.bmp" },
                Key(5, 0, 5, 6, 0), Key(3, 0, 5, 6, 1), Key(3, 1, 5, 6, 2), Key(0, 0, 5, 6, 3))));
            CollectionAssert.AreEqual(new[] { 0, 3, 3, 5 }, c.Keys.Select(k => k.Frame).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 0 }, c.Keys.Select(k => (int)k.Type).ToArray());
            Assert.AreEqual(21f, c.Keys[1].Delay);
            Assert.AreEqual(22f, c.Keys[2].Delay);
        }

        [Test]
        public void Compile_SortIsPerLayer()
        {
            RoCompiledStr c = RoStrCompiler.Compile(Str(
                Layer(new[] { "a.bmp" }, Key(9, 0, 5, 6, 0), Key(1, 0, 5, 6, 1)),
                Layer(new[] { "b.bmp" }, Key(8, 0, 5, 6, 2), Key(2, 0, 5, 6, 3))));
            CollectionAssert.AreEqual(new[] { 1, 9, 2, 8 }, c.Keys.Select(k => k.Frame).ToArray());
            Assert.AreEqual(0, c.Layers[0].FirstKey);
            Assert.AreEqual(2, c.Layers[1].FirstKey);
        }

        // Plan 4.2 / FR-2: the asset holds no play data. Every field of EffectAsset is listed; a new field must be added here
        // on purpose, so a forbidden one (asset-level start delay, duplicate, wav/sound, attachment, id) fails.
        // KeyData.Delay is the STR file's per-key animation delay and is allowed.
        static readonly string[] AllowedAssetFields =
        {
            "Path", "Kind", "Fps", "FrameCount", "Layers", "Keys", "TextureNames", "Textures", "Authored", "Length",
            "SpriteName", "SpriteAction", "Model", "Doubtful"
        };

        static readonly string[] ForbiddenNameParts = { "delay", "duplicate", "wav", "sound", "attach" };

        [Test]
        public void EffectAsset_HasNoPlayDataFields()
        {
            string[] fields = typeof(EffectAsset)
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(f => f.Name).ToArray();
            CollectionAssert.AreEquivalent(AllowedAssetFields, fields, "EffectAsset fields differ from the allowed list");
            foreach (string f in fields)
            {
                string lower = f.ToLowerInvariant();
                foreach (string bad in ForbiddenNameParts)
                    Assert.IsFalse(lower.Contains(bad), $"EffectAsset.{f} looks like play data ('{bad}')");
                Assert.AreNotEqual("id", lower, $"EffectAsset.{f} looks like an id field");
            }
        }

        [Test]
        public void KeyData_HasPerKeyDelay()
        {
            FieldInfo delay = typeof(EffectAsset.KeyData).GetField("Delay");
            Assert.IsNotNull(delay, "KeyData.Delay (the file's per-key animation delay) must stay");
            Assert.AreEqual(typeof(float), delay.FieldType);
        }
    }
}
