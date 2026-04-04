using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Items
{
    /// <summary>
    /// Менеджер инвентаря. Хранит предметы, сгруппированные по типам.
    /// Каждый тип привязан к своему сектору кругового колеса (8 секторов).
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        public static Inventory Instance { get; private set; }

        // Словарь: тип предмета → список предметов этого типа
        private Dictionary<ItemType, List<ItemData>> _itemsByType = new Dictionary<ItemType, List<ItemData>>();

        // Инициализируем все 8 типов пустыми списками
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

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
            Debug.Log($"[Inventory] Подобрал: {item.itemName} (Тип: {item.itemType}) — всего в ячейке: {_itemsByType[item.itemType].Count}");
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

            Debug.Log($"[Inventory] Открыл сундук: подобрано {items.Count} предметов.");
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
    }
}
