// T-NS04: NpcShipTrafficManager — Gaussian arrival shaping + min-spacing enforcement.
// Singleton MonoBehaviour, создаётся из NpcShipServer.OnNetworkSpawn (T-NS06).
//
// Pattern: MarketTimeService (Trade/Network/MarketTimeService.cs) — отдельный singleton с tick loop.
// Docs: docs/NPC_others_peacfull/pc_ship/04_LIVING_BEHAVIOR.md §3.

using System.Collections.Generic;
using ProjectC.PeacefulShip.Core;
using ProjectC.PeacefulShip.Stations;
using UnityEngine;

namespace ProjectC.PeacefulShip.Network
{
    /// <summary>
    /// Управляет расписанием прибытия NPC-кораблей.
    /// Гарантирует:
    ///   - Gaussian распределение вокруг meanArrivalIntervalSec
    ///   - min spacing между прибытиями на одну станцию
    ///   - Jitter ±globalJitterMaxSec (Q9: без rate limiting, FSM сама ограничивает)
    /// </summary>
    public class NpcShipTrafficManager : MonoBehaviour
    {
        public static NpcShipTrafficManager Instance { get; private set; }

        [Header("Global Shaping")]
        [SerializeField] private float globalJitterMaxSec = 2f;

        [Tooltip("Минимум секунд между любыми прибытиями на одну станцию.")]
        [SerializeField] private float defaultMinSpacingSec = 8f;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // StationId → last arrival timestamp (для min-spacing enforcement)
        private readonly Dictionary<string, float> _lastArrivalAtStation = new Dictionary<string, float>();

        // === Lifecycle ===

        /// <summary>Система не требует отдельного tick — это pure-data service.
        /// Вызывается по необходимости из NpcShipWorld.RegisterNpc и TickNpc.</summary>
        public static void CreateAndInitialize()
        {
            if (Instance != null) return;
            var go = new GameObject("[NpcShipTrafficManager]");
            Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<NpcShipTrafficManager>();
            Debug.Log("[NpcShipTrafficManager] Created");
        }

        public static void Shutdown()
        {
            if (Instance != null) Object.Destroy(Instance.gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // === Public API ===

        /// <summary>
        /// Возвращает nextArrivalAt (в секундах) для данного корабля на данной станции.
        /// Гарантирует Gaussian распределение и min-spacing.
        /// Q11: при 4 NPC и 2 станциях → ~240 сек средний интервал.
        /// </summary>
        /// <param name="stationId">StationId станции, к которой NPC направляется.</param>
        /// <param name="schedule">Расписание NPC (содержит Gaussian params + min spacing).</param>
        /// <param name="now">Time.time на момент вызова.</param>
        /// <returns>float — время следующего прибытия (Time.time).</returns>
        public float ScheduleNextArrival(string stationId, NpcShipSchedule schedule, float now)
        {
            if (schedule == null)
                return now + 120f; // fallback: 2 min

            // Box-Muller Gaussian
            float u1 = Random.value;
            float u2 = Random.value;
            float z = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);

            float rawInterval = schedule.meanArrivalIntervalSec + schedule.arrivalIntervalStdDev * z;
            float clamped = Mathf.Clamp(rawInterval, schedule.minArrivalSpacingSec, schedule.meanArrivalIntervalSec * 2f);

            // min-spacing enforcement vs last arrival at this station
            float lastArrival = GetLastArrivalAt(stationId);
            float proposed = now + clamped;
            float minSpacing = Mathf.Max(defaultMinSpacingSec, schedule.minArrivalSpacingSec);
            if (proposed - lastArrival < minSpacing)
                proposed = lastArrival + minSpacing;

            // Jitter
            proposed += Random.Range(-globalJitterMaxSec, globalJitterMaxSec);

            SetLastArrivalAt(stationId, proposed);

            if (debugMode)
                Debug.Log($"[NpcShipTrafficManager] ScheduleNextArrival station={stationId} " +
                          $"raw={rawInterval:F1}s clamped={clamped:F1}s proposed={proposed:F1} (now={now:F1}, last={lastArrival:F1})");

            return proposed;
        }

        /// <summary>Сбросить arrival tracking (при scene reload / shutdown).</summary>
        public void Clear()
        {
            _lastArrivalAtStation.Clear();
            if (debugMode) Debug.Log("[NpcShipTrafficManager] Clear — all arrival tracking reset");
        }

        // === Internal ===

        private float GetLastArrivalAt(string stationId)
        {
            return string.IsNullOrEmpty(stationId) ? 0f
                : _lastArrivalAtStation.TryGetValue(stationId, out var last) ? last : 0f;
        }

        private void SetLastArrivalAt(string stationId, float time)
        {
            if (string.IsNullOrEmpty(stationId)) return;
            _lastArrivalAtStation[stationId] = time;
        }
    }
}