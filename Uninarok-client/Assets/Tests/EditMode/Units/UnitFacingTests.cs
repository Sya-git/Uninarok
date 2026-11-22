using NUnit.Framework;
using Uninarok.Networking.Data;

namespace Uninarok.Units.Tests
{
    // TASKS-0009 T3.1 (FR-6; PLAN-0009 4.3)
    public class UnitFacingTests
    {
        [TestCase(0f, 0)]
        [TestCase(22f, 0)]
        [TestCase(22.4f, 0)]
        [TestCase(22.5f, 1)]
        [TestCase(23f, 1)]
        [TestCase(67.4f, 1)]
        [TestCase(90f, 2)]
        [TestCase(180f, 4)]
        [TestCase(-22f, 0)]
        [TestCase(-23f, 7)]
        [TestCase(-45f, 7)]
        [TestCase(359f, 0)]
        [TestCase(337f, 7)]
        [TestCase(720f, 0)]
        [TestCase(-720f, 0)]
        public void UnitFacing_CameraSector_Borders(float yaw, int sector) => Assert.That(UnitFacing.CameraSector(yaw), Is.EqualTo(sector));

        [Test]
        public void UnitFacing_Shown_SouthFrontAtDefault_BackAt180_SideAt90()
        {
            int At(float yaw, UninarokDirection d = UninarokDirection.South) => UnitFacing.Shown(d, UnitFacing.CameraSector(yaw));
            Assert.That(At(0f), Is.EqualTo(0), "front");
            Assert.That(At(22f), Is.EqualTo(0), "still the front");
            Assert.That(At(23f), Is.EqualTo(7), "the next direction");
            Assert.That(At(180f), Is.EqualTo(4), "back");
            Assert.That(At(90f), Is.EqualTo(6), "the camera looks east: south is on its right");
            Assert.That(At(-90f), Is.EqualTo(2), "the camera looks west: south is on its left");
        }

        // The client's own names of the sprite directions (composition-reference.md 1).
        [TestCase(UninarokDirection.South, 0)]
        [TestCase(UninarokDirection.SouthWest, 1)]
        [TestCase(UninarokDirection.West, 2)]
        [TestCase(UninarokDirection.NorthWest, 3)]
        [TestCase(UninarokDirection.North, 4)]
        [TestCase(UninarokDirection.NorthEast, 5)]
        [TestCase(UninarokDirection.East, 6)]
        [TestCase(UninarokDirection.SouthEast, 7)]
        public void UnitFacing_Shown_AtTheDefaultCamera(UninarokDirection world, int shown) => Assert.That(UnitFacing.Shown(world, 0), Is.EqualTo(shown));

        [Test]
        public void UnitFacing_Shown_UnitFacingTheCamera_ShowsItsFront()
        {
            // A camera turned to sector s looks along that direction; the unit facing back at it shows its front.
            for (int s = 0; s < 8; s++)
            {
                // Sector 2 looks east: the unit that faces west (network 2) faces the camera.
                var facing = (UninarokDirection)((4 - s) & 7);
                Assert.That(UnitFacing.Shown(facing, s), Is.EqualTo(0), $"sector {s}");
            }
        }
    }
}
