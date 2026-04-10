using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Trade
{
    /// <summary>
    /// Тип контракта (GDD_25 секция 6.1)
    /// </summary>
    public enum ContractType
    {
        Standard,   // Стандартная доставка, фиксированная награда
        Urgent,     // Срочная доставка, таймер ×0.5, награда ×1.5
        Receipt     // "Под расписку" — товар бесплатно, не доставил = долг ×1.5
    }

    /// <summary>
    /// Состояние контракта
    /// </summary>
    public enum ContractState
    {
        Pending,    // Ожидает принятия
        Active,     // Активен, выполняется
        Completed,  // Успешно завершён
        Failed      // Провален (таймер истёк или отменён)
    }

    /// <summary>
    /// Уровень долга игрока (GDD_25 секция 6.2)
    /// </summary>
    public enum DebtLevel
    {
        None,           // 0 CR — нет долга
        Warning,        // 1-100 CR — предупреждение
        Restricted,     // 100-500 CR — ограничение контрактов
        Hunted,         // 500-1000 CR — патруль НП преследует
        Bounty,         // 1000+ CR — ордер на арест
        Headhunt        // 5000+ CR — наёмные охотники
    }

    /// <summary>
    /// Данные контракта на доставку груза.
    /// GDD_25 секция 6: Контрактная Система.
    /// Сессия 7: ContractSystem.
    /// </summary>
    [Serializable]
    public class ContractData
    {
        // === Идентификация ===
        [Tooltip("Уникальный ID контракта: contract_{fromLocation}_{itemId}_{index}")]
        public string contractId;

        [Tooltip("Тип контракта")]
        public ContractType type;

        [Tooltip("Текущее состояние")]
        public ContractState state = ContractState.Pending;

        // === Груз и маршрут ===
        [Tooltip("ID товара (TradeItemDefinition.itemId)")]
        public string itemId;

        [Tooltip("Количество единиц товара")]
        public int quantity;

        [Tooltip("ID локации отправления (primium, secundus, tertius, quartus)")]
        public string fromLocationId;

        [Tooltip("ID локации назначения")]
        public string toLocationId;

        // === Награда и стоимость ===
        [Tooltip("Награда за выполнение (вычисляется)")]
        public float reward;

        [Tooltip("Стоимость груза (basePrice × quantity) — для расписки")]
        public float cargoValue;

        // === Таймер (реальное время в секундах) ===
        [Tooltip("Лимит времени в секундах (0 = без лимита)")]
        public float timeLimit;

        [Tooltip("Оставшееся время в секундах")]
        public float timeRemaining;

        // === Игрок ===
        [Tooltip("ID игрока, принявшего контракт (0 = свободен)")]
        public ulong assignedPlayerId;

        // === Расписка (для типа Receipt) ===
        [Tooltip("Это контракт «под расписку»?")]
        public bool isReceiptContract;

        // ==================== МЕТОДЫ ====================

        /// <summary>
        /// Создать новый контракт с автоматическим расчётом награды.
        /// GDD_25 секция 6.3: Награды за контракты.
        /// </summary>
        public static ContractData Create(
            ContractType type,
            string itemId,
            int quantity,
            string fromLocationId,
            string toLocationId,
            float itemBasePrice,
            float distanceKm,
            float npReputation = 0f)
        {
            var contract = new ContractData
            {
                contractId = $"contract_{fromLocationId}_{itemId}_{UnityEngine.Random.Range(1000, 9999)}",
                type = type,
                state = ContractState.Pending,
                itemId = itemId,
                quantity = quantity,
                fromLocationId = fromLocationId,
                toLocationId = toLocationId,
                assignedPlayerId = 0,
                isReceiptContract = (type == ContractType.Receipt)
            };

            // cargoValue = basePrice × quantity
            contract.cargoValue = itemBasePrice * quantity;

            // Расчёт награды по формуле GDD_25 секция 6.3:
            // reward = basePrice × quantity × 0.3 × distanceMultiplier × repBonus
            float baseReward = contract.cargoValue * 0.3f;

            // distanceMultiplier = 1.0 + (distanceKm / 100) × 0.5
            float distanceMultiplier = 1.0f + (distanceKm / 100f) * 0.5f;

            // reputationBonus = 1.0 + (rep_NP / 100) × 0.2
            float reputationBonus = 1.0f + (npReputation / 100f) * 0.2f;

            contract.reward = baseReward * distanceMultiplier * reputationBonus;

            // Urgent: награда ×1.5
            if (type == ContractType.Urgent)
            {
                contract.reward *= 1.5f;
            }

            // Receipt: награда = 30% от стоимости (уже посчитано выше)
            if (type == ContractType.Receipt)
            {
                // Для расписки награда чуть меньше — это "обучающий" контракт
                contract.reward = contract.cargoValue * 0.3f * distanceMultiplier;
            }

            // Таймер (реальное время, секунды) — утверждено решение 3A
            switch (type)
            {
                case ContractType.Urgent:
                    contract.timeLimit = 150f;   // 2.5 мин
                    break;
                case ContractType.Standard:
                    contract.timeLimit = 300f;   // 5 мин
                    break;
                case ContractType.Receipt:
                    contract.timeLimit = 600f;   // 10 мин (туториал, больше времени)
                    break;
            }
            contract.timeRemaining = contract.timeLimit;

            return contract;
        }

        /// <summary>
        /// Активировать контракт (принят игроком)
        /// </summary>
        public void Activate(ulong playerId)
        {
            assignedPlayerId = playerId;
            state = ContractState.Active;
            // Таймер уже установлен в Create()
        }

        /// <summary>
        /// Обновить таймер (вызывается каждый кадр или тик)
        /// </summary>
        public void TickTimer(float deltaTime)
        {
            if (state != ContractState.Active) return;
            if (timeLimit <= 0f) return; // Без лимита

            timeRemaining -= deltaTime;
            if (timeRemaining <= 0f)
            {
                timeRemaining = 0f;
                state = ContractState.Failed;
            }
        }

        /// <summary>
        /// Завершить контракт успешно
        /// </summary>
        public void Complete()
        {
            state = ContractState.Completed;
        }

        /// <summary>
        /// Провалить контракт
        /// </summary>
        public void Fail()
        {
            state = ContractState.Failed;
        }

        /// <summary>
        /// Получить отображаемое имя типа контракта
        /// </summary>
        public string GetTypeDisplayName()
        {
            switch (type)
            {
                case ContractType.Standard: return "[Стандарт]";
                case ContractType.Urgent: return "[Срочный]";
                case ContractType.Receipt: return "[Расписка]";
                default: return type.ToString();
            }
        }

        /// <summary>
        /// Получить цвет типа контракта для UI
        /// </summary>
        public Color GetTypeColor()
        {
            switch (type)
            {
                case ContractType.Standard: return new Color(0.3f, 0.6f, 1f); // синий
                case ContractType.Urgent: return new Color(1f, 0.5f, 0f);     // оранжевый
                case ContractType.Receipt: return new Color(0.3f, 1f, 0.3f);  // зелёный
                default: return Color.white;
            }
        }

        /// <summary>
        /// Получить оставшееся время в читаемом формате
        /// </summary>
        public string GetTimeRemainingString()
        {
            if (timeLimit <= 0f) return "∞";
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);
            return $"{minutes}:{seconds:D2}";
        }

        /// <summary>
        /// Процент оставшегося времени (0-1)
        /// </summary>
        public float GetTimePercent()
        {
            if (timeLimit <= 0f) return 1f;
            return timeRemaining / timeLimit;
        }
    }
}
