using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Trade.Config
{
    /// <summary>
    /// ScriptableObject — конфигурация рынка конкретной локации.
    /// READ-ONLY на сервере: хранит только базовые параметры (id, набор товаров).
    /// Runtime-состояние (текущие цены, сток, спрос) — в <see cref="ProjectC.Trade.Core.MarketState"/>.
    ///
    /// Заменяет старый <c>LocationMarket</c>, который держал mutable state в SO и терял его
    /// при рестарте сцены / перезагрузке Addressables.
    /// </summary>
    [CreateAssetMenu(fileName = "MarketConfig_", menuName = "ProjectC/Trade/Market Config")]
    public class MarketConfig : ScriptableObject
    {
        [Header("Location")]
        [Tooltip("Уникальный id локации: primium / secundus / tertius / quartus")]
        public string locationId = "";

        [Tooltip("Отображаемое имя (например 'Примум')")]
        public string displayName = "";

        [TextArea(2, 4)]
        [Tooltip("Описание (для UI / лора)")]
        public string description = "";

        [Header("Items")]
        [Tooltip("Конфигурации товаров этого рынка. Runtime-состояние в MarketState.")]
        public List<MarketItemConfig> items = new List<MarketItemConfig>();

        public MarketItemConfig GetItemConfig(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || items == null) return null;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].itemId == itemId) return items[i];
            }
            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!string.IsNullOrEmpty(locationId))
                locationId = locationId.ToUpperInvariant();
        }
#endif
    }
}
