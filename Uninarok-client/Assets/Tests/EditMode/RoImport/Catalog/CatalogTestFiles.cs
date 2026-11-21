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

        /// <summary>The repository's Hercules item database (PLAN-0008 D21); the test is ignored without it.</summary>
        public static string ItemDbPath
        {
            get
            {
                string path = Path.GetFullPath(Uninarok.RoImport.Editor.Catalog.RoImportSettings.DefaultItemDbPath);
                if (!File.Exists(path)) Assert.Ignore($"Hercules item_db.conf not found: {path}");
                return path;
            }
        }

        /// <summary>The bytes of <c>data/&lt;relative&gt;</c>.</summary>
        public static byte[] Data(string relative) => Read(DataRoot, relative);

        /// <summary>The bytes of <c>System/&lt;relative&gt;</c>.</summary>
        public static byte[] System(string relative) => Read(SystemRoot, relative);

        /// <summary>The decompiled text of <c>data/luafiles514/lua files/&lt;relative&gt;</c> (CP949).</summary>
        public static string TableLua(string relative) => Lua(Data(Tables + relative), Uninarok.RoImport.Core.Formats.RoLubEncoding.Cp949, relative);

        /// <summary>The decompiled text of <c>System/&lt;relative&gt;</c>.</summary>
        public static string SystemLua(string relative, Uninarok.RoImport.Core.Formats.RoLubEncoding encoding) => Lua(System(relative), encoding, relative);

        static string Lua(byte[] bytes, Uninarok.RoImport.Core.Formats.RoLubEncoding encoding, string relative)
        {
            bool ok = Uninarok.RoImport.Core.Formats.RoLubReader.TryRead(Uninarok.RoImport.Core.Paths.RoPath.Parse(relative), bytes, encoding,
                out string lua, out string reason);
            Assert.That(ok, Is.True, reason);
            return lua;
        }

        static byte[] Read(string root, string relative)
        {
            string file = Path.Combine(root, relative);
            if (!File.Exists(file)) Assert.Ignore($"source file not found: {relative}");
            return File.ReadAllBytes(file);
        }
    }
}
