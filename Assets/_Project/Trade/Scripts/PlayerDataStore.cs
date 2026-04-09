using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Trade
{
    /// <summary>
    /// Единый серверный источник данных игрока. Сессия 8F.
    /// 
    /// Архитектура:
    ///   credits    — ОДНО значение на игрока (общее для всех локаций)
    ///   warehouses — отдельный склад НА КАЖДУЮ локацию
    /// 
    /// Ключи PlayerPrefs:
    ///   PD_Credits_{clientId}
    ///   PD_Warehouse_{clientId}_{locationId}
    /// </summary>
    public class PlayerDataStore
    {
        private static PlayerDataStore _instance;
        public static PlayerDataStore Instance
        {
            get
            {
                if (_instance == null) _instance = new PlayerDataStore();
                return _instance;
            }
        }

        // Кэш данных в памяти
        private Dictionary<ulong, PlayerCreditsData> _creditsCache = new Dictionary<ulong, PlayerCreditsData>();
        private Dictionary<string, List<WarehouseSaveItem>> _warehouseCache = new Dictionary<string, List<WarehouseSaveItem>>();

        // ==================== КРЕДИТЫ (ОБЩИЕ) ====================

        public float GetCredits(ulong clientId)
        {
            if (_creditsCache.TryGetValue(clientId, out var data))
                return data.credits;

            float credits = PlayerPrefs.GetFloat($"PD_Credits_{clientId}", 1000f);
            _creditsCache[clientId] = new PlayerCreditsData { credits = credits };
            return credits;
        }

        public void SetCredits(ulong clientId, float amount)
        {
            float clamped = Mathf.Max(0f, amount);
            _creditsCache[clientId] = new PlayerCreditsData { credits = clamped };
            PlayerPrefs.SetFloat($"PD_Credits_{clientId}", clamped);
            PlayerPrefs.Save();
        }

        public void ModifyCredits(ulong clientId, float delta)
        {
            float current = GetCredits(clientId);
            SetCredits(clientId, current + delta);
        }

        // ==================== СКЛАД (ПРИВЯЗАН К ЛОКАЦИИ) ====================

        public List<WarehouseSaveItem> GetWarehouse(ulong clientId, string locationId)
        {
            string key = MakeWarehouseKey(clientId, locationId);
            if (_warehouseCache.TryGetValue(key, out var items))
                return items;

            string json = PlayerPrefs.GetString(key, "");
            var loaded = ParseWarehouseJson(json);
            _warehouseCache[key] = loaded;
            return loaded;
        }

        public void SetWarehouse(ulong clientId, string locationId, List<WarehouseSaveItem> items)
        {
            string key = MakeWarehouseKey(clientId, locationId);
            _warehouseCache[key] = items != null ? new List<WarehouseSaveItem>(items) : new List<WarehouseSaveItem>();

            if (items != null && items.Count > 0)
            {
                var data = new WarehouseSaveData { items = items };
                string json = JsonUtility.ToJson(data);
                PlayerPrefs.SetString(key, json);
            }
            else
            {
                PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.Save();
        }

        private string MakeWarehouseKey(ulong clientId, string locationId)
        {
            string loc = string.IsNullOrEmpty(locationId) ? "global" : locationId.ToLower();
            return $"PD_Warehouse_{clientId}_{loc}";
        }

        private List<WarehouseSaveItem> ParseWarehouseJson(string json)
        {
            var result = new List<WarehouseSaveItem>();
            if (string.IsNullOrEmpty(json)) return result;

            try
            {
                var data = JsonUtility.FromJson<WarehouseSaveData>(json);
                if (data != null && data.items != null)
                    result.AddRange(data.items);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerDataStore] Ошибка парсинга склада: {e.Message}");
            }
            return result;
        }

        // ==================== ОЧИСТКА КЭША ====================

        public void ClearCache(ulong clientId)
        {
            _creditsCache.Remove(clientId);
            // Удаляем все склады этого игрока из кэша
            var keysToRemove = new List<string>();
            foreach (var kvp in _warehouseCache)
            {
                if (kvp.Key.StartsWith($"PD_Warehouse_{clientId}_"))
                    keysToRemove.Add(kvp.Key);
            }
            foreach (var key in keysToRemove)
                _warehouseCache.Remove(key);
        }
    }

    [Serializable]
    public class PlayerCreditsData
    {
        public float credits = 1000f;
    }

    [Serializable]
    public class WarehouseSaveData
    {
        public List<WarehouseSaveItem> items;
    }

    [Serializable]
    public class WarehouseSaveItem
    {
        public string itemId;
        public int quantity;
    }
}
