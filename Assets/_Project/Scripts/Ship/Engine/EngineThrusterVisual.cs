using System.Collections.Generic;
using UnityEngine;
using ProjectC.Player;

namespace ProjectC.Ship.Engine
{
    /// <summary>
    /// T-ENG02: EngineThrusterVisual — клиентский визуальный компонент двигателя.
    ///
    /// Один компонент обслуживает N двигателей: у каждого юнита СВОЯ пара
    /// pivot + visuals, и каждый визуал поворачивается вокруг СВОЕГО pivot:
    ///
    ///   Slot_Engine (этот компонент)
    ///   ├── RotationAnchor_L (юнит 0: pivot — маркер точки вращения)
    ///   ├── RotationAnchor_R (юнит 1: pivot)
    ///   ├── EngineVisuals_L  (юнит 0: visuals)
    ///   └── EngineVisuals_R  (юнит 1: visuals)
    ///
    /// Вход (thrust/yaw) общий — угол отклонения один на всех, но применяется
    /// вокруг индивидуальной точки каждого юнита. Поэтому моторы поворачиваются
    /// каждый на месте, а не «вокруг общего центра».
    ///
    /// Базовые позы кешируются в пространстве Slot_Engine (до отклонения),
    /// потому поддерживаются произвольные вложенности и FBX-оффсеты.
    ///
    /// Настройка:
    ///   1. Добавь юнит на каждый мотор: Pivot — маркер точки вращения,
    ///      Visuals — корень визуала мотора, Propeller — лопасти (если есть).
    ///   2. Двигай Pivot мышкой к центру мотора: плечо ~0 = поворот на месте.
    ///      Длинное плечо подсвечивается красным гизмо + warning в Start.
    ///   3. Старые поля секции Legacy используются только если список пуст
    ///      (обратная совместимость существующих префабов).
    /// </summary>
    public class EngineThrusterVisual : MonoBehaviour
    {
        [System.Serializable]
        public class ThrusterUnit
        {
            [Tooltip("Точка вращения ЭТОГО визуала. Двигай мышкой к центру мотора.")]
            public Transform pivot;

            [Tooltip("Корень визуала ЭТОГО двигателя. Вращается вокруг pivot.")]
            public Transform visuals;

            [Tooltip("Лопасти ЭТОГО двигателя (под visuals). Необязательно.")]
            public Transform propeller;

            // Кеш базовых поз в пространстве Slot_Engine (рантайм, не сериализуется).
            internal Vector3 visualsBasePos;
            internal Quaternion visualsBaseRot;
            internal bool hasVisuals;
            internal Vector3 propellerBasePos;
            internal Quaternion propellerBaseRot;
            internal bool hasPropeller;
        }

        [Header("Thrusters (у каждого своя точка поворота)")]
        [Tooltip("По юниту на мотор. Пусто = используется Legacy-секция ниже.")]
        [SerializeField] private List<ThrusterUnit> _thrusters = new List<ThrusterUnit>();

        [Header("Propeller")]
        [Tooltip("Скорость вращения на полной тяге (об/сек). Отрицательное = обратное вращение.")]
        [SerializeField] private float _maxRpm = 10f;

        [Tooltip("Плавность следования оборотов (сек).")]
        [SerializeField] private float _rpmSmoothTime = 0.3f;

        [Tooltip("Ось вращения лопастей в локальном пространстве propeller-объекта.")]
        [SerializeField] private Vector3 _rotationAxis = Vector3.forward;

        [Header("Deflection (поворот двигателя)")]
        [Tooltip("Максимальный угол отклонения при полном yaw (градусы). 0 = не отклоняется.")]
        [SerializeField] private float _maxDeflectionAngle = 40f;

        [Tooltip("Плавность следования отклонения (сек).")]
        [SerializeField] private float _deflectionSmoothTime = 0.3f;

        [Header("Legacy (один мотор)")]
        [Tooltip("УСТАРЕЛО: точка вращения. Используется только если список Thrusters пуст.")]
        [SerializeField] private Transform _pivotPoint;

        [Tooltip("УСТАРЕЛО: контейнер визуалов. Используется только если список Thrusters пуст.")]
        [SerializeField] private Transform _visuals;

