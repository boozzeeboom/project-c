using System;
using System.Collections.Generic;
using ProjectC.Trade.Config;
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

        /// <summary>
        /// UTC ticks момента перехода в Completed/Failed.
        /// Ноль означает legacy snapshot без terminal timestamp.
        /// </summary>
        public long terminalAtUtcTicks;

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
            float npReputation = 0f,
            float standardTimeLimitSeconds = 300f,
            float urgentTimeLimitSeconds = 150f,
            float receiptTimeLimitSeconds = 600f)
        {
            float rewardMultiplier = type == ContractType.Urgent ? 1.5f : 1f;
            float timeLimitSeconds = standardTimeLimitSeconds;
            switch (type)
            {
                case ContractType.Urgent:
                    timeLimitSeconds = urgentTimeLimitSeconds;
                    break;
                case ContractType.Receipt:
                    timeLimitSeconds = receiptTimeLimitSeconds;
                    break;
            }

            return CreateConfigured(
                type,
                itemId,
                quantity,
                fromLocationId,
                toLocationId,
                itemBasePrice,
                distanceKm,
                npReputation,
                rewardMultiplier,
                timeLimitSeconds,
                type == ContractType.Receipt);
        }

        public static ContractData CreateConfigured(
            ContractType type,
            string itemId,
            int quantity,
            string fromLocationId,
            string toLocationId,
            float itemBasePrice,
            float distanceKm,
            float npReputation,
            float rewardMultiplier,
            float timeLimitSeconds,
            bool isReceiptContract)
        {
            fromLocationId = MarketConfigCollector.NormalizeLocationId(fromLocationId);
            toLocationId = MarketConfigCollector.NormalizeLocationId(toLocationId);

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
                isReceiptContract = isReceiptContract
            };

            contract.cargoValue = itemBasePrice * quantity;
            float baseReward = contract.cargoValue * 0.3f;
            float distanceMultiplier = 1.0f + (distanceKm / 100f) * 0.5f;
            float reputationBonus = isReceiptContract
                ? 1.0f
                : 1.0f + (npReputation / 100f) * 0.2f;

            contract.reward = baseReward
                * distanceMultiplier
                * reputationBonus
                * Mathf.Max(0f, rewardMultiplier);
            contract.timeLimit = Mathf.Max(0f, timeLimitSeconds);
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
                MarkTerminal();
            }
        }

        /// <summary>
        /// Завершить контракт успешно
        /// </summary>
        public void Complete()
        {
            state = ContractState.Completed;
            MarkTerminal();
        }

        /// <summary>
        /// Провалить контракт
        /// </summary>
        public void Fail()
        {
            state = ContractState.Failed;
            MarkTerminal();
        }

        private void MarkTerminal()
        {
            terminalAtUtcTicks = DateTime.UtcNow.Ticks;
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
