using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Sprites;
using Uninarok.RoImport.Editor.Ledger;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.Sprites;
using Object = UnityEngine.Object;

namespace Uninarok.RoImport.Editor.Sprites
{
    /// <summary>Options of one sprite conversion (PLAN-0007 4.4).</summary>
    public sealed class RoSpriteOptions
    {
        /// <summary>Write and attach the index texture, so palettes can recolour the sprite (FR-7).</summary>
        public bool Palettes;
    }

    /// <summary>
    /// SPR/ACT to a sheet and a <see cref="SpriteAnimation"/> (PLAN-0007 4.4; SPEC-0007 FR-2, FR-3, FR-8, FR-9, FR-11). The
    /// job unit is the ACT (D8); its SPR is a dependency. Everything is read, checked and compiled in memory first, so a bad
    /// file writes nothing (FR-8); then the sheet (shared by content through <see cref="RoSpriteSheetIndex"/>, D2), then the
    /// animation in place (its GUID kept), then the ledger (category <see cref="Category"/>: outputs the animation,
    /// dependencies the SPR). A static ACT (FR-11) gives the frame's sprite and no animation.
    /// </summary>
    public sealed class RoSpriteConverter : IRoConverter
    {
        public const string Category = "sprite";

        /// <summary>Converter rules: a change of <see cref="RoSpriteRules"/> or of the compiler raises it.</summary>
        public const int RulesVersion = 1;

        /// <summary>The version recorded in the ledger: a change of the converter or of the sheet rules converts again.</summary>
        static int LedgerRules => RulesVersion * 1000 + RoSpriteSheetWriter.Rules;

        public const string ShaderName = "Uninarok/RoSprite";

        public RoJobKind Kind => RoJobKind.Sprite;

        /// <summary><c>.act</c> files.</summary>
        public bool Handles(RoPath path) => path.Extension == ".act";

        /// <summary>SPR versions with verified rules (TASKS-0007 T0.1: the data set has 2.0 and 2.1).</summary>
        public static bool IsSupportedSpr(int major, int minor) => major == 2 && minor >= 0 && minor <= 1;

        /// <summary>ACT versions with verified rules (the data set has 2.0, 2.1, 2.3, 2.4 and 2.5).</summary>
        public static bool IsSupportedAct(int major, int minor) => major == 2 && minor >= 0 && minor <= 5;

        /// <summary>Pairs the ACT with the SPR of the same folder and name (plan §1).</summary>
        public RoResult<Object> Convert(RoImportContext ctx, RoPath act) =>
            Convert(ctx, act, act.WithExtension(".spr"), new RoSpriteOptions { Palettes = ctx.SpritePalettes });

        /// <summary>Converts <paramref name="act"/> against the images of <paramref name="spr"/> (any SPR, act-reference §6).</summary>
        public static RoResult<Object> Convert(RoImportContext ctx, RoPath act, RoPath spr, RoSpriteOptions options)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            options = options ?? new RoSpriteOptions { Palettes = ctx.SpritePalettes };
            string memoKey = "sprite:" + act.Canonical + "|" + spr.Canonical + (options.Palettes ? "|p" : "");
            // A remembered success whose asset was unloaded (RoBatch unloads unused assets between chunks) is looked up again:
            // the ledger says it is current, so that is a cheap reload, not a conversion (found by the catalog, TASKS-0008 T7.2).
            if (ctx.TryGetMemo(memoKey, out RoResult<Object> known) && (known.Status == RoStatus.Failed || known.Value != null))
                return known.Status == RoStatus.Failed ? known : RoResult<Object>.Skipped(known.Value);

            var result = Run(ctx, act, spr, options);
            ctx.Memo(memoKey, result);
            return result;
        }

