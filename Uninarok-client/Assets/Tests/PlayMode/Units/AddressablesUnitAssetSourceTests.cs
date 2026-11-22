using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Uninarok.Units.PlayTests
{
    // TASKS-0009 T4.3 (FR-14; PLAN-0009 4.2, R7): rapid look changes leak no Addressables handle.
    [Category("NeedsData")]
    public class AddressablesUnitAssetSourceTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator LoadChangeDestroy_1000Looks_EveryHandleCountReturnsToZero()
        {
            AddressablesUnitAssetSource source = null;
            yield return UnitPlayData.LoadSource(s => source = s);

            var loader = new UnitLoader(source);
            var looks = UnitPlayData.ReferenceLooks();
            var random = new System.Random(9);
            var units = new UnitParts[20];
            for (int i = 0; i < units.Length; i++) units[i] = new UnitParts();

            int applied = 0;
            for (int i = 0; i < 1000; i++)
            {
                var parts = units[random.Next(units.Length)];
                if (random.Next(5) == 0) loader.Release(parts);                       // destroyed, maybe mid-load
                else loader.Load(looks[random.Next(looks.Length)], parts, (_, __) => applied++); // changed, maybe mid-load
                if (i % 25 == 24) yield return null;
            }
            for (int i = 0; i < 600 && applied == 0; i++) yield return null;
            Assert.That(applied, Is.GreaterThan(0), "some loads were applied");

            foreach (var parts in units) loader.Release(parts);
            for (int i = 0; i < 600 && source.HeldCount > 0; i++) yield return null; // loads in flight release when they arrive
            Assert.That(source.HeldCount, Is.EqualTo(0));
            Assert.That(source.OpenHandles, Is.EqualTo(0));
            source.Dispose();
        }
    }
}
