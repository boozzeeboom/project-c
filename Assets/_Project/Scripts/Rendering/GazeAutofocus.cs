using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ProjectC.Core;

namespace ProjectC.Rendering
{
    /// <summary>
    /// DF-001 (rev.2): автофокус зрения для DistantFocus (Bokeh DoF).
    /// Камера — third-person SpringArm: она всегда смотрит на персонажа,
    /// поэтому БАЗА фокуса = дистанция до якоря (цели камеры), а не луч.
    /// Луч из центра лишь корректирует:
    ///  - попадание ближе якоря (стена в лицо) -> фокус на препятствие;
    ///  - луч ушёл мимо якоря (камеру задрали в небо/горы, персонаж не в центре)
    ///    -> фокус на даль (попадание или небо).
    /// Пишет только focusDistance в РАНТАЙМ-копию профиля (ассет не мутирует).
    /// Bokeh-параметры (aperture, focalLength, blades) живут в VolumeProfile-ассете.
    /// Floating Origin: мировых Vector3 между кадрами не хранит (только скаляры),
    /// хук сдвига не нужен.
    /// DF-001 (rev.5): персонаж не должен мылиться НИКОГДА (не фоторежим).
    /// Честная оптика не умеет «даль резкая + ближний резкий» одновременно,
    /// поэтому добавлен чит киношников — авто-диафрагма: фокус на персонаже ->
    /// широко (фон плывёт), фокус ушёл вдаль -> узко (глубина резкости растёт,
    /// персонаж остаётся читаемым). Плюс физика: Bokeh 50мм f/5.6 на дистанциях
    /// 2–12м даёт CoC ~2px на фоне — глазом не видно. База профиля: 85мм f/2.
    /// </summary>
    [RequireComponent(typeof(Volume))]
    [DisallowMultipleComponent]
    public sealed class GazeAutofocus : MonoBehaviour
    {
        [Header("Связи")]
        [Tooltip("Камера взгляда. Пусто = Camera.main на OnEnable.")]
        [SerializeField] private Camera _targetCamera;
        [Tooltip("Volume с DepthOfField. Пусто = Volume на этом же GO.")]
        [SerializeField] private Volume _focusVolume;
        [Tooltip("Риг камеры (даёт цель-якорь вживую, в т.ч. при смене персонаж/корабль). Пусто = авто-поиск.")]
        [SerializeField] private SpringArmCamera _cameraRig;
        [Tooltip("Ручной якорь (перебивает риг). Пусто = цель рига.")]
        [SerializeField] private Transform _manualAnchor;

        [Header("Якорь (персонаж резкий по умолчанию)")]
        [Tooltip("Луч проходит в этом радиусе от якоря = смотрим 'через' персонажа -> фокус на якорь.")]
        [Min(0.1f)] [SerializeField] private float _anchorSnapRadius = 2f;
        [Tooltip("Смещение точки фокуса над позицией якоря (примерно грудь персонажа).")]
        [SerializeField] private float _anchorHeightOffset = 1.2f;

        [Header("Рейкаст взгляда")]
        [Tooltip("Какие слои участвуют в поиске фокуса. Триггеры игнорируются всегда.")]
        [SerializeField] private LayerMask _focusLayers = ~0;
        [Tooltip("Рейкаст раз в N кадров (фокус всё равно сглажен).")]
        [Range(1, 30)] [SerializeField] private int _raycastEveryNFrames = 4;
        [Tooltip("Дальность луча. 0 = farClipPlane камеры.")]
        [Min(0f)] [SerializeField] private float _maxRayDistance;

        [Header("Фокус")]
        [Tooltip("Фокус при взгляде в небо мимо якоря (промах луча). 0 = maxRayDistance.")]
        [Min(0f)] [SerializeField] private float _skyFocusDistance;
        [Tooltip("Нижний кламп фокуса (не ближе).")]
        [Min(0.1f)] [SerializeField] private float _minFocusDistance = 0.5f;
        [Tooltip("Время сглаживания фокуса (SmoothDamp), сек.")]
        [Min(0.01f)] [SerializeField] private float _focusSmoothTime = 0.25f;

        [Header("Состояние")]
        [Tooltip("Выкл = Volume weight 0 (эффект погашен, скрипт спит).")]
        [SerializeField] private bool _focusEnabled = true;

        [Header("Стабильность фокуса (без плавания)")]
        [Tooltip("Смена цели меньше этого (м) игнорируется — убивает дрожь фокуса.")]
        [Min(0f)] [SerializeField] private float _focusDeadband = 0.75f;
        [Tooltip("Новое направление фокуса принимается после стольких повторов подряд (1 = сразу).")]
        [Range(1, 10)] [SerializeField] private int _branchSettleFrames = 2;

