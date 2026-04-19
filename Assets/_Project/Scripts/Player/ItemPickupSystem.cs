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

        private PlayerStateMachine _stateMachine;
        private InputAction _pickupAction;

        // Для индикации: ближайший доступный предмет или сундук
        private PickupItem _nearestPickup;
        private NetworkChestContainer _nearestChest;

        private void Awake()
        {
            _stateMachine = GetComponent<PlayerStateMachine>();

            _pickupAction = new InputAction("Pickup", binding: "<Keyboard>/e", expectedControlType: "Button");
        }

        private void OnEnable()
        {
            _pickupAction.Enable();
            _pickupAction.performed += ctx => TryPickup();
        }

        private void OnDisable()
        {
            _pickupAction.Disable();
            _pickupAction.performed -= ctx => TryPickup();
        }

        private void Update()
        {
            // Работает только в пешем режиме
            if (!_stateMachine.IsWalking)
            {
                _nearestPickup = null;
                _nearestChest = null;
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

            // Ищем сундуки (NetworkChestContainer)
            var chests = FindObjectsByType<NetworkChestContainer>(FindObjectsInactive.Include);
            foreach (var chest in chests)
            {
                if (chest == null || !chest.gameObject.activeSelf) continue;

                float dist = Vector3.Distance(transform.position, chest.transform.position);
                if (dist < chest.GetOpenRadius() && dist < nearestDist)
                {
                    nearestDist = dist;
                    _nearestChest = chest;
                    foundChest = true;
                    _nearestPickup = null; // Приоритет сундуку
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
        /// Попытка взаимодействия по нажатию E
        /// </summary>
        private void TryPickup()
        {
            if (!_stateMachine.IsWalking) return;

            // Приоритет: сундук
            if (_nearestChest != null)
            {
                _nearestChest.TryOpen();
                _nearestChest = null;
                return;
            }

            // Обычный предмет
            if (_nearestPickup != null)
            {
                _nearestPickup.Collect();
                _nearestPickup = null;
            }
        }

        /// <summary>
        /// Есть ли предмет или сундук рядом для взаимодействия (для UI подсказки)
        /// </summary>
        public bool HasNearbyPickup()
        {
            return (_nearestPickup != null || _nearestChest != null) && _stateMachine.IsWalking;
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
            return _nearestChest != null && _stateMachine.IsWalking;
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