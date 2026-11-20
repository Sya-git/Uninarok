using System.Collections.Generic;
using NUnit.Framework;
using Uninarok.RoImport.Core.Catalog.Lua;

namespace Uninarok.RoImport.Tests.Catalog
{
    // TASKS-0008 T2.1 (FR-1 to FR-7; PLAN-0008 4.1): the Lua data subset of the client's lub tables.
    public class RoLuaReaderTests
    {
        static RoLuaChunk Read(string lua, RoLuaOptions options = null, IReadOnlyDictionary<string, RoLuaValue> imports = null)
        {
            bool ok = RoLuaReader.TryRead(lua, options, imports, out var chunk, out string reason);
            Assert.That(ok, Is.True, reason);
            return chunk;
        }

        static RoLuaTable Table(RoLuaChunk chunk, string name)
        {
            Assert.That(chunk.TryGetTable(name, out var t), Is.True, $"global table {name}");
            return t;
        }

        static double Number(RoLuaValue v)
        {
            Assert.That(v.TryGetNumber(out double d), Is.True, $"a number, got {v?.Kind}");
            return d;
        }

        static string Text(RoLuaValue v)
        {
            Assert.That(v.TryGetString(out string s), Is.True, $"a string, got {v?.Kind}");
            return s;
        }

        [Test]
        public void ReadsNamedFieldsOfATable()
        {
            var t = Table(Read("JTtbl = {\n\tJT_NOVICE = 0,\n\tJT_KNIGHT = 7,\n\tNAME = \"x\"\n}"), "JTtbl");
            Assert.That(Number(t.Fields["JT_NOVICE"]), Is.EqualTo(0));
            Assert.That(Number(t.Fields["JT_KNIGHT"]), Is.EqualTo(7));
            Assert.That(Text(t.Fields["NAME"]), Is.EqualTo("x"));
        }

        [Test]
        public void ReadsNestedTablesAndPositionalItems()
        {
            var t = Table(Read("T = { { 1, 2 }, x = { y = 3 }, \"z\" }"), "T");
            Assert.That(t.Items.Count, Is.EqualTo(2));
            var first = (RoLuaTable)t.Items[0];
            Assert.That(Number(first.Items[1]), Is.EqualTo(2));
            Assert.That(Text(t.Items[1]), Is.EqualTo("z"));
            Assert.That(Number(((RoLuaTable)t.Fields["x"]).Fields["y"]), Is.EqualTo(3));
            Assert.That(t.TryGet(2, out var second) && Text(second) == "z", Is.True, "positional item 2 is key 2");
        }

        [Test]
        public void ReadsBracketKeys()
        {
            var chunk = Read("ids = { A = 5 }\nnames = { [ids.A] = \"five\", [501] = { k = 1 }, [\"str key\"] = 2, [-3] = 4 }");
            var names = Table(chunk, "names");
            Assert.That(Text(names.Indexed[5]), Is.EqualTo("five"));
            Assert.That(Number(((RoLuaTable)names.Indexed[501]).Fields["k"]), Is.EqualTo(1));
            Assert.That(Number(names.Fields["str key"]), Is.EqualTo(2));
            Assert.That(Number(names.Indexed[-3]), Is.EqualTo(4));
        }

        [Test]
        public void ReadsNumbers()
        {
            var t = Table(Read("T = { -1, 1.2, -0.5, 0x10, 1e3, 2.5E-1, - 7 }"), "T");
            double[] expected = { -1, 1.2, -0.5, 16, 1000, 0.25, -7 };
            for (int i = 0; i < expected.Length; i++) Assert.That(Number(t.Items[i]), Is.EqualTo(expected[i]), $"item {i}");
        }

        [Test]
        public void ReadsStringEscapes()
        {
            var t = Table(Read("T = { \"a\\\"b\\\\c\\n\\t\\65\", 'single \\'q\\'', \"\\r\\a\\0\", \"천사날개\" }"), "T");
            Assert.That(Text(t.Items[0]), Is.EqualTo("a\"b\\c\n\tA"));
            Assert.That(Text(t.Items[1]), Is.EqualTo("single 'q'"));
            Assert.That(Text(t.Items[2]), Is.EqualTo("\r\a\0"));
            Assert.That(Text(t.Items[3]), Is.EqualTo("천사날개"));
        }

