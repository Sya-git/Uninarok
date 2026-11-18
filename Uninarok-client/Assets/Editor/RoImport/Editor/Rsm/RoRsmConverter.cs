using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Materials;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Rsm;
using Uninarok.RoImport.Editor.Ledger;
using Uninarok.RoImport.Editor.Materials;
using Uninarok.RoImport.Editor.Pipeline;
using Object = UnityEngine.Object;

namespace Uninarok.RoImport.Editor.Rsm
{
    /// <summary>
    /// RSM model to prefab, meshes, materials, clips and controller (PLAN-0003 4.9; SPEC-0003 FR-1 to FR-10).
    /// </summary>
    public sealed class RoRsmConverter : IRoConverter
    {
        public const string Category = "model";
        public const int RulesVersion = 1;

        public RoJobKind Kind => RoJobKind.Model;

        /// <summary><c>.rsm</c> and <c>.rsm2</c> files.</summary>
        public bool Handles(RoPath path) => path.Extension == ".rsm" || path.Extension == ".rsm2";

        /// <summary>
        /// Converts one model, or skips it when the ledger says it is current (source hash, rules version, outputs present,
        /// texture dependencies unchanged). A missing, unreadable or unsupported model is one warning and a failed result.
        /// </summary>
        public RoResult<Object> Convert(RoImportContext ctx, RoPath path)
        {
            var result = ConvertModel(ctx, path);
            return new RoResult<Object>(result.Status, result.Value, result.Failure);
        }

        static RoResult<GameObject> ConvertModel(RoImportContext ctx, RoPath path)
        {
            if (!ctx.Source.TryGetFile(path, out var file))
                return Fail(ctx, path, RoIssueCode.SourceMissing, "no such file in the source folder");

            string hash = file.ContentHash();
            var ledger = ctx.Ledger(Category);
            if (!ctx.Force && ledger.IsCurrent(path, hash, RulesVersion, ctx.Source, ctx.Writer))
            {
                var current = ctx.Writer.Load<GameObject>(RsmOutputPaths.For(ctx.Roots, path, System.Array.Empty<string>()).Prefab);
                if (current != null) return RoResult<GameObject>.Skipped(current);
            }

            if (!RoRsmReader.TryRead(path, file.ReadAllBytes(), out var rsm, out string reason))
                return Fail(ctx, path, RoIssueCode.CorruptFile, reason);
            return Write(ctx, path, rsm, hash);
        }

