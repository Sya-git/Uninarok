using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Gnd;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Rsw;
using Uninarok.RoImport.Editor.Ledger;
using Uninarok.RoImport.Editor.Pipeline;
using Object = UnityEngine.Object;

namespace Uninarok.RoImport.Editor.Map
{
    /// <summary>
    /// RSW map to scene (PLAN-0006 4.6; SPEC-0006 FR-1 to FR-12). The ground, the map data and every distinct model are
    /// asked for on demand (converted when missing or stale); the hierarchy is built in a temporary scene, which is saved
    /// in place at <c>Scenes/&lt;name&gt;.unity</c> (GUID kept) and closed in a <c>finally</c>. The ledger (category
    /// <see cref="Category"/>) owns the scene and depends on the GND, the GAT and each model source. The additive scene
    /// beside it is never touched.
    /// </summary>
    public sealed class RoMapConverter : IRoConverter
    {
        public const string Category = "map";
        public const int RulesVersion = 1;

        public RoJobKind Kind => RoJobKind.Map;

        /// <summary><c>.rsw</c> files.</summary>
        public bool Handles(RoPath path) => path.Extension == ".rsw";

        public RoResult<Object> Convert(RoImportContext ctx, RoPath path)
        {
            var result = ConvertMap(ctx, path);
            if (result.Status != RoStatus.Failed) ctx.MapsConverted = true;
            return new RoResult<Object>(result.Status, result.Value, result.Failure);
        }

        static RoResult<SceneAsset> ConvertMap(RoImportContext ctx, RoPath path)
        {
            var paths = RoMapOutputPaths.For(ctx.Roots, path);
            if (!ctx.Source.TryGetFile(path, out var rswFile))
                return Fail(ctx, path, RoIssueCode.SourceMissing, "no such file in the source folder");

            string hash = rswFile.ContentHash();
            var ledger = ctx.Ledger(Category);
            if (!ctx.Force && ledger.IsCurrent(path, hash, RulesVersion, ctx.Source, ctx.Writer))
            {
                var current = ctx.Writer.Load<SceneAsset>(paths.Scene);
                if (current != null) return RoResult<SceneAsset>.Skipped(current);
            }

            // 1. Read the RSW and choose its rules row.
            if (!RoRswReader.TryRead(path, rswFile.ReadAllBytes(), out var rsw, out string reason))
                return Fail(ctx, path, RoIssueCode.CorruptFile, reason);
            if (!RoRswRules.TryGet(rsw.VersionMajor, rsw.VersionMinor, out var rules))
                return Fail(ctx, path, RoIssueCode.UnsupportedRswVersion,
                    $"RSW version {rsw.VersionMajor}.{rsw.VersionMinor} has no verified placement rules; the map is not converted");

            // 2. Dependencies on demand: ground, map data (its settings give the map space), models once per file.
            var gndPath = Beside(ctx, path, rsw.GndFile, paths.Gnd);
            var gatPath = Beside(ctx, path, rsw.GatFile, paths.Gat);
            var ground = ctx.Ground(gndPath);
            if (ground.Status == RoStatus.Failed || ground.Value == null)
                return Fail(ctx, path, RoIssueCode.SourceMissing, $"the ground '{gndPath.Canonical}' could not be converted: {ground.Failure?.Message}");
            var mapData = ctx.MapData(gatPath);
            if (mapData.Status == RoStatus.Failed || mapData.Value == null)
                return Fail(ctx, path, RoIssueCode.SourceMissing, $"the map data '{gatPath.Canonical}' could not be converted: {mapData.Failure?.Message}");
            if (!mapData.Value.HasSettings)
                return Fail(ctx, path, RoIssueCode.Unexpected, "the map data has no settings although the RSW exists");
            var settings = mapData.Value.Settings;
            var space = new RoMapSpace(settings.WidthCubes, settings.HeightCubes, settings.Zoom);

            ctx.BeginDependencies();
            var models = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            RoLedgerDependency[] modelDependencies;
            try
            {
                foreach (var m in rsw.Models)
                {
                    if (models.ContainsKey(m.File)) continue;
                    var model = RoPath.TryParse("model/" + m.File, out var modelPath) ? ctx.Model(modelPath) : default;
                    if (model.Status == RoStatus.Failed || model.Value == null)
                    {
                        ctx.Report.Warn(RoIssueCode.ModelMissing, modelPath.IsEmpty ? m.File : modelPath.Canonical, path.Canonical,
                            $"the model cannot be converted ({model.Failure?.Message ?? "not a valid path"}); its instances are placeholders");
                        models.Add(m.File, null);
                    }
                    else
                    {
                        models.Add(m.File, model.Value);
                    }
                }
            }
            finally
            {
                modelDependencies = ctx.EndDependencies();
            }

            // 3. Build in a temporary scene and save it in place.
            var notes = new RoMapSceneBuilder.Notes();
            RoStatus status;
            try
            {
                status = BuildAndSave(ctx, paths, new RoMapSceneBuilder.Input
                {
                    MapName = paths.MapName,
                    Rsw = rsw,
                    Space = space,
                    Rules = rules,
                    MapData = mapData.Value,
                    GroundPrefab = ground.Value,
                    Models = models
                }, notes);
            }
            catch (Exception e)
            {
                ledger.Forget(path);
                return Fail(ctx, path, RoIssueCode.Unexpected, $"building the map scene failed: {e.GetType().Name}: {e.Message}");
            }

            if (notes.ChildNodeNames > 0)
                ctx.Report.Warn(RoIssueCode.RswNodeNameIgnored, path.Canonical, null,
                    $"{notes.ChildNodeNames} instances name a child node of their model; the whole model is drawn");
            if (notes.UnknownAnimTypes.Count > 0)
                ctx.Report.Warn(RoIssueCode.RswUnknownAnimType, path.Canonical, null,
                    $"animation types {string.Join(", ", notes.UnknownAnimTypes)} are not known; treated as 2 (animated)");

            // 4. The ledger: the scene, and the inputs that change it.
            var dependencies = new List<RoLedgerDependency>
            {
                Dependency(ctx, gndPath),
                Dependency(ctx, gatPath)
            };
            dependencies.AddRange(modelDependencies);
            ledger.Record(new RoLedgerEntry
            {
                Source = path.Canonical,
                SourceHash = hash,
                Rules = RulesVersion,
                Outputs = new[] { paths.Scene },
                Dependencies = dependencies.OrderBy(d => d.Source, StringComparer.Ordinal).ToArray()
            }, ctx.Report);

            return new RoResult<SceneAsset>(status, ctx.Writer.Load<SceneAsset>(paths.Scene));
        }

