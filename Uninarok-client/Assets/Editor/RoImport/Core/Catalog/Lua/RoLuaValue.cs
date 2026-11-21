using System;
using System.Collections.Generic;

namespace Uninarok.RoImport.Core.Catalog.Lua
{
    /// <summary>The kind of a value read from a lub table (PLAN-0008 4.1).</summary>
    public enum RoLuaKind
    {
        Boolean,
        Number,
        String,
        Table,
    }

    /// <summary>
    /// A value of the Lua data subset the client's tables use (PLAN-0008 4.1): a boolean, a number, a string or a table.
    /// <c>nil</c> is not a value here: a field or global set to <c>nil</c> is absent.
    /// </summary>
    public abstract class RoLuaValue
    {
        public abstract RoLuaKind Kind { get; }

        public bool TryGetNumber(out double value)
        {
            if (this is RoLuaNumber n) { value = n.Value; return true; }
            value = 0;
            return false;
        }

        /// <summary>The value as an integer, when it is a number with no fraction inside the <c>long</c> range.</summary>
        public bool TryGetInteger(out long value)
        {
            value = 0;
            if (!(this is RoLuaNumber n)) return false;
            double d = n.Value;
            if (double.IsNaN(d) || double.IsInfinity(d) || Math.Floor(d) != d || d < long.MinValue || d > long.MaxValue) return false;
            value = (long)d;
            return true;
        }

        public bool TryGetString(out string value)
        {
            value = (this as RoLuaString)?.Value;
            return value != null;
        }
    }

    public sealed class RoLuaBoolean : RoLuaValue
    {
        public RoLuaBoolean(bool value) => Value = value;
        public bool Value { get; }
        public override RoLuaKind Kind => RoLuaKind.Boolean;
        public override string ToString() => Value ? "true" : "false";
    }

    public sealed class RoLuaNumber : RoLuaValue
    {
        public RoLuaNumber(double value) => Value = value;
        public double Value { get; }
        public override RoLuaKind Kind => RoLuaKind.Number;
        public override string ToString() => Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    }

    public sealed class RoLuaString : RoLuaValue
    {
        public RoLuaString(string value) => Value = value ?? throw new ArgumentNullException(nameof(value));
        public string Value { get; }
        public override RoLuaKind Kind => RoLuaKind.String;
        public override string ToString() => Value;
    }

    /// <summary>
    /// A table constructor: the positional part (<see cref="Items"/>, Lua keys 1..n), the fields with name or string keys
    /// (<see cref="Fields"/>) and the fields with integer keys (<see cref="Indexed"/>, from <c>[501] = …</c> or a reference
    /// such as <c>[jobtbl.JT_PORING] = …</c>). Keys are kept in the order they were first set.
    /// </summary>
    public sealed class RoLuaTable : RoLuaValue
    {
        readonly List<RoLuaValue> _items = new List<RoLuaValue>();
        readonly Dictionary<string, RoLuaValue> _fields = new Dictionary<string, RoLuaValue>(StringComparer.Ordinal);
        readonly Dictionary<long, RoLuaValue> _indexed = new Dictionary<long, RoLuaValue>();

        public override RoLuaKind Kind => RoLuaKind.Table;
        public IReadOnlyList<RoLuaValue> Items => _items;
        public IReadOnlyDictionary<string, RoLuaValue> Fields => _fields;
        public IReadOnlyDictionary<long, RoLuaValue> Indexed => _indexed;

        public bool TryGet(string key, out RoLuaValue value) => _fields.TryGetValue(key, out value);

        /// <summary>An integer key: an explicit <c>[k]</c> field, else the k-th positional item (Lua's 1-based array part).</summary>
        public bool TryGet(long key, out RoLuaValue value)
        {
            if (_indexed.TryGetValue(key, out value)) return true;
            if (key >= 1 && key <= _items.Count) { value = _items[(int)(key - 1)]; return true; }
            value = null;
            return false;
        }

        int _positions;

        /// <summary>
        /// Adds the next positional value. <c>null</c> (Lua <c>nil</c>) leaves a hole: the position is used and nothing is
        /// stored, and a later value lands in <see cref="Indexed"/> under its position (D17).
        /// </summary>
        public void Add(RoLuaValue item)
        {
            int position = ++_positions;
            if (item == null) return;
            if (position == _items.Count + 1) _items.Add(item);
            else _indexed[position] = item;
        }

        /// <summary>Sets a field; <c>null</c> (Lua <c>nil</c>) removes it.</summary>
        public void Set(string key, RoLuaValue value)
        {
            if (value == null) _fields.Remove(key);
            else _fields[key] = value;
        }

        /// <summary>Sets an integer field; <c>null</c> (Lua <c>nil</c>) removes it.</summary>
        public void Set(long key, RoLuaValue value)
        {
            if (value == null) _indexed.Remove(key);
            else _indexed[key] = value;
        }
    }
}
