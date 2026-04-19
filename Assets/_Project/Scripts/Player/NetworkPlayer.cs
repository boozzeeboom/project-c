using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectC.Core;
using ProjectC.Items;
using ProjectC.Trade;
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
        [SerializeField] private float positionCorrectionThreshold = 2f;

        [Tooltip("Скорость плавной коррекции позиции")]
        [SerializeField] private float positionCorrectionSpeed = 5f;

        // Серверная позиция для коррекции
        private Vector3 _serverPosition;
        private bool _hasServerPosition = false;
        
        /// <summary>
        /// Cooldown после сдвига мира — игнорируем серверную коррекцию пока мира не устаканится.
        /// </summary>
        private float _worldShiftCooldown = 0f;
        private const float WORLD_SHIFT_COOLDOWN_DURATION = 1f;

        public bool IsInShip => _inShip;
        public ShipController CurrentShip => _currentShip;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            networkObject = GetComponent<NetworkObject>();
            _controller = GetComponent<CharacterController>();

            // ПРИМЕЧАНИЕ: NetworkTransform.InterpolatePosition/Rotation/Scale 
            // отключаются ВРУЧНУЮ в Unity Editor на префабе Player.prefab
            // (API отличается в разных версиях Unity/NGO)

            // Находим ВСЕ Renderer и Collider на объекте (включая дочерние)
            GetComponentsInChildren(true, _playerRenderers);
            GetComponentsInChildren(true, _playerColliders);

            // Убираем CharacterController и сам NetworkObject из списков
            _playerRenderers.RemoveAll(r => r == null);
            _playerColliders.RemoveAll(c => c == null || c is CharacterController);

            // Отключаем старый PlayerController (если остался от legacy)
            var legacyController = GetComponent<PlayerController>();
            if (legacyController != null) legacyController.enabled = false;
            
            // Подписываемся на событие сдвига мира (для FloatingOriginMP)
            // После сдвига мира нужно сбросить клиентскую коррекцию чтобы избежать артефактов
            ProjectC.World.Streaming.FloatingOriginMP.OnWorldShifted += OnWorldShifted;

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

            // Сохраняем инвентарь перед отключением
            if (_inventory != null && _inventory.GetTotalItemCount() > 0)
            {
                _inventory.SaveToPrefs();
            }

            if (_myCamera != null) Destroy(_myCamera.gameObject);
            if (_inventoryUI != null) Destroy(_inventoryUI.gameObject);
            if (_inShip && _currentShip != null) _currentShip.RemovePilot(OwnerClientId);
            
            // Отписываемся от события сдвига мира
            ProjectC.World.Streaming.FloatingOriginMP.OnWorldShifted -= OnWorldShifted;
        }
        
        public override void OnDestroy()
        {
            // Отписываемся от события сдвига мира (для безопасности)
            ProjectC.World.Streaming.FloatingOriginMP.OnWorldShifted -= OnWorldShifted;
            base.OnDestroy();
        }
        
        /// <summary>
        /// Обработчик события сдвига мира от FloatingOriginMP.
        /// После сдвига мира сбрасываем клиентскую коррекцию позиции чтобы избежать артефактов.
        /// </summary>
        private void OnWorldShifted(Vector3 offset)
        {
            // ЗАЩИТА: проверяем IsOwner — только владелец должен обрабатывать сдвиг
            // На хосте могут быть ДВА NetworkPlayer с одинаковым OwnerClientId=0
            // (свой игрок + ghost/clone). IsOwner гарантирует что это НАШ игрок.
            if (!IsOwner)
            {
                return;
            }
            
            // ПРИМЕЧАНИЕ: Проверка позиции >500k УДАЛЕНА!
            // FloatingOriginMP теперь пропускает игрока с тегом "Player" на больших позициях.
            // Этот метод вызывается ПОСЛЕ сдвига мира, поэтому позиция уже должна быть корректной.
            // Если позиция огромная — это означает что сдвиг не применился к игроку,
            // и мы должны обработать это чтобы избежать артефактов.
            
            Debug.Log($"[NetworkPlayer] OnWorldShifted: offset={offset}, transform.position={transform.position}, IsOwner={IsOwner}");
            
            // Сбрасываем клиентскую коррекцию позиции
            // Это предотвращает артефакты которые возникают из-за рассинхронизации после сдвига
            _hasServerPosition = false;
            
            // Запускаем cooldown — игнорируем серверную коррекцию 1 секунду пока мир устаканится
            _worldShiftCooldown = WORLD_SHIFT_COOLDOWN_DURATION;
            
            // Сбрасываем velocity чтобы избежать рывков после сдвига
            _velocity = Vector3.zero;
            
            Debug.Log($"[NetworkPlayer] OnWorldShifted: коррекция сброшена, позиция={transform.position}, cooldown={_worldShiftCooldown}s");
        }

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
            if (Keyboard.current.fKey.wasPressedThisFrame)
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

                if (_currentShip != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                    _currentShip.SendShipInput(thrust, yaw, pitch, vertical, boost);

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

                // E — подбор
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    FindNearestInteractable();
                    TryPickup();
                }

                FindNearestInteractable();
                
                // DEBUG: Teleport to 1M for testing float precision
                if (Keyboard.current.tKey.wasPressedThisFrame && Keyboard.current.leftShiftKey.isPressed)
                {
                    TeleportToPosition(new Vector3(1000000f, 5f, 0f));
                    // После телепорта вызываем ResetOrigin с задержкой (ждём синхронизации)
                    Invoke(nameof(AutoResetOriginAfterTeleport), 0.5f);
                }
                
                // DEBUG: Manual ResetOrigin (Shift+R) — принудительный сброс
                if (Keyboard.current.rKey.wasPressedThisFrame && Keyboard.current.leftShiftKey.isPressed)
                {
                    Debug.Log("[NetworkPlayer] Manual ResetOrigin requested");
                    var fo = FindAnyObjectByType<ProjectC.World.Streaming.FloatingOriginMP>();
                    if (fo != null)
                    {
                        // КРИТИЧНО: запоминаем текущую позицию, обнуляем offset, затем телепортируем на локальную позицию
                        Vector3 worldPos = transform.position;
                        Debug.Log($"[NetworkPlayer] Current world position: {worldPos}");
                        
                        // Обнуляем totalOffset
                        fo.ResetOffset();
                        
                        // Телепортируем на локальную позицию (5, 5, 0) — рядом с origin
                        // Это "локальная" позиция относительно TradeZones
                        Vector3 localPos = new Vector3(5f, 5f, 0f);
                        Debug.Log($"[NetworkPlayer] Teleporting to local position: {localPos}");
                        
                        // Отключаем на мгновение CC чтобы избежать коллизий
                        _controller.enabled = false;
                        transform.position = localPos;
                        _controller.enabled = true;
                        _velocity = Vector3.zero;
                        
                        // После телепорта вызываем ResetOrigin для сдвига мира
                        fo.ResetOrigin();
                        
                        Debug.Log($"[NetworkPlayer] After ResetOrigin: player at {transform.position}, world shifted");
                    }
                    else
                    {
                        Debug.LogWarning("[NetworkPlayer] FloatingOriginMP not found!");
                    }
                }
            }
        }

        // ==================== ДВИЖЕНИЕ ====================

        private void FixedUpdate()
        {
            // Плавная коррекция позиции только для локального игрока (Owner)
            if (!IsOwner || _inShip) return;
            
            // Уменьшаем cooldown
            if (_worldShiftCooldown > 0)
            {
                _worldShiftCooldown -= Time.fixedDeltaTime;
                // Игнорируем серверную коррекцию пока cooldown активен
                return;
            }

            if (_hasServerPosition)
            {
                float dist = Vector3.Distance(transform.position, _serverPosition);
                if (dist > positionCorrectionThreshold)
                {
                    // Рассинхронизация — плавно возвращаем к серверной позиции
                    transform.position = Vector3.Lerp(transform.position, _serverPosition, positionCorrectionSpeed * Time.fixedDeltaTime);
                }
                else
                {
                    // Позиция синхронизирована — отключаем коррекцию
                    _hasServerPosition = false;
                }
            }
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

        // ==================== TRADE RPC (Сессия 5) ====================

        /// <summary>
        /// Купить товар — клиент запрашивает, сервер валидирует
        /// </summary>
        [Rpc(SendTo.Server)]
        public void TradeBuyServerRpc(string itemId, int quantity, string locationId)
        {
            // Серверная логика в TradeMarketServer
            if (TradeMarketServer.Instance != null)
            {
                // Передаём свой OwnerClientId
                TradeMarketServer.Instance.BuyItemServerRpc(itemId, quantity, locationId, OwnerClientId);
            }
            else
            {
                Debug.LogWarning("[NetworkPlayer] TradeMarketServer не найден");
            }
        }

        /// <summary>
        /// Продать товар — клиент запрашивает, сервер валидирует
        /// </summary>
        [Rpc(SendTo.Server)]
        public void TradeSellServerRpc(string itemId, int quantity, string locationId)
        {
            if (TradeMarketServer.Instance != null)
            {
                TradeMarketServer.Instance.SellItemServerRpc(itemId, quantity, locationId, OwnerClientId);
            }
            else
            {
                Debug.LogWarning("[NetworkPlayer] TradeMarketServer не найден");
            }
        }

        /// <summary>
        /// Результат торговли — сервер отправляет конкретному клиенту
        /// 
        /// СЕССИЯ 8L FIX: Фильтруем по targetClientId вместо IsOwner.
        /// Это решает проблему когда на хосте IsOwner проверяется относительно локального клиента,
        /// а не того кому предназначался RPC.
        /// </summary>
        [ClientRpc]
        public void TradeResultClientRpc(ulong targetClientId, bool success, string message, float newCredits, string itemId = "", int itemQuantity = 0, bool isPurchase = true,
            ClientRpcParams rpcParams = default)
        {
            // Сессия FIX: Проверяем targetClientId
            ulong localClientId = NetworkManager.Singleton.LocalClientId;
            Debug.Log($"[NetworkPlayer] TradeResultClientRpc: targetClientId={targetClientId}, localClientId={localClientId}, success={success}, itemId={itemId}, IsOwner={IsOwner}");
            
            // Всегда вызываем OnTradeResult если это для нас (добавил для диагностики)
            if (localClientId == targetClientId)
            {
                Debug.Log($"[NetworkPlayer] Вызываю OnTradeResult для клиента {targetClientId}");
                if (TradeUI.Instance != null)
                {
                    TradeUI.Instance.OnTradeResult(success, message, newCredits, itemId, itemQuantity, isPurchase);
                }
                else
                {
                    Debug.LogWarning($"[NetworkPlayer] TradeUI.Instance == null!");
                }
                
                // TradeDebugTools: принудительное обновление UI
                if (TradeDebugTools.Instance != null)
                {
                    Debug.Log($"[NetworkPlayer] Вызываю TradeDebugTools.ForceRefresh()");
                    TradeDebugTools.Instance.ForceRefresh();
                }
                else
                {
                    Debug.LogWarning($"[NetworkPlayer] TradeDebugTools.Instance == null!");
                }
            }
            else
            {
                Debug.Log($"[NetworkPlayer] Этот клиент ({localClientId}) НЕ целевой ({targetClientId}), пропускаю");
            }
        }

        public new bool IsLocalPlayer => IsOwner;
        public ulong GetOwnerId() => OwnerClientId;

        // ==================== CONTRACT RPC (Сессия 7) ====================

        /// <summary>
        /// Запросить доступные контракты — клиент → сервер
        /// </summary>
        [Rpc(SendTo.Server)]
        public void ContractRequestServerRpc(string locationId, RpcParams rpcParams = default)
        {
            if (ContractSystem.Instance != null)
            {
                ContractSystem.Instance.RequestAvailableContractsServerRpc(locationId);
            }
            else
            {
                Debug.LogWarning("[NetworkPlayer] ContractSystem не найден");
            }
        }

        /// <summary>
        /// Принять контракт — клиент → сервер
        /// </summary>
        [Rpc(SendTo.Server)]
        public void ContractAcceptServerRpc(string contractId, RpcParams rpcParams = default)
        {
            if (ContractSystem.Instance != null)
            {
                ContractSystem.Instance.AcceptContractServerRpc(contractId);
            }
            else
            {
                Debug.LogWarning("[NetworkPlayer] ContractSystem не найден");
            }
        }

        /// <summary>
        /// Завершить контракт — клиент → сервер
        /// </summary>
        [Rpc(SendTo.Server)]
        public void ContractCompleteServerRpc(string contractId, string completionLocationId, RpcParams rpcParams = default)
        {
            if (ContractSystem.Instance != null)
            {
                ContractSystem.Instance.CompleteContractServerRpc(contractId, completionLocationId);
            }
            else
            {
                Debug.LogWarning("[NetworkPlayer] ContractSystem не найден");
            }
        }

        /// <summary>
        /// Провалить контракт (отмена) — клиент → сервер
        /// </summary>
        [Rpc(SendTo.Server)]
        public void ContractFailServerRpc(string contractId, RpcParams rpcParams = default)
        {
            if (ContractSystem.Instance != null)
            {
                ContractSystem.Instance.FailContractServerRpc(contractId);
            }
            else
            {
                Debug.LogWarning("[NetworkPlayer] ContractSystem не найден");
            }
        }

        /// <summary>
        /// Список доступных контрактов — сервер → клиент
        /// </summary>
        [Rpc(SendTo.Owner)]
        public void ContractListClientRpc(string serializedContracts, string locationId, RpcParams rpcParams = default)
        {
            if (ContractBoardUI.Instance != null)
            {
                ContractBoardUI.Instance.OnContractsReceived(serializedContracts, locationId);
            }
            else
            {
                Debug.LogWarning("[NetworkPlayer] ContractBoardUI.Instance == null!");
            }
        }

        /// <summary>
        /// Результат контракта — сервер → клиент
        /// </summary>
        [Rpc(SendTo.Owner)]
        public void ContractResultClientRpc(bool success, string message, float reward, RpcParams rpcParams = default)
        {
            if (ContractBoardUI.Instance != null)
            {
                ContractBoardUI.Instance.OnContractResult(success, message, reward);
            }
            else if (TradeUI.Instance != null)
            {
                TradeUI.Instance.OnContractResult(success, message, reward);
            }
        }

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

        // ==================== FLOATING ORIGIN AUTO-FIX ====================

        /// <summary>
        /// Автоматический ResetOrigin после телепорта.
        /// Вызывается с задержкой через Invoke() чтобы дождаться синхронизации NGO.
        /// </summary>
        private void AutoResetOriginAfterTeleport()
        {
            if (!IsOwner) return;
            
            var fo = FindAnyObjectByType<ProjectC.World.Streaming.FloatingOriginMP>();
            if (fo == null)
            {
                Debug.LogWarning("[NetworkPlayer] AutoResetOriginAfterTeleport: FloatingOriginMP not found!");
                return;
            }
            
            // Проверяем что игрок далеко от origin
            float dist = transform.position.magnitude;
            if (dist < 500000f)
            {
                Debug.Log($"[NetworkPlayer] AutoResetOriginAfterTeleport: dist={dist} < 500k, skip");
                return;
            }
            
            Debug.Log($"[NetworkPlayer] AutoResetOriginAfterTeleport: dist={dist}, triggering ResetOrigin");
            
            // Обнуляем totalOffset
            fo.ResetOffset();
            
            // Телепортируем на локальную позицию рядом с origin
            _controller.enabled = false;
            transform.position = new Vector3(5f, 5f, 0f);
            _controller.enabled = true;
            _velocity = Vector3.zero;
            
            // Сдвигаем мир
            fo.ResetOrigin();
            
            Debug.Log($"[NetworkPlayer] AutoResetOriginAfterTeleport: player at {transform.position}, world shifted");
        }
    }
}
