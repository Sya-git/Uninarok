using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Uninarok.Sprites;

namespace Uninarok.Catalog
{
    /// <summary>
    /// What SPEC-0007 produced for one sprite: an animation or a static sprite (SPEC-0007 FR-11; PLAN-0008 4.4, D3). One of
    /// the two references is set. Loading goes through Addressables with the reference's <c>RuntimeKey</c>; the caller owns
    /// the handle and releases it (<c>Addressables.Release</c>), and nothing relies on the handle a reference caches.
    /// </summary>
    [Serializable]
    public struct SpriteRef
    {
        public SpriteAnimationReference Animation;
        /// <summary>The sheet's GUID and the sub-asset name of the sprite (<c>f12</c>, SPEC-0007 4.2).</summary>
        public AssetReferenceSprite Still;

        public bool HasAnimation => Animation != null && Animation.RuntimeKeyIsValid();
        public bool HasStill => Still != null && Still.RuntimeKeyIsValid();

        /// <summary>Neither reference has a valid key.</summary>
        public bool IsEmpty => !HasAnimation && !HasStill;

        public AsyncOperationHandle<SpriteAnimation> LoadAnimation() => Addressables.LoadAssetAsync<SpriteAnimation>(Animation.RuntimeKey);

        public AsyncOperationHandle<Sprite> LoadStill() => Addressables.LoadAssetAsync<Sprite>(Still.RuntimeKey);
    }
}
