using System;
using System.IO;
using NUnit.Framework;
using Uninarok.RoImport.Editor.Ui;

namespace Uninarok.RoImport.Tests.Sprites
{
    /// <summary>
    /// Access to the real <c>sprite/</c> and <c>palette/</c> files for tests marked <c>NeedsData</c> (PLAN-0007 §6). When the
    /// data folder or a file is absent the test is ignored with a message, never failed.
    /// </summary>
    public static class SpriteTestFiles
    {
        public const string Category = "NeedsData";

        /// <summary>The bytes of <c>data/&lt;relative&gt;</c> (for example <c>sprite/몬스터/poring.spr</c>).</summary>
        public static byte[] Bytes(string relative)
        {
            string root = RoSourceLocation.Resolve();
            if (!Directory.Exists(root)) Assert.Ignore($"source data folder not found: {root}");
            string file = Path.Combine(root, relative);
            if (!File.Exists(file)) Assert.Ignore($"source file not found: {relative}");
            return File.ReadAllBytes(file);
        }

        public static byte[] Slice(byte[] bytes, int length)
        {
            var copy = new byte[length];
            Array.Copy(bytes, copy, length);
            return copy;
        }
    }
}