        [Test]
        public void ReadsBooleansAndDropsNil()
        {
            var t = Table(Read("T = { a = true, b = false, c = nil }\nG = nil"), "T");
            Assert.That(((RoLuaBoolean)t.Fields["a"]).Value, Is.True);
            Assert.That(((RoLuaBoolean)t.Fields["b"]).Value, Is.False);
            Assert.That(t.Fields.ContainsKey("c"), Is.False);
        }

        [Test]
        public void ResolvesReferencesToImports()
        {
            var jobtbl = new RoLuaTable();
            jobtbl.Set("JT_PORING", new RoLuaNumber(1002));
            var imports = new Dictionary<string, RoLuaValue> { ["jobtbl"] = jobtbl };
            var chunk = Read("JobNameTable = {\n\t[jobtbl.JT_PORING] = \"PORING\"\n}", imports: imports);
            Assert.That(Text(Table(chunk, "JobNameTable").Indexed[1002]), Is.EqualTo("PORING"));
            Assert.That(chunk.Globals.ContainsKey("jobtbl"), Is.False, "imports are not globals of the chunk");
        }

        [Test]
        public void ResolvesEarlierGlobalsAndDottedAssignments()
        {
            var chunk = Read("EFFECT = {}\nEFFECT.EF_HIT = 3\nEFFECT.EF_NONE = -1\ntbl = {\n\t[2603] = { MonsterSize = 1.2, MonsterEff = { EFFECT.EF_HIT } }\n}\nCOPY = EFFECT.EF_HIT");
            Assert.That(Number(Table(chunk, "EFFECT").Fields["EF_NONE"]), Is.EqualTo(-1));
            var entry = (RoLuaTable)Table(chunk, "tbl").Indexed[2603];
            Assert.That(Number(entry.Fields["MonsterSize"]), Is.EqualTo(1.2));
            Assert.That(Number(((RoLuaTable)entry.Fields["MonsterEff"]).Items[0]), Is.EqualTo(3));
            Assert.That(Number(chunk.Globals["COPY"]), Is.EqualTo(3));
        }

        [Test]
        public void ReadsTopLevelGlobalsOfNumbers()
        {
            var chunk = Read("ET_SURPRISE = 0\nET_QUESTION = 1\nEMOTION_ORDERLIST = { ET_SURPRISE, ET_QUESTION }");
            Assert.That(Number(chunk.Globals["ET_QUESTION"]), Is.EqualTo(1));
            Assert.That(Number(Table(chunk, "EMOTION_ORDERLIST").Items[1]), Is.EqualTo(1));
        }

        [Test]
        public void SkipsFunctionsAndCalls()
        {
            const string lua =
                "main = function()\n" +
                "\tfor ItemID, DESC in pairs(tbl) do\n" +
                "\t\tif not(result) then\n\t\t\treturn false, msg\n\t\tend\n" +
                "\t\twhile x do x = nil end\n" +
                "\t\trepeat y = 1 until y\n" +
                "\tend\n" +
                "\treturn true, \"good end\"\n" +
                "end\n" +
                "function AddItem(a, b)\n\tlocal t = { a, b }\n\tdo end\nend\n" +
                "T = { A = 1 }\n" +
                "InsertEmotionListTable(ET_YUT2, -1, 87)\n" +
                "main()\n" +
                "AddItem(1, { 2, \")\" })\n" +
                "U = { B = 2 }\n";
            var chunk = Read(lua);
            Assert.That(Number(Table(chunk, "T").Fields["A"]), Is.EqualTo(1));
            Assert.That(Number(Table(chunk, "U").Fields["B"]), Is.EqualTo(2));
            Assert.That(chunk.Globals.ContainsKey("main"), Is.False, "a function is not data");
            Assert.That(chunk.Globals.ContainsKey("AddItem"), Is.False);
        }

