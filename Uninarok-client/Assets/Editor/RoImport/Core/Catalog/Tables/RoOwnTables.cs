using System;
using System.Collections.Generic;
using System.Globalization;
using Uninarok.Networking.Data;

namespace Uninarok.RoImport.Core.Catalog.Tables
{
    /// <summary>One row of <c>class-bodies.csv</c>: a player body file name and the class it draws (PLAN-0008 4.2, OQ-1).</summary>
    public readonly struct RoClassBody
    {
        public RoClassBody(string bodyName, PlayerJob job, bool doram, string kind, PlayerJob baseJob)
        {
            BodyName = bodyName;
            Job = job;
            Doram = doram;
            Kind = kind;
            BaseJob = baseJob;
        }

        /// <summary>The file name without sex and extension (<c>페코페코_기사</c> for <c>페코페코_기사_남.spr</c>).</summary>
        public string BodyName { get; }
        /// <summary>The class the server sends for this body (<c>JOB_KNIGHT2</c>).</summary>
        public PlayerJob Job { get; }
        /// <summary>True for a body of <c>도람족/</c>, false for <c>인간족/</c>.</summary>
        public bool Doram { get; }
        /// <summary><c>Base</c>, <c>Mount</c>, <c>Outfit</c> or <c>Costume1</c>…<c>Costume4</c> (<see cref="RoOwnTables.IsKind"/>).</summary>
        public string Kind { get; }
        /// <summary>The class whose catalog entry holds this body (<c>JOB_KNIGHT</c> for the peco knight).</summary>
        public PlayerJob BaseJob { get; }
    }

    /// <summary>
    /// The owner-reviewed tables that no lub provides (OQ-1; PLAN-0008 4.2; sprite-catalog.md §5): <c>class-bodies.csv</c>
    /// (<c>BodyName, Job, Race, Kind, BaseJob</c>, jobs as <see cref="PlayerJob"/> names) and <c>shield-names.csv</c>
    /// (<c>ViewId, Name</c>). Plain UTF-8 CSV: one header line, lines starting with <c>#</c> are comments, no quoting
    /// (no value holds a comma). Any malformed row rejects the file with its line number.
    /// </summary>
    public sealed class RoOwnTables
    {
        public const string Folder = "Assets/Editor/RoImport/Catalog/Data";
        public const string ClassBodiesFile = "class-bodies.csv";
        public const string ShieldNamesFile = "shield-names.csv";

        const string KindList = "Base, Mount, Outfit, Costume1, Costume2, Costume3, Costume4";

        readonly Dictionary<(string, bool), RoClassBody> _bodies;

        RoOwnTables(List<RoClassBody> bodies, Dictionary<(string, bool), RoClassBody> byName, Dictionary<int, string> shields)
        {
            Bodies = bodies;
            _bodies = byName;
            ShieldNames = shields;
        }

        /// <summary>True for an allowed value of the <c>Kind</c> column: Base, Mount, Outfit, Costume1 to Costume4.</summary>
        public static bool IsKind(string kind)
        {
            switch (kind)
            {
                case "Base": case "Mount": case "Outfit": case "Costume1": case "Costume2": case "Costume3": case "Costume4": return true;
                default: return false;
            }
        }

        /// <summary>The rows of <c>class-bodies.csv</c>, in file order.</summary>
        public IReadOnlyList<RoClassBody> Bodies { get; }

        /// <summary>Shield view id → shield file name (1 → <c>가드</c>).</summary>
        public IReadOnlyDictionary<int, string> ShieldNames { get; }

        public bool TryGetBody(string bodyName, bool doram, out RoClassBody body) => _bodies.TryGetValue((bodyName, doram), out body);

        public static bool TryRead(string classBodies, string shieldNames, out RoOwnTables tables, out string reason)
        {
            tables = null;
            if (!TryReadBodies(classBodies, out var bodies, out var byName, out reason)) return false;
            if (!TryReadShields(shieldNames, out var shields, out reason)) return false;
            tables = new RoOwnTables(bodies, byName, shields);
            return true;
        }