        /// <summary>
        /// Converts a model already read into <paramref name="rsm"/> and records it in the ledger with
        /// <paramref name="sourceHash"/>. Everything is built in memory first; outputs are written only when the whole
        /// model is built, so a model that fails leaves no partial output. Used by <see cref="Convert"/> and by tests
        /// with hand-made models.
        /// </summary>
        internal static RoResult<GameObject> Write(RoImportContext ctx, RoPath path, RoRsm rsm, string sourceHash)
        {
            string item = path.Canonical;

            // 1. Rules, tree, rest pose, meshes, animation: pure and in memory (4.1, 4.2, 4.4, 4.6, 4.8).
            if (!RsmRules.TryGet(rsm.VersionMajor, rsm.VersionMinor, out var rules))
                return Fail(ctx, path, RoIssueCode.UnsupportedRsmVersion,
                    $"RSM version {rsm.VersionMajor}.{rsm.VersionMinor} has no verified placement rules; the model is not converted");
            if (!RsmHierarchy.TryBuild(rsm, out var tree, out var treeIssue))
                return Fail(ctx, path, treeIssue.Code, $"{treeIssue.Message} ({treeIssue.Item})");
            foreach (var w in tree.Warnings) ctx.Report.Warn(w.Code, w.Item, item, w.Message);

            var rest = RsmPose.ComputeRest(rsm, tree, rules);
            int count = rsm.Nodes.Count;
            var meshData = new RsmMeshData[count];
            for (int n = 0; n < count; n++)
                meshData[n] = RsmMeshBuilder.Build(rsm.Nodes[n], rsm.ShadeType, rsm.HasShadeType, rest.Vertex[n],
                                                   RsmSpace.MirrorsWinding(rest.Vertex[n]), ctx.Report, item);
            var animation = RsmAnimationData.Build(rsm, tree, rest, rules);
            var paths = RsmOutputPaths.For(ctx.Roots, path, tree.UniqueNames);
            string modelName = path.FileNameWithoutExtension;

            // 2. Materials: shared, from the texture pipeline (4.3). Their textures are the model's dependencies.
            ctx.BeginDependencies();
            Material[][] materials;
            RoLedgerDependency[] dependencies;
            try
            {
                var byKey = new Dictionary<(int, bool), Material>();
                materials = meshData.Select(m => m.Submeshes.Select(s => MaterialOf(ctx, rsm, s, byKey, item)).ToArray()).ToArray();
            }
            finally
            {
                // always close the frame, so a failure here never leaks into the caller's dependencies
                dependencies = ctx.EndDependencies();
            }

            // 3. Write: meshes in place, then the prefab, then the ledger (4.7, 4.9).
            var ledger = ctx.Ledger(Category);
            GameObject root = null;
            try
            {
                var meshes = new Mesh[count];
                var outputs = new List<string>();
                for (int n = 0; n < count; n++)
                {
                    if (meshData[n].Submeshes.Count == 0) continue;
                    ctx.Writer.UpsertObject(paths.Meshes[n], RsmPrefabBuilder.ToMesh(tree.UniqueNames[n], meshData[n]));
                    meshes[n] = ctx.Writer.Load<Mesh>(paths.Meshes[n]);
                    outputs.Add(paths.Meshes[n]);
                }

                root = RsmPrefabBuilder.Build(modelName, tree, rest, meshes, materials);

                // Animated models: one clip per loop group, one controller, an animator on the root (4.8, D5, D10).
                if (animation.IsAnimated)
                {
                    var clips = new List<AnimationClip>();
                    for (int g = 0; g < animation.Loops.Count; g++)
                    {
                        string clipPath = paths.Clip(g);
                        string clipName = System.IO.Path.GetFileNameWithoutExtension(clipPath);
                        ctx.Writer.UpsertObject(clipPath, RsmClipBuilder.Build(clipName, animation.Loops[g], tree));
                        clips.Add(ctx.Writer.Load<AnimationClip>(clipPath));
                        outputs.Add(clipPath);
                    }
                    ctx.Writer.UpsertController(paths.Controller,
                        c => RsmControllerBuilder.Matches(c, clips), c => RsmControllerBuilder.Build(c, clips));
                    outputs.Add(paths.Controller);

                    var animator = root.AddComponent<Animator>();
                    animator.runtimeAnimatorController = ctx.Writer.Load<UnityEditor.Animations.AnimatorController>(paths.Controller);
                    animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                    animator.applyRootMotion = false;
                }

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
                return Fail(ctx, path, RoIssueCode.Unexpected, $"writing the model failed: {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
            }
        }

        /// <summary>The shared material of a submesh; a bad texture index gets the placeholder material and one warning.</summary>
        static Material MaterialOf(RoImportContext ctx, RoRsm rsm, RsmSubmesh submesh, Dictionary<(int, bool), Material> byKey, string referrer)
        {
            var key = (submesh.TextureIndex, submesh.TwoSided);
            if (byKey.TryGetValue(key, out var known)) return known;

            Material material;
            if (submesh.TextureIndex < 0 || submesh.TextureIndex >= rsm.Textures.Count)
            {
                ctx.Report.Warn(RoIssueCode.RsmBadIndex, referrer, null,
                    $"a node refers to texture {submesh.TextureIndex}, but the model has {rsm.Textures.Count}; the placeholder is used");
                material = RoPlaceholders.MissingMaterial(ctx).Value;
            }
            else
            {
                var texturePath = RoPath.Parse("texture").Combine(rsm.Textures[submesh.TextureIndex]);
                var texture = ctx.Texture(texturePath, referrer);
                texturePath = Preferred(ctx, texturePath);
                var surface = RoSurface.Opaque;
                if (texture.Status != RoStatus.Failed)
                {
                    var alpha = RoTextureConverter.AlphaOf(RoOutputPaths.Texture(ctx.Roots, texturePath));
                    if (alpha.HasValue) surface = RoMaterialFactory.SurfaceFor(alpha.Value);
                }
                material = ctx.Material(submesh.MaterialKey(texturePath, surface), referrer).Value
                           ?? RoPlaceholders.MissingMaterial(ctx).Value;
            }
            byKey.Add(key, material);
            return material;
        }

        /// <summary>
        /// The texture the pipeline actually uses: a <c>.tga</c> or <c>.png</c> with a <c>.bmp</c> of the same name beside it
        /// is converted from the <c>.bmp</c> (PLAN-0002 4.5, D19), so the material key names the <c>.bmp</c> too; otherwise two
        /// keys would own one material file (found by the T7.1 batch on izlude/iz_enchant).
        /// </summary>
        static RoPath Preferred(RoImportContext ctx, RoPath texture)
        {
            if (texture.Extension == ".bmp" || !ctx.Source.TryGetFile(texture, out _)) return texture;
            var bmp = texture.WithExtension(".bmp");
            return ctx.Source.TryGetFile(bmp, out _) ? bmp : texture;
        }

        static RoResult<GameObject> Fail(RoImportContext ctx, RoPath path, RoIssueCode code, string message)
        {
            var issue = new RoIssue(code, path.Canonical, null, message);
            ctx.Report.Warn(code, path.Canonical, null, message);
            return RoResult<GameObject>.Failed(issue);
        }
    }
}
