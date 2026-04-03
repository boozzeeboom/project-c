using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectC.Player
{
    /// <summary>
    /// Контроллер персонажа — классический вид от третьего лица
    /// Мышь X — поворот персонажа
    /// W — идти вперёд (куда смотрит персонаж)
    /// A/D — поворот влево/вправо
    /// Space — прыжок
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Движение")]
        [Tooltip("Скорость ходьбы")]
        [SerializeField] private float walkSpeed = 5f;

        [Tooltip("Скорость бега")]
        [SerializeField] private float runSpeed = 10f;

        [Header("Вращение")]
        [Tooltip("Чувствительность мыши для поворота")]
        [SerializeField] private float lookSensitivity = 3f;

        [Tooltip("Скорость поворота при A/D (градусы/сек)")]
        [SerializeField] private float rotationSpeed = 180f;

        [Header("Прыжок")]
        [Tooltip("Сила прыжка")]
        [SerializeField] private float jumpForce = 8f;

        [Tooltip("Гравитация")]
        [SerializeField] private float gravity = -20f;

        [Header("Камера")]
        [Tooltip("Ссылка на ThirdPersonCamera")]
        [SerializeField] private ThirdPersonCamera cameraController;

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

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

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
            if (cameraController == null)
            {
                cameraController = FindAnyObjectByType<ThirdPersonCamera>();
            }
        }

        private void Update()
        {
            HandleInput();
            HandleLook();
            HandleMovement();
        }

        private void HandleInput()
        {
            _moveInput = _moveAction.ReadValue<Vector2>();
            _lookInput = _lookAction.ReadValue<Vector2>();
        }

        /// <summary>
        /// Мышь вращает персонажа по Y
        /// </summary>
        private void HandleLook()
        {
            float yaw = _lookInput.x * lookSensitivity;
            transform.Rotate(Vector3.up, yaw);
        }

        /// <summary>
        /// Движение
        /// </summary>
        private void HandleMovement()
        {
            _isGrounded = _controller.isGrounded;

            if (_isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f;
            }

            // W/S = вперёд/назад относительно персонажа
            // A/D = поворот влево/вправо
            bool running = _runAction.ReadValue<float>() > 0.5f;
            float currentSpeed = running ? runSpeed : walkSpeed;

            // Вперёд/назад
            float forwardInput = _moveInput.y;
            if (Mathf.Abs(forwardInput) > 0.01f)
            {
                Vector3 moveDir = transform.forward * forwardInput;
                _controller.Move(moveDir * currentSpeed * Time.deltaTime);
            }

            // A/D = поворот
            float turnInput = _moveInput.x;
            if (Mathf.Abs(turnInput) > 0.01f)
            {
                transform.Rotate(Vector3.up, turnInput * rotationSpeed * Time.deltaTime);
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

        public bool IsGrounded => _isGrounded;
    }
}
