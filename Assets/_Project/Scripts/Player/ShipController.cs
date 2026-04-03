using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectC.Player
{
    /// <summary>
    /// Контроллер корабля — полёт на Rigidbody
    /// Вешается на объект корабля (Ship_01, Ship_02 и т.д.)
    /// Управляет ТОЛЬКО своим объектом
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ShipController : MonoBehaviour
    {
        [Header("Тяга")]
        [SerializeField] private float thrustForce = 500f;
        [SerializeField] private float maxSpeed = 30f;

        [Header("Вращение")]
        [Tooltip("Рыскание (A/D) — медленное, как у баржи")]
        [SerializeField] private float yawForce = 30f;

        [SerializeField] private float pitchForce = 40f;

        [Header("Вертикальное движение")]
        [Tooltip("Сила подъёма/снижения (Q = вниз, E = вверх)")]
        [SerializeField] private float verticalForce = 300f;

        [Header("Антигравитация")]
        [Tooltip("0 = падает, 1 = зависает")]
        [SerializeField] [Range(0f, 1.5f)] private float antiGravity = 1f;

        [Header("Аэродинамика")]
        [SerializeField] private float linearDrag = 1f;
        [SerializeField] private float angularDrag = 2f;

        [Header("Стабилизация")]
        [SerializeField] private float stabilizationForce = 50f;
        [SerializeField] private bool autoStabilize = true;

        // Rigidbody
        private Rigidbody _rb;

        // Ввод
        private InputAction _thrustPositive;
        private InputAction _thrustNegative;
        private InputAction _yawLeft;
        private InputAction _yawRight;
        private InputAction _pitchAction;
        private InputAction _verticalDown;
        private InputAction _verticalUp;
        private InputAction _boostAction;

        private float _thrustInput;
        private float _yawInput;
        private float _pitchInput;
        private float _verticalInput;
        private bool _boostActive;

        private void Awake()
        {
            // По умолчанию корабль не управляется — управление даёт PlayerStateMachine при посадке
            enabled = false;

            _rb = GetComponent<Rigidbody>();

            if (_rb != null)
            {
                _rb.linearDamping = linearDrag;
                _rb.angularDamping = angularDrag;
                _rb.useGravity = true;
                _rb.constraints = RigidbodyConstraints.None;
            }

            // Input Actions
            _thrustPositive = new InputAction("ThrustPos", binding: "<Keyboard>/w", expectedControlType: "Button");
            _thrustNegative = new InputAction("ThrustNeg", binding: "<Keyboard>/s", expectedControlType: "Button");
            _yawLeft = new InputAction("YawLeft", binding: "<Keyboard>/a", expectedControlType: "Button");
            _yawRight = new InputAction("YawRight", binding: "<Keyboard>/d", expectedControlType: "Button");
            _pitchAction = new InputAction("Pitch", binding: "<Mouse>/delta/y", expectedControlType: "Axis");
            _verticalDown = new InputAction("VerticalDown", binding: "<Keyboard>/q", expectedControlType: "Button");
            _verticalUp = new InputAction("VerticalUp", binding: "<Keyboard>/e", expectedControlType: "Button");
            _boostAction = new InputAction("Boost", binding: "<Keyboard>/leftShift", expectedControlType: "Button");
        }

        private void OnEnable()
        {
            _thrustPositive.Enable();
            _thrustNegative.Enable();
            _yawLeft.Enable();
            _yawRight.Enable();
            _pitchAction.Enable();
            _verticalDown.Enable();
            _verticalUp.Enable();
            _boostAction.Enable();
        }

        private void OnDisable()
        {
            if (_thrustPositive != null) _thrustPositive.Disable();
            if (_thrustNegative != null) _thrustNegative.Disable();
            if (_yawLeft != null) _yawLeft.Disable();
            if (_yawRight != null) _yawRight.Disable();
            if (_pitchAction != null) _pitchAction.Disable();
            if (_verticalDown != null) _verticalDown.Disable();
            if (_verticalUp != null) _verticalUp.Disable();
            if (_boostAction != null) _boostAction.Disable();
        }

        private void FixedUpdate()
        {
            if (_rb == null) return;

            HandleInput();
            ApplyThrust();
            ApplyAntiGravity();
            ApplyVertical();
            ApplyRotation();

            if (autoStabilize && HasNoInput())
                ApplyStabilization();

            ClampVelocity();
        }

        private void HandleInput()
        {
            _thrustInput = (_thrustPositive.ReadValue<float>() > 0.5f ? 1f : 0f)
                         - (_thrustNegative.ReadValue<float>() > 0.5f ? 1f : 0f);

            _yawInput = (_yawRight.ReadValue<float>() > 0.5f ? 1f : 0f)
                      - (_yawLeft.ReadValue<float>() > 0.5f ? 1f : 0f);

            _pitchInput = _pitchAction.ReadValue<float>();
            _pitchInput = Mathf.Clamp(_pitchInput, -1f, 1f);

            // Q = вниз, E = вверх
            _verticalInput = (_verticalUp.ReadValue<float>() > 0.5f ? 1f : 0f)
                           - (_verticalDown.ReadValue<float>() > 0.5f ? 1f : 0f);

            _boostActive = _boostAction.ReadValue<float>() > 0.5f;
        }

        private void ApplyThrust()
        {
            if (Mathf.Abs(_thrustInput) < 0.01f) return;

            float currentThrust = _boostActive ? thrustForce * 2f : thrustForce;
            _rb.AddForce(transform.forward * _thrustInput * currentThrust, ForceMode.Force);
        }

        private void ApplyAntiGravity()
        {
            if (antiGravity <= 0f) return;
            float gravityCompensation = _rb.mass * Mathf.Abs(Physics.gravity.y) * antiGravity;
            _rb.AddForce(Vector3.up * gravityCompensation, ForceMode.Force);
        }

        /// <summary>
        /// Вертикальное движение (Q/E — лифт)
        /// </summary>
        private void ApplyVertical()
        {
            if (Mathf.Abs(_verticalInput) < 0.01f) return;
            _rb.AddForce(Vector3.up * _verticalInput * verticalForce, ForceMode.Force);
        }

        private void ApplyRotation()
        {
            if (Mathf.Abs(_yawInput) > 0.01f)
                _rb.AddTorque(Vector3.up * _yawInput * yawForce, ForceMode.Force);

            if (Mathf.Abs(_pitchInput) > 0.01f)
                _rb.AddTorque(transform.right * -_pitchInput * pitchForce, ForceMode.Force);
        }

        private void ApplyStabilization()
        {
            Vector3 stabilizationTorque = Vector3.Cross(transform.up, Vector3.up) * stabilizationForce;
            _rb.AddTorque(stabilizationTorque, ForceMode.Force);
        }

        private bool HasNoInput()
        {
            return Mathf.Abs(_thrustInput) < 0.01f
                && Mathf.Abs(_yawInput) < 0.01f
                && Mathf.Abs(_pitchInput) < 0.01f
                && Mathf.Abs(_verticalInput) < 0.01f;
        }

        private void ClampVelocity()
        {
            if (_rb.linearVelocity.magnitude > maxSpeed)
                _rb.linearVelocity = _rb.linearVelocity.normalized * maxSpeed;
        }

        public float CurrentSpeed => _rb != null ? _rb.linearVelocity.magnitude : 0f;
        public bool IsGrounded => Physics.Raycast(transform.position, Vector3.down, out _, 1.5f);

        /// <summary>
        /// Точка выхода из корабля (на палубе)
        /// </summary>
        public Vector3 GetExitPosition()
        {
            return transform.position + Vector3.up * 1.5f;
        }
    }
}
