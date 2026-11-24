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
        /// <summary>A copy whose members can be set for one batch without changing the caller's options.</summary>
        internal RoImportOptions Clone() => (RoImportOptions)MemberwiseClone();

        /// <summary>Test hook: the Addressables settings of a catalog run (PLAN-0008 4.6) or of map scenes (PLAN-0010 D33); null: the project's.</summary>
        internal UnityEditor.AddressableAssets.Settings.AddressableAssetSettings CatalogAddressables;

        /// <summary>Convert even items the ledger says are current.</summary>
        public bool Force;

        /// <summary>Also write the optional cell type image of each map data (PLAN-0005 4.5, FR-8).</summary>
        public bool GatImage;

        /// <summary>Give converted sprites their index texture, so palettes can recolour them (PLAN-0007 4.4, FR-7).</summary>
        public bool SpritePalettes;

        /// <summary>Show a cancelable progress bar during a batch.</summary>
        public bool ShowProgress = true;

        /// <summary>Output roots; null: the default roots.</summary>
        public RoOutputRoots Roots;

        /// <summary>Source files; null: a folder source over <see cref="RoSourceLocation.Resolve"/> with the local hash cache.</summary>
        public IRoSource Source;

        /// <summary>The client's <c>System</c> folder (item and scale tables, PLAN-0008 4.2); null: <see cref="RoSourceLocation.ResolveSystem"/>.</summary>
        public IRoSource SystemSource;

        /// <summary>The server's item_db.conf of this run (PLAN-0008 D21); null: the project setting.</summary>
        public string ItemDbPath;

        /// <summary>Test hook: the folder of the own catalog CSV tables; null: the project's (PLAN-0008 4.2).</summary>
        internal string CatalogOwnTablesFolder;

        /// <summary>Also import the composition tables in a catalog run (PLAN-0009 4.1); bodies and equipment always do.</summary>
        public bool Composition;

        /// <summary>The catalog run the catalog converters work from (PLAN-0008 4.7); set by the catalog importer.</summary>
        internal Uninarok.RoImport.Editor.Catalog.RoCatalogRun CatalogPlan;

        /// <summary>Ledger folder; null: <see cref="RoLedger.DefaultFolder"/>.</summary>
        public string LedgerFolder;

        /// <summary>The item table of this run (PLAN-0008 4.8, FR-12); null: the project setting (<see cref="Catalog.RoImportSettings"/>).</summary>
        public Uninarok.RoImport.Core.Catalog.Tables.RoItemTableLanguage? ItemTableLanguage;

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

        /// <summary>
        /// Converts the ground of a map (or skips it when current) and returns its prefab (PLAN-0004 4.6): the source is
        /// <c>&lt;mapName&gt;.gnd</c> at the data root (D11). A ground that cannot be converted is
        /// <see cref="RoStatus.Failed"/> with no value and its reason.
        /// </summary>
        public static RoResult<GameObject> Ground(string mapName, RoImportOptions options = null)
        {
            if (!RoPath.TryParse((mapName ?? string.Empty) + ".gnd", out var path))
                return RoResult<GameObject>.Failed(new RoIssue(RoIssueCode.SourceMissing, mapName, null, "not a valid map name"));
            using (var session = new Session(options))
                return session.Context.Ground(path);
        }

        /// <summary>
        /// Converts the map data of a map (or skips it when current) and returns its asset (PLAN-0005 4.5): the source is
        /// <c>&lt;mapName&gt;.gat</c> at the data root, with <c>&lt;mapName&gt;.gnd</c> beside it (D9).
        /// </summary>
        public static RoResult<Uninarok.Map.MapData> MapData(string mapName, RoImportOptions options = null)
        {
            if (!RoPath.TryParse((mapName ?? string.Empty) + ".gat", out var path))
                return RoResult<Uninarok.Map.MapData>.Failed(new RoIssue(RoIssueCode.SourceMissing, mapName, null, "not a valid map name"));
            using (var session = new Session(options))
                return session.Context.MapData(path);
        }

        /// <summary>
        /// Converts a map (or skips it when current) and returns its scene (PLAN-0006 4.6): the source is
        /// <c>&lt;mapName&gt;.rsw</c> at the data root, with its ground and map data beside it (D16). Pass a report to
        /// keep the run's warnings.
        /// </summary>
        public static RoResult<SceneAsset> Map(string mapName, RoImportOptions options = null, RoReport report = null)
        {
            if (!RoPath.TryParse((mapName ?? string.Empty) + ".rsw", out var path))
                return RoResult<SceneAsset>.Failed(new RoIssue(RoIssueCode.SourceMissing, mapName, null, "not a valid map name"));
            using (var session = new Session(options, report))
            {
                var result = RoConverterRegistry.For(RoJobKind.Map).Convert(session.Context, path);
                return new RoResult<SceneAsset>(result.Status, result.Value as SceneAsset, result.Failure);
            }
        }

        /// <summary>
        /// Converts one ACT with the SPR of the same folder and name (or skips it when current) and returns its animation,
        /// or the sprite of a static ACT (PLAN-0007 4.4). A sprite that cannot be converted is <see cref="RoStatus.Failed"/>
        /// with no value and its reason.
        /// </summary>
        public static RoResult<UnityEngine.Object> Sprite(string actPath, RoImportOptions options = null)
        {
            if (!RoPath.TryParse(actPath, out var path))
                return RoResult<UnityEngine.Object>.Failed(new RoIssue(RoIssueCode.SourceMissing, actPath, null, "not a valid Ragnarok path"));
            using (var session = new Session(options))
                return RoConverterRegistry.For(RoJobKind.Sprite).Convert(session.Context, path);
        }

        /// <summary>
        /// Makes the effect entry of every ACT under <c>sprite/이팩트/</c> (PLAN-0012 4.4; SPEC-0012 FR-4): one
        /// <c>EffectAsset</c> each, nothing imported. The source is the options' or the project's data folder.
        /// </summary>
        public static RoReport EffectSprites(RoImportOptions options = null)
        {
            var source = options?.Source ?? new RoFolderSource(RoSourceLocation.Resolve());
            return Run(Effects.RoEffectSpriteEntryConverter.Jobs(source), options ?? new RoImportOptions { Source = source });
        }

        /// <summary>
        /// Rebuilds the effect catalog index (PLAN-0012 4.4; SPEC-0012 FR-5): every <c>EffectAsset</c> under the generated effect
        /// folder and <c>Data/Effects/Authored</c>, one key each. Rewritten only when it changes. The source tells which generated
        /// assets have lost their file.
        /// </summary>
        public static RoReport EffectIndex(RoImportOptions options = null)
        {
            var source = options?.Source ?? new RoFolderSource(RoSourceLocation.Resolve());
            return Run(new[] { new RoJob(RoJobKind.EffectIndex, Effects.RoEffectCatalogIndexConverter.JobPath) }, options ?? new RoImportOptions { Source = source });
        }

        /// <summary>Converts one PAL (or skips it when current) and returns its 256 × 1 palette texture (PLAN-0007 4.4, FR-7).</summary>
        public static RoResult<Texture2D> Palette(string palPath, RoImportOptions options = null)
        {
            if (!RoPath.TryParse(palPath, out var path))
                return RoResult<Texture2D>.Failed(new RoIssue(RoIssueCode.SourceMissing, palPath, null, "not a valid Ragnarok path"));
            using (var session = new Session(options))
                return Sprites.RoPaletteConverter.ConvertPalette(session.Context, path);
        }

        /// <summary>
        /// Imports the sprite catalog of <paramref name="categories"/> (SPEC-0008 FR-9; PLAN-0008 4.7): the tables they need, their
        /// plan, one job per record, then one index job per category; the report has the numbers of each category.
        /// </summary>
        public static global::Uninarok.RoImport.Editor.Catalog.RoCatalogReport Catalog(Uninarok.Catalog.SpriteCatalogCategory[] categories, RoImportOptions options = null) =>
            global::Uninarok.RoImport.Editor.Catalog.RoCatalogImporter.Run(categories, options);

        /// <summary>Imports the composition tables of the units alone (PLAN-0009 4.1; SPEC-0009 FR-3, FR-4, FR-11).</summary>
        public static global::Uninarok.RoImport.Editor.Catalog.RoCatalogReport Composition(RoImportOptions options = null)
        {
            options = (options ?? new RoImportOptions()).Clone();
            options.Composition = true;
            return Catalog(Array.Empty<Uninarok.Catalog.SpriteCatalogCategory>(), options);
        }

        /// <summary>Every category of the catalog (PLAN-0008 4.7).</summary>
        public static global::Uninarok.RoImport.Editor.Catalog.RoCatalogReport CatalogAll(RoImportOptions options = null) =>
            Catalog((Uninarok.Catalog.SpriteCatalogCategory[])Enum.GetValues(typeof(Uninarok.Catalog.SpriteCatalogCategory)), options);

        /// <summary>Runs a batch (plan 4.10): continues past failures and returns the report, whose summary lists them.</summary>
        public static RoReport Run(IEnumerable<RoJob> jobs, RoImportOptions options = null) => RoBatch.Run(jobs, options);

        /// <summary>
        /// One run's context and the things saved around it: ledgers, the hash cache and dirty assets are saved on dispose.
        /// </summary>
        internal sealed class Session : IDisposable
        {
            readonly RoHashCache _hashes;
            readonly RoImportOptions _options;

            public Session(RoImportOptions options, RoReport report = null)
            {
                options = options ?? new RoImportOptions();
                _options = options;
                report = report ?? new RoReport();
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
                    GatImage = options.GatImage,
                    SpritePalettes = options.SpritePalettes,
                    CatalogPlan = options.CatalogPlan,
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

            public void Dispose()
            {
                Save();
                // The map scenes of the project are loadable without another step (PLAN-0010 D33); another output folder is not the game's.
                if (Context.MapsConverted && (_options.Roots == null || _options.CatalogAddressables != null))
                    global::Uninarok.RoImport.Editor.Map.RoMapSceneAddressables.Ensure(_options.CatalogAddressables ?? UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.GetSettings(true), Context.Roots);
                // The generated effect folder exists once an effect asset is written, so its folder entry is made now (PLAN-0012 4.10, D27).
                if (Context.EffectsConverted && (_options.Roots == null || _options.CatalogAddressables != null))
                    global::Uninarok.RoImport.Editor.Catalog.RoCatalogAddressables.Ensure(_options.CatalogAddressables ?? global::Uninarok.RoImport.Editor.Catalog.RoCatalogAddressables.ProjectSettings(), Context.Roots);
            }
        }
    }
}
