using NUnit.Framework;
using UnityEditor;

namespace Uninarok.World.Tests
{
    // SPEC-0010 FR-18 (T3.1): the asset holds the plan's defaults.
    public class WorldSettingsTests
    {
        [Test]
        public void WorldSettings_Asset_HoldsThePlanDefaults()
        {
            var s = AssetDatabase.LoadAssetAtPath<WorldSettings>("Assets/Settings/World/WorldSettings.asset");
            Assert.IsNotNull(s, "Assets/Settings/World/WorldSettings.asset");
            Assert.AreEqual((17, 14, 1), (s.MaxWalkPath, s.MaxWalkPathObstructed, s.CellStackLimit));
            Assert.AreEqual((500, 5000, 150), (s.HoldRepeatMs, s.DeadMonsterLingerMs, s.WalkAnimationSpeed));
            Assert.IsFalse(s.WarpKeepsDirection);
        }
    }
}
