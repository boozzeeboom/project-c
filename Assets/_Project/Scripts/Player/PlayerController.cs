using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectC.Player
{
    /// <summary>
    /// Контроллер персонажа — пеший режим
    /// WASD движение + гравитация + прыжок
    /// Вращение обрабатывает камера (WorldCamera)
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Движение")]
        [Tooltip("Скорость ходьбы")]
        [SerializeField] private float walkSpeed = 5f;

        [Tooltip("Скорость бега")]
        [SerializeField] private float runSpeed = 10f;

        [Header("Прыжок")]
        [Tooltip("Сила прыжка")]
        [SerializeField] private float jumpForce = 8f;

        [Tooltip("Гравитация")]
        [SerializeField] private float gravity = -20f;

        [Header("Камера")]
        [Tooltip("Ссылка на камеру для определения направления")]
        [SerializeField] private Transform cameraTransform;

        // CharacterController
        private CharacterController _controller;
        private Vector3 _velocity;
        private bool _isGrounded;

        // Ввод
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _runAction;

        private Vector2 _moveInput;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            // Создаём Input Actions программно
            _moveAction = new InputAction("Move", expectedControlType: "Vector2");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            _jumpAction = new InputAction("Jump", binding: "<Keyboard>/space", expectedControlType: "Button");
            _runAction = new InputAction("Run", binding: "<Keyboard>/leftShift", expectedControlType: "Button");
        }

        private void OnEnable()
        {
            _moveAction.Enable();
            _jumpAction.Enable();
            _runAction.Enable();
        }

        private void OnDisable()
        {
            _moveAction.Disable();
            _jumpAction.Disable();
            _runAction.Disable();
        }

        private void Start()
        {
            // Если камера не назначена, ищем главную камеру
            if (cameraTransform == null)
            {
                var mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    cameraTransform = mainCamera.transform;
                }
            }
        }

        private void Update()
        {
            HandleInput();
            
            // Отладка
            if (Time.frameCount % 30 == 0)
            {
                Debug.Log($"[PlayerController] Move input: {_moveInput}, Grounded: {_isGrounded}, Speed: {(_runAction.ReadValue<float>() > 0.5f ? runSpeed : walkSpeed)}");
            }
            
            HandleMovement();
        }

        /// <summary>
        /// Считать ввод
        /// </summary>
        private void HandleInput()
        {
            _moveInput = _moveAction.ReadValue<Vector2>();
        }

        /// <summary>
        /// Обработка движения
        /// </summary>
        private void HandleMovement()
        {
            // Проверка земли
            _isGrounded = _controller.isGrounded;

            // Сброс вертикальной скорости на земле
            if (_isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f;
            }

            // Направление движения относительно камеры
            Vector3 forward = cameraTransform != null ? cameraTransform.forward : transform.forward;
            Vector3 right = cameraTransform != null ? cameraTransform.right : transform.right;

            // Убираем наклон по Y для горизонтального движения
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            // Вычисляем направление движения
            Vector3 moveDirection = forward * _moveInput.y + right * _moveInput.x;
            moveDirection.Normalize();

            // Определяем скорость (бег/ходьба)
            bool running = _runAction.ReadValue<float>() > 0.5f;
            float currentSpeed = running ? runSpeed : walkSpeed;

            // Поворачиваем персонажа по направлению движения
            if (moveDirection.magnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
            }

            // Применяем горизонтальное движение
            _controller.Move(moveDirection * currentSpeed * Time.deltaTime);

            // Отладка движения
            if (Time.frameCount % 30 == 0 && moveDirection.magnitude > 0.01f)
            {
                Debug.Log($"[PlayerController] Moving! Dir: {moveDirection}, Speed: {currentSpeed}, Pos: {transform.position}");
            }

            // Прыжок
            if (_isGrounded && _jumpAction.WasPerformedThisFrame())
            {
                _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            }

            // Гравитация
            _velocity.y += gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }

        /// <summary>
        /// Проверка: на земле ли персонаж?
        /// </summary>
        public bool IsGrounded => _isGrounded;
    }
}
