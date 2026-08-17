using System.Collections.Generic;
using ProjectC.Trade.Core;
using ProjectC.Trade.Dto;
using UnityEngine;

namespace ProjectC.Trade.Repository
{
    /// <summary>
    /// Реализация <see cref="IPlayerDataRepository"/> через PlayerPrefs.
    ///
    /// ОГРАНИЧЕНИЯ (фиксируем явно, чтобы не было «почему в dedicated server не работает»):
    ///   • PlayerPrefs локальны для каждого процесса → в dedicated server
    ///     данные не переживают рестарт (это лечит <see cref="ServerFileRepository"/>).
    ///   • Работает нормально в host-режиме (single-process).
    ///   • Для markets/contracts используется best-effort temp + backup key protocol;
    ///     PlayerPrefs всё равно не даёт filesystem-level atomic rename.
    ///   • Не thread-safe — вызывать ТОЛЬКО с main thread.
    ///
    /// Исправляет баги старой версии:
    ///   • Ключи стабильные (lower-case, без fallback на «global»).
    ///   • Нет ToLower() — id хранятся как есть, но при чтении/записи нормализуем.
    /// </summary>
    public class PlayerPrefsRepository : IPlayerDataRepository
    {
        public const float STARTING_CREDITS = 1000f;

        private const string MarketsKey = "PD2_Markets";
        private const string MarketsBackupKey = "PD2_Markets_bak";
        private const string MarketsTempKey = "PD2_Markets_tmp";
        private const string ContractsKey = "PD2_Contracts";
        private const string ContractsBackupKey = "PD2_Contracts_bak";
        private const string ContractsTempKey = "PD2_Contracts_tmp";

        public float GetCredits(ulong clientId)
        {
            return PlayerPrefs.GetFloat(CreditsKey(clientId), STARTING_CREDITS);
        }

        public void SetCredits(ulong clientId, float credits)
        {
            float clamped = Mathf.Max(0f, credits);
            PlayerPrefs.SetFloat(CreditsKey(clientId), clamped);
            PlayerPrefs.Save();
        }

        public bool TryModifyCredits(ulong clientId, float delta, out float newCredits, out string failReason)
        {
            failReason = null;
            float current = GetCredits(clientId);
            float target = current + delta;
            if (target < 0f) { newCredits = current; failReason = "insufficient_credits"; return false; }
            newCredits = target;
            SetCredits(clientId, newCredits);
            return true;
        }

        public bool TryGetWarehouse(ulong clientId, string locationId, out List<WarehouseEntry> items)
        {
            items = null;
            if (string.IsNullOrEmpty(locationId)) { return false; }
            string json = PlayerPrefs.GetString(WarehouseKey(clientId, locationId), "");
            if (string.IsNullOrEmpty(json)) { items = new List<WarehouseEntry>(); return true; }
            try
            {
                var data = JsonUtility.FromJson<WarehouseSaveData>(json);
                items = data?.items ?? new List<WarehouseEntry>();
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PlayerPrefsRepository] Ошибка парсинга склада: {e.Message}");
                items = new List<WarehouseEntry>();
                return true;
            }
        }

        public void SetWarehouse(ulong clientId, string locationId, List<WarehouseEntry> items)
        {
            if (string.IsNullOrEmpty(locationId)) return;
            if (items == null || items.Count == 0)
            {
                PlayerPrefs.DeleteKey(WarehouseKey(clientId, locationId));
            }
            else
            {
                var data = new WarehouseSaveData { items = items };
                PlayerPrefs.SetString(WarehouseKey(clientId, locationId), JsonUtility.ToJson(data));
            }
            PlayerPrefs.Save();
        }

