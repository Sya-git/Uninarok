using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Uninarok.RoImport.Core.Effects;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using GrfStr = GRF.FileFormats.StrFormat.Str;
using GrfStrHeader = GRF.FileFormats.StrFormat.StrHeader;
using GrfStrKeyFrame = GRF.FileFormats.StrFormat.StrKeyFrame;
using GrfByteReader = GRF.IO.ByteReader;
using EncodingService = Utilities.Services.EncodingService;

namespace Uninarok.RoImport.Core.Formats
{
    /// <summary>
    /// Adapter over GRF's STR reader (PLAN-0012 4.1, D24, SPEC-0012 FR-2, FR-10): the only STR code that touches GRF types.
    /// Every byte of the file is parsed by GRF; this class never reads, indexes or converts the bytes itself. The version
    /// comes from GRF's own header (<c>StrHeader</c>, which also checks the magic) and is checked against
    /// <see cref="SupportedVersion"/> before the body is trusted, because GRF reads any version with the 0x94 layout
    /// (str-reference.md §3, §9). It copies the members mapped by TASKS-0012 T0.1 and keeps values raw, except the angle,
    /// which GRF gives in degrees and which is turned back into the file's units (1024 per turn). It never throws: every
    /// failure is <c>false</c> with an issue code and a reason. Main thread only (it swaps GRF's global display encoding).
    /// </summary>
    public static class RoStrReader
    {
        /// <summary>The one version with verified rules (all 2,995 files of the data, str-reference.md §5).</summary>
        public const int SupportedVersion = 0x94;

        /// <summary>File angle units per turn; GRF gives raw × 360 / 1024 (str-reference.md §3).</summary>
        const float AngleUnitsPerTurn = 1024f;

        /// <summary>
        /// The header version as GRF's <c>StrHeader</c> reads it (GRF parses only the header). False when GRF rejects the
        /// header (wrong magic, too short). The version is GRF's <c>MajorVersion</c> when its <c>MinorVersion</c> is 0, as in
        /// every file of the data (GRF gives 148 for 0x94); otherwise <c>MajorVersion | MinorVersion &lt;&lt; 8</c>, which is never
        /// <see cref="SupportedVersion"/>.
        /// </summary>
        public static bool TryReadVersion(byte[] bytes, out int version)
        {
            version = 0;
            if (bytes == null || bytes.Length == 0) return false;
            try
            {
                var header = new GrfStrHeader(new GrfByteReader(bytes, 0), new GrfStr());
                version = header.MajorVersion | header.MinorVersion << 8;
                return true;
            }
            catch (Exception)
            {
                version = 0;
                return false;
            }
        }

        /// <summary>Plan 4.1 form: <see cref="TryRead(RoPath, byte[], out RoStr, out RoIssueCode, out string)"/> without the code.</summary>
        public static bool TryRead(RoPath path, byte[] bytes, out RoStr str, out string reason) =>
            TryRead(path, bytes, out str, out _, out reason);

        /// <summary>
        /// Reads an STR through GRF. False with <see cref="RoIssueCode.CorruptFile"/> when the file is empty, GRF rejects
        /// it or its content is inconsistent; false with <see cref="RoIssueCode.UnsupportedStrVersion"/> when the magic is
        /// right but the version is not <see cref="SupportedVersion"/>. Never throws.
        /// </summary>
        public static bool TryRead(RoPath path, byte[] bytes, out RoStr str, out RoIssueCode code, out string reason)
        {
            str = null;
            code = RoIssueCode.CorruptFile;
            if (bytes == null || bytes.Length == 0) return Fail("the file is empty", out reason);

            GrfStrHeader header;
            try
            {
                header = new GrfStrHeader(new GrfByteReader(bytes, 0), new GrfStr());
            }
            catch (Exception e)
            {
                return Fail($"GRF could not read the STR header: {e.GetType().Name}: {e.Message}", out reason);
            }

            int version = header.MajorVersion | header.MinorVersion << 8;
            if (version != SupportedVersion)
            {
                code = RoIssueCode.UnsupportedStrVersion;
                return Fail($"STR version 0x{version:X} ({header.HexVersionFormat}) has no verified rules; only 0x{SupportedVersion:X} is read", out reason);
            }

            // Texture names are CP949; GRF decodes them with its display encoding (as RoActReader, RoGndReader).
            var previous = EncodingService.DisplayEncoding;
            try
            {
                EncodingService.DisplayEncoding = EncodingService.Korean;
                str = Convert(new GrfStr((GRF.MultiType)bytes), version);
            }
            catch (Exception e)
            {
                // Includes OutOfMemoryException (a count of 0x7FFFFFFF fails at once in GRF, str-reference.md §9).
                str = null;
                return Fail($"GRF could not read the STR: {e.GetType().Name}: {e.Message}", out reason);
            }
            finally
            {
                EncodingService.DisplayEncoding = previous;
            }

            reason = null;
            return true;
        }

        /// <summary>GRF's degrees back to the file's units: largest difference from the file 4.9e-4 units over the data (T0.1).</summary>
        internal static float AngleFromGrfDegrees(float degrees) => degrees * AngleUnitsPerTurn / 360f;

