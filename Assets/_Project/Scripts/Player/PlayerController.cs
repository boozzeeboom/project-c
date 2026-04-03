using UnityEngine;
using UnityEngine.InputSystem;
using ProjectC.Core;

namespace ProjectC.Player
{
    /// <summary>
    /// Контроллер персонажа — вид от третьего лица
    /// Мышь = вращение камеры (в ThirdPersonCamera)
    /// W/S = вперёд/назад относительно камеры
    /// A/D = стрейф влево/вправо
    /// Персонаж поворачивается лицом к движению
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Движение")]
        [Tooltip("Скорость ходьбы")]
        [SerializeField] private float walkSpeed = 5f;

        [Tooltip("Скорость бега")]
        [SerializeField] private float runSpeed = 10f;

        [Tooltip("Скорость поворота к движению")]
        [SerializeField] private float rotationSpeed = 12f;

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
        private InputAction _jumpAction;
        private InputAction _runAction;

        private Vector2 _moveInput;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

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
            if (cameraController == null)
            {
                cameraController = FindAnyObjectByType<ThirdPersonCamera>();
            }
        }

        private void Update()
        {
            _moveInput = _moveAction.ReadValue<Vector2>();
            HandleMovement();
        }

        private void HandleMovement()
        {
            _isGrounded = _controller.isGrounded;

            if (_isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f;
            }

            // Направление от камеры
            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;

            if (cameraController != null)
            {
                forward = cameraController.CameraForward;
                right = cameraController.CameraRight;
            }

            // Направление движения
            Vector3 moveDirection = forward * _moveInput.y + right * _moveInput.x;

            bool hasInput = moveDirection.magnitude > 0.01f;

            if (hasInput)
            {
                moveDirection.Normalize();

                // Поворот персонажа к движению
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

                // Скорость
                bool running = _runAction.ReadValue<float>() > 0.5f;
                float currentSpeed = running ? runSpeed : walkSpeed;

                _controller.Move(moveDirection * currentSpeed * Time.deltaTime);
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
