using System.Linq;
using System.Text;
using NUnit.Framework;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Tests.Catalog
{
    // TASKS-0008 T2.3 (FR-1 to FR-7; PLAN-0008 4.1, T0.3 mapping table): the GRF adapter for lub files.
    public class RoLubReaderTests
    {
        static string Read(byte[] bytes, RoLubEncoding encoding, string path = "x.lub")
        {
            bool ok = RoLubReader.TryRead(RoPath.Parse(path), bytes, encoding, out string lua, out string reason);
            Assert.That(ok, Is.True, reason);
            return lua;
        }

        [Test, Category(CatalogTestFiles.Category)]
        public void DecompilesACp949Table()
        {
            string path = CatalogTestFiles.Tables + "datainfo/accname.lub";
            string lua = Read(CatalogTestFiles.Data(path), RoLubEncoding.Cp949, path);
            Assert.That(lua.Length, Is.EqualTo(172864), "length recorded by T0.3");
            Assert.That(lua, Does.Contain("[ACCESSORY_IDs.ACCESSORY_GOGGLES] = \"_고글\""));
        }

        [Test, Category(CatalogTestFiles.Category)]
        public void DecompilesAUtf8ItemTable()
        {
            string lua = Read(CatalogTestFiles.System("iteminfo_new.lub"), RoLubEncoding.Utf8, "iteminfo_new.lub");
            Assert.That(lua.Length, Is.EqualTo(14179615), "length recorded by T0.3");
            Assert.That(lua, Does.Contain("identifiedResourceName = \"빨간포션\""));
        }

        [Test, Category(CatalogTestFiles.Category)]
        public void ReadsAPlainTextFileInCp949()
        {
            string path = CatalogTestFiles.Tables + "quest/localquest/local_dewataquest_list.lub";
            byte[] bytes = CatalogTestFiles.Data(path);
            string lua = Read(bytes, RoLubEncoding.Cp949, path);
            Assert.That(lua, Does.Contain("데와타"));
            Assert.That(RoLubReader.TryRead(RoPath.Parse(path), bytes, RoLubEncoding.Utf8, out _, out _), Is.False,
                "CP949 bytes are not valid UTF-8");
        }

        [Test]
        public void ReadsPlainTextInUtf8()
        {
            Assert.That(Read(Encoding.UTF8.GetBytes("T = { \"천사날개\" }\n"), RoLubEncoding.Utf8), Does.Contain("천사날개"));
        }

        [Test]
        public void RejectsEmptyAndGarbage()
        {
            var path = RoPath.Parse("x.lub");
            Assert.That(RoLubReader.TryRead(path, new byte[0], RoLubEncoding.Cp949, out _, out string empty), Is.False);
            Assert.That(empty, Is.Not.Empty);
            Assert.That(RoLubReader.TryRead(path, null, RoLubEncoding.Cp949, out _, out _), Is.False);

            byte[] garbage = Enumerable.Range(0, 256).Select(i => (byte)(i * 37)).ToArray();
            Assert.That(RoLubReader.TryRead(path, garbage, RoLubEncoding.Cp949, out _, out _), Is.False, "garbage");
            Assert.That(RoLubReader.TryRead(path, garbage, RoLubEncoding.Utf8, out _, out _), Is.False, "garbage as UTF-8");

            byte[] bytecodeHeader = { 0x1B, (byte)'L', (byte)'u', (byte)'a', 0x51, 0, 1, 4, 4, 4, 8, 0 };
            Assert.That(RoLubReader.TryRead(path, bytecodeHeader, RoLubEncoding.Cp949, out _, out string cut), Is.False, "header only");
            Assert.That(cut, Is.Not.Empty);
        }

        [Test, Category(CatalogTestFiles.Category)]
        public void RejectsATruncatedBytecodeFile()
        {
            byte[] bytes = CatalogTestFiles.Data(CatalogTestFiles.Tables + "datainfo/accname.lub");
            byte[] cut = bytes.Take(100).ToArray();
            Assert.That(RoLubReader.TryRead(RoPath.Parse("accname.lub"), cut, RoLubEncoding.Cp949, out _, out string reason), Is.False);
            Assert.That(reason, Does.Contain("GRF"));
        }
    }
}
