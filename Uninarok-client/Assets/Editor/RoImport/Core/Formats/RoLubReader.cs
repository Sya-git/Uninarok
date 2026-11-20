using System;
using System.Text;
using Uninarok.RoImport.Core.Paths;
using GrfLub = GRF.FileFormats.LubFormat.Lub;
using EncodingService = Utilities.Services.EncodingService;

namespace Uninarok.RoImport.Core.Formats
{
    /// <summary>The text encoding of one lub file's strings (PLAN-0008 4.1; T0.3 mapping table).</summary>
    public enum RoLubEncoding
    {
        /// <summary>The item tables (<c>iteminfo_new.lub</c> and its language copies).</summary>
        Utf8,
        /// <summary>The <c>data/luafiles514</c> tables and <c>monster_size_effect_new.lub</c>.</summary>
        Cp949,
    }

    /// <summary>
    /// Adapter over GRF's lub decompiler (PLAN-0008 4.1, T0.3): the only lub code that touches GRF types. Lua 5.1 bytecode
    /// (<c>ESC Lua</c>) goes through <c>Lub.Decompile()</c> with <c>EncodingService.DisplayEncoding</c> set to the file's
    /// encoding for the call (GRF decodes string constants with it) and restored after; a plain-text file is decoded as
    /// given, strictly. It never throws: every failure is <c>false</c> with a reason.
    /// </summary>
    public static class RoLubReader
    {
        const byte Escape = 0x1B;

        public static bool TryRead(RoPath path, byte[] bytes, RoLubEncoding encoding, out string lua, out string reason)
        {
            lua = null;
            if (bytes == null || bytes.Length == 0) return Fail("the file is empty", out reason);

            Encoding text;
            try
            {
                // Unity's Mono provides code page 949 itself (EncodingService.Korean); CodePagesEncodingProvider is not in
                // Unity's API profile (T2.3 note in PLAN-0008 4.1).
                text = encoding == RoLubEncoding.Utf8 ? Encoding.UTF8 : Encoding.GetEncoding(949);
            }
            catch (Exception e)
            {
                return Fail($"the {encoding} encoding is not available: {e.Message}", out reason);
            }

            if (bytes[0] == Escape) return TryDecompile(bytes, text, out lua, out reason);
            return TryDecodeText(bytes, encoding, out lua, out reason);
        }

        static bool TryDecompile(byte[] bytes, Encoding text, out string lua, out string reason)
        {
            lua = null;
            var previous = EncodingService.DisplayEncoding;
            try
            {
                if (!GrfLub.IsCompiled(bytes)) return Fail("not a Lua 5.1 bytecode file", out reason);
                EncodingService.DisplayEncoding = text;
                lua = new GrfLub((GRF.MultiType)bytes).Decompile();
            }
            catch (Exception e)
            {
                lua = null;
                return Fail($"GRF could not decompile the file: {e.GetType().Name}: {e.Message}", out reason);
            }
            finally
            {
                EncodingService.DisplayEncoding = previous;
            }
            if (string.IsNullOrEmpty(lua)) return Fail("GRF decompiled the file to no text", out reason);
            reason = null;
            return true;
        }

        static bool TryDecodeText(byte[] bytes, RoLubEncoding encoding, out string lua, out string reason)
        {
            lua = null;
            string decoded;
            try
            {
                var strict = encoding == RoLubEncoding.Utf8
                    ? new UTF8Encoding(false, true)
                    : Encoding.GetEncoding(949, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                decoded = strict.GetString(bytes);
            }
            catch (Exception e)
            {
                return Fail($"not {encoding} text: {e.Message}", out reason);
            }

            for (int i = 0; i < decoded.Length; i++)
            {
                char c = decoded[i];
                if (c < ' ' && c != '\t' && c != '\r' && c != '\n' && c != '\f' && c != '\v')
                    return Fail($"not a text file (control character 0x{(int)c:X2} at {i})", out reason);
            }
            lua = decoded;
            reason = null;
            return true;
        }

        static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
