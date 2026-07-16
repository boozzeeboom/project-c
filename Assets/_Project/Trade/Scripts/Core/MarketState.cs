using System.Collections.Generic;
using ProjectC.Trade.Config;
using ProjectC.Trade.Service;

namespace ProjectC.Trade.Core
{
    /// <summary>
    /// Серверное runtime-состояние рынка одной локации. POCO, без Unity-зависимостей.
    /// Живёт в <see cref="TradeWorld"/> (server-only, in-memory).
    ///
    /// Создаётся при инициализации TradeWorld из <see cref="Config.MarketConfig"/>.
    /// При перезапуске сервера состояние сбрасывается к базовым значениям (как и должно быть —
    /// цены не должны переживать рестарт; постоянные данные игрока — в <see cref="Repository.IPlayerDataRepository"/>).
    /// </summary>
    public class MarketState
    {
        public readonly string locationId;
        public readonly MarketConfig config;  // ссылка на SO для доступа к MarketItemConfig

        // itemId → state
        private readonly Dictionary<string, MarketItemState> _items = new Dictionary<string, MarketItemState>();

        public IReadOnlyDictionary<string, MarketItemState> Items => _items;

        // Per-market overrides (из MarketConfig)
        public float PriceFloorRatio { get; private set; }
        public float PriceCeilingRatio { get; private set; }
        public float DecayHalfLifeSeconds { get; private set; }
        public float RegenMultiplier { get; private set; }

        public MarketState(string locationId, MarketConfig config)
        {
            this.locationId = locationId;
            this.config = config;
        }

        /// <summary>
        /// Построить начальное состояние из config. Вызывается один раз при создании.
        /// </summary>
        public void Initialize()
        {
            _items.Clear();

            // Кэшируем per-market overrides
            PriceFloorRatio = config != null ? config.priceFloorRatio : PriceFormula.PRICE_FLOOR_RATIO;
            PriceCeilingRatio = config != null ? config.priceCeilingRatio : PriceFormula.PRICE_CEILING_RATIO;
            DecayHalfLifeSeconds = config != null ? config.decayHalfLifeSeconds : PriceFormula.DEFAULT_HALF_LIFE_SECONDS;
            RegenMultiplier = config != null ? config.regenMultiplier : 1.0f;

            if (config == null || config.items == null) return;

            for (int i = 0; i < config.items.Count; i++)
            {
                var cfg = config.items[i];
                if (cfg == null || string.IsNullOrEmpty(cfg.itemId)) continue;

                var state = new MarketItemState(cfg)
                {
                    availableStock = cfg.initialStock,
                    currentPrice = cfg.basePrice,
                    demandFactor = 0f,
                    supplyFactor = 0f,
                    eventMultiplier = 1f,
                    version = 1
                };
                state.RecalculatePrice(PriceFloorRatio, PriceCeilingRatio);
                _items[cfg.itemId] = state;
            }
        }

        public MarketItemState GetItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            _items.TryGetValue(itemId, out var s);
            return s;
        }

        public bool HasItem(string itemId) => !string.IsNullOrEmpty(itemId) && _items.ContainsKey(itemId);

        /// <summary>
        /// Суммарная «версия» рынка — для delta-синхронизации (можно передать только изменённые позиции).
        /// </summary>
        public int ComputeVersion()
        {
            int max = 0;
            foreach (var kv in _items)
            {
                if (kv.Value != null && kv.Value.version > max) max = kv.Value.version;
            }
            return max;
        }
    }
}