        [Test]
        public void SkipFieldDropsTheFieldWhileParsing()
        {
            var options = new RoLuaOptions { SkipField = n => n.EndsWith("DescriptionName") || n == "identifiedDisplayName" };
            var chunk = Read(
                "tbl = {\n\t[501] = {\n" +
                "\t\tidentifiedDisplayName = \"Poção } Vermelha\",\n" +
                "\t\tidentifiedResourceName = \"빨간포션\",\n" +
                "\t\tidentifiedDescriptionName = { \"a } b\", { nested = \"{\" }, 'c' },\n" +
                "\t\tClassNum = 0\n\t}\n}", options);
            var item = (RoLuaTable)Table(chunk, "tbl").Indexed[501];
            Assert.That(item.Fields.Keys, Is.EquivalentTo(new[] { "identifiedResourceName", "ClassNum" }));
            Assert.That(Text(item.Fields["identifiedResourceName"]), Is.EqualTo("빨간포션"));
        }

        [Test]
        public void AcceptsCommentsSemicolonsAndTrailingSeparators()
        {
            var t = Table(Read("-- header\nT = { --[[ block\n comment ]] A = 1; B = 2, -- tail\n C = 3, }\n--[==[ long\n]==]"), "T");
            Assert.That(t.Fields.Count, Is.EqualTo(3));
        }

        [Test]
        public void ReadsEmptyText()
        {
            Assert.That(Read("").Globals.Count, Is.EqualTo(0));
        }

        [Test]
        public void RejectsAnUnknownConstructWithItsLine()
        {
            bool ok = RoLuaReader.TryRead("T = {\n\tA = 1,\n\tB = 1 + 2\n}", null, null, out _, out string reason);
            Assert.That(ok, Is.False);
            Assert.That(reason, Does.Contain("line 3"));
        }

        [Test]
        public void RejectsAnUnknownName()
        {
            bool ok = RoLuaReader.TryRead("T = {\n\t[jobtbl.JT_PORING] = \"PORING\"\n}", null, null, out _, out string reason);
            Assert.That(ok, Is.False);
            Assert.That(reason, Does.Contain("jobtbl").And.Contain("line 2"));
        }

        [Test]
        public void ABareUnknownNameIsNilAndLeavesAHole()
        {
            // D17: monster_size_effect_new.lub line 3253 has MonsterEff = { EF_LEVEL99_RED }, which the client reads as nil.
            var t = Table(Read("T = { a = UNKNOWN, list = { UNKNOWN }, gap = { 1, nil, 3 } }"), "T");
            Assert.That(t.Fields.ContainsKey("a"), Is.False);
            Assert.That(((RoLuaTable)t.Fields["list"]).Items.Count, Is.EqualTo(0));
            var gap = (RoLuaTable)t.Fields["gap"];
            Assert.That(gap.TryGet(2, out _), Is.False);
            Assert.That(gap.TryGet(3, out var third) && Number(third) == 3, Is.True, "the value after a hole keeps its position");
        }

        [Test]
        public void RejectsANilKey()
        {
            var jobtbl = new RoLuaTable();
            var imports = new Dictionary<string, RoLuaValue> { ["jobtbl"] = jobtbl };
            bool ok = RoLuaReader.TryRead("T = { [jobtbl.JT_MISSING] = 1 }", null, imports, out _, out string reason);
            Assert.That(ok, Is.False);
            Assert.That(reason, Does.Contain("line 1"));
        }

        [Test]
        public void RejectsLongStringsAndUnclosedInput()
        {
            Assert.That(RoLuaReader.TryRead("T = { name = [[x]] }", null, null, out _, out _), Is.False, "long string");
            Assert.That(RoLuaReader.TryRead("T = { A = 1", null, null, out _, out _), Is.False, "unclosed table");
            Assert.That(RoLuaReader.TryRead("T = { A = \"x }", null, null, out _, out _), Is.False, "unclosed string");
            Assert.That(RoLuaReader.TryRead("f = function() if x then", null, null, out _, out _), Is.False, "unclosed function");
            Assert.That(RoLuaReader.TryRead(null, null, null, out _, out _), Is.False, "null text");
        }
    }
}