        static bool TryReadBodies(string text, out List<RoClassBody> bodies, out Dictionary<(string, bool), RoClassBody> byName, out string reason)
        {
            bodies = new List<RoClassBody>();
            byName = new Dictionary<(string, bool), RoClassBody>();
            foreach (var (line, cells) in Rows(text, ClassBodiesFile, "BodyName,Job,Race,Kind,BaseJob", 5, out reason))
            {
                if (cells == null) return false;
                string at = $"{ClassBodiesFile} line {line}";
                if (cells[0].Length == 0) return Fail($"{at}: empty BodyName", out reason);
                if (!TryJob(cells[1], out var job)) return Fail($"{at}: '{cells[1]}' is not a PlayerJob", out reason);
                bool doram;
                if (cells[2] == "Human") doram = false;
                else if (cells[2] == "Doram") doram = true;
                else return Fail($"{at}: Race must be Human or Doram, not '{cells[2]}'", out reason);
                if (!IsKind(cells[3])) return Fail($"{at}: Kind '{cells[3]}' is not one of {KindList}", out reason);
                if (!TryJob(cells[4], out var baseJob)) return Fail($"{at}: '{cells[4]}' is not a PlayerJob", out reason);

                var body = new RoClassBody(cells[0], job, doram, cells[3], baseJob);
                if (byName.ContainsKey((body.BodyName, doram))) return Fail($"{at}: {body.BodyName} ({cells[2]}) is listed twice", out reason);
                byName.Add((body.BodyName, doram), body);
                bodies.Add(body);
            }
            return reason == null;
        }

        static bool TryReadShields(string text, out Dictionary<int, string> shields, out string reason)
        {
            shields = new Dictionary<int, string>();
            foreach (var (line, cells) in Rows(text, ShieldNamesFile, "ViewId,Name", 2, out reason))
            {
                if (cells == null) return false;
                string at = $"{ShieldNamesFile} line {line}";
                if (!int.TryParse(cells[0], NumberStyles.None, CultureInfo.InvariantCulture, out int id)) return Fail($"{at}: '{cells[0]}' is not a view id", out reason);
                if (cells[1].Length == 0) return Fail($"{at}: empty Name", out reason);
                if (shields.ContainsKey(id)) return Fail($"{at}: view id {id} is listed twice", out reason);
                shields.Add(id, cells[1]);
            }
            return reason == null;
        }

        // The data rows of a CSV after its header; on a malformed line yields (line, null) once with the reason set.
        static IEnumerable<(int line, string[] cells)> Rows(string text, string file, string header, int columns, out string reason)
        {
            reason = null;
            var rows = new List<(int, string[])>();
            if (text == null)
            {
                reason = $"{file}: no text";
                rows.Add((0, null));
                return rows;
            }

            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            bool sawHeader = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i].Trim();
                if (raw.Length == 0 || raw[0] == '#') continue;
                if (!sawHeader)
                {
                    if (raw.TrimStart('﻿') != header)
                    {
                        reason = $"{file} line {i + 1}: expected the header '{header}'";
                        rows.Add((i + 1, null));
                        return rows;
                    }
                    sawHeader = true;
                    continue;
                }
                string[] cells = raw.Split(',');
                if (cells.Length != columns)
                {
                    reason = $"{file} line {i + 1}: {cells.Length} columns, expected {columns}";
                    rows.Add((i + 1, null));
                    return rows;
                }
                for (int c = 0; c < cells.Length; c++) cells[c] = cells[c].Trim();
                rows.Add((i + 1, cells));
            }
            if (!sawHeader)
            {
                reason = $"{file}: no header";
                rows.Add((0, null));
            }
            return rows;
        }

        static bool TryJob(string name, out PlayerJob job) =>
            Enum.TryParse(name, false, out job) && Enum.IsDefined(typeof(PlayerJob), job) && name.StartsWith("JOB_", StringComparison.Ordinal);

        static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
