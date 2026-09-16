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

        // Рантайм-копия профиля: ассет из Inspector никогда не мутирует (паттерн DayNight).
        private VolumeProfile _runtimeProfile;
        private DepthOfField _dof;
        private float _targetFocus;
        private float _currentFocus;
        private float _smoothVelocity;
        private int _frameCounter;

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
            if (_targetCamera == null) _targetCamera = Camera.main;
            if (_focusVolume == null) _focusVolume = GetComponent<Volume>();
            if (_cameraRig == null) _cameraRig = FindFirstObjectByType<SpringArmCamera>();
            if (_focusVolume == null || _targetCamera == null) { enabled = false; return; }

            // Клонируем профиль: все Bokeh-настройки из ассета сохраняются,
            // но focusDistance пишем в копию.
            _runtimeProfile = Instantiate(_focusVolume.sharedProfile);
            _focusVolume.profile = _runtimeProfile;

            if (!_runtimeProfile.TryGet(out _dof)) { enabled = false; return; }
            if (_dof.mode.value == DepthOfFieldMode.Off) _dof.mode.value = DepthOfFieldMode.Bokeh;

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
            if (!_focusEnabled || _dof == null || _targetCamera == null) return;

            if (_frameCounter++ % Mathf.Max(1, _raycastEveryNFrames) == 0) UpdateFocusTarget();

            _currentFocus = Mathf.SmoothDamp(_currentFocus, _targetFocus, ref _smoothVelocity, _focusSmoothTime);
            _dof.focusDistance.value = Mathf.Max(_minFocusDistance, _currentFocus);
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

            // Луч прошёл рядом с якорем = персонаж в центре кадра.
            bool rayNearAnchor = DistancePointToRay(ResolveAnchorPoint(), origin, dir) <= _anchorSnapRadius;

            if (!hasHit)
            {
                // Небо: если якорь в кадре — держим его, иначе уходим на даль.
                _targetFocus = rayNearAnchor ? anchorDist : ResolveSkyFocus();
                return;
            }

            if (hit.distance <= anchorDist)
            {
                // Препятствие перед якорем (стена в лицо) или сам якорь — фокус на попадание.
                _targetFocus = Mathf.Max(_minFocusDistance, hit.distance);
                return;
            }

            // Попадание ДАЛЬШЕ якоря: смотрим мимо персонажа.
            // Якорь в центре -> держим персонажа резким, фон мылится.
            // Якорь вне центра (камеру увели на объект) -> фокус на дальнее.
            _targetFocus = rayNearAnchor ? anchorDist : hit.distance;
        }

        private Vector3 ResolveAnchorPoint()
        {
            if (_manualAnchor != null) return _manualAnchor.position + Vector3.up * _anchorHeightOffset;
            Transform rigTarget = _cameraRig != null ? _cameraRig.TargetTransform : null;
            if (rigTarget != null) return rigTarget.position + Vector3.up * _anchorHeightOffset;
            // Якоря нет — точка перед камерой, чтобы старый чисто-лучевой режим не ломался.
            return _targetCamera.transform.position + _targetCamera.transform.forward * 10f;
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
