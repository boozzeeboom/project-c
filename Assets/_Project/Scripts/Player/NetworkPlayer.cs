using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectC.Core;
using ProjectC.Items;
using System.Collections.Generic;

namespace ProjectC.Player
{
    /// <summary>
    /// Сетевой компонент игрока.
    /// • Движение: WASD + Space + Shift (локально, NetworkTransform синхронизирует)
    /// • Камера: персональная для каждого игрока
    /// • Инвентарь: E подобрать, Tab открыть колесо
    /// • Сундуки: E открыть
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class NetworkPlayer : NetworkBehaviour
    {
        [Header("Движение")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float runSpeed = 10f;
        [SerializeField] private float rotationSpeed = 12f;

        [Header("Прыжок")]
        [SerializeField] private float jumpForce = 8f;
        [SerializeField] private float gravity = -20f;

        [Header("Камера")]
        [SerializeField] private ThirdPersonCamera cameraPrefab;

        [Header("Инвентарь")]
        [Tooltip("Радиус подбора предмета/сундука")]
        [SerializeField] private float pickupRange = 3f;

        private CharacterController _controller;
        private Vector3 _velocity;
        private bool _isGrounded;
        private ThirdPersonCamera _myCamera;
        private Inventory _inventory;
        private InventoryUI _inventoryUI;

        // Ввод
        private Vector2 _moveInput;
        private bool _jumpPressed;
        private bool _runPressed;

        // Поиск ближайшего объекта
        private PickupItem _nearestPickup;
        private ChestContainer _nearestChest;

        // NetworkObject
        private NetworkObject networkObject;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            networkObject = GetComponent<NetworkObject>();
            _controller = GetComponent<CharacterController>();

            if (IsOwner)
            {
                Debug.Log($"[Player] Локальный игрок spawned. OwnerClientId: {OwnerClientId}");

                // Камера
                SpawnCamera();

                // Инвентарь
                SpawnInventory();

                // Интерактивность только для локального
                Update();
            }
            else
            {
                Debug.Log($"[Player] Удалённый игрок spawned. OwnerClientId: {OwnerClientId}");
                _controller.enabled = false;
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (_myCamera != null) Destroy(_myCamera.gameObject);
            if (_inventoryUI != null) Destroy(_inventoryUI.gameObject);

            Debug.Log($"[Player] Игрок despawned. OwnerClientId: {OwnerClientId}");
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
                    Debug.Log("[Player] Персональная камера создана");
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
            // Создаём Inventory для этого игрока
            var invObj = new GameObject("Inventory");
            invObj.transform.SetParent(transform);
            _inventory = invObj.AddComponent<Inventory>();

            // Создаём InventoryUI и связываем с Inventory
            var uiObj = new GameObject("InventoryUI");
            _inventoryUI = uiObj.AddComponent<InventoryUI>();
            // Связываем через поле (используем reflection т.к. поле private)
            var invField = typeof(InventoryUI).GetField("inventory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (invField != null)
            {
                invField.SetValue(_inventoryUI, _inventory);
            }

            Debug.Log("[Player] Инвентарь создан");
        }

        // ==================== ВВОД ====================

        private void Update()
        {
            if (!IsOwner) return;

            // Движение
            _moveInput = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) _moveInput.y += 1;
            if (Keyboard.current.sKey.isPressed) _moveInput.y -= 1;
            if (Keyboard.current.aKey.isPressed) _moveInput.x -= 1;
            if (Keyboard.current.dKey.isPressed) _moveInput.x += 1;
            _jumpPressed = Keyboard.current.spaceKey.wasPressedThisFrame;
            _runPressed = Keyboard.current.leftShiftKey.isPressed;

            ProcessMovement(_moveInput, _jumpPressed, _runPressed);

            // Подбор (E)
            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                FindNearestInteractable();
                TryPickup();
            }

            // Поиск для UI подсказок (каждый кадр)
            FindNearestInteractable();
        }

        // ==================== ДВИЖЕНИЕ ====================

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
            {
                _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            }

            _velocity.y += gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }

        // ==================== ПОДБОР ПРЕДМЕТОВ ====================

        private void FindNearestInteractable()
        {
            _nearestPickup = null;
            _nearestChest = null;
            float nearestDist = float.MaxValue;
            bool foundChest = false;

            // Ищем сундуки (приоритет)
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
            // Приоритет: сундук
            if (_nearestChest != null)
            {
                // Локально добавляем в свой инвентарь
                var loot = _nearestChest.GetLootItems();
                if (_inventory != null && loot.Count > 0)
                {
                    _inventory.AddMultipleItems(loot);
                    if (_inventoryUI != null) _inventoryUI.TriggerSectorFlash();
                }

                // Шлём всем чтобы скрыли сундук
                OpenChestRpc(_nearestChest.transform.position);
                _nearestChest = null;
                return;
            }

            // Обычный предмет
            if (_nearestPickup != null)
            {
                // Локально добавляем в свой инвентарь
                if (_inventory != null)
                {
                    _inventory.AddItem(_nearestPickup.itemData);
                }

                // Шлём всем чтобы скрыли предмет
                HidePickupRpc(_nearestPickup.transform.position);
                _nearestPickup = null;
            }
        }

        /// <summary>
        /// Синхронизация скрытия предмета всем клиентам
        /// </summary>
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

        /// <summary>
        /// Синхронизация открытия сундука всем клиентам
        /// </summary>
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
                    {
                        chest.gameObject.SetActive(false);
                    }
                    return;
                }
            }
        }

        public bool HasNearbyInteractable() => _nearestPickup != null || _nearestChest != null;
        public string GetNearbyInteractableName()
        {
            if (_nearestChest != null) return "Сундук";
            if (_nearestPickup != null && _nearestPickup.itemData != null) return _nearestPickup.itemData.itemName;
            return "";
        }
        public bool IsNearbyChest() => _nearestChest != null;

        public new bool IsLocalPlayer => IsOwner;
        public ulong GetOwnerId() => OwnerClientId;
    }
}
