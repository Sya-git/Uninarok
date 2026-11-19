using System;
using System.Collections.Generic;
using UnityEngine;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Sprites;
using GrfAct = GRF.FileFormats.ActFormat.Act;
using EncodingService = Utilities.Services.EncodingService;

namespace Uninarok.RoImport.Core.Formats
{
    /// <summary>
    /// Adapter over GRF's ACT reader (PLAN-0007 4.1, SPEC-0007 FR-3, FR-8): the only ACT code that touches GRF types. It is
    /// given the ACT bytes only (<c>new Act(bytes)</c>); pairing with an SPR is the converter's job. It copies the members
    /// recorded by TASKS-0007 T0.1 and never uses the derived ones (<c>Interval</c>, layer <c>Width</c>/<c>Height</c>,
    /// <c>Angle</c>). It never throws: every failure is <c>false</c> with a reason. Values are raw.
    /// </summary>
    public static class RoActReader
    {
        public static bool TryRead(RoPath path, byte[] bytes, out RoAct act, out string reason)
        {
            act = null;
            if (bytes == null || bytes.Length == 0) return Fail("the file is empty", out reason);
            if (bytes.Length < 4 || bytes[0] != 'A' || bytes[1] != 'C') return Fail("not an ACT file (no 'AC' header)", out reason);

            // Event names are CP949 like the other formats; GRF decodes with its display encoding (as RSW, GND and RSM).
            var previous = EncodingService.DisplayEncoding;
            try
            {
                EncodingService.DisplayEncoding = EncodingService.Korean;
                act = Convert(new GrfAct((GRF.MultiType)bytes));
            }
            catch (Exception e)
            {
                act = null;
                return Fail($"GRF could not read the animation: {e.GetType().Name}: {e.Message}", out reason);
            }
            finally
            {
                EncodingService.DisplayEncoding = previous;
            }

            reason = null;
            return true;
        }

        static RoAct Convert(GrfAct grf)
        {
            if (grf.Actions == null) throw new InvalidOperationException("the action list is missing");

            var actions = new List<RoActAction>(grf.Actions.Count);
            foreach (var a in grf.Actions)
            {
                if (a?.Frames == null) throw new InvalidOperationException("an action is missing");
                var frames = new List<RoActFrame>(a.Frames.Count);
                foreach (var f in a.Frames)
                {
                    if (f?.Layers == null) throw new InvalidOperationException("a frame is missing");
                    var layers = new RoActLayer[f.Layers.Count];
                    for (int i = 0; i < layers.Length; i++)
                    {
                        var l = f.Layers[i] ?? throw new InvalidOperationException("a layer is missing");
                        layers[i] = new RoActLayer((int)l.SpriteType, l.SpriteIndex, l.OffsetX, l.OffsetY, l.Mirror, l.ScaleX, l.ScaleY,
                            l.Rotation, new Color32(l.Color.R, l.Color.G, l.Color.B, l.Color.A));
                    }

                    var anchors = new Vector2Int[f.Anchors?.Count ?? 0];
                    for (int i = 0; i < anchors.Length; i++) anchors[i] = new Vector2Int(f.Anchors[i].OffsetX, f.Anchors[i].OffsetY);

                    frames.Add(new RoActFrame { Layers = layers, EventId = f.SoundId, Anchors = anchors });
                }
                actions.Add(new RoActAction { AnimationSpeed = a.AnimationSpeed, Frames = frames });
            }

            return new RoAct
            {
                VersionMajor = grf.Header.MajorVersion,
                VersionMinor = grf.Header.MinorVersion,
                Actions = actions,
                Events = grf.SoundFiles != null ? new List<string>(grf.SoundFiles) : new List<string>()
            };
        }

        static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
