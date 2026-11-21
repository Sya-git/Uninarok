using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Uninarok.Catalog;
using Uninarok.Networking.Data;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Editor.Sprites;
using Uninarok.Sprites;
using Object = UnityEngine.Object;

namespace Uninarok.RoImport.Editor.Catalog
{
    /// <summary>What the lookup preview asks for (PLAN-0008 4.9).</summary>
    public enum RoCatalogLookupMode
    {
        UnitJobId,
        BodyClass,
        HeadStyle,
        ItemId,
        ViewId,
        SkillId,
        EmoteId,
        EffectName,
    }

    /// <summary>One lookup: the mode and the inputs it needs.</summary>
    public sealed class RoCatalogLookupRequest
    {
        public RoCatalogLookupMode Mode;
        public int Id;
        public string Name;
        public PlayerJob Job = PlayerJob.JOB_NOVICE;
        public AccountSex Sex = AccountSex.SEX_MALE;
        public SpriteRace Race = SpriteRace.Human;
        public EquipmentKind Kind = EquipmentKind.Headgear;
    }

    /// <summary>What a lookup found: an animation or a static sprite, and a line saying how it was reached.</summary>
    public sealed class RoCatalogLookupResult
    {
        public SpriteAnimation Animation;
        public Sprite Still;
        public SpriteAnimation Trail;
        public string Description;
    }

    /// <summary>
    /// <c>Tools/Ragnarok/Catalog Lookup…</c> (SPEC-0008 FR-11; PLAN-0008 4.9): a mode, its inputs and Resolve, through the
    /// <see cref="SpriteCatalog"/> facade built from the imported index assets (editor only, no Addressables load). An
    /// animation opens in <see cref="RoSpritePreviewWindow"/>; a static sprite is shown here.
    /// </summary>
    public sealed class RoCatalogLookupWindow : EditorWindow
    {
        [SerializeField] RoCatalogLookupMode _mode = RoCatalogLookupMode.ItemId;
        [SerializeField] int _id = 2220;
        [SerializeField] string _name = "emotion";
        [SerializeField] PlayerJob _job = PlayerJob.JOB_KNIGHT;
        [SerializeField] AccountSex _sex = AccountSex.SEX_MALE;
        [SerializeField] SpriteRace _race = SpriteRace.Human;
        [SerializeField] EquipmentKind _kind = EquipmentKind.Headgear;
        string _message;
        Sprite _still;

        [MenuItem("Tools/Ragnarok/Catalog Lookup…", priority = 2)]
        public static void Open() => GetWindow<RoCatalogLookupWindow>("Catalog Lookup").Show();

        void OnGUI()
        {
            _mode = (RoCatalogLookupMode)EditorGUILayout.EnumPopup("Mode", _mode);
            switch (_mode)
            {
                case RoCatalogLookupMode.EffectName: _name = EditorGUILayout.TextField("Name", _name); break;
                case RoCatalogLookupMode.BodyClass: break;
                default: _id = EditorGUILayout.IntField(Label(_mode), _id); break;
            }
            bool player = _mode == RoCatalogLookupMode.BodyClass || _mode == RoCatalogLookupMode.ItemId || _mode == RoCatalogLookupMode.ViewId;
            if (player) _job = (PlayerJob)EditorGUILayout.EnumPopup("Class", _job);
            if (player || _mode == RoCatalogLookupMode.HeadStyle) _sex = (AccountSex)EditorGUILayout.EnumPopup("Sex", _sex);
            if (_mode == RoCatalogLookupMode.HeadStyle) _race = (SpriteRace)EditorGUILayout.EnumPopup("Race", _race);
            if (_mode == RoCatalogLookupMode.ViewId) _kind = (EquipmentKind)EditorGUILayout.EnumPopup("Kind", _kind);

            if (GUILayout.Button("Resolve"))
            {
                var request = new RoCatalogLookupRequest { Mode = _mode, Id = _id, Name = _name, Job = _job, Sex = _sex, Race = _race, Kind = _kind };
                var result = Resolve(request, new RoOutputRoots(), out _message);
                _still = result?.Still;
                if (result?.Animation != null) RoSpritePreviewWindow.Open(result.Animation);
            }
            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, MessageType.Info);
            if (_still != null)
            {
                var rect = GUILayoutUtility.GetRect(128, 128, GUILayout.ExpandWidth(false));
                var t = _still.textureRect;
                var tex = _still.texture;
                GUI.DrawTextureWithTexCoords(rect, tex, new Rect(t.x / tex.width, t.y / tex.height, t.width / tex.width, t.height / tex.height));
            }
        }

        static string Label(RoCatalogLookupMode mode)
        {
            switch (mode)
            {
                case RoCatalogLookupMode.UnitJobId: return "Job id";
                case RoCatalogLookupMode.HeadStyle: return "Style";
                case RoCatalogLookupMode.ItemId: return "Item id";
                case RoCatalogLookupMode.ViewId: return "View id";
                case RoCatalogLookupMode.SkillId: return "Skill id";
                case RoCatalogLookupMode.EmoteId: return "Emote id";
                default: return "Id";
            }
        }

