using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;
using Uninarok.Sprites;

namespace Uninarok.Catalog.PlayTests
{
    // TASKS-0008 T6.2 (FR-1; PLAN-0008 4.5, §6): the facade loads through Addressables in play mode (the editor's "Use Asset
    // Database" play-mode script). Needs the monster catalog imported into the project (T7.2); ignored with a message before.
    public class SpriteCatalogPlayTests
    {
        const int Poring = 1002;

        [UnityTest]
        public IEnumerator LoadAsyncMonsters_ThenPoring_LoadsItsAnimation_AndReleaseLetsGo()
        {
#if UNITY_EDITOR
            if (UnityEditor.AssetDatabase.LoadMainAssetAtPath(SpriteCatalogKeys.Index(SpriteCatalogCategory.Monsters)) == null)
                Assert.Ignore("the monster catalog is not imported yet (Tools/Ragnarok/Catalog/Monsters, TASKS-0008 T7.2)");
#endif
            var load = SpriteCatalog.LoadAsync(SpriteCatalogCategory.Monsters);
            yield return load;
            Assert.That(load.Status, Is.EqualTo(AsyncOperationStatus.Succeeded), load.OperationException?.ToString());
            var catalog = load.Result;

            Assert.That(catalog.TryGetUnit(Poring, out var poring), Is.True, "Poring 1002 is in the monster index");
            var animation = poring.Sprite.LoadAnimation();
            yield return animation;
            Assert.That(animation.Status, Is.EqualTo(AsyncOperationStatus.Succeeded), animation.OperationException?.ToString());
            Assert.That(animation.Result, Is.InstanceOf<SpriteAnimation>());
            Assert.That(animation.Result.Actions.Length, Is.GreaterThan(0));
            Addressables.Release(animation);

            Assert.That(catalog.HoldsLoadedAssets, Is.True);
            catalog.Release();
            Assert.That(catalog.HoldsLoadedAssets, Is.False, "Release lets the index assets go");
            Assert.That(catalog.TryGetUnit(Poring, out _), Is.False, "and the lookups miss");
            Addressables.Release(load);
            Assert.That(load.IsValid(), Is.False, "no handle left open");
        }
    }
}