        static RoStatus BuildAndSave(RoImportContext ctx, RoMapOutputPaths paths, RoMapSceneBuilder.Input input, RoMapSceneBuilder.Notes notes)
        {
            // Unity cannot add a scene beside an untitled one, so the map is built as the single scene and the scenes that
            // were open are reopened afterwards. Unsaved changes in an open scene would be lost: refuse instead (the map's
            // own scene excepted: re-conversion replaces it, and custom work belongs in the additive scene).
            var setup = EditorSceneManager.GetSceneManagerSetup();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var open = SceneManager.GetSceneAt(i);
                if (!open.isDirty) continue;
                if (open.path == paths.Scene)
                {
                    Debug.LogWarning($"[RoImport] '{paths.Scene}' was open with unsaved changes; they are discarded by re-conversion (keep custom work in the additive scene).");
                    continue;
                }
                throw new InvalidOperationException(
                    $"the open scene '{(open.path.Length > 0 ? open.path : open.name)}' has unsaved changes; save or close it first");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                RoMapSceneBuilder.Build(input, notes);
                return ctx.Writer.SaveScene(paths.Scene, scene);
            }
            finally
            {
                var reopen = setup.Where(s => !string.IsNullOrEmpty(s.path) && System.IO.File.Exists(s.path)).ToArray();
                if (reopen.Any(s => s.isLoaded))
                {
                    if (!reopen.Any(s => s.isActive)) reopen.First(s => s.isLoaded).isActive = true;
                    EditorSceneManager.RestoreSceneManagerSetup(reopen);
                }
                else
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }
        }

        /// <summary>
        /// The ground or walkability source of the map (PLAN-0006 4.1): the file the RSW names when it differs from
        /// <c>&lt;name&gt;.gnd</c> / <c>.gat</c> and exists, otherwise the default; a named file that does not exist is
        /// reported once.
        /// </summary>
        static RoPath Beside(RoImportContext ctx, RoPath map, string named, RoPath fallback)
        {
            if (string.IsNullOrEmpty(named) || !RoPath.TryParse(named, out var namedPath) || namedPath.Canonical == fallback.Canonical)
                return fallback;
            if (ctx.Source.TryGetFile(namedPath, out _)) return namedPath;
            ctx.Report.Warn(RoIssueCode.RswFileMismatch, map.Canonical, null,
                $"the map names '{named}', which does not exist; '{fallback.Canonical}' is used");
            return fallback;
        }

        static RoLedgerDependency Dependency(RoImportContext ctx, RoPath path) =>
            new RoLedgerDependency(path.Canonical, ctx.Source.TryGetFile(path, out var file) ? file.ContentHash() : string.Empty);

        static RoResult<SceneAsset> Fail(RoImportContext ctx, RoPath path, RoIssueCode code, string message)
        {
            ctx.Report.Warn(code, path.Canonical, null, message);
            return RoResult<SceneAsset>.Failed(new RoIssue(code, path.Canonical, null, message));
        }
    }
}
