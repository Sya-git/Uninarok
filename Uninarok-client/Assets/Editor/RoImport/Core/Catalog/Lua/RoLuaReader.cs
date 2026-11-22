using System;
using System.Collections.Generic;

namespace Uninarok.RoImport.Core.Catalog.Lua
{
    /// <summary>
    /// Reads the Lua data subset of the client's lub tables (PLAN-0008 4.1, D7): global assignments (<c>Name = value</c>,
    /// <c>Name.Field = value</c>), table constructors (positional values, <c>name = value</c>, <c>[expr] = value</c>),
    /// numbers (also negative and hexadecimal), strings with escapes, <c>true</c>/<c>false</c>/<c>nil</c>, and references
    /// (<c>jobtbl.JT_PORING</c>) resolved against the earlier globals of the chunk, then <paramref name="imports"/>; a
    /// bare unknown name is <c>nil</c> and a positional <c>nil</c> leaves a hole, as in Lua (D17).
    /// Function definitions and control blocks are skipped by balanced keyword counting, never evaluated; a top-level call
    /// whose arguments are data is recorded in <see cref="RoLuaChunk.Calls"/> (PLAN-0009 D4), any other is skipped. Anything
    /// else (an operator, a call inside a value, an unknown name) rejects the file with its line number. Never throws.
    /// </summary>
    public static class RoLuaReader
    {
        public static bool TryRead(string lua, RoLuaOptions options, IReadOnlyDictionary<string, RoLuaValue> imports,
                                   out RoLuaChunk chunk, out string reason)
        {
            chunk = null;
            if (lua == null)
            {
                reason = "no text";
                return false;
            }

            var lexer = new RoLuaLexer(lua);
            var parser = new Parser(lexer, options?.SkipField, imports);
            try
            {
                lexer.Next();
                parser.Chunk();
            }
            catch (RoLuaException e)
            {
                reason = $"line {lexer.Line(e.Position)}: {e.Message}";
                return false;
            }
            catch (Exception e)
            {
                reason = $"line {lexer.Line(lexer.Start)}: {e.GetType().Name}: {e.Message}";
                return false;
            }

            chunk = new RoLuaChunk(parser.Globals, parser.Calls);
            reason = null;
            return true;
        }

        sealed class Parser
        {
            readonly RoLuaLexer _lx;
            readonly Func<string, bool> _skipField;
            readonly IReadOnlyDictionary<string, RoLuaValue> _imports;
            readonly Dictionary<string, RoLuaValue> _globals = new Dictionary<string, RoLuaValue>(StringComparer.Ordinal);

            public Parser(RoLuaLexer lexer, Func<string, bool> skipField, IReadOnlyDictionary<string, RoLuaValue> imports)
            {
                _lx = lexer;
                _skipField = skipField;
                _imports = imports;
            }

            readonly List<RoLuaCall> _calls = new List<RoLuaCall>();

            public IReadOnlyDictionary<string, RoLuaValue> Globals => _globals;
            public IReadOnlyList<RoLuaCall> Calls => _calls;

            public void Chunk()
            {
                while (_lx.Kind != RoLuaToken.Eof)
                {
                    if (_lx.IsSymbol(";")) { _lx.Next(); continue; }
                    if (_lx.Kind != RoLuaToken.Name) throw _lx.Error($"unexpected {_lx.Describe()}");

                    if (_lx.IsName("function"))
                    {
                        _lx.Next();
                        SkipFunctionName();
                        SkipFunctionBody();
                    }
                    else if (_lx.IsName("local"))
                    {
                        _lx.Next();
                        if (_lx.IsName("function"))
                        {
                            _lx.Next();
                            SkipFunctionName();
                            SkipFunctionBody();
                        }
                        else
                        {
                            Statement();
                        }
                    }
                    else if (_lx.IsName("if") || _lx.IsName("do"))
                    {
                        _lx.Next();
                        SkipUntilEnd(1);
                    }
                    else if (_lx.IsName("for") || _lx.IsName("while"))
                    {
                        _lx.Next();
                        SkipUntilEnd(0);
                    }
                    else if (_lx.IsName("return"))
                    {
                        return; // a chunk ends at its return; nothing after it runs
                    }
                    else if (_lx.IsKeyword())
                    {
                        throw _lx.Error($"unexpected {_lx.Describe()}");
                    }
                    else
                    {
                        Statement();
                    }
                }
            }

