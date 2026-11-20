using System;
using System.Globalization;
using System.Text;

namespace Uninarok.RoImport.Core.Catalog.Lua
{
    /// <summary>A reader error at a position of the text; caught by <see cref="RoLuaReader"/>, never thrown out of it.</summary>
    sealed class RoLuaException : Exception
    {
        public RoLuaException(int position, string message) : base(message) => Position = position;
        public int Position { get; }
    }

    enum RoLuaToken
    {
        Eof,
        Name,
        Number,
        String,
        Symbol,
    }

    /// <summary>
    /// Tokens of the Lua data subset (PLAN-0008 4.1). A token is a span of the text: names, numbers and strings are only
    /// turned into .NET values when asked (<see cref="Text"/>, <see cref="NumberValue"/>, <see cref="StringValue"/>), so a
    /// skipped value (<see cref="RoLuaOptions.SkipField"/>) allocates nothing. Symbols are the literal strings below, so
    /// they compare by reference. Comments (<c>--</c>, <c>--[[ ]]</c>, <c>--[==[ ]==]</c>) are skipped; long strings
    /// (<c>[[ ]]</c>) are rejected (T0.3: none of the tables uses them).
    /// </summary>
    sealed class RoLuaLexer
    {
        readonly string _text;
        int _pos;

        public RoLuaLexer(string text) => _text = text;

        public RoLuaToken Kind { get; private set; }
        public int Start { get; private set; }
        public int End { get; private set; }
        /// <summary>The symbol of a <see cref="RoLuaToken.Symbol"/> token (one of the literals), else null.</summary>
        public string Symbol { get; private set; }

        /// <summary>The position to resume from; with <see cref="Restore"/> gives one token of look-ahead.</summary>
        public int State => _pos;

        public void Restore(int state)
        {
            _pos = state;
            Next();
        }

        public int Line(int position)
        {
            int line = 1;
            for (int i = 0; i < position && i < _text.Length; i++)
                if (_text[i] == '\n') line++;
            return line;
        }

        public bool IsSymbol(string symbol) => Kind == RoLuaToken.Symbol && ReferenceEquals(Symbol, symbol);

        /// <summary>True when the token is the name (or keyword) <paramref name="word"/>.</summary>
        public bool IsName(string word) =>
            Kind == RoLuaToken.Name && End - Start == word.Length && string.CompareOrdinal(_text, Start, word, 0, word.Length) == 0;

        public bool IsKeyword()
        {
            if (Kind != RoLuaToken.Name) return false;
            switch (End - Start)
            {
                case 2: return IsName("do") || IsName("if") || IsName("in") || IsName("or");
                case 3: return IsName("and") || IsName("end") || IsName("for") || IsName("nil") || IsName("not");
                case 4: return IsName("else") || IsName("goto") || IsName("then") || IsName("true");
                case 5: return IsName("break") || IsName("false") || IsName("local") || IsName("until") || IsName("while");
                case 6: return IsName("elseif") || IsName("repeat") || IsName("return");
                case 8: return IsName("function");
                default: return false;
            }
        }

        public string Text() => _text.Substring(Start, End - Start);

        public string Describe() =>
            Kind == RoLuaToken.Eof ? "the end of the text" : $"'{(End - Start > 40 ? _text.Substring(Start, 40) + "…" : Text())}'";

        public RoLuaException Error(string message) => new RoLuaException(Start, message);

        public void Next()
        {
            SkipSpaceAndComments();
            Start = _pos;
            Symbol = null;
            if (_pos >= _text.Length)
            {
                Kind = RoLuaToken.Eof;
                End = _pos;
                return;
            }

            char c = _text[_pos];
            if (char.IsLetter(c) || c == '_')
            {
                _pos++;
                while (_pos < _text.Length && (char.IsLetterOrDigit(_text[_pos]) || _text[_pos] == '_')) _pos++;
                Kind = RoLuaToken.Name;
            }
            else if (IsDigit(c) || (c == '.' && _pos + 1 < _text.Length && IsDigit(_text[_pos + 1])))
            {
                ScanNumber();
                Kind = RoLuaToken.Number;
            }
            else if (c == '"' || c == '\'')
            {
                ScanString(c);
                Kind = RoLuaToken.String;
            }
            else
            {
                Symbol = ScanSymbol(c);
                Kind = RoLuaToken.Symbol;
            }
            End = _pos;
        }

        public double NumberValue()
        {
            string s = Text();
            if (s.Length > 2 && s[0] == '0' && (s[1] == 'x' || s[1] == 'X'))
            {
                if (long.TryParse(s.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out long hex)) return hex;
            }
            else if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            {
                return d;
            }
            throw Error($"malformed number '{s}'");
        }

