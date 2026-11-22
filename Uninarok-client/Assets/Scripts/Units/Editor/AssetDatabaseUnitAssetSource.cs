using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.AddressableAssets;
using Uninarok.Catalog;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Editor.Catalog;
using Object = UnityEngine.Object;

namespace Uninarok.Units.Editor
{
    /// <summary>
    /// The editor's asset source (PLAN-0009 4.2, 4.9, D10): loads synchronously through the AssetDatabase and counts
    /// references like the game's, so a leak shows. Used by the unit composer and the EditMode tests.
    /// </summary>
    public sealed class AssetDatabaseUnitAssetSource : IUnitAssetSource
    {
        readonly Dictionary<string, int> _counts = new Dictionary<string, int>(StringComparer.Ordinal);

        public AssetDatabaseUnitAssetSource(SpriteCatalog catalog, CompositionTables tables)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Tables = tables;
        }

        /// <summary>The imported catalog and tables; null when the catalog is not imported.</summary>
        public static AssetDatabaseUnitAssetSource Create(RoOutputRoots roots = null)
        {
            roots = roots ?? new RoOutputRoots();
            var catalog = RoCatalogLookupWindow.LoadInEditor(roots);
            return catalog == null ? null : new AssetDatabaseUnitAssetSource(catalog, AssetDatabase.LoadAssetAtPath<CompositionTables>(RoCompositionTablesConverter.AssetPath(roots)));
        }

        public SpriteCatalog Catalog { get; }
        public CompositionTables Tables { get; }

        /// <summary>References held in total: 0 when everything acquired was released.</summary>
        public int HeldCount => _counts.Values.Sum();

        public int CountOf(AssetReference reference) => _counts.TryGetValue(Key(reference), out int n) ? n : 0;

        public void Acquire<T>(AssetReference reference, Action<T> done) where T : Object
        {
            string key = Key(reference);
            _counts.TryGetValue(key, out int n);
            _counts[key] = n + 1;
            done(Load<T>(reference));
        }

        public void Release(AssetReference reference)
        {
            string key = Key(reference);
            if (!_counts.TryGetValue(key, out int n) || n <= 0) throw new InvalidOperationException($"released more often than acquired: {key}");
            if (n == 1) _counts.Remove(key);
            else _counts[key] = n - 1;
        }

        static string Key(AssetReference reference) => reference.RuntimeKey.ToString();

        static T Load<T>(AssetReference reference) where T : Object
        {
            string path = AssetDatabase.GUIDToAssetPath(reference.AssetGUID);
            if (string.IsNullOrEmpty(path)) return null;
            return string.IsNullOrEmpty(reference.SubObjectName)
                ? AssetDatabase.LoadAssetAtPath<T>(path)
                : AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<T>().FirstOrDefault(o => o.name == reference.SubObjectName);
        }
    }
}
