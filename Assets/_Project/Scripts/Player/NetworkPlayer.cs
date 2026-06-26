using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectC.Combat;  // T-RTC06: PlayerAttacker, PlayerTarget, CombatServer
using ProjectC.Core;
using ProjectC.Equipment;  // T-NPC-12: InCombat
using ProjectC.Items;
using ProjectC.MetaRequirement;  // T-KEY-06: direct access to MetaRequirementClientState/Registry
using ProjectC.Network;
using ProjectC.Ship.Key;
using ProjectC.Trade;
using ProjectC.Trade.Dto;
using ProjectC.Trade.Network;
using ProjectC.UI;
using ProjectC.Skills;  // T-INP-01: SkillInputService
using ProjectC.World.Streaming;
using ProjectC.World.Chest;
using System.Collections.Generic;

namespace ProjectC.Player
{
    /// <summary>
    /// Сетевой компонент игрока.
    /// • Движение: WASD + Space + Shift (пеший), W/S/A/D/Q/E/Shift (корабль)
    /// • Переключение: F — сесть/выйти из корабля
    /// • Камера: персональная для каждого игрока
    /// • Инвентарь: E подобрать, Tab открыть колесо
    /// • Сундуки: E открыть
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class NetworkPlayer : NetworkBehaviour
    {
        [Header("Движение (пеший)")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float runSpeed = 10f;
        [SerializeField] private float rotationSpeed = 12f;

        [Header("Прыжок")]
        [SerializeField] private float jumpForce = 8f;
        [SerializeField] private float gravity = -20f;

        [Header("Камера")]
        [SerializeField] private ThirdPersonCamera cameraPrefab;

        [Header("Корабль")]
        [Tooltip("Максимальная дистанция для посадки (м)")]
        [SerializeField] private float boardDistance = 5f;

        [Header("Инвентарь")]
        [SerializeField] private float pickupRange = 3f;

        // Компоненты
        private CharacterController _controller;
        private Animator _animator;
        private Vector3 _velocity;
        private bool _isGrounded;
        private ThirdPersonCamera _myCamera;

        // Состояние
        private bool _inShip = false;
        private ShipController _currentShip;
        private List<Renderer> _playerRenderers = new List<Renderer>();
        private List<Collider> _playerColliders = new List<Collider>();

        // Ввод
        private Vector2 _moveInput;
        private bool _jumpPressed;
        private bool _runPressed;

        // Поиск ближайшего объекта
        private PickupItem _nearestPickup;
        private ChestContainer _nearestChest;
        private NetworkChestContainer _nearestNetworkChest;
        private ShipController _nearestShip;
        // T-NPC-03: NPC loot (credits pickup)
        private ProjectC.AI.NpcLootPickup _nearestNpcLoot;

        // NetworkObject
        private NetworkObject networkObject;

        // Chunk tracking
        private PlayerChunkTracker _playerChunkTracker;

        // Ship Key Subsystem: защита от двойного F пока ждём ответ сервера
        private float _lastCanBoardRequestTime = -10f;
        private ulong _pendingCanBoardShipId = ulong.MaxValue;
        private const float CAN_BOARD_REQUEST_TIMEOUT = 1.5f;

        // MetaRequirement Subsystem: защита от двойного E (или F) пока ждём ответ
        // сервера. Используется для не-корабельных interactable'ов (LockBox, дверь и т.п.).
        private float _lastCanUseRequestTime = -10f;
        private ulong _pendingCanUseInteractableId = ulong.MaxValue;
        private const float CAN_USE_REQUEST_TIMEOUT = 1.5f;

        // T-G07: Player gather animation — пульсация scale во время сбора
        [Header("Gather Animation")]
        [Tooltip("Амплитуда пульсации scale персонажа при сборе (0 = без анимации).")]
        [SerializeField] private float _gatherScaleAmplitude = 0.08f;
        [Tooltip("Период пульсации scale в секундах.")]
        [SerializeField] [Range(0.1f, 1.5f)] private float _gatherPulsePeriod = 0.6f;
        private Coroutine _gatherPulseCoroutine;
        private Vector3 _originalScale;
        private bool _subscribedToGather = false;
        private bool _gatherActive = false;



        // ==================== CLIENT-SIDE PREDICTION ====================

        [Header("Коррекция позиции (prediction)")]
        [Tooltip("Порог рассинхронизации для коррекции (м). Больше = меньше jitter")]
        [SerializeField] private float positionCorrectionThreshold = 99999f;

        [Tooltip("Скорость плавной коррекции позиции")]
        [SerializeField] private float positionCorrectionSpeed = 5f;

        // Серверная позиция для коррекции
        private Vector3 _serverPosition;
        private bool _hasServerPosition = false;
        
        /// <summary>
        /// Cooldown после сдвига мира — игнорируем серверную коррекцию пока мира не устаканится.
        /// </summary>
        // NOTE: FloatingOriginMP cooldown removed - scene-based architecture doesn't use world shifting

        // ==================== КАМЕРА ====================

        // ==================== КАМЕРА ====================

        public bool IsInShip => _inShip;
        public ShipController CurrentShip => _currentShip;

        /// <summary>
        /// Реальная мировая позиция игрока. Если пилот сидит в корабле — это
        /// позиция корабля (CharacterController отключён в ApplyShipState и
        /// transform.position игрока заморожен на точке посадки, пока корабль
        /// летит). Использовать вместо transform.position в любых дистанционных
        /// проверках (рынок, триггеры зон, диалоги), иначе игрок «вне зоны» в
        /// клиентской логике, хотя визуально он прилетел.
        /// </summary>
        public Vector3 GetEffectivePosition()
        {
            if (_inShip && _currentShip != null) return _currentShip.transform.position;
            return transform.position;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            networkObject = GetComponent<NetworkObject>();
            _controller = GetComponent<CharacterController>();
            _animator = GetComponentInChildren<Animator>();

            // ПРИМЕЧАНИЕ: NetworkTransform.InterpolatePosition/Rotation/Scale
            // отключаются ВРУЧНУЮ в Unity Editor на префабе Player.prefab
            // (API отличается в разных версиях Unity/NGO)

            // FIX (2026-06-04, INVESTIGATION_GHOST_PLAYER_CLONE.md, "second layer"):
            //   На хосте NGO 2.x авто-спавнит scene-placed NetworkObject'ы из BootstrapScene
            //   (включая scene-placed `PlayerSpawner`) как ОБЫЧНЫЕ NetworkObject'ы, не PlayerObject'ы.
            //   При этом OwnerClientId = ServerClientId = 0, а LocalClientId на хосте = 0,
            //   поэтому `IsOwner == true` даже для НЕ-PlayerObject'ов — это известный footgun NGO.
            //   Без guard'а scene-placed `PlayerSpawner` запускал SpawnCamera() + SpawnInventory()
            //   → второй призрак-клон + дубль InventoryUI.
            //
            //   ДИСКРИМИНАТОР (надёжный): наличие компонента `NetworkPlayerSpawner` на GameObject.
            //   • Scene-placed `PlayerSpawner` в BootstrapScene — ЕСТЬ (по дизайну: компонент был
            //     частью спавнера ещё до появления PlayerPrefab).
            //   • Auto-spawned `NetworkPlayer(Clone)` из PlayerPrefab — НЕТ (подтверждено живой
            //     иерархией 2026-06-04, см. INVESTIGATION_GHOST_PLAYER_CLONE.md).
            //
            //   ПРЕДЫДУЩАЯ ВЕРСИЯ использовала `!networkObject.IsPlayerObject` — НЕ надёжно,
            //   потому что NGO 2.x может НЕ установить IsPlayerObject до момента OnNetworkSpawn
            //   для auto-spawned префаба (timing race), и тогда guard ошибочно отключал
            //   настоящего игрока. Это давало симптом "после play host ничего не грузит".
            if (GetComponent<NetworkPlayerSpawner>() != null)
            {
                // Это scene-placed PlayerSpawner-пустышка, не настоящий игрок.
                if (_controller != null) _controller.enabled = false;
                enabled = false; // Update/FixedUpdate тоже не должны крутиться
                Debug.Log($"[NetworkPlayer] Skipping player init for scene-placed 'PlayerSpawner' GameObject (has NetworkPlayerSpawner marker, IsOwner={IsOwner}, IsPlayerObject={networkObject.IsPlayerObject}). См. INVESTIGATION_GHOST_PLAYER_CLONE.md.");
                return;
            }

            // Находим ВСЕ Renderer и Collider на объекте (включая дочерние)
            GetComponentsInChildren(true, _playerRenderers);
            GetComponentsInChildren(true, _playerColliders);

            // Убираем CharacterController и сам NetworkObject из списков
            _playerRenderers.RemoveAll(r => r == null);
            _playerColliders.RemoveAll(c => c == null || c is CharacterController);

            // Отключаем старый PlayerController (если остался от legacy)
            var legacyController = GetComponent<PlayerController>();
            if (legacyController != null) legacyController.enabled = false;

            // NOTE: FloatingOriginMP subscriptions removed - scene-based architecture doesn't use world shifting
            // Each scene has its own local origin, no need for FloatingOriginMP

            if (IsOwner)
            {
                SpawnCamera();
                SpawnInventory();

                // NOTE (cleanup Phase 9, 2026-06-05): legacy _inventory.LoadFromPrefs() убран —
                // v2 серверный инвентарь авторитативен, persistence = ответственность сервера.
                // Если reconnect-recovery нужен — реализовать через InventoryServer.RequestSnapshotOnReconnect.

                ApplyWalkingState();

                // Find PlayerChunkTracker for chunk streaming
                var chunkTrackers = FindObjectsByType<PlayerChunkTracker>();
                if (chunkTrackers.Length > 0)
                {
                    _playerChunkTracker = chunkTrackers[0];
                }

                // T-INP-01: SkillInputService (owner-only). Единая точка "нажата кнопка → выполнить навык".
                InitializeSkillInputService();
            }
            else
            {
                _controller.enabled = false;
            }

            // T-G07: подписка на события сбора (client, не server)
            TrySubscribeToGatherClientState();

            // T-RTC06: Регистрация IAttacker/IDamageTarget в CombatServer (server-side only).
            // Add-only: PlayerAttacker + PlayerTarget как компоненты, register/unregister в lifecycle.
            // Skip для scene-placed PlayerSpawner-пустышек (выше guard на NetworkPlayerSpawner).
            RegisterWithCombatServer();
        }

        /// <summary>
        /// T-RTC06: Зарегистрировать PlayerAttacker + PlayerTarget в CombatServer (server-side only).
        /// Add-only: компоненты создаются AddComponent, инициализируются clientId, регистрируются.
        /// Идемпотентно — повторный вызов безопасен (OnNetworkSpawn может вызываться при reconnect).
        /// </summary>
        /// <remarks>
        /// v0.1 fix (race condition): AddComponent вызывается ВСЕГДА, даже если CombatServer.Instance==null.
        /// Ранний return был багом — без компонентов pull-up (PlayerAttacker/Target.OnNetworkSpawn) не сработает.
        /// Push-down (CombatServer.OnNetworkSpawn → RecoverExistingEntities) подхватит, если Instance ещё null.
        /// </remarks>
        private void RegisterWithCombatServer()
        {
            if (!IsServer) return;

            // 1. ALWAYS add components first (pull-up будет ждать OnNetworkSpawn компонентов).
            //    GetComponent → AddComponent — идемпотентно.
            var attacker = GetComponent<PlayerAttacker>();
            if (attacker == null) attacker = gameObject.AddComponent<PlayerAttacker>();
            attacker.Initialize(OwnerClientId);

            var target = GetComponent<PlayerTarget>();
            if (target == null) target = gameObject.AddComponent<PlayerTarget>();
            target.Initialize(OwnerClientId);

            // 2. Try immediate registration. Если CombatServer ещё не spawn'нулся —
            //    push-down в CombatServer.OnNetworkSpawn → RecoverExistingEntities подхватит.
            if (CombatServer.Instance != null)
            {
                CombatServer.Instance.RegisterAttacker(OwnerClientId, attacker);
                CombatServer.Instance.RegisterTarget(OwnerClientId, target);
            }
            else if (Debug.isDebugBuild)
            {
                Debug.Log($"[NetworkPlayer] RegisterWithCombatServer: components added (PlayerAttacker/Target), but CombatServer.Instance==null — push-down will catch up. clientId={OwnerClientId}");
            }
        }

        /// <summary>
        /// T-RTC06: Unregister IAttacker/IDamageTarget из CombatServer при despawn/disconnect.
        /// </summary>
        private void UnregisterFromCombatServer()
        {
            if (!IsServer) return;
            if (CombatServer.Instance == null) return;

            CombatServer.Instance.UnregisterAttacker(OwnerClientId);
            CombatServer.Instance.UnregisterTarget(OwnerClientId);
            if (Debug.isDebugBuild) Debug.Log($"[NetworkPlayer] UnregisterFromCombatServer: clientId={OwnerClientId}");
        }

        /// <summary>
        /// T-RTC06 (DEBUG): Найти ближайший NpcTarget в радиусе 5м и отправить RequestAttackRpc.
        /// ВРЕМЕННЫЙ код — только для verify CombatEngine в Play Mode. Удалить в Phase 2
        /// (когда будет нормальный targeting через raycast + UI).
        /// </summary>
        private void DebugAttackNearestNpc()
        {
            const float MAX_RANGE = 15.0f;  // T-CB03: увеличено с 5м (playground ~ 6-12м от NPC до player после walking)
            NpcTarget nearest = null;
            float bestDistSq = MAX_RANGE * MAX_RANGE;

            foreach (var npc in FindObjectsByType<NpcTarget>(FindObjectsSortMode.None))
            {
                if (npc == null || !npc.IsAlive()) continue;
                float dSq = (npc.transform.position - transform.position).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    nearest = npc;
                }
            }

            if (nearest == null)
            {
                Debug.Log($"[NetworkPlayer] K-attack: no NpcTarget within {MAX_RANGE}м.");
                return;
            }

            // sourceId = 0 (первый source в PlayerAttacker; после T-CB03 — реальный weapon id).
            ulong targetId = nearest.GetTargetId();
            Debug.Log($"[NetworkPlayer] K-attack: targetId={targetId}, dist={Mathf.Sqrt(bestDistSq):F2}м");
            CombatServer.Instance.RequestAttackRpc(targetId, 0UL);
        }

        /// <summary>
        /// T-INP-01: Инициализация SkillInputService (owner-only).
        /// AddComponent идемпотентен — повторный вызов безопасен (reconnect).
        /// Target finder: "nearest NpcTarget в 15м" (legacy DebugAttackNearestNpc).
        /// </summary>
        private void InitializeSkillInputService()
        {
            var svc = GetComponent<SkillInputService>();
            if (svc == null) svc = gameObject.AddComponent<SkillInputService>();

            System.Func<ulong> targetFinder = () =>
            {
                const float MAX_RANGE = 15.0f;
                NpcTarget nearest = null;
                float bestDistSq = MAX_RANGE * MAX_RANGE;
                foreach (var npc in FindObjectsByType<NpcTarget>(FindObjectsSortMode.None))
                {
                    if (npc == null || !npc.IsAlive()) continue;
                    float dSq = (npc.transform.position - transform.position).sqrMagnitude;
                    if (dSq < bestDistSq) { bestDistSq = dSq; nearest = npc; }
                }
                return nearest != null ? nearest.GetTargetId() : 0UL;
            };

            svc.Initialize(this, targetFinder);
            if (Debug.isDebugBuild)
            {
                Debug.Log("[NetworkPlayer] InitializeSkillInputService: SkillInputService ready (owner-only)");
            }
        }

        /// <summary>
        /// T-INP-02: Единая точка primary attack input (ЛКМ + K-fallback).
        /// Делегирует в SkillInputService.TryActivate(Primary).
        /// </summary>
        private void HandlePrimaryAttackInput()
        {
            if (SkillInputService.Instance == null) return;
            SkillInputService.Instance.TryActivate(SkillInputSlot.Primary);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            // FIX (2026-06-04, см. OnNetworkSpawn): для scene-placed non-player
            // NetworkObject'ов у нас нет ни camera, ни inventory, ни ship state —
            // вся cleanup-логика ниже player-specific и должна быть пропущена.
            // Тот же надёжный дискриминатор, что и в OnNetworkSpawn: наличие
            // компонента NetworkPlayerSpawner на GameObject.
            if (GetComponent<NetworkPlayerSpawner>() != null)
            {
                return;
            }

            // NOTE (cleanup Phase 9, 2026-06-05): legacy _inventory.SaveToPrefs() убран —
            // v2 серверный инвентарь авторитативен, persistence = ответственность сервера.

            if (_myCamera != null) Destroy(_myCamera.gameObject);
            if (_inShip && _currentShip != null) _currentShip.RemovePilot(OwnerClientId);

            // T-G07: отписка от сбора
            UnsubscribeFromGatherClientState();

            // T-RTC06: Unregister от CombatServer
            UnregisterFromCombatServer();
        }
        
        // NOTE: FloatingOriginMP event handling removed - scene-based doesn't use world shifting

        // ==================== КАМЕРА ====================

        private void SpawnCamera()
        {
            if (cameraPrefab != null)
            {
                // ИСПРАВЛЕНО: камера спавнится как НЕЗАВИСИМЫЙ объект (НЕ дочерний).
                // Parenting камеры к игроку вызывало конфликт с FloatingOriginMP:
                // - camera.scene.GetRootGameObjects() захватывало игрока (root-объект)
                // - FloatingOriginMP пытался рапаренчить игрока под WorldRoot → краш иерархии
                // - Двойное смещение позиции: из parent и из LateUpdate
                var camObj = Instantiate(cameraPrefab.gameObject);
                camObj.name = $"ThirdPersonCamera_{OwnerClientId}";
                _myCamera = camObj.GetComponent<ThirdPersonCamera>();
                if (_myCamera != null)
                {
                    _myCamera.SetTarget(transform);
                    _myCamera.InitializeCamera();
                }
            }
            else
            {
                _myCamera = FindAnyObjectByType<ThirdPersonCamera>();
                if (_myCamera != null)
                {
                    _myCamera.SetTarget(transform);
                    _myCamera.InitializeCamera();
                }
            }
        }

        // ==================== ИНВЕНТАРЬ ====================

        private void SpawnInventory()
        {
            // Phase 4 (INVENTORY_V2_REFACTOR.md) + Phase 9 cleanup (2026-06-05):
            // TAB-колесо — сцен-placed [InventoryWheel] GameObject, инвентарь — v2 server-authoritative.
            // Метод оставлен как hook для будущей per-player inventory-instance логики.
        }

        // ==================== ВВОД ====================

        private void Update()
        {
            if (!IsOwner) return;

            // Update PlayerChunkTracker for server-side chunk streaming
            if (_playerChunkTracker != null)
            {
                _playerChunkTracker.ForceUpdatePlayerChunk(OwnerClientId, transform.position);
            }
            

            // F — переключение режимов
            // Guard: пропускаем RPC если NGO не готов или игрок не спавнен
            // (защита от NRE в __endSendRpc при scene transition / domain reload / shutdown)
            if (Keyboard.current.fKey.wasPressedThisFrame
                && NetworkManager.Singleton != null
                && IsSpawned)
            {
                // Q9 (принято 2026-06-19): F внутри CommPanel = стандартное поведение,
                // но CommPanel закрывается.
                if (ProjectC.Docking.UI.CommPanelWindow.Instance != null
                    && ProjectC.Docking.UI.CommPanelWindow.Instance.IsOpen)
                {
                    ProjectC.Docking.UI.CommPanelWindow.Instance.SetOpen(false);
                }

                // T-G05: Resource gathering — высший приоритет (выше boarding).
                // Если рядом ResourceNode и есть инструмент (MetaRequirement OK) →
                // F запустит сбор, НЕ посадку.
                if (!_inShip && TryGatherNearestNode())
                {
                    // Сбор поставлен в очередь (через MetaReq → OnAccessAllowed → gather).
                    // Не выполняем boarding.
                }
                // T-C04: Crafting station — приоритет между gathering и boarding.
                // F открывает CraftingWindow (через CraftingClientState.RequestSubscribe → RPC).
                else if (!_inShip && TryInteractNearestCraftingStation())
                {
                    // Запрос отправлен; окно откроется в OnSnapshotReceived (T-C05/T-C06 wire-in).
                }
                // COMPOSITE SHIP (Phase 3): Door interaction — приоритет выше ship boarding,
                // ниже crafting. F на двери открывает/закрывает (Toggle).
                else if (!_inShip && TryInteractNearestDoor())
                {
                    // DoorController.Toggle() вызван
                }
                // Ship Key Subsystem: разделение выхода/посадки.
                // - Выход (_inShip == true) — без проверки ключа (он уже сидит).
                // - Посадка (_inShip == false) — требуется ключ → шлём RequestCanBoardRpc
                //   и ждём ответа через ReceiveShipKeyCanBoardResponseTargetRpc.
                else if (_inShip)
                {
                    SubmitSwitchModeRpc();
                }
                else
                {
                    var nearestShip = FindNearestShip();
                    if (nearestShip == null) return; // не у корабля — ничего
                    // Защита от двойного F (race condition между request и response).
                    if (Time.unscaledTime - _lastCanBoardRequestTime < CAN_BOARD_REQUEST_TIMEOUT
                        && _pendingCanBoardShipId == nearestShip.NetworkObjectId)
                    {
                        return; // ещё ждём ответ
                    }
                    _lastCanBoardRequestTime = Time.unscaledTime;
                    _pendingCanBoardShipId = nearestShip.NetworkObjectId;
                    // T-KEY-06: direct call to MetaRequirementClientState (ownership-priority).
                    // ShipKeyClientState оставлен как legacy [Obsolete] alias, не используем.
                    if (MetaRequirementClientState.Instance != null)
                    {
                        MetaRequirementClientState.Instance.RequestCanUse(nearestShip.NetworkObjectId);
                    }
                    else
                    {
                        Debug.LogWarning("[NetworkPlayer] MetaRequirementClientState.Instance==null. F-key board skipped.");
                    }
                }
            }

            // P — открыть/закрыть CharacterWindow ("P"ress / "P"rofile / "P"erson)
            // (CharacterMenu v1, 2026-06-05: docs/Character-menu/00_OVERVIEW.md §7)
            if (Keyboard.current.pKey.wasPressedThisFrame
                && NetworkManager.Singleton != null
                && IsSpawned)
            {
                var cw = ProjectC.UI.Client.CharacterWindow.Instance;
                if (cw != null) cw.Toggle();
            }

            // T-DOCK-08: T — CommPanel (Dispatch). Q10: только если пилотирует.
            if (Keyboard.current.tKey.wasPressedThisFrame
                && NetworkManager.Singleton != null
                && IsSpawned)
            {
                // Q10: T игнорируется если игрок не пилотирует корабль
                if (!ProjectC.Docking.Client.DockingClientState.IsLocalPlayerPilotingShip())
                    return;
                var station = ProjectC.Docking.Network.DockingZoneRegistry.LocalPlayerStation
                           ?? ProjectC.Docking.Network.DockingZoneRegistry.LocalPlayerShipStation;
                if (station == null) return;  // не в OuterCommZone
                var cp = ProjectC.Docking.UI.CommPanelWindow.Instance;
                if (cp != null) cp.ToggleOpen();
            }

            if (_inShip)
            {
                // Управление кораблём
                float thrust = 0;
                if (Keyboard.current.wKey.isPressed) thrust += 1;
                if (Keyboard.current.sKey.isPressed) thrust -= 1;

                float yaw = 0;
                if (Keyboard.current.dKey.isPressed) yaw += 1;
                if (Keyboard.current.aKey.isPressed) yaw -= 1;

                float pitch = 0;
                if (Mouse.current.delta.y.ReadValue() > 1) pitch += 1;
                if (Mouse.current.delta.y.ReadValue() < -1) pitch -= 1;

                float vertical = 0;
                if (Keyboard.current.eKey.isPressed) vertical += 1;
                if (Keyboard.current.qKey.isPressed) vertical -= 1;

                bool boost = Keyboard.current.leftShiftKey.isPressed;

                // Guard: пропускаем ship input если NGO/корабль не готовы
                // (защита от NRE в __endSendRpc при scene transition / shutdown)
                if (_currentShip != null
                    && _currentShip.IsSpawned
                    && NetworkManager.Singleton != null
                    && NetworkManager.Singleton.IsListening)
                {
                    _currentShip.SendShipInput(thrust, yaw, pitch, vertical, boost);
                }

                // E в корабле — пока ничего
                if (Keyboard.current.eKey.wasPressedThisFrame && Keyboard.current.qKey.isPressed == false)
                {
                    // Reserved for future: docking/refueling
                }
            }
            else
            {
                // Пеший режим
                _moveInput = Vector2.zero;
                if (Keyboard.current.wKey.isPressed) _moveInput.y += 1;
                if (Keyboard.current.sKey.isPressed) _moveInput.y -= 1;
                if (Keyboard.current.aKey.isPressed) _moveInput.x -= 1;
                if (Keyboard.current.dKey.isPressed) _moveInput.x += 1;
                _jumpPressed = Keyboard.current.spaceKey.wasPressedThisFrame;
                _runPressed = Keyboard.current.leftShiftKey.isPressed;

                // T-P05: owner-only jump notification → server → StatsServer → DEX XP
                if (_jumpPressed && IsOwner)
                {
                    SubmitJumpRpc();
                }

                ProcessMovement(_moveInput, _jumpPressed, _runPressed);

                // E — подбор ИЛИ открыть рынок (если в MarketZone и рядом нет сундука)
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    // MetaRequirement Subsystem (R2-META-REQ-001): если рядом есть
                    // не-корабельный interactable с MetaRequirement (например, LockBox),
                    // — RequestCanUse. Иначе fallback на chest/pickup/market.
                    if (TryInteractNearestMetaRequirement()) return;

                    // T-Q11b: NPC interact (higher priority than market) — открыть диалог.
                    if (TryInteractNearestNpc()) return;

                    FindNearestInteractable();
                    if (_nearestChest != null || _nearestNetworkChest != null)
                    {
                        TryPickup();
                    }
                    else
                    {
                        // Сначала пробуем открыть рынок; если не в зоне — TryPickup
                        if (!ProjectC.Trade.Client.MarketInteractor.TryOpenMarket())
                        {
                            TryPickup();
                        }
                    }
                }

                // T-RTC06 (DEBUG): K — debug-attack nearest NPC. ВРЕМЕННЫЙ (только для verify).
                // Найти ближайший NpcTarget в радиусе 5м → RequestAttackRpc.
                if (Keyboard.current.kKey.wasPressedThisFrame
                    && NetworkManager.Singleton != null
                    && IsSpawned
                    && CombatServer.Instance != null)
                {
                    // T-INP-02: K-fallback для primary attack. Делегирует в SkillInputService.
                    // ЛКМ (mouse 0) обрабатывается в Patch 5.
                    // Animation trigger тоже делает SkillInputService.TryActivate.
                    HandlePrimaryAttackInput();
                }

                // T-INP-03: ЛКМ (Mouse 0) как primary attack — parallel к K-fallback.
                // Guard: только owner, IsSpawned, CombatServer жив.
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
                    && NetworkManager.Singleton != null
                    && IsSpawned
                    && CombatServer.Instance != null)
                {
                    HandlePrimaryAttackInput();
                }

                FindNearestInteractable();
                
                // DEBUG: Teleport to 1M for testing float precision
                // REMOVED: Shift+T teleport to 1M - not needed in scene-based architecture
                
                // DEBUG: Manual ResetOrigin (Shift+R) — removed, scene-based doesn't need FloatingOriginMP
            }
        }

