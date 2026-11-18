using System;
using System.Collections.Generic;
using UnityEngine;
using Uninarok.RoImport.Core.Imaging;
using Uninarok.RoImport.Core.Materials;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Editor.Materials;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Ledger;
using Uninarok.RoImport.Editor.Writing;

namespace Uninarok.RoImport.Editor.Pipeline
{
    /// <summary>
    /// Everything one conversion run shares (plan 4.10; SPEC-0002 FR-2, FR-6): the source, the writer, the report, the
    /// output roots, the PNG codec, the ledgers by category, a per-run memo (repeat requests are free) and the set of items
    /// in progress (a dependency cycle is caught instead of looping). Converters receive it; nothing in it needs a UI.
    /// The members that convert on demand (<c>Texture</c>, <c>Material</c>) are added by T6.2 and T5.3.
    /// </summary>
    public sealed class RoImportContext
    {
        readonly string _ledgerFolder;
        readonly Dictionary<string, RoLedger> _ledgers = new Dictionary<string, RoLedger>(StringComparer.Ordinal);
        readonly Dictionary<string, object> _memo = new Dictionary<string, object>(StringComparer.Ordinal);
        readonly HashSet<string> _inProgress = new HashSet<string>(StringComparer.Ordinal);

        /// <param name="ledgerFolder">Where the ledgers live; the committed <see cref="RoLedger.DefaultFolder"/> unless a test says otherwise.</param>
        public RoImportContext(IRoSource source, RoOutputRoots roots, string ledgerFolder = RoLedger.DefaultFolder, RoReport report = null)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Roots = roots ?? throw new ArgumentNullException(nameof(roots));
            _ledgerFolder = ledgerFolder ?? throw new ArgumentNullException(nameof(ledgerFolder));
            Report = report ?? new RoReport();
            Writer = new RoAssetWriter(roots);
            Png = new RoPngCodec();
        }

        public IRoSource Source { get; }
        public RoAssetWriter Writer { get; }
        public RoReport Report { get; }
        public RoOutputRoots Roots { get; }
        public IRoPngCodec Png { get; set; }

        /// <summary>Convert even when the ledger says an item is current.</summary>
        public bool Force { get; set; }

        /// <summary>Also write the optional cell type image of each map data (PLAN-0005 4.5, FR-8).</summary>
        public bool GatImage { get; set; }

        /// <summary>The ledger of a category (<c>texture</c>, <c>material</c>, ...), loaded once per run.</summary>
        public RoLedger Ledger(string category)
        {
            if (!_ledgers.TryGetValue(category, out var ledger))
            {
                ledger = RoLedger.Load(category, _ledgerFolder);
                _ledgers.Add(category, ledger);
            }
            return ledger;
        }

        /// <summary>Saves every ledger this run loaded (each writes only when its text changed).</summary>
        public void SaveLedgers()
        {
            foreach (var ledger in _ledgers.Values) ledger.Save();
        }

        sealed class TextureMemo
        {
            public RoResult<Texture2D> Result;
            public string Hash;
        }

        readonly Stack<List<RoLedgerDependency>> _dependencyFrames = new Stack<List<RoLedgerDependency>>();

        /// <summary>
        /// The texture of a Ragnarok path, converted on demand when missing or stale (plan 4.10, FR-6). Repeat requests in a
        /// run are free. A missing, unsupported or corrupt texture is <see cref="RoStatus.Failed"/> with the placeholder as
        /// its value and one warning naming the referrer. While a dependency frame is open, the texture is recorded in it.
        /// </summary>
        public RoResult<Texture2D> Texture(RoPath path, string referrer = null)
        {
            string key = "texture:" + path.Canonical;
            if (!TryGetMemo(key, out TextureMemo memo))
            {
                var result = RoTextureConverter.ConvertTexture(this, path, referrer, out string hash);
                memo = new TextureMemo { Result = result, Hash = hash };
                Memo(key, memo);
                AddDependency(path, hash);
                return result;
            }

            AddDependency(path, memo.Hash);
            return memo.Result.Status == RoStatus.Failed ? memo.Result : RoResult<Texture2D>.Skipped(memo.Result.Value);
        }

