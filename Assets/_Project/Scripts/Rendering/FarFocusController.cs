using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ProjectC.Core;

namespace ProjectC.Rendering
{
    /// <summary>
    /// DF-001 rev.7: контроллер взгляда для far-field прохода.
    /// Глубина центра кадра: луч (персонаж ловится лучом — CharacterController
    /// хитабелен) + фолбэк на якорь рига, если луч рядом с ним.
    /// Dwell: центр глубже FarThreshold держится LockTime (дефолт 1с) →
    /// _FarFocusLock 0→1: даль резкая, средний план чуть мылится.
    /// Пишет глобалы шейдеру каждый кадр. Bokeh-профиль при этом выключен.
    /// Floating Origin: только скаляры между кадрами, хук сдвига не нужен.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FarFocusController : MonoBehaviour
    {
        [Header("Связи")]
        [Tooltip("Камера взгляда. Пусто = ленивая привязка к ригу, запас — Camera.main.")]
        [SerializeField] private Camera _targetCamera;
        [Tooltip("Риг камеры (цель-якорь вживую). Пусто = авто-поиск.")]
        [SerializeField] private SpringArmCamera _cameraRig;
        [Tooltip("Ручной якорь (перебивает риг).")]
        [SerializeField] private Transform _manualAnchor;

        [Header("Центр кадра")]
        [Tooltip("Слои луча центра (персонаж входит — CharacterController хитабелен).")]
        [SerializeField] private LayerMask _focusLayers = ~0;
        [Tooltip("Рейкаст раз в N кадров.")]
        [Range(1, 30)] [SerializeField] private int _raycastEveryNFrames = 4;
        [Tooltip("Дальность луча. 0 = farClipPlane камеры.")]
        [Min(0f)] [SerializeField] private float _maxRayDistance;
        [Tooltip("Радиус: луч рядом с якорем = смотрим через персонажа.")]
        [Min(0.1f)] [SerializeField] private float _anchorSnapRadius = 2f;
        [Tooltip("Высота точки якоря над позицией цели.")]
        [SerializeField] private float _anchorHeightOffset = 1.2f;

        [Header("Лок взгляда (даль резкая)")]
        [Tooltip("Центр глубже этого (м) = кандидат на лок. Совместить с FarStart фичи.")]
        [Min(1f)] [SerializeField] private float _farThreshold = 60f;
        [Tooltip("Сколько держать взгляд, чтобы даль сфокусировалась (сек).")]
        [Min(0.1f)] [SerializeField] private float _lockTime = 1f;
        [Tooltip("Глубина при взгляде в небо мимо якоря. 0 = maxRayDistance.")]
        [Min(0f)] [SerializeField] private float _skyDepth;

        [Header("Gaussian Volume (фокус через проверенный Volume)")]
        [Tooltip("Вкл: писать gaussianStart/End в рантайм-копию профиля. Выкл: профиль рулится только ассетом.")]
        [SerializeField] private bool _driveGaussian = true;
        [Tooltip("Полоса блюра в покое (даль размыта): начало.")]
        [Min(1f)] [SerializeField] private float _restStart = 60f;
        [Tooltip("Полоса блюра в покое: конец.")]
        [Min(2f)] [SerializeField] private float _restEnd = 300f;
        [Tooltip("Полоса при локе вдаль (даль резкая): начало.")]
        [Min(1f)] [SerializeField] private float _lockStart = 50000f;
        [Tooltip("Полоса при локе вдаль: конец.")]
        [Min(2f)] [SerializeField] private float _lockEnd = 100000f;

        [Header("Отладка")]
        [Tooltip("Лог раз в секунду: камера/центр/лок.")]
        [SerializeField] private bool _debugLog;
        [Tooltip("Живое чтение: глубина центра (видно в Play).")]
        public float DebugCenterDepth;
        [Tooltip("Живое чтение: лок взгляда 0..1 (видно в Play).")]
        public float DebugFarLock;

        private static readonly int LockId = Shader.PropertyToID("_FarFocusLock");
        private static readonly int CenterId = Shader.PropertyToID("_FarFocusCenterDepth");

        private bool _cameraManuallySet;
        private float _centerDepth = 10f;
        private float _lockTimer;
        private float _farLock;
        private int _frameCounter;
        private bool _gaussianWarned;

        // Рантайм-копия профиля: ассет не мутирует (паттерн DayNight).
        private VolumeProfile _runtimeProfile;
        private UnityEngine.Rendering.Volume _focusVolume;
        private DepthOfField _dof;

        /// <summary>Глубина центра кадра, м (для HUD/отладки).</summary>
        public float CenterDepth => _centerDepth;
        /// <summary>Лок взгляда 0..1 (для HUD/отладки).</summary>
        public float FarLock => _farLock;

        private void OnEnable()
        {
            _cameraManuallySet = _targetCamera != null;
            EnsureBindings();

            // Рантайм-копия профиля под Gaussian-драйв (ассет не мутирует).
            _focusVolume = GetComponent<UnityEngine.Rendering.Volume>();
            if (_focusVolume != null)
            {
                _runtimeProfile = Instantiate(_focusVolume.sharedProfile);
                _focusVolume.profile = _runtimeProfile;
                _runtimeProfile.TryGet(out _dof);
            }
        }

        private void Update()
        {
            EnsureBindings();
            if (_targetCamera == null) return;

            if (_frameCounter++ % Mathf.Max(1, _raycastEveryNFrames) == 0) UpdateCenterDepth();

            // Dwell: держим даль — растёт лок, отвели взгляд — распад.
            if (_centerDepth > _farThreshold)
                _lockTimer += Time.deltaTime;
            else
                _lockTimer = Mathf.Max(0f, _lockTimer - Time.deltaTime * 2f);
            _farLock = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_lockTimer / Mathf.Max(0.01f, _lockTime)));

