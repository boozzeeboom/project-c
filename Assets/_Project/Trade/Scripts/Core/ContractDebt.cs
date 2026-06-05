using UnityEngine;

namespace ProjectC.Trade.Core
{
    /// <summary>
    /// Серверный POCO долга игрока перед НП (контрактная система, GDD_25 секция 6.2).
    ///
    /// В v1 был <c>PlayerDebt : MonoBehaviour</c> и создавался на лету на
    /// NetworkPlayer (см. legacy <c>ContractSystem.cs:759-784</c>). В v2 —
    /// чистая структура в памяти <see cref="ContractWorld"/>, не вешается
    /// на GameObject, не сериализуется в сцену. Долг живёт в
    /// <c>ContractWorld._playerDebts[clientId]</c>.
    ///
    /// Decay (затухание долга со временем) применяется в <see cref="ContractWorld.Tick"/>.
    /// <see cref="CanAcceptContracts"/> блокирует приём новых контрактов
    /// при <see cref="DebtLevel"/> &gt;= Restricted (≥ 100 CR).
    ///
    /// C2-этап миграции контрактов на v2-архитектуру (см. docs/dev/CONTRACT_V2_MIGRATION.md).
    /// </summary>
    public class ContractDebt
    {
        /// <summary>Owner clientId (для логов).</summary>
        public readonly ulong PlayerId;

        /// <summary>Текущий долг в CR.</summary>
        public float CurrentDebt;

        /// <summary>Время последнего decay-тика (Time.realtimeSinceStartup, в секундах).</summary>
        public float LastDecayTime;

        /// <summary>Текущий уровень долга (enum).</summary>
        public DebtLevel Level => ComputeLevel(CurrentDebt);

        public ContractDebt(ulong playerId, float initialDebt = 0f, float? now = null)
        {
            PlayerId = playerId;
            CurrentDebt = initialDebt;
            LastDecayTime = now ?? Time.realtimeSinceStartup;
        }

        /// <summary>
        /// Может ли игрок брать новые контракты.
        /// Блокируем при DebtLevel &gt;= Restricted (≥ 100 CR по GDD_25 §6.2).
        /// </summary>
        public bool CanAcceptContracts()
        {
            return Level < DebtLevel.Restricted;
        }

        /// <summary>Добавить долг (например, Receipt контракт провален: cargoValue × 1.5).</summary>
        public void AddDebt(float amount)
        {
            if (amount <= 0f) return;
            CurrentDebt += amount;
        }

        /// <summary>
        /// Применить decay долга (затухание) — вызывается из <see cref="ContractWorld.Tick"/>.
        /// По GDD_25 §6.2: долг затухает на 1 CR в час (3600с) при условии, что
        /// игрок не в активном «проваленном» контракте. Упрощённо — линейный decay.
        /// </summary>
        /// <param name="now">Текущее время (Time.realtimeSinceStartup).</param>
        public void CheckAndApplyDecay(float now)
        {
            const float DecayPerHour = 1f;
            const float DecayPerSecond = DecayPerHour / 3600f;
            const float MinDebt = 0f;

            float dt = now - LastDecayTime;
            if (dt <= 0f) return;

            float decay = dt * DecayPerSecond;
            CurrentDebt -= decay;
            if (CurrentDebt < MinDebt) CurrentDebt = MinDebt;
            LastDecayTime = now;
        }

        /// <summary>Маппинг CR → DebtLevel по GDD_25 §6.2.</summary>
        public static DebtLevel ComputeLevel(float debt)
        {
            if (debt <= 0f)        return DebtLevel.None;
            if (debt < 100f)       return DebtLevel.Warning;     // 1-100 CR
            if (debt < 500f)       return DebtLevel.Restricted;  // 100-500 CR
            if (debt < 1000f)      return DebtLevel.Hunted;      // 500-1000 CR
            if (debt < 5000f)      return DebtLevel.Bounty;      // 1000-5000 CR
            return DebtLevel.Headhunt; // 5000+ CR
        }

        /// <summary>Локализованная строка штрафа для UI debt-label (см. legacy PlayerDebt.GetDebtPenaltyString).</summary>
        public string GetDebtPenaltyString()
        {
            switch (Level)
            {
                case DebtLevel.None:       return "Нет долга";
                case DebtLevel.Warning:    return "Предупреждение НП";
                case DebtLevel.Restricted: return "Ограничение контрактов";
                case DebtLevel.Hunted:     return "Патруль НП преследует";
                case DebtLevel.Bounty:     return "Ордер на арест";
                case DebtLevel.Headhunt:   return "Наёмные охотники";
                default: return Level.ToString();
            }
        }

        /// <summary>Цвет долга для UI (как legacy PlayerDebt.GetDebtColor).</summary>
        public Color GetDebtColor()
        {
            switch (Level)
            {
                case DebtLevel.None:       return Color.white;
                case DebtLevel.Warning:    return new Color(1f, 0.9f, 0.4f); // жёлтый
                case DebtLevel.Restricted: return new Color(1f, 0.5f, 0f);   // оранжевый
                case DebtLevel.Hunted:     return new Color(1f, 0.3f, 0f);   // тёмно-оранжевый
                case DebtLevel.Bounty:     return new Color(1f, 0.2f, 0.2f); // красный
                case DebtLevel.Headhunt:   return new Color(0.8f, 0f, 0.8f); // фиолетовый
                default: return Color.white;
            }
        }
    }
}
