using System;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Editor.Pipeline;
using Object = UnityEngine.Object;

namespace Uninarok.RoImport.Editor.Gat
{
    /// <summary>GAT to the map data asset (PLAN-0005 4.5). Implemented by TASKS-0005 T4.2.</summary>
    public sealed class RoMapDataConverter : IRoConverter
    {
        public const string Category = "mapdata";
        public const int RulesVersion = 1;

        public RoJobKind Kind => RoJobKind.MapData;

        public bool Handles(RoPath path) => throw new NotImplementedException();

        public RoResult<Object> Convert(RoImportContext ctx, RoPath path) => throw new NotImplementedException();
    }
}
