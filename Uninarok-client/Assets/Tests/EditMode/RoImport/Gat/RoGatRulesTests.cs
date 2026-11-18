using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Map;
using Uninarok.RoImport.Core.Gat;

namespace Uninarok.RoImport.Tests.Gat
{
    // TASKS-0005 T3.1 / T3.2 (FR-1): the cell table of PLAN-0005 4.2 (Hercules map_gat2cell, D2), pinned here and against
    // docs/data/GatCellType.md.
    public class RoGatRulesTests
    {
        const MapCellFlags W = MapCellFlags.Walkable, S = MapCellFlags.Shootable, A = MapCellFlags.Water;

        [TestCase(0u, MapCellType.Walkable, W | S)]
        [TestCase(1u, MapCellType.Wall, MapCellFlags.None)]
        [TestCase(2u, MapCellType.Type2, W | S)]
        [TestCase(3u, MapCellType.Water, W | S | A)]
        [TestCase(4u, MapCellType.Type4, W | S)]
        [TestCase(5u, MapCellType.Cliff, S)]
        [TestCase(6u, MapCellType.Type6, W | S)]
        [TestCase(7u, MapCellType.Other, MapCellFlags.None)]
        [TestCase(0xFFFFFFFFu, MapCellType.Other, MapCellFlags.None)]   // NoGat, the absent cell
        [TestCase(1000u, MapCellType.Other, MapCellFlags.None)]
        public void RawType_MapsToTheServersTypeAndFlags(uint raw, MapCellType type, MapCellFlags flags)
        {
            Assert.AreEqual(type, RoGatRules.TypeOf(raw));
            Assert.AreEqual(flags, RoGatRules.FlagsOf(RoGatRules.TypeOf(raw)));
        }

        [Test]
        public void Bit31_IsUnrecognized_WithEveryType()
        {
            // the server does not mask bit 31 (GRF's Weird0 to Weird9): the cell gets no flag (D2)
            for (uint t = 0; t <= 9; t++)
            {
                Assert.AreEqual(MapCellType.Other, RoGatRules.TypeOf(0x80000000u | t), $"0x8000000{t}");
                Assert.AreEqual(MapCellFlags.None, RoGatRules.FlagsOf(RoGatRules.TypeOf(0x80000000u | t)));
            }
        }

        [Test]
        public void TheEnums_EqualTheTablesOfTheDataDefinition()
        {
            string doc = File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/data/GatCellType.md")));

            // | `Name` | `value` | walkable | shootable | water | ...
            var rows = Regex.Matches(doc, @"^\| `(\w+)` \| `(\d+)` \| (yes|no) \| (yes|no) \| (yes|no) \|", RegexOptions.Multiline)
                .Cast<Match>().ToArray();
            CollectionAssert.AreEquivalent(Enum.GetNames(typeof(MapCellType)), rows.Select(r => r.Groups[1].Value));
            foreach (var r in rows)
            {
                var type = (MapCellType)Enum.Parse(typeof(MapCellType), r.Groups[1].Value);
                Assert.AreEqual(int.Parse(r.Groups[2].Value), (int)type, r.Value);
                var flags = RoGatRules.FlagsOf(type);
                Assert.AreEqual(r.Groups[3].Value == "yes", (flags & W) != 0, r.Value + " walkable");
                Assert.AreEqual(r.Groups[4].Value == "yes", (flags & S) != 0, r.Value + " shootable");
                Assert.AreEqual(r.Groups[5].Value == "yes", (flags & A) != 0, r.Value + " water");
            }

            // | `Flag` | bit | ...
            var flagRows = Regex.Matches(doc, @"^\| `(\w+)` \| (\d) \|", RegexOptions.Multiline).Cast<Match>().ToArray();
            CollectionAssert.AreEquivalent(Enum.GetNames(typeof(MapCellFlags)).Where(n => n != nameof(MapCellFlags.None)),
                flagRows.Select(r => r.Groups[1].Value));
            foreach (var r in flagRows)
                Assert.AreEqual(1 << int.Parse(r.Groups[2].Value), Convert.ToInt32(Enum.Parse(typeof(MapCellFlags), r.Groups[1].Value)), r.Value);
        }
    }
}
