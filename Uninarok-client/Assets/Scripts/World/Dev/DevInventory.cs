using System;
using System.Collections.Generic;
using System.Text;
using Uninarok.Networking.Client;
using Uninarok.Networking.Packets.In.Map;

namespace Uninarok.World.Dev
{
    /// <summary>
    /// The item ids and amounts the server told of, so a command can name an item (SPEC-0010 FR-20; PLAN-0010 4.12, D35): the
    /// items listed at entry, without those worn, and whatever came in since. No inventory of the game: that is a later spec's. Dev only.
    /// </summary>
    sealed class DevInventory : IDisposable
    {
        readonly Dictionary<ushort, (uint item, int amount)> _slots = new Dictionary<ushort, (uint, int)>();
        readonly Subscriptions _subscriptions;

        public DevInventory(INetworkClient client)
        {
            _subscriptions = new Subscriptions(client);
            _subscriptions.Add<AUTHENTICATION_ACCEPTED>(OnEntry);
            _subscriptions.Add<INVENTORY_LIST_NORMAL>(OnList);
            _subscriptions.Add<INVENTORY_LIST_EQUIP>(OnEquipList);
            _subscriptions.Add<ITEM_PICKUP_ACK>(OnAdded);
            _subscriptions.Add<DROP_ITEM_ACK>(OnRemoved);
        }

        /// <summary>The lowest slot that holds the item.</summary>
        public bool TryFind(uint item, out ushort index, out int amount)
        {
            index = ushort.MaxValue;
            amount = 0;
            foreach (var slot in _slots)
                if (slot.Value.item == item && slot.Key <= index) (index, amount) = (slot.Key, slot.Value.amount);
            return amount > 0;
        }

        /// <summary>"501 x3, 911 x10", by slot.</summary>
        public string Describe()
        {
            var keys = new List<ushort>(_slots.Keys);
            keys.Sort();
            var text = new StringBuilder();
            foreach (ushort key in keys) text.Append(text.Length > 0 ? ", " : "").Append(_slots[key].item).Append(" x").Append(_slots[key].amount);
            return text.Length > 0 ? text.ToString() : "no items";
        }

        public void Dispose() => _subscriptions.Clear();

        // A new map server session, maybe another character's.
        void OnEntry(in AUTHENTICATION_ACCEPTED p) => _slots.Clear();

        void OnList(in INVENTORY_LIST_NORMAL p)
        {
            if (p.ListType != INVENTORY_LIST_NORMAL.Inventory) return;
            foreach (var item in p.Items) _slots[item.Index] = (item.ItemId, item.Amount);
        }

        void OnEquipList(in INVENTORY_LIST_EQUIP p)
        {
            if (p.ListType != INVENTORY_LIST_NORMAL.Inventory) return;
            foreach (var item in p.Items)
                if (item.WornAt == 0) _slots[item.Index] = (item.ItemId, 1);
        }

        void OnAdded(in ITEM_PICKUP_ACK p)
        {
            if (p.Result != 0 || p.Amount == 0) return;
            int held = _slots.TryGetValue(p.InventoryIndex, out var slot) && slot.item == p.ItemId ? slot.amount : 0;
            _slots[p.InventoryIndex] = (p.ItemId, held + p.Amount);
        }

        void OnRemoved(in DROP_ITEM_ACK p)
        {
            if (!_slots.TryGetValue(p.InventoryIndex, out var slot)) return;
            if (slot.amount > p.Amount) _slots[p.InventoryIndex] = (slot.item, slot.amount - p.Amount);
            else _slots.Remove(p.InventoryIndex);
        }
    }
}
