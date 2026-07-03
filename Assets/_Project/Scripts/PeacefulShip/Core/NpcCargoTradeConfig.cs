// T-CARGO-NPC-01: NpcCargoTradeConfig — список товаров + настройки для NPC-курьера.
// Описывает ЧТО покупать и продавать NPC-кораблём на каждой станции dwell.
// D26-D30 см. docs/NPC_others_peacfull/npc_ship/CARGO/T_CARGO_NPC_01_DESIGN_2026-07-03.md §2.
//
// Pattern: NpcShipRoute (PeacefulShip/Core/NpcShipRoute.cs) — массив struct'ов внутри SO.
// Convention: один class = один .cs файл (Unity 6: T-DOCK-13c fix).

using System;
using UnityEngine;

namespace ProjectC.PeacefulShip.Core
{
    /// <summary>
    /// Один item в cargo-стратегии NPC-курьера.
    /// D30: itemId хранится как string + валидируется через DatabaseResolver.TryGet.
    /// </summary>
    [Serializable]
    public struct NpcCargoTradeConfig
    {
        [Tooltip("ItemId товара (должен существовать в TradeDatabase). Пример: 'resource_mezium_box'.")]
        public string itemId;

        [Tooltip("Сколько единиц купить за 1 dwell. TradeWorld может дать меньше если сток рынка кончился.")]
        [Min(0)] public int desiredQuantity;

        [Tooltip("True = при прилёте на станцию сначала продать этот item (если есть в cargo) перед покупкой. " +
                 "False = только покупать, не продавать (например, для NPC, развозящего подарки).")]
        public bool sellOnArrival;

        [Tooltip("Защита от случайной продажи: не продавать больше этого количества за 1 unload. " +
                 "0 = продать весь доступный стек. Use case: оставить 1-2 единицы 'на расход'.")]
        [Min(0)] public int maxKeepQuantity;
    }

    /// <summary>
    /// Агрегатор: настройки поведения + список items.
    /// По умолчанию useUnlimitedCredits=true, sellAllOnArrival=true, buyConfiguredItemsAfterSell=true.
    /// </summary>
    [Serializable]
    public class NpcCargoTradeListConfig
    {
        [Header("Behavior (T-CARGO-NPC-01)")]
        [Tooltip("True = NPC игнорирует проверку credits при покупке (безлимитный кошелёк для тестов). " +
                 "False = проверяется Repository.GetCredits(npcInstanceId) — и если не хватает, покупка отклоняется.")]
        public bool useUnlimitedCredits = true;

        [Tooltip("Стоп-кран по слотам: даже если рынок позволит купить больше, NPC не превысит этот лимит. " +
                 "Используется чтобы NPC не скупал весь рынок, оставляя что-то игрокам.")]
        [Min(0)] public int maxLoadSlots = 8;

        [Tooltip("Стоп-кран по весу (кг). То же что maxLoadSlots, но по массе.")]
        [Min(0f)] public float maxLoadWeightKg = 200f;

        [Tooltip("True = при прилёте на станцию сначала продать ВСЁ cargo NPC-корабля на рынок этой станции. " +
                 "Это 'unload' фаза. D31: естественная последовательность курьера.")]
        public bool sellAllOnArrival = true;

        [Tooltip("True = после unload (если был) — скупить buyItems с рынка до заполнения maxLoad*. " +
                 "Это 'load' фаза. D31.")]
        public bool buyConfiguredItemsAfterSell = true;

        [Tooltip("Список товаров для покупки. Выполняются в порядке массива. " +
                 "Если рынок не дал нужное qty (stock кончился) — идём к следующему item.")]
        public NpcCargoTradeConfig[] buyItems;
    }
}
