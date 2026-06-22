// T-NS02: NpcShipSchedule — ScriptableObject с маршрутами и параметрами Gaussian shaping.
// Pattern: DockStationDefinition (Docking/Core/DockStationDefinition.cs), MarketConfig (Trade/Config/).
// Convention: один class = один .cs файл (Unity 6: T-DOCK-13c fix).

using UnityEngine;
using ProjectC.PeacefulShip.Core;

namespace ProjectC.PeacefulShip.Stations
{
    /// <summary>
    /// Расписание NPC-корабля. Один SO переиспользуется многими NPC (или один-на-один — на усмотрение дизайнера).
    /// Описывает:
    /// - Какие маршруты (stops) NPC обходит
    /// - Тип цикла (RoundTrip / Loop / RandomFromPool)
    /// - Параметры Gaussian shaping (meanArrivalIntervalSec + stdDev + minSpacing)
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
        }
#endif
    }
}