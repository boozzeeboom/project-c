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
    ///   • Не thread-safe — вызывать ТОЛЬКО с main thread.
    ///
    /// Исправляет баги старой версии:
    ///   • Ключи стабильные (lower-case, без fallback на «global»).
    ///   • Нет ToLower() — id хранятся как есть, но при чтении/записи нормализуем.
    /// </summary>
    public class PlayerPrefsRepository : IPlayerDataRepository
    {
        public const float STARTING_CREDITS = 1000f;

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
            data = null;
            string json = PlayerPrefs.GetString("PD2_Markets", "");
            if (string.IsNullOrEmpty(json)) return RepositoryLoadStatus.NoSaveFound;
            try
            {
                data = JsonUtility.FromJson<MarketSaveData>(json);
                return NormalizeMarkets(data);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PlayerPrefsRepository] LoadMarkets failed: {e.Message}");
                return RepositoryLoadStatus.CorruptSave;
            }
        }

        public void SaveMarkets(MarketSaveData data)
        {
            if (data == null) return;
            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString("PD2_Markets", json);
            PlayerPrefs.Save();
        }

        // --- Contracts ---

        public RepositoryLoadStatus TryLoadContracts(out ContractSaveData data)
        {
            data = null;
            string json = PlayerPrefs.GetString("PD2_Contracts", "");
            if (string.IsNullOrEmpty(json)) return RepositoryLoadStatus.NoSaveFound;
            try
            {
                data = JsonUtility.FromJson<ContractSaveData>(json);
                return NormalizeContracts(data);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PlayerPrefsRepository] LoadContracts failed: {e.Message}");
                return RepositoryLoadStatus.CorruptSave;
            }
        }

        public void SaveContracts(ContractSaveData data)
        {
            if (data == null) return;
            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString("PD2_Contracts", json);
            PlayerPrefs.Save();
        }

        private RepositoryLoadStatus NormalizeMarkets(MarketSaveData data)
        {
            if (data == null) return RepositoryLoadStatus.CorruptSave;
            if (data.schemaVersion > MarketSaveData.CurrentSchemaVersion)
                return RepositoryLoadStatus.UnsupportedSchema;

            bool migrated = data.schemaVersion != MarketSaveData.CurrentSchemaVersion;
            data.schemaVersion = MarketSaveData.CurrentSchemaVersion;
            if (data.markets == null) data.markets = new List<MarketLocationSaveEntry>();
            if (data.events == null) data.events = new List<MarketEventSaveEntry>();
            if (migrated) SaveMarkets(data);

            return data.HasData ? RepositoryLoadStatus.Loaded : RepositoryLoadStatus.ValidEmptySave;
        }

        private RepositoryLoadStatus NormalizeContracts(ContractSaveData data)
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
            if (migrated) SaveContracts(data);

            return data.HasData ? RepositoryLoadStatus.Loaded : RepositoryLoadStatus.ValidEmptySave;
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
