using UnityEngine;
using ProjectC.Player;

namespace ProjectC.Ship
{
    /// <summary>
    /// ShipPartShake — визуальный дребезг части корабля при тяге (W/S).
    ///
    /// Вешается на любой визуальный элемент (двигатель, антенна, блок) —
    /// элемент начинает вибрировать пропорционально thrust.
    ///
    /// Использует AnimationCurve для точной настройки формы колебаний
    /// (по умолчанию — синусоида). Работает через локальные позицию и вращение,
    /// не влияет на физику.
    ///
    /// Паттерн разрешения зависимостей — как в EngineThrusterVisual:
    /// ShipRootReference → ShipController → ShipInputReader.
    /// </summary>
    public class ShipPartShake : MonoBehaviour
    {
        [Header("Shake Profile")]
        [Tooltip("Кривая формы колебаний. Ось X = фаза (0-1), ось Y = амплитуда (-1..1). По умолчанию синусоида.")]
        [SerializeField] private AnimationCurve _shakeCurve;

        [Tooltip("Частота вибрации (Гц).")]
        [SerializeField] private float _frequency = 15f;

        [Tooltip("Амплитуда смещения позиции (X=вправо, Y=вверх, Z=вперёд) в локальном пространстве.")]
        [SerializeField] private Vector3 _positionAmplitude = new Vector3(0.01f, 0.01f, 0.02f);

        [Tooltip("Амплитуда вращения (градусы) вокруг локальных осей X/Y/Z.")]
        [SerializeField] private Vector3 _rotationAmplitude = new Vector3(0.5f, 0.3f, 0.5f);

        [Tooltip("Порог тяги (0-1), ниже которого дребезг не применяется.")]
        [Range(0f, 1f)]
        [SerializeField] private float _thrustThreshold = 0.05f;

        [Tooltip("Время сглаживания атаки/затухания (сек). Чтобы дрожь нарастала и спадала плавно.")]
        [SerializeField] private float _smoothTime = 0.4f;

        [Header("Dependencies")]
        [Tooltip("ShipRootReference на этой или родительской части корабля. Авто-поиск если null.")]
        [SerializeField] private ShipRootReference _rootRef;

        // Кешированные ссылки
        private ShipController _shipController;
        private ShipInputReader _inputReader;

        // Сохранённая базовая позиция/вращение (до дрожи)
        private Vector3 _baseLocalPos;
        private Quaternion _baseLocalRot;

        // Текущая фаза синусоиды (накапливается)
        private float _phase;

        // Сглаженное значение thrust (attack/release)
        private float _smoothThrust;
        private float _smoothVelocity;

        private void Start()
        {
            _baseLocalPos = transform.localPosition;
            _baseLocalRot = transform.localRotation;
            ResolveDependencies();

            // Если кривая не настроена — ставим дефолтную синусоиду
            EnsureSineCurve();
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
                Debug.LogWarning($"[ShipPartShake] '{name}': ShipController не найден.", this);
        }

        private void Update()
        {
            if (_shipController == null || _inputReader == null || !_shipController.enabled)
                return;

            if (!_shipController.IsEngineRunning)
                return;

            float targetThrust = Mathf.Abs(_inputReader.CurrentThrust);

            // Сглаживаем thrust для плавной атаки/затухания
            _smoothThrust = Mathf.SmoothDamp(_smoothThrust, targetThrust, ref _smoothVelocity, _smoothTime);

            if (_smoothThrust < _thrustThreshold)
                return;

            // Накапливаем фазу
            _phase += Time.deltaTime * _frequency;
            float phase01 = _phase % 1f;

            // Оцениваем кривую: фаза (0-1) → амплитуда (-1..1)
            float curveValue = _shakeCurve.Evaluate(phase01);

            // Масштабируем на thrust и амплитуды
            float intensity = _smoothThrust * curveValue;

            // Позиционное смещение
            Vector3 posOffset = Vector3.Scale(_positionAmplitude, new Vector3(intensity, intensity, intensity));
            transform.localPosition = _baseLocalPos + posOffset;

            // Вращательное смещение (в градусах)
            Vector3 rotOffset = Vector3.Scale(_rotationAmplitude, new Vector3(intensity, intensity, intensity));
            transform.localRotation = _baseLocalRot * Quaternion.Euler(rotOffset);
        }

        private void OnDisable()
        {
            // Сброс к базовым значениям при отключении
            if (this != null && transform != null)
            {
                transform.localPosition = _baseLocalPos;
                transform.localRotation = _baseLocalRot;
            }
        }

        /// <summary>
        /// Проверяет что кривая задаёт реальные колебания (не плоская).
        /// Если плоская — ставит синусоиду по умолчанию.
        /// </summary>
        private void EnsureSineCurve()
        {
            bool isFlat = _shakeCurve == null || _shakeCurve.length == 0;
            if (!isFlat && _shakeCurve.length > 0)
            {
                float maxAbs = 0f;
                for (int i = 0; i < _shakeCurve.length; i++)
                    maxAbs = Mathf.Max(maxAbs, Mathf.Abs(_shakeCurve[i].value));
                isFlat = maxAbs < 0.001f;
            }

            if (isFlat)
            {
                _shakeCurve = new AnimationCurve(
                    new Keyframe(0f, 0f, 1.5708f, 1.5708f),     // sin(0)=0, cos(0)=1
                    new Keyframe(0.25f, 1f, 0f, 0f),             // sin(π/2)=1
                    new Keyframe(0.5f, 0f, -1.5708f, -1.5708f),  // sin(π)=0
                    new Keyframe(0.75f, -1f, 0f, 0f),            // sin(3π/2)=-1
                    new Keyframe(1f, 0f, 1.5708f, 1.5708f)       // sin(2π)=0
                );
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_rootRef == null)
                _rootRef = GetComponentInParent<ShipRootReference>();

            EnsureSineCurve();
        }

        private void Reset()
        {
            _rootRef = GetComponentInParent<ShipRootReference>();
            _shakeCurve = null;
            EnsureSineCurve();
        }
#endif
    }
}
