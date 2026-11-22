using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Uninarok.Catalog;
using Uninarok.Networking.Data;
using Uninarok.Sprites;

namespace Uninarok.Units
{
    /// <summary>
    /// Finds and loads every part of a look through the catalog (PLAN-0009 4.2; SPEC-0009 FR-1, FR-2, FR-9). A part that is not
    /// found is left out and reported with its reason; the rest loads. The parts are replaced at once when everything
    /// arrived: what a unit already shows stays loaded until then, and unchanged assets are only counted again (the source
    /// shares them). Main thread.
    /// </summary>
    public sealed class UnitLoader
    {
        readonly IUnitAssetSource _source;

        public UnitLoader(IUnitAssetSource source) => _source = source ?? throw new ArgumentNullException(nameof(source));

        public IUnitAssetSource Source => _source;

        /// <summary>
        /// Resolves <paramref name="look"/>, acquires its assets, puts them in <paramref name="into"/> (releasing what it held) and
        /// calls <paramref name="done"/> once. A newer load for the same parts supersedes an older one still in flight: the
        /// older one releases what it got and never calls back.
        /// </summary>
        public void Load(in UnitLook look, UnitParts into, Action<UnitParts, LoadReport> done)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            new Request(_source, look, into, done).Start();
        }

        /// <summary>Releases everything <paramref name="parts"/> hold and drops a load in flight.</summary>
        public void Release(UnitParts parts)
        {
            if (parts == null) return;
            parts.Request++;
            foreach (var r in parts.Held) _source.Release(r);
            parts.Held.Clear();
            parts.Clear();
            parts.Version++;
        }

        // One load: collects into its own arrays, applies at the end.
        sealed class Request
        {
            readonly IUnitAssetSource _source;
            readonly UnitLook _look;
            readonly UnitParts _into;
            readonly Action<UnitParts, LoadReport> _done;
            readonly LoadReport _report = new LoadReport();
            readonly List<AssetReference> _held = new List<AssetReference>();
            readonly SpriteAnimation[] _animations = new SpriteAnimation[UnitParts.SlotCount];
            readonly Texture2D[] _palettes = new Texture2D[UnitParts.SlotCount];
            readonly int _id;
            /// <summary>The job whose sprites are used: the look's, or the one it inherits from (D24).</summary>
            PlayerJob _job;
            SpriteRace _race;
            bool _riding, _hidden;
            float _scale = 1f, _shadowScale;
            SpriteAnimation _shadowAnimation;
            Sprite _shadowSprite;
            int _pending = 1; // the guard: nothing finishes before Start has asked for everything

            public Request(IUnitAssetSource source, in UnitLook look, UnitParts into, Action<UnitParts, LoadReport> done)
            {
                _source = source;
                _look = look;
                _into = into;
                _done = done;
                _id = ++into.Request;
            }

            public void Start()
            {
                if (_look.Kind == UnitKind.Player) Player();
                else Unit();
                if (!_hidden) Shadow();
                Done();
            }

            // ---- resolution ----

            void Unit()
            {
                int job = _look.JobId;
                if (_source.Tables != null && _source.Tables.IsHidden(job))
                {
                    _hidden = true;
                    _report.Found(PartSlot.Body, $"job id {job}: drawn as nothing");
                    return;
                }
                if (!_source.Catalog.TryGetUnit(job, out var unit))
                {
                    _report.Missing(PartSlot.Body, $"no unit with job id {job} in the catalog");
                    return;
                }
                if (unit.Sprite.IsEmpty)
                {
                    _hidden = true;
                    _report.Found(PartSlot.Body, $"job id {job} ({unit.Name}): an empty record, drawn as nothing");
                    return;
                }
                if (!unit.Sprite.HasAnimation)
                {
                    _report.Missing(PartSlot.Body, $"job id {job} ({unit.Name}): its sprite is static, not a unit animation");
                    return;
                }
                _scale = unit.Scale > 0f ? unit.Scale : 1f;
                Animation(PartSlot.Body, unit.Sprite.Animation, $"job id {job}: {unit.Name}, scale {_scale}");
            }