        /// <summary>
        /// The facade over the index assets imported under <paramref name="roots"/>, the item and class tables when present; null
        /// when no index has been imported.
        /// </summary>
        public static SpriteCatalog LoadInEditor(RoOutputRoots roots)
        {
            var indices = RoCatalogOutputPaths.IndexedCategories
                .Select(c => AssetDatabase.LoadAssetAtPath<ScriptableObject>(RoCatalogOutputPaths.Index(roots, c)))
                .Where(i => i != null).ToList();
            if (indices.Count == 0) return null;
            return SpriteCatalog.Create(indices,
                AssetDatabase.LoadAssetAtPath<ItemSpriteTable>(RoCatalogOutputPaths.Items(roots)),
                AssetDatabase.LoadAssetAtPath<ClassTable>(RoCatalogOutputPaths.Classes(roots)));
        }

        /// <summary>Runs one lookup; null with a message when nothing is found or the catalog is not imported.</summary>
        public static RoCatalogLookupResult Resolve(RoCatalogLookupRequest request, RoOutputRoots roots, out string message)
        {
            var catalog = LoadInEditor(roots);
            if (catalog == null)
            {
                message = "The catalog is not imported: run Tools/Ragnarok/Import Catalog… first.";
                return null;
            }
            var r = Find(catalog, request, out message);
            return r;
        }

        static RoCatalogLookupResult Find(SpriteCatalog catalog, RoCatalogLookupRequest q, out string message)
        {
            switch (q.Mode)
            {
                case RoCatalogLookupMode.UnitJobId:
                    if (catalog.TryGetUnit(q.Id, out var unit)) return FromRef(unit.Sprite, $"unit {q.Id}: {unit.Name}, scale {unit.Scale}", out message);
                    break;
                case RoCatalogLookupMode.SkillId:
                    if (catalog.TryGetSkillIcon(q.Id, out var icon)) return FromRef(icon, $"skill {q.Id}", out message);
                    break;
                case RoCatalogLookupMode.EmoteId:
                    if (catalog.TryGetEmote(q.Id, out var set, out int action)) return FromRef(set, $"emote {q.Id}: action {action} of the emote set", out message);
                    break;
                case RoCatalogLookupMode.EffectName:
                    if (catalog.TryGetEffect(q.Name, out var effect)) return FromRef(effect, $"effect {q.Name}", out message);
                    break;
                case RoCatalogLookupMode.BodyClass:
                    if (catalog.TryFindBody(q.Job, out var bodyRef) && Load<BodyEntry>(bodyRef) is BodyEntry body && body.TryGetVariant(q.Job, q.Sex, out var variant))
                        return FromRef(variant.Sprite, $"body of {q.Job} {q.Sex}: {variant.Name} ({variant.Kind}), {variant.Palettes.Colours?.Length ?? 0} palettes", out message);
                    break;
                case RoCatalogLookupMode.HeadStyle:
                    if (catalog.TryFindHead(q.Id, out var headRef) && Load<HeadEntry>(headRef) is HeadEntry head && head.TryGetVariant(q.Sex, q.Race, out var hv))
                        return FromRef(hv.Sprite, $"head {q.Id} {q.Sex} {q.Race}, {hv.Palettes.Colours?.Length ?? 0} palettes", out message);
                    break;
                case RoCatalogLookupMode.ItemId:
                    if (catalog.TryFindEquipmentByItem(q.Id, out var kind, out var byItem))
                        return Equipment(catalog, byItem, q, $"item {q.Id} ({kind})", out message);
                    if (catalog.TryGetGroundItem(q.Id, true, out var ground)) return FromRef(ground, $"item {q.Id}: ground sprite (no equipment)", out message);
                    break;
                case RoCatalogLookupMode.ViewId:
                    if (catalog.TryFindEquipment(q.Kind, q.Id, 0, out var byView))
                        return Equipment(catalog, byView, q, $"{q.Kind} view id {q.Id}", out message);
                    break;
            }
            message = $"Nothing found for {q.Mode} {(q.Mode == RoCatalogLookupMode.EffectName ? q.Name : q.Id.ToString())}.";
            return null;
        }

        static RoCatalogLookupResult Equipment(SpriteCatalog catalog, EquipmentEntryReference reference, RoCatalogLookupRequest q, string what, out string message)
        {
            var entry = Load<EquipmentEntry>(reference);
            if (entry == null)
            {
                message = $"{what}: the entry asset is missing.";
                return null;
            }
            if (!entry.TryResolve(q.Job, q.Sex, catalog.Classes, out var animation, out var trail))
            {
                message = $"{what} ({entry.Name}): no slot for {q.Job} {q.Sex}; it draws nothing.";
                return null;
            }
            message = $"{what}: {entry.Name} for {q.Job} {q.Sex}" + (trail != null ? ", with a trail" : "");
            return new RoCatalogLookupResult
            {
                Animation = Load<SpriteAnimation>(animation),
                Trail = trail == null ? null : Load<SpriteAnimation>(trail),
                Description = message,
            };
        }

        static RoCatalogLookupResult FromRef(SpriteRef sprite, string what, out string message)
        {
            message = what;
            if (sprite.HasAnimation) return new RoCatalogLookupResult { Animation = Load<SpriteAnimation>(sprite.Animation), Description = what };
            if (sprite.HasStill)
            {
                string path = AssetDatabase.GUIDToAssetPath(sprite.Still.AssetGUID);
                var still = AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Sprite>().FirstOrDefault(s => s.name == sprite.Still.SubObjectName);
                return new RoCatalogLookupResult { Still = still, Description = what };
            }
            message = what + ": the record has no sprite.";
            return null;
        }

        // The asset a typed reference points at, through the AssetDatabase (editor only).
        static T Load<T>(AssetReference reference) where T : Object =>
            reference == null || string.IsNullOrEmpty(reference.AssetGUID) ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(reference.AssetGUID));
    }
}
