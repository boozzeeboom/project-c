using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using ProjectC.Core;
using ProjectC.Items;
using ProjectC.World.Chest;

namespace ProjectC.Player
{
    /// <summary>
    /// Система подбора предметов.
    /// Вешается на игрока. Клавиша E — подобрать ближайший предмет.
    /// Работает только в пешем режиме (проверяет PlayerStateMachine).
    /// 
    /// Iteration 4: Оптимизировано для работы с NetworkChestContainer.
    /// </summary>
    [RequireComponent(typeof(PlayerStateMachine))]
    public class ItemPickupSystem : MonoBehaviour
    {
        [Header("Настройки подбора")]
        [Tooltip("Максимальная дистанция подбора (м)")]
        [SerializeField] private float pickupRange = 3f;

        [Tooltip("Визуализация радиуса в редакторе")]
        [SerializeField] private bool showPickupRadius = true;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        private PlayerStateMachine _stateMachine;
        private InputAction _pickupAction;

        // Для индикации: ближайший доступный предмет или сундук
        private PickupItem _nearestPickup;
        private NetworkChestContainer _nearestChest;

        // Debug info
        private float _lastDebugTime = 0f;
        private const float DEBUG_INTERVAL = 0.5f;

        private void Awake()
        {
            _stateMachine = GetComponent<PlayerStateMachine>();
            if (_stateMachine == null)
            {
                Debug.LogError("[ItemPickupSystem] PlayerStateMachine not found!");
                return;
            }

            _pickupAction = new InputAction("Pickup", binding: "<Keyboard>/e", expectedControlType: "Button");
            
            if (debugMode)
                Debug.Log($"[ItemPickupSystem] Awake, IsWalking = {_stateMachine.IsWalking}");
        }

        private void OnEnable()
        {
            _pickupAction.Enable();
            _pickupAction.performed += ctx => OnPickupPressed();
            
            if (debugMode)
                Debug.Log("[ItemPickupSystem] OnEnable - Input subscribed");
        }

        private void OnDisable()
        {
            _pickupAction.Disable();
            _pickupAction.performed -= ctx => OnPickupPressed();
        }

        private void Update()
        {
            // Debug: показать состояние каждый кадр если debugMode
            if (debugMode && Time.time - _lastDebugTime > DEBUG_INTERVAL)
            {
                _lastDebugTime = Time.time;
                
                bool isWalking = _stateMachine != null && _stateMachine.IsWalking;
                Debug.Log($"[ItemPickupSystem] Update: Walking={isWalking}, Chest={_nearestChest != null}, Pickup={_nearestPickup != null}");
                
                // Проверяем все NetworkChestContainer на сцене
                var allChests = FindObjectsByType<NetworkChestContainer>(FindObjectsInactive.Include);
                Debug.Log($"[ItemPickupSystem] Total NetworkChestContainers on scene: {allChests.Length}");
                
                foreach (var chest in allChests)
                {
                    float dist = Vector3.Distance(transform.position, chest.transform.position);
                    Debug.Log($"[ItemPickupSystem]   - {chest.name} at {chest.transform.position}, dist={dist:F1}, spawned={chest.IsSpawned}");
                }
            }

            // Работает только в пешем режиме
            if (_stateMachine == null || !_stateMachine.IsWalking)
            {
                if (_nearestPickup != null || _nearestChest != null)
                {
                    if (debugMode)
                        Debug.Log($"[ItemPickupSystem] Not walking, clearing nearest. IsWalking={_stateMachine?.IsWalking}");
                    _nearestPickup = null;
                    _nearestChest = null;
                }
                return;
            }

            FindNearestInteractable();
        }