        static RoResult<Object> Run(RoImportContext ctx, RoPath act, RoPath spr, RoSpriteOptions options)
        {
            if (!ctx.Source.TryGetFile(act, out var actFile))
                return Fail(ctx, act, RoIssueCode.SourceMissing, "no such file in the source folder");
            if (!ctx.Source.TryGetFile(spr, out var sprFile))
                return Fail(ctx, act, RoIssueCode.SourceMissing, $"no images '{spr.Canonical}' for it");

            string actHash = actFile.ContentHash();
            string sprHash = sprFile.ContentHash();
            var ledger = ctx.Ledger(Category);
            var index = SheetIndex(ctx);
            string animPath = RoSpriteOutputPaths.Animation(ctx.Roots, act);

            // 0. Current: the ledger says so, the sheet is there (with its index texture when palettes are asked for).
            var sheetEntry = index.Find(sprHash);
            bool sheetCurrent = sheetEntry != null && sheetEntry.Rules == RoSpriteSheetWriter.Rules && ctx.Writer.Exists(sheetEntry.Sheet)
                                && (!options.Palettes || sheetEntry.HasIndex);
            if (!ctx.Force && sheetCurrent && ledger.IsCurrent(act, actHash, LedgerRules, ctx.Source, ctx.Writer)
                && ledger.Find(act).Dependencies.Any(d => d.Source == spr.Canonical))
            {
                var current = LoadCurrent(ctx, ledger.Find(act), sheetEntry, act, actFile);
                if (current != null) return RoResult<Object>.Skipped(current);
            }

            // 1. Read and check, in memory (FR-8). The header version first: GRF itself refuses some old versions.
            byte[] sprBytes = sprFile.ReadAllBytes(), actBytes = actFile.ReadAllBytes();
            if (RoSprReader.TryReadVersion(sprBytes, out int major, out int minor) && !IsSupportedSpr(major, minor))
                return Fail(ctx, act, RoIssueCode.UnsupportedSprVersion, $"SPR version {major}.{minor} of '{spr.Canonical}' has no verified rules");
            if (RoActReader.TryReadVersion(actBytes, out major, out minor) && !IsSupportedAct(major, minor))
                return Fail(ctx, act, RoIssueCode.UnsupportedActVersion, $"ACT version {major}.{minor} has no verified rules");
            if (!RoSprReader.TryRead(spr, sprBytes, out var sprite, out string reason))
                return Fail(ctx, act, RoIssueCode.CorruptFile, $"the images '{spr.Canonical}' cannot be read: {reason}");
            if (!RoActReader.TryRead(act, actBytes, out var animation, out reason))
                return Fail(ctx, act, RoIssueCode.CorruptFile, reason);
            if (!RoActCompiler.TryCompile(animation, sprite.Indexed.Count, sprite.TrueColor.Count, out var compiled, out reason))
                return Fail(ctx, act, RoIssueCode.SpriteIndexOutOfRange, $"{reason} ('{spr.Canonical}')");
            var sizes = Enumerable.Range(0, sprite.SlotCount).Select(i => new Vector2Int(sprite.Slot(i).Width, sprite.Slot(i).Height)).ToArray();
            if (!RoSpriteSheetPacker.TryPack(sizes, out var layout))
                return Fail(ctx, act, RoIssueCode.SpriteSheetTooLarge, $"the frames of '{spr.Canonical}' do not fit one {RoSpriteSheetPacker.MaxSize} sheet");
            ReportUnknownEvents(ctx, act, compiled.Events);

            bool isStatic = RoSpriteRules.IsStatic(animation) && DrawnLayer(animation, out _);
            RoSheetSprite? offsetSprite = isStatic ? OffsetSprite(animation, sprite) : null;

            try
            {
                // 2. The sheet, shared by content (FR-2).
                string sheetPath = sheetCurrent ? sheetEntry.Sheet : RoSpriteOutputPaths.Sheet(ctx.Roots, spr, sprHash);
                bool withIndex = options.Palettes || (sheetEntry?.HasIndex ?? false);
                var sheetStatus = RoStatus.Skipped;
                if (!sheetCurrent || ctx.Force)
                {
                    sheetStatus = RoSpriteSheetWriter.Write(ctx.Writer, ctx.Png, sheetPath, sprite, layout, RoSpriteRules.PixelsPerUnit, withIndex,
                        offsetSprite.HasValue ? new[] { offsetSprite.Value } : null);
                }
                else if (offsetSprite.HasValue && RoSpriteSheetWriter.EnsureSprite(sheetPath, layout, offsetSprite.Value))
                {
                    sheetStatus = RoStatus.Updated;
                }
                index.Record(new RoSheetEntry { Hash = sprHash, Sheet = sheetPath, Source = sheetEntry?.Source ?? spr.Canonical, HasIndex = withIndex, Rules = RoSpriteSheetWriter.Rules, IndexedCount = sprite.Indexed.Count });
                EnsureMaterial(ctx);

                var dependencies = new[] { new RoLedgerDependency(spr.Canonical, sprHash) };

                // 3a. Static: the frame's sprite, no animation (FR-11).
                if (isStatic)
                {
                    DrawnLayer(animation, out var layer);
                    int slot = RoSpriteRules.Slot(layer.SpriteType, layer.SpriteIndex, sprite.Indexed.Count);
                    string name = offsetSprite?.Name ?? RoSpriteSheetWriter.SpriteName(slot);
                    var staticSprite = RoSpriteSheetWriter.LoadSprite(sheetPath, name);
                    if (staticSprite == null)
                        return Fail(ctx, act, RoIssueCode.Unexpected, $"the sheet '{sheetPath}' has no sprite '{name}'");
                    ledger.Record(new RoLedgerEntry { Source = act.Canonical, SourceHash = actHash, Rules = LedgerRules, Dependencies = dependencies }, ctx.Report);
                    return new RoResult<Object>(sheetStatus, staticSprite);
                }

                // 3b. The animation, in place (FR-3, FR-9).
                var sprites = RoSpriteSheetWriter.LoadSprites(sheetPath, sprite.SlotCount);
                var fresh = ScriptableObject.CreateInstance<SpriteAnimation>();
                fresh.name = act.FileNameWithoutExtension;
                compiled.ApplyTo(fresh, sprites, RoSpriteRules.PixelsPerUnit);
                var status = ctx.Writer.UpsertObject(animPath, fresh);
                var asset = ctx.Writer.Load<SpriteAnimation>(animPath);
                if (status == RoStatus.Skipped && sheetStatus != RoStatus.Skipped) status = RoStatus.Updated;

                if (!ledger.Record(new RoLedgerEntry
                    {
                        Source = act.Canonical,
                        SourceHash = actHash,
                        Rules = LedgerRules,
                        Outputs = new[] { animPath },
                        Dependencies = dependencies
                    }, ctx.Report))
                    return RoResult<Object>.Failed(new RoIssue(RoIssueCode.OutputCollision, act.Canonical, null, $"'{animPath}' belongs to another source"), asset);
                return new RoResult<Object>(status, asset);
            }
            catch (Exception e)
            {
                ledger.Forget(act);
                return Fail(ctx, act, RoIssueCode.Unexpected, $"writing the sprite failed: {e.GetType().Name}: {e.Message}");
            }
        }

