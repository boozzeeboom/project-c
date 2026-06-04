using System.Collections.Generic;
using System.IO;
using ProjectC.Trade.Core;
using UnityEngine;

namespace ProjectC.Trade.Repository
{
    /// <summary>
    /// P1-заглушка: серверный репозиторий на JSON-файлах.
    /// Поведение при вызове: пишет/читает в <see cref="Application.persistentDataPath"/>.
    /// На этом этапе НЕ доводится до production-готовности (нужны миграции,
    /// concurrency lock, batched I/O). TODO на P1.
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

        [System.Serializable]
        private class SaveData
        {
            public List<WarehouseEntry> items;
        }
    }
}