            // Name { .Name | [expr] } followed by '=' (assignment) or call arguments (recorded when they are data).
            void Statement()
            {
                if (_lx.Kind != RoLuaToken.Name || _lx.IsKeyword()) throw _lx.Error($"unexpected {_lx.Describe()}");
                int at = _lx.Start;
                string root = _lx.Text();
                _lx.Next();
                List<object> path = null;
                while (true)
                {
                    if (_lx.IsSymbol("."))
                    {
                        _lx.Next();
                        (path ?? (path = new List<object>())).Add(ExpectName());
                    }
                    else if (_lx.IsSymbol("["))
                    {
                        _lx.Next();
                        var key = Value();
                        Expect("]");
                        (path ?? (path = new List<object>())).Add(Key(key, at));
                    }
                    else
                    {
                        break;
                    }
                }

                if (_lx.IsSymbol("="))
                {
                    _lx.Next();
                    var value = Value();
                    Assign(root, path, value, at);
                    if (_lx.IsSymbol(",")) throw _lx.Error("multiple assignment is not supported");
                    return;
                }

                string method = null;
                if (_lx.IsSymbol(":"))
                {
                    _lx.Next();
                    method = ExpectName();
                }
                if (!IsCallArguments()) throw _lx.Error($"unexpected {_lx.Describe()} after '{root}'");
                if (TryCallArguments(out var arguments))
                    _calls.Add(new RoLuaCall(root + (path == null ? "" : "." + string.Join(".", path)) + (method == null ? "" : ":" + method), arguments));
                while (IsCallArguments()) SkipCallArguments();
            }

            // The arguments of a call statement when all of them are data; otherwise the call is skipped and false.
            bool TryCallArguments(out List<RoLuaValue> arguments)
            {
                int open = _lx.Start;
                arguments = new List<RoLuaValue>();
                try
                {
                    if (!_lx.IsSymbol("("))
                    {
                        arguments.Add(Value()); // f{...} or f"..."
                        return true;
                    }
                    _lx.Next();
                    while (!_lx.IsSymbol(")"))
                    {
                        arguments.Add(Value());
                        if (_lx.IsSymbol(","))
                        {
                            _lx.Next();
                            if (_lx.IsSymbol(")")) throw _lx.Error("a trailing ','");
                        }
                        else if (!_lx.IsSymbol(")")) throw _lx.Error("not data");
                    }
                    _lx.Next();
                    return true;
                }
                catch (RoLuaException)
                {
                    _lx.Restore(open);
                    SkipCallArguments();
                    arguments = null;
                    return false;
                }
            }

            void Assign(string root, List<object> path, RoLuaValue value, int at)
            {
                if (path == null)
                {
                    if (value == null) _globals.Remove(root);
                    else _globals[root] = value;
                    return;
                }

                if (!_globals.TryGetValue(root, out var target) || !(target is RoLuaTable table))
                    throw new RoLuaException(at, $"'{root}' is not a table of this file");
                for (int i = 0; i < path.Count - 1; i++)
                {
                    var next = Get(table, path[i]);
                    table = next as RoLuaTable ?? throw new RoLuaException(at, $"'{root}' has no table at '{path[i]}'");
                }
                Set(table, path[path.Count - 1], value);
            }

