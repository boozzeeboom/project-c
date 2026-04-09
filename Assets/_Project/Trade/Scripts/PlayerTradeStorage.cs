using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ProjectC.Trade
{
    [Serializable]
    public class WarehouseItem
    {
        public TradeItemDefinition item;
        public int quantity;
    }

    /// <summary>
    /// Склад игрока — промежуточное хранилище между рынком и кораблём.
    /// Сессия 4: Покупка/продажа на рынке, погрузка/разгрузка корабля.
    /// </summary>
    public class PlayerTradeStorage : MonoBehaviour
    {
        [Header("Location")]
        [Tooltip("ID локации к которой привязан склад")]
        public string currentLocationId = "";

        [Header("Warehouse Limits")]
        public float maxWeight = 10000f;
        public float maxVolume = 200f;
        public int maxItemTypes = 50;

        [Header("Credits")]
        public float credits = 1000f;

        [Header("Warehouse Content")]
        public List<WarehouseItem> warehouse = new List<WarehouseItem>();

        // Сессия 8F: ClientId для доступа к PlayerDataStore
        private ulong _clientId;

        public float CurrentWeight
        {
            get
            {
                float total = 0f;
                foreach (var w in warehouse)
                    if (w.item != null) total += w.item.weight * w.quantity;
                return total;
            }
        }

        public float CurrentVolume
        {
            get
            {
                float total = 0f;
                foreach (var w in warehouse)
                    if (w.item != null) total += w.item.volume * w.quantity;
                return total;
            }
        }

        public bool BuyItem(TradeItemDefinition item, int quantity, float pricePerUnit)
        {
            if (item == null || quantity <= 0) return false;
            float totalCost = pricePerUnit * quantity;
            if (credits < totalCost) { Debug.LogWarning($"[PTS] Нет кредитов! {totalCost:F0} > {credits:F0}"); return false; }

            float newWeight = CurrentWeight + item.weight * quantity;
            float newVolume = CurrentVolume + item.volume * quantity;
            if (newWeight > maxWeight) { Debug.LogWarning($"[PlayerTradeStorage] Превышен лимит веса"); return false; }
            if (newVolume > maxVolume) { Debug.LogWarning($"[PlayerTradeStorage] Превышен лимит объёма"); return false; }

            var existing = warehouse.Find(w => w.item == item);
            if (existing == null && warehouse.Count >= maxItemTypes) { Debug.LogWarning($"[PlayerTradeStorage] Превышен лимит типов"); return false; }

            credits -= totalCost;
            if (existing != null) existing.quantity += quantity;
            else warehouse.Add(new WarehouseItem { item = item, quantity = quantity });

            Save();
            return true;
        }

        public bool SellItem(TradeItemDefinition item, int quantity, float pricePerUnit)
        {
            if (item == null || quantity <= 0) return false;
            var wi = warehouse.Find(w => w.item == item);
            if (wi == null || wi.quantity < quantity) { Debug.LogWarning($"[PlayerTradeStorage] Нет {item.displayName} на складе!"); return false; }

            float totalRevenue = pricePerUnit * quantity;
            credits += totalRevenue;
            wi.quantity -= quantity;
            if (wi.quantity <= 0) warehouse.Remove(wi);

            Save();
            return true;
        }

        public bool LoadToShip(string itemId, int quantity, ProjectC.Player.CargoSystem cargo)
        {
            if (cargo == null) { Debug.LogWarning("[PTS] CargoSystem == null"); return false; }
            var wi = warehouse.Find(w => w.item != null && w.item.itemId == itemId);
            if (wi == null || wi.quantity < quantity)
            {
                Debug.LogWarning($"[PTS] Нет {itemId} на складе! Есть: {warehouse.Count} типов");
                return false;
            }

            var item = wi.item;
            float newWeight = cargo.CurrentWeight + item.weight * quantity;
            float newVolume = cargo.CurrentVolume + item.volume * quantity;
            int newSlots = cargo.UsedSlots + item.slots * quantity;

            if (newWeight > cargo.MaxWeight)
            {
                Debug.LogWarning($"[PTS] Не хватает ВЕСА в трюме! {newWeight:F1} > {cargo.MaxWeight}");
                return false;
            }
            if (newVolume > cargo.MaxVolume)
            {
                Debug.LogWarning($"[PTS] Не хватает ОБЪЁМА в трюме! {newVolume:F1} > {cargo.MaxVolume}");
                return false;
            }
            if (newSlots > cargo.MaxSlots)
            {
                Debug.LogWarning($"[PTS] Не хватает СЛОТОВ в трюме! {newSlots} > {cargo.MaxSlots}");
                return false;
            }

            wi.quantity -= quantity;
            if (wi.quantity <= 0) warehouse.Remove(wi);

            bool added = cargo.AddCargo(item, quantity);
            if (!added)
            {
                Debug.LogError($"[PTS] Не удалось загрузить {item.displayName} x{quantity} в трюм!");
                return false;
            }

            Save();
            return true;
        }

        public bool UnloadFromShip(string itemId, int quantity, ProjectC.Player.CargoSystem cargo)
        {
            if (cargo == null) return false;
            int cargoQty = cargo.GetItemQuantity(itemId);
            if (cargoQty < quantity) return false;

            var item = cargo.cargo.Find(c => c.item != null && c.item.itemId == itemId)?.item;
            if (item == null) return false;

            float newWeight = CurrentWeight + item.weight * quantity;
            float newVolume = CurrentVolume + item.volume * quantity;
            if (newWeight > maxWeight || newVolume > maxVolume)
            {
                Debug.LogWarning($"[PlayerTradeStorage] Не хватает места на складе для {item.displayName}");
                return false;
            }

            cargo.RemoveCargo(itemId, quantity);
            var existing = warehouse.Find(w => w.item == item);
            if (existing != null) existing.quantity += quantity;
            else warehouse.Add(new WarehouseItem { item = item, quantity = quantity });

            Save();
            return true;
        }

        public void Save()
        {
            // Убеждаемся что clientId установлен
            if (_clientId == 0)
            {
                var nm = Unity.Netcode.NetworkManager.Singleton;
                if (nm != null && nm.IsListening) _clientId = nm.LocalClientId;
            }
            if (_clientId == 0) return; // Не сохраняем без clientId

            string locKey = string.IsNullOrEmpty(currentLocationId) ? "global" : currentLocationId.ToLower();

            // Сессия 8F: Сохраняем ТОЛЬКО склад в PlayerDataStore
            // Кредиты хранятся отдельно в PlayerDataStore (общие для всех локаций)
            var saveItems = new List<WarehouseSaveItem>();
            foreach (var w in warehouse)
                if (w.item != null) saveItems.Add(new WarehouseSaveItem { itemId = w.item.itemId, quantity = w.quantity });

            Debug.Log($"[PTS] Save: clientId={_clientId}, loc={locKey}, warehouse.Count={warehouse.Count}, saveItems.Count={saveItems.Count}");
            PlayerDataStore.Instance.SetWarehouse(_clientId, currentLocationId, saveItems);

            Debug.Log($"[PTS] Save: loc={locKey}, items={warehouse.Count}, weight={CurrentWeight:F1}/{maxWeight:F1}, volume={CurrentVolume:F1}/{maxVolume:F1}");
        }

        /// <summary>
        /// Загрузить данные из PlayerDataStore (единый источник). Сессия 8F.
        /// </summary>
        public void LoadFromPlayerDataStore(ulong clientId)
        {
            _clientId = clientId;

            // Кредиты — ОБЩИЕ для всех локаций
            credits = PlayerDataStore.Instance.GetCredits(clientId);

            // Склад — привязан к локации
            string locKey = string.IsNullOrEmpty(currentLocationId) ? "global" : currentLocationId.ToLower();
            var saveItems = PlayerDataStore.Instance.GetWarehouse(clientId, currentLocationId);

            warehouse.Clear();
            var db = FindTradeDatabase();
            if (db != null)
            {
                foreach (var si in saveItems)
                {
                    var def = db.GetItemById(si.itemId);
                    if (def != null) warehouse.Add(new WarehouseItem { item = def, quantity = si.quantity });
                }
            }

            Debug.Log($"[PTS] Loaded from PDS: loc={locKey}, credits={credits:F0}, items={warehouse.Count}");
        }

        /// <summary>
        /// Legacy Load из PlayerPrefs — оставлен для обратной совместимости.
        /// Используйте LoadFromPlayerDataStore.
        /// </summary>
        public void Load()
        {
            string locKey = string.IsNullOrEmpty(currentLocationId) ? "global" : currentLocationId.ToLower();

            // Сессия 8F: Пробуем новые ключи PD_, fallback на старые
            credits = PlayerPrefs.GetFloat($"PD_Credits_{_clientId}", -1f);
            if (credits < 0f)
            {
                // Fallback на старый ключ
                credits = PlayerPrefs.GetFloat($"TradeCredits_{locKey}", 1000f);
            }

            string json = PlayerPrefs.GetString($"PD_Warehouse_{_clientId}_{locKey}", "");
            if (string.IsNullOrEmpty(json))
            {
                // Fallback на старый ключ
                json = PlayerPrefs.GetString($"TradeWarehouse_{locKey}", "");
            }

            if (!string.IsNullOrEmpty(json))
            {
                var data = UnityEngine.JsonUtility.FromJson<WarehouseSaveData>(json);
                warehouse.Clear();
                var db = FindTradeDatabase();
                if (db != null && data != null)
                {
                    foreach (var si in data.items)
                    {
                        var def = db.GetItemById(si.itemId);
                        if (def != null) warehouse.Add(new WarehouseItem { item = def, quantity = si.quantity });
                    }
                }
            }
            else
            {
                warehouse.Clear();
            }

            Debug.Log($"[PTS] Load (legacy): loc={locKey}, credits={credits:F0}, items={warehouse.Count}");
        }

        private static TradeDatabase FindTradeDatabase()
        {
#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:TradeDatabase");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<TradeDatabase>(path);
            }
#endif
            return Resources.Load<TradeDatabase>("Trade/TradeItemDatabase");
        }

        /// <summary>
        /// Добавить товар контракта на склад (бесплатно, для Receipt контрактов).
        /// Сессия 7: ContractSystem.
        /// </summary>
        public bool AddContractItem(TradeItemDefinition item, int quantity)
        {
            if (item == null || quantity <= 0) return false;

            float newWeight = CurrentWeight + item.weight * quantity;
            float newVolume = CurrentVolume + item.volume * quantity;
            if (newWeight > maxWeight) { Debug.LogWarning($"[PTS] Не хватает места на складе для контрактного товара"); return false; }
            if (newVolume > maxVolume) { Debug.LogWarning($"[PTS] Не хватает объёма на складе для контрактного товара"); return false; }

            var existing = warehouse.Find(w => w.item == item);
            if (existing != null)
            {
                existing.quantity += quantity;
            }
            else
            {
                if (warehouse.Count >= maxItemTypes) { Debug.LogWarning($"[PTS] Превышен лимит типов для контрактного товара"); return false; }
                warehouse.Add(new WarehouseItem { item = item, quantity = quantity });
            }

            Save();
            return true;
        }

        /// <summary>
        /// Удалить товар со склада (для завершения контракта)
        /// Сессия 7: ContractSystem.
        /// </summary>
        public bool RemoveItem(string itemId, int quantity)
        {
            var wi = warehouse.Find(w => w.item != null && w.item.itemId == itemId);
            if (wi == null || wi.quantity < quantity) return false;

            wi.quantity -= quantity;
            if (wi.quantity <= 0) warehouse.Remove(wi);
            Save();
            return true;
        }

        private void OnApplicationQuit() { Save(); }
    }
}
