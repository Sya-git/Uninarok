using System;
using System.Collections.Generic;
using UnityEngine;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Gnd;
using Uninarok.RoImport.Core.Materials;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Editor.Ledger;
using Uninarok.RoImport.Editor.Materials;
using Uninarok.RoImport.Editor.Pipeline;
using Object = UnityEngine.Object;

namespace Uninarok.RoImport.Editor.Gnd
{
    /// <summary>
    /// GND ground to chunk meshes and a ground prefab (PLAN-0004 4.6; SPEC-0004 FR-1, FR-6). Everything is built in memory
    /// first; outputs are written only when the whole ground is built, meshes in place, then the prefab, then the ledger
    /// (category <see cref="Category"/>: outputs the prefab and meshes, dependencies the textures).
    /// </summary>
    public sealed class RoGndConverter : IRoConverter
    {
        public const string Category = "ground";
        public const int RulesVersion = 2;   // 2: ground textures and materials of its own, Clamp wrap (D17)

        public RoJobKind Kind => RoJobKind.Ground;

        /// <summary><c>.gnd</c> files.</summary>
        public bool Handles(RoPath path) => path.Extension == ".gnd";

        /// <summary>
        /// Converts one ground, or skips it when the ledger says it is current (source hash, rules version, outputs present,
        /// texture dependencies unchanged). A missing, unreadable or unsupported ground is one warning and a failed result.
        /// </summary>
        public RoResult<Object> Convert(RoImportContext ctx, RoPath path)
        {
            var result = ConvertGround(ctx, path);
            return new RoResult<Object>(result.Status, result.Value, result.Failure);
        }

        static RoResult<GameObject> ConvertGround(RoImportContext ctx, RoPath path)
        {
            if (!ctx.Source.TryGetFile(path, out var file))
                return Fail(ctx, path, RoIssueCode.SourceMissing, "no such file in the source folder");

            string hash = file.ContentHash();
            if (!ctx.Force && ctx.Ledger(Category).IsCurrent(path, hash, RulesVersion, ctx.Source, ctx.Writer))
            {
                var current = ctx.Writer.Load<GameObject>(RoGndOutputPaths.For(ctx.Roots, path).Prefab);
                if (current != null) return RoResult<GameObject>.Skipped(current);
            }

            if (!RoGndReader.TryRead(path, file.ReadAllBytes(), ctx.Report, out var gnd, out string reason))
                return Fail(ctx, path, RoIssueCode.CorruptFile, reason);
            return Write(ctx, path, gnd, hash);
        }

        /// <summary>
        /// Converts a ground already read into <paramref name="gnd"/> and records it in the ledger with
        /// <paramref name="sourceHash"/> (D14). Used by <see cref="Convert"/> and by tests with maps built in code.
        /// </summary>
        internal static RoResult<GameObject> Write(RoImportContext ctx, RoPath path, RoGnd gnd, string sourceHash)
        {
            string item = path.Canonical;

            // 1. Rules and meshes: pure and in memory (4.3, 4.4).
            if (!RoGndRules.TryGet(gnd.VersionMajor, gnd.VersionMinor, out var rules))
                return Fail(ctx, path, RoIssueCode.UnsupportedGndVersion,
                    $"GND version {gnd.VersionMajor}.{gnd.VersionMinor} has no verified ground rules; the ground is not converted");
            var chunks = RoGndMeshBuilder.Build(gnd, rules, ctx.Report);
            var paths = RoGndOutputPaths.For(ctx.Roots, path);

            // 2. Materials: shared, one per texture, from the texture pipeline (4.5). Their textures are the dependencies.
            ctx.BeginDependencies();
            Material[][] materials;
            RoLedgerDependency[] dependencies;
            try
            {
                var byTexture = new Dictionary<int, Material>();
                materials = new Material[chunks.Count][];
                for (int c = 0; c < chunks.Count; c++)
                {
                    var subs = chunks[c].Submeshes;
                    materials[c] = new Material[subs.Count];
                    for (int s = 0; s < subs.Count; s++) materials[c][s] = MaterialOf(ctx, gnd, subs[s].Texture, byTexture, item);
                }
            }
            finally
            {
                dependencies = ctx.EndDependencies();
            }

            // 3. Write: meshes in place, then the prefab, then the ledger (4.6).
            var ledger = ctx.Ledger(Category);
            GameObject root = null;
            try
            {
                var meshes = new Mesh[chunks.Count];
                var outputs = new List<string>();
                for (int c = 0; c < chunks.Count; c++)
                {
                    string meshPath = paths.Chunk(chunks[c].ChunkColumn, chunks[c].ChunkRow);
                    string meshName = System.IO.Path.GetFileNameWithoutExtension(meshPath);
                    ctx.Writer.UpsertObject(meshPath, RoGndPrefabBuilder.ToMesh(meshName, chunks[c]));
                    meshes[c] = ctx.Writer.Load<Mesh>(meshPath);
                    outputs.Add(meshPath);
                }

                root = RoGndPrefabBuilder.Build(System.IO.Path.GetFileNameWithoutExtension(paths.Prefab), chunks, meshes, materials);
                var saved = ctx.Writer.SavePrefab(paths.Prefab, root);
                if (saved.Status == RoStatus.Failed)
                {
                    ledger.Forget(path);
                    return Fail(ctx, path, saved.Failure.Code, saved.Failure.Message);
                }
                outputs.Add(paths.Prefab);

                ledger.Record(new RoLedgerEntry
                {
                    Source = item,
                    SourceHash = sourceHash ?? string.Empty,
                    Rules = RulesVersion,
                    Outputs = outputs.ToArray(),
                    Dependencies = dependencies
                }, ctx.Report);
                return new RoResult<GameObject>(saved.Status, saved.Value);
            }
            catch (Exception e)
            {
                ledger.Forget(path);
                return Fail(ctx, path, RoIssueCode.Unexpected, $"writing the ground failed: {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The ground material of a texture, on the ground's Clamp copy of it (4.5, D17); a missing texture's material shows
        /// the placeholder.
        /// </summary>
        static Material MaterialOf(RoImportContext ctx, RoGnd gnd, int textureIndex, Dictionary<int, Material> byTexture, string referrer)
        {
            if (byTexture.TryGetValue(textureIndex, out var known)) return known;

            var texturePath = RoPath.Parse("texture").Combine(gnd.Textures[textureIndex]);
            var texture = ctx.GroundTexture(texturePath, referrer);
            var surface = RoSurface.Opaque;
            if (texture.Status != RoStatus.Failed)
            {
                var alpha = RoTextureConverter.AlphaOf(RoOutputPaths.GroundTexture(ctx.Roots, texturePath));
                if (alpha.HasValue) surface = RoMaterialFactory.SurfaceFor(alpha.Value);
            }
            var material = ctx.Material(new RoMaterialKey(texturePath, surface, false, ground: true), referrer).Value
                           ?? RoPlaceholders.MissingMaterial(ctx).Value;
            byTexture.Add(textureIndex, material);
            return material;
        }

        static RoResult<GameObject> Fail(RoImportContext ctx, RoPath path, RoIssueCode code, string message)
        {
            ctx.Report.Warn(code, path.Canonical, null, message);
            return RoResult<GameObject>.Failed(new RoIssue(code, path.Canonical, null, message));
        }
    }
}
