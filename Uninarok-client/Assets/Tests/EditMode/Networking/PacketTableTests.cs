using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Networking.Core.Protocol;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-18, FR-19; plan 4.8.
    public class PacketTableTests
    {
        const string FixtureName = "hercules-packetlen-20220406.json";

        static string TestFixturePath => Path.Combine(Application.dataPath, "Tests/EditMode/Networking/Fixtures", FixtureName);
        static string DocsFixturePath => Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/specs/0001-networking-core/fixtures", FixtureName));

        static SortedDictionary<ushort, int> LoadFixture()
        {
            var map = new SortedDictionary<ushort, int>();
            foreach (Match m in Regex.Matches(File.ReadAllText(TestFixturePath), "\"(0x[0-9A-Fa-f]{4})\"\\s*:\\s*(-?\\d+)"))
                map.Add(System.Convert.ToUInt16(m.Groups[1].Value.Substring(2), 16), int.Parse(m.Groups[2].Value));
            return map;
        }

        [Test]
        public void TestFixture_IsTheSameFileAsTheDocsFixture()
        {
            // The tests use a copy; when the target version changes the docs fixture is re-extracted first, then copied.
            Assert.AreEqual(File.ReadAllText(DocsFixturePath), File.ReadAllText(TestFixturePath));
        }

        [Test]
        public void Fixture_HasNoInvalidValues()
        {
            var fixture = LoadFixture();
            Assert.Greater(fixture.Count, 1000);
            foreach (var kv in fixture)
            {
                Assert.AreNotEqual(0, kv.Key, "ID 0 is never in the table");
                Assert.IsTrue(kv.Value == -1 || kv.Value >= 2, $"0x{kv.Key:X4} has invalid length {kv.Value}");
            }
        }

        [Test]
        public void GeneratedTable_EqualsTheFixture_ForEveryId()
        {
            var fixture = LoadFixture();
            var table = HerculesPacketTable.Instance;
            Assert.AreEqual(fixture.Count, HerculesPacketTable.PacketCount);

            for (int id = 0; id <= ushort.MaxValue; id++)
            {
                int expected = fixture.TryGetValue((ushort)id, out int len) ? len : -2;
                Assert.AreEqual(expected, table.GetLength((ushort)id), $"length of 0x{id:X4}");
            }
        }

        [Test]
        public void GeneratedTable_NeverReturnsAnInvalidValue()
        {
            var table = HerculesPacketTable.Instance;
            for (int id = 0; id <= ushort.MaxValue; id++)
            {
                int len = table.GetLength((ushort)id);
                Assert.IsTrue(len == -2 || len == -1 || len >= 2, $"0x{id:X4} -> {len}");
            }
        }

        [Test]
        public void Table_KnownButUnhandledIds_AreNotUnknown()
        {
            // FR-19: an info packet the client does not implement yet is framed by its length, not a fault.
            Assert.AreNotEqual(-2, HerculesPacketTable.Instance.GetLength(0x0ADE));
        }
    }
}
