// T-NS02: NpcShipController — scene-placed NetworkBehaviour на корне NPC-корабля.
// Pattern: ShipController (Player/ShipController.cs), DockStationController (Docking/Network/).
//
// Требует на корне GameObject:
//   - NetworkObject (auto-spawn через ScenePlacedObjectSpawner)
//   - ShipController (для EnterDocked/ExitDocked + ApplyServerInput)
//   - Rigidbody (RequireComponent у ShipController)
//
// Сервер-only:
//   - OnNetworkSpawn → EnableNpcPilot(true), NpcShipZoneRegistry.Register, NpcShipWorld.RegisterNpc
//   - OnNetworkDespawn → EnableNpcPilot(false), Unregister
//   - ApplyMovementInput, ServerTeleport, AntiGravityBoostAfterExitDocked
//
// Q3: NpcInstanceId = NetworkObjectId | 0x8000_0000_0000_0000UL (sentinel bit)
// Q8: anti-gravity boost на 5 сек после ExitDocked (см. docs/.../04_LIVING_BEHAVIOR.md §2.3)

using System.Collections;
using ProjectC.Docking.Stations;
using ProjectC.Docking.Zones;
using ProjectC.PeacefulShip.Core;
using ProjectC.PeacefulShip.Network;
using ProjectC.Player;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.PeacefulShip.Stations
{
    /// <summary>
    /// Scene-placed контроллер мирного NPC-корабля.
    /// Автоматически спавнится через ScenePlacedObjectSpawner (как DockStation).
    /// См. docs/NPC_others_peacfull/pc_ship/03_V2_ARCHITECTURE.md §2.5.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(ShipController))]
    public class NpcShipController : NetworkBehaviour
    {
        [Header("Schedule (SO)")]
        [Tooltip("NpcShipSchedule с маршрутами и параметрами Gaussian shaping. " +
                 "Создаётся через Assets > Create > ProjectC > PeacefulShip > NpcShipSchedule.")]
        [SerializeField] private NpcShipSchedule schedule;

        [Header("Identity (Q3)")]
        [Tooltip("Если 0 — генерируется автоматически из NetworkObjectId | 0x8000...UL. " +
                 "Для стабильности между scene reloads рекомендуется оставить 0.")]
        [SerializeField] private ulong npcInstanceId = 0;

        [Header("Movement (server-only)")]
        [Tooltip("Множитель тяги для NPC (обычно < 1.0 — NPC летит медленнее игрока).")]
        [Range(0.1f, 1.5f)] [SerializeField] private float npcThrustMult = 0.6f;

        [Tooltip("Множитель рыскания для NPC (более плавные повороты).")]
        [Range(0.1f, 1.5f)] [SerializeField] private float npcYawMult = 0.4f;

        [Tooltip("Дистанция до цели (м), при которой считаем что прибыли.")]
#pragma warning disable 0414  // used in T-NS03 via NpcShipWorld (future refactor — direct read)
        [Min(1f)] [SerializeField] private float npcArrivalToleranceMeters = 50f;
#pragma warning restore 0414

        [Header("Anti-gravity boost (Q8)")]
        [Tooltip("Длительность boost после ExitDocked (сек). 0 = отключить.")]
        [Min(0f)] [SerializeField] private float antiGravityBoostDuration = 5f;

        [Tooltip("Значение AntiGravity во время boost (1.0 = норма, 1.5 = полная компенсация).")]
        [Range(0f, 1.5f)] [SerializeField] private float antiGravityBoostValue = 1.5f;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // === Public API ===
        public NpcShipSchedule Schedule => schedule;
        public ulong NpcInstanceId => npcInstanceId;
        public ShipController Ship => GetComponent<ShipController>();

        // === Private ===
        private Coroutine _antiGravityRoutine;

        // === Lifecycle ===

        private void Awake()
        {
            if (schedule == null)
            {
                Debug.LogError($"[NpcShipController:{gameObject.name}] schedule is null! " +
                               "Назначь NpcShipSchedule SO в инспекторе.", this);
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer)
            {
                // Клиенты не нуждаются в логике NPC-pilot
                enabled = false;
                return;
            }

            // Q3: sentinel id generation — если не задан вручную
            if (npcInstanceId == 0)
            {
                npcInstanceId = NetworkObjectId | 0x8000_0000_0000_0000UL;
                if (debugMode)
                    Debug.Log($"[NpcShipController:{gameObject.name}] Generated NpcInstanceId = {npcInstanceId} (NetworkObjectId={NetworkObjectId})");
            }

            // Q2: enable NPC-pilot на ShipController — теперь FixedUpdate применяет вход
            var ship = GetComponent<ShipController>();
            if (ship != null)
            {
                ship.EnableNpcPilot(true);
            }
            else
            {
                Debug.LogError($"[NpcShipController:{gameObject.name}] no ShipController on root!", this);
            }

            // Регистрация в локальном registry (нужно для DockingWorld.AssignPadForNpc)
            NpcShipZoneRegistry.Register(this);

            // Регистрация в server-side state machine (NpcShipWorld создаётся позже в T-NS03,
            // здесь только ленивая регистрация — OnNetworkSpawn может произойти раньше CreateAndInitialize)
            if (NpcShipWorld.Instance != null)
            {
                NpcShipWorld.Instance.RegisterNpc(npcInstanceId, ship, schedule);
            }

            if (debugMode)
                Debug.Log($"[NpcShipController:{gameObject.name}] OnNetworkSpawn — registered (id={npcInstanceId})");
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (IsServer)
            {
                // Stop anti-gravity routine если запущена
                if (_antiGravityRoutine != null)
                {
                    StopCoroutine(_antiGravityRoutine);
                    _antiGravityRoutine = null;
                }

                // Q2: disable NPC-pilot
                var ship = GetComponent<ShipController>();
                if (ship != null)
                {
                    ship.EnableNpcPilot(false);
                    ship.AntiGravity = 1f; // restore default
                }

                NpcShipZoneRegistry.Unregister(this);

                if (NpcShipWorld.Instance != null)
                {
                    NpcShipWorld.Instance.UnregisterNpc(npcInstanceId);
                }

                if (debugMode)
                    Debug.Log($"[NpcShipController:{gameObject.name}] OnNetworkDespawn — unregistered");
            }
        }

        // === Movement API (server-only) ===

        /// <summary>
        /// Применить движение к ShipController через новый ApplyServerInput (T-NS01).
        /// Вызывается из NpcShipWorld.TickNpc FSM.
        /// </summary>
        public void ApplyMovementInput(float thrust, float yaw, float pitch, float vertical)
        {
            if (!IsServer) return;
            var ship = GetComponent<ShipController>();
            if (ship == null) return;
            ship.ApplyServerInput(
                thrust * npcThrustMult,
                yaw * npcYawMult,
                pitch * npcThrustMult,
                vertical * npcThrustMult
            );
        }

        /// <summary>
        /// Server-only snap к позиции (для финального позиционирования на pad).
        /// В M1 используется редко — обычно NPC долетает через ApplyMovementInput.
        /// </summary>
        public void ServerTeleport(Vector3 worldPos, Quaternion worldRot)
        {
            if (!IsServer) return;
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = worldPos;
                rb.rotation = worldRot;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// Q8: Запускает anti-gravity boost на N сек.
        /// Вызывается после ExitDocked (Docked → Undocking → Departing).
        /// Предотвращает "падение" корабля пока NPC-pilot не подаст thrust.
        /// </summary>
        public void StartAntiGravityBoost()
        {
            if (!IsServer) return;
            if (antiGravityBoostDuration <= 0f) return;
            if (_antiGravityRoutine != null) StopCoroutine(_antiGravityRoutine);
            _antiGravityRoutine = StartCoroutine(AntiGravityBoostRoutine());
        }

        private IEnumerator AntiGravityBoostRoutine()
        {
            var ship = GetComponent<ShipController>();
            if (ship == null) yield break;

            float originalAntiGrav = ship.AntiGravity;
            ship.AntiGravity = antiGravityBoostValue;
            if (debugMode)
                Debug.Log($"[NpcShipController:{gameObject.name}] AntiGravity boost START ({antiGravityBoostDuration}s, value={antiGravityBoostValue})");

            yield return new WaitForSeconds(antiGravityBoostDuration);

            // Restore только если корабль ещё под NPC-pilot (не перехвачен игроком)
            if (ship != null && NpcShipZoneRegistry.Get(npcInstanceId) == this)
            {
                ship.AntiGravity = originalAntiGrav;
                if (debugMode)
                    Debug.Log($"[NpcShipController:{gameObject.name}] AntiGravity boost END (restored to {originalAntiGrav})");
            }

            _antiGravityRoutine = null;
        }


        // === M3.2: NavTick — прямой Rigidbody control (минуя ShipController.ApplyServerInput) ===
        // Корневая причина переделки: ApplyServerInput -> SmoothDamp -> AddTorque(ForceMode.Force)
        // с mass=2000, inertiaTensor~50000 даёт angular accel ~0.02°/с² → NPC не вращаются.
        // Решение: MoveRotation (прямое вращение за кадр) + linearVelocity assignment.

        public NavMode CurrentMode { get; private set; } = NavMode.Docked;
        public float LiftStartY { get; set; }
        public Vector3 CruiseTargetPos { get; set; }
        public float LiftSpeed = 8f;       // m/s
        public float CruiseSpeed = 12f;     // m/s
        public float ApproachSpeed = 5f;    // m/s возле станции
        public float MaxYawRate = 45f;     // deg/s — ПРЯМОЙ angular velocity
        public float DwellTime = 5f;      // s — время на паде перед стартом (5s для теста, потом route.dwellTimeSec)
        public float DockedSinceTime { get; private set; } = -1000f;
        private bool _scheduleAdvancedAfterDock = true; // true = первый Docked не двигаем schedule
        public string AssignedPadId { get; set; }
        public bool useNewNavTick = true;

        public enum NavMode : byte { Docked, Lifting, Yawing, Cruising, Berthing }

        /// <summary>Вызывается из NpcShipWorld.Update каждый FixedUpdate.</summary>
        public void NavTick(float dt) {
            if (!IsServer || !useNewNavTick) return;
            var ship = GetComponent<ShipController>();
            if (ship == null) return;
            // M3.2.10: guard — если спавн тайминг или IsDocked до NavMode, синхронизируем.
            // НО НЕ делаем return — Docked handler должен дойти до dwell-check.
            if (ship.IsDocked && CurrentMode != NavMode.Docked) {
                SetMode(NavMode.Docked);
                return;
            }
            var rb = GetComponent<Rigidbody>();
            if (rb == null) return;

            // M3.2.6: isKinematic guard — пропускаем только если НЕ в Docked.
            // В Docked mode сами ставим isKinematic; без этого guard выходит до dwell-check.
            if (rb.isKinematic && CurrentMode != NavMode.Docked) return;

            // Dwell logic: после touchdown начать dwell, после dwell -> lift
            if (CurrentMode == NavMode.Docked) {
                if (DockedSinceTime < 0) DockedSinceTime = Time.time;
                // При первом входе в Docked после завершения полёта — advance schedule
                if (DockedSinceTime > 1f && !_scheduleAdvancedAfterDock) {
                    AdvanceScheduleForCurrentNpc();
                    _scheduleAdvancedAfterDock = true;
                }
                if (!rb.isKinematic) rb.isKinematic = true;
                if (Time.time - DockedSinceTime > DwellTime) {
                    LiftStartY = ship.transform.position.y;
                    rb.isKinematic = false;
                    ship.ExitDocked();
                    _scheduleAdvancedAfterDock = false;
                    SetMode(NavMode.Lifting);
                    return;
                }
                return;
            }

            switch (CurrentMode) {
                case NavMode.Lifting: TickLift(rb); break;
                case NavMode.Yawing: TickYaw(rb); break;
                case NavMode.Cruising: TickCruise(rb); break;
                case NavMode.Berthing: TickBerth(rb); break;
            }
        }

        public void SetMode(NavMode m) {
            if (CurrentMode == m) return;
            var old = CurrentMode;
            CurrentMode = m;
            if (m == NavMode.Docked) {
                DockedSinceTime = Time.time;
                if (old == NavMode.Berthing) {
                    _scheduleAdvancedAfterDock = false; // после полёта — advance на след тике
                    AdvanceScheduleForCurrentNpc();
                }
            }
            if (m == NavMode.Lifting) {
                // M3.2.14: освободить старый пад (если был) перед взлётом
                if (Docking.Core.DockingWorld.Instance != null) {
                    var ship = GetComponent<ShipController>();
                    if (ship != null) Docking.Core.DockingWorld.Instance.ReleaseNpcAssignment(npcInstanceId, ship.NetworkObjectId);
                }
                AssignedPadId = null; // новый лег — новый пад
                var rb = GetComponent<Rigidbody>();
                if (rb != null) {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.detectCollisions = false;  // отключить collision detection при взлёте
                    rb.MoveRotation(Quaternion.Euler(0, rb.rotation.eulerAngles.y, 0));
                }
            }
            // Включить обратно при выходе из Lifting
            if (old == NavMode.Lifting && m != NavMode.Lifting) {
                var rb = GetComponent<Rigidbody>();
                if (rb != null) rb.detectCollisions = true;
            }
            Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] NavMode {old} → {m}");
        }

        void TickLift(Rigidbody rb) {
            float targetY = LiftStartY + 5f;
            float dy = targetY - rb.position.y;
            if (dy <= 0.1f) {
                // Достигли высоты → ищем станцию назначения
                var station = ResolveTargetStation();
                if (station.HasValue) {
                    CruiseTargetPos = station.Value;
                    SetMode(NavMode.Yawing);
                } else {
                    // Не нашли станцию — fallback hover
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                return;
            }
            // Прямая velocity вверх
            rb.linearVelocity = new Vector3(0, LiftSpeed, 0);
            rb.angularVelocity = Vector3.zero;
        }

        void TickYaw(Rigidbody rb) {
            if (CruiseTargetPos == Vector3.zero) {
                rb.linearVelocity = Vector3.zero;
                return;
            }
            Vector3 toTarget = CruiseTargetPos - rb.position;
            float targetYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            float currentYaw = rb.rotation.eulerAngles.y;
            float deltaYaw = Mathf.DeltaAngle(currentYaw, targetYaw);

            // Прямое вращение через MoveRotation (МИНУЕТ AddTorque)
            float yawStep = Mathf.Sign(deltaYaw) * Mathf.Min(Mathf.Abs(deltaYaw), MaxYawRate * Time.fixedDeltaTime);
            rb.MoveRotation(Quaternion.AngleAxis(currentYaw + yawStep, Vector3.up));
            rb.linearVelocity = Vector3.zero;

            if (Mathf.Abs(deltaYaw) < 3f) {
                SetMode(NavMode.Cruising);
            }
        }

        void TickCruise(Rigidbody rb) {
            if (CruiseTargetPos == Vector3.zero) {
                rb.linearVelocity = Vector3.zero;
                return;
            }
            Vector3 toTarget = CruiseTargetPos - rb.position;
            float dist = toTarget.magnitude;

            // Проверить: вошли ли в OuterCommZone целевой станции?
            var zone = ResolveCommZone();
            if (zone != null && Vector3.Distance(rb.position, zone.transform.position) < zone.CommRange) {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                SetMode(NavMode.Berthing);
                return;
            }

            // Если уже очень близко к станции — Berthing (fallback без зоны)
            if (dist < 50f) {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                SetMode(NavMode.Berthing);
                return;
            }

            Vector3 dir = toTarget.normalized;
            float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float currentYaw = rb.rotation.eulerAngles.y;
            float deltaYaw = Mathf.DeltaAngle(currentYaw, targetYaw);

            float yawStep = Mathf.Sign(deltaYaw) * Mathf.Min(Mathf.Abs(deltaYaw), MaxYawRate * Time.fixedDeltaTime);
            rb.MoveRotation(Quaternion.AngleAxis(currentYaw + yawStep, Vector3.up));

            float speed = (dist > 200f) ? CruiseSpeed : ApproachSpeed;
            float altHold = (CruiseTargetPos.y + 5f - rb.position.y) * 0.5f;
            rb.linearVelocity = new Vector3(dir.x * speed, Mathf.Clamp(altHold, -2f, 2f), dir.z * speed);
        }

        void TickBerth(Rigidbody rb) {
            // M3.2.13: очистить старый пад (может быть с другого лега/станции)
            if (!string.IsNullOrEmpty(AssignedPadId) && CruiseTargetPos != Vector3.zero) {
                // Проверить что текущий CruiseTargetPos — это пад. Если это центр станции — сбросить.
                float distToPad = Vector3.Distance(rb.position, CruiseTargetPos);
                if (distToPad > 200f) AssignedPadId = null; // слишком далеко — старый пад
            }

            // Если пад не назначен или устарел — запросить у диспетчера
            if (string.IsNullOrEmpty(AssignedPadId)) {
                var padId = TryAssignPadFromDispatcher();
                if (!string.IsNullOrEmpty(padId)) {
                    AssignedPadId = padId;
                    Vector3 padPos = ResolvePadPos();
                    if (padPos != Vector3.zero) {
                        CruiseTargetPos = padPos;
                    }
                } else {
                    rb.linearVelocity = Vector3.zero;
                    return;
                }
            }

            if (CruiseTargetPos == Vector3.zero) {
                rb.linearVelocity = Vector3.zero;
                return;
            }

            Vector3 toTarget = CruiseTargetPos - rb.position;
            float dist = toTarget.magnitude;

            // M3.2.12: проверять дистанцию только до ПАДА (если пад назначен),
            // или до станции (если пада нет). Док только при dist < 1.5f.
            bool canDock = !string.IsNullOrEmpty(AssignedPadId) ? dist < 1.5f : dist < 3f;
            if (canDock) {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                var ship = GetComponent<ShipController>();
                ship.EnterDocked();
                SetMode(NavMode.Docked);
                return;
            }

            Vector3 dir = toTarget.normalized;
            float speed = Mathf.Min(ApproachSpeed, dist * 2f);
            rb.linearVelocity = new Vector3(dir.x * speed, dir.y * speed, dir.z * speed);
            rb.angularVelocity = Vector3.zero;
        }

        Vector3? ResolveTargetStation() {
            var state = NpcShipWorld.Instance?.GetNpc(npcInstanceId);
            if (state == null || string.IsNullOrEmpty(state.CurrentRoute.toLocationId)) return null;
            var station = Docking.Network.DockingZoneRegistry.GetByLocation(state.CurrentRoute.toLocationId);
            if (station == null) return null;
            return station.transform.position;
        }

        OuterCommZone ResolveCommZone() {
            var state = NpcShipWorld.Instance?.GetNpc(npcInstanceId);
            if (state == null || string.IsNullOrEmpty(state.CurrentRoute.toLocationId)) return null;
            var station = Docking.Network.DockingZoneRegistry.GetByLocation(state.CurrentRoute.toLocationId);
            if (station == null) return null;
            return station.GetComponentInChildren<OuterCommZone>(true);
        }

        string TryAssignPadFromDispatcher() {
            var state = NpcShipWorld.Instance?.GetNpc(npcInstanceId);
            if (state == null || string.IsNullOrEmpty(state.CurrentRoute.toLocationId)) return null;
            var station = Docking.Network.DockingZoneRegistry.GetByLocation(state.CurrentRoute.toLocationId);
            if (station == null || Docking.Core.DockingWorld.Instance == null) return null;
            var ship = GetComponent<ShipController>();
            if (ship == null) return null;
            return Docking.Core.DockingWorld.Instance.AssignPadForNpc(
                station, ship, ship.ShipFlightClass, npcInstanceId);
        }

        Vector3 ResolvePadPos() {
            var state = NpcShipWorld.Instance?.GetNpc(npcInstanceId);
            if (state == null || string.IsNullOrEmpty(state.CurrentRoute.toLocationId)) return Vector3.zero;
            var station = Docking.Network.DockingZoneRegistry.GetByLocation(state.CurrentRoute.toLocationId);
            if (station == null || string.IsNullOrEmpty(AssignedPadId)) return Vector3.zero;
            var pads = station.GetComponentsInChildren<DockingPadTriggerBox>(true);
            for (int i = 0; i < pads.Length; i++) {
                if (pads[i].PadId == AssignedPadId) return pads[i].transform.position;
            }
            return Vector3.zero;
        }

        void AdvanceScheduleForCurrentNpc() {
            var state = NpcShipWorld.Instance?.GetNpc(npcInstanceId);
            if (state == null) return;
            var schedule = NpcShipWorld.Instance?.GetSchedule(npcInstanceId);
            if (schedule == null || schedule.routes == null || schedule.routes.Length == 0) return;
            var route = schedule.routes[0];
            state.ScheduleIndex++;
            if (state.ScheduleIndex % 2 == 1) {
                state.CurrentRoute = new ProjectC.PeacefulShip.Core.NpcShipRoute {
                    fromLocationId = route.toLocationId,
                    toLocationId = route.fromLocationId,
                    dwellTimeSec = route.dwellTimeSec
                };
            } else {
                state.CurrentRoute = route;
            }
            _scheduleAdvancedAfterDock = true;  // M3.2.11: не дать Docked handlerу advance снова
            Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Schedule advanced to {state.CurrentRoute.toLocationId}");
        }
    }
}