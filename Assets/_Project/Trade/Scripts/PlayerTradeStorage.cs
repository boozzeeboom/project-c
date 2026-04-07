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
            if (credits < totalCost) { Debug.LogWarning($"[PlayerTradeStorage] Нет кредитов! {totalCost:F0} > {credits:F0}"); return false; }

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
            return true;
        }

        public bool LoadToShip(string itemId, int quantity, ProjectC.Player.CargoSystem cargo)
        {
            if (cargo == null) return false;
            var wi = warehouse.Find(w => w.item != null && w.item.itemId == itemId);
            if (wi == null || wi.quantity < quantity) return false;

            var item = wi.item;
            if (cargo.CurrentWeight + item.weight * quantity > cargo.MaxWeight ||
                cargo.CurrentVolume + item.volume * quantity > cargo.MaxVolume ||
                cargo.UsedSlots + item.slots * quantity > cargo.MaxSlots)
            {
                Debug.LogWarning($"[PlayerTradeStorage] Не хватает места в трюме для {item.displayName}");
                return false;
            }

            wi.quantity -= quantity;
            if (wi.quantity <= 0) warehouse.Remove(wi);
            cargo.AddCargo(item, quantity);
            Debug.Log($"[PlayerTradeStorage] Погружено: {item.displayName} x{quantity}");
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
            return true;
        }

        public void Save()
        {
            PlayerPrefs.SetFloat("TradeCredits", credits);
            var data = new WarehouseSaveData { items = new List<WarehouseSaveItem>() };
            foreach (var w in warehouse)
                if (w.item != null) data.items.Add(new WarehouseSaveItem { itemId = w.item.itemId, quantity = w.quantity });
            PlayerPrefs.SetString("TradeWarehouse", UnityEngine.JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public void Load()
        {
            credits = PlayerPrefs.GetFloat("TradeCredits", 1000f);
            string json = PlayerPrefs.GetString("TradeWarehouse", "");
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

        private void OnApplicationQuit() { Save(); }
    }

    [Serializable]
    public class WarehouseSaveData { public List<WarehouseSaveItem> items; }
    [Serializable]
    public class WarehouseSaveItem { public string itemId; public int quantity; }
}