            void Player()
            {
                var job = _job = SpriteJob();
                if (UnitLook.IsBabyJob(_look.JobId)) _scale = UnitLook.BabyScale;
                var classes = _source.Catalog.Classes;
                var baseJob = job;
                if (classes != null)
                {
                    classes.TryGet(job, out _race, out baseJob);
                }

                if (!_source.Catalog.TryFindBody(job, out var body)) _report.Missing(PartSlot.Body, $"no body for {job} in the catalog");
                else Acquire<BodyEntry>(body, entry => Body(entry, job));

                int style = _look.HairStyle;
                if (!_source.Catalog.TryFindHead(style, out var head)) _report.Missing(PartSlot.Head, $"no hair style {style} in the catalog");
                else Acquire<HeadEntry>(head, entry => Head(entry, style));

                Equipment(PartSlot.HeadBottom, EquipmentKind.Headgear, _look.HeadBottom, 0, baseJob);
                Equipment(PartSlot.HeadMid, EquipmentKind.Headgear, _look.HeadMid, 0, baseJob);
                Equipment(PartSlot.HeadTop, EquipmentKind.Headgear, _look.HeadTop, 0, baseJob);
                Equipment(PartSlot.Garment, EquipmentKind.Garment, _look.Garment, 0, baseJob);
                Equipment(PartSlot.Weapon, EquipmentKind.Weapon, _look.Weapon, _look.WeaponItem, baseJob);
                Equipment(PartSlot.Shield, EquipmentKind.Shield, _look.Shield, _look.ShieldItem, baseJob);
            }

            // A job without a body of its own (baby, high, transcendent) uses its base job's sprites, as the client does.
            PlayerJob SpriteJob()
            {
                var job = _look.PlayerJob;
                if (!_source.Catalog.TryFindBody(job, out _) && _source.Tables != null && _source.Tables.TryGetBaseJob(_look.JobId, out int baseJob)
                    && _source.Catalog.TryFindBody((PlayerJob)baseJob, out _))
                    return (PlayerJob)baseJob;
                return job;
            }

            void Body(BodyEntry entry, PlayerJob job)
            {
                if (entry == null || !TryBodyVariant(entry, job, out var v))
                {
                    _report.Missing(PartSlot.Body, entry == null ? $"the body entry of {job} could not be loaded" : $"{entry.BaseJob} has no {_look.Sex} body");
                    return;
                }
                _riding = v.Kind == BodyVariantKind.Mount;
                if (!v.Sprite.HasAnimation)
                {
                    _report.Missing(PartSlot.Body, $"{v.Name}: no animation");
                    return;
                }
                string how = $"{_look.PlayerJob} {_look.Sex}: {v.Name} ({v.Kind})";
                if (job != _look.PlayerJob) how += $", as {job}";
                if (_scale != 1f) how += $", scale {_scale}";
                how += Palette(PartSlot.Body, v.Palettes, _look.ClothColour, "cloth colour");
                Animation(PartSlot.Body, v.Sprite.Animation, how);
            }

            // A costume style is the costume variant of the job; without one the class body shows.
            bool TryBodyVariant(BodyEntry entry, PlayerJob job, out BodyEntry.BodyVariant variant)
            {
                int style = _look.BodyStyle;
                if (style >= 1 && style <= 4)
                {
                    var wanted = (BodyVariantKind)((int)BodyVariantKind.Costume1 + style - 1);
                    foreach (var v in entry.Variants)
                        if (v.Kind == wanted && v.Job == job && v.Sex == _look.Sex)
                        {
                            variant = v;
                            return true;
                        }
                }
                return entry.TryGetVariant(job, _look.Sex, out variant);
            }

            void Head(HeadEntry entry, int style)
            {
                if (entry == null || !entry.TryGetVariant(_look.Sex, _race, out var v) || !v.Sprite.HasAnimation)
                {
                    _report.Missing(PartSlot.Head, entry == null ? $"the head entry of hair style {style} could not be loaded" : $"hair style {style} has no {_look.Sex} {_race} head");
                    return;
                }
                string how = $"hair style {style} {_look.Sex} {_race}";
                how += Palette(PartSlot.Head, v.Palettes, _look.HairColour, "hair colour");
                Animation(PartSlot.Head, v.Sprite.Animation, how);
            }

            // Colour 0 is the sprite's own; a colour the set does not have shows the sprite's own too.
            string Palette(PartSlot slot, in PaletteSet set, int colour, string what)
            {
                if (colour <= 0) return string.Empty;
                if (!set.TryGet(colour, out var reference)) return $", no {what} {colour} (own colours)";
                Acquire<Texture2D>(reference, t => _palettes[(int)slot] = t);
                return $", {what} {colour}";
            }

