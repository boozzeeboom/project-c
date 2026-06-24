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

        // Throttle pad assignment attempts (avoid per-frame spam)
        private readonly Dictionary<ulong, float> _lastPadAttempt = new Dictionary<ulong, float>();

        // === M2: Barge movement constants (all universal — ship speed independent) ===

        /// <summary>Высота подъёма от пада при Departing (vertical only).</summary>
        private const float PAD_CLEAR_HEIGHT = 5f;

        /// <summary>±градусов: внутри dead-zone → thrust+vertical; снаружи → yaw only.</summary>
        private const float YAW_DEAD_ZONE = 15f;

        /// <summary>Гистерезис: не выходим из yaw-фазы пока |bearing| < deadzone — hysteresis.</summary>
        private const float YAW_HYSTERESIS = 5f;

        /// <summary>Интервал между проверками курса (сек).</summary>
        private const float COURSE_RECHECK_INTERVAL = 5f;

        /// <summary>Если bearing при course-check > limit → stop thrust+vertical, yaw на месте.</summary>
        private const float BEARING_DRIFT_LIMIT = 30f;

        /// <summary>Горизонтальное расстояние до пада для переключения с диагонали на pure vertical descent.</summary>
        private const float APPROACH_HORIZ_DIST_THRESHOLD = 10f;

        /// <summary>Редирект на новое назначение — задержка (smooth arrival).</summary>
        private const float APPROACH_THRUST_SCALE = 50f;

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
                var controller = NpcShipZoneRegistry.Get(state.NpcInstanceId);
                if (controller == null) continue;

                // M3.1+: синхронизировать state в controller (NavTick читает отсюда)
                controller.CurrentRoute = state.CurrentRoute;
                controller.AssignedPadId = state.AssignedPadId;

                // M3.1+: новая NavTick работает параллельно со старой TickNpc.
                // Решение о переходе в NavTick-only — в M3.3 (когда старая логика выпилена).
                if (controller.useNewNavTick)
                {
                    controller.NavTick(dt);
                }

                // Старая логика остаётся активной пока не отключим в M3.3.
                // Чтобы не было двойного движения — отключаем старую если NavTick активен.
                if (!controller.useNewNavTick)
                {
                    TickNpc(state, dt);
                }
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
                    if (state.Ship != null && state.Ship.IsDocked)
                    {
                        ReleaseNpcAssignment(state);
                        controller.StartAntiGravityBoost();
                    }

                    // M2: record start position для диагонального расчёта на first-frame
                    if (state.StartPathPos == Vector3.zero)
                        state.StartPathPos = state.Ship.transform.position;

                    // LIFT ONLY — подъём на PAD_CLEAR_HEIGHT вертикально вверх
                    float liftTargetY = state.StartPathPos.y + PAD_CLEAR_HEIGHT;
                    float verticalCmd = ApplyAltitudeControl(state.Ship, liftTargetY);
                    controller.ApplyMovementInput(thrust: 0f, yaw: 0f, pitch: 0f, vertical: verticalCmd);

                    // Достигли чистой высоты → InTransit (там yaw + diagonal)
                    if (state.Ship.transform.position.y >= liftTargetY - 0.5f)
                    {
                        KillVerticalVelocity(state.Ship);
                        TransitionTo(state, NpcShipStatus.InTransit);
                        Debug.Log($"[NpcShipWorld:NPC] id={state.NpcInstanceId:X} Departing→InTransit (cleared pad, heading to {state.CurrentRoute.toLocationId})");
                    }
                    break;

                case NpcShipStatus.InTransit:
                    ApplyTransitMovement(state, controller);
                    // Detect arrival: вошли в OuterCommZone (коммуникационная зона порта)
                    var station = Docking.Network.DockingZoneRegistry.GetByLocation(state.CurrentRoute.toLocationId);
                    if (station != null)
                    {
                        var outerZone = station.GetComponentInChildren<ProjectC.Docking.Zones.OuterCommZone>(true);
                        if (outerZone != null && outerZone.CommRange > 0f)
                        {
                            float dist = Vector3.Distance(state.Ship.transform.position, outerZone.transform.position);
                            if (dist < outerZone.CommRange)
                            {
                                KillVerticalVelocity(state.Ship);
                                TransitionTo(state, NpcShipStatus.Approaching);
                                Debug.Log($"[NpcShipWorld:NPC] id={state.NpcInstanceId:X} InTransit→Approaching (entered OuterCommZone of {state.CurrentRoute.toLocationId}, dist={dist:F0}m)");
                            }
                        }
                        else
                        {
                            // Fallback: distance to station center
                            float dist = Vector3.Distance(state.Ship.transform.position, station.transform.position);
                            if (dist < 500f)
                            {
                                KillVerticalVelocity(state.Ship);
                                TransitionTo(state, NpcShipStatus.Approaching);
                                Debug.Log($"[NpcShipWorld:NPC] id={state.NpcInstanceId:X} InTransit→Approaching (no OuterCommZone, dist={dist:F0}m)");
                            }
                        }
                    }
                    break;

                case NpcShipStatus.Approaching:
                    // M2: hover → yaw to pad → diagonal → vertical descent
                    ApplyApproachMovement(state, controller);
                    // Try to assign a pad (throttled — not every frame!)
                    if (timeInState < 2f ||
                        (_lastPadAttempt.ContainsKey(state.NpcInstanceId) &&
                         Time.time - _lastPadAttempt[state.NpcInstanceId] < 2f))
                    {
                        // Skip this frame (throttle at 2s)
                    }
                    else
                    {
                        _lastPadAttempt[state.NpcInstanceId] = Time.time;
                        TryAssignPadForNpc(state);
                    }

                    // Pad-based touchdown detection (IsShipInside via OnTriggerEnter)
                    if (!string.IsNullOrEmpty(state.AssignedPadId))
                    {
                        var destStation = Docking.Network.DockingZoneRegistry.GetByLocation(state.CurrentRoute.toLocationId);
                        if (destStation != null)
                        {
                            foreach (var tb in destStation.GetComponentsInChildren<ProjectC.Docking.Stations.DockingPadTriggerBox>(true))
                            {
                                if (tb.PadId == state.AssignedPadId && tb.IsShipInside && !state.Ship.IsDocked)
                                {
                                    state.Ship.EnterDocked();
                                    TransitionTo(state, NpcShipStatus.Docking);
                                    Debug.Log($"[NpcShipWorld:NPC] id={state.NpcInstanceId:X} Approaching→Docking (trigger entered pad={state.AssignedPadId})");
                                    break;
                                }
                            }
                        }
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
                        state.AssignedPadId = null; // сброс на следующий цикл
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

        // === Movement helpers (M2 Barge 2.0) ===

        /// <summary>
        /// M2: Diagonal flight with course correction.
        /// YAW ONLY when not aligned (never with thrust or vertical).
        /// THRUST + VERTICAL when aligned (diagonal toward target).
        /// Periodic course re-check every COURSE_RECHECK_INTERVAL
        /// — only while thrusting (bearing < YAW_DEAD_ZONE).
        /// </summary>
        private void ApplyTransitMovement(NpcShipState state, NpcShipController controller)
        {
            var targetPos = ResolveStationWorldPos(state.CurrentRoute.toLocationId);
            if (!targetPos.HasValue) return;
            var ship = state.Ship;
            Vector3 pos = ship.transform.position;

            // Step 1: On first entry (FlightDirection == Vector3.zero), compute initial course
            if (state.FlightDirection == Vector3.zero)
            {
                Vector3 dir = (targetPos.Value - state.StartPathPos);
                if (dir.sqrMagnitude < 0.01f)
                    dir = (targetPos.Value - pos).normalized;
                state.FlightDirection = dir.normalized;
                state.LastCourseCheckTime = Time.time;
            }

            // Step 2: Compute bearing to current FlightDirection
            float targetBearing = Mathf.Atan2(state.FlightDirection.x, state.FlightDirection.z) * Mathf.Rad2Deg;
            float currentBearing = ship.transform.eulerAngles.y;
            float bearing = Mathf.DeltaAngle(currentBearing, targetBearing);

            // Step 3: YAW OR diagonal — NEVER together
            if (Mathf.Abs(bearing) > YAW_DEAD_ZONE)
            {
                // YAW ONLY on the spot (no thrust, maintain altitude with PD-controller)
                float yawInput = Mathf.Clamp(bearing * 0.5f, -1f, 1f);
                float hoverVertical = ApplyAltitudeControl(ship, pos.y);
                controller.ApplyMovementInput(thrust: 0f, yaw: yawInput, pitch: 0f, vertical: hoverVertical);
                // R2-007 debug: verify yaw is being called every frame
                if (Time.frameCount % 120 == 0)
                    Debug.Log($"[NpcShipWorld:NPC] id={state.NpcInstanceId:X} yaw phase bearing={bearing:F1}° yawIn={yawInput:F2} vertIn={hoverVertical:F2} posY={pos.y:F1}");
            }
            else
            {
                // DIAGONAL: thrust + vertical simultaneously, following A→B line
                // Step 2b: Periodic course correction — only while thrusting
                if (Time.time - state.LastCourseCheckTime > COURSE_RECHECK_INTERVAL)
                {
                    state.LastCourseCheckTime = Time.time;
                    Vector3 idealDir = (targetPos.Value - pos).normalized;
                    float idealBearing = Mathf.Atan2(idealDir.x, idealDir.z) * Mathf.Rad2Deg;
                    float bearingDiff = Mathf.DeltaAngle(currentBearing, idealBearing);

                    if (Mathf.Abs(bearingDiff) > BEARING_DRIFT_LIMIT)
                    {
                        // Off course: stop thrust, yaw in place
                        state.FlightDirection = idealDir;
                        Debug.Log($"[NpcShipWorld:NPC] id={state.NpcInstanceId:X} course correction: bearingDiff={bearingDiff:F1}°, recalculating");

                        // Fall through to still apply diagonal this frame
                        // (will switch to yaw-only next Tick on corrected FlightDirection)
                    }
                }

                float dist = Vector3.Distance(pos, targetPos.Value);
                float thrust = Mathf.Clamp01(dist / APPROACH_THRUST_SCALE) * 0.8f;

                // Diagonal Y: lerp from start height to target height based on progress
                float totalDist = Vector3.Distance(state.StartPathPos, targetPos.Value);
                float progress = totalDist > 0.01f ? 1f - (dist / totalDist) : 0f;
                float diagonalTargetY = Mathf.Lerp(state.StartPathPos.y, targetPos.Value.y, progress);

                float vertical = ApplyAltitudeControl(ship, diagonalTargetY);
                controller.ApplyMovementInput(thrust: thrust, yaw: 0f, pitch: 0f, vertical: vertical);
            }
        }

        /// <summary>
        /// M2: Approach sequence — hover → yaw to pad → diagonal → vertical descent.
        /// </summary>
        private void ApplyApproachMovement(NpcShipState state, NpcShipController controller)
        {
            Vector3? targetPos;
            if (!string.IsNullOrEmpty(state.AssignedPadId))
                targetPos = ResolvePadWorldPos(state.CurrentRoute.toLocationId, state.AssignedPadId);
            else
                targetPos = ResolveStationWorldPos(state.CurrentRoute.toLocationId);
            if (!targetPos.HasValue) return;
            var ship = state.Ship;
            Vector3 pos = ship.transform.position;
            Vector3 target = targetPos.Value;

            float horizDist = Vector3.Distance(
                new Vector3(pos.x, 0f, pos.z),
                new Vector3(target.x, 0f, target.z));

            // Phase 1: NO pad assigned — HOVER (all inputs zero, anti-grav holds)
            if (string.IsNullOrEmpty(state.AssignedPadId))
            {
                controller.ApplyMovementInput(thrust: 0f, yaw: 0f, pitch: 0f, vertical: 0f);
                return;
            }

            // Bearing to pad center
            float bearing = Mathf.Atan2(target.x - pos.x, target.z - pos.z) * Mathf.Rad2Deg - ship.transform.eulerAngles.y;
            bearing = Mathf.DeltaAngle(0f, bearing);

            // Phase 2: YAW ONLY toward pad (no thrust, no vertical)
            if (horizDist > APPROACH_HORIZ_DIST_THRESHOLD && Mathf.Abs(bearing) > YAW_DEAD_ZONE)
            {
                float yawInput = Mathf.Clamp(bearing * 0.5f, -1f, 1f);
                controller.ApplyMovementInput(thrust: 0f, yaw: yawInput, pitch: 0f, vertical: 0f);
                return;
            }

            // Phase 3: DIAGONAL thrust+vertical toward pad (when aligned)
            if (horizDist > APPROACH_HORIZ_DIST_THRESHOLD)
            {
                float thrust = Mathf.Clamp01(horizDist / APPROACH_THRUST_SCALE) * 0.4f;

                // Diagonal: lerp from station height to pad height
                float diagonalTargetY = Mathf.Lerp(state.StartPathPos.y, target.y + 1f, 0.5f);
                float vertical = ApplyAltitudeControl(ship, diagonalTargetY);
                controller.ApplyMovementInput(thrust: thrust, yaw: 0f, pitch: 0f, vertical: vertical);
                return;
            }

            // Phase 4: VERTICAL DESCENT (close to pad: horizDist < threshold)
            float verticalCmd = ApplyAltitudeControl(ship, target.y);
            controller.ApplyMovementInput(thrust: 0f, yaw: 0f, pitch: 0f, vertical: verticalCmd);
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

            // M2: сброс направления полёта при любом входе в InTransit
            if (newStatus == NpcShipStatus.InTransit)
                state.FlightDirection = Vector3.zero;
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
            if (state.Status == NpcShipStatus.Docked) return false;
            if (!string.IsNullOrEmpty(state.AssignedPadId)) return true; // уже назначен

            var station = Docking.Network.DockingZoneRegistry.GetByLocation(state.CurrentRoute.toLocationId);
            if (station == null) return false;
            if (Docking.Core.DockingWorld.Instance == null) return false;

            // Просто запросить пад без distance check — DockingWorld раздаёт свободные
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
            if (string.IsNullOrEmpty(locationId)) return null;
            var station = Docking.Network.DockingZoneRegistry.GetByLocation(locationId);
            if (station == null) return null;
            return station.transform.position;
        }

        private Vector3? ResolvePadWorldPos(string locationId, string padId)
        {
            if (string.IsNullOrEmpty(locationId) || string.IsNullOrEmpty(padId)) return null;
            var station = Docking.Network.DockingZoneRegistry.GetByLocation(locationId);
            if (station == null) return null;
            var triggerBoxes = station.GetComponentsInChildren<ProjectC.Docking.Stations.DockingPadTriggerBox>(true);
            for (int i = 0; i < triggerBoxes.Length; i++)
            {
                if (triggerBoxes[i].PadId == padId)
                    return triggerBoxes[i].transform.position;
            }
            return station.transform.position;
        }

        private Vector3? ResolveClosestPadWorldPos(string locationId)
        {
            var station = Docking.Network.DockingZoneRegistry.GetByLocation(locationId);
            if (station == null) return null;
            var triggerBoxes = station.GetComponentsInChildren<ProjectC.Docking.Stations.DockingPadTriggerBox>(true);
            if (triggerBoxes == null || triggerBoxes.Length == 0)
                return station.transform.position;
            return triggerBoxes[0].transform.position;
        }

        // === Altitude control (AntiGravity PD-controller) ===
        // Универсальный — НЕ зависит от силы двигателя. Управляет ship.AntiGravity напрямую
        // (анти-гравитация компенсирует вес, чтобы корабль завис/поднялся/опустился к targetY).
        // Возвращает vertical input (только для случая когда надо лететь вниз быстрее чем AG).
        private const float AG_BASE = 1.0f;        // полная компенсация веса
        private const float AG_KP = 0.05f;         // реакция на ошибку высоты
        private const float AG_KD = 0.4f;          // демпфирование вертикальной скорости
        private const float AG_MIN = 0.3f;         // нижний предел (иначе свалится)
        private const float AG_MAX = 1.5f;         // верхний предел (как у boost)

        /// <summary>
        /// Применяет PD-контроль высоты к ship.AntiGravity.
        /// Возвращает vertical input для дополнительного спуска когда AG_Min недостаточно.
        /// </summary>
        private float ApplyAltitudeControl(ShipController ship, float targetY)
        {
            if (ship == null) return 0f;
            var rb = ship.GetComponent<Rigidbody>();
            float currentY = ship.transform.position.y;
            float error = targetY - currentY; // >0 = нужно вверх, <0 = вниз
            float vertVel = rb != null ? rb.linearVelocity.y : 0f;

            // PD на AntiGravity: base + error*kp - vel*kd
            float deltaAG = error * AG_KP - vertVel * AG_KD;
            float newAG = Mathf.Clamp(AG_BASE + deltaAG, AG_MIN, AG_MAX);
            ship.AntiGravity = newAG;

            // Если target сильно ниже (нужен быстрый спуск) — добавляем vertical input
            // AG_Min=0.3 всё ещё даёт ~70% веса вниз, этого мало если нужно экстренно вниз
            if (error < -3f)
            {
                // Маппим ошибку: -3м → 0, -20м → -1.0
                float vertical = Mathf.Clamp(error * 0.05f, -1f, 0f);
                return vertical;
            }
            return 0f;
        }

        // Сбросить вертикальную velocity (anti-windup при смене state)
        private void KillVerticalVelocity(ShipController ship)
        {
            if (ship == null) return;
            var rb = ship.GetComponent<Rigidbody>();
            if (rb == null) return;
            var v = rb.linearVelocity;
            v.y = 0f;
            rb.linearVelocity = v;
        }
    }
}