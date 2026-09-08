using UnityEngine;
using ProjectC.Player;

namespace ProjectC.Ship.Engine
{
    /// <summary>
    /// T-ENG02: EngineThrusterVisual — клиентский визуальный компонент двигателя.
    ///
    /// Pivot and visual transforms may be anywhere in the Slot_Engine hierarchy:
    ///
    ///   Slot_Engine (this component)
    ///   ├── RotationAnchor (_pivotPoint — marker for the rotation point)
    ///   └── any visual/container/FBX hierarchy (_visuals)
    ///
    /// The visual pose is converted through Slot_Engine space before rotation,
    /// so different parent transforms and imported FBX root offsets are supported.
    ///
    /// Настройка:
    ///   1. Двигай RotationAnchor куда нужно — это точка вращения.
    ///   2. Назначь _visuals на корень любого визуала или его контейнер.
    ///   3. Никаких чисел и ручного пересчёта FBX-трансформаций не требуется.
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

        // Базовые позы в пространстве Slot_Engine (до отклонения)
        private Vector3 _visualsBaseLocalPos;
        private Quaternion _visualsBaseLocalRot;
        private Vector3 _propellerBaseLocalPos;
        private Quaternion _propellerBaseLocalRot;
        private float _propellerSpinAngle;

        private void Start()
        {
            ResolveDependencies();
            CacheVisualPoseInSlotSpace();
            CachePropellerPoseInSlotSpace();
        }

        private void CacheVisualPoseInSlotSpace()
        {
            if (_visuals == null)
                return;

            // Store the visual pose in this component's space, not in _visuals.parent space.
            // This keeps the pivot calculation valid when the visual is nested under
            // an FBX/container hierarchy with its own position, rotation, or scale.
            _visualsBaseLocalPos = transform.InverseTransformPoint(_visuals.position);
            _visualsBaseLocalRot = Quaternion.Inverse(transform.rotation) * _visuals.rotation;
        }


        private void CachePropellerPoseInSlotSpace()
        {
            if (_propeller == null)
                return;

            _propellerBaseLocalPos = transform.InverseTransformPoint(_propeller.position);
            _propellerBaseLocalRot = Quaternion.Inverse(transform.rotation) * _propeller.rotation;
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

            // --- Propeller spin ---
            if (_propeller != null)
            {
                float targetRpm = _maxRpm != 0f ? thrustNorm * _maxRpm : 0f;
                _currentRpm = Mathf.SmoothDamp(_currentRpm, targetRpm, ref _rpmVelocity, 0.3f);
                _propellerSpinAngle += _currentRpm * 360f * Time.deltaTime;
            }

            // --- Deflection: rotate the visual and propeller around the pivot in Slot_Engine space ---
            Quaternion deflectionRot = Quaternion.identity;
            Vector3 pivotLocal = _propellerBaseLocalPos;

            if (_maxDeflectionAngle != 0f && _pivotPoint != null)
            {
                float targetAngle = yawNorm * _maxDeflectionAngle;
                _currentAngle = Mathf.SmoothDamp(_currentAngle, targetAngle, ref _angleVelocity, _deflectionSmoothTime);

                // _pivotPoint and the FBX roots may have different parents. Convert
                // every pose through Slot_Engine so imported offsets remain correct.
                deflectionRot = Quaternion.AngleAxis(_currentAngle, Vector3.up);
                pivotLocal = transform.InverseTransformPoint(_pivotPoint.position);

                if (_visuals != null)
                {
                    Vector3 offset = _visualsBaseLocalPos - pivotLocal;
                    Vector3 targetLocalPos = pivotLocal + deflectionRot * offset;
                    Quaternion targetLocalRot = deflectionRot * _visualsBaseLocalRot;

                    _visuals.SetPositionAndRotation(
                        transform.TransformPoint(targetLocalPos),
                        transform.rotation * targetLocalRot);
                }
            }

            ApplyPropellerPose(deflectionRot, pivotLocal);
        }

        private void ApplyPropellerPose(Quaternion deflectionRot, Vector3 pivotLocal)
        {
            if (_propeller == null)
                return;

            Quaternion spinRot = _rotationAxis.sqrMagnitude > 0.0001f
                ? Quaternion.AngleAxis(_propellerSpinAngle, _rotationAxis.normalized)
                : Quaternion.identity;

            Vector3 offset = _propellerBaseLocalPos - pivotLocal;
            Vector3 targetLocalPos = pivotLocal + deflectionRot * offset;
            Quaternion targetLocalRot = deflectionRot * _propellerBaseLocalRot * spinRot;

            _propeller.SetPositionAndRotation(
                transform.TransformPoint(targetLocalPos),
                transform.rotation * targetLocalRot);
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
