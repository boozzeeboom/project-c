using System.Collections.Generic;
using ProjectC.Trade.Core;
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
