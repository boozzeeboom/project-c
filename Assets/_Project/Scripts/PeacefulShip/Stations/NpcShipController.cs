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
using System.Collections.Generic; // T-NS-NAV11: цепочка точек маршрута
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

            // T-NS-ALT01: рантайм-копии границ (сдвигаются при F8, см. ApplyRebaseTranslation).
            // T-NS-LOG02: спавн ПОСЛЕ ребейса (персистентная загрузка!) — инициализируем
            // сразу со сдвинутым базисом, иначе ProfileY clamp висит в редакторном кадре
            // до следующего сдвига. Базис: сериализованные значения + кумулятив сессии.
            _altFloor = altFloorY + ProjectC.World.FloatingOrigin.Network.GlobalMotionControlledRebaseSlice.CumulativeRebaseOffset.y;
            _altCeil = altCeilY + ProjectC.World.FloatingOrigin.Network.GlobalMotionControlledRebaseSlice.CumulativeRebaseOffset.y;

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

                // T-NS-WF10: не оставлять висячий слот теснины (экспайри 180 с есть,
                // но чистим сразу).
                NpcShipTrafficManager.Instance?.ReleaseWallSlot(npcInstanceId);

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
        /// Резерв/v2-хук (автопилот игрока): живой NavTick-путь использует прямой
        /// Rigidbody-контроль, а не этот метод (TickNpc удалён в T-NS-P2).
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
        // T-NS-WF04: раздельный кулдаун (лог показал 20/21 выходов по таймауту —
        // чистого расхождения в куче не бывает, короткий кулдаун даёт пинг-понг).
        [Tooltip("Пауза после выхода по ТАЙМАУТУ (с): корабль уходит прямо из кучи. Длиннее обычного.")]
        [Min(0f)] [SerializeField] private float avoidTimeoutCooldownSec = 9f;
        // T-NS-WF04b: escape вдоль стены (манёвр из WallFollow сохраняет прогресс
        // вдоль склона вместо отскока назад в кучу).
        [Tooltip("Вес касательной стены в escape-векторе, если Avoiding вызван из WallFollow (0=только away).")]
        [Range(0f, 1f)] [SerializeField] private float avoidWallBlend = 0.6f;

        // T-NS-AVOID4: разрыв петли Avoiding↔Cruising + эскалация
        [Header("Avoidance loop-breaker (server-only)")]
        [Tooltip("Пауза после выхода из манёвра (с): новые конфликты игнорируются, корабль летит прямо.")]
        [Min(0f)] [SerializeField] private float avoidCooldownSec = 2.5f;
        [Tooltip("Сколько входов в avoidance подряд (внутри окна ниже) терпим, прежде чем уйти вверх.")]
        [Min(2)] [SerializeField] private int avoidEscalateAfter = 3;
        [Tooltip("Окно (с): входы в avoidance чаще этого считаются одной серией затора.")]
        [Min(1f)] [SerializeField] private float avoidEscalationWindowSec = 10f;

        private float _avoidCooldownUntil;
        private int _avoidCycles;
        private float _lastAvoidResumeAt = -1000f;

        // T-NS-BZ07: raycast escape corridor — поиск выхода из Π-доков
        [Header("Escape corridor (raycast)")]
        [Tooltip("Количество лучей для поиска выхода из тесного пространства.")]
        [Range(4, 36)] [SerializeField] private int avoidEscapeRays = 16;
        [Tooltip("Максимальная дистанция луча (м).")]
        [Min(10f)] [SerializeField] private float avoidEscapeMaxDist = 200f;
        [Tooltip("Вес направления выхода: 0 = только away, 1 = только escape.")]
        [Range(0f, 1f)] [SerializeField] private float avoidEscapeBlend = 0.6f;

        private enum AvoidPhase : byte { Separate, Stop, BackOff }
        private AvoidPhase _avoidPhase;
        private float _avoidPhaseEnteredAt;
        private float _avoidStartedAt;
        private NavMode _resumeMode = NavMode.Cruising;
        private NpcShipController _avoidOther;
        private NpcProximityZoneBuilds _avoidBuild;
        private Vector3 _avoidFromPos;
        // T-NS-WF04b: запомненная касательная стены на входе в Avoiding из WallFollow
        // (направление, не позиция — FO-хук не нужен).
        private Vector3 _avoidWallDir;
        private bool _avoidHasWallDir;
        private Vector3 _escapeDir; // T-NS-BZ07: raycast-computed escape direction

        // PAD-ASSIGN-THROTTLE: не долбим диспетчер каждый FixedUpdate
        private float _lastPadAssignAttemptTime = float.MinValue;
        private const float PAD_ASSIGN_RETRY_SEC = 3f;
        // T-NS-PADS1: последняя дистанция до пада в Berthing (для progress-refresh окна посадки)
        private float _lastBerthDist = float.MaxValue;

        // === T-NS-BERTH2: Berthing watchdog + holding-точка (без новых NavMode) ===
        [Header("Berthing watchdog (server-only)")]
        [Tooltip("Сколько секунд без сближения с падом терпим, прежде чем прервать заход.")]
        [Min(1f)] [SerializeField] private float berthWatchdogSec = 20f;
        [Tooltip("Минимальное сближение (м), которое считается прогрессом захода.")]
        [Min(0.1f)] [SerializeField] private float berthMinProgressMeters = 2f;
        [Tooltip("Сколько прерванных заходов подряд терпим, прежде чем уйти на другую станцию.")]
        [Min(1)] [SerializeField] private int berthMaxAttempts = 3;
        [Tooltip("Относительный набор высоты (м) при прерывании захода перед повторной попыткой.")]
        [Min(5f)] [SerializeField] private float abortClimbMeters = 60f;
        [Tooltip("Высота holding-зависания над станцией (м над station.position.y), пока нет пада.")]
        [Min(10f)] [SerializeField] private float holdClearanceMeters = 60f;
        [Tooltip("Сколько неудачных запросов пада подряд терпим в holding, прежде чем уйти. 3 с/попытка.")]
        [Min(1)] [SerializeField] private int holdingMaxRetries = 20;

        // === T-NS-CORRIDOR6: Berthing-коридор (заход сверху, без новых NavMode) ===
        [Header("Berthing corridor (server-only)")]
        [Tooltip("Высота overhead-ворот над падом (м). Подход идёт к точке pad + clearance, " +
                 "спуск — строго вертикально. Одно семейство с holding/departure (60 м).")]
        [Min(10f)] [SerializeField] private float overheadClearanceMeters = 60f;
        [Tooltip("Радиус «трубы» (м): внутри него по горизонтали — вертикальный спуск на пад.")]
        [Min(5f)] [SerializeField] private float corridorRadiusMeters = 25f;
        [Tooltip("Допуск ворот (м): считаем что на высоте overhead, если |dy| меньше.")]
        [Min(1f)] [SerializeField] private float corridorGateToleranceMeters = 5f;
        [Tooltip("Макс. горизонтальная коррекция (м/с) при вертикальном спуске — держит «трубу».")]
        [Min(0.5f)] [SerializeField] private float descendLateralCap = 2f;
        // T-NS-DOCK01: зонд в Overhead — ранний go-around вместо 20 с гринда в стену.
        // Лидар в Descend выключен (труба вертикальна), в Overhead — только детект.
        [Tooltip("ВКЛ: в Overhead проверять прямую до ворот зондом и уходить на go-around сразу.")]
        [SerializeField] private bool berthOverheadProbe = true;
        [Tooltip("Дистанция зонда в Overhead (м): стена ближе — прерываем заход сразу.")]
        [Min(20f)] [SerializeField] private float berthOverheadProbeDist = 120f;
        private float _berthProbeNextAt;

        // Подфаза захода — приватная, наружу виден только NavMode.Berthing:
        // IsAvoidable/RestoreFromSave/переключатель режимов не трогаем.
        // После загрузки сейва — всегда Overhead (сверху безопасно). F8-безопасно (enum).
        private enum BerthPhase : byte { Overhead, Descend }
        private BerthPhase _berthPhase = BerthPhase.Overhead;

        // === T-NS-DEPART3: Departure-Chimney — уход от города вверх ===
        [Header("Departure chimney (server-only)")]
        [Tooltip("Относительный набор высоты над падом (м) перед уходом в Cruising. " +
                 "Замена хардкода +5 м: корабль выходит из «чаши» порта вертикально, " +
                 "где по замеру R11 300 м+ свободного неба, и только потом летит горизонтально.")]
        [Min(5f)] [SerializeField] private float departClimbMeters = 60f;

        // Состояние watchdog/holding — всё относительное (дистанции, таймеры, счётчики),
        // мировых Vector3 не храним: FO-хук не нужен, F8 не роняет заход.
        private float _berthNoProgressSince;
        private int _berthAttempts;
        private int _holdingRetries;
        private float _abortClimbRemaining; // м, >0 = идёт аварийный набор высоты

        // === T-NS-WF01: Terrain wall-follow — латеральный обход гор/скал (server-only) ===
        // Лор: пики ОБЛЕТАЮТ вокруг, не перелетают. Вертикаль в обходе заморожена
        // на высоте входа; возврат к профилю — пологой глиссадой (returnVerticalCap).
        // См. docs/NPC_others_peacfull/npc_ship/12_TERRAIN_WALLFOLLOW_NAV.md.
        [Header("Wall-follow terrain avoidance (server-only)")]
        [Tooltip("Радиус forward-SphereCast (м) — примерно полугабарит корпуса.")]
        [Min(1f)] [SerializeField] private float wallProbeRadius = 12f;
        [Tooltip("Дальность зонда: CruiseSpeed × это время (с).")]
        [Min(1f)] [SerializeField] private float wallLookAheadSec = 8f;
        [Tooltip("Мин. дальность зонда (м).")]
        [Min(10f)] [SerializeField] private float wallLookAheadMin = 150f;
        [Tooltip("Макс. дальность зонда (м).")]
        [Min(50f)] [SerializeField] private float wallLookAheadMax = 600f;
        [Tooltip("Держать склон на этом боковом отступе (м).")]
        [Min(5f)] [SerializeField] private float wallClearance = 40f;
        [Tooltip("Интервал проб зонда/LOS (с). Stagger по NpcInstanceId — задел на 200+.")]
        [Min(0.05f)] [SerializeField] private float wallProbeIntervalSec = 0.2f;
        [Tooltip("Предохранитель: максимум времени в обходе (с) → divert.")]
        [Min(10f)] [SerializeField] private float wallTimeoutSec = 120f;
        [Tooltip("Сколько секунд без смещения терпим (притирание к склону) → divert.")]
        [Min(3f)] [SerializeField] private float wallNoProgressSec = 15f;
        [Tooltip("Мин. смещение (м), которое считается прогрессом обхода.")]
        [Min(1f)] [SerializeField] private float wallMinProgressMeters = 5f;
        [Tooltip("Макс. вертикальная скорость возврата к профилю после LOS (м/с).")]
        [Min(0.5f)] [SerializeField] private float returnVerticalCap = 2f;
        [Tooltip("Хит ближе этого к цели (м) — геометрия станции, не стена (LOS чист).")]
        [Min(10f)] [SerializeField] private float wallArriveMargin = 100f;
        // T-NS-WF01b: антидребезг на кромке скалы (лево-право-стояние носом).
        [Tooltip("Подряд забитых проб для входа в обход (скользящий зацеп кромки не триггерит).")]
        [Min(1)] [SerializeField] private int wallEnterStrikes = 2;
        [Tooltip("Подряд чистых LOS для выхода (мерцание кромки не выпускает).")]
        [Min(1)] [SerializeField] private int wallExitStrikes = 3;
        // T-NS-WF08: wallForwardClearStrikes удалён — курсом рулит скоринг лидара.
        [Tooltip("Пауза после выхода из обхода: летим прямо, зонд молчит (с).")]
        [Min(0f)] [SerializeField] private float wallResumeCooldownSec = 5f;
        // T-NS-WF05: cruise-stuck — корабль втёрся в склон (зонд изнутри корпуса
        // слеп: SphereCast не видит перекрывающий коллайдер) и давит в Cruising вечно.
        [Tooltip("Мин. смещение (м), которое считается прогрессом круиза.")]
        [Min(1f)] [SerializeField] private float cruiseStuckDist = 10f;
        [Tooltip("Сколько секунд без прогресса терпим вдали от цели → recovery.")]
        [Min(3f)] [SerializeField] private float cruiseStuckSec = 10f;
        [Tooltip("Длительность recovery-отката назад+вбок (с).")]
        [Min(1f)] [SerializeField] private float cruiseRecoverSec = 3f;
        [Tooltip("Скорость отката (м/с).")]
        [Min(1f)] [SerializeField] private float cruiseRecoverSpeed = 6f;
        [Tooltip("Сколько recovery подряд терпим, прежде чем уйти на другую станцию.")]
        [Min(1)] [SerializeField] private int cruiseMaxRecoveries = 2;
        // T-NS-LOG02: contour-grind — корабль ДВИЖЕТСЯ (displacement-watchdog молчит),
        // но к цели не приближается (лог 233351: 0010 +182 м за 130 с, 002B +51 м —
        // огибают склон по горизонтали к слепой точке, advance по 3D не сходится).
        [Tooltip("Мин. скорость сближения с целью (м/с): ползём медленнее — контурный гринд.")]
        [Min(0.1f)] [SerializeField] private float cruiseGoalCloseMin = 1f;
        [Tooltip("Сколько секунд гринда терпим → replan с места, потом scatter/divert.")]
        [Min(10f)] [SerializeField] private float cruiseGoalStuckSec = 45f;
        [Tooltip("Сколько медленных replan подряд терпим за leg, потом scatter/divert.")]
        [Min(1)] [SerializeField] private int cruiseGoalMaxReplans = 2;
        // T-NS-WF06a: min-dwell обхода (фликер Гиганта: вход-выход за 0.6 с ×10/мин —
        // толстый SphereCast цепляет кромку, тонкий LOS-луч чист).
        [Tooltip("Мин. время в обходе (с): LOS-выход раньше игнорируется.")]
        [Min(0f)] [SerializeField] private float wallMinDwellSec = 2.5f;
        // === T-NS-WF08: «лидар» вместо бампера — веер лучей + VFH-скоринг ===
        // Один forward-луч = робот-пылесос без лидара: тыкается в кромку и дёргается.
        // Веер видит склон заранее и ведёт через просветы, а не бьётся в лоб.
        [Header("Lidar terrain sensing (server-only)")]
        [Tooltip("Полуширина веера (град): лучи 0, ±step … ±half.")]
        [Range(15f, 90f)] [SerializeField] private float lidarHalfAngle = 60f;
        [Tooltip("Шаг веера (град).")]
        [Range(5f, 30f)] [SerializeField] private float lidarStepDeg = 15f;
        [Tooltip("Вес клиренса в скоринге (0..1), остальное — держание курса к цели.")]
        [Range(0f, 1f)] [SerializeField] private float lidarClearWeight = 0.7f;
        [Tooltip("Вес инерции курса (0..1): держит сторону обхода, анти-флип.")]
        [Range(0f, 1f)] [SerializeField] private float lidarKeepWeight = 0.5f;
        [Tooltip("Бок вплотную (м): немедленный вход в обход (зацеп кромки).")]
        [Min(10f)] [SerializeField] private float lidarSideTrigger = 80f;
        // T-NS-WF12: слепота вниз — горизонтальные лучи идут по касательной над
        // восходящим склоном, корпус скребёт ниже линии лучей (Берег: Y→0, dist стоит).
        [Tooltip("Наклон нижнего луча (град вниз): видит склон/землю впереди-снизу.")]
        [Range(5f, 60f)] [SerializeField] private float lidarDownPitchDeg = 25f;
        [Tooltip("Крит. дистанция нижнего луча (м): ниже — аварийный набор (не в скалу же).")]
        [Min(10f)] [SerializeField] private float downCriticalDist = 60f;
        // T-NS-WF14: нижний луч входит отдельно (ближе, чем боковой): земля далеко
        // внизу — не повод для обхода (фликер Странника), земля вплотную — повод.
        [Tooltip("Триггер входа нижнего луча (м): должен быть < lidarSideTrigger.")]
        [Min(5f)] [SerializeField] private float lidarDownTrigger = 40f;
        private float _downClear = float.MaxValue; // между тактами скана
        // === T-NS-WF09: жадный вход + гриндеры (лог 000043) ===
        // Входов 44/76с: центр цепляет дальние скалы на краю lookahead → эпизоды
        // ровно min-dwell. Вход по центру — только внутренняя половина.
        [Tooltip("Доля lookahead: центр считается забитым только внутри неё (0..1).")]
        [Range(0.1f, 1f)] [SerializeField] private float lidarCenterFraction = 0.35f;
        // === T-NS-WF10: слот теснины — только один обход в радиусе (server-only) ===
        [Header("Wall slot (server-only)")]
        [Tooltip("Радиус слота: чужой обход ближе — висим в hover-wait, не лезем в кучу.")]
        [Min(50f)] [SerializeField] private float wallSlotRadius = 300f;
        [Tooltip("Сколько ждём слот в hover (с), потом divert.")]
        [Min(10f)] [SerializeField] private float wallWaitTimeoutSec = 60f;
        private float _wallWaitStartedAt; // 0 = не ждём
        // T-NS-WF10b: анти-фликер ожидания (спам wall-slot-wait): «чисто» засчитываем
        // только серией, иначе дребезг 0/2 на границе слота.
        private int _wallWaitClearStrikes;
        // === T-NS-NAV12a: scatter-jailbreak — выход из кучу (server-only) ===
        // Первый stuck-диагноз за leg (клин/гриндер) — не divert, а радиальный
        // разлёт с чистого места; второй — divert. Счётчик живёт один leg.
        [Header("Scatter jailbreak (server-only)")]
        [Tooltip("Скорость разлёта (м/с).")]
        [Min(1f)] [SerializeField] private float scatterSpeed = 10f;
        [Tooltip("Длительность разлёта (с): дистанция ≈ speed × sec.")]
        [Min(3f)] [SerializeField] private float scatterSec = 12f;
        private float _scatterUntil; // 0 = не разлетаемся
        private Vector3 _scatterDir;
        private int _legStuckCount; // stuck-диагнозов за текущий leg
        // Жук 42 с / Летучий 59 с в обходе без LOS: displacement-watchdog их не ловит
        // (ёрзают ±5 м). Ловит прогресс к ЦЕЛИ с момента входа.
        [Tooltip("Сколько секунд в обходе ждём приближения к цели → divert.")]
        [Min(10f)] [SerializeField] private float wallProgressSec = 35f;
        [Tooltip("Мин. приближение к цели за wallProgressSec (м), иначе divert.")]
        [Min(10f)] [SerializeField] private float wallProgressMin = 100f;
        [Tooltip("Слои препятствий для зонда (террейн, скалы, город). Триггеры игнорятся всегда.")]
        [SerializeField] private LayerMask wallObstacleMask = -1;

        // === T-NS-WF06b: эшелоны круиза — вертикальная развязка стаи (server-only) ===
        // Высота круиза = профиль + (id % N) × step, зажато в активный коридор.
        // Коридор святой: эшелон внутри min/max, не над ними. F8-безопасно (offset).
        // См. docs/NPC_others_peacfull/npc_ship/13_NAV_COORDINATOR_RESEARCH.md §3B.
        // T-NS-COORD01: дефолты ВКЛ по данным лога (стая — доминанта 2/3 прерываний).
        // Внимание: у scene-инстансов сериализовано старое false — выставить руками или Reset.
        [Header("Cruise echelons (server-only)")]
        [Tooltip("ВКЛ: эшелон высоты круиза по NpcInstanceId внутри активного коридора.")]
        [SerializeField] private bool useEchelons = true;
        // T-NS-WF07: шаг 150 м (было 15): лог показал max разнос 45 м при радиусах
        // avoidance 90–180 м — эшелоны не развязывали (3 корабля на ech=45 в шаре 200 м).
        // Коридор 1200–4450 — места хватает (4 × 150 = 600 м).
        [Tooltip("Шаг эшелона (м): должен превышать типичный avoidanceRadius (90-180).")]
        [Min(1f)] [SerializeField] private float echelonStep = 150f;
        [Tooltip("Число эшелонов (id % N).")]
        [Min(1)] [SerializeField] private int echelonCount = 4;
        [Tooltip("Запас эшелона от границ коридора (м).")]
        [Min(0f)] [SerializeField] private float echelonCorridorMargin = 50f;
        private float _echelonOffset;

        // Состояние обхода — F8-безопасно: таймеры/азимут, мировых Vector3 нет.
        // Высота входа (_wallEntryY) и CruiseTargetPos сдвигаются через ApplyRebaseTranslation.
        // T-NS-WF08: сторону держит keep-член скоринга лидара (rule-of-side удалён).
        private float _wallEntryY;
        private float _wallStartedAt;
        private float _wallTurnAccum; // накопленный разворот азимута на цель (петля 360° → divert)
        private float _wallLastBearing;
        private Vector3 _wallLastPos;
        private float _wallLastProgressAt;
        private float _wallProbeNextAt;
        private float _wallCooldownUntil;
        // T-NS-WF18: мин. клиренс с прошлого скана (для скорости по просвету).
        private float _wallMinClear = float.MaxValue;
        // T-NS-WF17: retreat из вогнутой ловушки (server-only, относительное — F8-safe).
        [Tooltip("Разворот азимута в обходе (град), при котором без приближения — ловушка, идём назад.")]
        [Min(90f)] [SerializeField] private float wallRetreatTurnDeg = 180f;
        [Tooltip("Длительность retreat-полёта назад по входу (с).")]
        [Min(3f)] [SerializeField] private float wallRetreatSec = 8f;
        private Vector3 _wallEntryDir;
        private Vector3 _retreatDir;
        private float _retreatUntil; // 0 = не отходим
        // T-NS-WF09: дистанция до цели на входе (прогресс-watchdog, относительна — F8-безопасно).
        private float _wallEntryDist;
        // T-NS-WF01b: состояние антидребезга (сброс — новый leg/divert).
        private int _wallBlockStrikes;
        private int _wallLosStrikes;
        private float _logBeatNextAt; // T-NS-LOG01: следующий heartbeat в глобальный лог
        // T-NS-WF05: состояние cruise-stuck (только относительные данные — F8-безопасно).
        private Vector3 _cruiseLastPos;
        private Vector3 _recoverStartPos; // T-NS-WF11: откуда начался recovery
        private bool _recoverArmed; // T-NS-WF11: ждём конца recovery для зачёта
        private float _cruiseLastProgressAt;
        private float _cruiseRecoverUntil;
        private Vector3 _cruiseRecoverDir;
        private int _cruiseRecoveries;
        // T-NS-LOG02: окно замера сближения с NavGoal (contour-grind).
        private float _goalProgDist;
        private float _goalProgAt; // 0 = не armed
        private int _goalProgIdx = -1;
        private int _goalSlowReplans;
        private bool _graphParityFlip; // T-NS-LOG02d: зеркало развода графа после slow-replan

        // === T-NS-GATE04: процедурные ворота городов (kill-switch useCityGates) ===
        // Выключено = старое поведение бит-в-бит. Код gates — отдельная область внизу,
        // одна точка входа из TickCruise. Удаление тикета = удалить область + 1 if.
        // === T-NS-WF06c: departure spacing — разнос вылетов пачкой (server-only) ===
        [Header("Departure spacing (server-only)")]
        [Tooltip("ВКЛ: ждать свободного взлёта у своей станции (не более одного Lifting рядом).")]
        [SerializeField] private bool useDepartureSpacing = true;
        [Tooltip("Радиус мьютекса взлёта от станции (м).")]
        [Min(50f)] [SerializeField] private float departureMutexRadius = 300f;
        private float _departWaitNextAt;

        [Header("City gates (procedural, server-only)")]
        [Tooltip("ВКЛ: заход через ворота на границе cityRadius. ВЫКЛ: старый прямой заход.")]
        [SerializeField] private bool useCityGates = false;
        [Tooltip("Дистанция до станции (м), ближе которой ищем ворота.")]
        [Min(100f)] [SerializeField] private float gateTriggerDist = 1500f;
        [Tooltip("Запас за границей города для точки ворот (м).")]
        [Min(0f)] [SerializeField] private float gateMargin = 100f;
        [Tooltip("Допуск прибытия в ворота (м).")]
        [Min(5f)] [SerializeField] private float gateArrivalTol = 60f;
        [Tooltip("Сколько азимутальных вариантов ворот (разнос трафика по NpcInstanceId).")]
        [Min(1)] [SerializeField] private int gateVariants = 4;
        [Tooltip("Угловой шаг варианта ворот (град).")]
        [Min(1f)] [SerializeField] private float gateVariantStepDeg = 20f;

        /// <summary>
        /// Приоритет расхождения: выше → делает полный манёвр, ниже → yield (ждёт).
        /// Авто-назначается из NpcInstanceId (детерминированно, без сетевой коммуникации).
        /// Здания имеют виртуальный приоритет 0 (ниже любого корабля).
        /// </summary>
        public uint AvoidancePriority => (uint)npcInstanceId;

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

        // T-NS-WF01: WallFollow — латеральный обход гор/скал (правило стены, выход по LOS).
        // T-NS-GATE04: GateApproach/CorridorLeg — процедурные ворота городов (useCityGates).
        // Значения добавлены в конец: CurrentMode не сериализуется напрямую, порядок безопасен.
        // Transient для сейвов — RestoreFromSave маппит их в Cruising.
        public enum NavMode : byte { Docked, Lifting, Yawing, Cruising, Berthing, Avoiding, AvoidYield, WallFollow, GateApproach, CorridorLeg }

        /// <summary>Вызывается из NpcShipWorld.Update каждый FixedUpdate.</summary>
        public void NavTick(float dt) {
            if (!IsServer || !useNewNavTick) return;
            var ship = GetComponent<ShipController>();
            if (ship == null || !ship.CanSimulateInCurrentCoordinates) return;
            // M3.2.10: guard — если спавн тайминг или IsDocked до NavMode, синхронизируем.
            // НО НЕ делаем return — Docked handler должен дойти до dwell-check.
            // Control authority: живой пилот на борту → NPC уступает управление силовому конвейеру игрока.
            if (ship.PilotCount > 0)
            {
                if (!_playerControlled)
                {
                    _playerControlled = true;
                    // T-NS-LOG02e: yield виден в Nav-логе (молчание стоило 3 сессии
                    // гаданий, лог 000914: корабль «не слушается», а им рулит игрок).
                    NpcShipNavLog.Transition(gameObject.name, npcInstanceId,
                        CurrentMode.ToString(), CurrentMode.ToString(), "player-yield", "");
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
                NpcShipNavLog.Transition(gameObject.name, npcInstanceId,
                    CurrentMode.ToString(), CurrentMode.ToString(), "player-release", "");
                if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Player released control — NPC autopilot resuming");
                if (CurrentMode == NavMode.Docked && !ship.IsDocked) SetMode(NavMode.Cruising, "player-release");
                var resumeStation = ResolveTargetStation();
                if (resumeStation.HasValue) SetCruiseTarget(resumeStation.Value);
                _avoidOther = null;
            }

            if (ship.IsDocked && CurrentMode != NavMode.Docked) {
                SetMode(NavMode.Docked, "isDocked-sync");
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
                    // T-NS-WF06c: departure mutex — не взлетать пачкой (проверка раз в 3 с,
                    // dwell молча продлевается, событие depart-wait видно в Nav-логе).
                    if (useDepartureSpacing && Time.time >= _departWaitNextAt) {
                        _departWaitNextAt = Time.time + 3f;
                        var home = ResolveCurrentStation();
                        var tm = NpcShipTrafficManager.Instance;
                        if (home.HasValue && tm != null
                            && !tm.IsDepartureClear(home.Value, departureMutexRadius, npcInstanceId)) {
                            NpcShipNavLog.Transition(gameObject.name, npcInstanceId, "Docked", "Docked",
                                "depart-wait", "");
                            return;
                        }
                    }
                    LiftStartY = ship.transform.position.y;
                    rb.isKinematic = false;
                    ship.ExitDocked();
                    _scheduleAdvancedAfterDock = false;
                    // _cargoTradeDone сбрасывается в SetMode(Docked) — здесь не нужно.
                    SetMode(NavMode.Lifting, "dwell-done");
                    return;
                }
                return;
            }

            // T-NS-AV02: расхождение NPC-кораблей (только в круизе) и зданий (во всех свободных режимах).
            // Berthing/Docked исключены: корабль на финальном заходе игнорирует билд-коллайдеры
            // (позже переосмыслим подход к заходу в док).
            // T-NS-AVOID4: cooldown после манёвра — летим прямо, не дребезжим Avoiding↔Cruising.
            // T-NS-WF02: ship-to-ship проверка расширена на WallFollow (расход за хребтом).
            // Корабль важнее горы: EnterAvoid из WallFollow, _resumeMode помнит WallFollow.
            if ((CurrentMode == NavMode.Lifting || CurrentMode == NavMode.Yawing || CurrentMode == NavMode.Cruising
                    || CurrentMode == NavMode.WallFollow)
                && Time.time >= _avoidCooldownUntil)
            {
                var pz = ProximityZone;
                if (pz != null)
                {
                    // Ship-to-ship: в Cruising и WallFollow
                    NpcShipController intruder = null;
                    float shipDist = float.MaxValue;
                    if (CurrentMode == NavMode.Cruising || CurrentMode == NavMode.WallFollow)
                        intruder = pz.FindClosestConflict(out shipDist);

                    // Ship-to-build: во всех свободных режимах
                    var build = pz.FindClosestBuildConflict(out float buildDist);

                    if (intruder != null && (build == null || shipDist <= buildDist))
                    {
                        EnterAvoid(rb, intruder);
                    }
                    else if (build != null)
                    {
                        if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] build avoidance: {build.gameObject.name} dist={buildDist:F1} fromPos={build.ClosestPoint(rb.position)}");
                        EnterAvoid(rb, build);
                    }
                }
            }

            // T-NS-LOG01: heartbeat 1/5с — видно «стоит носом» (spd≈0 вне Docked)
            // и залипание в режиме без переходов.
            if (Time.time >= _logBeatNextAt) {
                _logBeatNextAt = Time.time + 5f + ProbePhaseOffset();
                float distBT = CruiseTargetPos == Vector3.zero
                    ? -1f : Vector3.Distance(rb.position, CruiseTargetPos);
                string beatDetail = $"ech={_echelonOffset:F0}" + (CurrentMode == NavMode.WallFollow
                    ? $";losStrikes={_wallLosStrikes};turn={_wallTurnAccum:F0}"
                    : "");
                NpcShipNavLog.Heartbeat(gameObject.name, npcInstanceId, CurrentMode.ToString(),
                    rb.linearVelocity.magnitude, distBT, rb.position, beatDetail);
            }

            switch (CurrentMode) {
                case NavMode.Lifting: TickLift(rb); break;
                case NavMode.Yawing: TickYaw(rb); break;
                case NavMode.Cruising: TickCruise(rb); break;
                case NavMode.Berthing: TickBerth(rb); break;
                case NavMode.Avoiding: TickAvoid(rb); break;
                case NavMode.AvoidYield: TickAvoidYield(rb); break;
                case NavMode.WallFollow: TickWallFollow(rb); break;
                case NavMode.GateApproach: TickGateApproach(rb); break;
                case NavMode.CorridorLeg: TickCorridorLeg(rb); break;
            }
        }

        /// <summary>
        /// T-NS-LOG01: reason пишется в глобальный Nav-лог (файл сессии).
        /// По счётчикам переходов видно дребезг (WallFollow↔Cruising, Avoiding-петли).
        /// </summary>
        public void SetMode(NavMode m, string reason = null, string detail = "") {
            if (CurrentMode == m) return;
            var old = CurrentMode;
            CurrentMode = m;
            NpcShipNavLog.Transition(gameObject.name, npcInstanceId, old.ToString(), m.ToString(),
                reason ?? "", detail ?? "");
            if (m == NavMode.Docked) {
                DockedSinceTime = Time.time;
                // T-NS-BERTH2: успешный док — сбрасываем счётчики захода/holding.
                _berthAttempts = 0;
                _holdingRetries = 0;
                _abortClimbRemaining = 0f;
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
                // T-NS-BERTH2: новый leg — сбрасываем счётчики захода/holding.
                _berthAttempts = 0;
                _holdingRetries = 0;
                _abortClimbRemaining = 0f;
                // T-NS-WF01b: новый leg — сбрасываем strikes обхода.
                _wallBlockStrikes = 0;
                _wallLosStrikes = 0;
                // T-NS-NAV15: новый leg — сбрасываем счётчик одной точки.
                _wallSameSpotCount = 0;
                _lastWallEntryPos = Vector3.zero;
                // T-NS-WF05: новый leg — сбрасываем cruise-stuck.
                _cruiseRecoveries = 0;
                _cruiseLastProgressAt = 0f;
                _cruiseRecoverUntil = 0f;
                // T-NS-LOG02: новый leg — сбрасываем goal-watchdog.
                _goalProgAt = 0f;
                _goalSlowReplans = 0;
                _graphParityFlip = false;
                // T-NS-NAV12a: новый leg — сбрасываем счётчик затора и scatter.
                _legStuckCount = 0;
                _scatterUntil = 0f;
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
            // T-NS-LOG02: вход в Cruising — свежий замер сближения (манёвр кончился).
            if (m == NavMode.Cruising) _goalProgAt = 0f;
            if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] NavMode {old} → {m}");
        }

        void TickLift(Rigidbody rb) {
            // T-NS-DEPART3: Departure-Chimney — набираем departClimbMeters над падом
            // (LiftStartY = уровень пада на выходе из дока), только потом Yawing/Cruising.
            float targetY = LiftStartY + departClimbMeters;
            float dy = targetY - rb.position.y;
            if (dy <= 0.1f) {
                // Достигли высоты → ищем станцию назначения
                var station = ResolveTargetStation();
                if (station.HasValue) {
                    SetCruiseTarget(station.Value);
                    SetMode(NavMode.Yawing, "lift-done");
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
                SetMode(NavMode.Cruising, "yaw-aligned");
            }
        }

        void TickCruise(Rigidbody rb) {
            if (CruiseTargetPos == Vector3.zero) {
                rb.linearVelocity = Vector3.zero;
                return;
            }
            // T-NS-NAV12a: scatter-разлёт (jailbreak): летим прямо по курсу разлёта,
            // носом по курсу; зонд и watchdog молчат (база прогресса обновляется).
            if (Time.time < _scatterUntil) {
                float sYaw = Mathf.Atan2(_scatterDir.x, _scatterDir.z) * Mathf.Rad2Deg;
                float sCur = rb.rotation.eulerAngles.y;
                float sDelta = Mathf.DeltaAngle(sCur, sYaw);
                float sStep = Mathf.Sign(sDelta) * Mathf.Min(Mathf.Abs(sDelta), MaxYawRate * Time.fixedDeltaTime);
                rb.MoveRotation(Quaternion.AngleAxis(sCur + sStep, Vector3.up));
                // T-NS-ALT01: разлёт — тоже с возвратом в границы.
                float vyScat = ClampVyToLimits(0f, rb.position.y);
                rb.linearVelocity = new Vector3(_scatterDir.x * scatterSpeed, vyScat, _scatterDir.z * scatterSpeed);
                rb.angularVelocity = Vector3.zero;
                _cruiseLastPos = rb.position;
                _cruiseLastProgressAt = Time.time;
                return;
            }
            // T-NS-NAV11: advance по точкам плана (комм-зона/пад/watchdog — по станции).
            if (_navIdx < _navPlan.Count
                && Vector3.Distance(rb.position, _navPlan[_navIdx]) < navWpTol) {
                _navIdx++;
                // T-NS-LOG02: точку взяли — свежий замер и сброс медленных replan.
                _goalProgAt = 0f;
                _goalSlowReplans = 0;
                NpcShipNavLog.Transition(gameObject.name, npcInstanceId, "Cruising", "Cruising",
                    "nav-wp", $"idx={_navIdx}/{_navPlan.Count}");
            }
            // T-NS-LOG02e: blocked-point skip — точка в упор за стеной (лог 000914:
            // подлёт на полном ходу в лицо склона к слепой tangent-точке).
            // Пропускаем, ТОЛЬКО если шорткат к следующей цели чист (проверено лучом):
            // иначе стену всё равно разбирает WallFollow. Финал (станция) не скипаем.
            if (_navIdx < _navPlan.Count - 1) {
                Vector3 wpSkip = _navPlan[_navIdx];
                float dwSkip = Vector3.Distance(rb.position, wpSkip);
                if (dwSkip > navWpTol && dwSkip < navWpTol * 3f) {
                    Vector3 nxtSkip = _navPlan[_navIdx + 1];
                    if (RayClear(rb.position, wpSkip) < dwSkip - wallProbeRadius
                        && LegClear(rb.position, nxtSkip)) {
                        _navIdx++;
                        _goalProgAt = 0f;
                        NpcShipNavLog.Transition(gameObject.name, npcInstanceId, "Cruising", "Cruising",
                            "nav-wp-skip", $"idx={_navIdx}/{_navPlan.Count}");
                    }
                }
            }
            Vector3 goal = NavGoal();
            Vector3 toTarget = goal - rb.position;
            float dist = toTarget.magnitude;
            Vector3 stToTarget = CruiseTargetPos - rb.position;
            float stDist = stToTarget.magnitude;

            // T-NS-WF05: recovery-откат (без нового NavMode — остаёмся в Cruising,
            // в лог пишем событие вручную).
            if (Time.time < _cruiseRecoverUntil) {
                // T-NS-ALT01: откат — тоже с возвратом в границы.
                float vyRec = ClampVyToLimits(0f, rb.position.y);
                rb.linearVelocity = new Vector3(
                    _cruiseRecoverDir.x * cruiseRecoverSpeed, vyRec, _cruiseRecoverDir.z * cruiseRecoverSpeed);
                rb.angularVelocity = Vector3.zero;
                _cruiseLastPos = rb.position;
                _cruiseLastProgressAt = Time.time;
                return;
            }

            // T-NS-WF11: recovery кончился без смещения (вклинён намертво, Вавилон
            // 46 с на точке) — засчитываем провал сразу, не ждём новый watchdog.
            if (_recoverArmed && Time.time >= _cruiseRecoverUntil) {
                _recoverArmed = false;
                if ((_cruiseLastPos - _recoverStartPos).magnitude < cruiseStuckDist) {
                    _cruiseRecoveries++;
                    NpcShipNavLog.Transition(gameObject.name, npcInstanceId, "Cruising", "Cruising",
                        "recovery-failed", $"#{_cruiseRecoveries}");
                }
            }

            // T-NS-WF05 + WF14: watchdog прогресса вдали от СТАНЦИИ (там медленно — законно).
            // Схватывает втирание в склон, когда зонд слеп (старт внутри коллайдера).
            // T-NS-WF14: БЕЗ врат _wallCooldownUntil (Странник 40 с на точке с одним
            // stuck: пост-обходный кулдаун душил watchdog вместе с зондом).
            // T-NS-LOG02: дыра stDist<=150 — корабль в 140 м от станции давил в скалу
            // 112 с без единого перехода (лог 205734, NPC ...001F): comm-зона не
            // резолвится, watchdog слеп. Вблизи — не recovery, а форсированный
            // Berthing (там holding/пад/watchdog доведут или уведут в divert).
            if (_cruiseLastProgressAt <= 0f) {
                _cruiseLastPos = rb.position;
                _cruiseLastProgressAt = Time.time;
            } else if ((rb.position - _cruiseLastPos).magnitude >= cruiseStuckDist) {
                _cruiseLastPos = rb.position;
                _cruiseLastProgressAt = Time.time;
            } else if (Time.time - _cruiseLastProgressAt > cruiseStuckSec) {
                // Ждём слот теснины — не срываем (у ожидания свой таймаут 60 с).
                if (stDist <= 150f && _wallWaitStartedAt <= 0f) {
                    _cruiseLastPos = rb.position;
                    _cruiseLastProgressAt = Time.time;
                    NpcShipNavLog.Transition(gameObject.name, npcInstanceId, "Cruising", "Berthing",
                        "cruise-near-stuck", $"dist={stDist:F0}m");
                    if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Cruise near-station stuck (dist {stDist:F0}m) — forcing Berthing");
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    SetMode(NavMode.Berthing, "near-stuck");
                    return;
                }
                _cruiseRecoveries++;
                    if (_cruiseRecoveries > cruiseMaxRecoveries) {
                        if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Cruise stuck ×{_cruiseRecoveries} — scatter/divert");
                        StuckDivert(rb, "cruise");
                        return;
                    }
                    // T-NS-WF06a: откат веером — первое чистое (сначала строго назад),
                    // иначе max клиренс. Слепой откат упирался в тот же склон (Сильфида).
                    _cruiseRecoverDir = ComputeRecoverDir(rb, stToTarget);
                    _cruiseRecoverUntil = Time.time + cruiseRecoverSec;
                    _recoverStartPos = rb.position;
                    _recoverArmed = true;
                    _cruiseLastPos = rb.position;
                    _cruiseLastProgressAt = Time.time;
                    // T-NS-LOG02: часы goal-watchdog НЕ сбрасываем (3 с отката назад
                    // тонут в 45-с окне; сброс голодал slow-триггер — лог 234308, 000A).
                    NpcShipNavLog.Transition(gameObject.name, npcInstanceId, "Cruising", "Cruising",
                        "cruise-stuck", $"recovery#{_cruiseRecoveries}");
                    if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Cruise stuck — backing off #{_cruiseRecoveries}");
                    return;
                }

            // T-NS-LOG02: contour-grind — движемся, но к цели не приближаемся
            // (лог 233351: 0010 +182 м за 130 с, 002B +51 м — огибают склон
            // по горизонтали к слепой точке, displacement-watchdog их не видит).
            // Не работает: в ожидании слота (свой таймаут), в окнах recovery/
            // scatter/retreat (часы сброшены их стартом), у самой цели (добирает advance).
            if (_wallWaitStartedAt <= 0f && Time.time >= _cruiseRecoverUntil
                && Time.time >= _scatterUntil && (_retreatUntil <= 0f || Time.time >= _retreatUntil)) {
                if (_goalProgAt <= 0f || _navIdx != _goalProgIdx) {
                    _goalProgDist = dist;
                    _goalProgAt = Time.time;
                    _goalProgIdx = _navIdx;
                } else if (dist > navWpTol) {
                    float closing = (_goalProgDist - dist) / Mathf.Max(1f, Time.time - _goalProgAt);
                    if (closing >= cruiseGoalCloseMin) {
                        _goalProgDist = dist;
                        _goalProgAt = Time.time;
                    } else if (Time.time - _goalProgAt > cruiseGoalStuckSec) {
                        _goalProgDist = dist;
                        _goalProgAt = Time.time;
                        if (_goalSlowReplans >= cruiseGoalMaxReplans) {
                            if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Cruise goal grind (close {closing:F1} m/s) — scatter/divert");
                            StuckDivert(rb, "cruise-goal");
                            return;
                        }
                        _goalSlowReplans++;
                        NpcShipNavLog.Transition(gameObject.name, npcInstanceId, "Cruising", "Cruising",
                            "nav-replan-slow", $"close={closing:F1}m/s#{_goalSlowReplans}");
                        if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Cruise goal grind (close {closing:F1} m/s) — replanning route");
                        // T-NS-LOG02d: петля на той же точке (лог 234308, 0031: дважды
                        // один W1) — зеркалим чёт/нечет развод графа + штрафуем
                        // провалившуюся цель в hotspot-память (legacy-скоринг тоже видит).
                        _graphParityFlip = !_graphParityFlip;
                        var tmSlow = NpcShipTrafficManager.Instance;
                        if (_navIdx < _navPlan.Count) tmSlow?.RecordHotspot(_navPlan[_navIdx]);
                        else if (tmSlow != null) tmSlow.RecordHotspot(rb.position);
                        PlanRoute(CruiseTargetPos); // replan с ближней позиции
                        // T-NS-LOG02e: свежий замер к НОВОЙ цели (базовая линия от старой
                        // цели давала фантомный отрицательный closing — лог 235552, 0031).
                        // Счётчик медленных replan НЕ сбрасываем (петля всё равно закончится).
                        _goalProgDist = Vector3.Distance(rb.position, NavGoal());
                        _goalProgAt = Time.time;
                        _goalProgIdx = _navIdx;
                        return;
                    }
                }
            }

            // Проверить: вошли ли в OuterCommZone целевой станции?
            var zone = ResolveCommZone();
            if (zone != null && Vector3.Distance(rb.position, zone.transform.position) < zone.CommRange) {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                SetMode(NavMode.Berthing, "comm-zone");
                return;
            }

            // Если уже очень близко к станции — Berthing (fallback без зоны).
            // T-NS-NAV11: пока план не пройден — страховочная сетка advance.
            if (dist < 50f) {
                if (_navIdx < _navPlan.Count) { _navIdx++; return; }
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                SetMode(NavMode.Berthing, "near-station");
                return;
            }

            // T-NS-GATE04: ворота городов (триггер — дистанция до СТАНЦИИ, не точки).
            if (useCityGates && TryEnterGateApproach(rb, stDist)) return;

            // T-NS-WF08 + WF10: вход одним веером лидара (stagger — задел на 200+).
            // Вблизи цели не зондируем — там разбирается Berthing.
            // Слот занят → защёлка hover-wait (висим до слота/таймаута, не ползём).
            if (dist > 100f && Time.time >= _wallCooldownUntil) {
                if (_wallWaitStartedAt > 0f) {
                    if (Time.time - _wallWaitStartedAt > wallWaitTimeoutSec) {
                        _wallWaitStartedAt = 0f;
                        NpcShipTrafficManager.Instance?.RecordHotspot(rb.position);
                        NpcShipNavLog.Transition(gameObject.name, npcInstanceId, "Cruising", "Cruising",
                            "wall-slot-divert", "");
                        if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Wall slot wait timeout — diverting");
                        DivertToNextStation(rb);
                        return;
                    }
                    if (Time.time >= _wallProbeNextAt) {
                        _wallProbeNextAt = Time.time + wallProbeIntervalSec;
                        int retry = CheckWallEntry(rb, toTarget, dist);
                        if (retry == 1) { _wallWaitStartedAt = 0f; _wallWaitClearStrikes = 0; return; }
                        // T-NS-WF10b: «чисто» только серией ×3 (фликер границы слота),
                        // иначе висим дальше.
                        if (retry == 0 && ++_wallWaitClearStrikes >= 3) {
                            _wallWaitStartedAt = 0f;
                            _wallWaitClearStrikes = 0;
                        } else if (retry == 2) {
                            _wallWaitClearStrikes = 0;
                        }
                    }
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    return;
                }
                if (Time.time >= _wallProbeNextAt) {
                    _wallProbeNextAt = Time.time + wallProbeIntervalSec + ProbePhaseOffset();
                    int entry = CheckWallEntry(rb, toTarget, dist);
                    if (entry == 1) return;
                    if (entry == 2) {
                        _wallWaitStartedAt = Time.time;
                        NpcShipNavLog.Transition(gameObject.name, npcInstanceId, "Cruising", "Cruising",
                            "wall-slot-wait", "");
                        if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Wall slot busy — hovering");
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        return;
                    }
                }
            } else if (_wallWaitStartedAt > 0f) {
                // Кулдаун после манёвра — ожидание сбрасываем (начнём заново).
                _wallWaitStartedAt = 0f;
            }

            Vector3 dir = toTarget.normalized;
            float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float currentYaw = rb.rotation.eulerAngles.y;
            float deltaYaw = Mathf.DeltaAngle(currentYaw, targetYaw);

            float yawStep = Mathf.Sign(deltaYaw) * Mathf.Min(Mathf.Abs(deltaYaw), MaxYawRate * Time.fixedDeltaTime);
            rb.MoveRotation(Quaternion.AngleAxis(currentYaw + yawStep, Vector3.up));

            float speed = (dist > 200f) ? CruiseSpeed : ApproachSpeed;
            float altHold = (ProfileY() + 5f - rb.position.y) * 0.5f;
            // T-NS-WF01 (D3): возврат к профилю — тем же капом, что после LOS-выхода.
            // T-NS-ALT01: поверх — возврат в глобальные границы.
            float vyCruise = ClampVyToLimits(
                Mathf.Clamp(altHold, -returnVerticalCap, returnVerticalCap), rb.position.y);
            rb.linearVelocity = new Vector3(dir.x * speed, vyCruise, dir.z * speed);
        }

        void TickBerth(Rigidbody rb) {
            // T-NS-BERTH2: аварийный набор высоты после прерывания захода.
            // Запросы пада подавлены, пока не наберём высоту: повторный заход
            // начинается сверху (почти вертикальный спуск), а не в стену.
            if (_abortClimbRemaining > 0f) {
                float step = LiftSpeed * Time.fixedDeltaTime;
                rb.linearVelocity = new Vector3(0f, LiftSpeed, 0f);
                rb.angularVelocity = Vector3.zero;
                _abortClimbRemaining -= step;
                if (_abortClimbRemaining <= 0f) {
                    _abortClimbRemaining = 0f;
                    _lastBerthDist = float.MaxValue;
                    _berthNoProgressSince = Time.time;
                    rb.linearVelocity = Vector3.zero;
                    if (_berthAttempts >= berthMaxAttempts) {
                        DivertToNextStation(rb);
                    } else if (debugMode) {
                        Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Berthing go-around #{_berthAttempts} done — re-requesting pad");
                    }
                }
                return;
            }

            // Если пад не назначен — запросить у диспетчера (с троттлингом)
            if (string.IsNullOrEmpty(AssignedPadId)) {
                if (Time.time - _lastPadAssignAttemptTime < PAD_ASSIGN_RETRY_SEC) {
                    // T-NS-BERTH2: holding — ждём пад не на месте, а на высоте
                    // station.y + holdClearance (точка считается вживую каждый тик).
                    HoverAtHoldingAltitude(rb);
                    return;
                }
                _lastPadAssignAttemptTime = Time.time;
                var padId = TryAssignPadFromDispatcher();
                if (!string.IsNullOrEmpty(padId)) {
                    AssignedPadId = padId;
                    _berthPhase = BerthPhase.Overhead; // T-NS-CORRIDOR6: новый заход — всегда сверху
                    _holdingRetries = 0;
                    _lastBerthDist = float.MaxValue;
                    _berthNoProgressSince = Time.time;
                    Vector3 padPos = ResolvePadPos();
                    if (padPos != Vector3.zero) {
                        CruiseTargetPos = padPos;
                        // T-NS-NAV11: план исполнен (долетели) — чистим, дальше ведёт Berthing.
                        _navPlan.Clear();
                        _navIdx = 0;
                    }
                } else {
                    // T-NS-BERTH2: пад не дали — считаем holding-попытки, после лимита уходим
                    // на другую станцию вместо вечного зависания.
                    _holdingRetries++;
                    if (_holdingRetries >= holdingMaxRetries) {
                        _holdingRetries = 0;
                        if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Berthing holding gave up — diverting");
                        DivertToNextStation(rb);
                        return;
                    }
                    HoverAtHoldingAltitude(rb);
                    return;
                }
            }

            if (CruiseTargetPos == Vector3.zero) {
                // T-NS-BERTH2: пад назначен, но его позиции нет (конфиг сцены) —
                // считаем назначение битым и перезапрашиваем, а не висим вечно.
                if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Berthing pad '{AssignedPadId}' has no position — re-requesting");
                AssignedPadId = null;
                _lastBerthDist = float.MaxValue;
                rb.linearVelocity = Vector3.zero;
                return;
            }

            Vector3 toTarget = CruiseTargetPos - rb.position;
            float dist = toTarget.magnitude;

            // T-NS-PADS1: stale-pad guard — окно посадки могло истечь (Update в DockingWorld
            // сметает !used по landingWindowSec) или пад отжал игрок (T-NS08 displacement).
            // Без проверки корабль вслепую летит на чужой пад и докится поверх (dist < 1.5).
            var dwInstance = Docking.Core.DockingWorld.Instance;
            var shipForPad = GetComponent<ShipController>();
            if (dwInstance == null || shipForPad == null ||
                !dwInstance.GetAssignment(npcInstanceId, shipForPad.NetworkObjectId).HasValue) {
                if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Berthing pad stale (expired/displaced) — re-requesting");
                AssignedPadId = null;
                _lastBerthDist = float.MaxValue;
                rb.linearVelocity = Vector3.zero;
                return;
            }

            // T-NS-BERTH2: watchdog — сближение есть → прогресс (и продление окна посадки
            // из T-NS-PADS1); сближения нет дольше berthWatchdogSec → прерываем заход.
            if (dist < _lastBerthDist - berthMinProgressMeters) {
                _lastBerthDist = dist;
                _berthNoProgressSince = Time.time;
                dwInstance.RefreshNpcAssignment(npcInstanceId, shipForPad.NetworkObjectId);
            } else if (Time.time - _berthNoProgressSince > berthWatchdogSec) {
                if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Berthing watchdog: no progress for {berthWatchdogSec:F0}s at dist {dist:F1}m — aborting");
                AbortBerthApproach(rb);
                return;
            }

            // M3.2.12 + T-NS-NAV11: dist<50 — финиш только если план пройден
            // (иначе страховочная сетка advance — точки обычно берёт navWpTol выше).
            bool canDock = !string.IsNullOrEmpty(AssignedPadId) ? dist < 1.5f : dist < 3f;
            if (canDock) {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                var ship = GetComponent<ShipController>();
                // T-NS-PADS1: NPC-подтверждение касания — выставить used=true.
                // ConfirmTouchdown раньше вызывался только из игрокового RPC-пути
                // (DockingServer), поэтому окно NPC всегда истекало задним числом.
                // stationId для used-флага не критичен (поиск идёт по shipNetId+padId).
                var berthState = NpcShipWorld.Instance?.GetNpc(npcInstanceId);
                string berthStationId = berthState != null ? berthState.CurrentRoute.toLocationId : string.Empty;
                dwInstance.ConfirmTouchdown(npcInstanceId, ship.NetworkObjectId, AssignedPadId, berthStationId ?? string.Empty);
                ship.EnterDocked();
                SetMode(NavMode.Docked, "touchdown");
                return;
            }

            // T-NS-CORRIDOR6: заход через overhead-ворота (pad + clearance) + вертикальный
            // спуск в «трубе». Вместо слепой прямой через геометрию — горизонтальный подлёт
            // наверху (где свободно) и строго вертикальный финал. Watchdog/stale-guard выше
            // работают по 3D-дистанции до пада без изменений.
            Vector3 padPosCorridor = CruiseTargetPos; // = позиция пада (ставится при назначении)
            // T-NS-LOG02e: эскалация ворот — пад под козырьком/в кармане (лог 235552:
            // 0020/0012/0032 трижды clear=const + abort): каждая попытка заходит выше
            // на abortClimbMeters, вместо identical-retry в ту же стену. Вертикаль над
            // падом, не над пиком — лор цел. После berthMaxAttempts — divert как раньше.
            float gateExtra = abortClimbMeters * _berthAttempts;
            Vector3 gatePos = new Vector3(padPosCorridor.x, padPosCorridor.y + overheadClearanceMeters + gateExtra, padPosCorridor.z);
            Vector3 toPadFlat = new Vector3(padPosCorridor.x - rb.position.x, 0f, padPosCorridor.z - rb.position.z);
            float flatDist = toPadFlat.magnitude;

            if (_berthPhase == BerthPhase.Overhead) {
                // T-NS-DOCK01: зонд в Overhead — стена на пути к воротам видна сразу,
                // уходим на go-around, не ждём 20 с watchdog'а. Stagger по кораблям.
                if (berthOverheadProbe && Time.time >= _berthProbeNextAt) {
                    _berthProbeNextAt = Time.time + 0.5f + ProbePhaseOffset();
                    float gateDist0 = Vector3.Distance(rb.position, gatePos);
                    float gateClear = RayClear(rb.position, gatePos);
                    if (gateClear < Mathf.Min(berthOverheadProbeDist, Mathf.Max(0f, gateDist0 - 1f))) {
                        NpcShipNavLog.Transition(gameObject.name, npcInstanceId, "Berthing", "Berthing",
                            "berth-overhead-blocked", $"clear={gateClear:F0}m");
                        if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Berthing overhead blocked (clear {gateClear:F0}m) — aborting");
                        AbortBerthApproach(rb);
                        return;
                    }
                }
                if (flatDist < corridorRadiusMeters
                    && Mathf.Abs(rb.position.y - gatePos.y) < corridorGateToleranceMeters) {
                    _berthPhase = BerthPhase.Descend;
                    if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Berthing corridor: Overhead → Descend");
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    return;
                }
                // К воротам — та же скоростная формула, что была у прямой (без сюрпризов).
                Vector3 toGate = gatePos - rb.position;
                float gateDist = toGate.magnitude;
                if (gateDist < 0.01f) {
                    rb.linearVelocity = Vector3.zero;
                    return;
                }
                float gateSpeed = Mathf.Min(ApproachSpeed, gateDist * 2f);
                rb.linearVelocity = toGate.normalized * gateSpeed;
                rb.angularVelocity = Vector3.zero;
                return;
            }

            // Descend: строго вертикальный спуск + capped lateral-коррекция (держит «трубу»).
            // Выпали из трубы (сдвинули корпусом) — вернуться в Overhead, а не тянуть диагональ.
            if (flatDist > corridorRadiusMeters * 1.5f) {
                _berthPhase = BerthPhase.Overhead;
                if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Berthing corridor: out of pipe — back to Overhead");
                return;
            }
            float dyAbovePad = rb.position.y - padPosCorridor.y;
            float descendSpeed = dyAbovePad > 0f ? -Mathf.Min(ApproachSpeed, Mathf.Max(1f, dyAbovePad * 0.5f)) : 0f;
            Vector3 lateral = flatDist > 0.01f ? toPadFlat.normalized * Mathf.Min(descendLateralCap, flatDist * 0.5f) : Vector3.zero;
            rb.linearVelocity = new Vector3(lateral.x, descendSpeed, lateral.z);
            rb.angularVelocity = Vector3.zero;
        }

        // === T-NS-BERTH2: helpers — прерывание захода, divert, holding ===

        /// <summary>
        /// Прервать заход: освободить пад, начать относительный набор высоты.
        /// ReleaseAssignment напрямую (не ReleaseNpcAssignment): корабль в полёте,
        /// ExitDocked-паттерн с _lastUndockTime здесь не нужен.
        /// </summary>
        void AbortBerthApproach(Rigidbody rb) {
            // T-NS-LOG01: смены режима нет — пишем событие вручную (видно в SUMMARY).
            NpcShipNavLog.Transition(gameObject.name, npcInstanceId, "Berthing", "Berthing",
                "berth-abort", $"attempt={_berthAttempts + 1}");
            _berthAttempts++;
            var dw = Docking.Core.DockingWorld.Instance;
            var ship = GetComponent<ShipController>();
            if (dw != null && ship != null) dw.ReleaseAssignment(npcInstanceId, ship.NetworkObjectId);
            AssignedPadId = null;
            _lastBerthDist = float.MaxValue;
            _abortClimbRemaining = abortClimbMeters;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        /// <summary>
        /// T-NS-NAV12a: stuck-диагноз затора. Первый за leg — scatter (радиальный
        /// разлёт с чистого места, leg продолжается); второй — divert (разлёт не помог).
        /// </summary>
        void StuckDivert(Rigidbody rb, string cause) {
            _legStuckCount++;
            _cruiseRecoveries = 0;
            _goalProgAt = 0f; // T-NS-LOG02: после scatter/divert — свежий замер
            // T-NS-NAV12b: точка затора — в hotspot-память (навигатор обойдёт впредь).
            NpcShipTrafficManager.Instance?.RecordHotspot(rb.position);
            ResetWallState(); // слот отпустить в любом случае
            if (_legStuckCount >= 2) {
                if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Stuck ×{_legStuckCount} ({cause}) — diverting");
                _legStuckCount = 0;
                if (CurrentMode == NavMode.Cruising)
                    NpcShipNavLog.Transition(gameObject.name, npcInstanceId, "Cruising", "Cruising",
                        "stuck-divert", cause);
                else SetMode(NavMode.Cruising, "stuck-divert", cause);
                DivertToNextStation(rb);
                return;
            }
            // Jailbreak: разлёт от соседей + в просвет лидара, зонд и watchdog молчат.
            _scatterDir = ComputeScatterDir(rb);
            _scatterUntil = Time.time + scatterSec;
            _wallCooldownUntil = _scatterUntil + 5f;
            _cruiseLastPos = rb.position;
            _cruiseLastProgressAt = Time.time;
            if (CurrentMode == NavMode.Cruising)
                NpcShipNavLog.Transition(gameObject.name, npcInstanceId, "Cruising", "Cruising",
                    "scatter", cause);
            else SetMode(NavMode.Cruising, "scatter", cause);
            if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Stuck ({cause}) — scattering");
        }

        /// <summary>
        /// T-NS-NAV12a: направление разлёта — от ближайшего соседа, проверенное
        /// лидаром (чтобы не разлететься в соседнюю скалу). Никого рядом —
        /// чистый просвет лидара. Горизонталь (лор: без набора высоты).
        /// </summary>
        Vector3 ComputeScatterDir(Rigidbody rb) {
            Vector3 away = Vector3.zero;
            float bestD = float.MaxValue;
            var near = NpcShipZoneRegistry.QueryNearby(rb.position, 500f);
            for (int i = 0; i < near.Count; i++) {
                var o = near[i];
                if (o == null || o == this) continue;
                float d = Vector3.Distance(rb.position, o.transform.position);
                if (d < bestD) { bestD = d; away = rb.position - o.transform.position; }
            }
            away.y = 0f;
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            else fwd.Normalize();
            if (away.sqrMagnitude < 0.001f) {
                LidarScan(rb.position, fwd, scatterSpeed * scatterSec + wallClearance,
                    fwd, out Vector3 open, out _, out _, out _, out _);
                return open;
            }
            away.Normalize();
            float look = scatterSpeed * scatterSec + wallClearance;
            LidarScan(rb.position, away, look, away, out Vector3 best, out _, out _, out _, out _);
            return best;
        }

        /// <summary>
        /// Уйти на другую станцию после исчерпания попыток: следующий leg расписания
        /// (пинг-понг routes[0]) + Cruising с набранной высоты. Станции нет — висеть
        /// на месте, режим не менять (не strandим корабль).
        /// </summary>
        void DivertToNextStation(Rigidbody rb) {
            _berthAttempts = 0;
            _holdingRetries = 0;
            _abortClimbRemaining = 0f;
            // T-NS-WF10: уходим на другую станцию — слот не держим (страховка,
            // ResetWallState уже отпустил, но divert зовётся и из Berthing).
            NpcShipTrafficManager.Instance?.ReleaseWallSlot(npcInstanceId);
            _lastBerthDist = float.MaxValue;
            _berthNoProgressSince = Time.time;
            AdvanceScheduleForCurrentNpc();
            var station = ResolveTargetStation();
            if (station.HasValue) {
                SetCruiseTarget(station.Value);
                SetMode(NavMode.Cruising, "divert");
                if (debugMode) {
                    var st = NpcShipWorld.Instance?.GetNpc(npcInstanceId);
                    Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Diverting to {(st != null ? st.CurrentRoute.toLocationId : "?")}");
                }
            } else {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Divert failed: no station — holding");
            }
        }

        /// <summary>
        /// Holding-зависание: вертикальная «труба» к station.y + holdClearance.
        /// Точка считается вживую каждый тик — мировых Vector3 не храним (F8-безопасно).
        /// </summary>
        void HoverAtHoldingAltitude(Rigidbody rb) {
            var station = ResolveTargetStation();
            if (!station.HasValue) {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                return;
            }
            float targetY = station.Value.y + holdClearanceMeters;
            float dy = targetY - rb.position.y;
            // T-NS-ALT01: holding — тоже внутри границ.
            float vy = ClampVyToLimits(dy > 2f ? LiftSpeed : dy < -2f ? -2f : 0f, rb.position.y);
            rb.linearVelocity = new Vector3(0f, vy, 0f);
            rb.angularVelocity = Vector3.zero;
        }

        Vector3? ResolveTargetStation() {
            var state = NpcShipWorld.Instance?.GetNpc(npcInstanceId);
            if (state == null || string.IsNullOrEmpty(state.CurrentRoute.toLocationId)) return null;
            var station = Docking.Network.DockingZoneRegistry.GetByLocation(state.CurrentRoute.toLocationId);
            if (station == null) return null;
            return station.transform.position;
        }

        /// <summary>
        /// T-NS-WF06c: станция, где стоим (fromLocationId после advance = текущий leg).
        /// Позиция резолвится вживую из реестра — мировых кэшей нет (F8-безопасно).
        /// </summary>
        Vector3? ResolveCurrentStation() {
            var state = NpcShipWorld.Instance?.GetNpc(npcInstanceId);
            if (state == null || string.IsNullOrEmpty(state.CurrentRoute.fromLocationId)) return null;
            var station = Docking.Network.DockingZoneRegistry.GetByLocation(state.CurrentRoute.fromLocationId);
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
            int len = schedule.routes.Length;

            // T-NS-ROUTES5: один leg (классика M3.2) — пинг-понг туда-обратно, бит-в-бит как раньше.
            if (len == 1) {
                var route = schedule.routes[0];
                state.ScheduleIndex++;
                ProjectC.PeacefulShip.Core.NpcShipRoute next;
                if (state.ScheduleIndex % 2 == 1) {
                    next = new ProjectC.PeacefulShip.Core.NpcShipRoute {
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
                    next = route;
                }
                // Guard: цели нет в реестре — остаёмся на текущем leg, не strandим в hover.
                if (!HasStation(next.toLocationId)) {
                    state.ScheduleIndex--;
                    if (debugMode) Debug.LogWarning($"[NpcShipController:NPC:{npcInstanceId:X}] Schedule advance blocked: no station '{next.toLocationId}' — staying");
                    _scheduleAdvancedAfterDock = true;  // M3.2.11: не дать Docked handlerу advance снова
                    return;
                }
                state.CurrentRoute = next;
                _scheduleAdvancedAfterDock = true;  // M3.2.11: не дать Docked handlerу advance снова
                if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Schedule advanced to {state.CurrentRoute.toLocationId}");
                return;
            }

            // T-NS-ROUTES5: мульти-leg — последовательный обход (Loop; RoundTrip с >1 leg —
            // замкнутая цепочка как у Courier) или случайный вход (RandomFromPool).
            // Legs в неизвестные станции пропускаем; если битые все — остаёмся, не strandим.
            bool random = schedule.scheduleType == NpcShipSchedule.ScheduleType.RandomFromPool;
            int start = random ? Random.Range(0, len) : (state.ScheduleIndex + 1) % len;
            for (int attempt = 0; attempt < len; attempt++) {
                int idx = (start + attempt) % len;
                var candidate = schedule.routes[idx];
                if (HasStation(candidate.toLocationId)) {
                    state.ScheduleIndex = idx;
                    state.CurrentRoute = candidate;
                    _scheduleAdvancedAfterDock = true;  // M3.2.11: не дать Docked handlerу advance снова
                    if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Schedule advanced to {state.CurrentRoute.toLocationId} (leg {idx})");
                    return;
                }
            }
            if (debugMode) Debug.LogWarning($"[NpcShipController:NPC:{npcInstanceId:X}] Schedule advance blocked: no known stations in {len} legs — staying");
            _scheduleAdvancedAfterDock = true;  // M3.2.11: не дать Docked handlerу advance снова
        }

        /// <summary>T-NS-ROUTES5: есть ли станция с таким locationId в реестре (защита от strand).</summary>
        bool HasStation(string locationId) {
            if (string.IsNullOrEmpty(locationId)) return false;
            return Docking.Network.DockingZoneRegistry.GetByLocation(locationId) != null;
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
                      $"buyConfigured={trade.buyConfiguredItemsAfterSell} randomTrade={trade.randomTradeItems} " +
                      $"unlimited={trade.useUnlimitedCredits} " +
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

        /// <summary>Raycast-веер для поиска самого открытого направления (выход из Π-доков).</summary>
        Vector3 ComputeEscapeDir(Vector3 pos)
        {
            Vector3 bestDir = Vector3.zero;
            float bestDist = 0f;
            Vector3 origin = pos + Vector3.up * 2f;

            for (int i = 0; i < avoidEscapeRays; i++)
            {
                float angle = i * (360f / avoidEscapeRays) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                if (Physics.Raycast(origin, dir, out var hit, avoidEscapeMaxDist))
                {
                    if (hit.distance > bestDist) { bestDist = hit.distance; bestDir = dir; }
                }
                else if (avoidEscapeMaxDist > bestDist)
                {
                    bestDist = avoidEscapeMaxDist; bestDir = dir;
                }
            }
            return bestDir.sqrMagnitude > 0.001f ? bestDir : Vector3.zero;
        }

        void EnterAvoid(Rigidbody rb, NpcShipController other) {
            _resumeMode = (CurrentMode == NavMode.Avoiding || CurrentMode == NavMode.AvoidYield)
                ? NavMode.Cruising : CurrentMode;
            _avoidOther = other;
            _avoidBuild = null;
            _avoidFromPos = other.ProximityZone?.ClosestPoint(rb.position) ?? other.transform.position;
            _escapeDir = ComputeEscapeDir(rb.position);
            // T-NS-WF04b: из обхода запоминаем касательную — манёвр не отбросит назад в кучу.
            _avoidHasWallDir = (CurrentMode == NavMode.WallFollow);
            if (_avoidHasWallDir) _avoidWallDir = _wallMoveDir;

            // T-NS-BZ05: приоритет — выше делает full avoidance, ниже yield'ит
            if (AvoidancePriority < other.AvoidancePriority)
            {
                if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] YIELD to {other.NpcInstanceId:X} " +
                    $"(myPrio={AvoidancePriority} otherPrio={other.AvoidancePriority})");
                _avoidPhaseEnteredAt = Time.time;
                _avoidStartedAt = Time.time;
                SetMode(NavMode.AvoidYield, "yield-ship", $"vs={other.gameObject.name}:{other.NpcInstanceId:X}");
                return;
            }

            if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] AVOID {other.NpcInstanceId:X} " +
                $"(myPrio={AvoidancePriority} otherPrio={other.AvoidancePriority})");
            _avoidPhase = AvoidPhase.Separate;
            _avoidPhaseEnteredAt = Time.time;
            _avoidStartedAt = Time.time;
            CountAvoidCycle();
            // T-NS-WF07: второй участник в detail — видно пары (кто кого держит).
            SetMode(NavMode.Avoiding, "avoid-ship", $"vs={other.gameObject.name}:{other.NpcInstanceId:X}");
        }

        void EnterAvoid(Rigidbody rb, NpcProximityZoneBuilds build) {
            _resumeMode = (CurrentMode == NavMode.Avoiding || CurrentMode == NavMode.AvoidYield)
                ? NavMode.Cruising : CurrentMode;
            _avoidOther = null;
            _avoidBuild = build;
            _avoidFromPos = build.ClosestPoint(rb.position);
            _escapeDir = ComputeEscapeDir(rb.position);
            // Здания всегда имеют приоритет 0 → корабль всегда делает full avoidance
            _avoidPhase = AvoidPhase.Separate;
            _avoidPhaseEnteredAt = Time.time;
            _avoidStartedAt = Time.time;
            CountAvoidCycle();
            SetMode(NavMode.Avoiding, "avoid-build");
        }

        /// <summary>
        /// T-NS-AVOID4: входы в avoidance внутри окна — одна серия затора (счётчик для эскалации).
        /// </summary>
        void CountAvoidCycle() {
            if (Time.time - _lastAvoidResumeAt < avoidEscalationWindowSec) _avoidCycles++;
            else _avoidCycles = 1;
        }

        /// <summary>Yield: низкий приоритет — стоим и ждём пока high-priority корабль уедет.</summary>
        void TickAvoidYield(Rigidbody rb) {
            if (Time.time - _avoidStartedAt > avoidTimeout) { ResumeFromAvoid(rb, false); return; }
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            if (IsClearOfConflict()) ResumeFromAvoid(rb, true);
        }

        void TickAvoid(Rigidbody rb) {
            // Предохранитель — не зависаем в манёвре
            if (Time.time - _avoidStartedAt > avoidTimeout) { ResumeFromAvoid(rb, false); return; }

            // Горизонтальный вектор "от препятствия"
            Vector3 away = rb.position - _avoidFromPos;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            away.Normalize();

            // T-NS-BZ07: blend с escape-направлением (выход из Π-доков).
            // T-NS-WF04b: из WallFollow — blend с касательной стены (прогресс
            // вдоль склона вместо отскока назад в кучу).
            Vector3 moveDir = away;
            if (_avoidHasWallDir && _avoidWallDir.sqrMagnitude > 0.001f)
                moveDir = Vector3.Lerp(away, _avoidWallDir.normalized, avoidWallBlend).normalized;
            else if (_escapeDir.sqrMagnitude > 0.001f)
                moveDir = Vector3.Lerp(away, _escapeDir, avoidEscapeBlend).normalized;

            float t = Time.time - _avoidPhaseEnteredAt;
            switch (_avoidPhase) {
                case AvoidPhase.Separate:
                    // T-NS-AVOID4: эскалация — горизонтально не разошлись за серию
                    // входов → уходим вверх, где свободно (замер R11: 300 м+ над падами).
                    if (_avoidCycles >= avoidEscalateAfter) {
                        rb.linearVelocity = new Vector3(0f, LiftSpeed, 0f);
                    } else {
                        rb.linearVelocity = new Vector3(moveDir.x * avoidSeparateSpeed, 0f, moveDir.z * avoidSeparateSpeed);
                    }
                    rb.angularVelocity = Vector3.zero;
                    if (t >= avoidSeparateTime) { _avoidPhase = AvoidPhase.Stop; _avoidPhaseEnteredAt = Time.time; }
                    break;
                case AvoidPhase.Stop:
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    if (t >= avoidStopTime) { _avoidPhase = AvoidPhase.BackOff; _avoidPhaseEnteredAt = Time.time; }
                    break;
                case AvoidPhase.BackOff:
                    rb.linearVelocity = new Vector3(moveDir.x * avoidBackOffSpeed, 0f, moveDir.z * avoidBackOffSpeed);
                    rb.angularVelocity = Vector3.zero;
                    if (t >= avoidBackOffTime) {
                        if (IsClearOfConflict()) ResumeFromAvoid(rb, true);
                        else { _avoidPhase = AvoidPhase.Separate; _avoidPhaseEnteredAt = Time.time; }
                    }
                    break;
            }
        }

        bool IsClearOfConflict() {
            if (_avoidOther != null) {
                var pz = ProximityZone;
                var oz = _avoidOther.ProximityZone;
                if (pz == null || oz == null) return true;
                return pz.IsClearOf(oz);
            }
            if (_avoidBuild != null) {
                var pz = ProximityZone;
                if (pz == null) return true;
                return pz.IsClearOf(_avoidBuild);
            }
            return true;
        }

        /// <summary>
        /// T-NS-AVOID4: выход из манёвра с разрывом петли: cooldown на новые конфликты,
        /// при чистом выходе — сброс счётчика серии затора.
        /// </summary>
        void ResumeFromAvoid(Rigidbody rb, bool cleared) {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            _avoidOther = null;
            _avoidBuild = null;
            _avoidHasWallDir = false;
            _lastAvoidResumeAt = Time.time;
            // T-NS-WF04: после таймаута — длинный кулдаун: корабль уходит прямо
            // из кучи, а не клюёт соседей через 2.5 с (пинг-понг из лога).
            _avoidCooldownUntil = Time.time + (cleared ? avoidCooldownSec : avoidTimeoutCooldownSec);
            if (cleared) _avoidCycles = 0;
            // Возврат на прошлый маршрут: прежний режим + прежняя CruiseTargetPos (не менялась)
            SetMode(_resumeMode == NavMode.Avoiding || _resumeMode == NavMode.AvoidYield ? NavMode.Cruising : _resumeMode,
                cleared ? "avoid-clear" : "avoid-timeout");
        }

        // === T-NS-WF01: wall-follow — латеральный обход гор/скал ===

        /// <summary>Сдвиг фазы проб по кораблям (stagger): не все зондируют в один тик.</summary>
        float ProbePhaseOffset() => (float)(npcInstanceId % 10UL) * 0.02f;

        /// <summary>Дальность зонда от скорости (с клампом).</summary>
        float WallLookAhead() => Mathf.Clamp(CruiseSpeed * wallLookAheadSec, wallLookAheadMin, wallLookAheadMax);

        /// <summary>
        /// Фильтр попаданий зонда: корабли (свой/чужой) — не стены, их ведёт proximity.
        /// Триггеры уже отсечены QueryTriggerInteraction.Ignore. Террейн и non-convex
        /// меши бьются лучом без ограничений (в отличие от Collider.ClosestPoint).
        /// </summary>
        bool IsWallHit(RaycastHit hit) {
            return IsWallCollider(hit.collider);
        }

        /// <summary>Тот же фильтр стен, но по коллайдеру (для Overlap-проверок).</summary>
        bool IsWallCollider(Collider c) {
            if (c == null) return false;
            if (c.GetComponentInParent<ShipController>() != null) return false;
            if (c.GetComponentInParent<NpcShipController>() != null) return false;
            return true;
        }

        /// <summary>
        /// T-NS-WF08: «лидар» — веер тонких лучей вокруг курса к цели (горизонталь).
        /// Возвращает лучшее направление (VFH-lite): клиренс − отклонение от цели
        /// + инерция прошлого курса (держит сторону обхода без rule-of-side).
        /// centerClear/minClear — эффективные (минус wallClearance на габарит корпуса).
        /// Корабли в веере — не стены (их ведёт proximity): считаются чистыми.
        /// </summary>
        void LidarScan(Vector3 pos, Vector3 goalDir, float look, Vector3 keepDir,
            out Vector3 bestDir, out float centerClear, out float minClear, out float downClear,
            out float flatMin) {
            Vector3 origin = pos + Vector3.up * 2f;
            bestDir = goalDir;
            centerClear = look;
            minClear = look;
            flatMin = look;
            float bestScore = float.MinValue;
            float devW = 1f - lidarClearWeight;
            float half = Mathf.Max(1f, lidarHalfAngle);
            Vector3 kn = keepDir.sqrMagnitude > 0.001f ? keepDir.normalized : goalDir;
            for (float a = -lidarHalfAngle; a <= lidarHalfAngle + 0.01f; a += lidarStepDeg) {
                Vector3 d = Quaternion.AngleAxis(a, Vector3.up) * goalDir;
                float raw = look;
                if (Physics.Raycast(origin, d, out var hit, look,
                        wallObstacleMask, QueryTriggerInteraction.Ignore) && IsWallHit(hit))
                    raw = hit.distance;
                float eff = Mathf.Max(0f, raw - wallClearance); // габарит корпуса
                if (Mathf.Abs(a) < 0.01f) centerClear = eff;
                if (eff < minClear) minClear = eff;
                if (eff < flatMin) flatMin = eff;
                float score = lidarClearWeight * (eff / look)
                    - devW * (Mathf.Abs(a) / half)
                    + lidarKeepWeight * Vector3.Dot(d, kn);
                if (score > bestScore) { bestScore = score; bestDir = d; }
            }
            // T-NS-WF12: нижний луч (детекция only, в steering не участвует):
            // склон/земля впереди-снизу. Идёт в minClear (триггер входа).
            // Знак плюс: вокруг оси (up × goal) положительный угол кладёт луч вниз.
            Vector3 crossAxis = Vector3.Cross(Vector3.up, goalDir);
            Vector3 downDir = crossAxis.sqrMagnitude > 0.000001f
                ? Quaternion.AngleAxis(lidarDownPitchDeg, crossAxis.normalized) * goalDir
                : Vector3.zero;
            // Вырожденный случай (goalDir вертикален) — луча нет.
            downClear = look;
            if (downDir.sqrMagnitude > 0.001f) {
                downDir.Normalize();
                float draw = look;
                if (Physics.Raycast(origin, downDir, out var dhit, look,
                        wallObstacleMask, QueryTriggerInteraction.Ignore) && IsWallHit(dhit))
                    draw = dhit.distance;
                downClear = Mathf.Max(0f, draw - wallClearance);
                if (downClear < minClear) minClear = downClear;
            }
        }

        /// <summary>
        /// T-NS-WF08 + WF10: проверка входа в обход одним веером (Cruising + CorridorLeg).
        /// Центр забит → серия; бок вплотную → сразу (зацеп кромки).
        /// Возврат: 0 = чисто, 1 = вошли, 2 = ждать слот (теснина занята).
        /// </summary>
        int CheckWallEntry(Rigidbody rb, Vector3 toTarget, float dist) {
            Vector3 dirN = toTarget / dist;
            Vector3 fwdN = new Vector3(dirN.x, 0f, dirN.z);
            if (fwdN.sqrMagnitude < 0.001f) { _wallBlockStrikes = 0; return 0; }
            fwdN.Normalize();
            float lookN = Mathf.Min(WallLookAhead(), dist);
            LidarScan(rb.position, fwdN, lookN, fwdN,
                out _, out float centerN, out _, out float downN, out float flatN);
            // T-NS-WF14: бок — горизонталь; низ — отдельным ближним порогом
            // (далёкая земля не повод для обхода).
            bool enter = flatN < lidarSideTrigger || downN < lidarDownTrigger;
            if (!enter) {
                // T-NS-WF09: центр — только внутренняя доля lookahead (дальние
                // скалы на краю не дёргают в обход каждые 8 с).
                if (centerN < lookN * lidarCenterFraction) enter = (++_wallBlockStrikes >= wallEnterStrikes);
                else _wallBlockStrikes = 0;
            }
            if (!enter) return 0;
            _wallBlockStrikes = 0;
            // T-NS-WF10: слот теснины — в воронке только один (иначе танец кучи).
            var tm = NpcShipTrafficManager.Instance;
            if (tm != null && !tm.TryAcquireWallSlot(npcInstanceId, rb.position, wallSlotRadius))
                return 2;
            _wallWaitStartedAt = 0f;
            return TryEnterWallFollow(rb, toTarget, dist) ? 1 : 0;
        }

        // T-NS-WF10: ожидание слота инлайнится в TickCruise/TickCorridorLeg
        // (защёлка _wallWaitStartedAt) — отдельный метод не нужен, нового NavMode нет.

        /// <summary>
        /// Вход в обход из Cruising/CorridorLeg. Возврат — в тот же режим (_wallReturnMode).
        /// Вертикаль замораживается на высоте входа (лор: облетаем, не перелетаем).
        /// Сторону держит keep-член скоринга лидара (rule-of-side больше не нужен).
        /// </summary>
        bool TryEnterWallFollow(Rigidbody rb, Vector3 toTarget, float dist) {
            Vector3 dir = toTarget / dist;
            Vector3 fwd = new Vector3(dir.x, 0f, dir.z);
            if (fwd.sqrMagnitude < 0.001f) return false;
            fwd.Normalize();
            // T-NS-NAV15: та же вмятина в N-й раз — не эпизод, а переплан с места.
            if (_lastWallEntryPos.sqrMagnitude > 0.001f
                && Vector3.Distance(rb.position, _lastWallEntryPos) < wallSameSpotRadius)
                _wallSameSpotCount++;
            else _wallSameSpotCount = 1;
            _lastWallEntryPos = rb.position;
            if (_wallSameSpotCount >= wallSameSpotEntries) {
                _wallSameSpotCount = 0;
                NpcShipNavLog.Transition(gameObject.name, npcInstanceId,
                    CurrentMode.ToString(), CurrentMode.ToString(), "nav-replan", "");
                if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Same dent ×{wallSameSpotEntries} — replanning route");
                PlanRoute(CruiseTargetPos);
                return false;
            }
            _wallEntryY = rb.position.y;
            _wallEntryDist = dist;
            _wallStartedAt = Time.time;
            _wallTurnAccum = 0f;
            _wallLastBearing = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            _wallLastPos = rb.position;
            _wallLastProgressAt = Time.time;
            _wallProbeNextAt = Time.time + wallProbeIntervalSec;
            _wallLosStrikes = 0;
            _downClear = float.MaxValue;
            _wallEntryDir = fwd; // T-NS-WF17: помним вход — из ловушки выходим назад по нему
            _wallMinClear = float.MaxValue; // T-NS-WF18: до первого скана — открытое небо, полный ход
            _retreatUntil = 0f; // сброс просрочки (иначе новый эпизод сразу «закончит» retreat)
            _retreatDir = Vector3.zero;
            _wallMoveDir = fwd;
            _wallReturnMode = (CurrentMode == NavMode.CorridorLeg || CurrentMode == NavMode.GateApproach)
                ? NavMode.CorridorLeg : NavMode.Cruising;
            if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Cruising → WallFollow (lidar)");
            SetMode(NavMode.WallFollow, "probe");
            return true;
        }

        private Vector3 _wallMoveDir = Vector3.forward;
        private NavMode _wallReturnMode = NavMode.Cruising; // enum — F8-безопасно

        void TickWallFollow(Rigidbody rb) {
            if (CruiseTargetPos == Vector3.zero) { ResumeWallFollow(rb); return; }
            // T-NS-NAV11: в обходе цель — точка плана (станция, если план пройден).
            Vector3 wgoal = NavGoal();
            Vector3 toTarget = wgoal - rb.position;
            float dist = toTarget.magnitude;
            // Вблизи цели выходим — дальше Berthing (fallback как в TickCruise).
            if (dist < 50f) { ResumeWallFollow(rb); return; }

            // Предохранитель времени → divert на другую станцию.
            if (Time.time - _wallStartedAt > wallTimeoutSec) {
                if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] WallFollow timeout — diverting");
                // T-NS-WF07: причина divert в логе (timeout/loop/stuck).
                NpcShipNavLog.Transition(gameObject.name, npcInstanceId, "WallFollow", "WallFollow",
                    "wall-divert-timeout", $"t={Time.time - _wallStartedAt:F0}s");
                ResetWallState();
                DivertToNextStation(rb);
                return;
            }
            // T-NS-WF17: retreat идёт — азимут не копим (разворот назад законный).
            bool retreating = Time.time < _retreatUntil;
            if (retreating) {
                _wallMoveDir = _retreatDir;
            } else {
                // Накопленный разворот азимута: полный круг без LOS = кольцевая гора → divert.
                float bearing = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
                _wallTurnAccum += Mathf.Abs(Mathf.DeltaAngle(_wallLastBearing, bearing));
                _wallLastBearing = bearing;
            }
            if (_wallTurnAccum >= 360f) {
                if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] WallFollow loop (360°) — diverting");
                NpcShipNavLog.Transition(gameObject.name, npcInstanceId, "WallFollow", "WallFollow",
                    "wall-divert-loop", "");
                ResetWallState();
                DivertToNextStation(rb);
                return;
            }
            // T-NS-WF09: нет приближения к цели за wallProgressSec (ёрзание на месте,
            // Жук 42 с / Летучий 59 с) → divert. Displacement-watchdog ниже остаётся.
            if (Time.time - _wallStartedAt > wallProgressSec
                && _wallEntryDist - dist < wallProgressMin) {
                if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] WallFollow no target progress — scatter/divert");
                StuckDivert(rb, "wall-noprogress");
                return;
            }
            // T-NS-WF17: вогнутая ловушка — крутимся без приближения к цели:
            // назад по входу (оттуда прилетели — там чисто), потом replan.
            // Подъём по стене больше не маскирует стояние (см. horizontal ниже).
            if (!retreating && _wallTurnAccum >= wallRetreatTurnDeg
                && Time.time - _wallStartedAt > 15f && _wallEntryDist - dist < 50f) {
                Vector3 back = -_wallEntryDir;
                back.y = 0f;
                // T-NS-WF17b: fallback — текущий курс (fwd тут вне скоупа).
                if (back.sqrMagnitude < 0.001f) back = transform.forward;
                back.y = 0f;
                if (back.sqrMagnitude < 0.001f) back = Vector3.back;
                back.Normalize();
                _retreatDir = back;
                _retreatUntil = Time.time + wallRetreatSec;
                retreating = true;
                _wallMoveDir = _retreatDir;
                NpcShipNavLog.Transition(gameObject.name, npcInstanceId, "WallFollow", "WallFollow",
                    "wall-retreat", "");
                if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Concave trap — retreating along entry");
            }
            // Притирание к склону: нет ГОРИЗОНТАЛЬНОГО смещения дольше wallNoProgressSec → divert.
            Vector3 flatPos = new Vector3(rb.position.x, 0f, rb.position.z);
            Vector3 flatLast = new Vector3(_wallLastPos.x, 0f, _wallLastPos.z);
            float flatMoved = (flatPos - flatLast).magnitude;
            if (flatMoved >= wallMinProgressMeters) {
                _wallLastPos = rb.position;
                _wallLastProgressAt = Time.time;
            } else if (Time.time - _wallLastProgressAt > 5f && flatMoved < 1f) {
                // T-NS-WF18: мёртвое стояние (Шмель 0.1 м/с 15 с) — fast path,
                // не ждём полный wallNoProgressSec. Работает и в retreat.
                if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] WallFollow dead stop — scatter/divert");
                NpcShipNavLog.Transition(gameObject.name, npcInstanceId, "WallFollow", "WallFollow",
                    "wall-divert-dead", "");
                StuckDivert(rb, "wall-dead");
                return;
            } else if (!retreating && Time.time - _wallLastProgressAt > wallNoProgressSec) {
                if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] WallFollow stuck — scatter/divert");
                StuckDivert(rb, "wall-stuck");
                return;
            }

            // T-NS-WF17: конец retreat — переплан с чистого места и выход в круиз.
            if (_retreatUntil > 0f && Time.time >= _retreatUntil) {
                _retreatUntil = 0f;
                PlanRoute(CruiseTargetPos);
                ResumeWallFollow(rb);
                return;
            }
            // Staggered такт лидара: один веер → steering + согласие на выход.
            // T-NS-WF08: курс выбирает скоринг (просвет > лоб), а не фиксированный
            // перпендикуляр; сторону держит keep-член (rule-of-side удалён).
            // T-NS-WF17: в retreat скан и выходы пропускаем — летим назад по входу.
            if (!retreating && Time.time >= _wallProbeNextAt) {
                _wallProbeNextAt = Time.time + wallProbeIntervalSec;
                Vector3 fwd = new Vector3(toTarget.x / dist, 0f, toTarget.z / dist);
                if (fwd.sqrMagnitude < 0.001f) fwd = transform.forward;
                fwd.Normalize();
                float look = Mathf.Min(WallLookAhead(), dist);
                LidarScan(rb.position, fwd, look, _wallMoveDir,
                    out Vector3 best, out float centerW, out float minW, out float downW, out _);
                _wallMoveDir = best;
                _downClear = downW;
                _wallMinClear = minW;
                // Выход: dwell + LOS + нос свободен по лидару (согласие).
                bool exitOk = (Time.time - _wallStartedAt >= wallMinDwellSec)
                    && centerW > 0f && HasLineOfSight(rb.position, toTarget, dist);
                if (exitOk) {
                    if (++_wallLosStrikes >= wallExitStrikes) { ResumeWallFollow(rb); return; }
                } else {
                    _wallLosStrikes = 0;
                }
            }

            // Разворот к команде (тот же MoveRotation-стиль, что в круизе).
            float targetYaw = Mathf.Atan2(_wallMoveDir.x, _wallMoveDir.z) * Mathf.Rad2Deg;
            float currentYaw = rb.rotation.eulerAngles.y;
            float deltaYaw = Mathf.DeltaAngle(currentYaw, targetYaw);
            float yawStep = Mathf.Sign(deltaYaw) * Mathf.Min(Mathf.Abs(deltaYaw), MaxYawRate * Time.fixedDeltaTime);
            rb.MoveRotation(Quaternion.AngleAxis(currentYaw + yawStep, Vector3.up));

            float speed = dist > 200f ? CruiseSpeed : ApproachSpeed;
            float vy = Mathf.Clamp((_wallEntryY - rb.position.y) * 0.5f, -2f, 2f);
            // T-NS-WF12: земля критически близко снизу-впереди — аварийный набор
            // (в скалу нельзя; заморозка высоты отменяется только вверх).
            if (_downClear < downCriticalDist) vy = LiftSpeed * 0.5f;
            // T-NS-ALT01: поверх — глобальные границы (потолок важнее down-набора).
            vy = ClampVyToLimits(vy, rb.position.y);
            // T-NS-WF11: анти-strafe — боковая составляющая гасится рассинхроном носа.
            // Strafe (velocity по команде мгновенно) втирал борт в склон, пока нос
            // доворачивал 1–3 с: змейка вдоль скалы. В синхроне — полная команда,
            // в противоходе — чистый носовой полёт с доворотом.
            Vector3 nose = rb.rotation * Vector3.forward;
            nose.y = 0f;
            if (nose.sqrMagnitude < 0.001f) nose = _wallMoveDir;
            nose.Normalize();
            float align = Vector3.Dot(nose, _wallMoveDir);
            Vector3 velDir;
            if (align <= 0.05f) {
                velDir = nose;
            } else {
                Vector3 side = _wallMoveDir - nose * align;
                velDir = (nose * align + side * align).normalized;
                if (velDir.sqrMagnitude < 0.001f) velDir = nose;
            }
            // T-NS-WF13 + WF14 + WF18: скорость — max(выравнивание, просвет).
            // Рассинхрон в чистом небе = быстрый доворот (незачем ползти);
            // у скалы = осторожно. WF13 один давал creep-lock: медленно → дольше
            // у скалы → ещё медленнее (гринд Жука/Ската 2–5 м/с минутами).
            float alignScale = Mathf.Clamp01((align + 0.5f) / 1.5f);
            float openScale = Mathf.Clamp01(_wallMinClear / 150f);
            alignScale = Mathf.Max(alignScale, openScale);
            rb.linearVelocity = new Vector3(velDir.x * speed * alignScale, vy, velDir.z * speed * alignScale);
            rb.angularVelocity = Vector3.zero;
        }

        // === T-NS-ALT01: жёсткие границы высоты NPC (server-only) ===
        // Ниже/выше — не летаем вообще; выпавших тянем назад. Lifting/Berthing/
        // Avoiding не трогаем (взлёт/посадка/манёвр должны быть свободны).
        // Позже заменить глобальной системой коридоров высот (см. AltitudeCorridorSystem).
        [Header("Altitude limits (server-only)")]
        [Tooltip("Ниже — не летаем (м). Выпавших тянем вверх.")]
        [SerializeField] private float altFloorY = 1100f;
        [Tooltip("Выше — не летаем (м). Выпавших тянем вниз.")]
        [SerializeField] private float altCeilY = 4500f;
        // Рантайм-копии (сериализованные не портим): сдвигаются в ApplyRebaseTranslation,
        // иначе после F8 границы протухнут вместе с миром.
        private float _altFloor = 1100f;
        private float _altCeil = 4500f;

        /// <summary>Профильная высота круиза с эшелоном, зажатая в глобальные границы.</summary>
        float ProfileY() => Mathf.Clamp(CruiseTargetPos.y + _echelonOffset, _altFloor, _altCeil);

        /// <summary>
        /// T-NS-ALT01: возврат в коридор — перекрывает любую вертикальную команду
        /// в круизных режимах. Вне границ всегда тянем внутрь.
        /// </summary>
        float ClampVyToLimits(float vy, float y) {
            if (y < _altFloor) return Mathf.Max(vy, LiftSpeed * 0.5f);
            if (y > _altCeil) return Mathf.Min(vy, -LiftSpeed * 0.5f);
            return vy;
        }

        // === T-NS-NAV15: replan-on-evidence — повторные входы в одной точке ===
        // Странник: 3-с входы-выходы в одной вмятине + медленный подъём по стене
        // (Y −19 → +35 за 100 с, dist стоит). LOS чист через верх рима, корпус ниже
        // кромки — фликер вместо облёта. 3-й вход в радиусе = переплан с места
        // (ближний скан видит скалу лучше стартового), а не очередной эпизод.
        [Header("Replan on evidence (server-only)")]
        [Tooltip("Радиус «той же точки» для входов в обход (м).")]
        [Min(20f)] [SerializeField] private float wallSameSpotRadius = 150f;
        [Tooltip("Сколько входов в одной точке терпим, потом replan.")]
        [Min(2)] [SerializeField] private int wallSameSpotEntries = 3;
        private int _wallSameSpotCount;
        private Vector3 _lastWallEntryPos; // только сравнение (сброс, не навигация) — F8-безопасно

        // === T-NS-NAV11: навигатор — план обходов на старте leg'а (server-only) ===
        // Прямая A→B сквозь скалы = корень всех заторов. План: 1–2 точки обхода
        // сразу (тот же лидар, масштаб leg'а) + чёт/нечет id разводят встречных.
        // Эвойд/стена/слоты остаются страховкой. Kill-switch: useNavigator=false.
        [Header("Navigator (server-only)")]
        [Tooltip("ВКЛ: план обходов на старте leg'а вместо прямой сквозь скалы.")]
        [SerializeField] private bool useNavigator = true;
        [Tooltip("Боковое смещение точки обхода от точки попадания (м).")]
        [Min(50f)] [SerializeField] private float navBypassDist = 250f;
        [Tooltip("Макс. точек обхода (цель — всегда последняя).")]
        [Range(1, 3)] [SerializeField] private int navMaxBypass = 2;
        [Tooltip("Допуск прибытия в точку обхода (м).")]
        [Min(10f)] [SerializeField] private float navWpTol = 80f;
        // T-NS-DOCK01: финишный guard вынесен в поле (было navBypassDist*2 захардкожено).
        // Близко к цели — там разберутся Berthing/коридор, обход не вставляем.
        [Tooltip("Финишный guard (м): хит ближе к цели — обход не вставляем, ведёт Berthing.")]
        [Min(100f)] [SerializeField] private float navFinishGuardDist = 500f;
        // T-NS-GRAPH01: граф трасс — кольца гейтов вокруг дисков + Дейкстра.
        // Kill-switch: useRouteGraph=false = legacy-цепочка peak/bypass бит-в-бит.
        [Tooltip("ВКЛ: маршрут по графу гейтов (хребты из N пиков), fallback — legacy.")]
        [SerializeField] private bool useRouteGraph = true;
        [Tooltip("Макс. точек графа (больше — идём legacy).")]
        [Range(1, 8)] [SerializeField] private int routeGraphMaxWp = 6;
        private readonly List<Vector3> _graphWps = new List<Vector3>(8); // буфер графа — F8-безопасно (пересчёт)
        private readonly List<Vector3> _navPlan = new List<Vector3>(8);
        private int _navIdx; // мировые точки — сдвиг в ApplyRebaseTranslation

        /// <summary>
        /// Задать цель круиза + пересчитать эшелон (стабилен весь leg).
        /// Коридор — рамка: эшелон зажимается внутрь min/max активного коридора.
        /// </summary>
        void SetCruiseTarget(Vector3 stationPos) {
            CruiseTargetPos = stationPos;
            _echelonOffset = 0f;
            // T-NS-LOG02: новая цель — свежий замер сближения и сброс медленных replan.
            _goalProgAt = 0f;
            _goalSlowReplans = 0;
            _graphParityFlip = false;
            if (useEchelons) {
                float raw = (npcInstanceId % (ulong)Mathf.Max(1, echelonCount)) * echelonStep;
                var sys = ProjectC.Ship.AltitudeCorridorSystem.Instance;
                var corridor = sys != null ? sys.GetActiveCorridor(transform.position) : null;
                if (sys == null || corridor == null) {
                    _echelonOffset = raw;
                } else {
                    float lo = corridor.minAltitude + echelonCorridorMargin;
                    float hi = corridor.maxAltitude - echelonCorridorMargin;
                    // Коридор уже margins — без эшелона (offset 0).
                    if (hi > lo) _echelonOffset = Mathf.Clamp(stationPos.y + raw, lo, hi) - stationPos.y;
                }
            }
            // T-NS-NAV11: цель задана — планируем обходы (точки на ProfileY).
            // Всегда (не только с эшелонами): план не зависит от флага useEchelons.
            PlanRoute(stationPos);
        }

        /// <summary>Активная цель: точка плана или станция (план пуст/пройден).</summary>
        Vector3 NavGoal() => (_navIdx < _navPlan.Count) ? _navPlan[_navIdx] : CruiseTargetPos;

        /// <summary>
        /// T-NS-NAV11: план leg'а — пока прямая забита, вставляем точки обхода
        /// (макс navMaxBypass). Чистая прямая = одна точка-цель (как раньше).
        /// </summary>
        void PlanRoute(Vector3 target) {
            _navPlan.Clear();
            _navIdx = 0;
            if (!useNavigator) return;
            float y = ProfileY();
            Vector3 from = transform.position;
            Vector3 a = new Vector3(from.x, y, from.z);
            Vector3 b = new Vector3(target.x, y, target.z);
            if ((b - a).sqrMagnitude < 1f) { _navPlan.Add(b); return; }
            // Стартовый завал (порт отправления) пропускаем: шагаем вперёд, план — дальше.
            for (int s = 0; s < 3 && !LegClear(a, b); s++) {
                Vector3 step = b - a;
                step.y = 0f;
                if (step.sqrMagnitude < 1f) break;
                a += step.normalized * navBypassDist;
                a.y = y;
            }
            string how = "direct";
            // T-NS-DOCK01: цель внутри диска (станция в горе) — пики там не обходим,
            // заход ведёт Berthing напрямую; зонд потом доберёт меши-скалы.
            // T-NS-LOG02: только вблизи (иначе дальний leg 3+ км летел бы прямо
            // сквозь хребет — лог 205734, NPC ...0026). Вдали — граф/legacy.
            float homeDist = Vector3.Distance(a, b);
            bool mountainHome = homeDist < navFinishGuardDist
                && PeakRegistry.IsInsideDisc(b, navBypassDist, out _, out _);
            if (mountainHome) how = "mountain-home";
            // T-NS-GRAPH01: сначала граф (структурно, N пиков), fallback — legacy ниже.
            // T-NS-LOG02: молчаливый fallback — в лог (иначе 22/22 падений графа
            // не видно, лог 205734).
            if (!mountainHome && useRouteGraph)
            {
                var tmGraph = NpcShipTrafficManager.Instance;
                float parityBase = (npcInstanceId % 2UL == 0UL) ? 1f : -1f;
                // T-NS-LOG02d: после slow-replan идём зеркально (петля на той же точке).
                float parity = _graphParityFlip ? -parityBase : parityBase;
                bool graphOk = RouteGraph.TryBuild(a, b, y, navBypassDist, routeGraphMaxWp,
                        LegClear, LegClearGoal, IsGateValid,
                        c => tmGraph != null ? tmGraph.HotspotPenalty(c, 500f) : 0f,
                        parity, _graphWps, out int graphDiscs, out string graphFail);
                if (graphOk && _graphWps.Count > 0)
                {
                    for (int i = 0; i < _graphWps.Count; i++) _navPlan.Add(_graphWps[i]);
                    how = "graph" + _graphWps.Count;
                    _navPlan.Add(b);
                    string wpPosG = "";
                    for (int i = 0; i < _graphWps.Count && i < 3; i++)
                        wpPosG += $"W{i + 1}=({_graphWps[i].x:F0},{_graphWps[i].y:F0},{_graphWps[i].z:F0})";
                    NpcShipNavLog.Transition(gameObject.name, npcInstanceId, "Cruising", "Cruising",
                        "nav-plan", $"wp={_navPlan.Count}:{how}{wpPosG} tgt=({b.x:F0},{b.y:F0},{b.z:F0})");
                    return;
                }
                if (!graphOk && graphFail != "ok-direct")
                    NpcShipNavLog.Transition(gameObject.name, npcInstanceId, "Cruising", "Cruising",
                        "graph-fail", $"{graphFail} discs={graphDiscs}");
            }
            // T-NS-NAV16: сначала известные пики — точно, по касательным.
            // Зонд потом доберёт меши-скалы, которых в дисках нет.
            for (int it = 0; it < 2 && !mountainHome; it++) {
                if (!PeakRegistry.FindBlockingDisc(a, b, y, navBypassDist, out Vector3 pc, out float pr)) break;
                Vector3 twp = TangentBypass(a, b, pc, pr);
                if (twp.sqrMagnitude < 0.001f) break; // не нашли — летим прямо, страховка разберётся
                // T-NS-LOG02d: слепую точку в склон не коммитим (лог 234308:
                // tangent-точки внутри рельефа → вечный контурный гринд).
                // Дальше — probe-петля (со штрафами hotspot) или прямая со страховкой.
                if (!IsGateValid(twp)) break;
                _navPlan.Add(twp);
                a = twp;
                how = "peak" + _navPlan.Count;
            }
            // T-NS-NAV11b: строго не больше navMaxBypass обходов (было off-by-one: 3 вместо 2).
            for (int it = 0; it < navMaxBypass + 1 && _navPlan.Count < navMaxBypass; it++) {
                if (LegClear(a, b)) break;
                Vector3 hit = LegHit(a, b);
                // Близко к цели — там разберутся Berthing/коридор, обход не вставляем.
                if (Vector3.Distance(hit, b) < navFinishGuardDist) break;
                Vector3 wp = PickBypass(a, b, hit);
                if (wp.sqrMagnitude < 0.001f) break; // не нашли — летим прямо, страховка разберётся
                _navPlan.Add(wp);
                a = wp;
                how = "bypass" + _navPlan.Count;
            }
            _navPlan.Add(b);
            // T-NS-NAV11b: координаты точек в логе — видно, куда повели обходы.
            string wpPos = "";
            for (int i = 0; i < _navPlan.Count - 1 && i < 2; i++)
                wpPos += $"W{i + 1}=({_navPlan[i].x:F0},{_navPlan[i].y:F0},{_navPlan[i].z:F0})";
            NpcShipNavLog.Transition(gameObject.name, npcInstanceId, "Cruising", "Cruising",
                "nav-plan", $"wp={_navPlan.Count}:{how}{wpPos} tgt=({b.x:F0},{b.y:F0},{b.z:F0})");
        }

        /// <summary>
        /// T-NS-NAV16: касательный обход диска (вид сверху, XZ).
        /// Внутри диска (втёрты у пика) — радиальный выход с margin.
        /// Снаружи — две касательные: короче путь, ничья — чёт/нечет id.
        /// Y всегда ProfileY (латерально, по лору).
        /// </summary>
        Vector3 TangentBypass(Vector3 a, Vector3 b, Vector3 center, float radius) {
            Vector2 A = new Vector2(a.x, a.z);
            Vector2 B = new Vector2(b.x, b.z);
            Vector2 C = new Vector2(center.x, center.z);
            float y = ProfileY();
            Vector2 d = C - A;
            float L = d.magnitude;
            if (L < 0.001f) return new Vector3(C.x + radius + navBypassDist, y, C.y);
            Vector2 dn = d / L;
            if (L <= radius) {
                Vector2 o = A - C;
                if (o.sqrMagnitude < 0.001f) o = new Vector2(dn.y, -dn.x);
                o.Normalize();
                float rr = radius + navBypassDist;
                return new Vector3(C.x + o.x * rr, y, C.y + o.y * rr);
            }
            float alpha = Mathf.Asin(Mathf.Clamp(radius / L, 0f, 1f));
            float baseA = Mathf.Atan2(dn.y, dn.x);
            float T = Mathf.Sqrt(Mathf.Max(0f, L * L - radius * radius));
            float[] lens = new float[2];
            Vector2[] pts = new Vector2[2];
            for (int s = 0; s < 2; s++) {
                float ang = baseA + (s == 0 ? alpha : -alpha);
                Vector2 tdir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                Vector2 Tpt = A + tdir * T;
                Vector2 nout = Tpt - C;
                if (nout.sqrMagnitude < 0.001f) nout = tdir;
                else nout.Normalize();
                Tpt += nout * navBypassDist * 0.5f;
                pts[s] = Tpt;
                lens[s] = T + Vector2.Distance(Tpt, B);
            }
            int pick;
            if (lens[0] < lens[1] * 0.95f) pick = 0;
            else if (lens[1] < lens[0] * 0.95f) pick = 1;
            else pick = (npcInstanceId % 2UL == 0UL) ? 0 : 1;
            return new Vector3(pts[pick].x, y, pts[pick].y);
        }

        /// <summary>Чиста ли прямая (фильтр стен как у лидара).</summary>
        bool LegClear(Vector3 a, Vector3 b) {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1f) return true;
            if (Physics.Raycast(a, d / len, out var hit, len,
                    wallObstacleMask, QueryTriggerInteraction.Ignore))
                return !IsWallHit(hit);
            return true;
        }

        /// <summary>Ближайшая точка препятствия на прямой (для обхода).</summary>
        Vector3 LegHit(Vector3 a, Vector3 b) {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1f) return a;
            if (Physics.Raycast(a, d / len, out var hit, len,
                    wallObstacleMask, QueryTriggerInteraction.Ignore) && IsWallHit(hit))
                return hit.point;
            return (a + b) * 0.5f;
        }

        /// <summary>
        /// T-NS-NAV11 + 12b: точка обхода — hit ± перпендикуляр × navBypassDist.
        /// Скоринг: просвет вперёд − крюк − штраф hotspot'ов; ничья — чёт/нечет id
        /// (встречные расходятся по разным сторонам ещё на старте).
        /// </summary>
        Vector3 PickBypass(Vector3 a, Vector3 b, Vector3 hit) {
            Vector3 dir = b - a;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1f) return Vector3.zero;
            dir.Normalize();
            Vector3 perp = new Vector3(-dir.z, 0f, dir.x);
            float y = ProfileY();
            var tm = NpcShipTrafficManager.Instance;
            float[] scores = new float[2];
            Vector3[] cands = new Vector3[2];
            for (int s = 0; s < 2; s++) {
                float side = s == 0 ? 1f : -1f;
                Vector3 c = new Vector3(hit.x, y, hit.z) + perp * (side * navBypassDist);
                cands[s] = c;
                float clear = RayClear(c, b);
                float score = Mathf.Min(clear, 2000f) - Vector3.Distance(a, c) * 0.2f;
                if (tm != null) score -= tm.HotspotPenalty(c, 500f);
                scores[s] = score;
            }
            // T-NS-LOG02d: зарытые кандидаты отбрасываем (как гейты графа):
            // commit в склон = вечный гринд. Оба зарыты — летим прямо со страховкой.
            bool ok0 = IsGateValid(cands[0]);
            bool ok1 = IsGateValid(cands[1]);
            if (!ok0 && !ok1) return Vector3.zero;
            if (!ok0) return cands[1];
            if (!ok1) return cands[0];
            // Ничья (±5%) — чётные налево, нечётные направо.
            int pick = 0;
            if (scores[1] > scores[0] * 1.05f) pick = 1;
            else if (scores[0] > scores[1] * 1.05f) pick = 0;
            else pick = (npcInstanceId % 2UL == 0UL) ? 0 : 1;
            return cands[pick];
        }

        /// <summary>Просвет от точки к цели (во всю длину leg'а, фильтр стен).</summary>
        float RayClear(Vector3 from, Vector3 to) {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 1f) return 0f;
            if (Physics.Raycast(from, d / len, out var hit, len,
                    wallObstacleMask, QueryTriggerInteraction.Ignore) && IsWallHit(hit))
                return hit.distance;
            return len;
        }

        /// <summary>
        /// T-NS-LOG02: lenient-проверка финального прыжка графа — хит у самой цели
        /// (геометрия станции, как в HasLineOfSight) — не стена: граф дотягивается
        /// до порога горы-дома вместо молчаливого fallback в legacy.
        /// </summary>
        bool LegClearGoal(Vector3 from, Vector3 to) {
            float len = Vector3.Distance(from, to);
            if (len < 1f) return true;
            return RayClear(from, to) >= len - wallArriveMargin;
        }

        /// <summary>
        /// T-NS-LOG02: гейт валиден, если сам стоит в открытом месте (не зарыт
        /// в склон/меш): иначе Дейкстра честно не находит пути через гору,
        /// а legacy коммитит слепые точки. Корабли — не стены (как в лидаре).
        /// </summary>
        bool IsGateValid(Vector3 gate) {
            foreach (var c in Physics.OverlapSphere(gate, wallProbeRadius,
                         wallObstacleMask, QueryTriggerInteraction.Ignore))
                if (IsWallCollider(c)) return false;
            return true;
        }

        /// <summary>
        /// T-NS-WF06a: веер направлений отката из клина. Порядок — строго назад,
        /// затем ±40°/±80°: первое полностью чистое берём сразу, иначе max клиренс.
        /// </summary>
        Vector3 ComputeRecoverDir(Rigidbody rb, Vector3 toTarget) {
            Vector3 back = rb.rotation * Vector3.back;
            back.y = 0f;
            if (back.sqrMagnitude < 0.001f) {
                back = toTarget.sqrMagnitude > 0.001f ? -toTarget.normalized : -transform.forward;
                back.y = 0f;
                if (back.sqrMagnitude < 0.001f) back = Vector3.back;
                back.Normalize();
            } else {
                back.Normalize();
            }
            float need = cruiseRecoverSpeed * cruiseRecoverSec + wallProbeRadius + 10f;
            Vector3 origin = rb.position + Vector3.up * 2f;
            float[] fan = { 0f, 40f, -40f, 80f, -80f };
            Vector3 best = back;
            float bestClear = -1f;
            for (int i = 0; i < fan.Length; i++) {
                Vector3 d = Quaternion.AngleAxis(fan[i], Vector3.up) * back;
                float clear = need;
                if (Physics.Raycast(origin, d, out var hit, need,
                        wallObstacleMask, QueryTriggerInteraction.Ignore) && IsWallHit(hit))
                    clear = hit.distance;
                if (clear > bestClear) { bestClear = clear; best = d; }
                if (clear >= need) break; // полностью чисто — берём не глядя дальше
            }
            return best;
        }

        /// <summary>
        /// LOS до цели тем же фильтром, что зонд. Хит у самой цели (margin) —
        /// это геометрия станции, не стена: считаем чисто (дальше Berthing).
        /// </summary>
        bool HasLineOfSight(Vector3 pos, Vector3 toTarget, float dist) {
            Vector3 dir = toTarget / dist;
            if (Physics.Raycast(pos + Vector3.up * 2f, dir, out var hit, dist,
                    wallObstacleMask, QueryTriggerInteraction.Ignore)) {
                if (!IsWallHit(hit)) return true;
                return hit.distance >= dist - wallArriveMargin;
            }
            return true;
        }

        void ResumeWallFollow(Rigidbody rb) {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            // T-NS-WF10: вышли из обхода — слот свободен для ждущих.
            NpcShipTrafficManager.Instance?.ReleaseWallSlot(npcInstanceId);
            NavMode back = (_wallReturnMode == NavMode.CorridorLeg) ? NavMode.CorridorLeg : NavMode.Cruising;
            // T-NS-WF01b: длинная пауза зонда — корабль уходит прямо, не клюёт кромку.
            _wallCooldownUntil = Time.time + wallResumeCooldownSec;
            _wallLosStrikes = 0;
            _wallBlockStrikes = 0;
            if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] WallFollow → {back} (LOS clear)");
            SetMode(back, "LOS");
        }

        void ResetWallState() {
            _wallTurnAccum = 0f;
            _wallReturnMode = NavMode.Cruising;
            _wallLosStrikes = 0;
            _wallBlockStrikes = 0;
            _wallWaitStartedAt = 0f;
            _wallWaitClearStrikes = 0;
            // T-NS-WF10: divert/timeout — слот свободен для ждущих.
            NpcShipTrafficManager.Instance?.ReleaseWallSlot(npcInstanceId);
            _wallCooldownUntil = Time.time + wallResumeCooldownSec;
        }

        // === T-NS-GATE04: процедурные ворота городов (kill-switch useCityGates) ===
        // Удаление тикета = удалить эту область + один if в TickCruise + 2 значения enum.

        /// <summary>Городской коридор станции назначения (по дистанции, ворота — его граница).</summary>
        ProjectC.Ship.AltitudeCorridorData ResolveCityCorridor() {
            var sys = ProjectC.Ship.AltitudeCorridorSystem.Instance;
            if (sys == null) return null;
            var station = ResolveTargetStation();
            if (!station.HasValue) return null;
            var cities = sys.GetCityCorridors();
            for (int i = 0; i < cities.Count; i++) {
                var c = cities[i];
                if (c == null) continue;
                if (Vector3.Distance(station.Value, c.cityCenter) <= c.cityRadius + gateMargin)
                    return c;
            }
            return null;
        }

        /// <summary>
        /// Точка ворот: окружность cityRadius со стороны корабля + азимутальный разнос
        /// трафика по NpcInstanceId. Считается вживую каждый тик — мировых точек не храним.
        /// Высота — профиль, зажатый в коридор (коридор главнее профиля).
        /// </summary>
        Vector3 ComputeGatePoint(Vector3 shipPos, ProjectC.Ship.AltitudeCorridorData corridor) {
            Vector3 flat = new Vector3(corridor.cityCenter.x - shipPos.x, 0f, corridor.cityCenter.z - shipPos.z);
            if (flat.sqrMagnitude < 1f) flat = Vector3.forward;
            flat.Normalize();
            int variant = (int)(npcInstanceId % (ulong)Mathf.Max(1, gateVariants));
            float ang = (variant - (gateVariants - 1) * 0.5f) * gateVariantStepDeg;
            Vector3 dir = Quaternion.AngleAxis(ang, Vector3.up) * flat;
            Vector3 gate = new Vector3(corridor.cityCenter.x, 0f, corridor.cityCenter.z)
                - dir * (corridor.cityRadius + gateMargin);
            gate.y = Mathf.Clamp(ProfileY(), corridor.minAltitude, corridor.maxAltitude);
            return gate;
        }

        /// <summary>Одна точка входа из TickCruise. Нет коридора / далеко — false (старый путь).</summary>
        bool TryEnterGateApproach(Rigidbody rb, float distToTarget) {
            if (distToTarget > gateTriggerDist) return false;
            var corridor = ResolveCityCorridor();
            if (corridor == null) return false; // нет городского коридора — обычный заход
            float distToCity = Vector3.Distance(rb.position, corridor.cityCenter);
            if (distToCity <= corridor.cityRadius) {
                if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Cruising → CorridorLeg ({corridor.corridorId})");
                SetMode(NavMode.CorridorLeg, "gate-inside");
            } else {
                if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Cruising → GateApproach ({corridor.corridorId})");
                SetMode(NavMode.GateApproach, "gate");
            }
            return true;
        }

        /// <summary>Полёт к воротам; прибыли — CorridorLeg. Коридор пропал — назад в Cruising.</summary>
        void TickGateApproach(Rigidbody rb) {
            var corridor = ResolveCityCorridor();
            if (corridor == null) { SetMode(NavMode.Cruising, "gate-lost"); return; }
            Vector3 gate = ComputeGatePoint(rb.position, corridor);
            Vector3 toGate = gate - rb.position;
            // Ворота позади (корабль уже внутри) — сразу CorridorLeg.
            if (Vector3.Distance(rb.position, corridor.cityCenter) <= corridor.cityRadius) {
                SetMode(NavMode.CorridorLeg, "gate-reached");
                return;
            }
            FlyToward(rb, toGate, CruiseSpeed);
            Vector3 flat = new Vector3(toGate.x, 0f, toGate.z);
            if (flat.magnitude < gateArrivalTol) SetMode(NavMode.CorridorLeg, "gate-reached");
        }

        /// <summary>
        /// Плечо ворота→станция на профильной высоте. Вход в CommZone — Berthing (штатно).
        /// Стены на плече — через тот же WallFollow (возврат — сюда, _wallReturnMode).
        /// </summary>
        void TickCorridorLeg(Rigidbody rb) {
            var corridor = ResolveCityCorridor();
            if (corridor == null) { SetMode(NavMode.Cruising, "gate-lost"); return; }
            var station = ResolveTargetStation();
            if (!station.HasValue) { rb.linearVelocity = Vector3.zero; return; }
            Vector3 target = new Vector3(station.Value.x,
                Mathf.Clamp(ProfileY(), corridor.minAltitude, corridor.maxAltitude),
                station.Value.z);
            Vector3 toTarget = target - rb.position;
            float dist = toTarget.magnitude;

            var zone = ResolveCommZone();
            if (zone != null && Vector3.Distance(rb.position, zone.transform.position) < zone.CommRange) {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                SetMode(NavMode.Berthing, "comm-zone");
                return;
            }
            if (dist < 50f) {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                SetMode(NavMode.Berthing, "near-station");
                return;
            }
            if (dist > 100f && Time.time >= _wallCooldownUntil) {
                if (_wallWaitStartedAt > 0f) {
                    if (Time.time - _wallWaitStartedAt > wallWaitTimeoutSec) {
                        _wallWaitStartedAt = 0f;
                        NpcShipTrafficManager.Instance?.RecordHotspot(rb.position);
                        NpcShipNavLog.Transition(gameObject.name, npcInstanceId, "CorridorLeg", "CorridorLeg",
                            "wall-slot-divert", "");
                        if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Wall slot wait timeout — diverting");
                        DivertToNextStation(rb);
                        return;
                    }
                    if (Time.time >= _wallProbeNextAt) {
                        _wallProbeNextAt = Time.time + wallProbeIntervalSec;
                        int retry = CheckWallEntry(rb, toTarget, dist);
                        if (retry == 1) { _wallWaitStartedAt = 0f; _wallWaitClearStrikes = 0; return; }
                        if (retry == 0 && ++_wallWaitClearStrikes >= 3) {
                            _wallWaitStartedAt = 0f;
                            _wallWaitClearStrikes = 0;
                        } else if (retry == 2) {
                            _wallWaitClearStrikes = 0;
                        }
                    }
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    return;
                }
                if (Time.time >= _wallProbeNextAt) {
                    _wallProbeNextAt = Time.time + wallProbeIntervalSec + ProbePhaseOffset();
                    int entry = CheckWallEntry(rb, toTarget, dist);
                    if (entry == 1) return;
                    if (entry == 2) {
                        _wallWaitStartedAt = Time.time;
                        NpcShipNavLog.Transition(gameObject.name, npcInstanceId, "CorridorLeg", "CorridorLeg",
                            "wall-slot-wait", "");
                        if (debugMode) Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] Wall slot busy — hovering");
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        return;
                    }
                }
            } else if (_wallWaitStartedAt > 0f) {
                _wallWaitStartedAt = 0f;
            }
            FlyToward(rb, toTarget, dist > 200f ? CruiseSpeed : ApproachSpeed);
        }

        /// <summary>Общий крейсерский стиль: доворот + скорость к точке (без смены высоты рывком).</summary>
        void FlyToward(Rigidbody rb, Vector3 toTarget, float speed) {
            if (toTarget.sqrMagnitude < 0.01f) { rb.linearVelocity = Vector3.zero; return; }
            Vector3 dir = toTarget.normalized;
            float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float currentYaw = rb.rotation.eulerAngles.y;
            float deltaYaw = Mathf.DeltaAngle(currentYaw, targetYaw);
            float yawStep = Mathf.Sign(deltaYaw) * Mathf.Min(Mathf.Abs(deltaYaw), MaxYawRate * Time.fixedDeltaTime);
            rb.MoveRotation(Quaternion.AngleAxis(currentYaw + yawStep, Vector3.up));
            float vy = Mathf.Clamp(toTarget.y * 0.5f, -returnVerticalCap, returnVerticalCap);
            // T-NS-ALT01: ворота/плечо — тоже внутри глобальных границ.
            vy = ClampVyToLimits(vy, rb.position.y);
            rb.linearVelocity = new Vector3(dir.x * speed, vy, dir.z * speed);
            rb.angularVelocity = Vector3.zero;
        }

        // === T-NS-WF03: Floating Origin hook ===

        /// <summary>
        /// T-NS-WF03: сдвиг мировых Nav-кэшей вместе с миром (F8/F9).
        /// Состояние WallFollow (сторона/таймеры/азимут) — не мировые данные, хука не требуют.
        /// Вызывается из GlobalMotionControlledRebaseSlice (success + rollback), server-only.
        /// </summary>
        public int ApplyRebaseTranslation(Vector3 translation) {
            if (!IsServer) return 0;
            CruiseTargetPos += translation;
            _avoidFromPos += translation;
            _wallEntryY += translation.y;
            LiftStartY += translation.y;
            // T-NS-ALT01: границы едут вместе с миром.
            _altFloor += translation.y;
            _altCeil += translation.y;
            // T-NS-NAV11: точки плана — тоже мировые.
            for (int i = 0; i < _navPlan.Count; i++) _navPlan[i] += translation;
            return 1; // один корабль сдвинут (для маркера NpcShipNavShifted)
        }

        // === T-PERSIST: RestoreFromSave ===

        /// <summary>Восстановление NavTick-состояния после перезапуска сервера. Server-only.</summary>
        public void RestoreFromSave(ShipPositionSaveData data)
        {
            if (!IsServer) return;

            // ── NavMode (критично: EnterDocked ставит kinematic) ──
            NavMode savedMode = (NavMode)data.navMode;

            // avoiding/avoidYield/wallFollow/gates → transient → fallback to cruising.
            // T-NS-WF01/T-NS-GATE04: обход и ворота не переживают рестарт — resume чистым круизом.
            if (savedMode == NavMode.Avoiding || savedMode == NavMode.AvoidYield
                || savedMode == NavMode.WallFollow || savedMode == NavMode.GateApproach
                || savedMode == NavMode.CorridorLeg)
                savedMode = NavMode.Cruising;

            DwellTime = data.dwellTime > 0 ? data.dwellTime : 60f;
            _scheduleAdvancedAfterDock = data.scheduleAdvancedAfterDock;
            _cargoTradeDone = data.cargoTradeDone;
            AssignedPadId = string.IsNullOrEmpty(data.assignedPadId) ? null : data.assignedPadId;
            CruiseTargetPos = new Vector3(data.pxCruise, data.pyCruise, data.pzCruise);
            if (useEchelons) SetCruiseTarget(CruiseTargetPos); // пересчитать эшелон под текущий коридор
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