using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using ProjectC.UI;

namespace ProjectC.Core
{
    /// <summary>
    /// Spring Arm камера от третьего лица с collision avoidance и сглаживанием.
    /// Заменяет ThirdPersonCamera — сохраняет полный API-контракт.
    /// Архитектура: независимый корневой объект (НЕ дочерний игроку — FloatingOriginMP).
    /// </summary>
    public class SpringArmCamera : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════
        // Inspector: Target
        // ═══════════════════════════════════════════════════════════

        [Header("Target")]
        [Tooltip("Персонаж/корабль за которым следить")]
        [SerializeField] private Transform target;

        // ═══════════════════════════════════════════════════════════
        // Inspector: Orbit
        // ═══════════════════════════════════════════════════════════

        [Header("Orbit")]
        [Tooltip("Дистанция от цели (пеший режим)")]
        [SerializeField] private float distance = 5f;

        [Tooltip("Дистанция от цели (режим корабля)")]
        [SerializeField] private float shipDistance = 18f;

        [Tooltip("Высота камеры относительно цели (пеший)")]
        [SerializeField] private float height = 2f;

        [Tooltip("Высота камеры относительно цели (корабль)")]
        [SerializeField] private float shipHeight = 6f;

        [Tooltip("Минимальный вертикальный угол (pitch)")]
        [SerializeField] private float minVerticalAngle = -80f;

        [Tooltip("Максимальный вертикальный угол (pitch)")]
        [SerializeField] private float maxVerticalAngle = 80f;

        // ═══════════════════════════════════════════════════════════
        // Inspector: Sensitivity
        // ═══════════════════════════════════════════════════════════

        [Header("Sensitivity")]
        [Tooltip("Чувствительность мыши")]
        [SerializeField] private float mouseSensitivity = 3f;

        [Tooltip("Инвертировать Y")]
        [SerializeField] private bool invertY = false;

        // ═══════════════════════════════════════════════════════════
        // Inspector: Spring Arm (Collision Avoidance)
        // ═══════════════════════════════════════════════════════════

        [Header("Spring Arm")]
        [Tooltip("Радиус SphereCast для определения коллизий")]
        [SerializeField] private float sphereCastRadius = 0.4f;

        [Tooltip("Слои для проверки коллизий")]
        [SerializeField] private LayerMask collisionMask = ~0;

        [Tooltip("Отступ от стены при коллизии")]
        [SerializeField] private float wallOffset = 0.3f;

        // ═══════════════════════════════════════════════════════════
        // Inspector: Smoothing
        // ═══════════════════════════════════════════════════════════

        [Header("Smoothing")]
        [Tooltip("Время сглаживания позиции (SmoothDamp)")]
        [SerializeField] private float positionSmoothTime = 0.12f;

        // ═══════════════════════════════════════════════════════════
        // Inspector: Anti-Pop
        // ═══════════════════════════════════════════════════════════

        [Header("Anti-Pop")]
        [Tooltip("Гистерезис при выходе из коллизии (сек)")]
        [SerializeField] private float antiPopTime = 0.2f;

        // ═══════════════════════════════════════════════════════════
        // Inspector: Wall Recovery
        // ═══════════════════════════════════════════════════════════

        [Header("Wall Recovery")]
        [Tooltip("Максимальная скорость восстановления (m/s)")]
        [SerializeField] private float recoverySpeed = 10f;

        [Tooltip("Порог срабатывания recovery (отношение actualDist/desiredDist)")]
        [SerializeField] private float recoveryRatio = 0.4f;

        // ═══════════════════════════════════════════════════════════
        // Inspector: Camera Lag
        // ═══════════════════════════════════════════════════════════

        [Header("Camera Lag")]
        [Tooltip("Включить инерцию камеры (отставание от target)")]
        [SerializeField] private bool lagEnabled = true;

        [Tooltip("Время отставания по горизонтали (XZ)")]
        [SerializeField] private float lagHorizontalTime = 0.15f;

        [Tooltip("Время отставания по вертикали (Y)")]
        [SerializeField] private float lagVerticalTime = 0.05f;

        [Tooltip("Динамический lag: при беге отставание уменьшается")]
        [SerializeField] private bool dynamicLagEnabled = true;

        // ═══════════════════════════════════════════════════════════
        // Inspector: Adaptive Distance
        // ═══════════════════════════════════════════════════════════

        [Header("Adaptive Distance")]
        [Tooltip("Автоматически уменьшать дистанцию в узких пространствах")]
        [SerializeField] private bool adaptiveDistanceEnabled = true;

