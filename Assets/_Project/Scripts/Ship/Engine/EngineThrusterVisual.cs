using UnityEngine;
using ProjectC.Player;

namespace ProjectC.Ship.Engine
{
    /// <summary>
    /// T-ENG02: EngineThrusterVisual — клиентский визуальный компонент двигателя.
    /// Размещается на ModuleSlot GameObject (тип Engine).
    ///
    /// Как настроить:
    ///   1. Создай дочерний GameObject "Visuals" под слотом.
    ///   2. Под "Visuals" положи Body и Blade (_propeller).
    ///   3. Назначь _pivotTransform = "Visuals".
    ///   4. _pivotOffset (0,0,0) — подстрой если нужно сместить точку вращения.
    ///
    /// Иерархия:
    ///   Slot_Engine (этот компонент, НЕ вращается)
    ///   └── Visuals (_pivotTransform — вращается вокруг своей позиции + _pivotOffset)
    ///       ├── Body
    ///       └── Blade (_propeller)
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

        [Tooltip("Transform-контейнер визуалов (Body + Blade под ним). Вращается целиком. "
               + "Перемести его = изменишь позицию визуалов И точку вращения.")]
        [SerializeField] private Transform _pivotTransform;

        [Tooltip("Дополнительное смещение точки вращения относительно позиции _pivotTransform. "
               + "(0,0,0) = вращение вокруг центра _pivotTransform. Меняй число — визуалы не сдвинутся.")]
        [SerializeField] private Vector3 _pivotOffset = Vector3.zero;

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

        // Сохранённая базовая позиция/вращение pivotTransform (до отклонения)
        private Vector3 _pivotBaseLocalPos;
        private Quaternion _pivotBaseLocalRot;

        private void Start()
        {
            ResolveDependencies();
            SavePivotBase();
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
                Debug.LogWarning($"[EngineThrusterVisual] '{name}': ShipController не найден через ShipRootReference.", this);
        }

        private void SavePivotBase()
        {
            if (_pivotTransform != null)
            {
                _pivotBaseLocalPos = _pivotTransform.localPosition;
                _pivotBaseLocalRot = _pivotTransform.localRotation;
            }
        }

        private void Update()
        {
            if (_shipController == null || _inputReader == null)
                return;

            if (!_shipController.IsEngineRunning)
                return;

            float thrustNorm = Mathf.Abs(_inputReader.CurrentThrust);
            float yawNorm = _inputReader.CurrentYaw;

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

                if (_pivotTransform != null)
                {
                    // Точка вращения = базовая позиция pivot + _pivotOffset
                    Vector3 rotationCenter = _pivotBaseLocalPos + _pivotOffset;
                    Vector3 offset = _pivotBaseLocalPos - rotationCenter;
                    _pivotTransform.localPosition = rotationCenter + rot * offset;
                    _pivotTransform.localRotation = _pivotBaseLocalRot * rot;
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
