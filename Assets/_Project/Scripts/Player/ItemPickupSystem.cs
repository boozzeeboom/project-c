using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using ProjectC.Core;
using ProjectC.Items;

namespace ProjectC.Player
{
    /// <summary>
    /// Система подбора предметов.
    /// Вешается на игрока. Клавиша E — подобрать ближайший предмет.
    /// Работает только в пешем режиме (проверяет PlayerStateMachine).
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

        // Для индикации: ближайший доступный предмет
        private PickupItem _nearestPickup;

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
                return;
            }

            FindNearestPickup();
        }

        /// <summary>
        /// Найти ближайший подбираемый предмет в радиусе
        /// </summary>
        private void FindNearestPickup()
        {
            _nearestPickup = null;
            float nearestDist = float.MaxValue;

            // Находим все активные PickupItem на сцене (Unity 6 API — последняя версия)
            var pickups = FindObjectsByType<PickupItem>();

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

        /// <summary>
        /// Попытка подобрать предмет по нажатию E
        /// </summary>
        private void TryPickup()
        {
            if (!_stateMachine.IsWalking) return;

            if (_nearestPickup != null)
            {
                _nearestPickup.Collect();
                _nearestPickup = null;
            }
        }

        /// <summary>
        /// Есть ли предмет рядом для подбора (для UI подсказки)
        /// </summary>
        public bool HasNearbyPickup()
        {
            return _nearestPickup != null && _stateMachine.IsWalking;
        }

        /// <summary>
        /// Получить имя ближайшего предмета (для UI)
        /// </summary>
        public string GetNearbyPickupName()
        {
            if (_nearestPickup != null && _nearestPickup.itemData != null)
                return _nearestPickup.itemData.itemName;
            return "";
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
