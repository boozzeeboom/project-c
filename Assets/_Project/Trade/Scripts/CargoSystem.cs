using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;
using ProjectC.Trade;

namespace ProjectC.Player
{
    /// <summary>
    /// Тип корабля для определения характеристик груза
    /// </summary>
    public enum ShipClass
    {
        Light,      // Лёгкий: 4 слота, 100 кг, 3 м³
        Medium,     // Средний: 10 слотов, 500 кг, 12 м³
        HeavyI,     // Тяжёлый I: 20 слотов, 2000 кг, 40 м³
        HeavyII     // Тяжёлый II: 30 слотов, 5000 кг, 80 м³
    }

    /// <summary>
    /// Один элемент груза
    /// </summary>
    [Serializable]
    public class CargoItem
    {
        public TradeItemDefinition item;
        public int quantity;
    }

    /// <summary>
    /// Система груза корабля — отдельно от личного инвентаря.
    /// Влияет на скорость, может протекать/повреждаться при столкновении.
    /// Пока локальный (без сети) — сеть будет в Сессии 5.
    /// </summary>
    public class CargoSystem : MonoBehaviour
    {
        [Header("Ship Class")]
        [Tooltip("Класс корабля — определяет лимиты")]
        public ShipClass shipClass = ShipClass.Light;

        [Header("Cargo")]
        [Tooltip("Список грузов")]
        public List<CargoItem> cargo = new List<CargoItem>();

        // Характеристики по классу корабля (GDD_25 секция 4.1)
        private static readonly Dictionary<ShipClass, ShipCargoLimits> ShipLimits = new Dictionary<ShipClass, ShipCargoLimits>
        {
            { ShipClass.Light,    new ShipCargoLimits { maxSlots = 4,  maxWeight = 100f,  maxVolume = 3f,  penaltyFactor = 0.05f } },
            { ShipClass.Medium,   new ShipCargoLimits { maxSlots = 10, maxWeight = 500f,  maxVolume = 12f, penaltyFactor = 0.08f } },
            { ShipClass.HeavyI,   new ShipCargoLimits { maxSlots = 20, maxWeight = 2000f, maxVolume = 40f, penaltyFactor = 0.10f } },
            { ShipClass.HeavyII,  new ShipCargoLimits { maxSlots = 30, maxWeight = 5000f, maxVolume = 80f, penaltyFactor = 0.12f } },
        };

        private ShipCargoLimits Limits => ShipLimits[shipClass];

        // ==================== СВОЙСТВА ====================

        /// <summary>
        /// Текущий вес груза (кг)
        /// </summary>
        public float CurrentWeight
        {
            get
            {
                float total = 0f;
                foreach (var c in cargo)
                {
                    if (c.item != null)
                        total += c.item.weight * c.quantity;
                }
                return total;
            }
        }

        /// <summary>
        /// Текущий объём груза (м³)
        /// </summary>
        public float CurrentVolume
        {
            get
            {
                float total = 0f;
                foreach (var c in cargo)
                {
                    if (c.item != null)
                        total += c.item.volume * c.quantity;
                }
                return total;
            }
        }

        /// <summary>
        /// Использованные слоты
        /// </summary>
        public int UsedSlots
        {
            get
            {
                int total = 0;
                foreach (var c in cargo)
                {
                    if (c.item != null)
                        total += c.item.slots * c.quantity;
                }
                return total;
            }
        }

        public int MaxSlots => Limits.maxSlots;
        public float MaxWeight => Limits.maxWeight;
        public float MaxVolume => Limits.maxVolume;

        // ==================== МЕТОДЫ ====================

        /// <summary>
        /// Добавить груз в трюм
        /// </summary>
        /// <returns>true если успешно, false если не хватает места</returns>
        public bool AddCargo(TradeItemDefinition item, int quantity)
        {
            if (item == null || quantity <= 0) return false;

            // Проверяем лимиты
            float newWeight = CurrentWeight + item.weight * quantity;
            float newVolume = CurrentVolume + item.volume * quantity;
            int newSlots = UsedSlots + item.slots * quantity;

            if (newWeight > MaxWeight || newVolume > MaxVolume || newSlots > MaxSlots)
            {
                Debug.LogWarning($"[CargoSystem] Не хватает места для {item.displayName} x{quantity}\n" +
                    $"Вес: {CurrentWeight:F1}/{MaxWeight} → {newWeight:F1} | " +
                    $"Объём: {CurrentVolume:F1}/{MaxVolume} → {newVolume:F1} | " +
                    $"Слоты: {UsedSlots}/{MaxSlots} → {newSlots}");
                return false;
            }

            // Ищем существующий стаковый элемент
            var existing = cargo.Find(c => c.item == item);
            if (existing != null)
            {
                existing.quantity += quantity;
            }
            else
            {
                cargo.Add(new CargoItem { item = item, quantity = quantity });
            }

            return true;
        }

