using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Items
{
    /// <summary>
    /// Менеджер инвентаря. Хранит предметы, сгруппированные по типам.
    /// Каждый тип привязан к своему сектору кругового колеса (8 секторов).
    /// Каждый игрок имеет свой экземпляр Inventory (вешется на NetworkPlayer).
    /// Поддержка сохранения/загрузки (через PlayerPrefs) для реконнекта.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        // Словарь: тип предмета → список предметов этого типа
        private Dictionary<ItemType, List<ItemData>> _itemsByType = new Dictionary<ItemType, List<ItemData>>();

        // Инициализируем все 8 типов пустыми списками
        private void Awake()
        {
            foreach (ItemType type in System.Enum.GetValues(typeof(ItemType)))
            {
                _itemsByType[type] = new List<ItemData>();
            }
        }

        /// <summary>
        /// Добавить предмет в инвентарь (автоматически в свой тип)
        /// </summary>
        public void AddItem(ItemData item)
        {
            if (item == null) return;

            _itemsByType[item.itemType].Add(item);
        }

        /// <summary>
        /// Добавить несколько предметов пакетом (для сундуков).
        /// Вызывает событие для UI после каждого добавления.
        /// </summary>
        public void AddMultipleItems(List<ItemData> items)
        {
            if (items == null || items.Count == 0) return;

            foreach (var item in items)
            {
                AddItem(item);
            }
        }

        /// <summary>
        /// Получить все предметы определённого типа
        /// </summary>
        public List<ItemData> GetItemsByType(ItemType type)
        {
            if (_itemsByType.ContainsKey(type))
                return _itemsByType[type];
            return new List<ItemData>();
        }

        /// <summary>
        /// Получить количество предметов определённого типа
        /// </summary>
        public int GetCountByType(ItemType type)
        {
            if (_itemsByType.ContainsKey(type))
                return _itemsByType[type].Count;
            return 0;
        }

        /// <summary>
        /// Получить общее количество всех предметов
        /// </summary>
        public int GetTotalItemCount()
        {
            int total = 0;
            foreach (var list in _itemsByType.Values)
                total += list.Count;
            return total;
        }

        /// <summary>
        /// Содержит ли инвентарь хотя бы один предмет
        /// </summary>
        public bool HasItemsInType(ItemType type)
        {
            return GetCountByType(type) > 0;
        }

        /// <summary>
        /// Получить все типы которые содержат предметы
        /// </summary>
        public List<ItemType> GetNonEmptyTypes()
        {
            var result = new List<ItemType>();
            foreach (var kvp in _itemsByType)
            {
                if (kvp.Value.Count > 0)
                    result.Add(kvp.Key);
            }
            return result;
        }

        // ==================== СОХРАНЕНИЕ / ЗАГРУЗКА ====================

        /// <summary>
        /// Сохранить инвентарь в PlayerPrefs (для реконнекта)
        /// </summary>
        public void SaveToPrefs(string key = "InventoryData")
        {
            var saveData = new List<string>();
            foreach (var kvp in _itemsByType)
            {
                foreach (var item in kvp.Value)
                {
                    if (item != null)
                    {
                        // Сохраняем имя предмета как идентификатор
                        saveData.Add($"{(int)kvp.Key}:{item.itemName}");
                    }
                }
            }
            PlayerPrefs.SetString(key, string.Join(",", saveData));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Загрузить инвентарь из PlayerPrefs (после реконнекта)
        /// </summary>
        public void LoadFromPrefs(string key = "InventoryData")
        {
            string data = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(data))
            {
                Debug.Log("[Inventory] Нет сохранённых данных");
                return;
            }

            var parts = data.Split(',');
            int loaded = 0;

            // Загружаем ВСЕ предметы из всех Resources папок
            var allItems = Resources.LoadAll<ItemData>("");

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                var split = part.Split(':');
                if (split.Length >= 2 && int.TryParse(split[0], out int typeIdx))
                {
                    ItemType type = (ItemType)typeIdx;
                    string itemName = split[1];

                    // Ищем предмет по имени и типу
                    foreach (var item in allItems)
                    {
                        if (item.itemName == itemName && item.itemType == type)
                        {
                            _itemsByType[type].Add(item);
                            loaded++;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Очистить сохранённые данные
        /// </summary>
        public static void ClearSavedInventory(string key = "InventoryData")
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