        /// <summary>The value of a current conversion: the animation, or the static sprite named by the ACT.</summary>
        static Object LoadCurrent(RoImportContext ctx, RoLedgerEntry entry, RoSheetEntry sheet, RoPath act, Core.Source.RoSourceFile actFile)
        {
            if (entry.Outputs.Length > 0) return ctx.Writer.Load<SpriteAnimation>(entry.Outputs[0]);
            if (!RoActReader.TryRead(act, actFile.ReadAllBytes(), out var animation, out _) || !DrawnLayer(animation, out var layer)) return null;
            int slot = RoSpriteRules.Slot(layer.SpriteType, layer.SpriteIndex, sheet.IndexedCount);
            string name = layer.OffsetX != 0 || layer.OffsetY != 0
                ? RoSpriteSheetWriter.OffsetSpriteName(slot, layer.OffsetX, layer.OffsetY)
                : RoSpriteSheetWriter.SpriteName(slot);
            return RoSpriteSheetWriter.LoadSprite(sheet.Sheet, name);
        }

        static bool DrawnLayer(RoAct act, out RoActLayer layer)
        {
            layer = default;
            if (act.Actions.Count == 0 || act.Actions[0].Frames.Count == 0) return false;
            foreach (var l in act.Actions[0].Frames[0].Layers)
                if (!l.IsEmpty)
                {
                    layer = l;
                    return true;
                }
            return false;
        }

        /// <summary>The offset sprite of a static ACT whose layer is not at (0, 0), or null.</summary>
        static RoSheetSprite? OffsetSprite(RoAct act, RoSpr spr)
        {
            if (!DrawnLayer(act, out var layer) || (layer.OffsetX == 0 && layer.OffsetY == 0)) return null;
            int slot = RoSpriteRules.Slot(layer.SpriteType, layer.SpriteIndex, spr.Indexed.Count);
            var frame = spr.Slot(slot);
            return new RoSheetSprite(RoSpriteSheetWriter.OffsetSpriteName(slot, layer.OffsetX, layer.OffsetY), slot,
                RoSpriteRules.StaticPivot(frame.Width, frame.Height, layer.OffsetX, layer.OffsetY));
        }

        /// <summary>The run's sheet index, loaded once and saved with the ledgers (D14).</summary>
        static RoSpriteSheetIndex SheetIndex(RoImportContext ctx)
        {
            const string key = "sprite-sheet-index";
            if (ctx.TryGetMemo(key, out RoSpriteSheetIndex index)) return index;
            index = RoSpriteSheetIndex.Load(ctx.LedgerFolder);
            ctx.Memo(key, index);
            ctx.SaveWithLedgers(() => index.Save());
            return index;
        }

        /// <summary>The shared sprite material (plan §3), created or updated once per run.</summary>
        static void EnsureMaterial(RoImportContext ctx)
        {
            const string key = "sprite-material";
            if (ctx.TryGetMemo(key, out bool _)) return;
            ctx.Memo(key, true);
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                ctx.Report.Warn(RoIssueCode.ShaderMissing, ShaderName, null, $"shader '{ShaderName}' not found; the sprite material is not written");
                return;
            }
            ctx.Writer.UpsertObject(RoSpriteOutputPaths.DefaultMaterial(ctx.Roots), new Material(shader) { name = "sprite_default" });
        }

        /// <summary>Event names that are neither <c>atk</c> nor a sound: kept, reported once each as information (plan 4.4).</summary>
        static void ReportUnknownEvents(RoImportContext ctx, RoPath act, IEnumerable<string> events)
        {
            foreach (var e in events)
            {
                if (e == "atk" || e.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(e)) continue;
                string key = "sprite-event:" + e;
                if (ctx.TryGetMemo(key, out bool _)) continue;
                ctx.Memo(key, true);
                Debug.Log($"[RoImport] {act.Canonical}: event '{e}' is neither 'atk' nor a sound; kept as is");
            }
        }

        static RoResult<Object> Fail(RoImportContext ctx, RoPath act, RoIssueCode code, string message)
        {
            ctx.Report.Warn(code, act.Canonical, null, message);
            return RoResult<Object>.Failed(new RoIssue(code, act.Canonical, null, message));
        }
    }
}
