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
using ProjectC.Core.ShipPosition; // T-PERSIST: ShipPositionSaveData
using ProjectC.Docking.Stations;
using ProjectC.Docking.Zones;
using ProjectC.PeacefulShip.Core;
using ProjectC.PeacefulShip.Network;
using ProjectC.Player;
using ProjectC.Trade.Config; // MARKET-ID-REFACTOR: MarketConfigCollector.NormalizeLocationId
using ProjectC.Trade.Core; // T-CARGO-NPC-01 fix #5: TradeWorld.Instance post-check
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

        // ── M3.2.N: Class-based speed profile with designer override ──
        // По умолчанию: эффективная скорость = ClassBaseSpeed × multiplier (множители ниже).
        // Если overrideClassSpeeds = true — дизайнер задаёт абсолютные значения напрямую,
        // игнорируя класс и множители. Позволяет создавать уникальные корабли.

        [Header("   Speed config")]
        [Tooltip("Вкл: использовать абсолютные значения ниже (игнорируя класс и множители).\nВыкл: авто из класса × множители.")]
        [SerializeField] private bool overrideClassSpeeds = false;

        [Header("   ── Multipliers (× class base) ──")]
        [Tooltip("Множитель скорости взлёта. База зависит от класса корабля.")]
        [Range(0.1f, 3f)] [SerializeField] private float liftSpeedMult = 1f;

        [Tooltip("Множитель крейсерской скорости. База зависит от класса корабля.")]
        [Range(0.1f, 3f)] [SerializeField] private float cruiseSpeedMult = 1f;

        [Tooltip("Множитель скорости подлёта. База зависит от класса корабля.")]
        [Range(0.1f, 3f)] [SerializeField] private float approachSpeedMult = 1f;

        [Tooltip("Множитель скорости поворота. База зависит от класса корабля.")]
        [Range(0.1f, 3f)] [SerializeField] private float maxYawRateMult = 1f;

        [Header("   ── Absolute override (если Override = true) ──")]
        [Tooltip("Скорость взлёта (м/с). Используется только при overrideClassSpeeds=true.")]
        [Min(0.1f)] [SerializeField] private float customLiftSpeed = 8f;

        [Tooltip("Крейсерская скорость (м/с). Используется только при overrideClassSpeeds=true.")]
        [Min(0.1f)] [SerializeField] private float customCruiseSpeed = 12f;

        [Tooltip("Скорость подлёта (м/с). Используется только при overrideClassSpeeds=true.")]
        [Min(0.1f)] [SerializeField] private float customApproachSpeed = 5f;

        [Tooltip("Скорость поворота (°/с). Используется только при overrideClassSpeeds=true.")]
        [Min(1f)] [SerializeField] private float customMaxYawRate = 45f;

        // ── Effective speeds (computed in OnNetworkSpawn) ──
        public float LiftSpeed { get; private set; }
        public float CruiseSpeed { get; private set; }
        public float ApproachSpeed { get; private set; }
        public float MaxYawRate { get; private set; }

        /// <summary>Базовые скорости по классу корабля (lift, cruise, approach, yaw).</summary>
        public static (float lift, float cruise, float approach, float yaw) GetClassBaseSpeeds(ShipFlightClass cls) => cls switch
        {
            ShipFlightClass.Light   => (10f, 18f, 7f,  60f),
            ShipFlightClass.Medium  => (8f,  12f, 5f,  45f),
            ShipFlightClass.Heavy   => (6f,  8f,  3f,  30f),
            ShipFlightClass.HeavyII => (5f,  6f,  2f,  20f),
            _ => (8f, 12f, 5f, 45f),
        };

        void ResolveClassSpeeds()
        {
            if (overrideClassSpeeds)
            {
                LiftSpeed     = customLiftSpeed;
                CruiseSpeed   = customCruiseSpeed;
                ApproachSpeed = customApproachSpeed;
                MaxYawRate    = customMaxYawRate;
            }
            else
            {
                var ship = GetComponent<ShipController>();
                var cls = ship != null ? ship.ShipFlightClass : ShipFlightClass.Medium;
                var (baseLift, baseCruise, baseApproach, baseYaw) = GetClassBaseSpeeds(cls);
                LiftSpeed     = baseLift     * liftSpeedMult;
                CruiseSpeed   = baseCruise   * cruiseSpeedMult;
                ApproachSpeed = baseApproach * approachSpeedMult;
                MaxYawRate    = baseYaw      * maxYawRateMult;
            }
        }

        [Header("Anti-gravity boost (Q8)")]
        [Tooltip("Длительность boost после ExitDocked (сек). 0 = отключить.")]
        [Min(0f)] [SerializeField] private float antiGravityBoostDuration = 5f;

        [Tooltip("Значение AntiGravity во время boost (1.0 = норма, 1.5 = полная компенсация).")]
        [Range(0f, 1.5f)] [SerializeField] private float antiGravityBoostValue = 1.5f;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

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
                // ENGINE-STATE: NPC всегда с включённым двигателем
                ship.SetEngineRunning(true);
            }

            // M3.2.N: resolve class-based speeds (LiftSpeed, CruiseSpeed, etc.)
            ResolveClassSpeeds();

            // FIX: гарантируем что detectCollisions включён — иначе платформа не работает
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.detectCollisions = true;
                if (debugMode) Debug.Log($"[NpcShipController:{gameObject.name}] detectCollisions forced to TRUE on spawn");
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
        public float DwellTime = 5f;      // s — время на паде перед стартом (5s для теста, потом route.dwellTimeSec)
        public float DockedSinceTime { get; private set; } = -1000f;
        private bool _scheduleAdvancedAfterDock = true; // true = первый Docked не двигаем schedule
        public string AssignedPadId { get; set; }
        public bool useNewNavTick = true;
        // T-CARGO-NPC-01: флаг одноразового выполнения dwell-trade (unload+load) за docking.
        // Сбрасывается в false при lift (см. NavTick Docked-блок).
        private bool _cargoTradeDone = true;

        // T-PERSIST: internal getters для ShipPositionServer
        internal bool ScheduleAdvancedAfterDock => _scheduleAdvancedAfterDock;
        internal bool CargoTradeDone => _cargoTradeDone;

        // === Control authority (handoff): игрок vs NPC-автопилот (см. 08_CONTROL_AUTHORITY_AND_PHYSICS.md) ===
        public enum ControlAuthority : byte { None, NpcAutopilot, HumanPilot }
        private bool _playerControlled;
        /// <summary>true, пока управление у живого пилота (NavTick уступает силовому конвейеру).</summary>
        public bool IsPlayerControlled => _playerControlled;
        /// <summary>Кто сейчас управляет кораблём.</summary>
        public ControlAuthority Authority
        {
            get
            {
                var ship = Ship;
                return (ship != null && ship.PilotCount > 0) ? ControlAuthority.HumanPilot : ControlAuthority.NpcAutopilot;
            }
        }

        // === T-NS-AV02: ship-to-ship proximity avoidance (07_SHIP_PROXIMITY_AVOIDANCE.md) ===
        [Header("Ship avoidance maneuver (server-only)")]
        [Tooltip("Скорость расхождения от соседа (м/с).")]
        [SerializeField] private float avoidSeparateSpeed = 8f;
        [Tooltip("Длительность фазы расхождения (с).")]
        [SerializeField] private float avoidSeparateTime = 1.5f;
        [Tooltip("Пауза после расхождения (с).")]
        [SerializeField] private float avoidStopTime = 0.7f;
        [Tooltip("Скорость отъезда (м/с).")]
        [SerializeField] private float avoidBackOffSpeed = 5f;
        [Tooltip("Длительность отъезда (с).")]
        [SerializeField] private float avoidBackOffTime = 1.0f;
        [Tooltip("Предохранитель: максимум времени в манёвре (с).")]
        [SerializeField] private float avoidTimeout = 8f;

        private enum AvoidPhase : byte { Separate, Stop, BackOff }
        private AvoidPhase _avoidPhase;
        private float _avoidPhaseEnteredAt;
        private float _avoidStartedAt;
        private NavMode _resumeMode = NavMode.Cruising;
        private NpcShipController _avoidOther;

        private NpcProximityZone _proximityZone;
        private bool _proximityZoneResolved;
        /// <summary>Ленивая ссылка на зону расхождения (может отсутствовать — тогда манёвр выключен).</summary>
        public NpcProximityZone ProximityZone
        {
            get
            {
                if (!_proximityZoneResolved)
                {
                    _proximityZone = GetComponent<NpcProximityZone>();
                    _proximityZoneResolved = true;
                }
                return _proximityZone;
            }
        }

        public enum NavMode : byte { Docked, Lifting, Yawing, Cruising, Berthing, Avoiding }

        /// <summary>Вызывается из NpcShipWorld.Update каждый FixedUpdate.</summary>
        public void NavTick(float dt) {
            if (!IsServer || !useNewNavTick) return;
            var ship = GetComponent<ShipController>();
            if (ship == null) return;
            // M3.2.10: guard — если спавн тайминг или IsDocked до NavMode, синхронизируем.
            // НО НЕ делаем return — Docked handler должен дойти до dwell-check.
            // Control authority: живой пилот на борту → NPC уступает управление силовому конвейеру игрока.
            if (ship.PilotCount > 0)
            {
                if (!_playerControlled)
                {
                    _playerControlled = true;
                    if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Player took control — NPC autopilot yielding");
                }
                return; // ничего не пишем в Rigidbody — рулит игрок
            }
            if (_playerControlled)
            {
                // Игрок только что вышел — возвращаем NPC-автопилот
                _playerControlled = false;
                // ENGINE-STATE: NPC всегда восстанавливает включённый двигатель
                ship.SetEngineRunning(true);
                if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Player released control — NPC autopilot resuming");
                if (CurrentMode == NavMode.Docked && !ship.IsDocked) SetMode(NavMode.Cruising);
                var resumeStation = ResolveTargetStation();
                if (resumeStation.HasValue) CruiseTargetPos = resumeStation.Value;
                _avoidOther = null;
            }

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
                // T-CARGO-NPC-01: dwell trade (unload + load) выполняется ОДИН раз за docking.
                // Запускаем сразу после schedule advance (~1s в Docked), чтобы не задерживать lift.
                if (DockedSinceTime > 1f && !_cargoTradeDone) {
                    RunDwellCargoTrade();
                    _cargoTradeDone = true;
                }
                if (Time.time - DockedSinceTime > DwellTime) {
                    LiftStartY = ship.transform.position.y;
                    rb.isKinematic = false;
                    ship.ExitDocked();
                    _scheduleAdvancedAfterDock = false;
                    // _cargoTradeDone сбрасывается в SetMode(Docked) — здесь не нужно.
                    SetMode(NavMode.Lifting);
                    return;
                }
                return;
            }

            // T-NS-AV02: расхождение NPC-кораблей — только в свободном круизе.
            if (CurrentMode == NavMode.Cruising)
            {
                var pz = ProximityZone;
                if (pz != null)
                {
                    var intruder = pz.FindClosestConflict(out _);
                    if (intruder != null) EnterAvoid(rb, intruder);
                }
            }

            switch (CurrentMode) {
                case NavMode.Lifting: TickLift(rb); break;
                case NavMode.Yawing: TickYaw(rb); break;
                case NavMode.Cruising: TickCruise(rb); break;
                case NavMode.Berthing: TickBerth(rb); break;
                case NavMode.Avoiding: TickAvoid(rb); break;
            }
        }

        public void SetMode(NavMode m) {
            if (CurrentMode == m) return;
            var old = CurrentMode;
            CurrentMode = m;
            if (m == NavMode.Docked) {
                DockedSinceTime = Time.time;
                // T-CARGO-NPC-01: сбрасываем _cargoTradeDone в false при КАЖДОМ входе в Docked.
                _cargoTradeDone = false;
                if (old == NavMode.Berthing) {
                    _scheduleAdvancedAfterDock = false; // после полёта — advance на след тике
                    AdvanceScheduleForCurrentNpc();
                }
                // M3.2.15: вычислить DwellTime из schedule + случайная добавка.
                // base = route.dwellTimeSec + Random(dwellRandomAddMinSec, dwellRandomAddMaxSec)
                // clamped to schedule.minDwellTimeSec..maxDwellTimeSec.
                ResolveDwellTime();
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
                    // FIX: НЕ отключаем detectCollisions — это ломает коллайдер платформы,
                    // из-за чего игрок проваливается, NPC не спавнятся на палубе,
                    // и raycast/spherecast не находят платформу.
                    rb.MoveRotation(Quaternion.Euler(0, rb.rotation.eulerAngles.y, 0));
                }
            }
            if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] NavMode {old} → {m}");
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
            // Если пад не назначен — запросить у диспетчера
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
                    dwellTimeSec = route.dwellTimeSec,
                    dwellRandomAddMinSec = route.dwellRandomAddMinSec,
                    dwellRandomAddMaxSec = route.dwellRandomAddMaxSec,
                    flightDurationSec = route.flightDurationSec,
                    preferredShipClass = route.preferredShipClass,
                    demandCategory = route.demandCategory
                };
            } else {
                state.CurrentRoute = route;
            }
            _scheduleAdvancedAfterDock = true;  // M3.2.11: не дать Docked handlerу advance снова
            if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Schedule advanced to {state.CurrentRoute.toLocationId}");
        }

        // === M3.2.15: resolve DwellTime from schedule + random ===

        /// <summary>
        /// Вычисляет DwellTime при входе в Docked:
        /// base = route.dwellTimeSec + Random(dwellRandomAddMinSec, dwellRandomAddMaxSec),
        /// clamped to schedule.minDwellTimeSec..maxDwellTimeSec.
        /// Если schedule или route недоступны — fallback 60s.
        /// </summary>
        void ResolveDwellTime()
        {
            var state = NpcShipWorld.Instance?.GetNpc(npcInstanceId);
            var schedule = NpcShipWorld.Instance?.GetSchedule(npcInstanceId);
            if (state == null || schedule == null)
            {
                DwellTime = 60f;
                return;
            }

            var route = state.CurrentRoute;
            float baseDwell = route.dwellTimeSec;
            float randomAdd = 0f;
            if (route.dwellRandomAddMaxSec > 0f)
            {
                float minAdd = Mathf.Max(0f, route.dwellRandomAddMinSec);
                float maxAdd = Mathf.Max(minAdd, route.dwellRandomAddMaxSec);
                randomAdd = Random.Range(minAdd, maxAdd);
            }
            DwellTime = Mathf.Clamp(baseDwell + randomAdd,
                schedule.minDwellTimeSec, schedule.maxDwellTimeSec);

            if (debugMode)
                Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] DwellTime resolved: " +
                          $"base={baseDwell:F0} + random={randomAdd:F0} = {baseDwell + randomAdd:F0}s " +
                          $"(clamped [{schedule.minDwellTimeSec:F0}..{schedule.maxDwellTimeSec:F0}] → {DwellTime:F0}s)");
        }

        // === T-CARGO-NPC-01: dwell cargo trade (unload + load) ===
        // Вызывается из NavTick.Docked ровно ОДИН раз за docking (флаг _cargoTradeDone).
        // Делегирует всю работу NpcCargoService.RunDwellTrade (D31: unload → load).
        // Backward compat: если schedule.cargoTrade == null → no-op (старое M3.2 поведение).
        void RunDwellCargoTrade() {
            if (NpcCargoService.Instance == null) {
                Debug.LogWarning($"[NpcShipController:NPC:{npcInstanceId:X}] T-CARGO-NPC-01 SKIP: NpcCargoService.Instance==null");
                return;
            }
            var schedule = NpcShipWorld.Instance?.GetSchedule(npcInstanceId);
            if (schedule == null) {
                Debug.LogWarning($"[NpcShipController:NPC:{npcInstanceId:X}] T-CARGO-NPC-01 SKIP: schedule==null");
                return;
            }

            // T-CARGO-NPC-01 fix #2 (2026-07-03): lazy-init через GetOrInitCargoTrade().
            // Работает даже если SO был загружен до auto-fill логики (OnEnable пропустил).
            var trade = schedule.GetOrInitCargoTrade();
            if (trade == null) return; // M3.2 backward compat (недостижимо — GetOrInit всегда != null)

            var state = NpcShipWorld.Instance?.GetNpc(npcInstanceId);
            if (state == null) return;
            var ship = Ship;
            if (ship == null) return;

            // locationId = текущая станция (где NPC docked прямо сейчас).
            // ВАЖНО: AdvanceScheduleForCurrentNpc() сработал выше и переключил CurrentRoute
            // на СЛЕДУЮЩИЙ leg — значит CurrentRoute.fromLocationId теперь = текущая станция
            // (откуда летим дальше), а CurrentRoute.toLocationId = следующая.
            // Берём fromLocationId (post-advance), иначе будем торговать на чужой станции.
            string locationId = state.CurrentRoute.fromLocationId;
            if (string.IsNullOrEmpty(locationId)) {
                Debug.LogWarning($"[NpcShipController:NPC:{npcInstanceId:X}] T-CARGO-NPC-01 SKIP: locationId empty " +
                                 $"(route.from='{state.CurrentRoute.fromLocationId}' route.to='{state.CurrentRoute.toLocationId}', " +
                                 $"scheduleId='{schedule.scheduleId}')");
                return;
            }

            // ShipClass — из ResolvedCargoClass (для CargoData/CargoLimits).
            var shipClass = ship.ResolvedCargoClass;

            int buyItemCount = trade.buyItems != null ? trade.buyItems.Length : 0;
            if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] T-CARGO-NPC-01 DwellTrade START: loc='{locationId}' " +
                      $"shipClass={shipClass} buyItems={buyItemCount} sellAll={trade.sellAllOnArrival} " +
                      $"buyConfigured={trade.buyConfiguredItemsAfterSell} unlimited={trade.useUnlimitedCredits} " +
                      $"scheduleId='{schedule.scheduleId}' cargo='{trade.GetType().Name}'");

            NpcCargoService.Instance.RunDwellTrade(
                npcInstanceId, ship.NetworkObjectId, shipClass, locationId, trade);

            // T-CARGO-NPC-01 fix #5 (2026-07-03): пост-лог с резюме. Юзеру видна причина skip'а
            // даже если service вернул пустой отчёт.
            // MARKET-ID-REFACTOR: нормализуем locationId для проверки.
            var tw = TradeWorld.Instance;
            string normLocId = MarketConfigCollector.NormalizeLocationId(locationId);
            string twStatus = tw == null ? "NULL (MarketServer not spawned?)" :
                              tw.Markets != null && tw.Markets.ContainsKey(normLocId) ? $"OK ({tw.Markets.Count} markets)" :
                              $"MISSING (locationId='{locationId}' not in any MarketConfig)";
            if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] T-CARGO-NPC-01 DwellTrade END: loc='{locationId}' " +
                      $"TradeWorld={twStatus}");
        }