            // A value: table, string, number, boolean, nil (null), a function (skipped: null) or a reference.
            RoLuaValue Value()
            {
                switch (_lx.Kind)
                {
                    case RoLuaToken.String:
                    {
                        var s = new RoLuaString(_lx.StringValue());
                        _lx.Next();
                        return s;
                    }
                    case RoLuaToken.Number:
                    {
                        var n = new RoLuaNumber(_lx.NumberValue());
                        _lx.Next();
                        return n;
                    }
                    case RoLuaToken.Symbol:
                        if (_lx.IsSymbol("{")) return Table();
                        if (_lx.IsSymbol("-"))
                        {
                            _lx.Next();
                            if (_lx.Kind != RoLuaToken.Number) throw _lx.Error("only a number can be negated");
                            var n = new RoLuaNumber(-_lx.NumberValue());
                            _lx.Next();
                            return n;
                        }
                        break;
                    case RoLuaToken.Name:
                        if (_lx.IsName("true")) { _lx.Next(); return new RoLuaBoolean(true); }
                        if (_lx.IsName("false")) { _lx.Next(); return new RoLuaBoolean(false); }
                        if (_lx.IsName("nil")) { _lx.Next(); return null; }
                        if (_lx.IsName("function"))
                        {
                            _lx.Next();
                            SkipFunctionBody();
                            return null;
                        }
                        if (!_lx.IsKeyword()) return Reference();
                        break;
                }
                throw _lx.Error($"unexpected {_lx.Describe()}");
            }

            RoLuaValue Reference()
            {
                int at = _lx.Start;
                string root = _lx.Text();
                _lx.Next();
                RoLuaValue current;
                if (!_globals.TryGetValue(root, out current) && (_imports == null || !_imports.TryGetValue(root, out current)))
                {
                    // D17: an unknown bare name is nil, as in Lua; a field of it is an error, as in Lua.
                    if (_lx.IsSymbol(".") || _lx.IsSymbol("[")) throw new RoLuaException(at, $"unknown name '{root}'");
                    current = null;
                }

                string shown = root;
                while (true)
                {
                    object key;
                    if (_lx.IsSymbol("."))
                    {
                        _lx.Next();
                        key = ExpectName();
                    }
                    else if (_lx.IsSymbol("["))
                    {
                        _lx.Next();
                        key = Key(Value(), at);
                        Expect("]");
                    }
                    else
                    {
                        break;
                    }
                    if (!(current is RoLuaTable table)) throw new RoLuaException(at, $"'{shown}' is not a table");
                    current = Get(table, key);
                    shown += "." + key;
                }
                if (IsCallArguments() || _lx.IsSymbol(":")) throw _lx.Error("a call is not data");
                return current;
            }

            RoLuaTable Table()
            {
                int open = _lx.Start;
                _lx.Next();
                var table = new RoLuaTable();
                while (!_lx.IsSymbol("}"))
                {
                    if (_lx.Kind == RoLuaToken.Eof) throw new RoLuaException(open, "unclosed table");

                    if (_lx.IsSymbol("["))
                    {
                        int at = _lx.Start;
                        _lx.Next();
                        var keyValue = Value();
                        Expect("]");
                        Expect("=");
                        object key = Key(keyValue, at);
                        if (key is string name && _skipField != null && _skipField(name)) SkipValue();
                        else Set(table, key, Value());
                    }
                    else if (_lx.Kind == RoLuaToken.Name && !_lx.IsKeyword() && NextIsAssign())
                    {
                        string name = _lx.Text();
                        _lx.Next();
                        _lx.Next(); // '='
                        if (_skipField != null && _skipField(name)) SkipValue();
                        else table.Set(name, Value());
                    }
                    else
                    {
                        table.Add(Value()); // nil leaves a hole (D17)
                    }

                    if (_lx.IsSymbol(",") || _lx.IsSymbol(";")) _lx.Next();
                    else if (!_lx.IsSymbol("}")) throw _lx.Error($"unexpected {_lx.Describe()} in a table");
                }
                _lx.Next();
                return table;
            }

            bool NextIsAssign()
            {
                int resume = _lx.Start;
                _lx.Restore(_lx.State);
                bool assign = _lx.IsSymbol("=");
                _lx.Restore(resume);
                return assign;
            }

            static object Key(RoLuaValue value, int at)
            {
                if (value == null) throw new RoLuaException(at, "a table key is nil");
                if (value.TryGetString(out string s)) return s;
                if (value.TryGetInteger(out long n)) return n;
                throw new RoLuaException(at, $"a table key must be a string or an integer, not {value.Kind}");
            }

            static RoLuaValue Get(RoLuaTable table, object key)
            {
                RoLuaValue v;
                return key is string s ? (table.TryGet(s, out v) ? v : null) : (table.TryGet((long)key, out v) ? v : null);
            }

