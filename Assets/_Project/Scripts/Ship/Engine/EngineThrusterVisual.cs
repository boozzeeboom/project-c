using UnityEngine;
using ProjectC.Player;

namespace ProjectC.Ship.Engine
{
    /// <summary>
    /// T-ENG02: EngineThrusterVisual — клиентский визуальный компонент двигателя.
    /// Размещается на ModuleSlot GameObject (тип Engine).
    /// Использует ShipRootReference для доступа к ShipController и ShipInputReader.
    /// Никаких RPC, никакой модификации Rigidbody. Client-side only.
    ///
    /// Иерархия:
    ///   Slot_Engine (этот компонент)
    ///   └── Visuals (_visualRoot — поворачивается кодом вокруг _pivotOffset)
    ///       ├── Body (корпус)
    ///       └── Blade (_propeller — вращается)
    ///
    /// Настройка pivot:
    ///   _pivotOffset задаётся числом в инспекторе — двигать иерархию не нужно.
    /// </summary>
    public class EngineThrusterVisual : MonoBehaviour
    {
        [Header("Propeller")]
        [Tooltip("3D объект лопастей. Вращается вокруг локальной оси.")]
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

        [Tooltip("Точка вращения в локальных координатах слота. (0,0,0) = центр слота. "
               + "Меняй число — двигать иерархию не нужно.")]
        [SerializeField] private Vector3 _pivotOffset = Vector3.zero;

        [Tooltip("Transform-рут всех визуалов (корпус + блейд). Вращается вокруг _pivotOffset. "
               + "Если null — вращается сам слот.")]
        [SerializeField] private Transform _visualRoot;

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

        // Сохранённая базовая позиция/вращение visualRoot (до отклонения)
        private Vector3 _visualRootBaseLocalPos;
        private Quaternion _visualRootBaseLocalRot;

        private void Start()
        {
            ResolveDependencies();
            SaveVisualRootBase();
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

        private void SaveVisualRootBase()
        {
            if (_visualRoot != null)
            {
                _visualRootBaseLocalPos = _visualRoot.localPosition;
                _visualRootBaseLocalRot = _visualRoot.localRotation;
            }
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

                Quaternion rot = Quaternion.Euler(0f, _currentAngle, 0f);

                if (_visualRoot != null)
                {
                    // Вращаем visualRoot вокруг _pivotOffset
                    Vector3 offset = _visualRootBaseLocalPos - _pivotOffset;
                    _visualRoot.localPosition = _pivotOffset + rot * offset;
                    _visualRoot.localRotation = _visualRootBaseLocalRot * rot;
                }
                else
                {
                    // Fallback: вращаем сам слот
                    transform.localRotation = rot;
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
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
