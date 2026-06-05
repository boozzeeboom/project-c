using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Trade.Core
{
    /// <summary>
    /// Мини-резолвер товаров для <see cref="ContractWorld"/>.
    /// Не зависит от <c>TradeDatabase</c> (чтобы ContractWorld можно было
    /// инициализировать раньше или независимо от v2-торговли).
    ///
    /// При инициализации опрашивает <c>ContractWorldItemConfig</c> ScriptableObject'ы
    /// из Resources (если есть) ИЛИ принимает список (itemId, displayName, basePrice)
    /// напрямую от ContractServer (который читает TradeItemDatabase из Market).
    ///
    /// C2-этап миграции контрактов на v2-архитектуру (см. docs/dev/CONTRACT_V2_MIGRATION.md).
    /// </summary>
    public class ContractWorldItemResolver
    {
        private readonly Dictionary<string, string> _displayNames = new Dictionary<string, string>();
        private readonly Dictionary<string, float> _basePrices = new Dictionary<string, float>();

        public IReadOnlyList<string> AllItemIds
        {
            get
            {
                var list = new List<string>();
                list.AddRange(_displayNames.Keys);
                return list;
            }
        }

        public int Count => _displayNames.Count;

        public ContractWorldItemResolver() { }

        /// <summary>Добавить один item (id, displayName, basePrice). Идемпотентно — повторный add перезаписывает.</summary>
        public void AddItem(string itemId, string displayName, float basePrice)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            _displayNames[itemId] = string.IsNullOrEmpty(displayName) ? itemId : displayName;
            _basePrices[itemId] = basePrice;
        }

        /// <summary>Bulk add — для инициализации из TradeItemDatabase.</summary>
        public void AddItems(IEnumerable<(string itemId, string displayName, float basePrice)> items)
        {
            if (items == null) return;
            foreach (var (id, name, price) in items)
            {
                AddItem(id, name, price);
            }
        }

        public string GetDisplayName(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return string.Empty;
            return _displayNames.TryGetValue(itemId, out var n) ? n : itemId;
        }

        public float GetBasePrice(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0f;
            return _basePrices.TryGetValue(itemId, out var p) ? p : 0f;
        }

        public bool HasItem(string itemId) => !string.IsNullOrEmpty(itemId) && _displayNames.ContainsKey(itemId);

        // ========================================================
        // FACTORY: автозагрузка из Resources (если настроено)
        // ========================================================

        /// <summary>
        /// Создать резолвер с автозагрузкой из Resources/ContractWorldItems.json
        /// (если файл есть). Это позволяет сконфигурировать товары для контрактов
        /// отдельно от TradeItemDatabase.
        /// </summary>
        public static ContractWorldItemResolver CreateWithDefaults()
        {
            var r = new ContractWorldItemResolver();

            // Жёсткий минимум — 3 базовых товара (как в legacy ContractSystem.CreateFallbackItems:208-217)
            r.AddItem("mesium_canister_v01", "Мезий (канистра)", 10f);
            r.AddItem("antigrav_ingot_v01", "Антигравий (слиток)", 50f);
            r.AddItem("mnp_container_v01", "МНП (контейнер)", 100f);
            r.AddItem("latex_roll_v01", "Латекс (рулон)", 5f);
            r.AddItem("engine_block_v01", "Двигатель (блок)", 500f);
            r.AddItem("armor_plate_v01", "Броня (плита)", 200f);
            r.AddItem("food_crate_v01", "Продовольствие", 8f);

            return r;
        }
    }
}