        public bool TryGetCargo(ulong shipNetworkObjectId, out List<WarehouseEntry> items)
        {
            items = null;
            string json = PlayerPrefs.GetString(CargoKey(shipNetworkObjectId), "");
            if (string.IsNullOrEmpty(json)) { items = new List<WarehouseEntry>(); return true; }
            try
            {
                var data = JsonUtility.FromJson<WarehouseSaveData>(json);
                items = data?.items ?? new List<WarehouseEntry>();
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PlayerPrefsRepository] Ошибка парсинга cargo: {e.Message}");
                items = new List<WarehouseEntry>();
                return true;
            }
        }

        public void SetCargo(ulong shipNetworkObjectId, List<WarehouseEntry> items)
        {
            if (items == null || items.Count == 0)
            {
                PlayerPrefs.DeleteKey(CargoKey(shipNetworkObjectId));
            }
            else
            {
                var data = new WarehouseSaveData { items = items };
                PlayerPrefs.SetString(CargoKey(shipNetworkObjectId), JsonUtility.ToJson(data));
            }
            PlayerPrefs.Save();
        }

        // --- Markets ---

        public RepositoryLoadStatus TryLoadMarkets(out MarketSaveData data)
        {
            var primaryStatus = TryLoadMarketsFromKey(MarketsKey, out data, persistMigration: true);
            if (primaryStatus != RepositoryLoadStatus.NoSaveFound
                && primaryStatus != RepositoryLoadStatus.CorruptSave)
            {
                return primaryStatus;
            }

            var tempStatus = TryLoadMarketsFromKey(MarketsTempKey, out data, persistMigration: false);
            if (tempStatus == RepositoryLoadStatus.Loaded || tempStatus == RepositoryLoadStatus.ValidEmptySave)
                return RestoreMarketsSnapshot(data);
            if (tempStatus == RepositoryLoadStatus.UnsupportedSchema)
                return tempStatus;

            var backupStatus = TryLoadMarketsFromKey(MarketsBackupKey, out data, persistMigration: false);
            if (backupStatus == RepositoryLoadStatus.Loaded || backupStatus == RepositoryLoadStatus.ValidEmptySave)
                return RestoreMarketsSnapshot(data);
            if (backupStatus == RepositoryLoadStatus.UnsupportedSchema)
                return backupStatus;

            bool anySnapshotKey = PlayerPrefs.HasKey(MarketsKey)
                || PlayerPrefs.HasKey(MarketsBackupKey)
                || PlayerPrefs.HasKey(MarketsTempKey);
            data = null;
            return anySnapshotKey ? RepositoryLoadStatus.CorruptSave : RepositoryLoadStatus.NoSaveFound;
        }

        public void SaveMarkets(MarketSaveData data)
        {
            if (data == null) return;
            WriteSnapshotAtomically(MarketsKey, JsonUtility.ToJson(data));
        }

        // --- Contracts ---

        public RepositoryLoadStatus TryLoadContracts(out ContractSaveData data)
        {
            var primaryStatus = TryLoadContractsFromKey(ContractsKey, out data, persistMigration: true);
            if (primaryStatus != RepositoryLoadStatus.NoSaveFound
                && primaryStatus != RepositoryLoadStatus.CorruptSave)
            {
                return primaryStatus;
            }

            var tempStatus = TryLoadContractsFromKey(ContractsTempKey, out data, persistMigration: false);
            if (tempStatus == RepositoryLoadStatus.Loaded || tempStatus == RepositoryLoadStatus.ValidEmptySave)
                return RestoreContractsSnapshot(data);
            if (tempStatus == RepositoryLoadStatus.UnsupportedSchema)
                return tempStatus;

            var backupStatus = TryLoadContractsFromKey(ContractsBackupKey, out data, persistMigration: false);
            if (backupStatus == RepositoryLoadStatus.Loaded || backupStatus == RepositoryLoadStatus.ValidEmptySave)
                return RestoreContractsSnapshot(data);
            if (backupStatus == RepositoryLoadStatus.UnsupportedSchema)
                return backupStatus;

            bool anySnapshotKey = PlayerPrefs.HasKey(ContractsKey)
                || PlayerPrefs.HasKey(ContractsBackupKey)
                || PlayerPrefs.HasKey(ContractsTempKey);
            data = null;
            return anySnapshotKey ? RepositoryLoadStatus.CorruptSave : RepositoryLoadStatus.NoSaveFound;
        }

        public bool SaveContracts(ContractSaveData data)
        {
            if (data == null) return false;
            return WriteSnapshotAtomically(ContractsKey, JsonUtility.ToJson(data));
        }

        private RepositoryLoadStatus TryLoadMarketsFromKey(string key, out MarketSaveData data, bool persistMigration)
        {
            data = null;
            if (!PlayerPrefs.HasKey(key)) return RepositoryLoadStatus.NoSaveFound;

            string json = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(json)) return RepositoryLoadStatus.CorruptSave;
            try
            {
                data = JsonUtility.FromJson<MarketSaveData>(json);
                return NormalizeMarkets(data, persistMigration);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PlayerPrefsRepository] LoadMarkets key '{key}' failed: {e.Message}");
                data = null;
                return RepositoryLoadStatus.CorruptSave;
            }
        }

        private RepositoryLoadStatus TryLoadContractsFromKey(string key, out ContractSaveData data, bool persistMigration)
        {
            data = null;
            if (!PlayerPrefs.HasKey(key)) return RepositoryLoadStatus.NoSaveFound;

            string json = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(json)) return RepositoryLoadStatus.CorruptSave;
            try
            {
                data = JsonUtility.FromJson<ContractSaveData>(json);
                return NormalizeContracts(data, persistMigration);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PlayerPrefsRepository] LoadContracts key '{key}' failed: {e.Message}");
                data = null;
                return RepositoryLoadStatus.CorruptSave;
            }
        }

        private RepositoryLoadStatus NormalizeMarkets(MarketSaveData data, bool persistMigration)
        {
            if (data == null) return RepositoryLoadStatus.CorruptSave;
            if (data.schemaVersion > MarketSaveData.CurrentSchemaVersion)
                return RepositoryLoadStatus.UnsupportedSchema;

            bool migrated = data.schemaVersion != MarketSaveData.CurrentSchemaVersion;
            data.schemaVersion = MarketSaveData.CurrentSchemaVersion;
            if (data.markets == null) data.markets = new List<MarketLocationSaveEntry>();
            if (data.events == null) data.events = new List<MarketEventSaveEntry>();
            if (migrated && persistMigration) SaveMarkets(data);

            return data.HasData ? RepositoryLoadStatus.Loaded : RepositoryLoadStatus.ValidEmptySave;
        }

        private RepositoryLoadStatus NormalizeContracts(ContractSaveData data, bool persistMigration)
        {
            if (data == null) return RepositoryLoadStatus.CorruptSave;
            if (data.schemaVersion > ContractSaveData.CurrentSchemaVersion)
                return RepositoryLoadStatus.UnsupportedSchema;

            bool migrated = data.schemaVersion != ContractSaveData.CurrentSchemaVersion;
            data.schemaVersion = ContractSaveData.CurrentSchemaVersion;
            if (data.contracts == null) data.contracts = new List<ContractData>();
            if (data.debts == null) data.debts = new List<ContractDebtEntry>();
            if (data.playerContracts == null) data.playerContracts = new List<PlayerContractEntry>();
            if (data.locationContracts == null) data.locationContracts = new List<LocationContractEntry>();
            if (migrated && persistMigration) SaveContracts(data);

            return data.HasData ? RepositoryLoadStatus.Loaded : RepositoryLoadStatus.ValidEmptySave;
        }

        private static RepositoryLoadStatus RestoreMarketsSnapshot(MarketSaveData data)
        {
            WriteSnapshotAtomically(MarketsKey, JsonUtility.ToJson(data));
            return data.HasData ? RepositoryLoadStatus.Loaded : RepositoryLoadStatus.ValidEmptySave;
        }

        private static RepositoryLoadStatus RestoreContractsSnapshot(ContractSaveData data)
        {
            WriteSnapshotAtomically(ContractsKey, JsonUtility.ToJson(data));
            return data.HasData ? RepositoryLoadStatus.Loaded : RepositoryLoadStatus.ValidEmptySave;
        }

        private static bool WriteSnapshotAtomically(string key, string json)
        {
            string backupKey = key == MarketsKey ? MarketsBackupKey : ContractsBackupKey;
            string tempKey = key == MarketsKey ? MarketsTempKey : ContractsTempKey;

            try
            {
                // First durable point: a valid temp snapshot exists before the primary changes.
                PlayerPrefs.SetString(tempKey, json);
                PlayerPrefs.Save();

                if (PlayerPrefs.HasKey(key))
                    PlayerPrefs.SetString(backupKey, PlayerPrefs.GetString(key, ""));

                PlayerPrefs.SetString(key, json);
                PlayerPrefs.DeleteKey(tempKey);
                PlayerPrefs.Save();
                return true;
            }
            catch (System.Exception e)
            {
                // Не удаляем temp key после ошибки: следующий load может использовать
                // его как последний валидный snapshot.
                Debug.LogError($"[PlayerPrefsRepository] atomic snapshot write failed for '{key}': {e.Message}");
                return false;
            }
        }

        // --- Ключи (нижний регистр для id локации, чтобы 'PRIMIUM' и 'primium' не расходились) ---
        private static string CreditsKey(ulong clientId) => $"PD2_Credits_{clientId}";
        private static string WarehouseKey(ulong clientId, string locationId) => $"PD2_Warehouse_{clientId}_{(locationId ?? "").ToLowerInvariant()}";
        private static string CargoKey(ulong shipNetworkObjectId) => $"PD2_Cargo_{shipNetworkObjectId}";

        [System.Serializable]
        private class WarehouseSaveData
        {
            public List<WarehouseEntry> items;
        }
    }
}
