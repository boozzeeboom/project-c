using UnityEngine;
using UnityEngine.InputSystem;
using ProjectC.Core;
using ProjectC.Player;

namespace ProjectC.Core
{
    /// <summary>
    /// Управление режимами: пеший ↔ любой корабль на сцене
    /// F — сесть в ближайший корабль / выйти из текущего
    /// Корабли ищутся по тегу "Ship"
    /// </summary>
    public class PlayerStateMachine : MonoBehaviour
    {
        public enum PlayerState
        {
            Walking,
            Flying
        }

        [Header("Компоненты игрока")]
        [Tooltip("Контроллер пешего режима")]
        [SerializeField] private PlayerController walkController;

        [Tooltip("CharacterController игрока (отключается при посадке)")]
        [SerializeField] private CharacterController characterController;

        [Tooltip("Визуал игрока (MeshRenderer, скрывается при посадке)")]
        [SerializeField] private Renderer playerRenderer;

        [Header("Поиск кораблей")]
        [Tooltip("Тег кораблей на сцене")]
        [SerializeField] private string shipTag = "Ship";

        [Tooltip("Максимальная дистанция для посадки (м)")]
        [SerializeField] private float boardDistance = 5f;

        [Header("Камера")]
        [Tooltip("ThirdPersonCamera")]
        [SerializeField] private ThirdPersonCamera cameraController;

        [Header("Состояние")]
        [SerializeField] private PlayerState currentState = PlayerState.Walking;

        // Текущий корабль (null если пешком)
        private ShipController _currentShip;

        // Ввод
        private InputAction _switchAction;

        public PlayerState State => currentState;
        public bool IsWalking => currentState == PlayerState.Walking;
        public bool IsFlying => currentState == PlayerState.Flying;
        public ShipController CurrentShip => _currentShip;

        private void Awake()
        {
            _switchAction = new InputAction("SwitchMode", binding: "<Keyboard>/f", expectedControlType: "Button");
        }

        private void OnEnable()
        {
            _switchAction.Enable();
            _switchAction.performed += ctx => TrySwitchMode();
        }

        private void OnDisable()
        {
            _switchAction.Disable();
            _switchAction.performed -= ctx => TrySwitchMode();
        }

        private void Start()
        {
            ApplyWalking();
        }

        /// <summary>
        /// Попытка переключить режим
        /// </summary>
        public void TrySwitchMode()
        {
            if (currentState == PlayerState.Walking)
                TryBoardNearestShip();
            else
                Disembark();
        }

        /// <summary>
        /// Сесть в ближайший корабль
        /// </summary>
        private void TryBoardNearestShip()
        {
            ShipController nearestShip = FindNearestShip();

            if (nearestShip == null)
                return;

            _currentShip = nearestShip;
            ApplyFlying();
        }

        /// <summary>
        /// Выйти из корабля
        /// </summary>
        private void Disembark()
        {
            if (_currentShip == null) return;

            ShipController shipToExit = _currentShip;

            // Проверка: можно ли выйти (на земле или медленно)
            if (!shipToExit.IsGrounded && shipToExit.CurrentSpeed > 2f)
                return;

            // Перемещаем игрока на палубу корабля
            transform.position = shipToExit.GetExitPosition();

            // Выключаем корабль
            shipToExit.enabled = false;
            _currentShip = null;

            // Включаем пешехода
            ApplyWalking();
        }

        /// <summary>
        /// Применить режим корабля
        /// </summary>
        private void ApplyFlying()
        {
            if (_currentShip == null) return;

            currentState = PlayerState.Flying;

            // Отключаем CharacterController — игрок перестаёт быть физическим объектом
            if (characterController != null) characterController.enabled = false;
            // Скрываем визуал
            if (playerRenderer != null) playerRenderer.enabled = false;
            if (walkController != null) walkController.enabled = false;

            // Включаем корабль
            _currentShip.enabled = true;

            // Камера — следит за кораблём
            if (cameraController != null)
            {
                cameraController.SetTarget(_currentShip.transform);
                cameraController.SetShipMode(true);
            }
        }

        /// <summary>
        /// Применить режим пешехода
        /// </summary>
        private void ApplyWalking()
        {
            currentState = PlayerState.Walking;
            _currentShip = null;

            // Включаем CharacterController и визуал
            if (characterController != null) characterController.enabled = true;
            if (playerRenderer != null) playerRenderer.enabled = true;
            if (walkController != null) walkController.enabled = true;

            if (cameraController != null)
            {
                cameraController.SetTarget(transform);
                cameraController.SetShipMode(false);
            }
        }

        /// <summary>
        /// Найти ближайший корабль по тегу
        /// </summary>
        private ShipController FindNearestShip()
        {
            GameObject[] ships = GameObject.FindGameObjectsWithTag(shipTag);

            ShipController nearest = null;
            float minDist = float.MaxValue;

            foreach (GameObject ship in ships)
            {
                ShipController sc = ship.GetComponent<ShipController>();
                if (sc == null) continue;

                float dist = Vector3.Distance(transform.position, ship.transform.position);
                if (dist < minDist && dist <= boardDistance)
                {
                    minDist = dist;
                    nearest = sc;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Принудительно установить состояние
        /// </summary>
        public void ForceState(PlayerState state)
        {
            if (state == PlayerState.Walking)
                Disembark();
            else
                TryBoardNearestShip();
        }
    }
}