            void Equipment(PartSlot slot, EquipmentKind kind, int viewId, int itemId, PlayerJob baseJob)
            {
                if (viewId <= 0 && itemId <= 0) return; // none
                EquipmentEntryReference reference;
                string what;
                if (viewId > 0)
                {
                    what = itemId > 0 ? $"{kind} view id {viewId}, item {itemId}" : $"{kind} view id {viewId}";
                    if (!_source.Catalog.TryFindEquipment(kind, viewId, itemId, out reference))
                    {
                        _report.Missing(slot, $"{what} is not in the catalog");
                        return;
                    }
                }
                else
                {
                    what = $"{kind} item {itemId}";
                    if (!_source.Catalog.TryFindEquipmentByItem(itemId, out var found, out reference) || found != kind)
                    {
                        _report.Missing(slot, found != EquipmentKind.None && found != kind ? $"{what} is a {found}" : $"{what} is not in the catalog");
                        return;
                    }
                }
                Acquire<EquipmentEntry>(reference, entry => Equipment(slot, entry, what, baseJob));
            }

            void Equipment(PartSlot slot, EquipmentEntry entry, string what, PlayerJob baseJob)
            {
                var job = _job;
                if (entry == null)
                {
                    _report.Missing(slot, $"{what}: its entry could not be loaded");
                    return;
                }
                var classes = _source.Catalog.Classes;
                string on = string.Empty;
                if (!entry.TryResolve(job, _look.Sex, classes, out var animation, out var trail))
                {
                    if (baseJob == job || !entry.TryResolve(baseJob, _look.Sex, classes, out animation, out trail))
                    {
                        _report.Missing(slot, $"{what} ({entry.Name}): no animation for {job} {_look.Sex}");
                        return;
                    }
                    on = $" (as {baseJob})";
                }
                Animation(slot, animation, $"{what}: {entry.Name} for {job} {_look.Sex}{on}");
                if (slot == PartSlot.Weapon && trail != null) Animation(PartSlot.WeaponTrail, trail, $"{what}: its trail");
            }

            void Shadow()
            {
                var tables = _source.Tables;
                _shadowScale = tables != null ? tables.ShadowScaleOf(_look.JobId) : 0f;
                if (_shadowScale <= 0f || tables == null) return;
                if (tables.Shadow.HasAnimation) Acquire<SpriteAnimation>(tables.Shadow.Animation, a => _shadowAnimation = a);
                else if (tables.Shadow.HasStill) Acquire<Sprite>(tables.Shadow.Still, s => _shadowSprite = s);
            }

            // ---- loading ----

            void Animation(PartSlot slot, SpriteAnimationReference reference, string how) =>
                Acquire<SpriteAnimation>(reference, a =>
                {
                    _animations[(int)slot] = a;
                    if (a != null) _report.Found(slot, how);
                    else _report.Missing(slot, how + ": the animation could not be loaded");
                });

            void Acquire<T>(AssetReference reference, Action<T> got) where T : UnityEngine.Object
            {
                _pending++;
                _held.Add(reference);
                _source.Acquire<T>(reference, asset =>
                {
                    if (_id == _into.Request) got(asset); // superseded: ask for nothing more
                    Done();
                });
            }

            void Done()
            {
                if (--_pending > 0) return;
                if (_id != _into.Request)
                {
                    foreach (var r in _held) _source.Release(r);
                    return;
                }

                // Apply: the new assets are held before the old ones are let go.
                var old = _into.Held;
                _into.Held = _held;
                _into.Clear();
                Array.Copy(_animations, _into.Animations, _animations.Length);
                Array.Copy(_palettes, _into.Palettes, _palettes.Length);
                _into.Look = _look;
                _into.Race = _race;
                _into.Riding = _riding;
                _into.Scale = _scale;
                _into.Hidden = _hidden;
                _into.ShadowScale = _hidden || _into[PartSlot.Body] == null ? 0f : _shadowScale;
                _into.ShadowAnimation = _shadowAnimation;
                _into.ShadowSprite = _shadowSprite;
                _into.Version++;
                _done?.Invoke(_into, _report);
                foreach (var r in old) _source.Release(r);
            }
        }
    }
}