        [Tooltip("Порог срабатывания (отношение actualDist/desiredDist)")]
        [SerializeField] private float adaptiveThreshold = 0.7f;

        [Tooltip("Задержка перед уменьшением дистанции (гистерезис)")]
        [SerializeField] private float adaptiveDelay = 0.5f;

        [Tooltip("Скорость уменьшения дистанции")]
        [SerializeField] private float adaptiveSpeed = 3f;

        [Tooltip("Скорость восстановления дистанции")]
        [SerializeField] private float adaptiveRecoverySpeed = 2f;

        // ═══════════════════════════════════════════════════════════
        // Inspector: Mode Transition
        // ═══════════════════════════════════════════════════════════

        [Header("Mode Transition")]
        [Tooltip("Время плавного перехода walk↔ship (сек)")]
        [SerializeField] private float modeSwitchSmoothTime = 0.5f;

        // ═══════════════════════════════════════════════════════════
        // Inspector: LookAt
        // ═══════════════════════════════════════════════════════════

        [Header("LookAt")]
        [Tooltip("Высота точки взгляда — пеший (голова персонажа)")]
        [SerializeField] private float lookAtHeightWalk = 1.5f;

        [Tooltip("Высота точки взгляда — корабль (центр корпуса)")]
        [SerializeField] private float lookAtHeightShip = 4f;

        // ═══════════════════════════════════════════════════════════
        // Internal State: Orbit
        // ═══════════════════════════════════════════════════════════

        private float _yaw;
        private float _pitch;

        // Текущие интерполированные значения
        private float _currentDistance;
        private float _currentHeight;
        private float _currentLookAtHeight;

        // Целевые значения (для плавного перехода режимов)
        private float _targetDistance;
        private float _targetHeight;
        private float _targetLookAtHeight;
        private bool _isShip;

        // ═══════════════════════════════════════════════════════════
        // Internal State: SmoothDamp velocities
        // ═══════════════════════════════════════════════════════════

        private Vector3 _positionVelocity;
        private Vector3 _recoveryVelocity;
        private float _distanceVelocity;
        private float _heightVelocity;
        private float _lookAtVelocity;

        // ═══════════════════════════════════════════════════════════
        // Internal State: Camera Lag
        // ═══════════════════════════════════════════════════════════

        private Vector3 _lagTargetPos;

        // ═══════════════════════════════════════════════════════════
        // Internal State: Adaptive Distance
        // ═══════════════════════════════════════════════════════════

        private float _lastClearTime;

        // ═══════════════════════════════════════════════════════════
        // Internal State: Collision
        // ═══════════════════════════════════════════════════════════

        private float _collisionExitTime;
        private bool _wasColliding;

        // ═══════════════════════════════════════════════════════════
        // Internal State: Input
        // ═══════════════════════════════════════════════════════════

        private InputAction _lookAction;
        private Vector2 _lookInput;

        // ═══════════════════════════════════════════════════════════
        // Internal State: Initialization
        // ═══════════════════════════════════════════════════════════

        private bool _cameraInitialized;
        private Camera _camera;

        // ═══════════════════════════════════════════════════════════
        // Internal State: UI (сохранено из ThirdPersonCamera)
        // ═══════════════════════════════════════════════════════════

        private ProjectC.UI.ControlHintsUI _cachedControlHintsUI;
        private Canvas _cachedCanvas;

        // ═══════════════════════════════════════════════════════════
        // Internal State: SettingsManager bridge
        // ═══════════════════════════════════════════════════════════

        private float _cachedMouseSensitivity = 3f;
        private bool _cachedInvertY = false;

        // ═══════════════════════════════════════════════════════════
        // Public API (API-контракт ThirdPersonCamera)
        // ═══════════════════════════════════════════════════════════

        public Camera CameraComponent => _camera;
        public Transform TargetTransform => target;

