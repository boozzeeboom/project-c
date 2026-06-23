// T-NS03: NpcShipWorld — server-only state machine для всех NPC-кораблей.
// Полная FSM реализация. Stub (T-NS02) заменён этим файлом.
//
// Pattern: DockingWorld (Docking/Core/DockingWorld.cs:19), QuestWorld (Quests/Core/QuestWorld.cs).
// FSM transition diagram: docs/NPC_others_peacfull/pc_ship/04_LIVING_BEHAVIOR.md §2.

using System.Collections.Generic;
using ProjectC.PeacefulShip.Network;  // NpcShipZoneRegistry (T-NS02)
using ProjectC.PeacefulShip.Stations;  // NpcShipSchedule, NpcShipController
using ProjectC.Player;
using UnityEngine;

namespace ProjectC.PeacefulShip.Core
{
    /// <summary>
    /// Server-only state machine для всех NPC-кораблей.
    /// Singleton MonoBehaviour, DontDestroyOnLoad.
    /// Created by NpcShipServer.OnNetworkSpawn (T-NS06).
    /// </summary>
    public class NpcShipWorld : MonoBehaviour
    {
        public static NpcShipWorld Instance { get; private set; }

        // === State ===

        // NpcInstanceId → NpcShipState (server-only SOT)
        private readonly Dictionary<ulong, NpcShipState> _npcByInstanceId = new Dictionary<ulong, NpcShipState>();

        // NpcInstanceId → NpcShipSchedule (SO ссылка для FSM logic)
        private readonly Dictionary<ulong, NpcShipSchedule> _scheduleByNpcInstanceId = new Dictionary<ulong, NpcShipSchedule>();

        // StationId → последний arrival timestamp (для min spacing Q11)
        private readonly Dictionary<string, float> _lastArrivalAtStation = new Dictionary<string, float>();
    // Throttle pad assignment attempts (avoid per-frame spam)
    private readonly Dictionary<ulong, float> _lastPadAttempt = new Dictionary<ulong, float>();
    // === NPC barge flight constants ===
    private const float CRUISE_ALT_OFFSET = 100f;
    private const float APPROACH_DIST = 20f;  // докинг только в 20м от пада

        // === Events (server fires, others subscribe) ===
        // Q10 stubs (v2 subscribers: TradeWorld, QuestServer)
#pragma warning disable 0067
        public event System.Action<ulong, string> OnNpcShipArrived;
        public event System.Action<ulong, string> OnNpcShipDeparted;
#pragma warning restore 0067

        // === Lifecycle ===

        /// <summary>
        /// Singleton init. Вызывается из NpcShipServer.OnNetworkSpawn (T-NS06).
        /// </summary>
        public static void CreateAndInitialize()
        {
            if (Instance != null) return;
            var go = new GameObject("[NpcShipWorld]");
            Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<NpcShipWorld>();
            Debug.Log("[NpcShipWorld] Created");
        }

