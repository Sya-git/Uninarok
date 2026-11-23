using System;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In.Map;

namespace Uninarok.Networking.Tests
{
    // SPEC-0011 FR-1, FR-5, FR-11, FR-12 (plan 4.1, T1.2): the combat packets decode their samples with nothing left over, and
    // the hot and warm ones allocate nothing. Samples are from SPEC-0010's captures unless written otherwise.
    public partial class PacketDecodeTests
    {
        // ---- DAMAGE (0x08C8, 34) ----

        [Test]
        public void DAMAGE_DecodesTheRealCapture()
        {
            // world-prt_fild08.bin, the first blow: a monster misses the own character
            var miss = new DAMAGE(Hex("69c58e06" + "80841e00" + "4dc33934" + "90030000" + "00000000" + "00000000" + "00" + "0100" + "00" + "00000000"));
            Assert.AreEqual(110019945u, miss.SourceId);
            Assert.AreEqual(2000000u, miss.TargetId);
            Assert.AreEqual(0x3439C34Du, miss.Tick);
            Assert.AreEqual((912, 0), (miss.AttackMt, miss.AttackedMt));
            Assert.AreEqual((0, 0), (miss.Damage, miss.LeftDamage));
            Assert.AreEqual((0, 1, 0), ((int)miss.IsSpDamage, (int)miss.Count, (int)miss.Type));

            // world-prt_fild08.bin, the lucky dodge
            var dodge = new DAMAGE(Hex("7fc58e06" + "80841e00" + "0e773d34" + "90030000" + "00000000" + "00000000" + "00" + "0100" + "0b" + "00000000"));
            Assert.AreEqual((110019967u, 2000000u), (dodge.SourceId, dodge.TargetId));
            Assert.AreEqual((912, 0, 1, 11), (dodge.AttackMt, dodge.Damage, (int)dodge.Count, (int)dodge.Type));
        }

        [Test]
        public void DAMAGE_DecodesSignedValues_AndRejectsWrongLengths()
        {
            // a dual-wield critical of 1,234 and 567, then a heal (negative damage, count 0)
            var crit = new DAMAGE(Hex("01000000" + "02000000" + "10000000" + "F4010000" + "2C010000" + "D2040000" + "00" + "0200" + "0A" + "37020000"));
            Assert.AreEqual((500, 300, 1234, 567), (crit.AttackMt, crit.AttackedMt, crit.Damage, crit.LeftDamage));
            Assert.AreEqual((2, 10), ((int)crit.Count, (int)crit.Type));
            var heal = new DAMAGE(Hex("01000000" + "01000000" + "10000000" + "00000000" + "00000000" + "9CFFFFFF" + "00" + "0000" + "00" + "00000000"));
            Assert.AreEqual((-100, 0), (heal.Damage, (int)heal.Count));
            Assert.Throws<PacketFormatException>(() => new DAMAGE(new byte[31]));
            Assert.Throws<PacketFormatException>(() => new DAMAGE(new byte[33]));
        }

        // ---- LONG_PARAMETER_CHANGE (0x0ACB, 12) ----

        [Test]
        public void LONG_PARAMETER_CHANGE_DecodesTheRealCapture_AndRejectsWrongLengths()
        {
            // world-prontera.bin: base EXP to the next level, 350
            var next = new LONG_PARAMETER_CHANGE(Hex("1600" + "5e01000000000000"));
            Assert.AreEqual((LONG_PARAMETER_CHANGE.NextBaseExp, 350L), (next.VarId, next.Value));
            // above 32 bits
            var big = new LONG_PARAMETER_CHANGE(Hex("0100" + "0000000001000000"));
            Assert.AreEqual((LONG_PARAMETER_CHANGE.BaseExp, 1L << 32), (big.VarId, big.Value));
            Assert.Throws<PacketFormatException>(() => new LONG_PARAMETER_CHANGE(new byte[9]));
            Assert.Throws<PacketFormatException>(() => new LONG_PARAMETER_CHANGE(new byte[11]));
        }

        // ---- EXP_GAIN (0x0ACC, 18) ----

        [Test]
        public void EXP_GAIN_DecodesTheRealCapture_AndRejectsWrongLengths()
        {
            // world-prt_fild08.bin: 1 base EXP
            var p = new EXP_GAIN(Hex("80841e00" + "0100000000000000" + "0100" + "0000"));
            Assert.AreEqual((2000000u, 1L, 1, 0), (p.UnitId, p.Amount, (int)p.VarId, (int)p.Quest));
            Assert.Throws<PacketFormatException>(() => new EXP_GAIN(new byte[15]));
            Assert.Throws<PacketFormatException>(() => new EXP_GAIN(new byte[17]));
        }

        [Test]
        public void CombatStatusPackets_SizesAndIds()
        {
            Assert.AreEqual((0x08C8, 34), ((int)PacketIds.DAMAGE, DAMAGE.Size));
            Assert.AreEqual((0x0ACB, 12), ((int)PacketIds.LONG_PARAMETER_CHANGE, LONG_PARAMETER_CHANGE.Size));
            Assert.AreEqual((0x0ACC, 18), ((int)PacketIds.EXP_GAIN, EXP_GAIN.Size));
        }

        [Test]
        public void CombatStatusPackets_ThousandDecodes_AllocateNothing()
        {
            var damage = Hex("69c58e06" + "80841e00" + "4dc33934" + "90030000" + "00000000" + "00000000" + "00" + "0100" + "00" + "00000000");
            var parameter = Hex("0500" + "64000000");
            var longParameter = Hex("1600" + "5e01000000000000");
            var gain = Hex("80841e00" + "0100000000000000" + "0100" + "0000");
            long sum = 0;
            Action run = () =>
            {
                for (int i = 0; i < Decodes; i++)
                    sum += new DAMAGE(damage).AttackMt + new PARAMETER_CHANGE(parameter).Value + new LONG_PARAMETER_CHANGE(longParameter).Value + new EXP_GAIN(gain).Amount;
            };
            run();
            Assert.That(() => run(), UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
            Assert.AreNotEqual(0L, sum);
        }
    }
}
