using UnityEngine;
using ProjectC.Player;

namespace ProjectC.Ship.Engine
{
    /// <summary>
    /// T-ENG02: EngineThrusterVisual — клиентский визуальный компонент двигателя.
    ///
    /// Два независимых Transform'а (двигаются мышкой, никаких чисел):
    ///
    ///   Slot_Engine (этот компонент, НЕ вращается)
    ///   ├── PivotPoint   (_pivotPoint — маркер точки вращения, пустой)
    ///   └── Visuals      (_visuals — контейнер Body + Blade, вращается вокруг PivotPoint)
    ///       ├── Body
    ///       └── Blade (_propeller)
    ///
    /// Настройка:
    ///   1. Двигай PivotPoint куда нужно — это точка вращения.
    ///   2. Двигай Visuals куда нужно — это позиция визуала.
    ///   3. Всё. Никаких чисел, никакого перетаскивания дочерних.
    /// </summary>
    public class EngineThrusterVisual : MonoBehaviour
    {
        [Header("Propeller")]
        [Tooltip("3D объект лопастей (под _visuals). Вращается вокруг локальной оси.")]
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

        [Tooltip("Пустой маркер — точка вращения. Двигай мышкой, визуалы не смещаются.")]
        [SerializeField] private Transform _pivotPoint;

        [Tooltip("Контейнер визуалов (Body + Blade). Вращается вокруг _pivotPoint. Двигай мышкой.")]
        [SerializeField] private Transform _visuals;

        [Header("Dependencies")]
        [Tooltip("ShipRootReference на этой или родительской части корабля. Авто-поиск если null.")]
        [SerializeField] private ShipRootReference _rootRef;

        [Header("NPC Fallback")]
        [Tooltip("Скорость корабля (м/с), соответствующая 100% тяге. Используется когда нет пилота за штурвалом (NPC-автопилот).")]
        [SerializeField] private float _maxReferenceSpeed = 10f;

        [Tooltip("Угловая скорость рыскания (град/с), соответствующая 100% yaw. Используется для NPC-автопилота.")]
        [SerializeField] private float _maxRefYawRate = 45f;

        // Кешированные ссылки
        private ShipController _shipController;
        private ShipInputReader _inputReader;
        private Rigidbody _rbody;

        // Smooth state
        private float _currentAngle;
        private float _angleVelocity;
        private float _currentRpm;
        private float _rpmVelocity;

        // Сохранённая базовая позиция/вращение _visuals (до отклонения)
        private Vector3 _visualsBaseLocalPos;
        private Quaternion _visualsBaseLocalRot;

        private void Start()
        {
            ResolveDependencies();
            if (_visuals != null)
            {
                _visualsBaseLocalPos = _visuals.localPosition;
                _visualsBaseLocalRot = _visuals.localRotation;
            }
        }

        private void ResolveDependencies()
        {
            if (_rootRef == null)
                _rootRef = GetComponentInParent<ShipRootReference>();

            if (_rootRef != null)
            {
                _shipController = _rootRef.ShipController;
                if (_shipController != null)
                {
                    _inputReader = _shipController.GetComponent<ShipInputReader>();
                    _rbody = _shipController.GetComponent<Rigidbody>();
                }
            }

            if (_shipController == null)
                Debug.LogWarning($"[EngineThrusterVisual] '{name}': ShipController не найден.", this);
        }

        private void Update()
        {
            if (_shipController == null || !_shipController.enabled)
                return;

            if (!_shipController.IsEngineRunning)
                return;

            // Источник thrust/yaw: пилот за штурвалом → клавиатурный ввод,
            // нет пилота (NPC-автопилот) → вывод из Rigidbody.
            float thrustNorm, yawNorm;
            if (_inputReader != null && _inputReader.isActiveAndEnabled)
            {
                thrustNorm = Mathf.Abs(_inputReader.CurrentThrust);
                yawNorm = _inputReader.CurrentYaw;
            }
            else if (_rbody != null)
            {
                float speed = _rbody.linearVelocity.magnitude;
                thrustNorm = _maxReferenceSpeed > 0.01f
                    ? Mathf.Clamp01(speed / _maxReferenceSpeed)
                    : 0f;

                float yawRateRad = _rbody.angularVelocity.y;
                float maxYawRad = _maxRefYawRate * Mathf.Deg2Rad;
                yawNorm = maxYawRad > 0.001f
                    ? Mathf.Clamp(yawRateRad / maxYawRad, -1f, 1f)
                    : 0f;
            }
            else
            {
                thrustNorm = 0f;
                yawNorm = 0f;
            }

            // --- Propeller rotation ---
            if (_maxRpm != 0f && _propeller != null)
            {
                float targetRpm = thrustNorm * _maxRpm;
                _currentRpm = Mathf.SmoothDamp(_currentRpm, targetRpm, ref _rpmVelocity, 0.3f);

                if (Mathf.Abs(_currentRpm) > 0.001f)
                    _propeller.Rotate(_rotationAxis, _currentRpm * 360f * Time.deltaTime, Space.Self);
            }

            // --- Deflection: вращаем _visuals вокруг _pivotPoint ---
            if (_maxDeflectionAngle != 0f && _visuals != null && _pivotPoint != null)
            {
                float targetAngle = yawNorm * _maxDeflectionAngle;
                _currentAngle = Mathf.SmoothDamp(_currentAngle, targetAngle, ref _angleVelocity, _deflectionSmoothTime);

                Quaternion rot = Quaternion.Euler(0f, _currentAngle, 0f);

                // Вращаем _visuals вокруг точки _pivotPoint (в локальном пространстве слота)
                Vector3 pivotLocal = _pivotPoint.localPosition;
                Vector3 offset = _visualsBaseLocalPos - pivotLocal;
                _visuals.localPosition = pivotLocal + rot * offset;
                _visuals.localRotation = _visualsBaseLocalRot * rot;
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
