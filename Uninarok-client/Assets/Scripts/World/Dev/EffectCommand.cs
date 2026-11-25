using System;
using System.Globalization;

namespace Uninarok.World.Dev
{
    /// <summary>What a /effect command asks for (SPEC-0012 FR-8, PLAN-0012 4.8).</summary>
    public enum EffectCommandKind
    {
        PlayOnSelf,
        PlayAtCell,
        Stop,
    }

    /// <summary>A parsed /effect command line (the text after "/effect").</summary>
    public readonly struct EffectCommand
    {
        public EffectCommandKind Kind { get; }
        public string Path { get; }
        public int CellX { get; }
        public int CellY { get; }
        public bool Loop { get; }

        public EffectCommand(EffectCommandKind kind, string path, int cellX, int cellY, bool loop)
        {
            Kind = kind;
            Path = path;
            CellX = cellX;
            CellY = cellY;
            Loop = loop;
        }

        /// <summary>Parses the arguments of /effect; false with a message in <paramref name="error"/> when they do not fit a form.</summary>
        public static bool TryParse(string args, out EffectCommand cmd, out string error)
        {
            cmd = default;
            error = null;
            const string usage = "usage: /effect <path> [x y] [loop] | /effect stop";

            var rest = (args ?? string.Empty).Trim();
            if (rest.Length == 0)
            {
                error = usage;
                return false;
            }

            // Parse from the end: optional "loop", then optional two integers (the cell).
            var loop = false;
            if (TryPopToken(rest, out var head, out var last)
                && string.Equals(last, "loop", StringComparison.OrdinalIgnoreCase))
            {
                loop = true;
                rest = head;
            }

            var hasCell = false;
            int cellX = 0, cellY = 0;
            if (TryPopToken(rest, out var h1, out var tokY)
                && TryPopToken(h1, out var h2, out var tokX)
                && int.TryParse(tokY, NumberStyles.Integer, CultureInfo.InvariantCulture, out cellY)
                && int.TryParse(tokX, NumberStyles.Integer, CultureInfo.InvariantCulture, out cellX))
            {
                hasCell = true;
                rest = h2;
            }

            var quoted = rest.Length >= 2 && rest[0] == '"' && rest[rest.Length - 1] == '"';
            var path = quoted ? rest.Substring(1, rest.Length - 2).Trim() : rest;
            if (path.Length == 0)
            {
                error = usage;
                return false;
            }

            if (!quoted && !loop && !hasCell && string.Equals(path, "stop", StringComparison.OrdinalIgnoreCase))
            {
                cmd = new EffectCommand(EffectCommandKind.Stop, null, 0, 0, false);
                return true;
            }

            cmd = hasCell
                ? new EffectCommand(EffectCommandKind.PlayAtCell, path, cellX, cellY, loop)
                : new EffectCommand(EffectCommandKind.PlayOnSelf, path, 0, 0, loop);
            return true;
        }

        // Splits the last whitespace-delimited token off s; head is trimmed of trailing whitespace.
        static bool TryPopToken(string s, out string head, out string token)
        {
            var end = s.Length;
            while (end > 0 && char.IsWhiteSpace(s[end - 1])) end--;
            var start = end;
            while (start > 0 && !char.IsWhiteSpace(s[start - 1])) start--;
            if (start == end)
            {
                head = s;
                token = null;
                return false;
            }
            token = s.Substring(start, end - start);
            head = s.Substring(0, start).TrimEnd();
            return true;
        }
    }
}
