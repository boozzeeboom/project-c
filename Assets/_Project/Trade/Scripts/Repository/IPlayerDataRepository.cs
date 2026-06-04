using System.Collections.Generic;
using ProjectC.Trade.Core;

namespace ProjectC.Trade.Repository
{
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
        // --- Credits ---
        float GetCredits(ulong clientId);
        void SetCredits(ulong clientId, float credits);
        bool TryModifyCredits(ulong clientId, float delta, out float newCredits, out string failReason);

        // --- Warehouse (привязан к локации) ---
        bool TryGetWarehouse(ulong clientId, string locationId, out List<WarehouseEntry> items);
        void SetWarehouse(ulong clientId, string locationId, List<WarehouseEntry> items);

        // --- Cargo (привязан к NetworkObjectId корабля) ---
        bool TryGetCargo(ulong shipNetworkObjectId, out List<WarehouseEntry> items);
        void SetCargo(ulong shipNetworkObjectId, List<WarehouseEntry> items);
    }
}