// === T-NS-AV02: ship-to-ship avoidance maneuver ===

        void EnterAvoid(Rigidbody rb, NpcShipController other) {
            _resumeMode = (CurrentMode == NavMode.Avoiding) ? NavMode.Cruising : CurrentMode;
            _avoidOther = other;
            _avoidPhase = AvoidPhase.Separate;
            _avoidPhaseEnteredAt = Time.time;
            _avoidStartedAt = Time.time;
            SetMode(NavMode.Avoiding);
        }

        void TickAvoid(Rigidbody rb) {
            // Предохранитель — не зависаем в манёвре
            if (Time.time - _avoidStartedAt > avoidTimeout) { ResumeFromAvoid(rb); return; }

            // Горизонтальный вектор "от соседа"
            Vector3 away = Vector3.zero;
            if (_avoidOther != null) {
                away = rb.position - _avoidOther.transform.position;
                away.y = 0f;
            }
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            away.Normalize();

            float t = Time.time - _avoidPhaseEnteredAt;
            switch (_avoidPhase) {
                case AvoidPhase.Separate:
                    rb.linearVelocity = new Vector3(away.x * avoidSeparateSpeed, 0f, away.z * avoidSeparateSpeed);
                    rb.angularVelocity = Vector3.zero;
                    if (t >= avoidSeparateTime) { _avoidPhase = AvoidPhase.Stop; _avoidPhaseEnteredAt = Time.time; }
                    break;
                case AvoidPhase.Stop:
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    if (t >= avoidStopTime) { _avoidPhase = AvoidPhase.BackOff; _avoidPhaseEnteredAt = Time.time; }
                    break;
                case AvoidPhase.BackOff:
                    rb.linearVelocity = new Vector3(away.x * avoidBackOffSpeed, 0f, away.z * avoidBackOffSpeed);
                    rb.angularVelocity = Vector3.zero;
                    if (t >= avoidBackOffTime) {
                        if (IsClearOfConflict()) ResumeFromAvoid(rb);
                        else { _avoidPhase = AvoidPhase.Separate; _avoidPhaseEnteredAt = Time.time; }
                    }
                    break;
            }
        }

        bool IsClearOfConflict() {
            if (_avoidOther == null) return true;
            var pz = ProximityZone;
            if (pz == null) return true;
            float d = Vector3.Distance(transform.position, _avoidOther.transform.position);
            float otherAvoid = _avoidOther.ProximityZone != null ? _avoidOther.ProximityZone.AvoidanceRadius : 0f;
            return d >= pz.ClearRadius + otherAvoid;
        }

        void ResumeFromAvoid(Rigidbody rb) {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            _avoidOther = null;
            // Возврат на прошлый маршрут: прежний режим + прежняя CruiseTargetPos (не менялась)
            SetMode(_resumeMode == NavMode.Avoiding ? NavMode.Cruising : _resumeMode);
        }

        // === T-PERSIST: RestoreFromSave ===

        /// <summary>Восстановление NavTick-состояния после перезапуска сервера. Server-only.</summary>
        public void RestoreFromSave(ShipPositionSaveData data)
        {
            if (!IsServer) return;

            // ── NavMode (критично: EnterDocked ставит kinematic) ──
            NavMode savedMode = (NavMode)data.navMode;

            // avoiding → transient → fallback to cruising
            if (savedMode == NavMode.Avoiding)
                savedMode = NavMode.Cruising;

            DwellTime = data.dwellTime > 0 ? data.dwellTime : 60f;
            _scheduleAdvancedAfterDock = data.scheduleAdvancedAfterDock;
            _cargoTradeDone = data.cargoTradeDone;
            AssignedPadId = string.IsNullOrEmpty(data.assignedPadId) ? null : data.assignedPadId;
            CruiseTargetPos = new Vector3(data.pxCruise, data.pyCruise, data.pzCruise);
            LiftStartY = data.liftStartY;

            var ship = GetComponent<ShipController>();
            var rb = GetComponent<Rigidbody>();

            // Восстанавливаем режим
            switch (savedMode)
            {
                case NavMode.Docked:
                    CurrentMode = NavMode.Docked;
                    DockedSinceTime = Time.time - Mathf.Min(data.dockedSinceTimeOffset, DwellTime * 0.9f);
                    if (rb != null) rb.isKinematic = true;
                    if (ship != null && !ship.IsDocked) ship.EnterDocked();
                    break;

                case NavMode.Lifting:
                    CurrentMode = NavMode.Lifting;
                    if (rb != null) rb.isKinematic = false;
                    if (ship != null && ship.IsDocked) ship.ExitDocked();
                    break;

                case NavMode.Yawing:
                case NavMode.Cruising:
                    CurrentMode = savedMode;
                    if (rb != null) rb.isKinematic = false;
                    if (ship != null && ship.IsDocked) ship.ExitDocked();
                    break;

                case NavMode.Berthing:
                    CurrentMode = NavMode.Berthing;
                    if (rb != null) rb.isKinematic = false;
                    if (ship != null && ship.IsDocked) ship.ExitDocked();
                    // Если пад назначен и мы на дистанции касания — док сработает на первом NavTick
                    break;
            }

            // Восстановить NpcShipState
            if (NpcShipWorld.Instance != null)
                NpcShipWorld.Instance.RestoreNpcState(npcInstanceId, data);

            if (debugMode)
                Debug.Log($"[NpcShipController:{gameObject.name}] RestoreFromSave mode={savedMode} " +
                          $"idx={data.scheduleIndex} docked={ship != null && ship.IsDocked}");
        }

    }
}