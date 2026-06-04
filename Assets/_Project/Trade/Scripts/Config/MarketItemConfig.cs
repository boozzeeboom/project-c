using System;
using UnityEngine;

namespace ProjectC.Trade.Config
{
    /// <summary>
    /// Конфигурация одного товара на рынке.
    /// Содержит ТОЛЬКО статику (id, базовая цена, начальный сток, регенерация).
    /// НЕ хранит текущую цену / спрос / предложение — это всё в <see cref="Core.MarketItemState"/>.
    ///
    /// Ссылка на <see cref="TradeItemDefinition"/> нужна ТОЛЬКО для UI (иконка, описание, вес/объём/слоты).
    /// Серверная логика НЕ полагается на definition — только на itemId.
    /// </summary>
    [Serializable]
    public class MarketItemConfig
    {
        [Header("Item")]
        [Tooltip("Уникальный id товара (напр. 'mesium_canister_v01')")]
        public string itemId = "";

        [Tooltip("Ссылка на TradeItemDefinition (для UI: иконка, описание, вес/объём/слоты)")]
        public TradeItemDefinition definition;

        [Header("Economy")]
        [Tooltip("Базовая цена CR. На сервере: currentPrice = basePrice * (1 + demand - supply) * event")]
        public float basePrice = 10f;

        [Tooltip("Начальный сток при инициализации")]
        public int initialStock = 50;

        [Tooltip("Регенерация стока за тик (0..1, доля от initialStock). 0.02 = +2% за тик.")]
        [Range(0f, 0.5f)]
        public float regenPerTick = 0.02f;

        [Header("Restrictions")]
        [Tooltip("Фракция, которой разрешено торговать (None = всем)")]
        public Faction factionRestriction = Faction.None;

        [Tooltip("Разрешена ли покупка у рынка")]
        public bool allowBuy = true;

        [Tooltip("Разрешена ли продажа на рынок")]
        public bool allowSell = true;
    }
}