            static void Set(RoLuaTable table, object key, RoLuaValue value)
            {
                if (key is string s) table.Set(s, value);
                else table.Set((long)key, value);
            }

            string ExpectName()
            {
                if (_lx.Kind != RoLuaToken.Name || _lx.IsKeyword()) throw _lx.Error($"expected a name, found {_lx.Describe()}");
                string name = _lx.Text();
                _lx.Next();
                return name;
            }

            void Expect(string symbol)
            {
                if (!_lx.IsSymbol(symbol)) throw _lx.Error($"expected '{symbol}', found {_lx.Describe()}");
                _lx.Next();
            }

            bool IsCallArguments() => _lx.IsSymbol("(") || _lx.IsSymbol("{") || _lx.Kind == RoLuaToken.String;

            void SkipCallArguments()
            {
                if (_lx.Kind == RoLuaToken.String) { _lx.Next(); return; }
                SkipBalanced();
            }

            // From an opening '(', '{' or '[' to its matching close, strings and comments handled by the lexer.
            void SkipBalanced()
            {
                int open = _lx.Start;
                int depth = 0;
                do
                {
                    if (_lx.Kind == RoLuaToken.Eof) throw new RoLuaException(open, "unclosed bracket");
                    if (_lx.IsSymbol("(") || _lx.IsSymbol("{") || _lx.IsSymbol("[")) depth++;
                    else if (_lx.IsSymbol(")") || _lx.IsSymbol("}") || _lx.IsSymbol("]")) depth--;
                    _lx.Next();
                } while (depth > 0);
            }

            // One value whose field was skipped: up to the ',' / ';' / '}' that ends it, nothing built.
            void SkipValue()
            {
                int start = _lx.Start;
                int depth = 0;
                while (true)
                {
                    if (_lx.Kind == RoLuaToken.Eof) throw new RoLuaException(start, "unclosed table");
                    if (depth == 0 && (_lx.IsSymbol(",") || _lx.IsSymbol(";") || _lx.IsSymbol("}"))) return;
                    if (_lx.IsName("function"))
                    {
                        _lx.Next();
                        SkipFunctionBody();
                        continue;
                    }
                    if (_lx.IsSymbol("(") || _lx.IsSymbol("{") || _lx.IsSymbol("[")) depth++;
                    else if (_lx.IsSymbol(")") || _lx.IsSymbol("}") || _lx.IsSymbol("]")) depth--;
                    _lx.Next();
                }
            }

            // After 'function': the name path of a function statement (a.b:c).
            void SkipFunctionName()
            {
                ExpectName();
                while (_lx.IsSymbol(".") || _lx.IsSymbol(":"))
                {
                    _lx.Next();
                    ExpectName();
                }
            }

            // '(' params ')' body 'end'.
            void SkipFunctionBody()
            {
                if (!_lx.IsSymbol("(")) throw _lx.Error($"expected '(' after function, found {_lx.Describe()}");
                int start = _lx.Start;
                SkipBalanced();
                SkipUntilEnd(1, start);
            }

            // Skips a block by keyword counting: 'function', 'do', 'if' and 'repeat' open, 'end' and 'until' close ('while'
            // and 'for' open through their 'do'). Returns after the closing keyword that brings the depth back to zero;
            // with depth 0 the block opens with its first 'do'.
            void SkipUntilEnd(int depth, int start = -1)
            {
                if (start < 0) start = _lx.Start;
                bool opened = depth > 0;
                while (true)
                {
                    if (_lx.Kind == RoLuaToken.Eof) throw new RoLuaException(start, "unclosed block (missing 'end')");
                    if (_lx.Kind == RoLuaToken.Name)
                    {
                        if (_lx.IsName("function") || _lx.IsName("do") || _lx.IsName("if") || _lx.IsName("repeat"))
                        {
                            depth++;
                            opened = true;
                        }
                        else if (_lx.IsName("end") || _lx.IsName("until"))
                        {
                            depth--;
                            if (depth < 0) throw _lx.Error("unexpected 'end'");
                            if (depth == 0 && opened)
                            {
                                _lx.Next();
                                return;
                            }
                        }
                    }
                    _lx.Next();
                }
            }
        }
    }
}
