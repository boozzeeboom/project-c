using System.Collections.Generic;
using System.IO;
using ProjectC.Trade.Core;
using ProjectC.Trade.Dto;
using UnityEngine;

namespace ProjectC.Trade.Repository
{
    /// <summary>
    /// Серверный репозиторий на JSON-файлах.
    /// Поведение при вызове: пишет/читает в <see cref="Application.persistentDataPath"/>.
    /// Market/contract snapshots используют schema migration, future-version guard,
    /// atomic writes и recovery из `.bak` backup.
    ///
    /// Создан чтобы:
    ///   1. Доказать, что интерфейс IPlayerDataRepository подходит для обоих
    ///      сценариев (host + dedicated).
    ///   2. Дать dedicated server точку расширения без переписывания TradeWorld.
    /// </summary>
    public class ServerFileRepository : IPlayerDataRepository
    {
        public const float STARTING_CREDITS = 1000f;
        private readonly string _rootDir;
        private readonly Dictionary<string, float> _creditsCache = new Dictionary<string, float>();

        public ServerFileRepository(string rootDir = null)
        {
            _rootDir = string.IsNullOrEmpty(rootDir)
                ? Path.Combine(Application.persistentDataPath, "ServerData")
                : rootDir;
            try { Directory.CreateDirectory(_rootDir); }
            catch (System.Exception e) { Debug.LogError($"[ServerFileRepository] mkdir failed: {e.Message}"); }
        }

        public float GetCredits(ulong clientId)
        {
            string key = clientId.ToString();
            if (_creditsCache.TryGetValue(key, out var v)) return v;

            string path = Path.Combine(_rootDir, $"credits_{clientId}.txt");
            if (!File.Exists(path)) { _creditsCache[key] = STARTING_CREDITS; return STARTING_CREDITS; }
            try
            {
                float val = float.Parse(File.ReadAllText(path).Trim());
                _creditsCache[key] = val;
                return val;
            }
            catch { _creditsCache[key] = STARTING_CREDITS; return STARTING_CREDITS; }
        }

        public void SetCredits(ulong clientId, float credits)
        {
            float clamped = Mathf.Max(0f, credits);
            _creditsCache[clientId.ToString()] = clamped;
            try
            {
                string path = Path.Combine(_rootDir, $"credits_{clientId}.txt");
                File.WriteAllText(path, clamped.ToString());
            }
            catch (System.Exception e) { Debug.LogError($"[ServerFileRepository] write credits failed: {e.Message}"); }
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
            items = new List<WarehouseEntry>();
            if (string.IsNullOrEmpty(locationId)) return false;
            string path = Path.Combine(_rootDir, $"warehouse_{clientId}_{(locationId ?? "").ToLowerInvariant()}.json");
            if (!File.Exists(path)) return true;
            try
            {
                string json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data?.items != null) items.AddRange(data.items);
            }
            catch (System.Exception e) { Debug.LogWarning($"[ServerFileRepository] read warehouse failed: {e.Message}"); }
            return true;
        }

        public void SetWarehouse(ulong clientId, string locationId, List<WarehouseEntry> items)
        {
            if (string.IsNullOrEmpty(locationId)) return;
            string path = Path.Combine(_rootDir, $"warehouse_{clientId}_{(locationId ?? "").ToLowerInvariant()}.json");
            try
            {
                if (items == null || items.Count == 0) { if (File.Exists(path)) File.Delete(path); return; }
                var data = new SaveData { items = items };
                File.WriteAllText(path, JsonUtility.ToJson(data));
            }
            catch (System.Exception e) { Debug.LogError($"[ServerFileRepository] write warehouse failed: {e.Message}"); }
        }