        /// <summary>
        /// Горизонтальное направление камеры (куда бежит персонаж по W)
        /// </summary>
        public Vector3 CameraForward
        {
            get
            {
                float yawRad = _yaw * Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(yawRad), 0, Mathf.Cos(yawRad));
            }
        }

        public Vector3 CameraRight
        {
            get
            {
                float yawRad = _yaw * Mathf.Deg2Rad;
                return new Vector3(Mathf.Cos(yawRad), 0, -Mathf.Sin(yawRad));
            }
        }

        /// <summary>
        /// Установить новую цель (например, корабль)
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            if (newTarget != null)
            {
                target = newTarget;
                _lagTargetPos = target.position;
            }
        }

        /// <summary>
        /// COMPOSITE SHIP: установить target и сразу переключить режим камеры.
        /// Заменяет пару SetTarget+SetShipMode.
        /// </summary>
        public void SetTargetMode(Transform newTarget, bool isShip)
        {
            SetTarget(newTarget);
            SetShipMode(isShip);
        }

        /// <summary>
        /// Переключить режим камеры (пеший ↔ корабль).
        /// Только задаёт цели — плавный переход в LateUpdate через SmoothDamp.
        /// </summary>
        public void SetShipMode(bool isShip)
        {
            _isShip = isShip;
            _targetDistance = isShip ? shipDistance : distance;
            _targetHeight = isShip ? shipHeight : height;
            _targetLookAtHeight = isShip ? lookAtHeightShip : lookAtHeightWalk;
        }

        /// <summary>
        /// Инициализировать камеру после назначения target.
        /// Вызывается из NetworkPlayer.SpawnCamera() сразу после SetTarget().
        /// Безопасно вызывать несколько раз — повторная инициализация игнорируется.
        /// </summary>
        public void InitializeCamera()
        {
            if (_cameraInitialized) return;
            if (target == null)
            {
                Debug.LogWarning("[SpringArmCamera] InitializeCamera вызван до SetTarget! Камера не инициализирована.");
                return;
            }

            _yaw = 0f;
            _pitch = 15f;
            _currentDistance = distance;
            _currentHeight = height;
            _currentLookAtHeight = lookAtHeightWalk;
            _targetDistance = distance;
            _targetHeight = height;
            _targetLookAtHeight = lookAtHeightWalk;
            _lagTargetPos = target.position;
            _lastClearTime = Time.time;

            // Блокируем курсор ТОЛЬКО если NetworkManager активен
            bool inActiveGame = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            if (inActiveGame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            UpdateCameraPosition();

            // Создаём UI подсказок если нет
            CreateControlHintsUI();

            _cameraInitialized = true;

            // Регистрируем камеру для Billboard
            Billboard.ActiveCamera = transform;

            // Подписка на изменения настроек
            RefreshSettings();
            SettingsManager.OnMouseSensitivityChanged += OnSensitivityChanged;
            SettingsManager.OnInvertYChanged += OnInvertChanged;
        }

        // ═══════════════════════════════════════════════════════════
        // Settings callbacks
        // ═══════════════════════════════════════════════════════════

        private void OnSensitivityChanged(float v) => _cachedMouseSensitivity = v;
        private void OnInvertChanged(bool v) => _cachedInvertY = v;
        private void RefreshSettings()
        {
            _cachedMouseSensitivity = SettingsManager.MouseSensitivity;
            _cachedInvertY = SettingsManager.InvertY;
        }

        // ═══════════════════════════════════════════════════════════
        // Unity Lifecycle
        // ═══════════════════════════════════════════════════════════

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera != null)
            {
                _camera.farClipPlane = 1000000f;
                _camera.nearClipPlane = 0.5f;
            }

            _lookAction = new InputAction("Look", binding: "<Mouse>/delta", expectedControlType: "Vector2");
        }

        private void OnEnable() => _lookAction.Enable();
        private void OnDisable() => _lookAction.Disable();

        private void OnDestroy()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Start()
        {
            if (_cameraInitialized) return;

            if (target == null) return;

            InitializeCamera();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            // Pipeline
            ReadInput();
            UpdateModeTransition();
            UpdateLag();
            Vector3 desiredPos = ComputeDesiredPosition();
            Vector3 resolvedPos = ResolveCollision(desiredPos);
            UpdateAdaptiveDistance();
            SmoothPosition(resolvedPos);
            UpdateLookAt();
        }

        // ═══════════════════════════════════════════════════════════
        // Pipeline Step 1: ReadInput
        // ═══════════════════════════════════════════════════════════

        private void ReadInput()
        {
            _lookInput = _lookAction.ReadValue<Vector2>();

            float sens = _cachedMouseSensitivity;
            float invert = _cachedInvertY ? -1f : 1f;

            _yaw += _lookInput.x * sens;
            _pitch -= _lookInput.y * sens * invert;
            _pitch = Mathf.Clamp(_pitch, minVerticalAngle, maxVerticalAngle);
        }

        // ═══════════════════════════════════════════════════════════
        // Pipeline Step 2: UpdateModeTransition
        // ═══════════════════════════════════════════════════════════

        private void UpdateModeTransition()
        {
            _currentDistance = Mathf.SmoothDamp(
                _currentDistance, _targetDistance,
                ref _distanceVelocity, modeSwitchSmoothTime);

            _currentHeight = Mathf.SmoothDamp(
                _currentHeight, _targetHeight,
                ref _heightVelocity, modeSwitchSmoothTime);

            _currentLookAtHeight = Mathf.SmoothDamp(
                _currentLookAtHeight, _targetLookAtHeight,
                ref _lookAtVelocity, modeSwitchSmoothTime);
        }

        // ═══════════════════════════════════════════════════════════
        // Pipeline Step 3: UpdateLag
        // ═══════════════════════════════════════════════════════════

        private void UpdateLag()
        {
            if (!lagEnabled || target == null)
            {
                _lagTargetPos = target != null ? target.position : Vector3.zero;
                return;
            }

            Vector3 delta = target.position - _lagTargetPos;

            // Динамический lag: при беге отставание уменьшается
            float lagXZ, lagY;
            if (dynamicLagEnabled)
            {
                float speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
                float speedFactor = Mathf.InverseLerp(0f, 10f, speed);
                float dynamicMultiplier = Mathf.Lerp(1f, 0.3f, speedFactor);
                lagXZ = 1f / (lagHorizontalTime * dynamicMultiplier);
                lagY = 1f / (lagVerticalTime * dynamicMultiplier);
            }
            else
            {
                lagXZ = 1f / Mathf.Max(lagHorizontalTime, 0.001f);
                lagY = 1f / Mathf.Max(lagVerticalTime, 0.001f);
            }

            _lagTargetPos.x += delta.x * lagXZ * Time.deltaTime;
            _lagTargetPos.z += delta.z * lagXZ * Time.deltaTime;
            _lagTargetPos.y += delta.y * lagY * Time.deltaTime;
        }

        // ═══════════════════════════════════════════════════════════
        // Pipeline Step 4: ComputeDesiredPosition
        // ═══════════════════════════════════════════════════════════

        private Vector3 ComputeDesiredPosition()
        {
            Vector3 orbitDir = SphericalToCartesian(_yaw, _pitch);
            return _lagTargetPos + orbitDir * _currentDistance + Vector3.up * _currentHeight;
        }

        private static Vector3 SphericalToCartesian(float yaw, float pitch)
        {
            float yawRad = yaw * Mathf.Deg2Rad;
            float pitchRad = pitch * Mathf.Deg2Rad;

            return new Vector3(
                -Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
                Mathf.Sin(pitchRad),
                -Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
            );
        }

        // ═══════════════════════════════════════════════════════════
        // Pipeline Step 4: ResolveCollision (SphereCast + Anti-Pop)
        // ═══════════════════════════════════════════════════════════

        private Vector3 ResolveCollision(Vector3 desiredPos)
        {
            Vector3 from = _lagTargetPos + Vector3.up * _currentLookAtHeight;
            Vector3 direction = (desiredPos - from).normalized;
            float maxDist = Vector3.Distance(from, desiredPos);

            if (maxDist < 0.01f) return desiredPos;

            float currentTime = Time.time;
            bool hit = Physics.SphereCast(
                from, sphereCastRadius, direction,
                out RaycastHit hitInfo, maxDist, collisionMask);

            if (hit)
            {
                _wasColliding = true;
                _collisionExitTime = currentTime;

                return hitInfo.point + hitInfo.normal * (sphereCastRadius + wallOffset);
            }
            else if (_wasColliding && currentTime - _collisionExitTime < antiPopTime)
            {
                // Anti-pop гистерезис: остаёмся прижатыми ещё antiPopTime
                return transform.position;
            }
            else
            {
                _wasColliding = false;
                return desiredPos;
            }
        }

        // ═══════════════════════════════════════════════════════════
        // Pipeline Step 5: SmoothPosition (SmoothDamp + Wall Recovery)
        // ═══════════════════════════════════════════════════════════

        private void SmoothPosition(Vector3 cameraTargetPos)
        {
            float actualDist = Vector3.Distance(cameraTargetPos, _lagTargetPos);
            float desiredDist = _targetDistance;
            float ratio = actualDist / Mathf.Max(desiredDist, 0.1f);

            if (ratio < recoveryRatio)
            {
                // Fast recovery: камера сильно прижата
                float fastSmoothTime = positionSmoothTime * 0.3f;
                transform.position = Vector3.SmoothDamp(
                    transform.position, cameraTargetPos,
                    ref _recoveryVelocity, fastSmoothTime,
                    recoverySpeed);
            }
            else
            {
                // Normal smooth
                transform.position = Vector3.SmoothDamp(
                    transform.position, cameraTargetPos,
                    ref _positionVelocity, positionSmoothTime);
            }
        }

        // ═══════════════════════════════════════════════════════════
        // Pipeline Step 6: UpdateAdaptiveDistance
        // ═══════════════════════════════════════════════════════════

        private void UpdateAdaptiveDistance()
        {
            if (!adaptiveDistanceEnabled) return;

            float actualDist = Vector3.Distance(transform.position, _lagTargetPos);
            float desiredDist = _isShip ? shipDistance : distance;
            float ratio = actualDist / Mathf.Max(desiredDist, 0.1f);
            float currentTime = Time.time;

            if (ratio < adaptiveThreshold && _wasColliding)
            {
                // Камера постоянно прижата → уменьшаем дистанцию
                if (currentTime - _lastClearTime > adaptiveDelay)
                {
                    float minDist = Mathf.Max(1f, actualDist - wallOffset - sphereCastRadius);
                    _targetDistance = Mathf.Lerp(
                        _targetDistance, minDist,
                        adaptiveSpeed * Time.deltaTime);
                }
            }
            else
            {
                // Восстанавливаем базовую дистанцию
                float baseDist = _isShip ? shipDistance : distance;
                _targetDistance = Mathf.Lerp(
                    _targetDistance, baseDist,
                    adaptiveRecoverySpeed * Time.deltaTime);

                if (ratio > 0.95f)
                    _lastClearTime = currentTime;
            }
        }

        // ═══════════════════════════════════════════════════════════
        // Pipeline Step 7: UpdateLookAt
        // ═══════════════════════════════════════════════════════════

        private void UpdateLookAt()
        {
            Vector3 lookTarget = _lagTargetPos + Vector3.up * _currentLookAtHeight;
            transform.LookAt(lookTarget);
        }

        // ═══════════════════════════════════════════════════════════
        // Legacy helper (сохранено для совместимости)
        // ═══════════════════════════════════════════════════════════

        private void UpdateCameraPosition()
        {
            // Вызывается только при инициализации — мгновенный прыжок на позицию
            Vector3 orbitDir = SphericalToCartesian(_yaw, _pitch);
            transform.position = target.position + orbitDir * _currentDistance + Vector3.up * _currentHeight;
            transform.LookAt(target.position + Vector3.up * _currentLookAtHeight);
        }

        // ═══════════════════════════════════════════════════════════
        // UI: ControlHints (сохранено из ThirdPersonCamera)
        // ═══════════════════════════════════════════════════════════

        private void CreateControlHintsUI()
        {
            if (_cachedControlHintsUI != null) return;

            var existingHints = FindObjectsByType<ProjectC.UI.ControlHintsUI>(FindObjectsInactive.Include);
            if (existingHints != null && existingHints.Length > 0)
            {
                _cachedControlHintsUI = existingHints[0];
                return;
            }

            var hudManager = ProjectC.UI.HUDManager.EnsureExists();
            _cachedCanvas = hudManager.GetOrCreateHUDCanvas();

            var (textObj, textRect, tmpText) = hudManager.CreateHUDText(
                "ControlHintsText",
                null,
                fontSize: 14,
                color: Color.white,
                alignment: TextAlignmentOptions.TopLeft,
                anchoredPosition: new Vector2(20, -20),
                sizeDelta: new Vector2(300, 300)
            );

            GameObject hintsObj = new GameObject("ControlHintsUI");
            hintsObj.transform.SetParent(_cachedCanvas.transform);
            _cachedControlHintsUI = hintsObj.AddComponent<ProjectC.UI.ControlHintsUI>();
            _cachedControlHintsUI.hintsText = tmpText;
        }

        // ═══════════════════════════════════════════════════════════
        // Editor: Gizmos
        // ═══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (target == null) return;

            // Отображаем SphereCast
            float lookAtH = Application.isPlaying ? _currentLookAtHeight : lookAtHeightWalk;
            float dist = Application.isPlaying ? _currentDistance : distance;
            float h = Application.isPlaying ? _currentHeight : height;

            Vector3 orbitDir = SphericalToCartesian(_yaw, _pitch);
            Vector3 desiredPos = target.position + orbitDir * dist + Vector3.up * h;
            Vector3 from = target.position + Vector3.up * lookAtH;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(from, sphereCastRadius);
            Gizmos.DrawLine(from, desiredPos);

            Gizmos.color = _wasColliding ? Color.red : Color.green;
            Gizmos.DrawWireSphere(transform.position, sphereCastRadius);
        }
#endif
    }
}
