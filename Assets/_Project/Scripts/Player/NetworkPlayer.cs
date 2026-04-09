using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectC.Core;
using ProjectC.Items;
using ProjectC.Trade;
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
        private ShipController _nearestShip;

        // NetworkObject
        private NetworkObject networkObject;

        // ==================== CLIENT-SIDE PREDICTION ====================

        [Header("Коррекция позиции (prediction)")]
        [Tooltip("Порог рассинхронизации для коррекции (м)")]
        [SerializeField] private float positionCorrectionThreshold = 0.5f;

        [Tooltip("Скорость плавной коррекции позиции")]
        [SerializeField] private float positionCorrectionSpeed = 10f;

        // Серверная позиция для коррекции
        private Vector3 _serverPosition;
        private bool _hasServerPosition = false;

        public bool IsInShip => _inShip;
        public ShipController CurrentShip => _currentShip;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            networkObject = GetComponent<NetworkObject>();
            _controller = GetComponent<CharacterController>();

            // Находим ВСЕ Renderer и Collider на объекте (включая дочерние)
            GetComponentsInChildren(true, _playerRenderers);
            GetComponentsInChildren(true, _playerColliders);

            // Убираем CharacterController и сам NetworkObject из списков
            _playerRenderers.RemoveAll(r => r == null);
            _playerColliders.RemoveAll(c => c == null || c is CharacterController);

            // Отключаем старый PlayerController (если остался от legacy)
            var legacyController = GetComponent<PlayerController>();
            if (legacyController != null) legacyController.enabled = false;

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
        }

        // ==================== КАМЕРА ====================

        private void SpawnCamera()
        {
            if (cameraPrefab != null)
            {
                var camObj = Instantiate(cameraPrefab.gameObject, transform);
                _myCamera = camObj.GetComponent<ThirdPersonCamera>();
                if (_myCamera != null)
                {
                    _myCamera.SetTarget(transform);
                }
            }
            else
            {
                _myCamera = FindAnyObjectByType<ThirdPersonCamera>();
                if (_myCamera != null)
                {
                    _myCamera.SetTarget(transform);
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
            var invField = typeof(InventoryUI).GetField("inventory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (invField != null)
                invField.SetValue(_inventoryUI, _inventory);
        }

        // ==================== ВВОД ====================

        private void Update()
        {
            if (!IsOwner) return;

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

                if (_currentShip != null)
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
            }
        }

        // ==================== ДВИЖЕНИЕ ====================

        private void FixedUpdate()
        {
            // Плавная коррекция позиции только для локального игрока (Owner)
            if (!IsOwner || _inShip) return;

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
            var ships = FindObjectsByType<ShipController>(FindObjectsInactive.Include);
            ShipController nearest = null;
            float minDist = float.MaxValue;

            foreach (var ship in ships)
            {
                if (!ship.gameObject.activeSelf) continue;
                float dist = Vector3.Distance(transform.position, ship.transform.position);
                if (dist < boardDistance && dist < minDist)
                {
                    minDist = dist;
                    nearest = ship;
                }
            }

            return nearest;
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
            float nearestDist = float.MaxValue;
            bool foundChest = false;

            var chests = FindObjectsByType<ChestContainer>(FindObjectsInactive.Include);
            foreach (var chest in chests)
            {
                if (!chest.gameObject.activeSelf) continue;
                float dist = Vector3.Distance(transform.position, chest.transform.position);
                if (dist < chest.GetOpenRadius() && dist < nearestDist)
                {
                    nearestDist = dist;
                    _nearestChest = chest;
                    foundChest = true;
                }
            }

            if (!foundChest)
            {
                var pickups = FindObjectsByType<PickupItem>(FindObjectsInactive.Include);
                nearestDist = float.MaxValue;
                foreach (var pickup in pickups)
                {
                    if (!pickup.gameObject.activeSelf) continue;
                    float dist = Vector3.Distance(transform.position, pickup.transform.position);
                    if (dist < pickupRange && dist < nearestDist)
                    {
                        nearestDist = dist;
                        _nearestPickup = pickup;
                    }
                }
            }
        }

        private void TryPickup()
        {
            if (_inShip) return;

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
        /// Вызывается сервером для коррекции позиции клиента при рассинхронизации
        /// </summary>
        [Rpc(SendTo.Owner)]
        public void ApplyServerPositionRpc(Vector3 serverPosition, RpcParams rpcParams = default)
        {
            _serverPosition = serverPosition;
            _hasServerPosition = true;
        }

        // ==================== TRADE RPC (Сессия 5) ====================

        /// <summary>
        /// Купить товар — клиент запрашивает, сервер валидирует
        /// </summary>
        [Rpc(SendTo.Server)]
        public void TradeBuyServerRpc(string itemId, int quantity, string locationId, RpcParams rpcParams = default)
        {
            // Серверная логика в TradeMarketServer
            if (TradeMarketServer.Instance != null)
            {
                TradeMarketServer.Instance.BuyItemServerRpc(itemId, quantity, locationId);
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
        public void TradeSellServerRpc(string itemId, int quantity, string locationId, RpcParams rpcParams = default)
        {
            if (TradeMarketServer.Instance != null)
            {
                TradeMarketServer.Instance.SellItemServerRpc(itemId, quantity, locationId);
            }
            else
            {
                Debug.LogWarning("[NetworkPlayer] TradeMarketServer не найден");
            }
        }

        /// <summary>
        /// Результат торговли — сервер отправляет клиенту
        /// Сессия 8C: добавлены itemId, itemQuantity, isPurchase для синхронизации склада
        /// </summary>
        [Rpc(SendTo.Owner)]
        public void TradeResultClientRpc(bool success, string message, float newCredits, string itemId = "", int itemQuantity = 0, bool isPurchase = true, RpcParams rpcParams = default)
        {
            if (TradeUI.Instance != null)
            {
                TradeUI.Instance.OnTradeResult(success, message, newCredits, itemId, itemQuantity, isPurchase);
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
    }
}
