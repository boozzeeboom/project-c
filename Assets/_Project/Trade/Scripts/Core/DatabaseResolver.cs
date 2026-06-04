using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Trade.Core
{
    /// <summary>
    /// Имплементация <see cref="TradeItemDefinitionResolver"/> поверх <see cref="TradeDatabase"/>.
    /// Создаётся один раз в <see cref="Network.MarketServer"/> и инжектится в <see cref="TradeWorld"/>.
    /// </summary>
    public class DatabaseResolver : TradeItemDefinitionResolver
    {
        private readonly Dictionary<string, TradeItemDefinition> _byId;

        public DatabaseResolver(TradeDatabase db)
        {
            _byId = new Dictionary<string, TradeItemDefinition>();
            if (db == null || db.allItems == null) return;
            for (int i = 0; i < db.allItems.Count; i++)
            {
                var def = db.allItems[i];
                if (def == null || string.IsNullOrEmpty(def.itemId)) continue;
                _byId[def.itemId] = def;
            }
        }

        public bool TryGet(string itemId, out TradeItemDefinition def)
        {
            if (string.IsNullOrEmpty(itemId)) { def = null; return false; }
            return _byId.TryGetValue(itemId, out def);
        }

        public float GetWeight(string itemId)
        {
            return TryGet(itemId, out var d) ? d.weight : 0f;
        }

        public float GetVolume(string itemId)
        {
            return TryGet(itemId, out var d) ? d.volume : 0f;
        }

        public int GetSlots(string itemId)
        {
            return TryGet(itemId, out var d) ? d.slots : 0;
        }

        public string GetDisplayName(string itemId)
        {
            return TryGet(itemId, out var d) ? d.displayName : itemId;
        }
    }
}