        /// <summary>
        /// Убрать груз из трюма
        /// </summary>
        public bool RemoveCargo(string itemId, int quantity)
        {
            var cargoItem = cargo.Find(c => c.item != null && c.item.itemId == itemId);
            if (cargoItem == null)
            {
                Debug.LogWarning($"[CargoSystem] Предмет {itemId} не найден в трюме");
                return false;
            }

            if (cargoItem.quantity < quantity)
            {
                Debug.LogWarning($"[CargoSystem] Недостаточно {itemId}: есть {cargoItem.quantity}, нужно {quantity}");
                return false;
            }

            cargoItem.quantity -= quantity;
            if (cargoItem.quantity <= 0)
            {
                cargo.Remove(cargoItem);
            }

            return true;
        }

        /// <summary>
        /// Получить количество конкретного предмета в трюме
        /// </summary>
        public int GetItemQuantity(string itemId)
        {
            var cargoItem = cargo.Find(c => c.item != null && c.item.itemId == itemId);
            return cargoItem?.quantity ?? 0;
        }

        // ==================== ФИЗИКА (GDD_25 секция 4.4) ====================

        /// <summary>
        /// Расчёт множителя скорости от груза
        /// formula: 1.0 - (cargo_weight / max_capacity) × penalty_factor
        /// Перегруз: дополнительный штраф -20% за каждые 10% сверх лимита
        /// </summary>
        public float GetSpeedPenalty()
        {
            float weightRatio = CurrentWeight / MaxWeight;
            float penaltyFactor = Limits.penaltyFactor;

            float speedMultiplier = 1.0f - weightRatio * penaltyFactor;

            // Перегруз: штраф за каждые 10% сверх лимита
            if (weightRatio > 1.0f)
            {
                float overloadPercent = (weightRatio - 1.0f) / 0.1f; // каждые 10%
                float overloadPenalty = Mathf.Floor(overloadPercent) * 0.20f;
                speedMultiplier -= overloadPenalty;
            }

            return Mathf.Max(0f, speedMultiplier);
        }

        // ==================== СТОЛКНОВЕНИЯ (GDD_25 секция 4.3) ====================

        /// <summary>
        /// Проверка протечки опасного груза при столкновении (5% шанс для каждого isDangerous предмета)
        /// </summary>
        /// <returns>true если протечка произошла</returns>
        public bool CheckLeakOnCollision()
        {
            foreach (var c in cargo)
            {
                if (c.item != null && c.item.isDangerous)
                {
                    // 5% шанс на каждый стаковый элемент
                    if (UnityEngine.Random.value < 0.05f)
                    {
                        int leakedAmount = Mathf.Max(1, Mathf.CeilToInt(c.quantity * 0.1f)); // 10% утечка
                        Debug.LogWarning($"[CargoSystem] ПРОТЕЧКА: {c.item.displayName} — потеряно {leakedAmount} ед.!");
                        c.quantity -= leakedAmount;
                        if (c.quantity <= 0)
                            cargo.Remove(c);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Проверка повреждения хрупкого груза при столкновении (10% шанс)
        /// </summary>
        /// <returns>true если повреждение произошло</returns>
        public bool CheckFragileDamageOnCollision()
        {
            foreach (var c in cargo)
            {
                if (c.item != null && c.item.isFragile)
                {
                    // 10% шанс
                    if (UnityEngine.Random.value < 0.10f)
                    {
                        Debug.LogWarning($"[CargoSystem] ПОВРЕЖДЕНИЕ: {c.item.displayName} x{c.quantity} — хрупкий груз повреждён!");
                        // Пока просто лог — в будущем можно добавить статус "damaged" с пониженной ценой
                        return true;
                    }
                }
            }
            return false;
        }

        // ==================== УТИЛИТЫ ====================

        public void ClearCargo()
        {
            cargo.Clear();
        }

        private void OnValidate()
        {
            Debug.Log($"[CargoSystem] Класс: {shipClass} | " +
                $"Слоты: {MaxSlots}, Вес: {MaxWeight} кг, Объём: {MaxVolume} м³, Penalty: {Limits.penaltyFactor}");
        }
    }

    /// <summary>
    /// Характеристики лимитов для класса корабля
    /// </summary>
    [Serializable]
    public struct ShipCargoLimits
    {
        public int maxSlots;
        public float maxWeight;
        public float maxVolume;
        public float penaltyFactor;
    }
}