        /// <summary>
        /// Test support (wrong-version input without touching bytes): reads <paramref name="bytes"/> through GRF, sets the
        /// header version with GRF's <c>SetVersion</c> and returns what GRF's <c>Save</c> writes. GRF's writer is not a
        /// byte-exact round trip (for example a <c>+inf</c> delay is written as 0); only the version is meant to differ.
        /// </summary>
        internal static byte[] RewriteVersionThroughGrf(byte[] bytes, byte major, byte minor)
        {
            string temp = Path.GetTempFileName();
            var previous = EncodingService.DisplayEncoding;
            try
            {
                EncodingService.DisplayEncoding = EncodingService.Korean;
                var grf = new GrfStr((GRF.MultiType)bytes);
                grf.Header.SetVersion(major, minor);
                grf.Save(temp);
                return File.ReadAllBytes(temp);
            }
            finally
            {
                EncodingService.DisplayEncoding = previous;
                try { File.Delete(temp); } catch (IOException) { }
            }
        }

        /// <summary>
        /// Test support (a synthetic STR without touching bytes): GRF's own objects are filled (one layer per entry of
        /// <paramref name="layerTextures"/>, each with those texture names and one key at frame 0) and GRF's <c>Save</c> writes
        /// the file, which this returns.
        /// </summary>
        internal static byte[] BuildThroughGrf(string[][] layerTextures, int fps, int maxKeyFrame)
        {
            string temp = Path.GetTempFileName();
            var previous = EncodingService.DisplayEncoding;
            try
            {
                EncodingService.DisplayEncoding = EncodingService.Korean;
                var grf = new GrfStr();
                grf.Header.SetVersion(SupportedVersion, 0);
                grf.Fps = fps;
                grf.MaxKeyFrame = maxKeyFrame;
                foreach (var names in layerTextures)
                {
                    var layer = new GRF.FileFormats.StrFormat.StrLayer(grf);
                    layer.TextureNames.AddRange(names);
                    var key = new GrfStrKeyFrame
                    {
                        FrameIndex = 0, Type = 0, TextureIndex = 0, AnimationType = GRF.FileFormats.StrFormat.AnimationType.Stop,
                        UVs = new float[8], Positions = new float[8], Color = new float[] { 255, 255, 255, 255 },
                        BezierPositions = new float[8], BlendSrc = 5, BlendDst = 7,
                    };
                    layer.KeyFrames.Add(key);
                    grf.Layers.Add(layer);
                }
                grf.Save(temp);
                return File.ReadAllBytes(temp);
            }
            finally
            {
                EncodingService.DisplayEncoding = previous;
                try { File.Delete(temp); } catch (IOException) { }
            }
        }

        static RoStr Convert(GrfStr grf, int version)
        {
            if (grf.Layers == null) throw new InvalidOperationException("the layer list is missing");
            if (grf.Fps <= 0) throw new InvalidOperationException($"the frame rate {grf.Fps} is not positive");
            if (grf.MaxKeyFrame < 0) throw new InvalidOperationException($"the frame count {grf.MaxKeyFrame} is negative");

            var layers = new List<RoStrLayer>(grf.Layers.Count);
            foreach (var l in grf.Layers)
            {
                if (l == null) throw new InvalidOperationException($"layer {layers.Count} is missing");
                var names = l.TextureNames != null ? new List<string>(l.TextureNames) : new List<string>();
                var source = l.KeyFrames ?? new List<GrfStrKeyFrame>();
                var keys = new List<RoStrKey>(source.Count);
                foreach (var k in source) keys.Add(Key(k, layers.Count, keys.Count));
                layers.Add(new RoStrLayer { TextureNames = names, Keys = keys });
            }

            return new RoStr { Version = version, Fps = grf.Fps, FrameCount = grf.MaxKeyFrame, Layers = layers };
        }

        static RoStrKey Key(GrfStrKeyFrame k, int layer, int index)
        {
            if (k == null) throw new InvalidOperationException($"key {index} of layer {layer} is missing");
            var uv = k.UVs; var xy = k.Positions; var c = k.Color;
            if (uv == null || uv.Length < 8 || xy == null || xy.Length < 8 || c == null || c.Length < 4)
                throw new InvalidOperationException($"key {index} of layer {layer} has incomplete values");

            return new RoStrKey(
                k.FrameIndex, k.Type, new Vector2(k.Offset.X, k.Offset.Y),
                new Vector2(uv[0], uv[1]), new Vector2(uv[2], uv[3]), new Vector2(uv[4], uv[5]), new Vector2(uv[6], uv[7]),
                new Vector2(xy[0], xy[4]), new Vector2(xy[1], xy[5]), new Vector2(xy[2], xy[6]), new Vector2(xy[3], xy[7]),
                k.TextureIndex, (int)k.AnimationType, k.Delay, AngleFromGrfDegrees(k.Angle),
                new Vector4(c[0], c[1], c[2], c[3]),
                k.BlendSrc, k.BlendDst, k.MtPresent);
        }

        static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
