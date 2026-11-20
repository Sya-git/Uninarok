using System.IO;
using NUnit.Framework;
using Uninarok.RoImport.Editor.Ui;

namespace Uninarok.RoImport.Tests.Catalog
{
    /// <summary>
    /// Access to the real <c>data/</c> and <c>System/</c> files for catalog tests marked <c>NeedsData</c> (PLAN-0008 §6).
    /// <c>System</c> is the folder next to the data root. When a folder or file is absent the test is ignored, never failed.
    /// </summary>
    public static class CatalogTestFiles
    {
        public const string Category = "NeedsData";
        public const string Tables = "luafiles514/lua files/";

        public static string DataRoot
        {
            get
            {
                string root = RoSourceLocation.Resolve();
                if (!Directory.Exists(root)) Assert.Ignore($"source data folder not found: {root}");
                return root;
            }
        }

        public static string SystemRoot
        {
            get
            {
                string system = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(DataRoot).TrimEnd('/', '\\')) ?? ".", "System");
                if (!Directory.Exists(system)) Assert.Ignore($"System folder not found: {system}");
                return system;
            }
        }

        /// <summary>The bytes of <c>data/&lt;relative&gt;</c>.</summary>
        public static byte[] Data(string relative) => Read(DataRoot, relative);

        /// <summary>The bytes of <c>System/&lt;relative&gt;</c>.</summary>
        public static byte[] System(string relative) => Read(SystemRoot, relative);

        static byte[] Read(string root, string relative)
        {
            string file = Path.Combine(root, relative);
            if (!File.Exists(file)) Assert.Ignore($"source file not found: {relative}");
            return File.ReadAllBytes(file);
        }
    }
}