        [Header("Авто-диафрагма (персонаж не мылится)")]
        [Tooltip("Вкл: диафрагма едет от Near (фокус на персонаже) к Far (фокус вдали). Выкл: значение из ассета.")]
        [SerializeField] private bool _autoAperture = true;
        [Tooltip("Диафрагма при фокусе на персонаже (фон плывёт сильно).")]
        [Range(1f, 32f)] [SerializeField] private float _nearAperture = 2.8f;
        [Tooltip("Диафрагма при фокусе вдали (глубина резкости большая, персонаж читаем).")]
        [Range(1f, 32f)] [SerializeField] private float _farAperture = 8f;
        [Tooltip("Дистанция фокуса, с которой начинается зауживание.")]
        [Min(1f)] [SerializeField] private float _farStart = 30f;
        [Tooltip("Дистанция фокуса, где диафрагма уже полностью Far.")]
        [Min(2f)] [SerializeField] private float _farEnd = 300f;

        [Header("Отладка")]
        [Tooltip("Писать в консоль камеру/якорь/фокус раз в секунду. Включить для диагностики.")]
        [SerializeField] private bool _debugLog;

        // Ручная камера из инспектора (перебивает авто-привязку к ригу).
        private bool _cameraManuallySet;

        // Рантайм-копия профиля: ассет из Inspector никогда не мутирует (паттерн DayNight).
        private VolumeProfile _runtimeProfile;
        private DepthOfField _dof;
        private float _targetFocus;
        private float _currentFocus;
        private float _smoothVelocity;
        private int _frameCounter;
        private int _lastBranch = -1;
        private int _branchRepeat;

        /// <summary>Текущий фокус (для HUD/отладки).</summary>
        public float CurrentFocusDistance => _currentFocus;

        /// <summary>Тумблер для меню настроек (SettingsManager): вкл/выкл эффекта.</summary>
        public void SetFocusEnabled(bool enabled)
        {
            _focusEnabled = enabled;
            ApplyEnabledState();
        }

        private void OnEnable()
        {
            // Привязки камеры/рига — ленивые (EnsureBindings): камера игрока
            // (префаб ThirdPersonCamera) спавнится ПОЗЖЕ Bootstrap, разовый
            // Camera.main здесь поймал бы статичную камеру Bootstrap.
            _cameraManuallySet = _targetCamera != null;
            // Пользовательский тумблер (Esc → Графика → Эффекты): сцена может
            // пересоздать объект после смены настройки — читаем актуальное.
            _focusEnabled = SettingsManager.DepthOfField;
            if (_focusVolume == null) _focusVolume = GetComponent<Volume>();
            if (_focusVolume == null) { enabled = false; return; }

            // Клонируем профиль: все Bokeh-настройки из ассета сохраняются,
            // но focusDistance пишем в копию.
            _runtimeProfile = Instantiate(_focusVolume.sharedProfile);
            _focusVolume.profile = _runtimeProfile;

            if (!_runtimeProfile.TryGet(out _dof))
            {
                // Самопочинка: в ассете нет DepthOfField (битый профиль) — создаём
                // в рантайм-копии с нейтральными значениями, эффект не должен молча умирать.
                Debug.LogWarning("[GazeAutofocus] DepthOfField отсутствует в профиле — создан fallback. Проверьте FocusVolumeProfile.asset.");
                _dof = _runtimeProfile.Add<DepthOfField>(true);
                _dof.mode.value = DepthOfFieldMode.Bokeh;
            }
            if (_dof.mode.value == DepthOfFieldMode.Off) _dof.mode.value = DepthOfFieldMode.Bokeh;

            EnsureBindings();
            _currentFocus = _targetFocus = ResolveAnchorDistance();
            _frameCounter = 0;
            ApplyEnabledState();
        }

        private void OnDisable()
        {
            if (_focusVolume != null) _focusVolume.weight = 0f;
        }

        private void Update()
        {
            if (!_focusEnabled || _dof == null) return;
            EnsureBindings();
            if (_targetCamera == null) return; // камера игрока ещё не заспавнилась — держим фокус

            if (_frameCounter++ % Mathf.Max(1, _raycastEveryNFrames) == 0) UpdateFocusTarget();

            _currentFocus = Mathf.SmoothDamp(_currentFocus, _targetFocus, ref _smoothVelocity, _focusSmoothTime);
            _dof.focusDistance.value = Mathf.Max(_minFocusDistance, _currentFocus);

            if (_autoAperture)
            {
                float t = Mathf.InverseLerp(_farStart, _farEnd, _currentFocus);
                _dof.aperture.value = Mathf.Lerp(_nearAperture, _farAperture, t);
            }

            if (_debugLog && _frameCounter % 60 == 0)
                Debug.Log($"[GazeAutofocus] cam={_targetCamera.name} anchor={ResolveAnchorDistance():F1} target={_targetFocus:F1} cur={_currentFocus:F1}");
        }