        /// <summary>
        /// The ground's Clamp copy of a texture (PLAN-0004 D17), converted on demand like <see cref="Texture"/>, with the
        /// same placeholder and warning rules. The source is recorded as a dependency, as for <see cref="Texture"/>.
        /// </summary>
        public RoResult<Texture2D> GroundTexture(RoPath path, string referrer = null)
        {
            string key = "groundtexture:" + path.Canonical;
            if (!TryGetMemo(key, out TextureMemo memo))
            {
                var result = RoTextureConverter.ConvertTexture(this, path, referrer, out string hash, ground: true);
                memo = new TextureMemo { Result = result, Hash = hash };
                Memo(key, memo);
                AddDependency(path, hash);
                return result;
            }

            AddDependency(path, memo.Hash);
            return memo.Result.Status == RoStatus.Failed ? memo.Result : RoResult<Texture2D>.Skipped(memo.Result.Value);
        }

        /// <summary>Starts collecting the inputs a converter asks for (its ledger dependencies, plan 4.8). Frames nest.</summary>
        public void BeginDependencies() => _dependencyFrames.Push(new List<RoLedgerDependency>());

        /// <summary>Ends the innermost frame and returns its dependencies (each source once).</summary>
        public RoLedgerDependency[] EndDependencies()
        {
            if (_dependencyFrames.Count == 0) throw new InvalidOperationException("EndDependencies without BeginDependencies.");
            return _dependencyFrames.Pop().ToArray();
        }

        void AddDependency(RoPath path, string hash)
        {
            if (_dependencyFrames.Count == 0) return;
            var frame = _dependencyFrames.Peek();
            foreach (var d in frame)
                if (d.Source == path.Canonical) return;
            frame.Add(new RoLedgerDependency(path.Canonical, hash));
        }

        /// <summary>The shared material of a key, created or updated on demand (plan 4.6, FR-6, FR-9).</summary>
        public RoResult<Material> Material(RoMaterialKey key, string referrer = null) =>
            RoMaterialFactory.GetOrCreate(this, key, referrer);

        /// <summary>
        /// The ground prefab of a <c>.gnd</c> path, converted on demand when missing or stale (PLAN-0004 4.6; SPEC-0002
        /// FR-6), so the map converter of PLAN-0006 asks for it. Repeat requests in a run are free.
        /// </summary>
        public RoResult<GameObject> Ground(RoPath path)
        {
            string key = "ground:" + path.Canonical;
            if (TryGetMemo(key, out RoResult<GameObject> known))
                return known.Status == RoStatus.Failed ? known : RoResult<GameObject>.Skipped(known.Value);

            var result = RoConverterRegistry.For(RoJobKind.Ground).Convert(this, path);
            var typed = new RoResult<GameObject>(result.Status, result.Value as GameObject, result.Failure);
            Memo(key, typed);
            return typed;
        }

        /// <summary>
        /// The map data asset of a <c>.gat</c> path, converted on demand when missing or stale (PLAN-0005 4.5), so the map
        /// converter of PLAN-0006 asks for it. Repeat requests in a run are free.
        /// </summary>
        public RoResult<Uninarok.Map.MapData> MapData(RoPath path) => throw new NotImplementedException();

        /// <summary>A result already produced in this run, by key (e.g. <c>texture:texture/a.bmp</c>).</summary>
        internal bool TryGetMemo<T>(string key, out T value)
        {
            if (_memo.TryGetValue(key, out object stored) && stored is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }

        internal void Memo<T>(string key, T value) => _memo[key] = value;

        /// <summary>Marks an item as being converted; false if it already is (a dependency cycle).</summary>
        internal bool TryEnter(string key) => _inProgress.Add(key);

        internal void Exit(string key) => _inProgress.Remove(key);
    }
}
