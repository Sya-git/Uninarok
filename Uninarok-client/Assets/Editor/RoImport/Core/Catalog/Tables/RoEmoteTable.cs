using System;
using System.Collections.Generic;

namespace Uninarok.RoImport.Core.Catalog.Tables
{
    /// <summary>
    /// The emote ids of <c>emotion/emotionlist.lub</c> (PLAN-0008 4.2, 4.3.9; FR-6): the top-level <c>ET_*</c> globals
    /// (<c>ET_SURPRISE = 0</c>) and <c>EMOTION_ORDERLIST</c>. The loader calls after them are skipped by the reader.
    /// </summary>
    public sealed class RoEmoteTable
    {
        public const string File = "emotionlist.lub";
        const string Prefix = "ET_";

        RoEmoteTable(Dictionary<string, int> ids, int[] order)
        {
            Ids = ids;
            Order = order;
        }

        /// <summary><c>ET_SURPRISE</c> → 0.</summary>
        public IReadOnlyDictionary<string, int> Ids { get; }

        /// <summary><c>EMOTION_ORDERLIST</c>: the emote ids in the order of the client's emote window (empty when absent).</summary>
        public IReadOnlyList<int> Order { get; }

        public static bool TryRead(string lua, out RoEmoteTable table, out string reason)
        {
            table = null;
            if (!RoLuaTables.TryRead(lua, File, null, null, out var chunk, out reason)) return false;

            var ids = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var kv in chunk.Globals)
                if (kv.Key.StartsWith(Prefix, StringComparison.Ordinal) && kv.Value.TryGetInteger(out long id)) ids[kv.Key] = (int)id;
            if (ids.Count == 0)
            {
                reason = $"{File}: no {Prefix}* ids";
                return false;
            }

            var order = new List<int>();
            if (chunk.TryGetTable("EMOTION_ORDERLIST", out var list))
                foreach (var item in list.Items)
                    if (item.TryGetInteger(out long id)) order.Add((int)id);
            table = new RoEmoteTable(ids, order.ToArray());
            return true;
        }
    }
}
