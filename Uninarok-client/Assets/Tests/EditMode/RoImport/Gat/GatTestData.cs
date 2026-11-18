using System.IO;
using NUnit.Framework;
using Uninarok.RoImport.Editor.Ui;

namespace Uninarok.RoImport.Tests.Gat
{
    /// <summary>
    /// Access to the real <c>.gat</c> files for tests marked <c>NeedsData</c> (PLAN-0005 §6). When the data folder or a
    /// file is absent the test is ignored with a message, never failed. Map files sit at the data root (PLAN-0005 D9).
    /// </summary>
    public static class GatTestData
    {
        /// <summary>The bytes of <c>&lt;map&gt;.gat</c>, or an ignored test when the data is not there.</summary>
        public static byte[] MapBytes(string map)
        {
            string root = RoSourceLocation.Resolve();
            if (!Directory.Exists(root)) Assert.Ignore($"source data folder not found: {root}");
            string file = Path.Combine(root, map + ".gat");
            if (!File.Exists(file)) Assert.Ignore($"source file not found: {map}.gat");
            return File.ReadAllBytes(file);
        }
    }
}