        /// <summary>
        /// Найти ближайший подбираемый предмет или сундук в радиусе.
        /// </summary>
        private void FindNearestInteractable()
        {
            _nearestPickup = null;
            _nearestChest = null;
            float nearestDist = float.MaxValue;
            bool foundChest = false;

            // Сначала ищем все NetworkChestContainer на сцене
            var chests = FindObjectsByType<NetworkChestContainer>(FindObjectsInactive.Include);
            
            if (debugMode)
                Debug.Log($"[ItemPickupSystem] FindObjectsByType found {chests.Length} chests");

            foreach (var chest in chests)
            {
                if (chest == null || !chest.gameObject.activeSelf) continue;

                float dist = Vector3.Distance(transform.position, chest.transform.position);
                float openRadius = chest.GetOpenRadius();

                if (debugMode)
                    Debug.Log($"[ItemPickupSystem] Chest {chest.name}: dist={dist:F1}, radius={openRadius:F1}, IsSpawned={chest.IsSpawned}");

                if (dist < openRadius && dist < nearestDist)
                {
                    nearestDist = dist;
                    _nearestChest = chest;
                    foundChest = true;
                    _nearestPickup = null;
                    
                    if (debugMode)
                        Debug.Log($"[ItemPickupSystem] >>> SELECTED as nearest chest!");
                }
            }

            // Если сундук не найден — ищем обычные предметы
            if (!foundChest)
            {
                var pickups = FindObjectsByType<PickupItem>(FindObjectsInactive.Include);

                foreach (var pickup in pickups)
                {
                    if (pickup == null || !pickup.gameObject.activeSelf) continue;

                    float dist = Vector3.Distance(transform.position, pickup.transform.position);
                    if (dist < pickupRange && dist < nearestDist)
                    {
                        nearestDist = dist;
                        _nearestPickup = pickup;
                    }
                }
            }
        }

        /// <summary>
        /// Called when E is pressed
        /// </summary>
        private void OnPickupPressed()
        {
            if (debugMode)
                Debug.Log($"[ItemPickupSystem] E pressed! Walking={_stateMachine?.IsWalking}, Chest={_nearestChest != null}, Pickup={_nearestPickup != null}");

            if (_stateMachine == null || !_stateMachine.IsWalking)
            {
                if (debugMode)
                    Debug.Log("[ItemPickupSystem] Cannot interact - not walking");
                return;
            }

            // Приоритет: сундук
            if (_nearestChest != null)
            {
                if (debugMode)
                    Debug.Log($"[ItemPickupSystem] Opening chest: {_nearestChest.name}");
                _nearestChest.TryOpen();
                _nearestChest = null;
                return;
            }

            // Обычный предмет
            if (_nearestPickup != null)
            {
                if (debugMode)
                    Debug.Log($"[ItemPickupSystem] Collecting pickup: {_nearestPickup.name}");
                _nearestPickup.Collect();
                _nearestPickup = null;
            }
        }

        /// <summary>
        /// Есть ли предмет или сундук рядом для взаимодействия (для UI подсказки)
        /// </summary>
        public bool HasNearbyPickup()
        {
            return (_nearestPickup != null || _nearestChest != null) && (_stateMachine != null && _stateMachine.IsWalking);
        }

        /// <summary>
        /// Получить имя ближайшего предмета или сундука (для UI)
        /// </summary>
        public string GetNearbyPickupName()
        {
            // Сундук — приоритет
            if (_nearestChest != null)
                return "Сундук";

            if (_nearestPickup != null && _nearestPickup.itemData != null)
                return _nearestPickup.itemData.itemName;
            return "";
        }

        /// <summary>
        /// Является ли ближайшее взаимодействие сундуком (для UI иконки)
        /// </summary>
        public bool IsNearbyChest()
        {
            return _nearestChest != null && _stateMachine != null && _stateMachine.IsWalking;
        }

        private void OnDrawGizmosSelected()
        {
            if (showPickupRadius)
            {
                Gizmos.color = new Color(0, 1, 0, 0.3f);
                Gizmos.DrawWireSphere(transform.position, pickupRange);
            }
        }
    }
}