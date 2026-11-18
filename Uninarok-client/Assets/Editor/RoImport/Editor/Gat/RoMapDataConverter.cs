using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Uninarok.Map;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Gat;
using Uninarok.RoImport.Core.Gnd;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Editor.Ledger;
using Uninarok.RoImport.Editor.Pipeline;
using Object = UnityEngine.Object;

namespace Uninarok.RoImport.Editor.Gat
{
    /// <summary>
    /// GAT to the map data asset (PLAN-0005 4.5; SPEC-0005 FR-1, FR-3, FR-6, FR-8). The GAT is read through GRF, the GND
    /// beside it only for its size, zoom and version (the map space and the diagonal). Everything is built in memory first;
    /// the asset is written in place (one per map, its GUID kept), then the optional cell image, then the ledger (category
    /// <see cref="Category"/>: outputs the asset and the image, dependency the GND).
    /// </summary>
    public sealed class RoMapDataConverter : IRoConverter
    {
        public const string Category = "mapdata";

        /// <summary>Converter rules; the cell table has its own <see cref="RoGatRules.RulesVersion"/>, folded in below.</summary>
        public const int RulesVersion = 1;

        /// <summary>The version recorded in the ledger: a change of the converter or of the cell table converts again.</summary>
        static int LedgerRules => RulesVersion * 1000 + RoGatRules.RulesVersion;

        public RoJobKind Kind => RoJobKind.MapData;

        /// <summary><c>.gat</c> files.</summary>
        public bool Handles(RoPath path) => path.Extension == ".gat";

        public RoResult<Object> Convert(RoImportContext ctx, RoPath path)
        {
            var result = ConvertMapData(ctx, path);
            return new RoResult<Object>(result.Status, result.Value, result.Failure);
        }

        static RoResult<MapData> ConvertMapData(RoImportContext ctx, RoPath path)
        {
            var paths = RoMapDataOutputPaths.For(ctx.Roots, path);
            if (!ctx.Source.TryGetFile(path, out var gatFile))
                return Fail(ctx, path, RoIssueCode.SourceMissing, "no such file in the source folder");
            var gndPath = path.WithExtension(".gnd");
            if (!ctx.Source.TryGetFile(gndPath, out var gndFile))
                return Fail(ctx, path, RoIssueCode.SourceMissing, $"no ground '{gndPath.Canonical}' beside it (the map space comes from it)");

            string hash = gatFile.ContentHash();
            var ledger = ctx.Ledger(Category);
            if (!ctx.Force && ledger.IsCurrent(path, hash, LedgerRules, ctx.Source, ctx.Writer)
                           && (!ctx.GatImage || ledger.Find(path).Outputs.Contains(paths.Image)))
            {
                var current = ctx.Writer.Load<MapData>(paths.Asset);
                if (current != null) return RoResult<MapData>.Skipped(current);
            }

            // 1. Read: the GAT through GRF, the GND for its map space and rules.
            if (!RoGatReader.TryRead(path, gatFile.ReadAllBytes(), out var gat, out string reason))
                return Fail(ctx, path, RoIssueCode.GatBadHeader, reason);
            if (!RoGndReader.TryRead(gndPath, gndFile.ReadAllBytes(), null, out var gnd, out reason))
                return Fail(ctx, path, RoIssueCode.CorruptFile, $"the ground '{gndPath.Canonical}' cannot be read: {reason}");
            if (!RoGndRules.TryGet(gnd.VersionMajor, gnd.VersionMinor, out var rules))
                return Fail(ctx, path, RoIssueCode.UnsupportedGndVersion,
                    $"GND version {gnd.VersionMajor}.{gnd.VersionMinor} has no verified ground rules; the map data is not converted");

            // 2. Build: pure and in memory (4.3); a grid that is not twice the ground is reported by the builder (D7).
            var space = new RoMapSpace(gnd.Width, gnd.Height, gnd.Zoom);
            var arrays = RoGatBuilder.Build(path.Canonical, gat, space, rules, ctx.Report);
            if (arrays == null)
                return RoResult<MapData>.Failed(new RoIssue(RoIssueCode.GatGndMismatch, path.Canonical, null,
                    $"the GAT has {gat.Width} x {gat.Height} cells, the GND {gnd.Width} x {gnd.Height} cubes"));
            var image = ctx.GatImage ? RoGatImage.Build(arrays.CellsX, arrays.CellsZ, arrays.Types) : null;

            // 3. Write: the asset in place, then the image, then the ledger (4.5).
            try
            {
                string name = path.FileNameWithoutExtension;
                var fresh = ScriptableObject.CreateInstance<MapData>();
                fresh.name = name;
                fresh.Assign(name, arrays.CellsX, arrays.CellsZ, arrays.CellSize, arrays.Origin, arrays.RowSign, arrays.DiagonalFlip,
                    arrays.Heights, arrays.Types, arrays.Flags, arrays.RawTypes);
                var status = ctx.Writer.UpsertObject(paths.Asset, fresh);
                var asset = ctx.Writer.Load<MapData>(paths.Asset);
                var outputs = new List<string> { paths.Asset };

                var previous = ledger.Find(path);
                if (image != null)
                {
                    status = Combine(status, RoGatImageWriter.Write(ctx, paths.Image, image));
                    outputs.Add(paths.Image);
                }
                else if (previous != null && previous.Outputs.Contains(paths.Image) && ctx.Writer.Exists(paths.Image))
                {
                    outputs.Add(paths.Image);   // an image made by an earlier request stays owned, not deleted
                }

                ledger.Record(new RoLedgerEntry
                {
                    Source = path.Canonical,
                    SourceHash = hash,
                    Rules = LedgerRules,
                    Outputs = outputs.ToArray(),
                    Dependencies = new[] { new RoLedgerDependency(gndPath.Canonical, gndFile.ContentHash()) }
                }, ctx.Report);
                return new RoResult<MapData>(status, asset);
            }
            catch (Exception e)
            {
                ledger.Forget(path);
                return Fail(ctx, path, RoIssueCode.Unexpected, $"writing the map data failed: {e.GetType().Name}: {e.Message}");
            }
        }

        /// <summary>Created if the asset was, otherwise Updated if anything changed, otherwise Skipped.</summary>
        static RoStatus Combine(RoStatus asset, RoStatus image)
        {
            if (asset == RoStatus.Created) return RoStatus.Created;
            if (asset == RoStatus.Updated || image == RoStatus.Updated || image == RoStatus.Created) return RoStatus.Updated;
            return asset;
        }

        static RoResult<MapData> Fail(RoImportContext ctx, RoPath path, RoIssueCode code, string message)
        {
            ctx.Report.Warn(code, path.Canonical, null, message);
            return RoResult<MapData>.Failed(new RoIssue(code, path.Canonical, null, message));
        }
    }
}
