using System;
using System.Text;

namespace Uninarok.RoImport.Core.Paths
{
    /// <summary>
    /// A Ragnarok path in canonical form (plan 4.1, SPEC-0002 FR-1): forward slashes, Unicode NFC, invariant lowercase,
    /// relative to the <c>data</c> folder. <c>data\texture\a.bmp</c>, <c>DATA/Texture/a.bmp</c> and <c>texture/a.bmp</c>
    /// are one path. Equality, hashing and ordering are ordinal on <see cref="Canonical"/>.
    /// <c>default(RoPath)</c> is the empty root: <see cref="Canonical"/> is "" and <see cref="Combine"/> on it is <see cref="Parse"/>.
    /// </summary>
    public readonly struct RoPath : IEquatable<RoPath>, IComparable<RoPath>
    {
        const string DataSegment = "data";

        readonly string _canonical;

        RoPath(string canonical) => _canonical = canonical;

        /// <summary>The canonical form, e.g. <c>texture/유저인터페이스/a.bmp</c>. Never null.</summary>
        public string Canonical => _canonical ?? string.Empty;

        public bool IsEmpty => string.IsNullOrEmpty(_canonical);

        /// <summary>The lowercase extension with its dot (<c>.bmp</c>), or "" when the file name has none.</summary>
        public string Extension
        {
            get
            {
                int dot = ExtensionDot(Canonical);
                return dot < 0 ? string.Empty : Canonical.Substring(dot);
            }
        }

        public string FileNameWithoutExtension
        {
            get
            {
                string path = Canonical;
                int start = path.LastIndexOf('/') + 1;
                int dot = ExtensionDot(path);
                return dot < 0 ? path.Substring(start) : path.Substring(start, dot - start);
            }
        }

        /// <summary>The containing folder; the empty root for a top-level item.</summary>
        public RoPath Folder
        {
            get
            {
                int slash = Canonical.LastIndexOf('/');
                return slash < 0 ? default : new RoPath(Canonical.Substring(0, slash));
            }
        }

        /// <summary>Parses a raw Ragnarok path. Throws <see cref="RoPathException"/> if it is not valid (plan 4.1 rule 5).</summary>
        public static RoPath Parse(string raw)
        {
            if (!TryNormalize(raw, stripData: true, out string canonical, out string reason))
                throw new RoPathException(raw, reason);
            return new RoPath(canonical);
        }

        public static bool TryParse(string raw, out RoPath path)
        {
            bool ok = TryNormalize(raw, stripData: true, out string canonical, out _);
            path = ok ? new RoPath(canonical) : default;
            return ok;
        }

        /// <summary>
        /// Like <see cref="TryParse"/> but keeps a leading <c>data</c> segment: for paths that are already relative to the
        /// data root, such as files found on disk by <see cref="Source.RoFolderSource"/>.
        /// </summary>
        internal static bool TryParseRelative(string relative, out RoPath path)
        {
            bool ok = TryNormalize(relative, stripData: false, out string canonical, out _);
            path = ok ? new RoPath(canonical) : default;
            return ok;
        }

        /// <summary>Same folder and name with another extension (".png" or "png"; "" removes it).</summary>
        public RoPath WithExtension(string extension)
        {
            if (IsEmpty) throw new RoPathException(extension, "the empty root has no file name");
            string ext = (extension ?? string.Empty).Trim().ToLowerInvariant();
            if (ext.Length > 0 && ext[0] != '.') ext = "." + ext;
            if (ext.IndexOf('/') >= 0 || ext.IndexOf('\\') >= 0) throw new RoPathException(extension, "an extension cannot contain a slash");

            int dot = ExtensionDot(Canonical);
            string stem = dot < 0 ? Canonical : Canonical.Substring(0, dot);
            if (!TryNormalize(stem + ext, stripData: false, out string canonical, out string reason))
                throw new RoPathException(extension, reason);
            return new RoPath(canonical);
        }

        /// <summary>
        /// This path used as a folder, plus a relative path normalized by the same rules. A leading <c>data/</c> of the
        /// relative part is kept (it is a real folder inside this one); on the empty root this is <see cref="Parse"/>.
        /// </summary>
        public RoPath Combine(string relative)
        {
            if (IsEmpty) return Parse(relative);
            if (!TryNormalize(relative, stripData: false, out string rel, out string reason))
                throw new RoPathException(relative, reason);
            return new RoPath(Canonical + "/" + rel);
        }

        /// <summary>Plan 4.1 rules, in order: slashes and trim, NFC, lowercase, strip one leading data/, reject.</summary>
        static bool TryNormalize(string raw, bool stripData, out string canonical, out string reason)
        {
            canonical = null;
            if (raw == null) { reason = "null"; return false; }

            // 1. Backslashes to slashes, repeated slashes collapsed, whitespace and outer slashes trimmed.
            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw.Trim())
            {
                char ch = c == '\\' ? '/' : c;
                if (ch == '/' && sb.Length > 0 && sb[sb.Length - 1] == '/') continue;
                sb.Append(ch);
            }
            string s = sb.ToString().Trim('/').Trim();

            // 2. Unicode form C, so a Korean name stored decomposed equals the composed one.
            if (!s.IsNormalized(NormalizationForm.FormC)) s = s.Normalize(NormalizationForm.FormC);

            // 3. Invariant lowercase (Hangul syllables have no case and are unchanged).
            s = s.ToLowerInvariant();

            // 4. One leading data segment.
            if (stripData)
            {
                if (s == DataSegment) s = string.Empty;
                else if (s.StartsWith(DataSegment + "/", StringComparison.Ordinal)) s = s.Substring(DataSegment.Length + 1);
            }

            // 5. Reject.
            if (s.Length == 0) { reason = "empty"; return false; }
            if (s.Length >= 2 && s[1] == ':' && s[0] >= 'a' && s[0] <= 'z') { reason = "drive letter"; return false; }
            foreach (char c in s)
                if (char.IsControl(c)) { reason = $"control character U+{(int)c:X4}"; return false; }
            foreach (string segment in s.Split('/'))
                if (segment == "." || segment == "..") { reason = $"'{segment}' segment"; return false; }

            canonical = s;
            reason = null;
            return true;
        }

        /// <summary>Index of the extension's dot in the last segment, or -1.</summary>
        static int ExtensionDot(string path)
        {
            int dot = path.LastIndexOf('.');
            if (dot < 0 || dot < path.LastIndexOf('/') || dot == path.Length - 1) return -1;
            return dot;
        }

        public bool Equals(RoPath other) => string.Equals(Canonical, other.Canonical, StringComparison.Ordinal);
        public int CompareTo(RoPath other) => string.CompareOrdinal(Canonical, other.Canonical);
        public override bool Equals(object obj) => obj is RoPath other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Canonical);
        public override string ToString() => Canonical;

        public static bool operator ==(RoPath a, RoPath b) => a.Equals(b);
        public static bool operator !=(RoPath a, RoPath b) => !a.Equals(b);
    }
}
