namespace ProjectC.Trade.Dto
{
    /// <summary>
    /// Код результата операции с контрактом (accept / complete / fail / list).
    /// Передаётся клиенту через <see cref="ContractResultDto"/>, на клиенте
    /// маппится в локализованное сообщение (см. <c>ContractClientState.LocalizeResultCode</c>).
    ///
    /// Отдельный enum от <see cref="TradeResultCode"/>, чтобы коды были
    /// специфичны для контрактной подсистемы (MaxActiveReached, WrongDestination,
    /// CargoMissing и т.п.). Если в будущем понадобится объединить — отдельный refactor.
    ///
    /// C2-этап миграции контрактов на v2-архитектуру (см. docs/dev/CONTRACT_V2_MIGRATION.md).
    /// </summary>
    public enum ContractResultCode : byte
    {
        /// <summary>Операция успешна.</summary>
        Ok = 0,

        /// <summary>Игрок не в зоне NPC-агента НП (ContractZone).</summary>
        NotInZone = 1,

        /// <summary>Контракт с указанным contractId не найден.</summary>
        ContractNotFound = 2,

        /// <summary>Контракт уже принят другим игроком или истёк (state != Pending).</summary>
        ContractNotPending = 3,

        /// <summary>Контракт не в активном состоянии (state != Active) — нельзя сдать/провалить.</summary>
        ContractNotActive = 4,

        /// <summary>Контракт принадлежит другому игроку.</summary>
        ContractNotAssigned = 5,

        /// <summary>Превышен лимит активных контрактов на игрока (default 3).</summary>
        MaxActiveReached = 6,

        /// <summary>Долг игрока превышает порог — контракты ограничены (DebtLevel &gt;= Restricted).</summary>
        TooMuchDebt = 7,

        /// <summary>Таймер контракта истёк (state переведён в Failed).</summary>
        TimerExpired = 8,

        /// <summary>Игрок не в целевой локации (для complete).</summary>
        WrongDestination = 9,

        /// <summary>Нет нужного груза ни на складе, ни в трюме (для non-Receipt контракта).</summary>
        CargoMissing = 10,

        /// <summary>Нет места на складе игрока для бесплатного груза (для Receipt контракта).</summary>
        WarehouseFull = 11,

        /// <summary>itemId контракта не найден в TradeItemDatabase.</summary>
        ItemNotFound = 12,

        /// <summary>Rate limit превышен (защита от спама RPC).</summary>
        RateLimited = 13,

        /// <summary>Внутренняя ошибка сервера (catch-all).</summary>
        InternalError = 99,
    }
}
