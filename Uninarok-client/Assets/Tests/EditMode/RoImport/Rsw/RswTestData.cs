using System.IO;
using NUnit.Framework;
using Uninarok.RoImport.Editor.Ui;

namespace Uninarok.RoImport.Tests.Rsw
{
    /// <summary>
    /// Access to the real <c>.rsw</c> files for tests marked <c>NeedsData</c> (PLAN-0006 §6). When the data folder or a
    /// file is absent the test is ignored with a message, never failed. Map files sit at the data root (TASKS-0006 T0.1).
    /// </summary>
    public static class RswTestData
    {
        /// <summary>The bytes of <c>&lt;map&gt;.rsw</c>, or an ignored test when the data is not there.</summary>
        public static byte[] MapBytes(string map)
        {
            string root = RoSourceLocation.Resolve();
            if (!Directory.Exists(root)) Assert.Ignore($"source data folder not found: {root}");
            string file = Path.Combine(root, map + ".rsw");
            if (!File.Exists(file)) Assert.Ignore($"source file not found: {map}.rsw");
            return File.ReadAllBytes(file);
        }
    }
}
