using System;
using System.Collections.Generic;
using UnityEngine;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Rsw;
using GrfRsw = GRF.FileFormats.RswFormat.Rsw;
using GrfModel = GRF.FileFormats.RswFormat.RswObjects.Model;
using GrfLight = GRF.FileFormats.RswFormat.RswObjects.Light;
using GrfSound = GRF.FileFormats.RswFormat.RswObjects.Sound;
using GrfEffect = GRF.FileFormats.RswFormat.RswObjects.Effect;
using GrfVector = GRF.Graphics.TkVector3;
using EncodingService = Utilities.Services.EncodingService;

namespace Uninarok.RoImport.Core.Formats
{
    /// <summary>
    /// Adapter over GRF's RSW reader (PLAN-0006 4.1, SPEC-0006 FR-1): the only RSW code that touches GRF types. It copies
    /// the GRF members that correspond to file fields (the mapping recorded by TASKS-0006 T0.2) into an
    /// <see cref="RoRsw"/>; derived and editor-only members (<c>Light.Color</c>, <c>RealRange</c>, <c>Custom_*</c>,
    /// <c>Ground</c>, <c>QuadTree</c>) are never used. It never throws for bad input: every failure is <c>false</c> with a
    /// reason. Values are raw; no rule of PLAN-0006 4.3 is applied.
    /// </summary>
    public static class RoRswReader
    {
        public static bool TryRead(RoPath path, byte[] bytes, out RoRsw rsw, out string reason)
        {
            rsw = null;
            if (bytes == null || bytes.Length == 0) return Fail("the file is empty", out reason);
            if (bytes.Length < 6 || bytes[0] != 'G' || bytes[1] != 'R' || bytes[2] != 'S' || bytes[3] != 'W')
                return Fail("not an RSW file (no 'GRSW' header)", out reason);

            // GRF decodes strings with its display encoding, UTF-8 in the editor, which loses the Korean names (CP949 in
            // the file). Read with the Korean encoding and restore the previous one (as GND and RSM).
            var previous = EncodingService.DisplayEncoding;
            try
            {
                EncodingService.DisplayEncoding = EncodingService.Korean;
                var grf = new GrfRsw((GRF.MultiType)bytes);
                rsw = Convert(grf);
            }
            catch (Exception e)
            {
                rsw = null;
                return Fail($"GRF could not read the map: {e.GetType().Name}: {e.Message}", out reason);
            }
            finally
            {
                EncodingService.DisplayEncoding = previous;
            }

            reason = null;
            return true;
        }

        static RoRsw Convert(GrfRsw grf)
        {
            var header = grf.Header ?? throw new InvalidOperationException("the header is missing");
            var water = grf.Water ?? throw new InvalidOperationException("the water block is missing");
            var light = grf.Light ?? throw new InvalidOperationException("the light block is missing");
            if (grf.Objects == null) throw new InvalidOperationException("the object list is missing");

            var models = new List<RoRswModel>();
            var lights = new List<RoRswPointLight>();
            var sounds = new List<RoRswSound>();
            var effects = new List<RoRswEffect>();
            foreach (var o in grf.Objects)
            {
                switch (o)
                {
                    case GrfModel m:
                        models.Add(new RoRswModel(m.Name ?? string.Empty, m.AnimationType, m.AnimationSpeed, m.BlockType,
                            m.ModelName ?? string.Empty, m.NodeName ?? string.Empty, V(m.Position), V(m.Rotation), V(m.Scale)));
                        break;
                    case GrfLight l:
                        var c = l.ColorVector;
                        lights.Add(new RoRswPointLight(l.Name ?? string.Empty, V(l.Position), new Color(c.X, c.Y, c.Z), l.Range));
                        break;
                    case GrfSound s:
                        sounds.Add(new RoRswSound(s.Name ?? string.Empty, s.WaveName ?? string.Empty, V(s.Position), s.Volume,
                            s.Width, s.Height, s.Range, s.Cycle));
                        break;
                    case GrfEffect e:
                        var p = e.Param ?? Array.Empty<float>();
                        effects.Add(new RoRswEffect(e.Name ?? string.Empty, V(e.Position), e.EffectNumber, e.EmitSpeed,
                            At(p, 0), At(p, 1), At(p, 2), At(p, 3)));
                        break;
                    case null:
                        throw new InvalidOperationException("an object is missing");
                }
            }

            return new RoRsw
            {
                VersionMajor = header.MajorVersion,
                VersionMinor = header.MinorVersion,
                Build = header.BuildNumber,
                GndFile = header.GroundFile ?? string.Empty,
                GatFile = header.AltitudeFile ?? string.Empty,
                Water = new RoRswWater(water.Level, water.Type, water.WaveHeight, water.WaveSpeed, water.WavePitch),
                Light = new RoRswLight(light.Longitude, light.Latitude,
                    new Color(light.DiffuseRed, light.DiffuseGreen, light.DiffuseBlue),
                    new Color(light.AmbientRed, light.AmbientGreen, light.AmbientBlue),
                    light.Intensity),
                Models = models,
                Lights = lights,
                Sounds = sounds,
                Effects = effects
            };
        }

        static Vector3 V(GrfVector v) => new Vector3(v.X, v.Y, v.Z);

        static float At(float[] values, int i) => i < values.Length ? values[i] : 0f;

        static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
