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
        /// Резерв v2 (Gaussian shaping): живых вызовов ScheduleNextArrival сейчас нет
        /// (см. T-NS-P2); lifecycle (Create/Shutdown) — из NpcShipServer.</summary>
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
            _wallSlots.Clear();
            if (debugMode) Debug.Log("[NpcShipTrafficManager] Clear — all arrival tracking reset");
        }

        // === T-NS-WF10: слоты теснин — только один обход в радиусе ===
        // Воронка (один проход у скалы, все маршруты через него): локальный steering
        // сходится в танец всей кучи (лог 000528: шестёрка синхронно 23.1→31.1→40.1).
        // Слот сериализует проход: держатель летит, остальные висят в hover-wait.
        private readonly Dictionary<ulong, (Vector3 pos, float at)> _wallSlots
            = new Dictionary<ulong, (Vector3 pos, float at)>();
        private const float WallSlotExpireSec = 180f;

        /// <summary>
        /// Занять/обновить слот обхода. false = рядом чужой слот (ждать).
        /// Server-only. Протухшие (>180 с) чистятся по ходу.
        /// </summary>
        public bool TryAcquireWallSlot(ulong npcId, Vector3 pos, float radius)
        {
            float now = Time.time;
            ulong staleId = 0;
            bool hasStale = false;
            foreach (var kv in _wallSlots)
            {
                if (kv.Key == npcId) continue;
                if (now - kv.Value.at > WallSlotExpireSec) { staleId = kv.Key; hasStale = true; continue; }
                if (Vector3.Distance(kv.Value.pos, pos) <= radius) return false;
            }
            if (hasStale) _wallSlots.Remove(staleId);
            _wallSlots[npcId] = (pos, now);
            return true;
        }

        /// <summary>Освободить слот (выход из обхода / divert / despawn).</summary>
        public void ReleaseWallSlot(ulong npcId)
        {
            _wallSlots.Remove(npcId);
        }

        /// <summary>T-NS-WF10: сдвиг позиций слотов вместе с миром (F8/F9).</summary>
        public int ApplyRebaseTranslation(Vector3 translation)
        {
            int n = 0;
            if (_wallSlots.Count > 0)
            {
                var keys = new List<ulong>(_wallSlots.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    var v = _wallSlots[keys[i]];
                    _wallSlots[keys[i]] = (v.pos + translation, v.at);
                }
                n = _wallSlots.Count;
            }
            // T-NS-NAV12b: hotspot-карта едет вместе с миром.
            for (int i = 0; i < _hotspots.Count; i++)
            {
                var h = _hotspots[i];
                _hotspots[i] = (h.pos + translation, h.at, h.hits);
                n++;
            }
            return n;
        }

        // === T-NS-NAV12b: hotspot-карта — обучение на заторах (server-only) ===
        // Точки wall-divert-noprogress/stuck, cruise-stuck-divert, wall-slot-divert:
        // места, где leg'и регулярно умирают. Навигатор проверяет прямую через карту
        // и вставляет обход заранее. Список ограничен, с expiry — не архив, а память.
        private readonly List<(Vector3 pos, float at, int hits)> _hotspots
            = new List<(Vector3 pos, float at, int hits)>();
        private const int HotspotMax = 20;
        private const float HotspotExpireSec = 600f;
        private const float HotspotMergeR = 400f;

        /// <summary>Записать точку затора (схлопывание в радиусе, счётчик hits).</summary>
        public void RecordHotspot(Vector3 pos)
        {
            float now = Time.time;
            _hotspots.RemoveAll(h => now - h.at > HotspotExpireSec);
            for (int i = 0; i < _hotspots.Count; i++)
            {
                if (Vector3.Distance(_hotspots[i].pos, pos) <= HotspotMergeR)
                {
                    var h = _hotspots[i];
                    _hotspots[i] = (h.pos, now, h.hits + 1);
                    return;
                }
            }
            if (_hotspots.Count >= HotspotMax) _hotspots.RemoveAt(0);
            _hotspots.Add((pos, now, 1));
        }

        /// <summary>Штраф позиции: рядом с частыми заторами — огромный (маршрут обойдёт).</summary>
        public float HotspotPenalty(Vector3 pos, float radius)
        {
            float now = Time.time;
            float pen = 0f;
            for (int i = 0; i < _hotspots.Count; i++)
            {
                var h = _hotspots[i];
                if (now - h.at > HotspotExpireSec) continue;
                float d = Vector3.Distance(h.pos, pos);
                if (d < radius) pen += (1f - d / radius) * 10000f * h.hits;
            }
            return pen;
        }

        /// <summary>
        /// T-NS-WF06c: свободен ли взлёт у станции (departure mutex): никто чужой
        /// не в Lifting/Yawing в радиусе. Разносит вылеты пачкой — стая не стартует
        /// одновременно через одну скалу. Server-only, запрос через spatial index.
        /// </summary>
        public bool IsDepartureClear(Vector3 stationPos, float radius, ulong requesterId)
        {
            var nearby = NpcShipZoneRegistry.QueryNearby(stationPos, radius);
            for (int i = 0; i < nearby.Count; i++)
            {
                var npc = nearby[i];
                if (npc == null || npc.NpcInstanceId == requesterId) continue;
                var m = npc.CurrentMode;
                if (m == Stations.NpcShipController.NavMode.Lifting
                    || m == Stations.NpcShipController.NavMode.Yawing)
                    return false;
            }
            return true;
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