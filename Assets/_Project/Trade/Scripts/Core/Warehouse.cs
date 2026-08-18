using System;
using System.Collections.Generic;

namespace ProjectC.Trade.Core
{
    /// <summary>
    /// Запись склада: один тип товара + количество.
    /// </summary>
    [Serializable]
    public struct WarehouseEntry
    {
        public string itemId;
        public int quantity;

        // Empty contractId means ordinary warehouse/cargo cargo.
        // Contract-owned entries are only valid in CargoData, never in Warehouse.
        public string contractId;
        public ulong contractOwnerPlayerId;

        public bool IsContractOwned => !string.IsNullOrEmpty(contractId);

        public WarehouseEntry(string itemId, int quantity)
        {
            this.itemId = itemId;
            this.quantity = quantity;
            contractId = null;
            contractOwnerPlayerId = 0;
        }

        public WarehouseEntry(string itemId, int quantity, string contractId, ulong contractOwnerPlayerId)
        {
            this.itemId = itemId;
            this.quantity = quantity;
            this.contractId = contractId;
            this.contractOwnerPlayerId = contractOwnerPlayerId;
        }
    }

    /// <summary>
    /// Склад игрока на конкретной локации. POCO, server-only, in-memory.
    /// Персистится через <see cref="Repository.IPlayerDataRepository"/>.
    ///
    /// Ключ: (ownerClientId, locationId) — склад ОДНОЙ локации.
    /// (В старой системе была путаница «глобальный vs локальный» — здесь однозначно привязан к локации.)
    /// </summary>
    public class Warehouse
    {
        public readonly ulong ownerClientId;
        public readonly string locationId;
        private readonly List<WarehouseEntry> _items = new List<WarehouseEntry>();

        public IReadOnlyList<WarehouseEntry> Items => _items;

        // Лимиты — сейчас захардкожены (Stage 2.5). В будущем — в PlayerProgression/SO.
        public const float DEFAULT_MAX_WEIGHT = 10000f;
        public const float DEFAULT_MAX_VOLUME = 200f;
        public const int DEFAULT_MAX_ITEM_TYPES = 50;

        public float MaxWeight { get; set; } = DEFAULT_MAX_WEIGHT;
        public float MaxVolume { get; set; } = DEFAULT_MAX_VOLUME;
        public int MaxItemTypes { get; set; } = DEFAULT_MAX_ITEM_TYPES;

        public Warehouse(ulong ownerClientId, string locationId)
        {
            this.ownerClientId = ownerClientId;
            this.locationId = locationId;
        }

        public int GetQuantity(string itemId)
        {
            for (int i = 0; i < _items.Count; i++)
                if (!_items[i].IsContractOwned && _items[i].itemId == itemId) return _items[i].quantity;
            return 0;
        }

        public bool TryAdd(string itemId, int quantity, TradeItemDefinitionResolver resolver, out string failReason)
        {
            failReason = null;
            if (string.IsNullOrEmpty(itemId) || quantity <= 0) { failReason = "invalid_args"; return false; }

            float itemWeight = resolver.GetWeight(itemId);
            float itemVolume = resolver.GetVolume(itemId);

            int existingIdx = -1;
            for (int i = 0; i < _items.Count; i++)
                if (!_items[i].IsContractOwned && _items[i].itemId == itemId) { existingIdx = i; break; }

            if (existingIdx < 0)
            {
                if (_items.Count >= MaxItemTypes) { failReason = "warehouse_max_types"; return false; }
            }

            float newWeight = ComputeTotalWeight(resolver) + itemWeight * quantity;
            float newVolume = ComputeTotalVolume(resolver) + itemVolume * quantity;
            if (newWeight > MaxWeight) { failReason = "warehouse_max_weight"; return false; }
            if (newVolume > MaxVolume) { failReason = "warehouse_max_volume"; return false; }

            if (existingIdx >= 0)
            {
                var e = _items[existingIdx];
                e.quantity += quantity;
                _items[existingIdx] = e;
            }
            else
            {
                _items.Add(new WarehouseEntry(itemId, quantity));
            }
            return true;
        }

        public bool TryRemove(string itemId, int quantity, out string failReason)
        {
            failReason = null;
            if (string.IsNullOrEmpty(itemId) || quantity <= 0) { failReason = "invalid_args"; return false; }
            int idx = -1;
            for (int i = 0; i < _items.Count; i++)
                if (!_items[i].IsContractOwned && _items[i].itemId == itemId) { idx = i; break; }
            if (idx < 0) { failReason = "item_not_in_warehouse"; return false; }
            if (_items[idx].quantity < quantity) { failReason = "insufficient_quantity"; return false; }

            var e = _items[idx];
            e.quantity -= quantity;
            if (e.quantity <= 0) _items.RemoveAt(idx);
            else _items[idx] = e;
            return true;
        }

        public float ComputeTotalWeight(TradeItemDefinitionResolver resolver)
        {
            float t = 0;
            for (int i = 0; i < _items.Count; i++)
                t += resolver.GetWeight(_items[i].itemId) * _items[i].quantity;
            return t;
        }

        public float ComputeTotalVolume(TradeItemDefinitionResolver resolver)
        {
            float t = 0;
            for (int i = 0; i < _items.Count; i++)
                t += resolver.GetVolume(_items[i].itemId) * _items[i].quantity;
            return t;
        }

        /// <summary>
        /// Загрузить из репозитория (вызывается TradeWorld при первом обращении).
        /// </summary>
        public void LoadFrom(List<WarehouseEntry> items)
        {
            _items.Clear();
            if (items == null) return;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].IsContractOwned
                    && !string.IsNullOrEmpty(items[i].itemId)
                    && items[i].quantity > 0)
                {
                    _items.Add(new WarehouseEntry(items[i].itemId, items[i].quantity));
                }
        }

        public List<WarehouseEntry> SaveToList()
        {
            var list = new List<WarehouseEntry>(_items.Count);
            for (int i = 0; i < _items.Count; i++)
                if (!_items[i].IsContractOwned && _items[i].quantity > 0)
                    list.Add(new WarehouseEntry(_items[i].itemId, _items[i].quantity));
            return list;
        }

        public void Clear() => _items.Clear();
    }
}
