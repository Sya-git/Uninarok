using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Text;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-18: a wrong declared size desynchronizes the whole stream, so every registered packet is checked
    // against the Hercules table (plan 4.8, 6). Passes trivially while no packet is registered.
    public class PacketSizeTests
    {
        private struct Declared
        {
            public Type Type;
            public bool Incoming;
            public ushort Id;
            public int Size;
        }

        // The assembly that holds the packet structs (WireText lives in the same assembly).
        private static IEnumerable<Declared> AllPackets()
        {
            foreach (Type t in typeof(WireText).Assembly.GetTypes())
            {
                var inAttr = t.GetCustomAttribute<InPacketAttribute>();
                if (inAttr != null) yield return new Declared { Type = t, Incoming = true, Id = inAttr.Id, Size = inAttr.Size };
                var outAttr = t.GetCustomAttribute<OutPacketAttribute>();
                if (outAttr != null) yield return new Declared { Type = t, Incoming = false, Id = outAttr.Id, Size = outAttr.Size };
            }
        }

        [Test]
        public void EveryDeclaredSize_EqualsTheHerculesTable()
        {
            foreach (var p in AllPackets())
                Assert.AreEqual(HerculesPacketTable.Instance.GetLength(p.Id), p.Size,
                    $"{p.Type.Name} (0x{p.Id:X4}) declares size {p.Size}, the Hercules table says {HerculesPacketTable.Instance.GetLength(p.Id)}");
        }

        [Test]
        public void EveryPacketId_IsPresentInTheTable()
        {
            foreach (var p in AllPackets())
                Assert.AreNotEqual(-2, HerculesPacketTable.Instance.GetLength(p.Id), $"{p.Type.Name} uses unknown ID 0x{p.Id:X4}");
        }

        [Test]
        public void EveryPacket_HasAConstSize_ThatMatchesItsAttribute()
        {
            foreach (var p in AllPackets())
            {
                FieldInfo size = p.Type.GetField("Size", BindingFlags.Public | BindingFlags.Static);
                Assert.IsNotNull(size, $"{p.Type.Name} must declare 'public const int Size'");
                Assert.IsTrue(size.IsLiteral, $"{p.Type.Name}.Size must be a const");
                Assert.AreEqual(p.Size, (int)size.GetRawConstantValue(), $"{p.Type.Name}.Size differs from its attribute");
            }
        }

        [Test]
        public void IdsAreUnique_PerDirection()
        {
            foreach (var direction in new[] { true, false })
            {
                var dupes = AllPackets().Where(p => p.Incoming == direction).GroupBy(p => p.Id).Where(g => g.Count() > 1).ToList();
                Assert.IsEmpty(dupes, "duplicate IDs: " + string.Join(", ", dupes.Select(g => $"0x{g.Key:X4}")));
            }
        }

        [Test]
        public void EveryPacket_IsAReadonlyStruct()
        {
            foreach (var p in AllPackets())
            {
                Assert.IsTrue(p.Type.IsValueType, $"{p.Type.Name} must be a struct");
                Assert.IsTrue(p.Type.GetCustomAttributes().Any(a => a.GetType().FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute"),
                    $"{p.Type.Name} must be a readonly struct");
            }
        }

        [Test]
        public void EveryOutgoingPacket_ImplementsIOutgoingPacket()
        {
            foreach (var p in AllPackets().Where(x => !x.Incoming))
                Assert.IsTrue(typeof(IOutgoingPacket).IsAssignableFrom(p.Type), $"{p.Type.Name} must implement IOutgoingPacket");
        }
    }
}
