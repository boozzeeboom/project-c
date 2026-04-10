using UnityEngine;

namespace ProjectC.Trade
{
    /// <summary>
    /// Долговая система игрока.
    /// GDD_25 секция 6.2: Система «Под расписку» — Долговая система.
    /// GDD_23 секция 7.1: Торговые репутации — НП.
    ///
    /// Компонент вешается на NetworkPlayer сервером при первом долге.
    /// Все данные хранятся только на сервере.
    /// Сессия 7: ContractSystem.
    /// </summary>
    public class PlayerDebt : MonoBehaviour
    {
        [Header("Debt State")]
        [Tooltip("Текущий долг в кредитах")]
        [SerializeField] private float currentDebt;

        [Tooltip("Время последнего обновления долга (Time.time)")]
        [SerializeField] private float lastDebtUpdateTime;

        [Header("Settings")]
        [Tooltip("Процент затухания долга в день (1% = 0.01)")]
        [SerializeField] private float debtInterestRate = 0.01f;

        [Tooltip("ID владельца (для отладки)")]
        [SerializeField] private ulong ownerId;

        // Публичные свойства
        public float CurrentDebt => currentDebt;
        public ulong OwnerId => ownerId;

        /// <summary>
        /// Инициализировать компонент долга
        /// </summary>
        public void Init(ulong playerId, float initialDebt = 0f)
        {
            ownerId = playerId;
            currentDebt = initialDebt;
            lastDebtUpdateTime = Time.time;
        }

        // ==================== ОСНОВНЫЕ МЕТОДЫ ====================

        /// <summary>
        /// Добавить долг (при провале контракта «под расписку»)
        /// GDD_25: debt = cargoValue × 1.5
        /// </summary>
        public void AddDebt(float amount)
        {
            currentDebt += amount;
            lastDebtUpdateTime = Time.time;
        }

        /// <summary>
        /// Погасить часть долга
        /// </summary>
        public void PayDebt(float amount)
        {
            if (currentDebt <= 0f) return;

            currentDebt -= amount;
            if (currentDebt < 0f) currentDebt = 0f;
            lastDebtUpdateTime = Time.time;
        }

        /// <summary>
        /// Обновить долг со временем (затухание 1% в день).
        /// Вызывается сервером раз в день (или при подключении игрока).
        /// GDD_25: «Долг затухает на 1% в день (очень медленно)»
        /// </summary>
        public void UpdateDebtOverTime()
        {
            if (currentDebt <= 0f) return;

            float daysSinceUpdate = (Time.time - lastDebtUpdateTime) / 86400f; // 86400 сек = 1 день
            if (daysSinceUpdate < 1f) return; // Меньше дня — не обновляем

            // Затухание: долг уменьшается на 1% за каждый прошедший день
            float decayMultiplier = Mathf.Pow(1f - debtInterestRate, daysSinceUpdate);
            float oldDebt = currentDebt;
            currentDebt *= decayMultiplier;

            // Округляем до 2 знаков
            currentDebt = Mathf.Round(currentDebt * 100f) / 100f;

            lastDebtUpdateTime = Time.time;

            if (currentDebt < 0.01f)
            {
                currentDebt = 0f;
            }
        }

        // ==================== ПРОВЕРКИ ====================

        /// <summary>
        /// Получить текущий уровень долга
        /// GDD_25 секция 6.2: Долговая система
        /// </summary>
        public DebtLevel GetDebtLevel()
        {
            if (currentDebt <= 0f) return DebtLevel.None;
            if (currentDebt <= 100f) return DebtLevel.Warning;
            if (currentDebt <= 500f) return DebtLevel.Restricted;
            if (currentDebt <= 1000f) return DebtLevel.Hunted;
            if (currentDebt <= 5000f) return DebtLevel.Bounty;
            return DebtLevel.Headhunt;
        }

        /// <summary>
        /// Проверить может ли игрок принимать контракты
        /// GDD_25: при долге 100-500 CR — ограничение контрактов
        /// </summary>
        /// <returns>true если контракты доступны</returns>
        public bool CanAcceptContracts()
        {
            DebtLevel level = GetDebtLevel();
            // None и Warning — можно все контракты
            // Restricted и выше — нельзя принимать новые
            return level == DebtLevel.None || level == DebtLevel.Warning;
        }

        /// <summary>
        /// Получить штраф за долг (для отображения в UI)
        /// </summary>
        public string GetDebtPenaltyString()
        {
            DebtLevel level = GetDebtLevel();
            switch (level)
            {
                case DebtLevel.None:
                    return "";
                case DebtLevel.Warning:
                    return "[Предупреждение НП]";
                case DebtLevel.Restricted:
                    return "[Ограничение контрактов]";
                case DebtLevel.Hunted:
                    return "[Патруль НП преследует]";
                case DebtLevel.Bounty:
                    return "[Ордер на арест]";
                case DebtLevel.Headhunt:
                    return "[Наёмные охотники]";
                default:
                    return "";
            }
        }

        /// <summary>
        /// Получить цвет уровня долга для UI
        /// </summary>
        public Color GetDebtColor()
        {
            DebtLevel level = GetDebtLevel();
            switch (level)
            {
                case DebtLevel.None: return Color.green;
                case DebtLevel.Warning: return Color.yellow;
                case DebtLevel.Restricted: return new Color(1f, 0.5f, 0f); // оранжевый
                case DebtLevel.Hunted: return Color.red;
                case DebtLevel.Bounty: return new Color(0.8f, 0f, 0f); // тёмно-красный
                case DebtLevel.Headhunt: return new Color(0.5f, 0f, 0f); // очень тёмно-красный
                default: return Color.white;
            }
        }

        // ==================== УТИЛИТЫ ====================

        /// <summary>
        /// Проверить и применить затухание (вызывается каждый тик рынка)
        /// </summary>
        public void CheckAndApplyDecay()
        {
            float daysSinceUpdate = (Time.time - lastDebtUpdateTime) / 86400f;
            if (daysSinceUpdate >= 1f)
            {
                UpdateDebtOverTime();
            }
        }

        /// <summary>
        /// Полный сброс долга (для квеста искупления)
        /// </summary>
        public void ClearDebt()
        {
            if (currentDebt > 0f)
            {
                Debug.Log($"[PlayerDebt] Игрок {ownerId}: долг {currentDebt:F0} CR полностью погашен (квест искупления)");
                currentDebt = 0f;
                lastDebtUpdateTime = Time.time;
            }
        }

        private void OnValidate()
        {
            if (currentDebt < 0f) currentDebt = 0f;
        }
    }
}
