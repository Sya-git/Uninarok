using NUnit.Framework;
using Uninarok.Units.Editor;
using Uninarok.World.View;

namespace Uninarok.World.Tests
{
    // SPEC-0011 FR-4, FR-14 (PLAN-0011 4.7, D18): the server's weapon values to base types, on the imported catalog.
    [Category("NeedsData")]
    public class CatalogWeaponTypesTests
    {
        [Test]
        public void CatalogWeaponTypes_ViewIdItemIdUnknown()
        {
            var source = AssetDatabaseUnitAssetSource.Create();
            if (source == null || source.Tables == null) Assert.Ignore("the catalog or the composition tables are not imported");
            var types = new CatalogWeaponTypes(source);
            Assert.AreEqual(1, types.TypeOf(1), "view id 1: short sword");
            Assert.AreEqual(11, types.TypeOf(11), "view id 11: bow");
            Assert.AreEqual(1, types.TypeOf(1201), "item 1201, Knife: a short sword");
            Assert.AreEqual(1, types.TypeOf(1207), "item 1207, Main Gauche: a short sword");
            Assert.AreEqual(2, types.TypeOf(1101), "item 1101, Sword");
            Assert.AreEqual(11, types.TypeOf(1701), "item 1701, Bow");
            Assert.AreEqual(0, types.TypeOf(2101), "item 2101, Guard: not a weapon");
            Assert.AreEqual(0, types.TypeOf(9_999_999), "unknown: bare hand");
            Assert.AreEqual(0, types.TypeOf(0));
            Assert.AreEqual(0, types.BaseJobOf(4001), "high novice: novice");
            Assert.AreEqual(7, types.BaseJobOf(7), "a knight is itself");
        }
    }
}
