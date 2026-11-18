using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Uninarok.RoImport.Core.Materials;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Ledger;
using Uninarok.RoImport.Editor.Ui;

namespace Uninarok.RoImport.Editor.Pipeline
{
    /// <summary>Options of one run (plan 4.10). Null members mean the project defaults.</summary>
    public sealed class RoImportOptions
    {
        /// <summary>Convert even items the ledger says are current.</summary>
        public bool Force;

        /// <summary>Show a cancelable progress bar during a batch.</summary>
        public bool ShowProgress = true;

        /// <summary>Output roots; null: the default roots.</summary>
        public RoOutputRoots Roots;

        /// <summary>Source files; null: a folder source over <see cref="RoSourceLocation.Resolve"/> with the local hash cache.</summary>
        public IRoSource Source;

        /// <summary>Ledger folder; null: <see cref="RoLedger.DefaultFolder"/>.</summary>
        public string LedgerFolder;

        /// <summary>Test hook: asked before each job; true cancels the batch there, like the progress bar's Cancel.</summary>
        internal Func<RoJob, bool> CancelBefore;

        /// <summary>Test hook: converters to use instead of <see cref="RoConverterRegistry"/>.</summary>
        internal IReadOnlyList<IRoConverter> Converters;
    }

    /// <summary>
    /// Conversions by Ragnarok path, from code or the editor (plan 4.10; SPEC-0002 FR-2). Nothing here needs a window or a
    /// selection; the menu and the window call <see cref="Run"/> too. The later plans add <c>Model</c>, <c>Map</c> and
    /// <c>Sprite</c> as one-line wrappers over <see cref="Run"/>.
    /// Inside the <c>Uninarok.RoImport</c> namespaces the name <c>RoImport</c> means the namespace; use an alias there.
    /// </summary>
    public static class RoImport
    {
        /// <summary>Converts one texture (or skips it when current) and returns it, or the placeholder with the failure.</summary>
        public static RoResult<Texture2D> Texture(string roPath, RoImportOptions options = null)
        {
            if (!RoPath.TryParse(roPath, out var path))
                return RoResult<Texture2D>.Failed(new RoIssue(RoIssueCode.SourceMissing, roPath, null, "not a valid Ragnarok path"));
            using (var session = new Session(options))
                return session.Context.Texture(path);
        }

        /// <summary>The shared material of a texture, surface and sidedness, converting the texture first if needed (FR-6).</summary>
        public static RoResult<Material> Material(RoPath texture, RoSurface surface, bool twoSided, RoImportOptions options = null)
        {
            using (var session = new Session(options))
                return session.Context.Material(new RoMaterialKey(texture, surface, twoSided));
        }

        /// <summary>
        /// Converts one RSM model (or skips it when current) and returns its prefab (PLAN-0003 4.9). A model that cannot be
        /// converted is <see cref="RoStatus.Failed"/> with no value and its reason.
        /// </summary>
        public static RoResult<GameObject> Model(string roPath, RoImportOptions options = null)
        {
            if (!RoPath.TryParse(roPath, out var path))
                return RoResult<GameObject>.Failed(new RoIssue(RoIssueCode.SourceMissing, roPath, null, "not a valid Ragnarok path"));
            using (var session = new Session(options))
            {
                var result = RoConverterRegistry.For(RoJobKind.Model).Convert(session.Context, path);
                return new RoResult<GameObject>(result.Status, result.Value as GameObject, result.Failure);
            }
        }

        /// <summary>Runs a batch (plan 4.10): continues past failures and returns the report, whose summary lists them.</summary>
        public static RoReport Run(IEnumerable<RoJob> jobs, RoImportOptions options = null) => RoBatch.Run(jobs, options);

        /// <summary>
        /// One run's context and the things saved around it: ledgers, the hash cache and dirty assets are saved on dispose.
        /// </summary>
        internal sealed class Session : IDisposable
        {
            readonly RoHashCache _hashes;

            public Session(RoImportOptions options)
            {
                options = options ?? new RoImportOptions();
                var report = new RoReport();
                IRoSource source = options.Source;
                if (source == null)
                {
                    _hashes = new RoHashCache(RoHashCache.DefaultFile);
                    _hashes.Load();
                    source = new RoFolderSource(RoSourceLocation.Resolve(), report, _hashes);
                }
                Context = new RoImportContext(source, options.Roots ?? new RoOutputRoots(), options.LedgerFolder ?? RoLedger.DefaultFolder, report)
                {
                    Force = options.Force,
                };
            }

            public RoImportContext Context { get; }

            /// <summary>Saves dirty assets, the ledgers and the hash cache: the end of a chunk or of the run.</summary>
            public void Save()
            {
                AssetDatabase.SaveAssets();
                Context.SaveLedgers();
                _hashes?.Save();
            }

            public void Dispose() => Save();
        }
    }
}
