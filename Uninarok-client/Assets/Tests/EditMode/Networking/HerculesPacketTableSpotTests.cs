using NUnit.Framework;
using Uninarok.Networking.Core.Protocol;

namespace Uninarok.Networking.Tests
{
    // T4.1 Done-when: spot values of the generated table (SPEC-0001 FR-18, FR-19).
    public class HerculesPacketTableSpotTests
    {
        [Test]
        public void KnownFixedPacket_ReturnsItsLength()
        {
            Assert.AreEqual(55, HerculesPacketTable.Instance.GetLength(0x0064));    // REQUEST_LOGIN
        }

        [Test]
        public void KnownVariablePacket_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, HerculesPacketTable.Instance.GetLength(0x006B));    // CHARACTER_DATA
        }

        [Test]
        public void IdZeroAndUnlistedIds_AreUnknown()
        {
            Assert.AreEqual(-2, HerculesPacketTable.Instance.GetLength(0x0000));
            Assert.AreEqual(-2, HerculesPacketTable.Instance.GetLength(0xFFFF));
        }

        [Test]
        public void Table_HasAllFixtureEntries()
        {
            Assert.AreEqual(1595, HerculesPacketTable.PacketCount);
        }
    }
}
