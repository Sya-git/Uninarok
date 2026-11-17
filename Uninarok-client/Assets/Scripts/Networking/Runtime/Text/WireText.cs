using Unity.Collections;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Text
{
    /// <summary>
    /// Single place where wire text is converted (SPEC-0001 FR-6, spec section 6; plan 4.2).
    /// Wire encoding is Windows-1252 through a hand-written table; text is held as UTF-8 in
    /// <c>FixedString*</c>. To support another codepage (for example CP949) swap the table here, nowhere else.
    /// Nothing here allocates managed memory.
    /// </summary>
    public static class WireText
    {
        /// <summary>Name of the wire encoding, for diagnostics.</summary>
        public const string Encoding = "windows-1252";

        // Code points for wire bytes 0x80..0x9F. Bytes 0x81, 0x8D, 0x8F, 0x90, 0x9D are undefined in Windows-1252
        // and map to the matching C1 control, so every byte round-trips.
        private static readonly ushort[] High = new ushort[32]
        {
            0x20AC, 0x0081, 0x201A, 0x0192, 0x201E, 0x2026, 0x2020, 0x2021,
            0x02C6, 0x2030, 0x0160, 0x2039, 0x0152, 0x008D, 0x017D, 0x008F,
            0x0090, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2013, 0x2014,
            0x02DC, 0x2122, 0x0161, 0x203A, 0x0153, 0x009D, 0x017E, 0x0178,
        };

        /// <summary>Largest UTF-8 size of a text of <paramref name="wireBytes"/> - 1 characters (3 bytes per character).</summary>
        internal static int WorstCaseUtf8Bytes(int wireBytes) => wireBytes > 1 ? (wireBytes - 1) * 3 : 0;

        public static void Read(ref PacketReader r, int wireBytes, ref FixedString32Bytes value) => ReadCore(ref r, wireBytes, ref value);
        public static void Read(ref PacketReader r, int wireBytes, ref FixedString64Bytes value) => ReadCore(ref r, wireBytes, ref value);
        public static void Read(ref PacketReader r, int wireBytes, ref FixedString128Bytes value) => ReadCore(ref r, wireBytes, ref value);
        public static void Read(ref PacketReader r, int wireBytes, ref FixedString512Bytes value) => ReadCore(ref r, wireBytes, ref value);

        public static void Write(ref PacketWriter w, int wireBytes, in FixedString32Bytes value) => WriteCore(ref w, wireBytes, in value);
        public static void Write(ref PacketWriter w, int wireBytes, in FixedString64Bytes value) => WriteCore(ref w, wireBytes, in value);
        public static void Write(ref PacketWriter w, int wireBytes, in FixedString128Bytes value) => WriteCore(ref w, wireBytes, in value);
        public static void Write(ref PacketWriter w, int wireBytes, in FixedString512Bytes value) => WriteCore(ref w, wireBytes, in value);

        private static void ReadCore<T>(ref PacketReader r, int wireBytes, ref T value)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            var field = r.ReadSpan(wireBytes);   // always consumes exactly wireBytes
            value.Clear();
            for (int i = 0; i < field.Length; i++)
            {
                byte b = field[i];
                if (b == 0) break;
                int cp = b >= 0x80 && b <= 0x9F ? High[b - 0x80] : b;
                // Result longer than the capacity is cut at a character boundary: Append fails without a partial write.
                if (FixedStringMethods.Append(ref value, new Unicode.Rune(cp)) != FormatError.None) break;
            }
        }

        private static void WriteCore<T>(ref PacketWriter w, int wireBytes, in T value)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            if (wireBytes <= 0) return;
            int maxChars = wireBytes - 1;           // the last byte is always the NUL terminator
            int written = 0;
            int len = value.Length;
            int i = 0;
            while (i < len && written < maxChars)
            {
                int cp = DecodeUtf8(in value, len, ref i);
                if (cp == 0) break;
                w.WriteByte(Encode(cp));
                written++;
            }
            w.Skip(wireBytes - written);            // NUL terminator plus zero fill
        }

        private static byte Encode(int cp)
        {
            if (cp < 0x80 || (cp >= 0xA0 && cp <= 0xFF)) return (byte)cp;
            for (int k = 0; k < High.Length; k++)
                if (High[k] == cp) return (byte)(0x80 + k);
            return (byte)'?';
        }

        // Decodes one code point at i and advances i. Malformed input yields U+FFFD (written as '?').
        private static int DecodeUtf8<T>(in T value, int len, ref int i) where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            int b0 = value[i++];
            if (b0 < 0x80) return b0;
            int extra = b0 >= 0xF0 ? 3 : b0 >= 0xE0 ? 2 : b0 >= 0xC0 ? 1 : -1;
            if (extra < 0 || i + extra > len) { i = len; return 0xFFFD; }
            int cp = b0 & (0x3F >> extra);
            for (int k = 0; k < extra; k++) cp = (cp << 6) | (value[i++] & 0x3F);
            return cp;
        }
    }
}
