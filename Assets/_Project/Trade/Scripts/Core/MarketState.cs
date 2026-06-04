using System.Collections.Generic;
using ProjectC.Trade.Config;

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
            if (config == null || config.items == null) return;

            for (int i = 0; i < config.items.Count; i++)
            {
                var cfg = config.items[i];
                if (cfg == null || string.IsNullOrEmpty(cfg.itemId)) continue;

                _items[cfg.itemId] = new MarketItemState(cfg)
                {
                    availableStock = cfg.initialStock,
                    currentPrice = cfg.basePrice,
                    demandFactor = 0f,
                    supplyFactor = 0f,
                    eventMultiplier = 1f,
                    version = 1
                };
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