        [Tooltip("УСТАРЕЛО: лопасти. Используется только если список Thrusters пуст.")]
        [SerializeField] private Transform _propeller;

        [Header("Diagnostics")]
        [Tooltip("Плечо pivot→visuals больше этого (м) = misplacement: красное гизмо + warning в Start.")]
        [SerializeField] private float _leverWarnDistance = 5f;

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

        // Эффективные юниты на этот ран (список или один legacy). Строится в Start.
        private readonly List<ThrusterUnit> _effectiveUnits = new List<ThrusterUnit>();

        // Общее smooth-состояние (вход один на всех юнитов — сглаживаем один раз).
        private float _currentAngle;
        private float _angleVelocity;
        private float _currentRpm;
        private float _rpmVelocity;
        private float _propellerSpinAngle;

        private void Start()
        {
            ResolveDependencies();
            BuildEffectiveUnits(_effectiveUnits);
            foreach (var unit in _effectiveUnits)
                CacheUnitPoseInSlotSpace(unit);
            ValidateLevers();
        }

        /// <summary>
        /// T-ENG02 multi-pivot: список wins; пустой список + старые поля = один legacy-юнит.
        /// </summary>
        private void BuildEffectiveUnits(List<ThrusterUnit> outUnits)
        {
            outUnits.Clear();
            if (_thrusters != null)
            {
                foreach (var unit in _thrusters)
                {
                    if (unit != null && (unit.visuals != null || unit.propeller != null))
                        outUnits.Add(unit);
                }
            }
            if (outUnits.Count == 0
                && (_pivotPoint != null || _visuals != null || _propeller != null))
            {
                outUnits.Add(new ThrusterUnit
                {
                    pivot = _pivotPoint,
                    visuals = _visuals,
                    propeller = _propeller
                });
            }
        }

        private void CacheUnitPoseInSlotSpace(ThrusterUnit unit)
        {
            // Store poses in this component's space, not in parent space.
            // This keeps the pivot calculation valid when the visual is nested under
            // an FBX/container hierarchy with its own position, rotation, or scale.
            if (unit.visuals != null)
            {
                unit.visualsBasePos = transform.InverseTransformPoint(unit.visuals.position);
                unit.visualsBaseRot = Quaternion.Inverse(transform.rotation) * unit.visuals.rotation;
                unit.hasVisuals = true;
            }
            if (unit.propeller != null)
            {
                unit.propellerBasePos = transform.InverseTransformPoint(unit.propeller.position);
                unit.propellerBaseRot = Quaternion.Inverse(transform.rotation) * unit.propeller.rotation;
                unit.hasPropeller = true;
            }
        }

