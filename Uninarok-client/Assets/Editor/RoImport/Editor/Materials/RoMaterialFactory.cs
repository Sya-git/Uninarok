using System;
using UnityEngine;
using UnityEngine.Rendering;
using Uninarok.RoImport.Core.Imaging;
using Uninarok.RoImport.Core.Materials;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Editor.Ledger;
using Uninarok.RoImport.Editor.Pipeline;

namespace Uninarok.RoImport.Editor.Materials
{
    /// <summary>
    /// Shared, lit URP materials (plan 4.6, D10, D11; SPEC-0002 FR-9). One material per <see cref="RoMaterialKey"/>, at
    /// <c>Art/_materials/&lt;texture&gt;__&lt;suffix&gt;.mat</c>, written in place through the writer and recorded in the
    /// <c>material</c> ledger. A ground key's material uses the ground's Clamp texture and is never given to a model
    /// (PLAN-0004 D17). Every property is set explicitly in <see cref="Setup"/>, independent of the material inspector.
    /// </summary>
    public static class RoMaterialFactory
    {
        public const string Category = "material";
        public const string ShaderName = "Universal Render Pipeline/Lit";

        /// <summary>Rules version of the material setup; bump it when <see cref="Setup"/> changes (plan 4.8).</summary>
        public const int Rules = 1;

        public static RoSurface SurfaceFor(RoTextureAlpha alpha)
        {
            switch (alpha)
            {
                case RoTextureAlpha.None: return RoSurface.Opaque;
                case RoTextureAlpha.Keyed: return RoSurface.Cutout;
                case RoTextureAlpha.Partial: return RoSurface.Blend;
                default: throw new ArgumentOutOfRangeException(nameof(alpha), alpha, null);
            }
        }

        public static RoResult<Material> GetOrCreate(RoImportContext ctx, RoMaterialKey key) => GetOrCreate(ctx, key, null);

        internal static RoResult<Material> GetOrCreate(RoImportContext ctx, RoMaterialKey key, string referrer)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (key.Ground && IsModel(referrer))
                throw new ArgumentException($"'{referrer}' is a model and asks for the ground material '{key}'; models never use ground materials (PLAN-0004 D17).", nameof(key));
            string memoKey = Category + ":" + key;
            if (ctx.TryGetMemo(memoKey, out Material done)) return RoResult<Material>.Skipped(done);

            // A missing, unsupported or corrupt texture comes back Failed with the placeholder as its value (warned once by
            // ctx.Texture; plan 4.6, 4.12). Its hash is recorded empty, so the material is rebuilt once the real texture works.
            var texture = key.Ground ? ctx.GroundTexture(key.Texture, referrer) : ctx.Texture(key.Texture, referrer);
            bool placeholder = texture.Status == RoStatus.Failed;
            if (placeholder && texture.Value == null) texture = RoPlaceholders.MissingTexture(ctx);

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                ctx.Report.Warn(RoIssueCode.ShaderMissing, key.ToString(), referrer, $"shader '{ShaderName}' not found");
                return RoResult<Material>.Failed(new RoIssue(RoIssueCode.ShaderMissing, key.ToString(), referrer, $"shader '{ShaderName}' not found"));
            }

            string path = RoOutputPaths.Material(ctx.Roots, key);
            var id = RoPath.Parse(key.ToString());
            string hash = !placeholder && ctx.Source.TryGetFile(key.Texture, out var file) ? file.ContentHash() : string.Empty;
            var ledger = ctx.Ledger(Category);

            if (!ctx.Force && ledger.IsCurrent(id, hash, Rules, ctx.Source, ctx.Writer))
            {
                var current = ctx.Writer.Load<Material>(path);
                if (current != null)
                {
                    ctx.Memo(memoKey, current);
                    return RoResult<Material>.Skipped(current);
                }
            }

            var fresh = new Material(shader);
            Setup(fresh, key.Surface, key.TwoSided, texture.Value);
            var status = ctx.Writer.UpsertObject(path, fresh);
            var material = ctx.Writer.Load<Material>(path);
            ledger.Record(new RoLedgerEntry { Source = id.Canonical, SourceHash = hash, Rules = Rules, Outputs = new[] { path } }, ctx.Report);