            Shader.SetGlobalFloat(LockId, _farLock);
            Shader.SetGlobalFloat(CenterId, _centerDepth);
            DebugCenterDepth = _centerDepth;
            DebugFarLock = _farLock;

            // Gaussian-драйв через проверенный Volume-путь: лок уводит полосу
            // за контент (даль резкая), покой держит полосу на дали.
            // Персонаж (≤35м) всегда ближе Start → не мылится никогда.
            if (_driveGaussian && _dof != null)
            {
                if (_dof.mode.value != DepthOfFieldMode.Gaussian)
                {
                    _dof.mode.value = DepthOfFieldMode.Gaussian;
                    if (!_gaussianWarned)
                    {
                        _gaussianWarned = true;
                        Debug.LogWarning("[FarFocus] DepthOfField переключён в Gaussian для драйва фокуса.");
                    }
                }
                _dof.gaussianStart.value = Mathf.Lerp(_restStart, _lockStart, _farLock);
                _dof.gaussianEnd.value = Mathf.Lerp(_restEnd, _lockEnd, _farLock);
            }

            if (_debugLog && _frameCounter % 60 == 0)
                Debug.Log($"[FarFocus] cam={_targetCamera.name} center={_centerDepth:F0} lock={_farLock:F2}");
        }

        private void UpdateCenterDepth()
        {
            Vector3 origin = _targetCamera.transform.position;
            Vector3 dir = _targetCamera.transform.forward;
            float maxDist = _maxRayDistance > 0f ? _maxRayDistance : _targetCamera.farClipPlane;

            if (Physics.Raycast(new Ray(origin, dir), out RaycastHit hit,
                maxDist, _focusLayers, QueryTriggerInteraction.Ignore))
            {
                _centerDepth = hit.distance;
                return;
            }

            // Промах: рядом с якорем — смотрим через персонажа, центр = якорь.
            if (HasAnchor() && DistancePointToRay(ResolveAnchorPoint(), origin, dir) <= _anchorSnapRadius)
                _centerDepth = ResolveAnchorDistance();
            else
                _centerDepth = _skyDepth > 0f ? _skyDepth : maxDist;
        }

        private void EnsureBindings()
        {
            if (_cameraRig == null) _cameraRig = FindAnyObjectByType<SpringArmCamera>();
            if (_cameraManuallySet) return;
            Camera rigCam = _cameraRig != null ? _cameraRig.CameraComponent : null;
            _targetCamera = rigCam != null ? rigCam : Camera.main;
        }

        private bool HasAnchor()
        {
            if (_manualAnchor != null) return true;
            return _cameraRig != null && _cameraRig.TargetTransform != null;
        }

        private Vector3 ResolveAnchorPoint()
        {
            if (_manualAnchor != null) return _manualAnchor.position + Vector3.up * _anchorHeightOffset;
            Transform rigTarget = _cameraRig != null ? _cameraRig.TargetTransform : null;
            if (rigTarget != null) return rigTarget.position + Vector3.up * _anchorHeightOffset;
            return _targetCamera.transform.position + _targetCamera.transform.forward * 10f;
        }

        private float ResolveAnchorDistance()
        {
            if (_targetCamera == null) return 10f;
            return Mathf.Max(0.5f, Vector3.Distance(_targetCamera.transform.position, ResolveAnchorPoint()));
        }

        private static float DistancePointToRay(Vector3 point, Vector3 origin, Vector3 dir)
        {
            Vector3 toPoint = point - origin;
            float along = Vector3.Dot(toPoint, dir);
            if (along <= 0f) return float.MaxValue;
            return (toPoint - dir * along).magnitude;
        }
    }
}