        /// <summary>
        /// T-ENG02 multi-pivot: плечо pivot→visuals в метрах. Большое плечо =
        /// мотор описывает дугу вместо поворота на месте (см. Ship_medium_base до фикса).
        /// </summary>
        private void ValidateLevers()
        {
            foreach (var unit in _effectiveUnits)
            {
                if (unit.pivot == null || unit.visuals == null)
                    continue;
                float lever = Vector3.Distance(unit.pivot.position, unit.visuals.position);
                if (lever > _leverWarnDistance)
                {
                    Debug.LogWarning(
                        $"[EngineThrusterVisual] '{name}': юнит visuals='{unit.visuals.name}' — " +
                        $"плечо pivot→visuals {lever:F1} м (> {_leverWarnDistance:F0} м). " +
                        $"Двигатель будет описывать дугу вместо поворота на месте. " +
                        $"Подвинь '{unit.pivot.name}' к центру визуала.", this);
                }
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

            // Источник thrust/yaw: пилот за штурвалом → клавиатурный ввод,
            // нет пилота (NPC-автопилот) → вывод из Rigidbody.
            // T-SHIP-FIX12: двигатель заглушен — нулевые входы (угол/обороты плавно гаснут,
            // поза не застывает в отклонённой).
            float thrustNorm, yawNorm;
            if (!_shipController.IsEngineRunning)
            {
                thrustNorm = 0f;
                yawNorm = 0f;
            }
            else if (_inputReader != null && _inputReader.isActiveAndEnabled)
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

                // T-SHIP-FIX12: yaw — по локальной оси корабля, не мировой Y
                // (при крене/тангаже мировая Y врёт).
                Vector3 localAngVel = _shipController.transform.InverseTransformDirection(_rbody.angularVelocity);
                float yawRateRad = localAngVel.y;
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

            // --- Propeller spin (общий на всех юнитов: вход один) ---
            float targetRpm = _maxRpm != 0f ? thrustNorm * _maxRpm : 0f;
            _currentRpm = Mathf.SmoothDamp(_currentRpm, targetRpm, ref _rpmVelocity, _rpmSmoothTime);
            _propellerSpinAngle += _currentRpm * 360f * Time.deltaTime;

            // --- Deflection (общий угол; применяется вокруг СВОЕГО pivot каждого юнита) ---
            Quaternion deflectionRot = Quaternion.identity;
            bool canDeflect = _maxDeflectionAngle != 0f && AnyPivotAssigned();
            if (canDeflect)
            {
                float targetAngle = yawNorm * _maxDeflectionAngle;
                _currentAngle = Mathf.SmoothDamp(_currentAngle, targetAngle, ref _angleVelocity, _deflectionSmoothTime);
                deflectionRot = Quaternion.AngleAxis(_currentAngle, Vector3.up);
            }

            foreach (var unit in _effectiveUnits)
                ApplyUnitPose(unit, deflectionRot, canDeflect);
        }

        private bool AnyPivotAssigned()
        {
            foreach (var unit in _effectiveUnits)
            {
                if (unit.pivot != null)
                    return true;
            }
            return false;
        }

        private void ApplyUnitPose(ThrusterUnit unit, Quaternion deflectionRot, bool canDeflect)
        {
            // Pivot и FBX-корни могут иметь разных родителей. Конвертируем
            // каждую позу через Slot_Engine, чтобы импортированные оффсеты остались корректны.
            Vector3 pivotLocal = unit.hasPropeller ? unit.propellerBasePos : Vector3.zero;

            if (canDeflect && unit.pivot != null)
            {
                pivotLocal = transform.InverseTransformPoint(unit.pivot.position);

                if (unit.hasVisuals && unit.visuals != null)
                {
                    Vector3 offset = unit.visualsBasePos - pivotLocal;
                    Vector3 targetLocalPos = pivotLocal + deflectionRot * offset;
                    Quaternion targetLocalRot = deflectionRot * unit.visualsBaseRot;

                    unit.visuals.SetPositionAndRotation(
                        transform.TransformPoint(targetLocalPos),
                        transform.rotation * targetLocalRot);
                }
            }

            ApplyUnitPropellerPose(unit, deflectionRot, pivotLocal);
        }

        private void ApplyUnitPropellerPose(ThrusterUnit unit, Quaternion deflectionRot, Vector3 pivotLocal)
        {
            if (!unit.hasPropeller || unit.propeller == null)
                return;

            Quaternion spinRot = _rotationAxis.sqrMagnitude > 0.0001f
                ? Quaternion.AngleAxis(_propellerSpinAngle, _rotationAxis.normalized)
                : Quaternion.identity;

            Vector3 offset = unit.propellerBasePos - pivotLocal;
            Vector3 targetLocalPos = pivotLocal + deflectionRot * offset;
            Quaternion targetLocalRot = deflectionRot * unit.propellerBaseRot * spinRot;

            unit.propeller.SetPositionAndRotation(
                transform.TransformPoint(targetLocalPos),
                transform.rotation * targetLocalRot);
        }

        private void OnDrawGizmosSelected()
        {
            // Плечо каждого юнита: жёлтое = норма, красное = misplacement (см. ValidateLevers).
            var display = new List<ThrusterUnit>();
            BuildEffectiveUnits(display);
            foreach (var unit in display)
            {
                if (unit.pivot == null || unit.visuals == null)
                    continue;
                bool bad = Vector3.Distance(unit.pivot.position, unit.visuals.position) > _leverWarnDistance;
                Gizmos.color = bad ? Color.red : Color.yellow;
                Gizmos.DrawWireSphere(unit.pivot.position, 0.3f);
                Gizmos.DrawLine(unit.pivot.position, unit.visuals.position);
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

