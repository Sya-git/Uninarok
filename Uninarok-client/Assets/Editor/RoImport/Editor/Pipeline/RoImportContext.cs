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

        /// <summary>
        /// The converted texture of a Ragnarok texture path, for materials (FR-6). Minimal form (T5.3): loads an already
        /// converted texture and reports <see cref="RoIssueCode.TextureMissing"/> once when there is none. T6.2 replaces this
        /// with conversion on demand.
        /// </summary>
        public RoResult<Texture2D> Texture(RoPath path, string referrer = null)
        {
            string key = "texture:" + path.Canonical;
            if (TryGetMemo(key, out Texture2D done)) return RoResult<Texture2D>.Skipped(done);

            var texture = Writer.Load<Texture2D>(RoOutputPaths.Texture(Roots, path));
            if (texture == null)
            {
                const string why = "the texture has not been converted";
                Report.Warn(RoIssueCode.TextureMissing, path.Canonical, referrer, why);
                return RoResult<Texture2D>.Failed(new RoIssue(RoIssueCode.TextureMissing, path.Canonical, referrer, why));
            }
            Memo(key, texture);
            return RoResult<Texture2D>.Skipped(texture);
        }

        /// <summary>The shared material of a key, created or updated on demand (plan 4.6, FR-6, FR-9).</summary>
        public RoResult<Material> Material(RoMaterialKey key, string referrer = null) =>
            RoMaterialFactory.GetOrCreate(this, key, referrer);

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
