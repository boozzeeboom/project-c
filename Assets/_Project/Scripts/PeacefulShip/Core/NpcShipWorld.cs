// T-NS03: NpcShipWorld — server-only state machine для всех NPC-кораблей.
// Полная FSM реализация. Stub (T-NS02) заменён этим файлом.
//
// Pattern: DockingWorld (Docking/Core/DockingWorld.cs:19), QuestWorld (Quests/Core/QuestWorld.cs).
// FSM transition diagram: docs/NPC_others_peacfull/pc_ship/04_LIVING_BEHAVIOR.md §2.

using System.Collections.Generic;
using ProjectC.Core.ShipPosition; // T-PERSIST: ShipPositionSaveData
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
    // === NPC physics flight constants (barge, no pitch/roll) ===
    private const float NPC_CRUISE_ALTITUDE_OFFSET = 100f;  // летим на 100м выше точки старта
    private const float NPC_APPROACH_DISTANCE = 500f;       // начинаем снижение за 500м
    private const float NPC_YAW_GAIN = 0.02f;               // мягкая коррекция курса (было 0.5 → осцилляции)
    private const float NPC_YAW_CLAMP = 0.3f;                // макс yaw input
    private const float NPC_ALT_HOLD_GAIN = 0.05f;           // коррекция высоты

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

                    var state = new NpcShipState(id, ship);
                    // M3.2.1: задать первый route при регистрации. Без этого state.CurrentRoute пустой,
                    // и NavTick.ResolveTargetStation() возвращает null → NPC зависает в Lifting без цели.
                    if (schedule != null && schedule.routes != null && schedule.routes.Length > 0)
                    {
                        state.CurrentRoute = schedule.routes[0];
                        state.ScheduleIndex = 0;
                    }
                    _npcByInstanceId[id] = state;
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

        public NpcShipSchedule GetSchedule(ulong id)
            => _scheduleByNpcInstanceId.TryGetValue(id, out var s) ? s : null;

        public int AllNpcCount => _npcByInstanceId.Count;

        // === T-PERSIST: RestoreNpcState ===

        /// <summary>T-PERSIST: восстановить FSM-состояние NPC из сохранённых данных.</summary>
        public void RestoreNpcState(ulong npcInstanceId, ShipPositionSaveData data)
        {
            if (!_npcByInstanceId.TryGetValue(npcInstanceId, out var state)) return;
            if (!_scheduleByNpcInstanceId.TryGetValue(npcInstanceId, out var schedule)) return;

            state.ScheduleIndex = data.scheduleIndex;
            state.LastKnownPosition = new Vector3(data.px, data.py, data.pz);
            state.StateEnteredAt = Time.time; // сервер перезапущен — таймер состояния сброшен

            // Восстановить CurrentRoute из schedule по сохранённому индексу
            if (schedule.routes != null && schedule.routes.Length > 0
                && data.scheduleIndex >= 0 && data.scheduleIndex < schedule.routes.Length)
            {
                state.CurrentRoute = schedule.routes[data.scheduleIndex];
            }

            Debug.Log($"[NpcShipWorld] RestoreNpcState id={npcInstanceId:X} idx={state.ScheduleIndex} " +
                      $"route={state.CurrentRoute.fromLocationId}→{state.CurrentRoute.toLocationId}");
        }

        /// <summary>Read-only iterate all NPCs (for FSM tick, debugging).</summary>
        public IEnumerable<NpcShipState> AllNpcs => _npcByInstanceId.Values;

        // === FSM Tick ===

        /// <summary>
        /// Per-frame FSM tick — вызывается у каждого зарегистрированного NPC.
        /// Документация: docs/NPC_others_peacfull/pc_ship/04_LIVING_BEHAVIOR.md §2.
        /// </summary>
        private void FixedUpdate()
                {
                    if (!ProjectC.Trade.Network.NetworkingUtils.IsServerSafe()) return;

                    float dt = Time.fixedDeltaTime;
                    foreach (var state in _npcByInstanceId.Values)
                    {
                        if (state == null || state.Ship == null) continue;
                        var controller = NpcShipZoneRegistry.Get(state.NpcInstanceId);
                        if (controller == null) continue;

                        // M3.2: только NavTick с прямым Rigidbody control.
                        // ShipController.FixedUpdate пропускает физику если _hasNpcPilot && _pilots.Count==0.
                        controller.NavTick(dt);
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
                    float cruiseTarget = startY + NPC_CRUISE_ALTITUDE_OFFSET;
                    if (state.Ship.transform.position.y >= cruiseTarget - 5f || timeInState > 20f)
                    {
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
            // Pure vertical climb via physics: full vertical thrust, no forward/yaw
            controller.ApplyMovementInput(thrust: 0f, yaw: 0f, pitch: 0f, vertical: 1.0f);
        }

        private void ApplyTransitMovement(NpcShipState state, NpcShipController controller)
        {
            var targetPos = ResolveStationWorldPos(state.CurrentRoute.toLocationId);
            if (!targetPos.HasValue) return;
            var ship = state.Ship;
            
            // Determine bearing to target
            Vector3 dir = (targetPos.Value - ship.transform.position).normalized;
            float bearing = CalcBearing(ship.transform.position, targetPos.Value) - ship.transform.eulerAngles.y;
            bearing = Mathf.DeltaAngle(0f, bearing);
            
            // Gentle yaw correction (reduce gain to prevent oscillation)
            float yawInput = Mathf.Clamp(bearing * NPC_YAW_GAIN, -NPC_YAW_CLAMP, NPC_YAW_CLAMP);
            
            // Altitude hold: maintain cruise altitude
            float startY = state.StateEnteredAt > 0 ? state.LastKnownPosition.y : ship.transform.position.y;
            float cruiseAlt = startY + NPC_CRUISE_ALTITUDE_OFFSET;
            float altError = cruiseAlt - ship.transform.position.y;
            float verticalInput = Mathf.Clamp(altError * NPC_ALT_HOLD_GAIN, -0.3f, 0.3f);
            
            // Gentle forward thrust
            float thrustInput = Mathf.Clamp01(Mathf.Abs(UnityEngine.Vector3.Dot(dir, ship.transform.forward)));
            
            controller.ApplyMovementInput(
                thrust: thrustInput * 1.2f,
                yaw: yawInput,
                pitch: 0f,
                vertical: verticalInput
            );
        }

        private void ApplyApproachMovement(NpcShipState state, NpcShipController controller)
        {
            var targetPos = ResolveStationWorldPos(state.CurrentRoute.toLocationId);
            if (!targetPos.HasValue) return;
            var ship = state.Ship;
            float dist = UnityEngine.Vector3.Distance(ship.transform.position, targetPos.Value);
            
            // Bearing to target
            Vector3 dir = (targetPos.Value - ship.transform.position).normalized;
            float bearing = CalcBearing(ship.transform.position, targetPos.Value) - ship.transform.eulerAngles.y;
            bearing = Mathf.DeltaAngle(0f, bearing);
            float yawInput = Mathf.Clamp(bearing * NPC_YAW_GAIN, -NPC_YAW_CLAMP, NPC_YAW_CLAMP);
            
            // Slow down + descend to station altitude + 5m
            float thrustInput = Mathf.Clamp01(dist / NPC_APPROACH_DISTANCE) * 0.8f;
            float targetAlt = targetPos.Value.y + 5f;
            float altError = targetAlt - ship.transform.position.y;
            float verticalInput = Mathf.Clamp(altError * NPC_ALT_HOLD_GAIN * 2f, -0.5f, 0.5f);
            
            controller.ApplyMovementInput(
                thrust: thrustInput,
                yaw: yawInput,
                pitch: 0f,
                vertical: verticalInput
            );
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
            // T-NS05: DockingWorld.Instance.AssignPadForNpc(...)
            if (state.Ship == null || state.Ship.IsDocked) return false;

            // Stub check: if within docking tolerance, claim the pad via DockingWorld
            var targetPos = ResolveStationWorldPos(state.CurrentRoute.toLocationId);
            if (!targetPos.HasValue) return false;
            float dist = Vector3.Distance(state.Ship.transform.position, targetPos.Value);
            if (dist > 200f) return false; // not close enough

            // Call into DockingWorld (server-side only)
            if (Docking.Core.DockingWorld.Instance == null) return false;
            var station = Docking.Network.DockingZoneRegistry.GetByLocation(state.CurrentRoute.toLocationId);
            if (station == null) return false;

            // AssignPadForNpc проверяет maxConcurrentLandings, sentinel id
            return Docking.Core.DockingWorld.Instance.AssignPadForNpc(
                            station, state.Ship, state.Ship.ShipFlightClass, state.NpcInstanceId) != null;
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
            if (string.IsNullOrEmpty(locationId)) return null;
            var station = Docking.Network.DockingZoneRegistry.GetByLocation(locationId);
            if (station == null) return null;
            return station.transform.position;
        }
    }
}