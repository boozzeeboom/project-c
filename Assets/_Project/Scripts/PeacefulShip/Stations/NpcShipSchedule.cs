// T-NS02: NpcShipSchedule — ScriptableObject с маршрутами и параметрами Gaussian shaping.
// Pattern: DockStationDefinition (Docking/Core/DockStationDefinition.cs), MarketConfig (Trade/Config/).
// Convention: один class = один .cs файл (Unity 6: T-DOCK-13c fix).
//
// T-CARGO-NPC-01: добавлена секция cargoTrade (что NPC покупает/продаёт на станциях).
// D29: cargoTrade = вложенный [Serializable] class внутри SO, по аналогии с NpcShipRoute[].

using UnityEngine;
using ProjectC.PeacefulShip.Core;
// T-CARGO-NPC-01: TradeItemDefinitionResolver — runtime-only, в OnValidate недоступен.
// D30: валидация itemId делается в NpcCargoService при первом trade (см. T_CARGO_NPC_01_DESIGN §4.2).

namespace ProjectC.PeacefulShip.Stations
{
    /// <summary>
    /// Расписание NPC-корабля. Один SO переиспользуется многими NPC (или один-на-один — на усмотрение дизайнера).
    /// Описывает:
    /// - Какие маршруты (stops) NPC обходит
    /// - Тип цикла (RoundTrip / Loop / RandomFromPool)
    /// - Параметры Gaussian shaping (meanArrivalIntervalSec + stdDev + minSpacing)
    /// - cargoTrade (T-CARGO-NPC-01): что NPC покупает/продаёт на станциях
    /// См. docs/NPC_others_peacfull/pc_ship/04_LIVING_BEHAVIOR.md §3.
    /// </summary>
    [CreateAssetMenu(fileName = "NpcShipSchedule_", menuName = "ProjectC/PeacefulShip/NpcShipSchedule", order = 110)]
    public class NpcShipSchedule : ScriptableObject
    {
        /// <summary>Тип цикла маршрута.</summary>
        public enum ScheduleType : byte
        {
            RoundTrip,        // A → B → A → B ...
            Loop,             // A → B → C → A → B → C ...
            RandomFromPool    // M1: pick random route from pool на каждом леге
        }

        [Header("Identity")]
        [Tooltip("Stable ID. Используется для логирования и v2 marketplace.")]
        public string scheduleId = "SCH-NPC-001";

        [Tooltip("Человекочитаемое имя для UI/логов.")]
        public string displayName = "Курьер Примум-Тест";

        [Header("Behavior")]
        [Tooltip("RoundTrip / Loop / RandomFromPool (см. enum).")]
        public ScheduleType scheduleType = ScheduleType.RoundTrip;

        [Tooltip("Список leg'ов маршрута. Для RoundTrip — обычно 1 leg (туда-обратно). " +
                 "Для Loop — несколько (A→B→C).")]
        public NpcShipRoute[] routes;

        [Header("Traffic Shaping (Gaussian)")]
        [Tooltip("Среднее время между прибытиями на одну станцию (сек). " +
                 "Default 480 = 8 мин. При 4 NPC и 2 станциях → ~4 мин средний интервал.")]
        public float meanArrivalIntervalSec = 480f;

        [Tooltip("Std dev для Gaussian (сек). Default 90 = 1.5 мин. " +
                 "99.7% прибытий в диапазоне [mean - 3*stdDev, mean + 3*stdDev].")]
        public float arrivalIntervalStdDev = 90f;

        [Tooltip("Минимум секунд между прибытиями на одну станцию. Default 60 = 1 мин. " +
                 "Гарантирует что NPC не прибывают пачками.")]
        public float minArrivalSpacingSec = 60f;

        [Header("NPC Behavior (Q5/Q8)")]
        [Tooltip("Мин. dwell time на станции (включает Docked + Loading), сек. Default 60.")]
        [Min(0f)] public float minDwellTimeSec = 60f;

        [Tooltip("Макс. dwell time на станции, сек. Default 90 = 1.5 мин (Q5).")]
        [Min(0f)] public float maxDwellTimeSec = 90f;

        [Header("NPC Cargo Trade (T-CARGO-NPC-01)")]
        [Tooltip("Что NPC покупает/продаёт на станциях. D26-D30. " +
                 "Пустой/null конфиг = NPC ничего не грузит (поведение как до эпика, M3.2 no-op Loading).")]
        public NpcCargoTradeListConfig cargoTrade = new NpcCargoTradeListConfig();

        // T-CARGO-NPC-01: legacy asset'ы (созданные до эпика) имеют cargoTrade=null после десериализации.
        // OnEnable — Unity hook, вызывается при load SO. Восстанавливаем default.
        // Также auto-fill buyItems дефолтным набором, чтобы NPC возил груз сразу после
        // domain reload без необходимости запускать Editor-команду Fill NpcShipSchedule Cargo Trade.
        // По scheduleId матчим пресет: SCH-NPC-001 (Courier) или SCH-NPC-002 (Trader).
        private void OnEnable()
        {
            if (cargoTrade == null)
                cargoTrade = new NpcCargoTradeListConfig();

            // Auto-fill только если buyItems пуст/null И scheduleId матчит известный пресет.
            // Это даёт zero-touch опыт: юзер не обязан запускать Editor-команду.
            if (cargoTrade.buyItems == null || cargoTrade.buyItems.Length == 0)
                TryAutoFillBuyItems();
        }

