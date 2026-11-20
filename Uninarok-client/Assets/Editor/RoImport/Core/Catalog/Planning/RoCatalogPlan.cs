using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Uninarok.Catalog;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Core.Catalog.Planning
{
    /// <summary>
    /// One ACT/SPR pair a record converts (PLAN-0008 4.3). <see cref="Act"/> is empty for a static sprite (SPR only). The
    /// <see cref="Role"/> says what the pair is to the record (<c>unit</c>, <c>body:male:human</c>, <c>trail</c>, …).
    /// </summary>
    public readonly struct RoCatalogPair : IEquatable<RoCatalogPair>
    {
        public readonly RoPath Act;
        public readonly RoPath Spr;
        public readonly string Role;

        public RoCatalogPair(RoPath act, RoPath spr, string role)
        {
            if (spr.IsEmpty) throw new ArgumentException("a pair needs its SPR", nameof(spr));
            Act = act;
            Spr = spr;
            Role = role ?? string.Empty;
        }

        public bool IsStatic => Act.IsEmpty;

        public bool Equals(RoCatalogPair other) => Act == other.Act && Spr == other.Spr && string.Equals(Role, other.Role, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is RoCatalogPair p && Equals(p);
        public override int GetHashCode() => (Act.GetHashCode() * 397) ^ Spr.GetHashCode() ^ StringComparer.Ordinal.GetHashCode(Role ?? string.Empty);
        public override string ToString() => $"{Role}: {(IsStatic ? "-" : Act.Canonical)} + {Spr.Canonical}";
    }

    /// <summary>A named value of a record (ids, names, scale, palette numbers), kept as invariant text (PLAN-0008 4.3).</summary>
    public readonly struct RoCatalogValue : IEquatable<RoCatalogValue>
    {
        public readonly string Name;
        public readonly string Text;

        public RoCatalogValue(string name, string text)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("a value needs a name", nameof(name));
            Name = name;
            Text = text ?? string.Empty;
        }

        public static RoCatalogValue Of(string name, string text) => new RoCatalogValue(name, text);
        public static RoCatalogValue Of(string name, int value) => new RoCatalogValue(name, value.ToString(CultureInfo.InvariantCulture));
        public static RoCatalogValue Of(string name, float value) => new RoCatalogValue(name, value.ToString("R", CultureInfo.InvariantCulture));

        public bool Equals(RoCatalogValue other) => string.Equals(Name, other.Name, StringComparison.Ordinal) && string.Equals(Text, other.Text, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is RoCatalogValue v && Equals(v);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Name ?? string.Empty) ^ StringComparer.Ordinal.GetHashCode(Text ?? string.Empty);
        public override string ToString() => $"{Name}={Text}";
    }

    /// <summary>A gap between the tables and the files (SPEC-0008 FR-10): always a warning.</summary>
    public readonly struct RoCatalogGap
    {
        public readonly RoIssueCode Code;
        public readonly SpriteCatalogCategory Category;
        public readonly string Item;
        public readonly string Message;

        public RoCatalogGap(RoIssueCode code, SpriteCatalogCategory category, string item, string message)
        {
            Code = code;
            Category = category;
            Item = item ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public override string ToString() => $"{Code} [{Category}] {Item}: {Message}";
    }

    /// <summary>
    /// One thing to import (PLAN-0008 4.3): a unit, a body class, a headgear and so on, with the pairs to convert and its
    /// values. <see cref="Fingerprint"/> is the SHA-256 of <see cref="CanonicalText"/>, so it changes with any pair or value
    /// (the catalog ledger compares it, PLAN-0008 4.7).
    /// </summary>
    public sealed class RoCatalogRecord
    {
        public SpriteCatalogCategory Category { get; }
        public string Key { get; }
        public IReadOnlyList<RoCatalogPair> Pairs { get; }
        public IReadOnlyList<RoCatalogValue> Values { get; }
        public string Fingerprint { get; }

        public RoCatalogRecord(SpriteCatalogCategory category, string key, IEnumerable<RoCatalogPair> pairs, IEnumerable<RoCatalogValue> values)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("a record needs a key", nameof(key));
            Category = category;
            Key = key;
            Pairs = (pairs ?? Enumerable.Empty<RoCatalogPair>()).ToArray();
            Values = (values ?? Enumerable.Empty<RoCatalogValue>()).ToArray();
            Fingerprint = Sha256(CanonicalText);
        }

        /// <summary>The first value of that name, or null.</summary>
        public string Value(string name)
        {
            foreach (var v in Values)
                if (string.Equals(v.Name, name, StringComparison.Ordinal)) return v.Text;
            return null;
        }

        /// <summary>One line per field, in the record's order, escaped: what the fingerprint hashes.</summary>
        public string CanonicalText
        {
            get
            {
                var sb = new StringBuilder();
                sb.Append("record\t").Append(Category.ToString()).Append('\t').Append(Escape(Key)).Append('\n');
                foreach (var p in Pairs)
                    sb.Append("pair\t").Append(Escape(p.Role)).Append('\t').Append(Escape(p.Act.Canonical)).Append('\t').Append(Escape(p.Spr.Canonical)).Append('\n');
                foreach (var v in Values)
                    sb.Append("value\t").Append(Escape(v.Name)).Append('\t').Append(Escape(v.Text)).Append('\n');
                return sb.ToString();
            }
        }

        static string Escape(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "\\r");

        static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        public override string ToString() => $"{Category}/{Key}";
    }

    /// <summary>
    /// What a catalog run will import (PLAN-0008 4.3, D1): the records, sorted by (category, key), and the gaps, one per
    /// (code, item), grouped by category. Built by the planners through <see cref="Builder"/>; planning writes nothing.
    /// </summary>
    public sealed class RoCatalogPlan
    {
        RoCatalogPlan(IReadOnlyList<RoCatalogRecord> records, IReadOnlyList<RoCatalogGap> gaps)
        {
            Records = records;
            Gaps = gaps;
        }

        public IReadOnlyList<RoCatalogRecord> Records { get; }
        public IReadOnlyList<RoCatalogGap> Gaps { get; }

        public IEnumerable<RoCatalogRecord> RecordsOf(SpriteCatalogCategory category) => Records.Where(r => r.Category == category);
        public IEnumerable<RoCatalogGap> GapsOf(SpriteCatalogCategory category) => Gaps.Where(g => g.Category == category);

        /// <summary>Copies the gaps to <paramref name="report"/> as warnings, referrer = the category name (PLAN-0008 4.3).</summary>
        public void ReportGaps(RoReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            foreach (var g in Gaps) report.Warn(g.Code, g.Item, g.Category.ToString(), g.Message);
        }

        public sealed class Builder
        {
            readonly Dictionary<(SpriteCatalogCategory, string), RoCatalogRecord> _records = new Dictionary<(SpriteCatalogCategory, string), RoCatalogRecord>();
            readonly List<RoCatalogGap> _gaps = new List<RoCatalogGap>();
            readonly HashSet<(RoIssueCode, string)> _gapKeys = new HashSet<(RoIssueCode, string)>();

            /// <summary>Adds a record; a second record with the same (category, key) is a planner bug and throws.</summary>
            public RoCatalogRecord Add(RoCatalogRecord record)
            {
                if (record == null) throw new ArgumentNullException(nameof(record));
                if (!_records.TryAdd((record.Category, record.Key), record))
                    throw new InvalidOperationException($"the plan already has {record}");
                return record;
            }

            public RoCatalogRecord Add(SpriteCatalogCategory category, string key, IEnumerable<RoCatalogPair> pairs, IEnumerable<RoCatalogValue> values) =>
                Add(new RoCatalogRecord(category, key, pairs, values));

            public bool Contains(SpriteCatalogCategory category, string key) => _records.ContainsKey((category, key));

            /// <summary>Adds a gap; true when it is new. The same (code, item) is kept once (FR-10).</summary>
            public bool Gap(RoIssueCode code, SpriteCatalogCategory category, string item, string message)
            {
                if (!_gapKeys.Add((code, item ?? string.Empty))) return false;
                _gaps.Add(new RoCatalogGap(code, category, item, message));
                return true;
            }

            public int RecordCount => _records.Count;
            public int GapCount => _gaps.Count;

            public RoCatalogPlan Build()
            {
                var records = _records.Values
                    .OrderBy(r => r.Category).ThenBy(r => r.Key, StringComparer.Ordinal).ToArray();
                // Grouped by category; within a category the order the planner found them (deterministic, D1).
                var gaps = _gaps.Select((g, i) => (g, i)).OrderBy(x => x.g.Category).ThenBy(x => x.i).Select(x => x.g).ToArray();
                return new RoCatalogPlan(records, gaps);
            }
        }
    }
}
