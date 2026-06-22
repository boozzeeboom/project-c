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
                    // If we're still docked, release the pad assignment first
                    if (state.Ship != null && state.Ship.IsDocked)
                    {
                        ReleaseNpcAssignment(state);
                        controller.StartAntiGravityBoost();
                    }
                    // Move toward target station with positive vertical
                    ApplyDepartingMovement(state, controller);
                    // After 3 sec (climb time) — switch to InTransit
                    if (timeInState > 3f)
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
                    // Try to assign a pad (delegates to DockingWorld.AssignPadForNpc — T-NS05)
                    if (TryAssignPadForNpc(state))
                    {
                        TransitionTo(state, NpcShipStatus.Docking);
                        Debug.Log($"[NpcShipWorld:NPC] id={state.NpcInstanceId:X} Approaching→Docking");
                    }
                    else if (timeInState > 30f)
                    {
                        // Timeout — divert to next station
                        Debug.LogWarning($"[NpcShipWorld:NPC] id={state.NpcInstanceId:X} Approaching timeout → Diverting");
                        TransitionTo(state, NpcShipStatus.Diverting);
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
            // Climb vertically + slight forward thrust
            controller.ApplyMovementInput(thrust: 0.4f, yaw: 0f, pitch: 0.2f, vertical: 0.6f);
        }

        private void ApplyTransitMovement(NpcShipState state, NpcShipController controller)
        {
            var targetPos = ResolveStationWorldPos(state.CurrentRoute.toLocationId);
            if (!targetPos.HasValue) return;
            var ship = state.Ship;
            Vector3 dir = (targetPos.Value - ship.transform.position).normalized;
            float bearing = CalcBearing(ship.transform.position, targetPos.Value) - ship.transform.eulerAngles.y;
            bearing = Mathf.DeltaAngle(0f, bearing);
            controller.ApplyMovementInput(
                thrust: 0.6f,
                yaw: Mathf.Clamp(bearing * 0.5f, -1f, 1f),
                pitch: 0f,
                vertical: 0.2f * dir.y // maintain altitude
            );
        }

        private void ApplyApproachMovement(NpcShipState state, NpcShipController controller)
        {
            var targetPos = ResolveStationWorldPos(state.CurrentRoute.toLocationId);
            if (!targetPos.HasValue) return;
            var ship = state.Ship;
            float dist = Vector3.Distance(ship.transform.position, targetPos.Value);
            float bearing = CalcBearing(ship.transform.position, targetPos.Value) - ship.transform.eulerAngles.y;
            bearing = Mathf.DeltaAngle(0f, bearing);
            // Slow down + descend
            float thrust = Mathf.Clamp01(dist / 200f) * 0.4f;
            controller.ApplyMovementInput(
                thrust: thrust,
                yaw: Mathf.Clamp(bearing * 0.5f, -0.5f, 0.5f),
                pitch: -0.1f, // descent
                vertical: -0.3f
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
            switch (schedule.scheduleType)
            {
                case NpcShipSchedule.ScheduleType.RoundTrip:
                    // 0 → 1 → 0 → 1
                    state.ScheduleIndex = (state.ScheduleIndex + 1) % 2;
                    break;
                case NpcShipSchedule.ScheduleType.Loop:
                    // 0 → 1 → 2 → 0
                    state.ScheduleIndex = (state.ScheduleIndex + 1) % schedule.routes.Length;
                    break;
                case NpcShipSchedule.ScheduleType.RandomFromPool:
                    state.ScheduleIndex = Random.Range(0, schedule.routes.Length);
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
                station, state.Ship, state.Ship.ShipFlightClass, state.NpcInstanceId);
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