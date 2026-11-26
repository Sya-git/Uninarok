using System;
using UnityEngine;

namespace Uninarok.Effects
{
    // sorted by key
    public sealed class EffectCatalogIndex : ScriptableObject
    {
        public string[] Keys; public EffectAssetRef[] Assets; public bool[] SourceMissing;
    }

    // Addressables key = GUID, as PLAN-0008
    [Serializable]
    public struct EffectAssetRef { public string Guid; }
}