        public bool TryGetCargo(ulong shipNetworkObjectId, out List<WarehouseEntry> items)
        {
            items = new List<WarehouseEntry>();
            string path = Path.Combine(_rootDir, $"cargo_{shipNetworkObjectId}.json");
            if (!File.Exists(path)) return true;
            try
            {
                string json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data?.items != null) items.AddRange(data.items);
            }
            catch (System.Exception e) { Debug.LogWarning($"[ServerFileRepository] read cargo failed: {e.Message}"); }
            return true;
        }

        public void SetCargo(ulong shipNetworkObjectId, List<WarehouseEntry> items)
        {
            string path = Path.Combine(_rootDir, $"cargo_{shipNetworkObjectId}.json");
            try
            {
                if (items == null || items.Count == 0) { if (File.Exists(path)) File.Delete(path); return; }
                var data = new SaveData { items = items };
                File.WriteAllText(path, JsonUtility.ToJson(data));
            }
            catch (System.Exception e) { Debug.LogError($"[ServerFileRepository] write cargo failed: {e.Message}"); }
        }

        // --- Markets ---

        public RepositoryLoadStatus TryLoadMarkets(out MarketSaveData data)
        {
            data = null;
            string path = Path.Combine(_rootDir, "markets.json");
            var loadStatus = TryLoadJsonWithBackup(path, "markets", out data);
            if (loadStatus != RepositoryLoadStatus.Loaded) return loadStatus;
            return TryMigrateMarkets(data, path);
        }

        public void SaveMarkets(MarketSaveData data)
        {
            if (data == null) return;
            string path = Path.Combine(_rootDir, "markets.json");
            try
            {
                string json = JsonUtility.ToJson(data);
                WriteJsonAtomically(path, json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ServerFileRepository] SaveMarkets failed: {e.Message}");
            }
        }

        // --- Contracts ---

        public RepositoryLoadStatus TryLoadContracts(out ContractSaveData data)
        {
            data = null;
            string path = Path.Combine(_rootDir, "contracts.json");
            var loadStatus = TryLoadJsonWithBackup(path, "contracts", out data);
            if (loadStatus != RepositoryLoadStatus.Loaded) return loadStatus;
            return TryMigrateContracts(data, path);
        }

        public bool SaveContracts(ContractSaveData data)
        {
            if (data == null) return false;
            string path = Path.Combine(_rootDir, "contracts.json");
            try
            {
                string json = JsonUtility.ToJson(data);
                WriteJsonAtomically(path, json);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ServerFileRepository] SaveContracts failed: {e.Message}");
                return false;
            }
        }

        private static RepositoryLoadStatus TryLoadJsonWithBackup<T>(string path, string label, out T data)
            where T : class
        {
            data = null;
            bool primaryExists = File.Exists(path);
            if (primaryExists && TryReadJson(path, label, out data))
                return RepositoryLoadStatus.Loaded;

            string backupPath = path + ".bak";
            if (!File.Exists(backupPath) || !TryReadJson(backupPath, label + ".bak", out data))
            {
                if (primaryExists || File.Exists(backupPath))
                    Debug.LogWarning($"[ServerFileRepository] {label}: primary and backup snapshots are unavailable");
                return primaryExists || File.Exists(backupPath)
                    ? RepositoryLoadStatus.CorruptSave
                    : RepositoryLoadStatus.NoSaveFound;
            }

            try
            {
                File.Copy(backupPath, path, overwrite: true);
                Debug.LogWarning($"[ServerFileRepository] {label}: recovered primary snapshot from {backupPath}");
            }
            catch (System.Exception e)
            {
                // Snapshot уже прочитан и может быть использован в памяти даже если
                // восстановить основной файл не удалось.
                Debug.LogError($"[ServerFileRepository] {label}: backup recovery write failed: {e.Message}");
            }

            return RepositoryLoadStatus.Loaded;
        }

        private static bool TryReadJson<T>(string path, string label, out T data)
            where T : class
        {
            data = null;
            try
            {
                string json = File.ReadAllText(path);
                data = JsonUtility.FromJson<T>(json);
                if (data == null)
                {
                    Debug.LogWarning($"[ServerFileRepository] {label}: deserialized snapshot is null");
                    return false;
                }
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ServerFileRepository] {label}: read/deserialize failed: {e.Message}");
                return false;
            }
        }

        private static RepositoryLoadStatus TryMigrateMarkets(MarketSaveData data, string path)
        {
            if (data == null) return RepositoryLoadStatus.CorruptSave;
            if (data.schemaVersion > MarketSaveData.CurrentSchemaVersion)
            {
                Debug.LogError($"[ServerFileRepository] markets schema {data.schemaVersion} is newer than supported {MarketSaveData.CurrentSchemaVersion}; refusing {path}");
                return RepositoryLoadStatus.UnsupportedSchema;
            }

            bool migrated = data.schemaVersion != MarketSaveData.CurrentSchemaVersion;
            data.schemaVersion = MarketSaveData.CurrentSchemaVersion;
            if (data.markets == null) data.markets = new List<MarketLocationSaveEntry>();
            if (data.events == null) data.events = new List<MarketEventSaveEntry>();

            if (migrated)
            {
                Debug.Log($"[ServerFileRepository] migrated markets snapshot to schema {MarketSaveData.CurrentSchemaVersion}");
                PersistMigratedSnapshot(path, JsonUtility.ToJson(data), "markets");
            }
            return data.HasData ? RepositoryLoadStatus.Loaded : RepositoryLoadStatus.ValidEmptySave;
        }

        private static RepositoryLoadStatus TryMigrateContracts(ContractSaveData data, string path)
        {
            if (data == null) return RepositoryLoadStatus.CorruptSave;
            if (data.schemaVersion > ContractSaveData.CurrentSchemaVersion)
            {
                Debug.LogError($"[ServerFileRepository] contracts schema {data.schemaVersion} is newer than supported {ContractSaveData.CurrentSchemaVersion}; refusing {path}");
                return RepositoryLoadStatus.UnsupportedSchema;
            }

            bool migrated = data.schemaVersion != ContractSaveData.CurrentSchemaVersion;
            data.schemaVersion = ContractSaveData.CurrentSchemaVersion;
            if (data.contracts == null) data.contracts = new List<ContractData>();
            if (data.debts == null) data.debts = new List<ContractDebtEntry>();
            if (data.playerContracts == null) data.playerContracts = new List<PlayerContractEntry>();
            if (data.locationContracts == null) data.locationContracts = new List<LocationContractEntry>();

            if (migrated)
            {
                Debug.Log($"[ServerFileRepository] migrated contracts snapshot to schema {ContractSaveData.CurrentSchemaVersion}");
                PersistMigratedSnapshot(path, JsonUtility.ToJson(data), "contracts");
            }
            return data.HasData ? RepositoryLoadStatus.Loaded : RepositoryLoadStatus.ValidEmptySave;
        }

        private static void PersistMigratedSnapshot(string path, string json, string label)
        {
            try
            {
                WriteJsonAtomically(path, json);
            }
            catch (System.Exception e)
            {
                // Migration в памяти всё равно остаётся валидной; следующий SaveAll
                // повторит запись. Не превращаем успешный load в hard failure.
                Debug.LogWarning($"[ServerFileRepository] {label}: migrated snapshot write failed: {e.Message}");
            }
        }

        private static void WriteJsonAtomically(string path, string json)
        {
            string tempPath = path + ".tmp";
            string backupPath = path + ".bak";

            try
            {
                File.WriteAllText(tempPath, json);
                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            catch (System.PlatformNotSupportedException)
            {
                // Fallback for filesystems without File.Replace support.
                File.Copy(tempPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        [System.Serializable]
        private class SaveData
        {
            public List<WarehouseEntry> items;
        }
    }
}
