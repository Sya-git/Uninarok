using System;
using System.Collections.Generic;
using Uninarok.Effects;
using Uninarok.RoImport.Core.Source;

namespace Uninarok.RoImport.Core.Effects
{
    /// <summary>
    /// Read-only check of the effect data (PLAN-0012 4.9, D30, SPEC-0012 FR-1). Only asks <see cref="IRoSource"/> for
    /// existence and counts; never opens RO file bytes. Stub until T6.2.
    /// </summary>
    public static class RoEffectDataCheck
    {
        public static RoEffectDataReport Run(IRoSource source, EffectCatalogIndex index,
            Func<EffectAssetRef, EffectAsset> load, IEnumerable<EffectAsset> authored = null) =>
            throw new NotImplementedException("T6.2");
    }
}