            ctx.Memo(memoKey, material);
            return new RoResult<Material>(status, material);
        }

        /// <summary>A model referrer: <c>.rsm</c> or <c>.rsm2</c>.</summary>
        static bool IsModel(string referrer) =>
            referrer != null && RoPath.TryParse(referrer, out var path) && (path.Extension == ".rsm" || path.Extension == ".rsm2");

        /// <summary>
        /// The whole material setup, in one place (plan 4.6 table, D10). Besides the table it sets what URP itself writes
        /// when it saves a Lit material (render type tag, disabled passes), so a rebuilt material compares equal to the saved
        /// one and a rerun rewrites nothing.
        /// </summary>
        internal static void Setup(Material m, RoSurface surface, bool twoSided, Texture texture)
        {
            m.SetTexture("_BaseMap", texture);
            m.SetTexture("_MainTex", texture);           // URP keeps the legacy names in sync on save
            m.SetColor("_BaseColor", Color.white);
            m.SetColor("_Color", Color.white);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 0f);
            m.SetFloat("_SpecularHighlights", 0f);
            m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            m.SetFloat("_Cull", (float)(twoSided ? CullMode.Off : CullMode.Back));
            m.doubleSidedGI = twoSided;                  // URP sets it from _Cull on save
            m.SetFloat("_AlphaToMask", surface == RoSurface.Cutout ? 1f : 0f);   // URP enables it with alpha clipping

            switch (surface)
            {
                case RoSurface.Opaque:
                    m.SetFloat("_Surface", 0f);
                    m.SetFloat("_AlphaClip", 0f);
                    SetBlend(m, BlendMode.One, BlendMode.Zero, zWrite: true);
                    m.DisableKeyword("_ALPHATEST_ON");
                    m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.SetOverrideTag("RenderType", "Opaque");
                    m.renderQueue = (int)RenderQueue.Geometry;                 // 2000
                    break;
                case RoSurface.Cutout:
                    m.SetFloat("_Surface", 0f);
                    m.SetFloat("_AlphaClip", 1f);
                    m.SetFloat("_Cutoff", 0.5f);
                    SetBlend(m, BlendMode.One, BlendMode.Zero, zWrite: true);
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.SetOverrideTag("RenderType", "TransparentCutout");
                    m.renderQueue = (int)RenderQueue.AlphaTest;                // 2450
                    break;
                case RoSurface.Blend:
                    m.SetFloat("_Surface", 1f);
                    m.SetFloat("_AlphaClip", 0f);
                    SetBlend(m, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, zWrite: false);
                    m.DisableKeyword("_ALPHATEST_ON");
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.SetOverrideTag("RenderType", "Transparent");
                    m.renderQueue = (int)RenderQueue.Transparent;              // 3000
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(surface), surface, null);
            }

            // URP 17 defaults to "preserve specular" for transparent surfaces, which turns plain alpha blending into
            // premultiplied (One / OneMinusSrcAlpha, _ALPHAPREMULTIPLY_ON). Off keeps the plan's SrcAlpha / OneMinusSrcAlpha.
            m.SetFloat("_BlendModePreserveSpecular", 0f);
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            // Passes as URP sets them on save: transparent surfaces write no depth prepass and cast no shadow.
            bool transparent = surface == RoSurface.Blend;
            m.SetShaderPassEnabled("DepthOnly", !transparent);
            m.SetShaderPassEnabled("SHADOWCASTER", !transparent);
            m.SetShaderPassEnabled("MOTIONVECTORS", false);
        }

        static void SetBlend(Material m, BlendMode src, BlendMode dst, bool zWrite)
        {
            m.SetFloat("_SrcBlend", (float)src);
            m.SetFloat("_DstBlend", (float)dst);
            // Alpha channel factors as URP sets them: One / Zero when opaque, One / OneMinusSrcAlpha when blended.
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)(dst == BlendMode.Zero ? BlendMode.Zero : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", zWrite ? 1f : 0f);
        }
    }
}
