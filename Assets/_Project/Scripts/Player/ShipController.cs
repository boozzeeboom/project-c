using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectC.Player
{
    /// <summary>
    /// Контроллер корабля — полёт на Rigidbody
    /// Шаг 1: Базовое управление (тяга + вращение)
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ShipController : MonoBehaviour
    {
        [Header("Тяга")]
        [Tooltip("Сила тяги вперёд/назад")]
        [SerializeField] private float thrustForce = 500f;

        [Tooltip("Максимальная скорость")]
        [SerializeField] private float maxSpeed = 30f;

        [Header("Вращение")]
        [Tooltip("Сила рыскания (yaw, лево/право)")]
        [SerializeField] private float yawForce = 200f;

        [Tooltip("Сила тангажа (pitch, вверх/вниз)")]
        [SerializeField] private float pitchForce = 200f;

        [Tooltip("Сила крена (roll)")]
        [SerializeField] private float rollForce = 150f;

        [Header("Антигравитация")]
        [Tooltip("Компенсация гравитации (0 = падает, 1 = зависает)")]
        [SerializeField] [Range(0f, 1.5f)] private float antiGravity = 1f;

        [Header("Аэродинамика")]
        [Tooltip("Сопротивление воздуха (линейное)")]
        [SerializeField] private float linearDrag = 1f;

        [Tooltip("Сопротивление воздуха (угловое)")]
        [SerializeField] private float angularDrag = 2f;

        [Header("Стабилизация")]
        [Tooltip("Сила возврата к горизонту")]
        [SerializeField] private float stabilizationForce = 50f;

        [Tooltip("Автоматическая стабилизация при отсутствии ввода")]
        [SerializeField] private bool autoStabilize = true;

        // Rigidbody
        private Rigidbody _rb;

        // Ввод
        private InputAction _thrustPositive;
        private InputAction _thrustNegative;
        private InputAction _yawLeft;
        private InputAction _yawRight;
        private InputAction _pitchAction;
        private InputAction _rollLeft;
        private InputAction _rollRight;
        private InputAction _boostAction;

        private float _thrustInput;
        private float _yawInput;
        private float _pitchInput;
        private float _rollInput;
        private bool _boostActive;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            // Настройки Rigidbody для полёта
            _rb.linearDamping = linearDrag;
            _rb.angularDamping = angularDrag;
            _rb.useGravity = true;
            _rb.constraints = RigidbodyConstraints.None;

            // Input Actions — отдельные клавиши
            _thrustPositive = new InputAction("ThrustPos", binding: "<Keyboard>/w", expectedControlType: "Button");
            _thrustNegative = new InputAction("ThrustNeg", binding: "<Keyboard>/s", expectedControlType: "Button");
            _yawLeft = new InputAction("YawLeft", binding: "<Keyboard>/a", expectedControlType: "Button");
            _yawRight = new InputAction("YawRight", binding: "<Keyboard>/d", expectedControlType: "Button");
            _pitchAction = new InputAction("Pitch", binding: "<Mouse>/delta/y", expectedControlType: "Axis");
            _rollLeft = new InputAction("RollLeft", binding: "<Keyboard>/q", expectedControlType: "Button");
            _rollRight = new InputAction("RollRight", binding: "<Keyboard>/e", expectedControlType: "Button");
            _boostAction = new InputAction("Boost", binding: "<Keyboard>/leftShift", expectedControlType: "Button");
        }

        private void OnEnable()
        {
            _thrustPositive.Enable();
            _thrustNegative.Enable();
            _yawLeft.Enable();
            _yawRight.Enable();
            _pitchAction.Enable();
            _rollLeft.Enable();
            _rollRight.Enable();
            _boostAction.Enable();
        }

        private void OnDisable()
        {
            _thrustPositive.Disable();
            _thrustNegative.Disable();
            _yawLeft.Disable();
            _yawRight.Disable();
            _pitchAction.Disable();
            _rollLeft.Disable();
            _rollRight.Disable();
            _boostAction.Disable();
        }

        private void FixedUpdate()
        {
            HandleInput();
            ApplyThrust();
            ApplyAntiGravity();
            ApplyRotation();

            if (autoStabilize && HasNoInput())
            {
                ApplyStabilization();
            }

            ClampVelocity();
        }

        private void HandleInput()
        {
            // Комбинируем положительную и отрицательную ось
            _thrustInput = (_thrustPositive.ReadValue<float>() > 0.5f ? 1f : 0f)
                         - (_thrustNegative.ReadValue<float>() > 0.5f ? 1f : 0f);

            _yawInput = (_yawRight.ReadValue<float>() > 0.5f ? 1f : 0f)
                      - (_yawLeft.ReadValue<float>() > 0.5f ? 1f : 0f);

            _pitchInput = _pitchAction.ReadValue<float>();
            _pitchInput = Mathf.Clamp(_pitchInput, -1f, 1f);

            _rollInput = (_rollRight.ReadValue<float>() > 0.5f ? 1f : 0f)
                       - (_rollLeft.ReadValue<float>() > 0.5f ? 1f : 0f);

            _boostActive = _boostAction.ReadValue<float>() > 0.5f;
        }

        /// <summary>
        /// Тяга вперёд/назад
        /// </summary>
        private void ApplyThrust()
        {
            if (Mathf.Abs(_thrustInput) < 0.01f) return;

            float currentThrust = _boostActive ? thrustForce * 2f : thrustForce;
            Vector3 force = transform.forward * _thrustInput * currentThrust;
            _rb.AddForce(force, ForceMode.Force);
        }

        /// <summary>
        /// Компенсация гравитации — корабль «зависает» в воздухе
        /// </summary>
        private void ApplyAntiGravity()
        {
            if (antiGravity <= 0f) return;

            // Компенсируем гравитацию: масса * g * antiGravity
            float gravityCompensation = _rb.mass * Mathf.Abs(Physics.gravity.y) * antiGravity;
            _rb.AddForce(Vector3.up * gravityCompensation, ForceMode.Force);
        }

        /// <summary>
        /// Вращение: рыскание, тангаж, крен
        /// </summary>
        private void ApplyRotation()
        {
            // Рыскание (A/D — поворот влево/вправо)
            if (Mathf.Abs(_yawInput) > 0.01f)
            {
                _rb.AddTorque(Vector3.up * _yawInput * yawForce, ForceMode.Force);
            }

            // Тангаж (мышь Y — нос вверх/вниз)
            if (Mathf.Abs(_pitchInput) > 0.01f)
            {
                _rb.AddTorque(transform.right * -_pitchInput * pitchForce, ForceMode.Force);
            }

            // Крен (Q/E — наклон влево/вправо)
            if (Mathf.Abs(_rollInput) > 0.01f)
            {
                _rb.AddTorque(-transform.forward * _rollInput * rollForce, ForceMode.Force);
            }
        }

        /// <summary>
        /// Стабилизация — возврат к горизонту
        /// </summary>
        private void ApplyStabilization()
        {
            // Вычисляем отклонение от горизонта
            Vector3 currentUp = transform.up;
            Vector3 desiredUp = Vector3.up;

            // Torque для возврата
            Vector3 stabilizationTorque = Vector3.Cross(currentUp, desiredUp) * stabilizationForce;
            _rb.AddTorque(stabilizationTorque, ForceMode.Force);
        }

        /// <summary>
        /// Проверка: есть ли ввод от игрока?
        /// </summary>
        private bool HasNoInput()
        {
            return Mathf.Abs(_thrustInput) < 0.01f
                && Mathf.Abs(_yawInput) < 0.01f
                && Mathf.Abs(_pitchInput) < 0.01f
                && Mathf.Abs(_rollInput) < 0.01f;
        }

        /// <summary>
        /// Ограничение максимальной скорости
        /// </summary>
        private void ClampVelocity()
        {
            if (_rb.linearVelocity.magnitude > maxSpeed)
            {
                _rb.linearVelocity = _rb.linearVelocity.normalized * maxSpeed;
            }
        }

        /// <summary>
        /// Текущая скорость корабля
        /// </summary>
        public float CurrentSpeed => _rb.linearVelocity.magnitude;

        /// <summary>
        /// Находится ли корабль на земле?
        /// </summary>
        public bool IsGrounded
        {
            get
            {
                return Physics.Raycast(transform.position, Vector3.down, out _, 1.5f);
            }
        }
    }
}