// ==================== ДВИЖЕНИЕ ====================

        private void FixedUpdate()
        {
            if (!IsOwner) return;

            if (_hasServerPosition)
            {
                float dist = Vector3.Distance(transform.position, _serverPosition);
                if (dist > positionCorrectionThreshold)
                {
                    if (Time.frameCount % 60 == 0)
                    {
                        Debug.LogWarning($"[NetworkPlayer] CORRECTING position! dist={dist:F2}, transform.pos={transform.position}, _serverPos={_serverPosition}");
                    }
                    transform.position = Vector3.Lerp(transform.position, _serverPosition, positionCorrectionSpeed * Time.fixedDeltaTime);
                }
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, transform.rotation, rotationSpeed * Time.fixedDeltaTime);
        }

        private void ProcessMovement(Vector2 moveInput, bool jump, bool run)
        {
            _isGrounded = _controller.isGrounded;
            if (_isGrounded && _velocity.y < 0) _velocity.y = -2f;

            // R2-NONE: animator parameters
            if (_animator != null)
            {
                _animator.SetBool("IsGrounded", _isGrounded);
                float speed = moveInput.magnitude > 0.01f ? (run ? runSpeed : walkSpeed) : 0f;
                _animator.SetFloat("Speed", speed);

                // T-NPC-12: InCombat flag — true если экипировано WeaponMain.
                bool inCombat = false;
                if (EquipmentWorld.Instance != null)
                {
                    var equip = EquipmentWorld.Instance.GetEquipment(OwnerClientId);
                    if (equip != null && equip.TryGetItemId(EquipSlot.WeaponMain, out int weaponId) && weaponId > 0)
                        inCombat = true;
                }
                _animator.SetBool("InCombat", inCombat);

                // T-NPC-13: BlendTree MoveX/MoveY (поставлены ниже после вычисления hasInput).
            }

            Vector3 forward = _myCamera != null ? _myCamera.CameraForward : Vector3.forward;
            Vector3 right = _myCamera != null ? _myCamera.CameraRight : Vector3.right;

            Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;
            bool hasInput = moveDirection.magnitude > 0.01f;

            // T-NPC-13: BlendTree MoveX/MoveY (directional locomotion).
            if (_animator != null)
            {
                float moveX = hasInput ? moveInput.x : 0f;
                float moveY = hasInput ? moveInput.y : 0f;
                _animator.SetFloat("MoveX", moveX);
                _animator.SetFloat("MoveY", moveY);
            }

            
            // moveDirection + hasInput уже вычислены выше (для BlendTree MoveX/MoveY).
            if (hasInput)
            {
                moveDirection.Normalize();
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

                float currentSpeed = run ? runSpeed : walkSpeed;
                _controller.Move(moveDirection * currentSpeed * Time.deltaTime);
            }

            if (_isGrounded && jump)
            {
                _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
                if (_animator != null) _animator.SetTrigger("Jump");
            }

            _velocity.y += gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }

        // ==================== КОРАБЛЬ ====================

        /// <summary>
        /// Синхронизация переключения режима всем клиентам
        /// </summary>
        [Rpc(SendTo.Everyone)]
        public void SubmitSwitchModeRpc(RpcParams rpcParams = default)
        {
            // Defense-in-depth (T-KEY-06): если это посадка И сервер знает, что у клиента
            // нет ключа — молча отказываем (визуально ничего не происходит). Клиент уже должен был
            // отказать через MetaRequirementClientState.RequestCanUse, но если он пропустил
            // проверку (чит/баг) — сервер не пустит.
            if (!_inShip) // посадка
            {
                ulong serverClientId = rpcParams.Receive.SenderClientId;
                var nearestShip = FindNearestShip();
                if (nearestShip != null)
                {
                    // T-KEY-06: direct call to MetaRequirementRegistry (ownership-priority for ships).
                    // ShipKeyServer.CanPlayerBoard DEPRECATED.
                    bool allowed = MetaRequirementRegistry.Instance != null
                        && MetaRequirementRegistry.Instance.CanPlayerUse(serverClientId, nearestShip.NetworkObjectId);
                    if (!allowed)
                    {
                        Debug.LogWarning($"[NetworkPlayer] SubmitSwitchModeRpc BLOCKED on server: " +
                                         $"client={serverClientId} ship={nearestShip.NetworkObjectId} (MetaRequirement denied)");
                        return;
                    }
                }

            }
            if (_inShip)
            {
                // Выход из корабля
                if (_currentShip == null) return;

                // Проверка: можно ли выйти
                if (!_currentShip.IsGrounded && _currentShip.CurrentSpeed > 2f) return;

                // Телепорт на палубу
                transform.position = _currentShip.GetExitPosition();

                // COMPOSITE SHIP (Phase 1): отпарентить от корабля
                transform.SetParent(null);

                // Включаем управление — игрок снова может ходить
                _controller.enabled = true;

                // Снимаем пилота (себя)
                _currentShip.RemovePilot(OwnerClientId);
                _currentShip = null;

                _inShip = false;
                ApplyWalkingState();
            }
            else
            {
                // Посадка в ближайший корабль
                _nearestShip = FindNearestShip();
                if (_nearestShip == null) return;

                _currentShip = _nearestShip;
                _inShip = true;

                // COMPOSITE SHIP (Phase 1): игрок НЕ пропадает.
                // _controller отключается чтобы не мог ходить во время пилотирования.
                // Renderer'ы остаются включены — игрок стоит в кресле/у штурвала.
                // В будущем — анимация сидения.
                _controller.enabled = false;

                // COMPOSITE SHIP (Phase 1): парентим игрока к корню корабля.
                // worldPositionStays=true — игрок сохраняет мировую позицию (своё место в кресле).
                // Без парентирования коллайдер игрока остаётся на месте, а корабль улетает →
                // физика «дергается» из-за оверлапа коллайдеров.
                transform.SetParent(_currentShip.ShipRoot, true);

                // Добавляем себя как пилота (кооп)
                _currentShip.AddPilot(this);

                ApplyShipState();
            }
        }

        private ShipController FindNearestShip()
        {
            // REFACTORED: Use InteractableManager instead of FindObjectsByType
            // Zero allocations in hot path
            return InteractableManager.FindNearestShip(transform.position, boardDistance);
        }

        private void ApplyWalkingState()
        {
            if (_myCamera != null)
            {
                _myCamera.SetTargetMode(transform, false);
            }
        }

        private void ApplyShipState()
        {
            if (_currentShip != null && _myCamera != null)
            {
                // COMPOSITE SHIP (Phase 1): камера следит за КОРНЕМ корабля, а не за ShipController
                // (ShipController уже на корне, но явно используем ShipRoot для ясности и для
                // будущей поддержки случая, когда ShipController не на корне).
                _myCamera.SetTargetMode(_currentShip.ShipRoot, true);
            }
        }

        // ==================== ПОДБОР ПРЕДМЕТОВ ====================

        private void FindNearestInteractable()
        {
            _nearestPickup = null;
            _nearestChest = null;
            _nearestNetworkChest = null;
            _nearestNpcLoot = null;

            // First check NEW NetworkChestContainer (higher priority)
            var networkChests = FindObjectsByType<NetworkChestContainer>(FindObjectsInactive.Include);
            foreach (var chest in networkChests)
            {
                if (chest == null || !chest.gameObject.activeSelf || !chest.IsSpawned) continue;
                
                float dist = Vector3.Distance(transform.position, chest.transform.position);
                float openRadius = chest.GetOpenRadius();
                
                if (dist < openRadius)
                {
                    _nearestNetworkChest = chest;
                    return;
                }
            }

            // Fallback: check old ChestContainer
            _nearestChest = InteractableManager.FindNearestChest(transform.position, float.MaxValue);
            
            // Then check pickups if no chest nearby
            if (_nearestChest == null)
            {
                _nearestPickup = InteractableManager.FindNearestPickup(transform.position, pickupRange);
            }

            // T-NPC-03: NpcLootPickup (lower priority than chest/pickup but still within range)
            _nearestNpcLoot = InteractableManager.FindNearestNpcLoot(transform.position, pickupRange);
        }

        // ==================== META REQUIREMENT (E-key для LockBox, дверей и т.д.) ====================
        // Использует FindObjectsByType<NetworkObject> с MetaRequirement в радиусе interactDistance.
        // Сейчас используем простой FindObjectsByType (на каждый E — единичный поиск, без GC issues
        // потому что список обычно < 50 объектов). При необходимости — кэшировать в InteractableManager.

        /// <summary>Возвращает true если нашёл interactable с MetaRequirement и обработал E.
        /// Возвращает false если рядом ничего нет — тогда caller fallback на chest/pickup/market.</summary>
        private bool TryInteractNearestMetaRequirement()
        {
            if (_inShip) return false;

            // Ищем ближайший MetaRequirement с NetworkBehaviour и активным GO
            var all = FindObjectsByType<ProjectC.MetaRequirement.MetaRequirement>(FindObjectsInactive.Exclude);
            ProjectC.MetaRequirement.MetaRequirement nearest = null;
            float minDist = float.MaxValue;
            Vector3 pos = GetEffectivePosition();
            float range = Mathf.Max(pickupRange, boardDistance); // reuse ranges
            foreach (var mr in all)
            {
                if (mr == null || !mr.IsSpawned) continue;
                // Пропускаем корабли (ShipController) — у них свой flow через F
                if (mr.GetComponent<ShipController>() != null) continue;
                float dist;
                var collider = mr.GetComponentInChildren<Collider>();
                if (collider != null)
                {
                    Vector3 closest = collider.bounds.ClosestPoint(pos);
                    dist = Vector3.Distance(pos, closest);
                }
                else
                {
                    dist = Vector3.Distance(pos, mr.transform.position);
                }
                if (dist < range && dist < minDist)
                {
                    minDist = dist;
                    nearest = mr;
                }
            }
            if (nearest == null) return false;

            // Защита от двойного E
            if (Time.unscaledTime - _lastCanUseRequestTime < CAN_USE_REQUEST_TIMEOUT
                && _pendingCanUseInteractableId == nearest.NetworkObjectId)
            {
                return true; // ещё ждём ответ — НЕ даём fallback-логике отработать
            }
            _lastCanUseRequestTime = Time.unscaledTime;
            _pendingCanUseInteractableId = nearest.NetworkObjectId;
            ProjectC.MetaRequirement.MetaRequirementClientState.Instance?.RequestCanUse(nearest.NetworkObjectId);
            return true;
        }

        // T-G05: Resource gathering. Find nearest ResourceNode, call MetaReq check,
        // и если tool OK → OnAccessAllowed → ResourceNode.OnMetaAccessAllowed → GatheringClientState.RequestStartGather.
        // Если tool нет — MetaReq сам покажет отказ (через MetaRequirementToast).
        // Возвращает true если найден ResourceNode (даже если MetaReq deny — toast всё равно покажется).
        private bool TryGatherNearestNode()
        {
            if (_inShip) return false;

            var nearest = InteractableManager.FindNearestResourceNode(GetEffectivePosition(), pickupRange);
            if (nearest == null) return false;

            // Защита от двойного F (тот же паттерн что у MetaRequirement E-key handler).
            // _lastCanUseRequestTime / _pendingCanUseInteractableId — общие с TryInteractNearestMetaRequirement.
            if (Time.unscaledTime - _lastCanUseRequestTime < CAN_USE_REQUEST_TIMEOUT
                && _pendingCanUseInteractableId == nearest.NetworkObjectId)
            {
                return true; // уже ждём ответ сервера на этот нод
            }
            _lastCanUseRequestTime = Time.unscaledTime;
            _pendingCanUseInteractableId = nearest.NetworkObjectId;

            // MetaReq проверит инструмент (All/Any/AtLeastN).
            // - deny → MetaRequirementClientState.OnAccessDenied → toast "Нужен ..."
            // - allow → OnAccessAllowed → ResourceNode.OnMetaAccessAllowed → GatheringClientState.RequestStartGather
            ProjectC.MetaRequirement.MetaRequirementClientState.Instance?.RequestCanUse(nearest.NetworkObjectId);
            return true;
        }

        // T-C04: Crafting station interaction. F → CraftingClientState.RequestSubscribe → RPC.
        // Snapshot вернётся в OnCraftingSnapshotReceived (T-C05) и откроет CraftingWindow (T-C06).
        private bool TryInteractNearestCraftingStation()
        {
            if (_inShip) return false;
            var nearest = InteractableManager.FindNearestCraftingStation(GetEffectivePosition(), pickupRange);
            if (nearest == null)
            {
                // Один раз в секунду лог чтобы не спамить
                if (Time.unscaledTime - _lastCanUseRequestTime > 1f) Debug.Log($"[NetworkPlayer] F-crafting: no station in range (range={pickupRange}, pos={GetEffectivePosition()})");
                return false;
            }
            Debug.Log($"[NetworkPlayer] F-crafting: found station {nearest.NetworkObjectId} '{nearest.DisplayName}' at {nearest.transform.position}");

            // Защита от двойного F на ту же станцию (race condition)
            if (Time.unscaledTime - _lastCanUseRequestTime < CAN_USE_REQUEST_TIMEOUT
                && _pendingCanUseInteractableId == nearest.NetworkObjectId)
            {
                return true;
            }
            _lastCanUseRequestTime = Time.unscaledTime;
            _pendingCanUseInteractableId = nearest.NetworkObjectId;

            // T-C06: открыть окно или переключить на новую станцию
            var wnd = ProjectC.Crafting.UI.CraftingWindow.Instance;
            if (wnd != null)
            {
                wnd.SwitchStation(nearest.NetworkObjectId, nearest.Config);
            }
            ProjectC.Crafting.CraftingClientState.Instance?.RequestSubscribe(nearest.NetworkObjectId);
            return true;
        }

        // COMPOSITE SHIP (Phase 3): Door interaction. F → найти ближайшую дверь → Toggle().
        private bool TryInteractNearestDoor()
        {
            if (_inShip) return false;

            var allDoors = FindObjectsByType<ProjectC.Ship.DoorController>(FindObjectsInactive.Exclude);
            ProjectC.Ship.DoorController nearest = null;
            float minDist = float.MaxValue;
            Vector3 pos = GetEffectivePosition();
            float range = Mathf.Max(pickupRange, boardDistance);

            foreach (var door in allDoors)
            {
                if (door == null || !door.gameObject.activeSelf) continue;

                float dist;
                var col = door.GetComponent<Collider>();
                if (col != null)
                {
                    Vector3 closest = col.bounds.ClosestPoint(pos);
                    dist = Vector3.Distance(pos, closest);
                }
                else
                {
                    dist = Vector3.Distance(pos, door.transform.position);
                }

                if (dist < range && dist < minDist)
                {
                    minDist = dist;
                    nearest = door;
                }
            }

            if (nearest == null) return false;

            Debug.Log($"[NetworkPlayer:{OwnerClientId}] F → Door '{nearest.name}' (dist={minDist:F2}m)");
            nearest.Toggle();
            return true;
        }

        // T-Q11b: NPC dialog trigger. Find nearest NpcController, call RequestTalkToNpc.
        // Higher priority than market/chest. Skip если в корабле.
        private bool TryInteractNearestNpc()
        {
            if (_inShip) return false;
            var allNpcs = FindObjectsByType<ProjectC.Quests.NpcController>(FindObjectsInactive.Exclude);
            ProjectC.Quests.NpcController nearest = null;
            float minDist = float.MaxValue;
            Vector3 pos = GetEffectivePosition();
            float range = Mathf.Max(pickupRange, boardDistance);
            foreach (var npc in allNpcs)
            {
                if (npc == null || npc.Definition == null) continue;
                // Distance check (uses npc.PlayerInRange trigger OR IsWithinDistance fallback)
                bool inRange = npc.PlayerInRange || npc.IsWithinDistance(pos);
                if (!inRange) continue;
                float dist = Vector3.Distance(pos, npc.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = npc;
                }
            }
            if (nearest == null || string.IsNullOrEmpty(nearest.NpcId)) return false;

            if (Debug.isDebugBuild) Debug.Log($"[NetworkPlayer:{OwnerClientId}] E → NPC '{nearest.NpcId}' (dist={minDist:F2}m)");
            // Forward to QuestServer (server validates + sends DialogStepDto back)
            ProjectC.Quests.QuestServer.Instance?.RequestTalkToNpcRpc(nearest.NpcId, null);
            return true;
        }

        private void TryPickup()
        {
            if (_inShip) return;

            // NEW: NetworkChestContainer (priority)
            if (_nearestNetworkChest != null)
            {
                _nearestNetworkChest.TryOpen();
                _nearestNetworkChest = null;
                return;
            }

            // NOTE (cleanup Phase 9, 2026-06-05): старый ChestContainer путь УДАЛЁН.
            // v2 chest = NetworkChestContainer (выше). Legacy ChestContainer.cs оставлен
            // только для ChunkNetworkSpawner (старый API), но TryPickup() не должен к нему
            // обращаться — chest теперь выдаёт loot через NetworkChestContainer.TryOpen →
            // server RPC → InventoryServer.AddItem.

            // PickupItem
            if (_nearestPickup != null)
            {
                // v2 путь: PickupItem.Collect() шлёт RequestPickup через InventoryClientState.
                // Подтверждение через OnInventoryResult → HandlePickupResult деактивирует GO.
                _nearestPickup.Collect();

                // Серверная RPC — скрыть предмет у ВСЕХ
                HidePickupRpc(_nearestPickup.transform.position);
                _nearestPickup = null;
                return;
            }

            // T-NPC-03: NPC loot (credits pickup).
            // Если рядом нет chest и нет PickupItem — пробуем NpcLoot.
            if (_nearestNpcLoot != null)
            {
                // Server-side collection: call NpcLootPickup.Collect(clientId).
                if (IsServer)
                {
                    _nearestNpcLoot.Collect(OwnerClientId);
                }
                else
                {
                    // Клиент отправляет RPC серверу.
                    CollectNpcLootServerRpc(_nearestNpcLoot.NetworkObjectId);
                }
                _nearestNpcLoot = null;
            }
        }

        [Rpc(SendTo.Everyone)]
        private void HidePickupRpc(Vector3 targetPos, RpcParams rpcParams = default)
        {
            var pickups = FindObjectsByType<PickupItem>(FindObjectsInactive.Include);
            foreach (var pickup in pickups)
            {
                if (!pickup.gameObject.activeSelf) continue;
                float dist = Vector3.Distance(targetPos, pickup.transform.position);
                if (dist < 3f)
                {
                    pickup.gameObject.SetActive(false);
                    return;
                }
            }
        }

        // T-NPC-03: client → server RPC для сбора NpcLootPickup credits.
        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void CollectNpcLootServerRpc(ulong lootNetId, RpcParams rpcParams = default)
        {
            // Найти NpcLootPickup по NetworkObjectId.
            var loot = FindObjectsByType<ProjectC.AI.NpcLootPickup>(FindObjectsSortMode.None);
            foreach (var l in loot)
            {
                if (l.NetworkObjectId == lootNetId && l.IsSpawned)
                {
                    ulong clientId = rpcParams.Receive.SenderClientId;
                    l.Collect(clientId);
                    return;
                }
            }
            Debug.LogWarning($"[NetworkPlayer] CollectNpcLootServerRpc: loot {lootNetId} not found.");
        }

        [Rpc(SendTo.Everyone)]
        private void OpenChestRpc(Vector3 targetPos, RpcParams rpcParams = default)
        {
            var chests = FindObjectsByType<ChestContainer>(FindObjectsInactive.Include);
            foreach (var chest in chests)
            {
                if (!chest.gameObject.activeSelf) continue;
                float dist = Vector3.Distance(targetPos, chest.transform.position);
                if (dist < chest.openRadius * 1.5f)
                {
                    chest.Open();
                    if (chest.autoDestroy)
                        chest.gameObject.SetActive(false);
                    return;
                }
            }
        }

        public bool HasNearbyInteractable() => !_inShip && (_nearestPickup != null || _nearestChest != null);
        public string GetNearbyInteractableName()
        {
            if (_inShip) return "";
            if (_nearestChest != null) return "Сундук";
            if (_nearestPickup != null && _nearestPickup.itemData != null) return _nearestPickup.itemData.itemName;
            return "";
        }
        public bool IsNearbyChest() => !_inShip && _nearestChest != null;

        /// <summary>
        /// Вызывается сервером для коррекции позиции клиента при рассинхронизации.
        /// 
        /// ВЫКЛЮЧЕНО: Клиентская коррекция вызывает артефакты при работе с FloatingOriginMP.
        /// При сдвиге мира серверная позиция уже устаревает, и коррекция только мешает.
        /// 
        /// TODO: Если нужна коррекция — реализовать через WorldAware систему.
        /// </summary>
        [Rpc(SendTo.Owner)]
        public void ApplyServerPositionRpc(Vector3 serverPosition, RpcParams rpcParams = default)
        {
            // ОТКЛЮЧЕНО: Полностью игнорируем серверную коррекцию позиции
            // Это решает проблему артефактов при работе с FloatingOriginMP
            // Debug.Log($"[NetworkPlayer] ApplyServerPositionRpc: игнорируем (серверная позиция={serverPosition})");
        }

        // ==================== LEGACY TRADE/CONTRACT RPC REMOVED (C1-cleanup 2026-06-05) ====================
        // C1-cleanup: удалены 9 legacy RPC:
        //   - Trade: TradeBuyServerRpc, TradeSellServerRpc, TradeResultClientRpc
        //   - Contracts: ContractRequestServerRpc, ContractAcceptServerRpc, ContractCompleteServerRpc,
        //                ContractFailServerRpc, ContractListClientRpc, ContractResultClientRpc
        // Все они проксировали в v1 TradeMarketServer / ContractSystem / ContractBoardUI,
        // которые удалены в C1. v2-цепочка идёт через:
        //   - MarketServer.RequestBuyRpc / RequestSellRpc / RequestLoadToShipRpc / RequestUnloadFromShipRpc
        //     + NetworkPlayer.ReceiveMarketSnapshotTargetRpc / ReceiveTradeResultTargetRpc
        //   - ContractServer.RequestListRpc / RequestAcceptRpc / RequestCompleteRpc / RequestFailRpc
        //     + NetworkPlayer.ReceiveContractSnapshotTargetRpc / ReceiveContractResultTargetRpc
        // (см. docs/dev/C1_CLEANUP_PLAN_2026-06-05.md и MARKETS_V2_AUDIT_2026-06-05.md §2.1 C4/C5)

        public new bool IsLocalPlayer => IsOwner;
        public ulong GetOwnerId() => OwnerClientId;

        // ==================== TELEPORT RPC (Phase 2) ====================

        /// <summary>
        /// Телепортировать игрока — вызывается с клиента (любой клиент может телепортировать)
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void TeleportServerRpc(Vector3 position)
        {
            TeleportToPosition(position);
        }

        // ==================== T-P05: JUMP RPC (Character Progression) ====================
        // Server-не знает о прыжках (spaceKey.wasPressedThisFrame читается только на owner).
        // Owner→server RPC: нотифицируем сервер о прыжке → publish PlayerJumpedEvent →
        // StatsServer.OnJumped → ApplyXp(JumpXp) → DEX.

        /// <summary>
        /// Owner→server notification: "я только что прыгнул". Вызывается из Update() на
        /// owner client когда _jumpPressed=true. Публикует PlayerJumpedEvent, StatsServer
        /// подписан и начисляет DEX XP.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void SubmitJumpRpc(RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            ProjectC.Core.WorldEventBus.Publish(new ProjectC.Core.PlayerJumpedEvent
            {
                PlayerId = clientId,
                TimestampUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            });
        }

        /// <summary>
        /// Телепортировать всех на позицию — вызывается с сервера
        /// </summary>
        [Rpc(SendTo.Everyone)]
        public void TeleportAllClientRpc(Vector3 position, RpcParams rpcParams = default)
        {
            // Для non-owned объектов просто устанавливаем позицию
            if (!IsOwner)
            {
                _controller.enabled = false;
                transform.position = position;
                _controller.enabled = true;
            }
        }

        /// <summary>
        /// Телепортировать на позицию (серверная логика)
        /// </summary>
        public void TeleportToPosition(Vector3 position)
        {
            Debug.Log($"[NetworkPlayer] Teleport to {position}");

            // Отключаем CharacterController чтобы избежать коллизий
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = true;

            // Сбрасываем velocity
            _velocity = Vector3.zero;

            // Сбрасываем серверную позицию для коррекции
            _serverPosition = position;
            _hasServerPosition = true;

            // Оповещаем всех клиентов
            TeleportAllClientRpc(position);

            // Immediately reset _hasServerPosition to prevent position correction
            // from dragging player back to old position during scene transitions
            _hasServerPosition = false;
        }

        /// <summary>
        /// Телепортировать локального игрока (вызов с владельца)
        /// </summary>
        public void TeleportLocal(Vector3 position)
        {
            if (IsOwner)
            {
                TeleportServerRpc(position);
            }
        }

        // ==================== TRADE V2 RPC TARGETS ====================
        // MarketServer (server-only singleton) вызывает эти методы НА конкретном
        // NetworkPlayer, чтобы доставить snapshot / trade result именно этому
        // клиенту. NGO 2.x нативно поддерживает SendTo.Owner — нужно лишь
        // вызвать метод на player-owned NetworkObject с сервера.

        [Rpc(SendTo.Owner)]
        public void ReceiveMarketSnapshotTargetRpc(MarketSnapshotDto snapshot, RpcParams rpcParams = default)
        {
            Debug.Log($"[NetworkPlayer:{OwnerClientId}] ReceiveMarketSnapshotTargetRpc: loc={snapshot.locationId} items={(snapshot.items?.Length ?? 0)}");
            ProjectC.Trade.Client.MarketClientState.Instance?.OnSnapshotReceived(snapshot);
        }

        [Rpc(SendTo.Owner)]
        public void ReceiveTradeResultTargetRpc(TradeResultDto result, RpcParams rpcParams = default)
        {
            ProjectC.Trade.Client.MarketClientState.Instance?.OnTradeResultReceived(result);
        }

        /// <summary>
        /// Клиентский вызов — попросить сервер установить множитель времени рынка.
        /// </summary>
        public void RequestSetMarketTimeMultiplier(float multiplier)
        {
            if (MarketServer.Instance != null)
            {
                MarketServer.Instance.RequestSetTimeMultiplierRpc(multiplier);
            }
        }

        // ==================== CONTRACT V2 RPC TARGETS ====================
        // ContractServer (server-only singleton) вызывает эти методы НА конкретном
        // NetworkPlayer, чтобы доставить snapshot / result именно этому клиенту.
        // Аналог ReceiveMarketSnapshotTargetRpc / ReceiveTradeResultTargetRpc.
        // Добавлено в C2-этапе миграции контрактов на v2-архитектуру.
        // Legacy RPC ContractListClientRpc / ContractResultClientRpc (lines 788, 804)
        // продолжают работать параллельно для регресса v1-подсистемы; удаляются в C5.

        [Rpc(SendTo.Owner)]
        public void ReceiveContractSnapshotTargetRpc(ProjectC.Trade.Dto.ContractSnapshotDto snapshot, RpcParams rpcParams = default)
        {
            ProjectC.Trade.Client.ContractClientState.Instance?.OnSnapshotReceived(snapshot);
        }

        [Rpc(SendTo.Owner)]
        public void ReceiveContractResultTargetRpc(ProjectC.Trade.Dto.ContractResultDto result, RpcParams rpcParams = default)
        {
            ProjectC.Trade.Client.ContractClientState.Instance?.OnTradeResultReceived(result);
        }

        // ==================== INVENTORY V2 RPC TARGETS ====================
        // Phase 1 (INVENTORY_V2_REFACTOR.md): InventoryServer (server-only singleton) вызывает
        // эти методы НА конкретном NetworkPlayer, чтобы доставить inventory snapshot / result
        // именно этому клиенту. Аналог ReceiveContract*TargetRpc выше.
        //
        // Legacy RPC: НЕТ. Старая NetworkInventory (Assets/_Project/Scripts/Core/NetworkInventory.cs)
        // использовала NetworkVariable<InventoryData> для авто-синхронизации ВСЕМ клиентам —
        // новый дизайн шлёт snapshot ТОЛЬКО Owner'у (security: не светим чужие инвентари).

        [Rpc(SendTo.Owner)]
        public void ReceiveInventorySnapshotTargetRpc(ProjectC.Items.Dto.InventorySnapshotDto snapshot, RpcParams rpcParams = default)
        {
            ProjectC.Items.Client.InventoryClientState.Instance?.OnSnapshotReceived(snapshot);
        }

        [Rpc(SendTo.Owner)]
        public void ReceiveInventoryResultTargetRpc(ProjectC.Items.Dto.InventoryResultDto result, RpcParams rpcParams = default)
        {
            ProjectC.Items.Client.InventoryClientState.Instance?.OnResultReceived(result);
        }

        // ==================== SHIP KEY RPCs (Target, Owner) ====================
        // Доставка ответа от ShipKeyServer (после RequestCanBoardRpc) и реестра привязок.
        // Вызываются через netPlayer.ReceiveShipKeyCanBoardResponseTargetRpc(...)
        // из ProjectC.Ship.Key.ShipKeyServer.

        [Rpc(SendTo.Owner)]
        public void ReceiveShipKeyCanBoardResponseTargetRpc(ulong shipNetworkObjectId, bool allowed, string reason, RpcParams rpcParams = default)
        {
            // Сбрасываем race-protection. Клиентский SubmitSwitchModeRpc дёргается изнутри
            // ShipKeyClientState.OnCanBoardResponse (если allowed).
            _pendingCanBoardShipId = ulong.MaxValue;
            _lastCanBoardRequestTime = -10f;
            ProjectC.Ship.Key.ShipKeyClientState.Instance?.OnCanBoardResponse(shipNetworkObjectId, allowed, reason);
        }

        [Rpc(SendTo.Owner)]
        public void ReceiveShipKeyBindingsTargetRpc(ulong[] shipNetIds, int[] keyItemIds, Unity.Collections.FixedString64Bytes[] displayNames, RpcParams rpcParams = default)
        {
            ProjectC.Ship.Key.ShipKeyClientState.Instance?.OnBindingsPushed(shipNetIds, keyItemIds, displayNames);
        }

        // ==================== META REQUIREMENT RPCs (Target, Owner) ====================
        // Доставка ответа от MetaRequirementRegistry (после RequestCanUseRpc) и реестра требований.
        // Вызываются через netPlayer.ReceiveMetaRequirement*TargetRpc(...) из
        // ProjectC.MetaRequirement.MetaRequirementRegistry.

        [Rpc(SendTo.Owner)]
        public void ReceiveMetaRequirementResponseTargetRpc(ulong interactableNetworkObjectId, bool allowed, string reason, RpcParams rpcParams = default)
        {
            // Сбрасываем race-protection. Клиентский OnUseAllowed (например, LockBox-анимация)
            // дёргается изнутри MetaRequirementClientState.OnCanUseResponse.
            _lastCanUseRequestTime = -10f;
            _pendingCanUseInteractableId = ulong.MaxValue;
            ProjectC.MetaRequirement.MetaRequirementClientState.Instance?.OnCanUseResponse(interactableNetworkObjectId, allowed, reason);

            // T-KEY-07: если ответ на F-key ship boarding (allowed) — садим в корабль.
            // _pendingCanBoardShipId устанавливается в Update() при нажатии F.
            if (allowed && _pendingCanBoardShipId == interactableNetworkObjectId)
            {
                _pendingCanBoardShipId = ulong.MaxValue;
                _lastCanBoardRequestTime = -10f;
                Debug.Log($"[NetworkPlayer] MetaRequirement allowed for ship (netId={interactableNetworkObjectId}). Calling SubmitSwitchModeRpc.");
                SubmitSwitchModeRpc();
            }
        }

        /// <summary>
        /// Bulk-push реестра требований с сервера на клиента. Параметр itemIdsArr —
        /// "jagged array" int[][], сериализуется NGO как массив массивов. Для
        /// совместимости с NGO 2.x: используем стандартный механизм сериализации
        /// (читаем/пишем длину + плоский массив).
        /// </summary>
        [Rpc(SendTo.Owner)]
        public void ReceiveMetaRequirementBindingsTargetRpc(
            ulong[] netIds,
            Unity.Collections.FixedString64Bytes[] displayNames,
            int[][] itemIdsArr,
            byte[] logics,
            int[] requiredCounts,
            bool[] consumeOnUses,
            RpcParams rpcParams = default)
        {
            ProjectC.MetaRequirement.MetaRequirementClientState.Instance?.OnRequirementsPushed(
                netIds, displayNames, itemIdsArr, logics, requiredCounts, consumeOnUses);
        }

        // ==================== QUEST V2 RPC TARGETS ====================
        // T-Q07: QuestServer (server-only singleton) вызывает эти методы НА конкретном
        // NetworkPlayer, чтобы доставить snapshot / result именно этому клиенту.
        // Pattern: ReceiveContractSnapshotTargetRpc / ReceiveContractResultTargetRpc (line 848/854).
        // Клиентский state: ProjectC.Quests.Client.QuestClientState.

        [Rpc(SendTo.Owner)]
        public void ReceiveQuestSnapshotTargetRpc(ProjectC.Quests.Dto.QuestSnapshotDto snapshot, RpcParams rpcParams = default)
        {
            ProjectC.Quests.Client.QuestClientState.Instance?.OnQuestSnapshotReceived(snapshot);
        }

        [Rpc(SendTo.Owner)]
        public void ReceiveReputationSnapshotTargetRpc(ProjectC.Quests.Dto.ReputationSnapshotDto snapshot, RpcParams rpcParams = default)
        {
            ProjectC.Reputation.ReputationClientState.Instance?.OnReputationSnapshotReceived(snapshot);
        }

        [Rpc(SendTo.Owner)]
        public void ReceiveNpcAttitudeSnapshotTargetRpc(ProjectC.Quests.Dto.NpcAttitudeSnapshotDto snapshot, RpcParams rpcParams = default)
        {
            ProjectC.Reputation.NpcAttitudeClientState.Instance?.OnNpcAttitudeSnapshotReceived(snapshot);
        }

        // T-G03: Gather result — server pushes tick updates to owner client.
        // GatheringClientState (T-G04) подпишется на это и поднимет events для UI.
        [Rpc(SendTo.Owner)]
        public void ReceiveGatherResultTargetRpc(ProjectC.ResourceNode.GatherResult result, RpcParams rpcParams = default)
        {
            ProjectC.ResourceNode.GatheringClientState.Instance?.OnGatherResultReceived(result);
        }

        // T-P06: Stats snapshot — server pushes PlayerStats update to owner client.
        // StatsClientState (T-P04) примет snapshot и fire'ит OnStatsUpdated event.
        // CharacterWindow (T-P16) подпишется для отображения progress bars / tier class.
        [Rpc(SendTo.Owner)]
        public void ReceiveStatsSnapshotTargetRpc(ProjectC.Stats.Dto.StatsSnapshotDto snapshot, RpcParams rpcParams = default)
        {
            ProjectC.Stats.StatsClientState.Instance?.OnStatsSnapshotReceived(snapshot);
        }

        // T-P09: Equipment snapshot — server pushes EquipmentData update to owner client.
        // EquipmentClientState (T-P08 stub, T-P10 full) примет snapshot. T-P17 UI подпишется.
        [Rpc(SendTo.Owner)]
        public void ReceiveEquipmentSnapshotTargetRpc(ProjectC.Equipment.Dto.EquipmentSnapshotDto snapshot, RpcParams rpcParams = default)
        {
            ProjectC.Equipment.EquipmentClientState.Instance?.OnEquipmentSnapshotReceived(snapshot);
        }

        // T-P09: Equipment equip result (ack/deny of RequestEquip/RequestUnequip RPC).
        // EquipmentClientState (T-P08 stub) примет result. T-P17 UI покажет toast.
        [Rpc(SendTo.Owner)]
        public void ReceiveEquipResultTargetRpc(ProjectC.Equipment.Dto.EquipResultDto result, RpcParams rpcParams = default)
        {
            ProjectC.Equipment.EquipmentClientState.Instance?.OnEquipResultReceived(result);
        }

        // T-P13: Skills snapshot — server pushes learned skills to owner client.
        // SkillsClientState (T-P13) примет snapshot и fire'ит OnSkillsUpdated event.
        // CharacterWindow (T-P14) подпишется для отображения skill rows с LOCKED/AVAILABLE/LEARNED states.
        [Rpc(SendTo.Owner)]
        public void ReceiveSkillsSnapshotTargetRpc(ProjectC.Skills.Dto.SkillsSnapshotDto snapshot, RpcParams rpcParams = default)
        {
            ProjectC.Skills.SkillsClientState.Instance?.OnSkillsSnapshotReceived(snapshot);
        }

        // T-P13: Skills learn/forget result (ack/deny of RequestLearnSkillRpc/RequestForgetSkillRpc).
        // SkillsClientState (T-P13) примет result. T-P14 UI покажет toast.
        [Rpc(SendTo.Owner)]
        public void ReceiveSkillResultTargetRpc(ProjectC.Skills.Dto.SkillResultDto result, RpcParams rpcParams = default)
        {
            ProjectC.Skills.SkillsClientState.Instance?.OnSkillResultReceived(result);
        }

        // T-C03: Crafting result — server pushes ack/deny/progress to owner client.
        // CraftingClientState (T-C05) подпишется на это и поднимет events для CraftingProgressController.
        [Rpc(SendTo.Owner)]
        public void ReceiveCraftingResultTargetRpc(ProjectC.Crafting.CraftingResultDto result, RpcParams rpcParams = default)
        {
            if (Debug.isDebugBuild) Debug.Log($"[NetworkPlayer] ReceiveCraftingResultTargetRpc CALLED: station={result.stationNetId} code={result.code} msg={result.message}");
            var inst = ProjectC.Crafting.CraftingClientState.Instance;
            if (inst != null) inst.OnCraftingResultReceived(result);
        }

        // T-C03: Crafting snapshot — full state of one station (sent on subscribe + after each mutation).
        [Rpc(SendTo.Owner)]
        public void ReceiveCraftingSnapshotTargetRpc(ProjectC.Crafting.CraftingSnapshotDto snapshot, RpcParams rpcParams = default)
        {
            if (Debug.isDebugBuild) Debug.Log($"[NetworkPlayer] ReceiveCraftingSnapshotTargetRpc CALLED: station={snapshot.stationNetId} state={snapshot.jobState} owner={snapshot.ownerClientId} recipe={snapshot.activeRecipeId}");
            var inst = ProjectC.Crafting.CraftingClientState.Instance;
            if (inst != null) inst.OnCraftingSnapshotReceived(snapshot);
        }

        [Rpc(SendTo.Owner)]
        public void ReceiveQuestResultTargetRpc(ProjectC.Quests.Dto.QuestResultDto result, RpcParams rpcParams = default)
        {
            ProjectC.Quests.Client.QuestClientState.Instance?.OnQuestResultReceived(result);
        }

        [Rpc(SendTo.Owner)]
        public void ReceiveReputationResultTargetRpc(ProjectC.Quests.Dto.ReputationResultDto result, RpcParams rpcParams = default)
        {
            ProjectC.Quests.Client.QuestClientState.Instance?.OnReputationResultReceived(result);
        }

        // Server-push notification: EventDriven quest auto-discovered.
        [Rpc(SendTo.Owner)]
        public void ReceiveQuestDiscoveredTargetRpc(string questId, string displayName, RpcParams rpcParams = default)
        {
            // T-Q11: QuestClientState.OnQuestDiscovered (UI "New Quest!" toast).
            // T-Q07: route через QuestClientState.RaiseOnQuestDiscovered (event'ы в .NET — only raisable from declaring type).
            if (Debug.isDebugBuild) Debug.Log($"[NetworkPlayer:{OwnerClientId}] ReceiveQuestDiscovered: {questId} '{displayName}'");
            ProjectC.Quests.Client.QuestClientState.Instance?.RaiseOnQuestDiscovered(questId, displayName);
        }

        // ==================== EXCHANGE (Resources Exchanger) RPC TARGETS ====================
        // T-E03: ExchangeServer вызывает этот метод НА конкретном NetworkPlayer,
        // чтобы доставить результат Pack/Unpack именно этому клиенту.
        // Аналог ReceiveTradeResultTargetRpc / ReceiveCraftingResultTargetRpc.
        // Паттерн: TargetRpc на owner'а.

        [Rpc(SendTo.Owner)]
        public void ReceiveExchangeResultTargetRpc(ProjectC.Trade.Dto.ExchangeResultDto result, RpcParams rpcParams = default)
        {
            ProjectC.Trade.Client.ExchangeClientState.Instance?.OnExchangeResultReceived(result);
        }

        // ==================== DIALOG V2 RPC TARGETS ====================
        // T-Q10: QuestServer (server-only singleton) шлёт dialog steps конкретному клиенту.
        // Клиентский handler: T-Q11 UI binding (DialogWindow.OnStepReceived).

        [Rpc(SendTo.Owner)]
        public void ReceiveDialogStepTargetRpc(ProjectC.Quests.Dto.DialogStepDto step, RpcParams rpcParams = default)
        {
            if (Debug.isDebugBuild) Debug.Log($"[NetworkPlayer:{OwnerClientId}] ReceiveDialogStep: tree={step.treeId} node={step.nodeId} options={step.options?.Length ?? 0} isEnd={step.isEnd}");
            ProjectC.Quests.Client.QuestClientState.Instance?.RaiseOnDialogStepReceived(step);
        }

        [Rpc(SendTo.Owner)]
        public void ReceiveDialogActionResultTargetRpc(ProjectC.Quests.Dto.DialogActionResultDto result, RpcParams rpcParams = default)
        {
            if (Debug.isDebugBuild) Debug.Log($"[NetworkPlayer:{OwnerClientId}] ReceiveDialogActionResult: type={result.actionType} success={result.success}");
            ProjectC.Quests.Client.QuestClientState.Instance?.RaiseOnDialogActionResultReceived(result);
        }

        public void RequestEndConversation()
        {
            ProjectC.Quests.QuestServer.Instance?.RequestEndConversationRpc();
        }

        /// <summary>T-Q11a: client-side wrappers для E-key trigger.</summary>
        public void RequestTalkToNpc(string npcId, string treeIdHint = null)
        {
            ProjectC.Quests.QuestServer.Instance?.RequestTalkToNpcRpc(npcId, treeIdHint);
        }

        public void RequestAdvanceDialogue(string treeId, string nodeId, int optionIndex, string npcId)
        {
            ProjectC.Quests.QuestServer.Instance?.RequestAdvanceDialogueRpc(treeId, nodeId, optionIndex, npcId);
        }

        // ==========================================================
        // T-G07: Gather animation (player scale pulse)
        // ==========================================================

        /// <summary>Подписка на GatheringClientState события (вызывается в OnNetworkSpawn).</summary>
        private void TrySubscribeToGatherClientState()
        {
            if (_subscribedToGather) return;
            var state = ProjectC.ResourceNode.GatheringClientState.Instance;
            if (state == null) return;
            state.OnGatherProgress += OnGatherProgress;
            state.OnGatherCompleted += OnGatherEnded;
            state.OnGatherInterrupted += OnGatherEnded;
            state.OnGatherDenied += OnGatherEnded;
            state.OnGatherCancelled += OnGatherCancelled;
            _subscribedToGather = true;
        }

        private void UnsubscribeFromGatherClientState()
        {
            if (!_subscribedToGather) return;
            var state = ProjectC.ResourceNode.GatheringClientState.Instance;
            if (state != null)
            {
                state.OnGatherProgress -= OnGatherProgress;
                state.OnGatherCompleted -= OnGatherEnded;
                state.OnGatherInterrupted -= OnGatherEnded;
                state.OnGatherDenied -= OnGatherEnded;
                state.OnGatherCancelled -= OnGatherCancelled;
            }
            _subscribedToGather = false;
            StopGatherPulse();
        }

        private void OnGatherProgress(float progress)
        {
            if (!_gatherActive)
            {
                _gatherActive = true;
                _originalScale = transform.localScale;
                StartGatherPulse();
            }
        }

        private void OnGatherEnded(string _unused1, int _unused2, bool _unused3)
        {
            if (_gatherActive) StopGatherPulse();
        }

        private void OnGatherEnded(string _unused)
        {
            if (_gatherActive) StopGatherPulse();
        }

        private void OnGatherCancelled()
        {
            if (_gatherActive) StopGatherPulse();
        }

        private void StartGatherPulse()
        {
            if (_gatherPulseCoroutine != null) StopCoroutine(_gatherPulseCoroutine);
            _gatherPulseCoroutine = StartCoroutine(GatherPulseLoop());
        }

        private void StopGatherPulse()
        {
            _gatherActive = false;
            if (_gatherPulseCoroutine != null)
            {
                StopCoroutine(_gatherPulseCoroutine);
                _gatherPulseCoroutine = null;
            }
            transform.localScale = _originalScale;
        }

        private System.Collections.IEnumerator GatherPulseLoop()
        {
            if (_gatherScaleAmplitude <= 0f) yield break;
            float amp = _gatherScaleAmplitude;
            float period = Mathf.Max(0.01f, _gatherPulsePeriod);
            while (true)
            {
                float t = Mathf.Sin(Time.time * (2f * Mathf.PI / period));
                transform.localScale = _originalScale * (1.0f + amp * t);
                yield return null;
            }
        }
    }
}
