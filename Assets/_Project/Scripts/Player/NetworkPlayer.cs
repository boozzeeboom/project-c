using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectC.Core;
using ProjectC.Items;
using ProjectC.Network;
using ProjectC.Trade;
using ProjectC.Trade.Dto;
using ProjectC.Trade.Network;
using ProjectC.UI;
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
        private Vector3 _velocity;
        private bool _isGrounded;
        private ThirdPersonCamera _myCamera;
        private Inventory _inventory;
        private InventoryUI _inventoryUI;

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

        // NetworkObject
        private NetworkObject networkObject;

        // Chunk tracking
        private PlayerChunkTracker _playerChunkTracker;



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

                // Загружаем инвентарь из сохранения (после реконнекта)
                if (_inventory != null)
                {
                    _inventory.LoadFromPrefs();
                    if (_inventory.GetTotalItemCount() > 0 && _inventoryUI != null)
                    {
                        _inventoryUI.TriggerSectorFlash();
                    }
                }

                ApplyWalkingState();

                // Find PlayerChunkTracker for chunk streaming
                var chunkTrackers = FindObjectsByType<PlayerChunkTracker>();
                if (chunkTrackers.Length > 0)
                {
                    _playerChunkTracker = chunkTrackers[0];
                }

            }
            else
            {
                _controller.enabled = false;
            }
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

            // Сохраняем инвентарь перед отключением
            if (_inventory != null && _inventory.GetTotalItemCount() > 0)
            {
                _inventory.SaveToPrefs();
            }

            if (_myCamera != null) Destroy(_myCamera.gameObject);
            if (_inventoryUI != null) Destroy(_inventoryUI.gameObject);
            if (_inShip && _currentShip != null) _currentShip.RemovePilot(OwnerClientId);
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
            var invObj = new GameObject("Inventory");
            invObj.transform.SetParent(transform);
            _inventory = invObj.AddComponent<Inventory>();

            var uiObj = new GameObject("InventoryUI");
            _inventoryUI = uiObj.AddComponent<InventoryUI>();
            // REFACTORED (R3-001): Используем SetInventory() вместо reflection
            _inventoryUI.SetInventory(_inventory);
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
                SubmitSwitchModeRpc();
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

                ProcessMovement(_moveInput, _jumpPressed, _runPressed);

                // E — подбор ИЛИ открыть рынок (если в MarketZone и рядом нет сундука)
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
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

            Vector3 forward = _myCamera != null ? _myCamera.CameraForward : Vector3.forward;
            Vector3 right = _myCamera != null ? _myCamera.CameraRight : Vector3.right;

            Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;
            bool hasInput = moveDirection.magnitude > 0.01f;

            if (hasInput)
            {
                moveDirection.Normalize();
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

                float currentSpeed = run ? runSpeed : walkSpeed;
                _controller.Move(moveDirection * currentSpeed * Time.deltaTime);
            }

            if (_isGrounded && jump)
                _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);

            _velocity.y += gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }

        // ==================== КОРАБЛЬ ====================

        /// <summary>
        /// Синхронизация переключения режима всем клиентам
        /// </summary>
        [Rpc(SendTo.Everyone)]
        private void SubmitSwitchModeRpc(RpcParams rpcParams = default)
        {
            if (_inShip)
            {
                // Выход из корабля
                if (_currentShip == null) return;

                // Проверка: можно ли выйти
                if (!_currentShip.IsGrounded && _currentShip.CurrentSpeed > 2f) return;

                // Телепорт на палубу
                transform.position = _currentShip.GetExitPosition();

                // Показываем игрока
                _controller.enabled = true;
                foreach (var r in _playerRenderers) r.enabled = true;
                foreach (var c in _playerColliders) c.enabled = true;

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

                // Скрываем игрока (ВСЕ renderer и collider)
                _controller.enabled = false;
                foreach (var r in _playerRenderers) r.enabled = false;
                foreach (var c in _playerColliders) c.enabled = true; // Collider оставляем для рейкаста

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
                _myCamera.SetTarget(transform);
                _myCamera.SetShipMode(false);
            }
        }

        private void ApplyShipState()
        {
            if (_currentShip != null && _myCamera != null)
            {
                _myCamera.SetTarget(_currentShip.transform);
                _myCamera.SetShipMode(true);
            }
        }

        // ==================== ПОДБОР ПРЕДМЕТОВ ====================

        private void FindNearestInteractable()
        {
            _nearestPickup = null;
            _nearestChest = null;
            _nearestNetworkChest = null;

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

            // Old ChestContainer
            if (_nearestChest != null)
            {
                var loot = _nearestChest.GetLootItems();
                if (_inventory != null && loot.Count > 0)
                {
                    _inventory.AddMultipleItems(loot);
                    if (_inventoryUI != null) _inventoryUI.TriggerSectorFlash();
                }
                OpenChestRpc(_nearestChest.transform.position);
                _nearestChest = null;
                return;
            }

            // PickupItem
            if (_nearestPickup != null)
            {
                if (_inventory != null)
                    _inventory.AddItem(_nearestPickup.itemData);
                if (_inventoryUI != null) _inventoryUI.TriggerSectorFlash();

                // Серверная RPC — скрыть предмет у ВСЕХ
                HidePickupRpc(_nearestPickup.transform.position);
                _nearestPickup = null;
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
    }
}
