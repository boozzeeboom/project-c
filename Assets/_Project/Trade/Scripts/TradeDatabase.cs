using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectC.Trade
{
    [CreateAssetMenu(fileName = "TradeItemDatabase", menuName = "ProjectC/Trade Database")]
    public class TradeDatabase : ScriptableObject
    {
        [Header("All Trade Items")]
        [Tooltip("Список всех товаров (заполняется через инспектор)")]
        public List<TradeItemDefinition> allItems = new();

        private Dictionary<string, TradeItemDefinition> _itemsById;
        private Dictionary<string, TradeItemDefinition> _itemsByDisplayName;

        private void OnValidate()
        {
            // Перестроить индексы при изменении в инспекторе
            RebuildIndices();
        }

        private void RebuildIndices()
        {
            _itemsById = new Dictionary<string, TradeItemDefinition>();
            _itemsByDisplayName = new Dictionary<string, TradeItemDefinition>();

            foreach (var item in allItems)
            {
                if (item != null)
                {
                    if (!string.IsNullOrEmpty(item.itemId))
                        _itemsById[item.itemId] = item;

                    if (!string.IsNullOrEmpty(item.displayName))
                        _itemsByDisplayName[item.displayName] = item;
                }
            }
        }

        /// <summary>
        /// Получить товар по уникальному ID
        /// </summary>
        public TradeItemDefinition GetItemById(string id)
        {
            if (_itemsById == null) RebuildIndices();
            return _itemsById.TryGetValue(id, out var item) ? item : null;
        }

        /// <summary>
        /// Получить товар по отображаемому имени
        /// </summary>
        public TradeItemDefinition GetItemByDisplayName(string name)
        {
            if (_itemsByDisplayName == null) RebuildIndices();
            return _itemsByDisplayName.TryGetValue(name, out var item) ? item : null;
        }

        /// <summary>
        /// Получить все товары, доступные определённой фракции
        /// </summary>
        public List<TradeItemDefinition> GetItemsByFaction(Faction faction)
        {
            if (allItems == null) return new List<TradeItemDefinition>();

            return allItems
                .Where(item => item != null && (item.requiredFaction == Faction.None || item.requiredFaction == faction))
                .ToList();
        }

        /// <summary>
        /// Получить все контрабандные товары
        /// </summary>
        public List<TradeItemDefinition> GetContrabandItems()
        {
            if (allItems == null) return new List<TradeItemDefinition>();

            return allItems
                .Where(item => item != null && item.isContraband)
                .ToList();
        }

        /// <summary>
        /// Получить все товары
        /// </summary>
        public List<TradeItemDefinition> GetAllItems()
        {
            return allItems.Where(item => item != null).ToList();
        }
    }
}