        // Ленивая привязка: риг и камера игрока появляются после Bootstrap.
        // Ручная камера из инспектора всегда побеждает.
        private void EnsureBindings()
        {
            if (_cameraRig == null) _cameraRig = FindAnyObjectByType<SpringArmCamera>();
            if (_cameraManuallySet) return;
            Camera rigCam = _cameraRig != null ? _cameraRig.CameraComponent : null;
            _targetCamera = rigCam != null ? rigCam : Camera.main;
        }

        private void UpdateFocusTarget()
        {
            float anchorDist = ResolveAnchorDistance();
            Vector3 origin = _targetCamera.transform.position;
            Vector3 dir = _targetCamera.transform.forward;
            float maxDist = ResolveMaxRayDistance();

            bool hasHit = Physics.Raycast(
                new Ray(origin, dir), out RaycastHit hit,
                maxDist, _focusLayers, QueryTriggerInteraction.Ignore);

            // Якорь неизвестен (риг ещё без цели): чисто лучевой режим со СВЕЖЕЙ камеры.
            if (!HasAnchor())
            {
                AcceptFocusTarget(
                    hasHit ? Mathf.Max(_minFocusDistance, hit.distance) : ResolveSkyFocus(),
                    hasHit ? 10 : 11);
                return;
            }

            // Луч прошёл рядом с якорем = персонаж в центре кадра.
            bool rayNearAnchor = DistancePointToRay(ResolveAnchorPoint(), origin, dir) <= _anchorSnapRadius;

            if (!hasHit)
            {
                // Небо: если якорь в кадре — держим его, иначе уходим на даль.
                AcceptFocusTarget(rayNearAnchor ? anchorDist : ResolveSkyFocus(), rayNearAnchor ? 20 : 21);
                return;
            }

            if (hit.distance <= anchorDist)
            {
                // Препятствие перед якорем (стена в лицо) или сам якорь — фокус на попадание.
                AcceptFocusTarget(Mathf.Max(_minFocusDistance, hit.distance), 30);
                return;
            }

            // Попадание ДАЛЬШЕ якоря: смотрим мимо персонажа.
            // Якорь в центре -> держим персонажа резким, фон мылится.
            // Якорь вне центра (камеру увели на объект) -> фокус на дальнее.
            AcceptFocusTarget(rayNearAnchor ? anchorDist : hit.distance, rayNearAnchor ? 40 : 41);
        }

        // Приём цели с гистерезисом: ветка применяется после повторов подряд
        // (не реагируем на одиночные чихи луча), микродвижки ниже deadband игнорятся.
        private void AcceptFocusTarget(float candidate, int branch)
        {
            if (branch != _lastBranch)
            {
                _lastBranch = branch;
                _branchRepeat = 1;
                if (_branchSettleFrames > 1) return;
            }
            else
            {
                _branchRepeat++;
                if (_branchRepeat < Mathf.Max(1, _branchSettleFrames)) return;
            }

            if (Mathf.Abs(candidate - _targetFocus) < Mathf.Max(0f, _focusDeadband)) return;
            _targetFocus = candidate;
        }

        private Vector3 ResolveAnchorPoint()
        {
            if (_manualAnchor != null) return _manualAnchor.position + Vector3.up * _anchorHeightOffset;
            Transform rigTarget = _cameraRig != null ? _cameraRig.TargetTransform : null;
            if (rigTarget != null) return rigTarget.position + Vector3.up * _anchorHeightOffset;
            // Якоря нет — точка перед камерой, чтобы старый чисто-лучевой режим не ломался.
            return _targetCamera.transform.position + _targetCamera.transform.forward * 10f;
        }

        private bool HasAnchor()
        {
            if (_manualAnchor != null) return true;
            return _cameraRig != null && _cameraRig.TargetTransform != null;
        }

        private float ResolveAnchorDistance()
        {
            if (_targetCamera == null) return 10f;
            return Mathf.Max(_minFocusDistance,
                Vector3.Distance(_targetCamera.transform.position, ResolveAnchorPoint()));
        }

        private static float DistancePointToRay(Vector3 point, Vector3 origin, Vector3 dir)
        {
            Vector3 toPoint = point - origin;
            float along = Vector3.Dot(toPoint, dir);
            if (along <= 0f) return float.MaxValue; // якорь позади камеры — «не в кадре»
            return (toPoint - dir * along).magnitude;
        }

        private float ResolveMaxRayDistance()
        {
            if (_maxRayDistance > 0f) return _maxRayDistance;
            return _targetCamera != null ? _targetCamera.farClipPlane : 1000f;
        }

        private float ResolveSkyFocus()
        {
            if (_skyFocusDistance > 0f) return _skyFocusDistance;
            return ResolveMaxRayDistance();
        }

        private void ApplyEnabledState()
        {
            if (_focusVolume != null) _focusVolume.weight = _focusEnabled ? 1f : 0f;
        }
    }
}
