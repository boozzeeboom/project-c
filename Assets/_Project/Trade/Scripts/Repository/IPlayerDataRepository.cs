using System;
using System.Collections.Generic;
using ProjectC.Trade.Core;
using ProjectC.Trade.Dto;

namespace ProjectC.Trade.Repository
{
    /// <summary>
    /// Результат попытки загрузки snapshot persistence.
    /// Loaded и ValidEmptySave означают валидный snapshot; остальные значения
    /// требуют отдельной политики и не должны трактоваться как пустой save.
    /// </summary>
    public enum RepositoryLoadStatus
    {
        NoSaveFound,
        Loaded,
        ValidEmptySave,
        CorruptSave,
        UnsupportedSchema
    }

    /// <summary>
    /// Интерфейс хранилища постоянных данных игрока и его кораблей.
    /// Реализации:
    ///   • <see cref="PlayerPrefsRepository"/> — по умолчанию (host-only, single-process).
    ///   • <see cref="ServerFileRepository"/> — P1, JSON-файлы (для dedicated server).
    ///
    /// Ключи:
    ///   credits:{clientId}                                 — общие кредиты
    ///   warehouse:{clientId}:{locationId}                  — склад
    ///   cargo:{shipNetworkObjectId}                        — груз корабля
    /// </summary>
    public interface IPlayerDataRepository
    {
        /// <summary>
        /// Acquires the process-wide player-economy transaction lock.
        /// The scope serializes compound domain mutations; it does not make
        /// multiple files/keys crash-atomically committed.
        /// </summary>
        IDisposable AcquireTransactionLock();

        // --- Credits ---
        float GetCredits(ulong clientId);
        bool SetCredits(ulong clientId, float credits);
        bool TryModifyCredits(ulong clientId, float delta, out float newCredits, out string failReason);

        // --- Warehouse (привязан к локации) ---
        bool TryGetWarehouse(ulong clientId, string locationId, out List<WarehouseEntry> items);
        bool SetWarehouse(ulong clientId, string locationId, List<WarehouseEntry> items);

        // --- Cargo (привязан к NetworkObjectId корабля) ---
        bool TryGetCargo(ulong shipNetworkObjectId, out List<WarehouseEntry> items);
        bool SetCargo(ulong shipNetworkObjectId, List<WarehouseEntry> items);

        // --- Contracts (T-Q?? persistence) ---
        RepositoryLoadStatus TryLoadContracts(out ContractSaveData data);
        bool SaveContracts(ContractSaveData data);

        // --- Markets (runtime state persistence) ---
        RepositoryLoadStatus TryLoadMarkets(out MarketSaveData data);
        bool SaveMarkets(MarketSaveData data);
    }
}