        /// <summary>The decoded string of a <see cref="RoLuaToken.String"/> token (the escapes of Lua 5.1).</summary>
        public string StringValue()
        {
            int from = Start + 1, to = End - 1;
            int backslash = _text.IndexOf('\\', from, to - from);
            if (backslash < 0) return _text.Substring(from, to - from);

            var sb = new StringBuilder(to - from);
            for (int i = from; i < to; i++)
            {
                char c = _text[i];
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                char e = _text[++i];
                switch (e)
                {
                    case 'a': sb.Append('\a'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'v': sb.Append('\v'); break;
                    case '\r':
                        sb.Append('\n');
                        if (i + 1 < to && _text[i + 1] == '\n') i++;
                        break;
                    default:
                        if (IsDigit(e))
                        {
                            int value = 0, digits = 0;
                            while (digits < 3 && i < to && IsDigit(_text[i]))
                            {
                                value = value * 10 + (_text[i] - '0');
                                i++;
                                digits++;
                            }
                            i--;
                            if (value > 255) throw new RoLuaException(i, "escape sequence too large");
                            sb.Append((char)value);
                        }
                        else
                        {
                            sb.Append(e); // \\, \", \', \newline and Lua 5.1's "any other character stands for itself"
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        static bool IsDigit(char c) => c >= '0' && c <= '9';

        void SkipSpaceAndComments()
        {
            while (_pos < _text.Length)
            {
                char c = _text[_pos];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '\f' || c == '\v' || c == '﻿')
                {
                    _pos++;
                }
                else if (c == '-' && _pos + 1 < _text.Length && _text[_pos + 1] == '-')
                {
                    _pos += 2;
                    int level = LongBracketLevel(_pos);
                    if (level >= 0) SkipLongBracket(_pos, level, "comment");
                    else
                        while (_pos < _text.Length && _text[_pos] != '\n') _pos++;
                }
                else
                {
                    return;
                }
            }
        }

        /// <summary>The level of a long bracket opening at <paramref name="at"/> (<c>[[</c> = 0, <c>[==[</c> = 2), or −1.</summary>
        int LongBracketLevel(int at)
        {
            if (at >= _text.Length || _text[at] != '[') return -1;
            int i = at + 1, level = 0;
            while (i < _text.Length && _text[i] == '=') { level++; i++; }
            return i < _text.Length && _text[i] == '[' ? level : -1;
        }

        void SkipLongBracket(int at, int level, string what)
        {
            int start = at;
            string close = "]" + new string('=', level) + "]";
            int end = _text.IndexOf(close, at + level + 2, StringComparison.Ordinal);
            if (end < 0) throw new RoLuaException(start, $"unclosed long {what}");
            _pos = end + close.Length;
        }

        void ScanNumber()
        {
            if (_text[_pos] == '0' && _pos + 1 < _text.Length && (_text[_pos + 1] == 'x' || _text[_pos + 1] == 'X'))
            {
                _pos += 2;
                while (_pos < _text.Length && Uri.IsHexDigit(_text[_pos])) _pos++;
                return;
            }
            while (_pos < _text.Length && (IsDigit(_text[_pos]) || _text[_pos] == '.')) _pos++;
            if (_pos < _text.Length && (_text[_pos] == 'e' || _text[_pos] == 'E'))
            {
                _pos++;
                if (_pos < _text.Length && (_text[_pos] == '+' || _text[_pos] == '-')) _pos++;
                while (_pos < _text.Length && IsDigit(_text[_pos])) _pos++;
            }
            if (_pos < _text.Length && (char.IsLetter(_text[_pos]) || _text[_pos] == '_'))
                throw new RoLuaException(Start, "malformed number");
        }

        void ScanString(char quote)
        {
            int start = _pos++;
            while (true)
            {
                if (_pos >= _text.Length) throw new RoLuaException(start, "unclosed string");
                char c = _text[_pos];
                if (c == quote)
                {
                    _pos++;
                    return;
                }
                if (c == '\n') throw new RoLuaException(start, "unclosed string");
                if (c == '\\')
                {
                    _pos++;
                    if (_pos >= _text.Length) throw new RoLuaException(start, "unclosed string");
                }
                _pos++;
            }
        }

        string ScanSymbol(char c)
        {
            char n = _pos + 1 < _text.Length ? _text[_pos + 1] : '\0';
            _pos++;
            switch (c)
            {
                case '{': return "{";
                case '}': return "}";
                case '(': return "(";
                case ')': return ")";
                case ']': return "]";
                case ',': return ",";
                case ';': return ";";
                case ':': return ":";
                case '+': return "+";
                case '-': return "-";
                case '*': return "*";
                case '/': return "/";
                case '%': return "%";
                case '^': return "^";
                case '#': return "#";
                case '[':
                    if (n == '[' || n == '=')
                    {
                        if (LongBracketLevel(_pos - 1) >= 0) throw new RoLuaException(_pos - 1, "long strings ([[ ]]) are not supported");
                    }
                    return "[";
                case '=':
                    if (n == '=') { _pos++; return "=="; }
                    return "=";
                case '~':
                    if (n == '=') { _pos++; return "~="; }
                    break;
                case '<':
                    if (n == '=') { _pos++; return "<="; }
                    return "<";
                case '>':
                    if (n == '=') { _pos++; return ">="; }
                    return ">";
                case '.':
                    if (n == '.')
                    {
                        _pos++;
                        if (_pos < _text.Length && _text[_pos] == '.') { _pos++; return "..."; }
                        return "..";
                    }
                    return ".";
            }
            throw new RoLuaException(_pos - 1, $"unexpected character '{c}'");
        }
    }
}
