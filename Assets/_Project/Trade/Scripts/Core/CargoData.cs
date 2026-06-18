using System.Collections.Generic;
using ProjectC.Player;
using ProjectC.Trade.Core; // T-CARGO-01: ShipClass живёт здесь (перенесён из ProjectC.Player)
using UnityEngine;

namespace ProjectC.Trade.Core
{
    /// <summary>
    /// Server-side данные о грузе корабля. POCO.
    ///
    /// Привязан к NetworkObjectId корабля (НЕ к clientId — несколько игроков
    /// могут иметь корабли с разными NetworkObjectId; один игрок может
    /// управлять несколькими кораблями в зоне).
    ///
    /// Хранится в <see cref="Repository.IPlayerDataRepository"/> с ключом
    /// «ship:{shipNetworkObjectId}» — это позволяет восстановить груз при
    /// перезагрузке сервера / переподключении.
    /// </summary>
    public class CargoData
    {
        public readonly ulong shipNetworkObjectId;
        public readonly ShipClass shipClass;
        private readonly List<WarehouseEntry> _items = new List<WarehouseEntry>();

        // T-CARGO-06: если override установлен (из ShipCargoRegistry) — TryAdd
        // использует его вместо статического ShipClassLimits.Get(shipClass).
        private ShipClassLimits.Limits? _limitsOverride;

        public void SetLimitsOverride(ShipClassLimits.Limits limits)
        {
            _limitsOverride = limits;
        }

        public void ClearLimitsOverride()
        {
            _limitsOverride = null;
        }

        public IReadOnlyList<WarehouseEntry> Items => _items;

        public CargoData(ulong shipNetworkObjectId, ShipClass shipClass)
        {
            this.shipNetworkObjectId = shipNetworkObjectId;
            this.shipClass = shipClass;
        }

        public int GetQuantity(string itemId)
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].itemId == itemId) return _items[i].quantity;
            return 0;
        }

        public bool TryAdd(string itemId, int quantity, TradeItemDefinitionResolver resolver, out string failReason)
        {
            failReason = null;
            if (string.IsNullOrEmpty(itemId) || quantity <= 0) { failReason = "invalid_args"; return false; }

            // T-CARGO-06: лимиты = _limitsOverride (если установлен TradeWorld) или
            // статический fallback по shipClass. Установка через SetLimitsOverride().
            var limits = _limitsOverride ?? ShipClassLimits.Get(shipClass);
            int itemSlots = resolver.GetSlots(itemId);
            float itemWeight = resolver.GetWeight(itemId);
            float itemVolume = resolver.GetVolume(itemId);

            float newWeight = ComputeTotalWeight(resolver) + itemWeight * quantity;
            float newVolume = ComputeTotalVolume(resolver) + itemVolume * quantity;
            int newSlots = ComputeTotalSlots(resolver) + itemSlots * quantity;

            if (newWeight > limits.maxWeight) { failReason = "cargo_max_weight"; return false; }
            if (newVolume > limits.maxVolume) { failReason = "cargo_max_volume"; return false; }
            if (newSlots > limits.maxSlots) { failReason = "cargo_max_slots"; return false; }

            int idx = -1;
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].itemId == itemId) { idx = i; break; }
            if (idx >= 0)
            {
                var e = _items[idx];
                e.quantity += quantity;
                _items[idx] = e;
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
                if (_items[i].itemId == itemId) { idx = i; break; }
            if (idx < 0) { failReason = "item_not_in_cargo"; return false; }
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

        public int ComputeTotalSlots(TradeItemDefinitionResolver resolver)
        {
            int t = 0;
            for (int i = 0; i < _items.Count; i++)
                t += resolver.GetSlots(_items[i].itemId) * _items[i].quantity;
            return t;
        }

        public void LoadFrom(List<WarehouseEntry> items)
        {
            _items.Clear();
            if (items == null) return;
            for (int i = 0; i < items.Count; i++)
                if (!string.IsNullOrEmpty(items[i].itemId) && items[i].quantity > 0)
                    _items.Add(items[i]);
        }

        public List<WarehouseEntry> SaveToList()
        {
            var list = new List<WarehouseEntry>(_items.Count);
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].quantity > 0) list.Add(_items[i]);
            return list;
        }

        public void Clear() => _items.Clear();
    }

    /// <summary>
    /// Лимиты корабля по классу. Копия значений из CargoSystem, чтобы серверная
    /// логика не зависела от MonoBehaviour-компонента.
    /// T-CARGO-03: penaltyFactor перенесён сюда из CargoSystem.cs:49-52,
    /// чтобы GetSpeedPenalty жил в TradeWorld, а не в удаляемом legacy-коде.
    /// </summary>
    public static class ShipClassLimits
    {
        public struct Limits
        {
            public int maxSlots;
            public float maxWeight;
            public float maxVolume;
            public float penaltyFactor; // T-CARGO-03: для GetSpeedPenalty
        }

        public static Limits Get(ShipClass cls)
        {
            switch (cls)
            {
                case ShipClass.Light:   return new Limits { maxSlots = 4,  maxWeight = 100f,  maxVolume = 3f,  penaltyFactor = 0.05f };
                case ShipClass.Medium:  return new Limits { maxSlots = 10, maxWeight = 500f,  maxVolume = 12f, penaltyFactor = 0.08f };
                case ShipClass.HeavyI:  return new Limits { maxSlots = 20, maxWeight = 2000f, maxVolume = 40f, penaltyFactor = 0.10f };
                case ShipClass.HeavyII: return new Limits { maxSlots = 30, maxWeight = 5000f, maxVolume = 80f, penaltyFactor = 0.12f };
                default: return new Limits { maxSlots = 4, maxWeight = 100f, maxVolume = 3f, penaltyFactor = 0.05f };
            }
        }
    }
}
