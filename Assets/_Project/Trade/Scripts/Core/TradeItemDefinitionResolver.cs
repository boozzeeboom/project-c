using UnityEngine;

namespace ProjectC.Trade.Core
{
    /// <summary>
    /// Помогает получить вес/объём/слоты по itemId, не завися от
    /// MonoBehaviour-компонента TradeDatabase. Реализация
    /// <see cref="Resources.TradeItemDefinitionResolver"/> живёт в TradeWorld.
    ///
    /// Зачем: серверная логика (TradeWorld, CargoData, Warehouse) — POCO,
    /// она не может дёргать UnityEditor APIs или FindObjectsByType. Резолвер
    /// инжектируется в конструктор TradeWorld и используется для расчётов.
    /// </summary>
    public interface TradeItemDefinitionResolver
    {
        bool TryGet(string itemId, out TradeItemDefinition def);
        float GetWeight(string itemId);
        float GetVolume(string itemId);
        int GetSlots(string itemId);
        string GetDisplayName(string itemId);
    }
}
