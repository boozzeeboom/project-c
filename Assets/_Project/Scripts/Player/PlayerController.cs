using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectC.Player
{
    /// <summary>
    /// Контроллер персонажа — пеший режим
    /// WASD движение + гравитация + прыжок
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Движение")]
        [Tooltip("Скорость ходьбы")]
        [SerializeField] private float walkSpeed = 5f;

        [Tooltip("Скорость бега")]
        [SerializeField] private float runSpeed = 10f;

        [Tooltip("Скорость поворота (градусы/сек)")]
        [SerializeField] private float rotationSpeed = 360f;

        [Header("Прыжок")]
        [Tooltip("Сила прыжка")]
        [SerializeField] private float jumpForce = 8f;

        [Tooltip("Гравитация")]
        [SerializeField] private float gravity = -20f;

        [Header("Камера")]
        [Tooltip("Ссылка на камеру для управления взглядом")]
        [SerializeField] private Transform cameraTransform;

        [Tooltip("Чувствительность мыши X")]
        [SerializeField] private float mouseSensitivityX = 2f;

        [Tooltip("Чувствительность мыши Y")]
        [SerializeField] private float mouseSensitivityY = 2f;

        [Tooltip("Минимальный угол обзора")]
        [SerializeField] private float minLookAngle = -80f;

        [Tooltip("Максимальный угол обзора")]
        [SerializeField] private float maxLookAngle = 80f;

        // CharacterController
        private CharacterController _controller;
        private Vector3 _velocity;
        private bool _isGrounded;

        // Ввод
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _runAction;

        private Vector2 _moveInput;
        private Vector2 _lookInput;

        // Вращение камеры
        private float _cameraPitch = 0f;

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

            _lookAction = new InputAction("Look", binding: "<Mouse>/delta", expectedControlType: "Vector2");
            _jumpAction = new InputAction("Jump", binding: "<Keyboard>/space", expectedControlType: "Button");
            _runAction = new InputAction("Run", binding: "<Keyboard>/leftShift", expectedControlType: "Button");
        }

        private void OnEnable()
        {
            _moveAction.Enable();
            _lookAction.Enable();
            _jumpAction.Enable();
            _runAction.Enable();
        }

        private void OnDisable()
        {
            _moveAction.Disable();
            _lookAction.Disable();
            _jumpAction.Disable();
            _runAction.Disable();
        }

        private void Start()
        {
            // Блокируем курсор
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Если камера не назначена, ищем главную камеру
            if (cameraTransform == null)
            {
                var mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    cameraTransform = mainCamera.transform;
                    _cameraPitch = cameraTransform.eulerAngles.x;
                }
            }
        }

        private void Update()
        {
            HandleInput();
            HandleLook();
            HandleMovement();
        }

        /// <summary>
        /// Считать ввод
        /// </summary>
        private void HandleInput()
        {
            _moveInput = _moveAction.ReadValue<Vector2>();
            _lookInput = _lookAction.ReadValue<Vector2>();
        }

        /// <summary>
        /// Обработка вращения камеры
        /// </summary>
        private void HandleLook()
        {
            if (cameraTransform == null) return;

            // Вращение персонажа по горизонтали
            float yaw = _lookInput.x * mouseSensitivityX;
            transform.Rotate(Vector3.up * yaw);

            // Вращение камеры по вертикали
            float pitch = -_lookInput.y * mouseSensitivityY;
            _cameraPitch += pitch;
            _cameraPitch = Mathf.Clamp(_cameraPitch, minLookAngle, maxLookAngle);

            cameraTransform.localEulerAngles = Vector3.right * _cameraPitch;
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
            float currentSpeed = _runAction.IsPressed() ? runSpeed : walkSpeed;

            // Применяем горизонтальное движение
            _controller.Move(moveDirection * currentSpeed * Time.deltaTime);

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

        /// <summary>
        /// Текущая скорость движения
        /// </summary>
        public float CurrentSpeed => _runAction.IsPressed() ? runSpeed : walkSpeed;
    }
}