        private void TryAutoFillBuyItems()
        {
            // Сопоставление scheduleId → дефолтный buyItems.
            // ItemId взяты из MarketConfig_Primium (проверено 2026-07-03).
            NpcCargoTradeConfig[] preset = null;
            if (scheduleId == "SCH-NPC-001") // Courier
            {
                preset = new NpcCargoTradeConfig[]
                {
                    new NpcCargoTradeConfig { itemId = "resource_mezium_box",   desiredQuantity = 3, sellOnArrival = true, maxKeepQuantity = 0 },
                    new NpcCargoTradeConfig { itemId = "resource_antigrav_box", desiredQuantity = 2, sellOnArrival = true, maxKeepQuantity = 0 },
                };
                cargoTrade.maxLoadSlots = 8;
                cargoTrade.maxLoadWeightKg = 200f;
            }
            else if (scheduleId == "SCH-NPC-002") // Trader
            {
                preset = new NpcCargoTradeConfig[]
                {
                    new NpcCargoTradeConfig { itemId = "resource_copper_wire_box", desiredQuantity = 5, sellOnArrival = true, maxKeepQuantity = 0 },
                    new NpcCargoTradeConfig { itemId = "resource_brass_sheet_box", desiredQuantity = 4, sellOnArrival = true, maxKeepQuantity = 0 },
                };
                cargoTrade.maxLoadSlots = 10;
                cargoTrade.maxLoadWeightKg = 400f;
            }

            if (preset != null)
            {
                cargoTrade.buyItems = preset;
                cargoTrade.useUnlimitedCredits = true;
                cargoTrade.sellAllOnArrival = true;
                cargoTrade.buyConfiguredItemsAfterSell = true;
                Debug.Log($"[NpcShipSchedule:{name}] T-CARGO-NPC-01 auto-filled buyItems from scheduleId='{scheduleId}' preset " +
                          $"(items={preset.Length}, maxLoad={cargoTrade.maxLoadSlots}slots/{cargoTrade.maxLoadWeightKg:F0}kg)");
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Validate max >= min
            if (maxDwellTimeSec < minDwellTimeSec)
            {
                Debug.LogWarning($"[NpcShipSchedule:{name}] maxDwellTimeSec ({maxDwellTimeSec}) < minDwellTimeSec ({minDwellTimeSec})", this);
            }

            // Validate routes
            if (routes == null || routes.Length == 0)
            {
                Debug.LogWarning($"[NpcShipSchedule:{name}] routes array is empty", this);
            }
            else
            {
                for (int i = 0; i < routes.Length; i++)
                {
                    var r = routes[i];
                    if (string.IsNullOrEmpty(r.fromLocationId) || string.IsNullOrEmpty(r.toLocationId))
                    {
                        Debug.LogError($"[NpcShipSchedule:{name}] routes[{i}] missing fromLocationId or toLocationId", this);
                    }
                    if (r.dwellTimeSec < 0f)
                    {
                        Debug.LogError($"[NpcShipSchedule:{name}] routes[{i}].dwellTimeSec < 0", this);
                    }
                }
            }

            // T-CARGO-NPC-01: validate cargoTrade.buyItems (itemId known to TradeDatabase)
            if (cargoTrade == null) return; // допустимо: NPC без cargo trade
            if (cargoTrade.buyItems == null || cargoTrade.buyItems.Length == 0) return;

            // OnValidate не имеет доступа к TradeWorld.Instance (он создан в runtime).
            // Валидируем itemId через Resources.Load — fallback на TradeDatabase, если возможно.
            // D30: DatabaseResolver.TryGet — runtime-only. В Editor только warning о пустых itemId.
            for (int i = 0; i < cargoTrade.buyItems.Length; i++)
            {
                var item = cargoTrade.buyItems[i];
                if (string.IsNullOrEmpty(item.itemId))
                {
                    Debug.LogError($"[NpcShipSchedule:{name}] cargoTrade.buyItems[{i}].itemId is empty", this);
                }
                else if (item.desiredQuantity < 0)
                {
                    Debug.LogError($"[NpcShipSchedule:{name}] cargoTrade.buyItems[{i}].desiredQuantity < 0", this);
                }
            }

            if (cargoTrade.maxLoadSlots < 0)
                Debug.LogError($"[NpcShipSchedule:{name}] cargoTrade.maxLoadSlots < 0", this);
            if (cargoTrade.maxLoadWeightKg < 0f)
                Debug.LogError($"[NpcShipSchedule:{name}] cargoTrade.maxLoadWeightKg < 0", this);
        }
#endif
    }
}