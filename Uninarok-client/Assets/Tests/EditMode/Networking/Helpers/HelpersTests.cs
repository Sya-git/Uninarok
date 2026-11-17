using NUnit.Framework;

namespace Uninarok.Networking.Tests
{
    // T2.1: proves the framer test helpers behave as the framer suites will rely on.
    public class HelpersTests
    {
        [Test]
        public void TableStub_ReportsFixedVariableAndUnknown()
        {
            var t = new TableStub().Fixed(0x0064, 55).Variable(0x006B).Raw(0x0001, 0);
            Assert.AreEqual(55, t.GetLength(0x0064));
            Assert.AreEqual(-1, t.GetLength(0x006B));
            Assert.AreEqual(0, t.GetLength(0x0001));
            Assert.AreEqual(-2, t.GetLength(0x9999));
        }

        [Test]
        public void RecordingRouter_RecordsPayloadCopies_AndReportsUnhandled()
        {
            var router = new RecordingRouter().HandleOnly(0x0064);
            byte[] data = { 1, 2, 3 };
            Assert.IsTrue(router.Route(0x0064, data));
            data[0] = 99;                                   // the recorded copy must not change
            Assert.IsFalse(router.Route(0x0065, data));
            Assert.AreEqual(1, router.Packets.Count);
            Assert.AreEqual(new byte[] { 1, 2, 3 }, router.Packets[0].Payload);
        }
    }
}