        public static void Shutdown()
        {
            if (Instance != null) Object.Destroy(Instance.gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // === Public API (server-side) ===

        public void RegisterNpc(ulong id, ShipController ship, NpcShipSchedule schedule)
        {
            if (id == 0 || ship == null) return;
            if (_npcByInstanceId.ContainsKey(id)) return; // idempotent

            _npcByInstanceId[id] = new NpcShipState(id, ship);
            _scheduleByNpcInstanceId[id] = schedule;
            Debug.Log($"[NpcShipWorld] RegisterNpc id={id:X}");
        }

        public void UnregisterNpc(ulong id)
        {
            if (_npcByInstanceId.Remove(id))
            {
                _scheduleByNpcInstanceId.Remove(id);
                Debug.Log($"[NpcShipWorld] UnregisterNpc id={id:X}");
            }
        }

        public NpcShipState GetNpc(ulong id)
            => _npcByInstanceId.TryGetValue(id, out var s) ? s : null;

        public int AllNpcCount => _npcByInstanceId.Count;

        /// <summary>Read-only iterate all NPCs (for FSM tick, debugging).</summary>
        public IEnumerable<NpcShipState> AllNpcs => _npcByInstanceId.Values;

        // === FSM Tick ===

        /// <summary>
        /// Per-frame FSM tick — вызывается у каждого зарегистрированного NPC.
        /// Документация: docs/NPC_others_peacfull/pc_ship/04_LIVING_BEHAVIOR.md §2.
        /// </summary>
        private void Update()
        {
            if (!ProjectC.Trade.Network.NetworkingUtils.IsServerSafe()) return;

            float dt = Time.deltaTime;
            foreach (var state in _npcByInstanceId.Values)
            {
                if (state == null || state.Ship == null) continue;
                TickNpc(state, dt);
            }
        }

        private void TickNpc(NpcShipState state, float dt)
        {
            if (!_scheduleByNpcInstanceId.TryGetValue(state.NpcInstanceId, out var schedule) || schedule == null)
                return;

            var controller = NpcShipZoneRegistry.Get(state.NpcInstanceId);
            if (controller == null) return;

            float timeInState = Time.time - state.StateEnteredAt;

            switch (state.Status)
            {
                case NpcShipStatus.Idle:
                    // First leg — start cycle. Pick initial route based on schedule.
                    if (schedule.routes != null && schedule.routes.Length > 0)
                    {
                        state.CurrentRoute = schedule.routes[0];
                        state.ScheduleIndex = 0;
                        TransitionTo(state, NpcShipStatus.Departing);
                        Debug.Log($"[NpcShipWorld:NPC] id={state.NpcInstanceId:X} Idle→Departing (route {state.CurrentRoute.fromLocationId}→{state.CurrentRoute.toLocationId})");
                    }
                    break;

                case NpcShipStatus.Departing:
                    // Pre-condition: Docked → ExitDocked + start anti-grav + take off
                    if (state.Ship != null && state.Ship.IsDocked)
                    {
                        ReleaseNpcAssignment(state);
                        controller.StartAntiGravityBoost();
                    }
                    // Pure vertical climb with physics
                    ApplyDepartingMovement(state, controller);
                    // Transition after enough climb time (15s for 100m) or altitude check
                    float startY = state.StateEnteredAt > 0 ? state.LastKnownPosition.y : state.Ship.transform.position.y;
                    float cruiseTarget = startY + CRUISE_ALT_OFFSET;
                    if (state.Ship.transform.position.y >= cruiseTarget - 5f || timeInState > 20f)
                    {
                        state.FlightDirection = Vector3.zero; // fresh direction for transit
                        TransitionTo(state, NpcShipStatus.InTransit);
                    }
                    break;

                case NpcShipStatus.InTransit:
                    // Cruise toward target station at altitude
                    ApplyTransitMovement(state, controller);
                    // Detect arrival: dist < 500m
                    var targetPos = ResolveStationWorldPos(state.CurrentRoute.toLocationId);
                    if (targetPos.HasValue && Vector3.Distance(state.Ship.transform.position, targetPos.Value) < 500f)
                    {
                        TransitionTo(state, NpcShipStatus.Approaching);
                        Debug.Log($"[NpcShipWorld:NPC] id={state.NpcInstanceId:X} InTransit→Approaching (dist<500m to {state.CurrentRoute.toLocationId})");
                    }
                    break;

                case NpcShipStatus.Approaching:
                    // Slow descent toward target station
                    ApplyApproachMovement(state, controller);
                    // Try to assign a pad (throttled — not every frame!)
                    if (timeInState < 2f || _lastPadAttempt.ContainsKey(state.NpcInstanceId) && 
                        Time.time - _lastPadAttempt[state.NpcInstanceId] < 2f)
                    {
                        // Skip this frame (throttle at 2s)
                    }
                    else
                    {
                        _lastPadAttempt[state.NpcInstanceId] = Time.time;
                        if (TryAssignPadForNpc(state))
                        {
                            TransitionTo(state, NpcShipStatus.Docking);
                            Debug.Log($"[NpcShipWorld:NPC] id={state.NpcInstanceId:X} Approaching→Docking");
                        }
                        // No timeout→Diverting: stay in Approaching, keep retrying
                        // NPC hovers until pad frees up. This prevents the ping-pong loop.
                    }
                    break;

                case NpcShipStatus.Holding:
                    // Wait 5 sec, retry
                    if (timeInState > 5f)
                    {
                        TransitionTo(state, NpcShipStatus.Approaching);
                    }
                    break;

                case NpcShipStatus.Diverting:
                    // Pick next route in schedule
                    AdvanceScheduleIndex(state, schedule);
                    TransitionTo(state, NpcShipStatus.InTransit);
                    Debug.Log($"[NpcShipWorld:NPC] id={state.NpcInstanceId:X} Diverting→InTransit (next: {state.CurrentRoute.toLocationId})");
                    break;

                case NpcShipStatus.Docking:
                    // Brief state — server teleport or wait for trigger
                    // Auto-detect touchdown via DockingPadTriggerBox (handled server-side via T-NS05 NotifyTouchedDown)
                    // Fallback: after 5 sec assume docked
                    if (state.Ship != null && state.Ship.IsDocked)
                    {
                        TransitionTo(state, NpcShipStatus.Docked);
                        Debug.Log($"[NpcShipWorld:NPC] id={state.NpcInstanceId:X} Docking→Docked");
                    }
                    else if (timeInState > 10f)
                    {
                        // Timeout — force EnterDocked to advance
                        if (state.Ship != null) state.Ship.EnterDocked();
                        TransitionTo(state, NpcShipStatus.Docked);
                    }
                    break;

                case NpcShipStatus.Docked:
                    // Wait for dwellTime elapsed (30-90s Q5), then Loading
                    float dwellTime = Mathf.Clamp(state.CurrentRoute.dwellTimeSec, schedule.minDwellTimeSec, schedule.maxDwellTimeSec);
                    if (timeInState > dwellTime)
                    {
                        TransitionTo(state, NpcShipStatus.Loading);
                        Debug.Log($"[NpcShipWorld:NPC] id={state.NpcInstanceId:X} Docked→Loading (dwelled {timeInState:F1}s of {dwellTime:F1}s)");
                    }
                    break;

                case NpcShipStatus.Loading:
                    // 30-90 sec no-op pause (Q5). Visual only — real cargo in v2.
                    float loadingTime = schedule.maxDwellTimeSec * 0.5f; // ~45s default
                    if (timeInState > loadingTime)
                    {
                        TransitionTo(state, NpcShipStatus.Undocking);
                    }
                    break;

                case NpcShipStatus.Undocking:
                    // ExitDocked + start anti-grav
                    if (state.Ship != null && state.Ship.IsDocked)
                    {
                        ReleaseNpcAssignment(state);
                        state.Ship.ExitDocked();
                        controller.StartAntiGravityBoost();
                    }
                    if (timeInState > 2f)
                    {
                        state.AssignedPadId = null; // reset — next destination gets fresh pad
                        AdvanceScheduleIndex(state, schedule);
                        TransitionTo(state, NpcShipStatus.Departing);
                        Debug.Log($"[NpcShipWorld:NPC] id={state.NpcInstanceId:X} Undocking→Departing (next leg: {state.CurrentRoute.toLocationId})");
                    }
                    break;

                case NpcShipStatus.Done:
                    // Cycle restart
                    state.ScheduleIndex = 0;
                    TransitionTo(state, NpcShipStatus.Departing);
                    break;
            }
        }

        // === Movement helpers ===

        private void ApplyDepartingMovement(NpcShipState state, NpcShipController controller)
        {
            // Pure vertical climb via ship physics, no forward movement
            controller.ApplyMovementInput(thrust: 0f, yaw: 0f, pitch: 0f, vertical: 1.0f);
        }

        private void ApplyTransitMovement(NpcShipState state, NpcShipController controller)
        {
            var targetPos = ResolveStationWorldPos(state.CurrentRoute.toLocationId);
            if (!targetPos.HasValue) return;
            var ship = state.Ship;
            var pos = ship.transform.position;

            // FlightDirection = вектор к цели (запоминаем один раз)
            if (state.FlightDirection == Vector3.zero)
            {
                Vector3 dir = (targetPos.Value - pos).normalized;
                dir.y = 0f;
                state.FlightDirection = dir;
            }

            float dist = Vector3.Distance(new Vector3(pos.x, 0, pos.z),
                new Vector3(targetPos.Value.x, 0, targetPos.Value.z));

            if (dist > APPROACH_DIST)
            {
                // Поворот к цели (прямой transform.rotation, не через physics)
                var targetRot = Quaternion.LookRotation(state.FlightDirection);
                ship.transform.rotation = Quaternion.RotateTowards(
                    ship.transform.rotation, targetRot, 90f * Time.deltaTime);

                // Удержание высоты
                float cruiseAlt = (state.StateEnteredAt > 0 ? state.LastKnownPosition.y : pos.y) + CRUISE_ALT_OFFSET;
                float altErr = cruiseAlt - pos.y;
                float vertical = Mathf.Clamp(altErr * 0.05f, -0.3f, 0.3f);

                // Вперёд только когда почти смотрим в нужную сторону
                float facingDot = Vector3.Dot(ship.transform.forward, state.FlightDirection);
                float thrust = Mathf.Clamp01((facingDot - 0.5f) * 2f) * 0.8f; // 0→1 когда смотрит в цель

                controller.ApplyMovementInput(thrust: thrust, yaw: 0f, pitch: 0f, vertical: vertical);
            }
            else
            {
                state.FlightDirection = Vector3.zero; // сбросить для следующей фазы
            }
        }

        private void ApplyApproachMovement(NpcShipState state, NpcShipController controller)
        {
            Vector3? targetPos;
            if (!string.IsNullOrEmpty(state.AssignedPadId))
                targetPos = ResolvePadWorldPos(state.CurrentRoute.toLocationId, state.AssignedPadId);
            else
                targetPos = ResolveStationWorldPos(state.CurrentRoute.toLocationId);
            if (!targetPos.HasValue) return;

            var ship = state.Ship;
            var pos = ship.transform.position;
            Vector3 target = targetPos.Value;
            target.y += 3f; // 3м над падом

            Vector3 dir = (target - pos).normalized;
            float dist = Vector3.Distance(pos, target);

            if (dist > 2f)
            {
                var targetRot = Quaternion.LookRotation(dir);
                ship.transform.rotation = Quaternion.RotateTowards(ship.transform.rotation, targetRot, 30f * Time.deltaTime);

                float speed = Mathf.Clamp01(dist / 50f) * 0.4f; // тормозим на подходе
                float altErr = target.y - pos.y;
                float vertical = Mathf.Clamp(altErr * 0.1f, -0.5f, 0.5f);

                controller.ApplyMovementInput(thrust: speed, yaw: 0f, pitch: 0f, vertical: vertical);
            }
            else
            {
                // Уже на месте — нулевая тяга
                controller.ApplyMovementInput(thrust: 0f, yaw: 0f, pitch: 0f, vertical: 0f);
            }
        }

        private static float CalcBearing(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        // === Schedule helpers ===

        private void AdvanceScheduleIndex(NpcShipState state, NpcShipSchedule schedule)
        {
            if (schedule.routes == null || schedule.routes.Length == 0) return;
            int len = schedule.routes.Length;
            switch (schedule.scheduleType)
            {
                case NpcShipSchedule.ScheduleType.RoundTrip:
                    if (len == 1)
                    {
                        var route = schedule.routes[0];
                        // ОДИН маршрут: чередуем forward/reverse каждый вызов
                        // Сначала инкрементим, потом решаем направление
                        state.ScheduleIndex++;
                        // Нечётный (1,3,5) = только что завершил forward → летит reverse
                        // Чётный (2,4,6) = только что завершил reverse → летит forward
                        if (state.ScheduleIndex % 2 == 1)
                        {
                            // Reverse direction
                            state.CurrentRoute = new NpcShipRoute
                            {
                                fromLocationId = route.toLocationId,
                                toLocationId = route.fromLocationId,
                                dwellTimeSec = route.dwellTimeSec,
                                flightDurationSec = route.flightDurationSec,
                                preferredShipClass = route.preferredShipClass,
                                demandCategory = route.demandCategory
                            };
                        }
                        else
                        {
                            state.CurrentRoute = route; // forward
                        }
                        return;
                    }
                    else
                    {
                        state.ScheduleIndex = (state.ScheduleIndex + 1) % len;
                        state.CurrentRoute = schedule.routes[state.ScheduleIndex];
                    }
                    return;
                case NpcShipSchedule.ScheduleType.Loop:
                    state.ScheduleIndex = (state.ScheduleIndex + 1) % len;
                    break;
                case NpcShipSchedule.ScheduleType.RandomFromPool:
                    state.ScheduleIndex = Random.Range(0, len);
                    break;
            }
            state.CurrentRoute = schedule.routes[state.ScheduleIndex];
        }

        // === State transitions ===

        private void TransitionTo(NpcShipState state, NpcShipStatus newStatus)
        {
            state.Status = newStatus;
            state.StateEnteredAt = Time.time;
            state.LastKnownPosition = state.Ship != null ? state.Ship.transform.position : Vector3.zero;
        }

        // === Docking integration (Q5) ===

        /// <summary>
        /// Tries to assign a pad for NPC at current target station.
        /// Returns true if assignment succeeded.
        /// DELEGATE to DockingWorld.AssignPadForNpc (T-NS05).
        /// </summary>
        private bool TryAssignPadForNpc(NpcShipState state)
        {
            if (state.Ship == null) return false;
            if (Docking.Core.DockingWorld.Instance == null) return false;
            var station = Docking.Network.DockingZoneRegistry.GetByLocation(state.CurrentRoute.toLocationId);
            if (station == null) return false;

            // === distance check: NPC должен быть физически рядом с падом ===
            float distToStation = Vector3.Distance(state.Ship.transform.position, station.transform.position);
            if (distToStation > APPROACH_DIST) return false; // не рядом — жди

            string assignedPadId = Docking.Core.DockingWorld.Instance.AssignPadForNpc(
                station, state.Ship, state.Ship.ShipFlightClass, state.NpcInstanceId);
            if (!string.IsNullOrEmpty(assignedPadId))
            {
                state.AssignedPadId = assignedPadId;
                return true;
            }
            return false;
        }

        private void ReleaseNpcAssignment(NpcShipState state)
        {
            // T-NS05: ReleaseNpcAssignment
            if (Docking.Core.DockingWorld.Instance == null) return;
            if (state.Ship == null || state.Ship.NetworkObject == null) return;
            Docking.Core.DockingWorld.Instance.ReleaseNpcAssignment(state.NpcInstanceId, state.Ship.NetworkObjectId);
        }

        
        // === Station position lookup ===

        private Vector3? ResolveStationWorldPos(string locationId)
        {
            var station = ProjectC.Docking.Network.DockingZoneRegistry.GetByLocation(locationId);
            if (station == null) return null;
            return station.transform.position;
        }

        /// <summary>
        /// Реальная позиция пада в сцене (из DockingPadTriggerBox). Приоритет: scene > station center.
        /// </summary>
        private Vector3? ResolvePadWorldPos(string locationId, string padId)
        {
            var station = ProjectC.Docking.Network.DockingZoneRegistry.GetByLocation(locationId);
            if (station == null) return null;
            var triggerBoxes = station.GetComponentsInChildren<ProjectC.Docking.Stations.DockingPadTriggerBox>(true);
            for (int i = 0; i < triggerBoxes.Length; i++)
            {
                if (triggerBoxes[i].PadId == padId)
                    return triggerBoxes[i].transform.position;
            }
            return station.transform.position;
        }
    }
}
