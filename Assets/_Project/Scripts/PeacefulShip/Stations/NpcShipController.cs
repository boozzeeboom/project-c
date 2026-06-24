// T-NS M3.1: NpcShipController — scene-placed NetworkBehaviour на корне NPC-корабля.
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
//   - NavTick (M3.1+) — чистая 7-режимная FSM, заменит старый NpcShipWorld.TickNpc в M3.3
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

        [Header("Movement tuning (M3 — универсальный)")]
        [Tooltip("Множитель тяги для NPC (1.0 = как игрок). Снижай если NPC слишком быстро летит.")]
        [Range(0.1f, 1.5f)] [SerializeField] private float npcThrustMult = 1.0f;

        [Tooltip("Множитель рыскания для NPC. Снижай если NPC крутится слишком резко.")]
        [Range(0.1f, 1.5f)] [SerializeField] private float npcYawMult = 1.0f;

        // === T-NS M3.1: NavTick parameters (вместо magic numbers в NpcShipWorld) ===

        [Header("NavTick tuning (M3.1+)")]
        [Tooltip("Высота набора при Lifting (м). 5м — безопасно для уникального пада.")]
        [Min(1f)] [SerializeField] private float liftClearanceMeters = 5f;

        [Tooltip("Порог входа в 'aligned' (yaw считается выровненным). 15° — мягкий вход.")]
        [Range(1f, 45f)] [SerializeField] private float yawAlignEntryDeg = 15f;

        [Tooltip("Порог выхода из 'aligned' (hysteresis — не выходим пока не ушли далеко). 5° — узкий выход.")]
        [Range(1f, 30f)] [SerializeField] private float yawAlignExitDeg = 5f;

        [Tooltip("Coarse gain для yaw input (bearing → [-1,1]). 0.02 = медленный разворот.")]
        [Range(0.005f, 0.1f)] [SerializeField] private float yawGainCoarse = 0.02f;

        [Tooltip("Замедление thrust при подходе: thrust *= clamp01(dist/100) * maxThrust. 100м — окно торможения.")]
        [Min(10f)] [SerializeField] private float thrustSlowdownWindowMeters = 100f;

        [Tooltip("Максимальный thrust input (1.0 = полная тяга).")]
        [Range(0.1f, 1.0f)] [SerializeField] private float maxThrustInput = 0.6f;

        [Header("Anti-gravity boost (Q8)")]
        [Tooltip("Длительность boost после ExitDocked (сек). 0 = отключить.")]
        [Min(0f)] [SerializeField] private float antiGravityBoostDuration = 5f;

        [Tooltip("Значение AntiGravity во время boost (1.0 = норма, 1.5 = полная компенсация).")]
        [Range(0f, 1.5f)] [SerializeField] private float antiGravityBoostValue = 1.5f;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;
        [Tooltip("Включить новую 7-режимную NavTick FSM (M3). Если false — старый NpcShipWorld.TickNpc управляет.")]
        public bool useNewNavTick = true;

        // === Public API ===
        public NpcShipSchedule Schedule => schedule;
        public ulong NpcInstanceId => npcInstanceId;
        public ShipController Ship => GetComponent<ShipController>();

        // === NavTick runtime state (M3.1) ===
        public NavMode CurrentMode { get; private set; } = NavMode.Docked;
        public string AssignedPadId { get; set; }
        public float LastPadRequestTime { get; private set; }
        public float LastCourseCheckTime { get; private set; }
        public bool WasYawAligned { get; private set; }
        public Vector3 StartPathPos { get; set; }
        public NavTarget CruiseTargetPos { get; set; }

        /// <summary>Time.time когда вошли в NavMode.Docked. Используется для dwell time.</summary>
        public float DockedSinceTime { get; private set; }

        /// <summary>Текущий route (синк с NpcShipState.CurrentRoute — обновляется NpcShipWorld).</summary>
        public NpcShipRoute CurrentRoute { get; set; }

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

            // T-NS03: fix angular damping для NPC yaw (scene-placed HeavyII имеет angularDamping=8,
            // что практически блокирует поворот при yawForce=5)
            var rb = GetComponent<Rigidbody>();
            if (rb != null && rb.angularDamping > 2f)
            {
                float oldAngularDamping = rb.angularDamping;
                rb.angularDamping = 0.8f;
                if (debugMode)
                    Debug.Log($"[NpcShipController:{gameObject.name}] Reduced angularDamping from {oldAngularDamping:F1} → 0.8 for NPC yaw");
            }

            // NPC solid collider: ребёнок RootCollider (solid BoxCollider) для OnTriggerEnter с pad-триггерами.
            // Все существующие коллайдеры (если trigger) → solid, чтобы Unity вызывал OnTriggerEnter.
            // NPC↔NPC коллизия отключается через layer matrix в ProjectSettings.
            EnsureNpcSolidCollider();

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

        /// <summary>
        /// NPC solid collider: обеспечивает чтобы корабль имел solid BoxCollider.
        /// Все имеющиеся BoxCollider → solid (isTrigger=false). Если нет вообще — добавляем.
        /// NPC↔NPC коллизия отключается через Layer Collision Matrix в ProjectSettings.
        /// </summary>
        private void EnsureNpcSolidCollider()
        {
            var cols = GetComponentsInChildren<BoxCollider>(true);
            if (cols.Length > 0)
            {
                foreach (var c in cols) c.isTrigger = false;
            }
            else
            {
                // Добавляем дефолтный solid BoxCollider размера как у ShipController
                var col = gameObject.AddComponent<BoxCollider>();
                col.isTrigger = false;
                col.size = new Vector3(4f, 2.5f, 6f);
                col.center = new Vector3(0f, 0f, 0f);
            }
        }

        // === Movement API (server-only) — legacy, оставлен для обратной совместимости с M2 ===

        /// <summary>
        /// Применить движение к ShipController через новый ApplyServerInput (T-NS01).
        /// Вызывается из NpcShipWorld.TickNpc FSM (legacy M2 код).
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

        // === T-NS M3.1: NavTick — чистая 7-режимная FSM ===

        /// <summary>
        /// Главная точка входа NavTick. Вызывается из NpcShipWorld (в M3.3 — заменит TickNpc switch).
        /// Один режим = один input pattern. Все spatial conditions через NavChecks.
        /// </summary>
        public void NavTick(float dt)
        {
            if (!IsServer) return;
            if (!useNewNavTick) return; // legacy: NpcShipWorld.TickNpc управляет

            var ship = Ship;
            if (ship == null) return;

            // Coast-guard: если корабль docked — мы в Docked mode, выходим
            if (ship.IsDocked)
            {
                if (CurrentMode != NavMode.Docked) SetMode(NavMode.Docked);
                return;
            }

            // Docked → Lifting: при ExitDocked()
            // (вызывается из NavDockedToLifting() из NpcShipWorld в M3.3, или через тест)

            switch (CurrentMode)
            {
                case NavMode.Docked: TickDocked(); break;
                case NavMode.Lifting: TickLifting(); break;
                case NavMode.Yawing: TickYawing(); break;
                case NavMode.Cruising: TickCruising(); break;
                case NavMode.Holding: TickHolding(); break;
                case NavMode.Berthing: TickBerthing(); break;
                case NavMode.Hover: TickHover(); break;
            }
        }

        /// <summary>Сменить режим с логированием (R2-003: всегда логировать переходы).</summary>
        public void SetMode(NavMode newMode, string reason = null)
        {
            if (CurrentMode == newMode) return;
            var old = CurrentMode;
            CurrentMode = newMode;
            // При входе в Lifting — запомнить startY для условия IsLiftedTo
            if (newMode == NavMode.Lifting)
            {
                StartPathPos = Ship.transform.position;
            }
            // При входе в Yawing — сбросить aligned-флаг
            if (newMode == NavMode.Yawing)
            {
                WasYawAligned = false;
            }
            // При входе в Docked — запомнить timestamp для dwell time
            if (newMode == NavMode.Docked)
            {
                DockedSinceTime = Time.time;
            }
            if (debugMode)
            {
                string r = string.IsNullOrEmpty(reason) ? "" : $" ({reason})";
                Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] NavMode {old} → {newMode}{r}");
            }
        }

        // === Mode handlers (по одному input pattern каждый) ===

        private void TickDocked()
        {
            // Docked: двигатель заблокирован. Решение о старте leg принимает NpcShipWorld
            // (он advance schedule index + дёргает BeginNewLeg). Здесь только нулевой input.
            // M3.1+: NpcShipWorld.Update вызывает controller.DebugForceLeg/BeginNewLeg после dwell time.
            ApplyZeroInput();
        }

        private void TickLifting()
        {
            var ship = Ship;
            Vector3 pos = ship.transform.position;
            if (NavChecks.IsLiftedTo(pos.y, StartPathPos.y, liftClearanceMeters))
            {
                // Достигли высоты → вычислить CruiseTarget (станция) и перейти в Yawing
                var target = ResolveStationCenterPos();
                if (target.HasValue)
                {
                    CruiseTargetPos = target;
                    SetMode(NavMode.Yawing, $"cleared pad, heading to {CurrentRoute.toLocationId}");
                }
                else
                {
                    // M3.1.5: станция не найдена (locationId mismatch) — сбросить vertical velocity
                    // чтобы NPC не всплывал по инерции вверх. И остаться в Lifting.
                    Debug.LogWarning($"[NpcShipController:NPC:{npcInstanceId:X}] No station for {CurrentRoute.toLocationId} — staying Lifting, velocity reset");
                    var rb = ship.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        var v = rb.linearVelocity; v.y = 0f; rb.linearVelocity = v;
                    }
                    ApplyZeroInput();
                }
            }
            else
            {
                // Vertical input ONLY (через ApplyServerInput — тот же код что у игрока на Space)
                ship.ApplyServerInput(thrust: 0f, yaw: 0f, pitch: 0f, vertical: 1f);
            }
        }

        private void TickYawing()
        {
            if (!CruiseTargetPos.HasValue) { SetMode(NavMode.Hover, "no target"); return; }
            var ship = Ship;
            Vector3 pos = ship.transform.position;
            float shipYaw = ship.transform.eulerAngles.y;
            Vector3 target = CruiseTargetPos.Value;
            float targetBearing = NavChecks.BearingDegrees(pos, target);

            bool isAligned = NavChecks.IsYawAligned(shipYaw, targetBearing, WasYawAligned,
                                                    yawAlignEntryDeg, yawAlignExitDeg);

            if (isAligned && WasYawAligned)
            {
                // Уже были выровнены — переходим в Cruising
                SetMode(NavMode.Cruising, $"aligned to bearing {targetBearing:F1}°");
                return;
            }
            WasYawAligned = isAligned;

            if (!isAligned)
            {
                // Yaw ONLY (anti-gravity держит высоту через ship.AntiGravity=1.0)
                float bearing = Mathf.DeltaAngle(shipYaw, targetBearing);
                float yawInput = Mathf.Clamp(bearing * yawGainCoarse, -1f, 1f);
                ship.ApplyServerInput(thrust: 0f, yaw: yawInput, pitch: 0f, vertical: 0f);
            }
            else
            {
                // Только что вошли в aligned — один кадр ждём, потом Cruising (через WasYawAligned)
                ApplyZeroInput();
            }
        }

        private void TickCruising()
        {
            if (!CruiseTargetPos.HasValue) { SetMode(NavMode.Hover, "no target"); return; }
            var ship = Ship;
            Vector3 pos = ship.transform.position;
            Vector3 target = CruiseTargetPos.Value;

            // Проверить: вошли ли в OuterCommZone целевой станции?
            var zone = ResolveCommZone(CurrentRoute.toLocationId);
            if (zone != null && NavChecks.IsInCommZone(pos, zone))
            {
                SetMode(NavMode.Holding, $"entered OuterCommZone of {CurrentRoute.toLocationId} (r={zone.CommRange:F0})");
                return;
            }

            // Periodic course correction: каждые 5 сек проверяем отклонение от идеального bearing
            // Если |bearing| > 30° → stop thrust, обновить target, в Yawing
            float shipYaw = ship.transform.eulerAngles.y;
            float idealBearing = NavChecks.BearingDegrees(pos, target);
            float bearing = Mathf.DeltaAngle(shipYaw, idealBearing);
            if (Time.time - LastCourseCheckTime > 5f)
            {
                LastCourseCheckTime = Time.time;
                if (Mathf.Abs(bearing) > 30f)
                {
                    // Off course — go back to Yawing
                    SetMode(NavMode.Yawing, $"course correction: bearing {bearing:F1}° > 30°");
                    return;
                }
            }

            // Diagonal flight: thrust + vertical по прямой A→B
            float dist = Vector3.Distance(pos, target);
            float thrust = Mathf.Clamp01(dist / thrustSlowdownWindowMeters) * maxThrustInput;

            // Diagonal Y: lerp от startY к targetY по прогрессу
            float totalDist = Vector3.Distance(StartPathPos, target);
            float progress = totalDist > 0.01f ? 1f - (dist / totalDist) : 0f;
            float diagonalTargetY = Mathf.Lerp(StartPathPos.y, target.y, Mathf.Clamp01(progress));

            // Vertical input: PD-подобный контроль через ApplyServerInput.
            // Ошибка по Y → input. Не магические числа — delta в метрах, clamp в [-1,1].
            float yError = diagonalTargetY - pos.y;
            float vertical = Mathf.Clamp(yError * 0.05f, -1f, 1f);

            ship.ApplyServerInput(thrust: thrust, yaw: 0f, pitch: 0f, vertical: vertical);
        }

        private void TickHolding()
        {
            // Запрашиваем pad каждые 2 сек (anti-spam)
            if (Time.time - LastPadRequestTime > 2f)
            {
                LastPadRequestTime = Time.time;
                string padId = TryAssignPadFromDispatcher();
                if (!string.IsNullOrEmpty(padId))
                {
                    AssignedPadId = padId;
                    CruiseTargetPos = ResolvePadPos(CurrentRoute.toLocationId, AssignedPadId);
                    if (CruiseTargetPos.HasValue)
                    {
                        SetMode(NavMode.Berthing, $"pad {padId} assigned");
                        return;
                    }
                }
            }
            // Hover: только anti-gravity, нулевые input
            ApplyZeroInput();
        }

        private void TickBerthing()
        {
            if (!CruiseTargetPos.HasValue || string.IsNullOrEmpty(AssignedPadId))
            {
                SetMode(NavMode.Holding, "missing pad in Berthing — fallback");
                return;
            }
            var ship = Ship;
            Vector3 pos = ship.transform.position;
            Vector3 padPos = CruiseTargetPos.Value;

            // Проверить trigger-bокс пада — если вошли → EnterDocked → Docked
            var pad = ResolvePadTriggerBox(AssignedPadId);
            if (NavChecks.IsInsidePadTrigger(ship, pad))
            {
                ship.EnterDocked();
                SetMode(NavMode.Docked, $"trigger entered pad {AssignedPadId}");
                return;
            }

            // Yaw к паду
            float shipYaw = ship.transform.eulerAngles.y;
            float padBearing = NavChecks.BearingDegrees(pos, padPos);
            float bearing = Mathf.DeltaAngle(shipYaw, padBearing);
            float horizDist = NavChecks.HorizontalDistance(pos, padPos);

            if (horizDist > 10f)
            {
                if (Mathf.Abs(bearing) > yawAlignExitDeg)
                {
                    // Yaw ONLY (на месте)
                    float yawInput = Mathf.Clamp(bearing * yawGainCoarse, -1f, 1f);
                    ship.ApplyServerInput(thrust: 0f, yaw: yawInput, pitch: 0f, vertical: 0f);
                    return;
                }
                // Diagonal: thrust + vertical
                float thrust = Mathf.Clamp01(horizDist / 50f) * 0.4f;
                float yError = padPos.y - pos.y;
                float vertical = Mathf.Clamp(yError * 0.05f, -1f, 1f);
                ship.ApplyServerInput(thrust: thrust, yaw: 0f, pitch: 0f, vertical: vertical);
                return;
            }

            // Vertical descent (close to pad: horizDist < 10m)
            float yErrorFinal = padPos.y - pos.y;
            float verticalFinal = Mathf.Clamp(yErrorFinal * 0.05f, -1f, 1f);
            ship.ApplyServerInput(thrust: 0f, yaw: 0f, pitch: 0f, vertical: verticalFinal);
        }

        private void TickHover()
        {
            // Только anti-gravity держит высоту, никаких input
            ApplyZeroInput();
        }

        private void ApplyZeroInput()
        {
            var ship = Ship;
            if (ship == null) return;
            ship.ApplyServerInput(thrust: 0f, yaw: 0f, pitch: 0f, vertical: 0f);
        }

        // === Station/pad lookup (используется NavTick) ===

        private NavTarget ResolveStationCenterPos()
        {
            if (CurrentRoute.toLocationId == null) return NavTarget.None;
            var station = Docking.Network.DockingZoneRegistry.GetByLocation(CurrentRoute.toLocationId);
            if (station == null) return NavTarget.None;
            return station.transform.position;
        }

        private OuterCommZone ResolveCommZone(string locationId)
        {
            if (string.IsNullOrEmpty(locationId)) return null;
            var station = Docking.Network.DockingZoneRegistry.GetByLocation(locationId);
            if (station == null) return null;
            return station.GetComponentInChildren<OuterCommZone>(true);
        }

        private NavTarget ResolvePadPos(string locationId, string padId)
        {
            if (string.IsNullOrEmpty(locationId) || string.IsNullOrEmpty(padId)) return NavTarget.None;
            var station = Docking.Network.DockingZoneRegistry.GetByLocation(locationId);
            if (station == null) return NavTarget.None;
            var boxes = station.GetComponentsInChildren<DockingPadTriggerBox>(true);
            for (int i = 0; i < boxes.Length; i++)
            {
                if (boxes[i].PadId == padId) return boxes[i].transform.position;
            }
            return station.transform.position;
        }

        private DockingPadTriggerBox ResolvePadTriggerBox(string padId)
        {
            if (string.IsNullOrEmpty(padId)) return null;
            var station = Docking.Network.DockingZoneRegistry.GetByLocation(CurrentRoute.toLocationId);
            if (station == null) return null;
            var boxes = station.GetComponentsInChildren<DockingPadTriggerBox>(true);
            for (int i = 0; i < boxes.Length; i++)
            {
                if (boxes[i].PadId == padId) return boxes[i];
            }
            return null;
        }

        private string TryAssignPadFromDispatcher()
        {
            if (Ship == null || CurrentRoute.toLocationId == null) return null;
            var station = Docking.Network.DockingZoneRegistry.GetByLocation(CurrentRoute.toLocationId);
            if (station == null) return null;
            if (Docking.Core.DockingWorld.Instance == null) return null;

            // Q6: maxConcurrentLandings проверяется внутри AssignPadForNpc
            return Docking.Core.DockingWorld.Instance.AssignPadForNpc(
                station, Ship, Ship.ShipFlightClass, npcInstanceId);
        }

        /// <summary>
        /// Старт нового leg: ExitDocked (снять kinematic) + StartAntiGravityBoost + SetMode(Lifting).
        /// Вызывается из TickDocked (после dwell time) или DebugForceLeg (MCP).
        /// </summary>
        public void BeginNewLeg()
        {
            if (!IsServer) return;
            AssignedPadId = null;
            // 1. Снимаем kinematic через стандартный API
            var ship = Ship;
            if (ship != null && ship.IsDocked)
            {
                ship.ExitDocked();
            }
            // 2. Anti-grav boost 5 сек — чтобы не упал пока не набрал высоту
            StartAntiGravityBoost();
            // 3. Mode → Lifting, StartPathPos запомнится в SetMode
            SetMode(NavMode.Lifting, "BeginNewLeg");
        }

        /// <summary>Debug-команда: принудительно начать leg (для MCP execute_code тестирования).</summary>
        [ContextMenu("Debug: Force New Leg")]
        public void DebugForceLeg()
        {
            Debug.Log($"[NpcShipController:NPC:{npcInstanceId:X}] DebugForceLeg called — was in {CurrentMode}");
            BeginNewLeg();
        }
    }
}
