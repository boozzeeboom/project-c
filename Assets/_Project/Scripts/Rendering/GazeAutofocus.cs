using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectC.Rendering
{
    /// <summary>
    /// DF-001: автофокус зрения для DistantFocus (Bokeh DoF).
    /// Рейкаст из центра камеры взгляда: дистанция попадания = целевой фокус.
    /// Промах (небо) = взгляд вдаль -> фокус уходит на даль.
    /// Пишет только focusDistance в РАНТАЙМ-копию профиля (ассет не мутирует).
    /// Bokeh-параметры (aperture, focalLength, blades) живут в VolumeProfile-ассете
    /// и крутятся в Inspector/Editor — скрипт их не трогает.
    /// Floating Origin: мировых Vector3 между кадрами не хранит (только скаляр
    /// дистанции), хук сдвига не нужен.
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

        [Header("Рейкаст взгляда")]
        [Tooltip("Какие слои участвуют в поиске фокуса. Триггеры игнорируются всегда.")]
        [SerializeField] private LayerMask _focusLayers = ~0;
        [Tooltip("Рейкаст раз в N кадров (фокус всё равно сглажен).")]
        [Range(1, 30)] [SerializeField] private int _raycastEveryNFrames = 4;
        [Tooltip("Дальность луча. 0 = farClipPlane камеры.")]
        [Min(0f)] [SerializeField] private float _maxRayDistance;

        [Header("Фокус")]
        [Tooltip("Фокус при взгляде в небо (промах луча). 0 = maxRayDistance.")]
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
            if (_focusVolume == null || _targetCamera == null) { enabled = false; return; }

            // Клонируем профиль: все Bokeh-настройки из ассета сохраняются,
            // но focusDistance пишем в копию.
            _runtimeProfile = Instantiate(_focusVolume.sharedProfile);
            _focusVolume.profile = _runtimeProfile;

            if (!_runtimeProfile.TryGet(out _dof)) { enabled = false; return; }
            if (_dof.mode.value == DepthOfFieldMode.Off) _dof.mode.value = DepthOfFieldMode.Bokeh;

            _currentFocus = _targetFocus = ResolveSkyFocus();
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
            float maxDist = ResolveMaxRayDistance();
            var ray = new Ray(_targetCamera.transform.position, _targetCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, maxDist, _focusLayers, QueryTriggerInteraction.Ignore))
                _targetFocus = Mathf.Max(_minFocusDistance, hit.distance);
            else
                _targetFocus = ResolveSkyFocus();
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
