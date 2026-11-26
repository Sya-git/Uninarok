using System;
using System.Text;

namespace Uninarok.Effects
{
    /// <summary>
    /// Runtime path normalisation for effect keys (SPEC-0012 FR-5, plan 4.4, D21). Own light copy of the rules the
    /// importer applies, with no reference to the import assemblies: NFC, forward slashes, collapsed and trimmed
    /// separators, invariant lowercase, one leading <c>data/</c> stripped. A key is relative to the effect data folder.
    /// </summary>
    public static class EffectPath
    {
        const string DataSegment = "data";

        /// <summary>Relative forms the client data uses, tried in order by the catalog.</summary>
        public static readonly string[] Prefixes = { "", "texture/effect/", "texture/", "sprite/" };

        /// <summary>
        /// Canonical key (relative to the effect data root); false (and a null key) for empty, "." / ".." segment,
        /// drive-letter or control-character input.
        /// </summary>
        public static bool TryNormalize(string raw, out string key)
        {
            key = null;
            if (raw == null) return false;

            // Slashes unified and collapsed, whitespace and outer separators trimmed.
            raw = raw.Trim();
            var buffer = new char[raw.Length];
            int n = 0;
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i] == '\\' ? '/' : raw[i];
                if (c == '/' && n > 0 && buffer[n - 1] == '/') continue;
                buffer[n++] = c;
            }
            int start = 0;
            while (start < n && buffer[start] == '/') start++;
            while (n > start && buffer[n - 1] == '/') n--;
            string s = new string(buffer, start, n - start).Trim();

            if (!s.IsNormalized(NormalizationForm.FormC)) s = s.Normalize(NormalizationForm.FormC);
            s = s.ToLowerInvariant();

            if (s == DataSegment) s = string.Empty;
            else if (s.StartsWith(DataSegment + "/", StringComparison.Ordinal)) s = s.Substring(DataSegment.Length + 1);

            if (s.Length == 0) return false;
            if (s.Length >= 2 && s[1] == ':' && s[0] >= 'a' && s[0] <= 'z') return false;

            int segStart = 0;
            for (int i = 0; i <= s.Length; i++)
            {
                if (i < s.Length)
                {
                    if (char.IsControl(s[i])) return false;
                    if (s[i] != '/') continue;
                }
                int len = i - segStart;
                if (len == 1 && s[segStart] == '.') return false;
                if (len == 2 && s[segStart] == '.' && s[segStart + 1] == '.') return false;
                segStart = i + 1;
            }

            key = s;
            return true;
        }
    }
}
