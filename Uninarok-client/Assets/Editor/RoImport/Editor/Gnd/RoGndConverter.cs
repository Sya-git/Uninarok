using System;
using UnityEngine;
using Uninarok.RoImport.Core.Gnd;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Editor.Pipeline;
using Object = UnityEngine.Object;

namespace Uninarok.RoImport.Editor.Gnd
{
    /// <summary>GND ground to chunk meshes and a ground prefab (PLAN-0004 4.6; SPEC-0004 FR-1, FR-6).</summary>
    public sealed class RoGndConverter : IRoConverter
    {
        public const string Category = "ground";
        public const int RulesVersion = 1;

        public RoJobKind Kind => throw new NotImplementedException("TASKS-0004 T5.2");

        /// <summary><c>.gnd</c> files.</summary>
        public bool Handles(RoPath path) => path.Extension == ".gnd";

        public RoResult<Object> Convert(RoImportContext ctx, RoPath path) =>
            throw new NotImplementedException("TASKS-0004 T5.2");

        /// <summary>Converts a ground already read into <paramref name="gnd"/> (D14); used by <see cref="Convert"/> and tests.</summary>
        internal static RoResult<GameObject> Write(RoImportContext ctx, RoPath path, RoGnd gnd, string sourceHash) =>
            throw new NotImplementedException("TASKS-0004 T5.2");
    }
}
