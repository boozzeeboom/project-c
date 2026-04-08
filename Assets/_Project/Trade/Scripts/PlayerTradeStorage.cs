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

            Debug.Log($"[PlayerTradeStorage] Куплено: {item.displayName} x{quantity} за {totalCost:F0} CR");
            Save(); // Сессия 8: Сохраняем сразу после покупки
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

            Debug.Log($"[PlayerTradeStorage] Продано: {item.displayName} x{quantity} за {totalRevenue:F0} CR");
            Save(); // Сессия 8: Сохраняем сразу после продажи
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

            Debug.Log($"[PTS] Погрузка {item.displayName} x{quantity}: " +
                      $"вес {cargo.CurrentWeight:F1}/{cargo.MaxWeight}, " +
                      $"объём {cargo.CurrentVolume:F1}/{cargo.MaxVolume}, " +
                      $"слоты {cargo.UsedSlots}/{cargo.MaxSlots} → новые слоты: {newSlots}");

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
            cargo.AddCargo(item, quantity);
            Debug.Log($"[PTS] Погружено: {item.displayName} x{quantity}");
            Save(); // Сессия 8: Сохраняем после погрузки
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

            Debug.Log($"[PlayerTradeStorage] Разгружено: {item.displayName} x{quantity}");
            Save(); // Сессия 8: Сохраняем после разгрузки
            return true;
        }

        public void Save()
        {
            string locKey = string.IsNullOrEmpty(currentLocationId) ? "global" : currentLocationId.ToLower();
            Debug.Log($"[PlayerTradeStorage] Save локация={locKey}, кредиты={credits:F0}, товаров={warehouse.Count}");
            PlayerPrefs.SetFloat($"TradeCredits_{locKey}", credits);
            var data = new WarehouseSaveData { items = new List<WarehouseSaveItem>() };
            foreach (var w in warehouse)
                if (w.item != null) data.items.Add(new WarehouseSaveItem { itemId = w.item.itemId, quantity = w.quantity });
            PlayerPrefs.SetString($"TradeWarehouse_{locKey}", UnityEngine.JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public void Load()
        {
            string locKey = string.IsNullOrEmpty(currentLocationId) ? "global" : currentLocationId.ToLower();
            credits = PlayerPrefs.GetFloat($"TradeCredits_{locKey}", 1000f);
            string json = PlayerPrefs.GetString($"TradeWarehouse_{locKey}", "");
            Debug.Log($"[PlayerTradeStorage] Load локация={locKey}, ключ=TradeWarehouse_{locKey}, json длина={json.Length}");
            if (!string.IsNullOrEmpty(json))
            {
                var data = UnityEngine.JsonUtility.FromJson<WarehouseSaveData>(json);
                warehouse.Clear();
                var db = FindTradeDatabase();
                Debug.Log($"[PlayerTradeStorage] FindTradeDatabase: {db != null}, items={data?.items?.Count ?? 0}");
                if (db != null && data != null)
                {
                    foreach (var si in data.items)
                    {
                        var def = db.GetItemById(si.itemId);
                        Debug.Log($"[PlayerTradeStorage]   - {si.itemId}: found={def != null}");
                        if (def != null) warehouse.Add(new WarehouseItem { item = def, quantity = si.quantity });
                    }
                }
                Debug.Log($"[PlayerTradeStorage] Загружено {warehouse.Count} товаров");
            }
            else
            {
                warehouse.Clear();
                Debug.Log($"[PlayerTradeStorage] Нет данных для локации {locKey}, склад пуст");
            }
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

            Debug.Log($"[PlayerTradeStorage] Контрактный товар: {item.displayName} x{quantity} (бесплатно)");
            Save(); // Сессия 8: Сохраняем после добавления контрактного товара
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
            Save(); // Сессия 8: Сохраняем после удаления товара
            return true;
        }

        private void OnApplicationQuit() { Save(); }
    }

    [Serializable]
    public class WarehouseSaveData { public List<WarehouseSaveItem> items; }
    [Serializable]
    public class WarehouseSaveItem { public string itemId; public int quantity; }
}
