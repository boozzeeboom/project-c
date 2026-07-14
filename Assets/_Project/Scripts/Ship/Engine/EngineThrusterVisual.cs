using UnityEngine;
using ProjectC.Player;

namespace ProjectC.Ship.Engine
{
    /// <summary>
    /// T-ENG02: EngineThrusterVisual — клиентский визуальный компонент двигателя.
    /// Размещается на ModuleSlot GameObject (тип Engine) или его дочернем объекте.
    /// Использует ShipRootReference для доступа к ShipController и ShipInputReader.
    /// Никаких RPC, никакой модификации Rigidbody. Client-side only.
    ///
    /// Алгоритм:
    ///   1. Читает CurrentThrust / CurrentYaw из ShipInputReader (работает когда локальный игрок — пилот).
    ///   2. Вращает _propeller вокруг _rotationAxis пропорционально thrust.
    ///   3. Отклоняет transform.localRotation по Y пропорционально yaw.
    ///   4. При _maxDeflectionAngle = 0 — отклонение отключено.
    ///   5. При _maxRpm = 0 — вращение отключено.
    /// </summary>
    public class EngineThrusterVisual : MonoBehaviour
    {
        [Header("Propeller")]
        [Tooltip("3D объект лопастей (дочерний от этого Transform). Вращается вокруг локальной оси.")]
        [SerializeField] private Transform _propeller;

        [Tooltip("Скорость вращения на полной тяге (об/сек). Отрицательное = обратное вращение.")]
        [SerializeField] private float _maxRpm = 10f;

        [Tooltip("Ось вращения лопастей в локальном пространстве propeller-объекта.")]
        [SerializeField] private Vector3 _rotationAxis = Vector3.forward;

        [Header("Deflection (поворот двигателя)")]
        [Tooltip("Максимальный угол отклонения при полном yaw (градусы). 0 = не отклоняется.")]
        [SerializeField] private float _maxDeflectionAngle = 40f;

        [Tooltip("Плавность следования отклонения (сек).")]
        [SerializeField] private float _deflectionSmoothTime = 0.3f;

        [Header("Dependencies")]
        [Tooltip("ShipRootReference на этой или родительской части корабля. Авто-поиск если null.")]
        [SerializeField] private ShipRootReference _rootRef;

        // Кешированные ссылки
        private ShipController _shipController;
        private ShipInputReader _inputReader;

        // Smooth state
        private float _currentAngle;
        private float _angleVelocity;
        private float _currentRpm;
        private float _rpmVelocity;

        private void Start()
        {
            ResolveDependencies();
        }

        private void ResolveDependencies()
        {
            if (_rootRef == null)
                _rootRef = GetComponentInParent<ShipRootReference>();

            if (_rootRef != null)
            {
                _shipController = _rootRef.ShipController;
                if (_shipController != null)
                    _inputReader = _shipController.GetComponent<ShipInputReader>();
            }

            if (_shipController == null)
                Debug.LogWarning($"[EngineThrusterVisual] '{name}': ShipController не найден через ShipRootReference. " +
                    "Убедись, что ShipRootReference присутствует на корне корабля.", this);
        }

        private void Update()
        {
            if (_shipController == null || _inputReader == null)
                return;

            // Двигатель должен быть запущен
            if (!_shipController.IsEngineRunning)
                return;

            // Читаем локальный ввод (работает когда локальный игрок — пилот)
            float thrustNorm = Mathf.Abs(_inputReader.CurrentThrust); // 0..1
            float yawNorm = _inputReader.CurrentYaw;                  // -1..1

            // --- Propeller rotation ---
            if (_maxRpm != 0f && _propeller != null)
            {
                float targetRpm = thrustNorm * _maxRpm;
                _currentRpm = Mathf.SmoothDamp(_currentRpm, targetRpm, ref _rpmVelocity, 0.3f);

                if (Mathf.Abs(_currentRpm) > 0.001f)
                    _propeller.Rotate(_rotationAxis, _currentRpm * 360f * Time.deltaTime, Space.Self);
            }

            // --- Deflection ---
            if (_maxDeflectionAngle != 0f)
            {
                float targetAngle = yawNorm * _maxDeflectionAngle;
                _currentAngle = Mathf.SmoothDamp(_currentAngle, targetAngle, ref _angleVelocity, _deflectionSmoothTime);
                transform.localRotation = Quaternion.Euler(0f, _currentAngle, 0f);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Авто-поиск _rootRef в редакторе для удобства
            if (_rootRef == null)
                _rootRef = GetComponentInParent<ShipRootReference>();
        }

        private void Reset()
        {
            _rootRef = GetComponentInParent<ShipRootReference>();
        }
#endif
    }
}
