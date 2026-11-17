using System;
using UnityEditor;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Editor.Writing
{
    /// <summary>The only code that writes assets (plan 4.7). Contract only: implemented by T4.4.</summary>
    public sealed class RoAssetWriter
    {
        public RoAssetWriter(RoOutputRoots roots) { }

        public bool InBatch => throw new NotImplementedException("T4.4");
        public RoStatus WriteBytes(string projectPath, byte[] bytes, Action<AssetImporter> configure = null) => throw new NotImplementedException("T4.4");
        public RoStatus UpsertObject<T>(string projectPath, T fresh) where T : UnityEngine.Object => throw new NotImplementedException("T4.4");
        public bool Exists(string projectPath) => throw new NotImplementedException("T4.4");
        public T Load<T>(string projectPath) where T : UnityEngine.Object => throw new NotImplementedException("T4.4");
        public void BeginBatch() => throw new NotImplementedException("T4.4");
        public void EndBatch() => throw new NotImplementedException("T4.4");
    }
}
