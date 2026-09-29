using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using ProjectC.UI;
using ProjectC.World.FloatingOrigin;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.Core
{
    /// <summary>
    /// Spring Arm камера от третьего лица.
    /// Архитектура: независимый корневой объект (НЕ дочерний игроку — FloatingOriginMP).
    /// Pipeline: ReadInput → ModeTransition → CameraLag → ComputeDesired
    ///        → ResolveCollision(chain-cast+AntiPop+nearClip) → AdaptiveDistance
    ///        → SmoothPosition(dead-zone+Recovery) → LookAt(сглаженный)
    /// Lag = инерция (walk 0.15s, ship откл). SmoothDamp = anti-jitter (0.04s).
    /// При падении — вертикальный lag ускоряется в 2.5x (гистерезис вкл/выкл).
    /// Dead-zone 3mm — убивает микро-осцилляции.
    /// T-CAM17: Adaptive — 3 состояния SHRINK/REST/RECOVER. REST (у стены, но дистанция
    /// влезает) — точка покоя: ничего не делает. Переключение больше не на ratio,
    /// а на условие влезания + устойчивые таймеры — реле-цикл T-CAM16 устранён.
    /// T-CAM14: near-clip constraint вынесен в ResolveCollision (единый источник),
    /// AdaptiveDistance работает поверх отдельной пользовательской zoom-дистанции,
    /// positionSmoothTime 0.08→0.04 (возврат к задумке T-CAM10: Lag/Smooth 3.75×).
    /// </summary>
    public class SpringArmCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Orbit")]
        [SerializeField] private float distance = 5f;
        [SerializeField] private float shipDistance = 18f;
        [SerializeField] private float height = 2f;
        [SerializeField] private float shipHeight = 6f;
        [SerializeField] private float minVerticalAngle = -80f;
        [SerializeField] private float maxVerticalAngle = 80f;

        [Header("Sensitivity")]
        [SerializeField] private float mouseSensitivity = 3f;
        [SerializeField] private bool invertY = false;

        [Header("Collision Avoidance")]
        [SerializeField] private float sphereCastRadius = 0.4f;
        [Tooltip("Не исключайте слой Default — на нём вся геометрия мира!")]
        [SerializeField] private LayerMask collisionMask = ~0;
        [Tooltip("Не считать NPC с NavMeshAgent препятствием для камеры")]
        [SerializeField] private bool ignoreNavMeshAgents = true;
        [SerializeField] private float wallOffset = 0.3f;

        [Header("Anti-Pop")]
        [Tooltip("Гистерезис при выходе из коллизии (сек)")]
        [SerializeField] private float antiPopTime = 0.2f;
        [Tooltip("T-CAM17: не держать замороженную точку, если цель ушла дальше этого (м) — stale-guard")]
        [SerializeField] private float antiPopStaleDistance = 0.35f;

        [Header("Wall Recovery")]
        [Tooltip("Максимальная скорость восстановления позиции (m/s)")]
        [SerializeField] private float recoverySpeed = 10f;
        [Tooltip("Порог срабатывания recovery: отношение actualDist/desiredDist")]
        [SerializeField] private float recoveryRatio = 0.4f;

        [Header("Camera Lag")]
        [Tooltip("Инерция камеры: отставание от цели при движении")]
        [SerializeField] private bool lagEnabled = true;
        [Tooltip("Время отставания по горизонтали XZ (walk)")]
        [SerializeField] private float lagHorizontalTime = 0.15f;
        [Tooltip("Время отставания по вертикали Y (walk)")]
        [SerializeField] private float lagVerticalTime = 0.05f;
        [Tooltip("Меньше отставания при беге + быстрее Vertical при падении")]
        [SerializeField] private bool dynamicLagEnabled = true;
        [Tooltip("T-CAM17: порог входа в быстрый вертикальный режим (м/с)")]
        [SerializeField] private float vertBoostEnterSpeed = 6.5f;
        [Tooltip("T-CAM17: порог выхода из быстрого вертикального режима (м/с)")]
        [SerializeField] private float vertBoostExitSpeed = 3.5f;

        [Header("Adaptive Distance")]
        [Tooltip("T-CAM17: SHRINK (не влезает) / REST (точка покоя у стены) / RECOVER (свободно)")]
        [SerializeField] private bool adaptiveDistanceEnabled = true;
        [Tooltip("Дедбенд покоя: shrink только если цель больше фактической дистанции на это (м)")]
        [SerializeField] private float adaptiveFitDeadband = 0.3f;
        [Tooltip("Устойчивая коллизия перед shrink (сек)")]
        [SerializeField] private float shrinkDelay = 0.2f;
        [Tooltip("Устойчивый просвет перед recovery (сек)")]
        [SerializeField] private float recoverDelay = 0.3f;
        [Tooltip("T-CAM18: перед recovery проверять лучом на полной дистанции — иначе «мячик»")]
        [SerializeField] private bool adaptiveProbeEnabled = true;
        [Tooltip("Скорость уменьшения дистанции")]
        [SerializeField] private float adaptiveSpeed = 3f;
        [Tooltip("Скорость восстановления дистанции")]
        [SerializeField] private float adaptiveRecoverySpeed = 2f;

        [Header("Smoothing")]
        [Tooltip("Anti-jitter сглаживание позиции камеры (быстрое — инерция в Lag)")]
        [SerializeField] private float positionSmoothTime = 0.04f;
        [SerializeField] private float modeSwitchSmoothTime = 0.5f;
        [Tooltip("T-CAM17: окно медленного следования дистанции после смены режима (сек)")]
        [SerializeField] private float modeSwitchWindow = 1f;
        [Tooltip("T-CAM17: быстрое следование дистанции за целью/зумом (сек)")]
        [SerializeField] private float zoomFollowTime = 0.15f;
        [Tooltip("T-CAM17: сглаживание точки взгляда — убивает 1-кадровые щелчки горизонта (сек)")]
        [SerializeField] private float lookSmoothTime = 0.03f;

        [Header("LookAt")]
        [SerializeField] private float lookAtHeightWalk = 1.5f;
        [SerializeField] private float lookAtHeightShip = 4f;

        [Header("Zoom")]
        [Tooltip("Минимальная дистанция камеры (зум колёсиком)")]
        [SerializeField] private float zoomMinDistance = 0.5f;
        [Tooltip("Максимальная дистанция камеры (зум колёсиком)")]
        [SerializeField] private float zoomMaxDistance = 50f;
        [Tooltip("Минимальная дистанция в режиме корабля")]
        [SerializeField] private float zoomMinDistanceShip = 2f;
        [Tooltip("Максимальная дистанция в режиме корабля")]
        [SerializeField] private float zoomMaxDistanceShip = 350f;

        private float _yaw, _pitch;
        private float _currentDistance, _currentHeight, _currentLookAtHeight;
        private float _targetDistance, _targetHeight, _targetLookAtHeight;
        private float _userDistance;
        private bool _isShip;

        private float _distanceVelocity, _heightVelocity, _lookAtVelocity;

        private Vector3 _lagTargetPos;
        private float _lagSpeed;
        private float _shrinkTimer;
        private float _recoverTimer;

        private float _collisionExitTime;
        private bool _wasColliding;
        private Vector3 _lastCollisionPos;
        private Vector3 _collisionLagPos;

        private Vector3 _smoothLookPos;
        private bool _vertBoostActive;
        private float _modeSwitchTimer;

        private InputAction _lookAction;
        private InputAction _zoomAction;
        private Vector2 _lookInput;
        private float _zoomInput;
        private bool _cameraInitialized;
        private Camera _camera;

        private ProjectC.UI.ControlHintsUI _cachedControlHintsUI;
        private Canvas _cachedCanvas;
        private float _cachedMouseSensitivity = 3f;
        private bool _cachedInvertY = false;
        private float _cachedZoomSensitivity = 3f;

        public Camera CameraComponent => _camera;
        public Transform TargetTransform => target;

        // T-FO06Y: read-only runtime evidence surface for camera ownership/history capture.
        public bool CameraInitialized => _cameraInitialized;
        public Vector3 LagTargetPosition => _lagTargetPos;
        public float LagSpeed => _lagSpeed;
        public bool WasColliding => _wasColliding;
        public Vector3 LastCollisionPosition => _lastCollisionPos;
        public float CollisionExitTime => _collisionExitTime;
        public bool IsShipMode => _isShip;

        /// <summary>
        /// Explicit floating-origin camera-history snapshot. This API is dormant until a reviewed
        /// native adapter invokes it; ordinary camera update flow does not call it.
        /// </summary>
        public readonly struct GlobalMotionCameraHistorySnapshot
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector3 LagTargetPosition;
            public readonly float LagSpeed;
            public readonly bool WasColliding;
            public readonly Vector3 LastCollisionPosition;
            public readonly float CollisionExitTime;
            public readonly bool IsShipMode;
            public readonly bool CameraInitialized;
            public readonly ulong CameraInstanceId;
            public readonly ulong TargetInstanceId;
            public readonly bool BillboardBound;

            public GlobalMotionCameraHistorySnapshot(
                Vector3 position,
                Quaternion rotation,
                Vector3 lagTargetPosition,
                float lagSpeed,
                bool wasColliding,
                Vector3 lastCollisionPosition,
                float collisionExitTime,
                bool isShipMode,
                bool cameraInitialized,
                ulong cameraInstanceId,
                ulong targetInstanceId,
                bool billboardBound)
            {
                Position = position;
                Rotation = rotation;
                LagTargetPosition = lagTargetPosition;
                LagSpeed = lagSpeed;
                WasColliding = wasColliding;
                LastCollisionPosition = lastCollisionPosition;
                CollisionExitTime = collisionExitTime;
                IsShipMode = isShipMode;
                CameraInitialized = cameraInitialized;
                CameraInstanceId = cameraInstanceId;
                TargetInstanceId = targetInstanceId;
                BillboardBound = billboardBound;
            }
        }

        public bool IsGlobalMotionCameraReady =>
            isActiveAndEnabled &&
            _cameraInitialized &&
            _camera != null &&
            _camera.enabled &&
            target != null &&
            Billboard.ActiveCamera == transform;

        public bool TryCaptureGlobalMotionCameraHistory(
            out GlobalMotionCameraHistorySnapshot snapshot,
            out string error)
        {
            snapshot = default;
            if (!TryValidateGlobalMotionCameraState(out error)) return false;

            snapshot = new GlobalMotionCameraHistorySnapshot(
                transform.position,
                transform.rotation,
                _lagTargetPos,
                _lagSpeed,
                _wasColliding,
                _lastCollisionPos,
                _collisionExitTime,
                _isShip,
                _cameraInitialized,
                UnityEngine.EntityId.ToULong(_camera.GetEntityId()),
                UnityEngine.EntityId.ToULong(target.GetEntityId()),
                Billboard.ActiveCamera == transform);
            return true;
        }

        public bool TryApplyGlobalMotionCameraTranslation(Vector3 translation, out string error)
        {
            error = null;
            if (!GlobalPosition.IsFiniteValue(translation.x) ||
                !GlobalPosition.IsFiniteValue(translation.y) ||
                !GlobalPosition.IsFiniteValue(translation.z))
            {
                error = "camera_translation_not_finite";
                return false;
            }
            if (!TryValidateGlobalMotionCameraState(out error)) return false;

            transform.position += translation;
            _lagTargetPos += translation;
            // T-FO06DE: безусловно — stale точка при colliding=False только мусорила
            // в evidence-лог (f8_6: collisionPos навсегда в досдвиговых координатах),
            // а потребляется значение лишь в anti-pop окне при _wasColliding.
            _lastCollisionPos += translation;
            // T-CAM17: stale-guard и сглаженный взгляд — тоже мировые точки, едут со сдвигом.
            _collisionLagPos += translation;
            _smoothLookPos += translation;
            return IsFiniteGlobalMotionCameraState(out error);
        }

        public bool TryRestoreGlobalMotionCameraHistory(
            GlobalMotionCameraHistorySnapshot snapshot,
            out string error)
        {
            if (!TryValidateGlobalMotionCameraState(out error)) return false;
            if (UnityEngine.EntityId.ToULong(_camera.GetEntityId()) != snapshot.CameraInstanceId ||
                UnityEngine.EntityId.ToULong(target.GetEntityId()) != snapshot.TargetInstanceId ||
                !snapshot.CameraInitialized ||
                !snapshot.BillboardBound)
            {
                error = "camera_history_identity_mismatch";
                return false;
            }

            transform.SetPositionAndRotation(snapshot.Position, snapshot.Rotation);
            _lagTargetPos = snapshot.LagTargetPosition;
            _lagSpeed = snapshot.LagSpeed;
            _wasColliding = snapshot.WasColliding;
            _lastCollisionPos = snapshot.LastCollisionPosition;
            _collisionExitTime = snapshot.CollisionExitTime;
            _isShip = snapshot.IsShipMode;
            _cameraInitialized = snapshot.CameraInitialized;
            // T-CAM17: структура снапшота не тронута — новые точки консервативно
            // привязываем к текущей цели (stale-guard и взгляд стартуют без рывка).
            _collisionLagPos = _lagTargetPos;
            _smoothLookPos = _lagTargetPos + Vector3.up * _currentLookAtHeight;
            return IsFiniteGlobalMotionCameraState(out error);
        }

        public bool TryValidateGlobalMotionCameraHistory(out string error)
        {
            return TryValidateGlobalMotionCameraState(out error) &&
                IsFiniteGlobalMotionCameraState(out error);
        }

        private bool TryValidateGlobalMotionCameraState(out string error)
        {
            error = null;
            if (!isActiveAndEnabled)
            {
                error = "camera_component_inactive";
                return false;
            }
            if (!_cameraInitialized)
            {
                error = "camera_not_initialized";
                return false;
            }
            if (_camera == null || !_camera.enabled)
            {
                error = "camera_component_not_ready";
                return false;
            }
            if (target == null)
            {
                error = "camera_target_missing";
                return false;
            }
            if (Billboard.ActiveCamera != transform)
            {
                error = "camera_billboard_binding_missing";
                return false;
            }
            return true;
        }

        private bool IsFiniteGlobalMotionCameraState(out string error)
        {
            error = null;
            if (!IsFinite(transform.position) || !IsFinite(transform.rotation) ||
                !IsFinite(_lagTargetPos) || !IsFinite(_lastCollisionPos) ||
                !IsFinite(_collisionLagPos) || !IsFinite(_smoothLookPos) ||
                !GlobalPosition.IsFiniteValue(_lagSpeed) ||
                !GlobalPosition.IsFiniteValue(_collisionExitTime))
            {
                error = "camera_history_state_not_finite";
                return false;
            }
            return true;
        }

        private static bool IsFinite(Vector3 value) =>
            GlobalPosition.IsFiniteValue(value.x) &&
            GlobalPosition.IsFiniteValue(value.y) &&
            GlobalPosition.IsFiniteValue(value.z);

        private static bool IsFinite(Quaternion value) =>
            GlobalPosition.IsFiniteValue(value.x) &&
            GlobalPosition.IsFiniteValue(value.y) &&
            GlobalPosition.IsFiniteValue(value.z) &&
            GlobalPosition.IsFiniteValue(value.w);

        public Vector3 CameraForward
        {
            get
            {
                float r = _yaw * Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(r), 0, Mathf.Cos(r));
            }
        }

        public Vector3 CameraRight
        {
            get
            {
                float r = _yaw * Mathf.Deg2Rad;
                return new Vector3(Mathf.Cos(r), 0, -Mathf.Sin(r));
            }
        }

        public void SetTarget(Transform newTarget)
        {
            if (newTarget != null)
            {
                target = newTarget;
                _lagTargetPos = target.position;
                _smoothLookPos = target.position + Vector3.up * _currentLookAtHeight;
            }
        }

        /// <summary>
        /// Мгновенно привязать камеру к цели (без пружины).
        /// Вызывать после телепорта/респавна/загрузки сохранения.
        /// </summary>
        public void Snap()
        {
            if (target == null) return;
            _lagTargetPos = target.position;
            SnapCameraToPosition();
        }

        public void SetTargetMode(Transform newTarget, bool isShip)
        {
            SetTarget(newTarget);
            SetShipMode(isShip);
        }

        public void SetShipMode(bool isShip)
        {
            _isShip = isShip;
            _userDistance = _targetDistance = isShip ? shipDistance : distance;
            _targetHeight = isShip ? shipHeight : height;
            _targetLookAtHeight = isShip ? lookAtHeightShip : lookAtHeightWalk;
            _modeSwitchTimer = modeSwitchWindow;

            if (target != null)
            {
                _lagTargetPos = target.position;
                _smoothLookPos = target.position + Vector3.up * _targetLookAtHeight;
            }
        }

        public void InitializeCamera()
        {
            if (_cameraInitialized) return;
            if (target == null)
            {
                Debug.LogWarning("[SpringArmCamera] InitializeCamera called before SetTarget!");
                return;
            }

            _yaw = 0f;
            _pitch = 15f;
            _currentDistance = _userDistance = _targetDistance = distance;
            _currentHeight = _targetHeight = height;
            _currentLookAtHeight = _targetLookAtHeight = lookAtHeightWalk;
            _lagTargetPos = target.position;
            _smoothLookPos = target.position + Vector3.up * _currentLookAtHeight;
            _shrinkTimer = 0f;
            _recoverTimer = 0f;

            bool inGame = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            Cursor.lockState = inGame ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !inGame;

            SnapCameraToPosition();
            CreateControlHintsUI();
            _cameraInitialized = true;

            Billboard.ActiveCamera = transform;
            RefreshSettings();
            SettingsManager.OnMouseSensitivityChanged += v => _cachedMouseSensitivity = v;
            SettingsManager.OnInvertYChanged += v => _cachedInvertY = v;
            SettingsManager.OnCameraZoomSensitivityChanged += v => _cachedZoomSensitivity = v;
        }

        private void RefreshSettings()
        {
            _cachedMouseSensitivity = SettingsManager.MouseSensitivity;
            _cachedInvertY = SettingsManager.InvertY;
            _cachedZoomSensitivity = SettingsManager.CameraZoomSensitivity;
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera != null)
            {
                _camera.farClipPlane = ProjectC.World.ViewDistanceApplier.ResolveCameraFar(); // T-LOD01: far из пресета дальности (было 1000000)
                // 0.5f не позволяет физически приблизить камеру к zoomMinDistance=0.5.
                _camera.nearClipPlane = 0.1f;
            }
            _cachedMouseSensitivity = mouseSensitivity;
            _cachedInvertY = invertY;
            _cachedZoomSensitivity = SettingsManager.CameraZoomSensitivity;
            _lookAction = new InputAction("Look", binding: "<Mouse>/delta", expectedControlType: "Vector2");
            _zoomAction = new InputAction("Zoom", binding: "<Mouse>/scroll/y", expectedControlType: "Float");
        }

        private void OnEnable() { _lookAction.Enable(); _zoomAction.Enable(); }
        private void OnDisable() { _lookAction.Disable(); _zoomAction.Disable(); }

        private void OnDestroy()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Start()
        {
            if (!_cameraInitialized && target != null)
                InitializeCamera();
        }

        private void LateUpdate()
        {
            if (target == null || Cursor.lockState != CursorLockMode.Locked)
            {
                ProjectC.World.FloatingOrigin.Network.GlobalMotionRuntimeEvidenceProbe.RecordEvent("camera", "LateUpdate.skip", $"target={(target != null ? target.name : "<none>")} cursor={Cursor.lockState}");
                return;
            }

            ProjectC.World.FloatingOrigin.Network.GlobalMotionRuntimeEvidenceProbe.RecordEvent("camera", "LateUpdate.begin", $"target={target.name} lagTarget={_lagTargetPos} colliding={_wasColliding}");

            // Auto-snap при большом скачке (телепорт, загрузка сохранения, респавн).
            // UpdateLag ловит только >100m; здесь обрабатываем 10-100m.
            float snapDist = Vector3.Distance(_lagTargetPos, target.position);
            if (snapDist > 10f)
            {
                _lagTargetPos = target.position;
                SnapCameraToPosition();
            }

            ReadInput();
            UpdateModeTransition();
            UpdateZoom();

            // Не позволяем орбите уйти ближе пользовательского минимума.
            // Раньше здесь использовалась сумма near-clip + sphereCastRadius + buffer,
            // поэтому zoomMinDistance=0.5 фактически был недостижим.
            float minDist = Mathf.Max(0.1f, GetMinimumCameraDistance());
            float heightDiff = _currentHeight - _currentLookAtHeight;
            float minHorizontalDist = Mathf.Sqrt(Mathf.Max(0f, minDist * minDist - heightDiff * heightDiff));
            _currentDistance = Mathf.Max(_currentDistance, minHorizontalDist);

            UpdateLag();
            Vector3 desiredPos = ComputeDesiredPosition();
            Vector3 resolvedPos = ResolveCollision(desiredPos);
            UpdateAdaptiveDistance();
            SmoothPosition(resolvedPos);
            UpdateLookAt();
            ProjectC.World.FloatingOrigin.Network.GlobalMotionRuntimeEvidenceProbe.RecordEvent("camera", "LateUpdate.end", $"pos={transform.position} lagTarget={_lagTargetPos} colliding={_wasColliding} collisionPos={_lastCollisionPos}");
        }

        private void ReadInput()
        {
            _lookInput = _lookAction.ReadValue<Vector2>();

            // Dead-zone: убиваем шум сенсора (~0.01 magnitude)
            const float deadZone = 0.01f;
            if (_lookInput.sqrMagnitude < deadZone * deadZone)
                return;

            float sens = _cachedMouseSensitivity;
            float inv = _cachedInvertY ? -1f : 1f;
            _yaw += _lookInput.x * sens;
            _pitch -= _lookInput.y * sens * inv;
            _pitch = Mathf.Clamp(_pitch, minVerticalAngle, maxVerticalAngle);
        }

        private void UpdateModeTransition()
        {
            // T-CAM17: дистанция следует быстро (зум/адаптив отзывчивы), медленно —
            // только в окне после смены режима walk↔ship. Высота/взгляд меняются лишь
            // при смене режима — им оставлен медленный переход.
            _modeSwitchTimer = Mathf.Max(0f, _modeSwitchTimer - Time.deltaTime);
            float distTime = _modeSwitchTimer > 0f ? modeSwitchSmoothTime : zoomFollowTime;
            _currentDistance = Mathf.SmoothDamp(_currentDistance, _targetDistance, ref _distanceVelocity, distTime);
            _currentHeight = Mathf.SmoothDamp(_currentHeight, _targetHeight, ref _heightVelocity, modeSwitchSmoothTime);
            _currentLookAtHeight = Mathf.SmoothDamp(_currentLookAtHeight, _targetLookAtHeight, ref _lookAtVelocity, modeSwitchSmoothTime);
        }

        private void UpdateZoom()
        {
            _zoomInput = _zoomAction.ReadValue<float>();

            // Dead-zone: отсекаем шум скролла
            if (Mathf.Abs(_zoomInput) < 0.001f) return;

            // T-CAM19: шаг пропорционален текущей дистанции. Линейный шаг (~1.5м/нотч)
            // точен пешком, но на корабле (2–350м) требует сотни нотчей. Теперь ~15%
            // дистанции/нотч при sens 3: пешком 5м → 0.75м/нотч, корабль 100м → 15м/нотч.
            // Минимум 0.25м — не глохнет у zoomMin. Направление: вверх — ближе.
            float notches = _zoomInput / 120f;
            float sensScale = _cachedZoomSensitivity / 3f;
            float zoomDelta = notches * Mathf.Max(_userDistance * 0.15f, 0.25f) * sensScale;
            float newTarget = _userDistance - zoomDelta;

            float minDist = _isShip ? zoomMinDistanceShip : zoomMinDistance;
            float maxDist = _isShip ? zoomMaxDistanceShip : zoomMaxDistance;
            _userDistance = Mathf.Clamp(newTarget, minDist, maxDist);
        }

        private void UpdateLag()
        {
            if (!lagEnabled || _isShip || target == null)
            {
                _lagTargetPos = target != null ? target.position : Vector3.zero;
                return;
            }

            float initDist = Vector3.Distance(_lagTargetPos, target.position);
            if (initDist > 100f || _lagTargetPos == Vector3.zero)
            {
                _lagTargetPos = target.position;
                return;
            }

            Vector3 delta = target.position - _lagTargetPos;

            float maxLagDist = 10f;
            if (delta.magnitude > maxLagDist)
            {
                delta = delta.normalized * maxLagDist;
                _lagTargetPos = target.position - delta;
            }

            float lagXZ, lagY;
            if (dynamicLagEnabled)
            {
                float rawSpeed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
                rawSpeed = Mathf.Min(rawSpeed, 30f);
                float speedLerp = 1f - Mathf.Exp(-Time.deltaTime / 0.1f);
                _lagSpeed = Mathf.Lerp(_lagSpeed, rawSpeed, speedLerp);
                float speedFactor = Mathf.InverseLerp(0f, 10f, _lagSpeed);
                float dynamicMul = Mathf.Lerp(1f, 0.3f, speedFactor);
                float effXZ = lagHorizontalTime * dynamicMul;
                float effY = lagVerticalTime * dynamicMul;

                // T-CAM17: гистерезис вместо дискретного порога 5 м/с — иначе дребезг
                // вкл/выкл ровно перед приземлением, где уже активны коллизия+adaptive.
                float vertSpeed = Mathf.Abs(delta.y) / Mathf.Max(Time.deltaTime, 0.0001f);
                if (_vertBoostActive)
                {
                    if (vertSpeed < vertBoostExitSpeed)
                        _vertBoostActive = false;
                }
                else if (vertSpeed > vertBoostEnterSpeed)
                {
                    _vertBoostActive = true;
                }
                if (_vertBoostActive)
                    effY *= 0.4f;

                lagXZ = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(effXZ, 0.001f));
                lagY = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(effY, 0.001f));
            }
            else
            {
                lagXZ = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(lagHorizontalTime, 0.001f));
                lagY = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(lagVerticalTime, 0.001f));
            }

            _lagTargetPos.x += delta.x * lagXZ;
            _lagTargetPos.z += delta.z * lagXZ;
            _lagTargetPos.y += delta.y * lagY;
        }

        private Vector3 ComputeDesiredPosition()
        {
            float yr = _yaw * Mathf.Deg2Rad;
            float pr = _pitch * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(-Mathf.Sin(yr) * Mathf.Cos(pr), Mathf.Sin(pr), -Mathf.Cos(yr) * Mathf.Cos(pr));
            return _lagTargetPos + dir * _currentDistance + Vector3.up * _currentHeight;
        }

        private Vector3 ResolveCollision(Vector3 desiredPos)
        {
            Vector3 lookTarget = _lagTargetPos + Vector3.up * _currentLookAtHeight;
            Vector3 from = lookTarget;
            Vector3 dir = (desiredPos - from).normalized;
            float maxDist = Vector3.Distance(from, desiredPos);
            if (maxDist < 0.01f) return desiredPos;

            float currentTime = Time.time;
            float remainingDist = maxDist;
            Vector3 castOrigin = from;

            // T-CAM14: единый near-clip constraint — здесь, а не в SmoothPosition.
            // ResolveCollision — authority по позиции: если resolvedPos ближе minDist,
            // выталкиваем СРАЗУ, чтобы exp-Lerp в SmoothPosition не боролся с push'ем.
            float nearClipMin = Mathf.Max(0.1f, GetMinimumCameraDistance());

            for (int i = 0; i < 8; i++)
            {
                if (!Physics.SphereCast(castOrigin, sphereCastRadius, dir, out RaycastHit hitInfo, remainingDist, collisionMask, QueryTriggerInteraction.Ignore))
                {
                    if (UseAntiPopHold(currentTime))
                        return ClampNearClip(_lastCollisionPos, lookTarget, nearClipMin);
                    _wasColliding = false;
                    return ClampNearClip(desiredPos, lookTarget, nearClipMin);
                }

                if (!ShouldIgnoreCollision(hitInfo.collider))
                {
                    _wasColliding = true;
                    _collisionExitTime = currentTime;
                    _lastCollisionPos = hitInfo.point + hitInfo.normal * (sphereCastRadius + wallOffset);
                    _collisionLagPos = _lagTargetPos;
                    return ClampNearClip(_lastCollisionPos, lookTarget, nearClipMin);
                }

                float distToHit = hitInfo.distance;
                remainingDist -= (distToHit + sphereCastRadius + 0.1f);
                if (remainingDist <= 0f) break;
                castOrigin = castOrigin + dir * (distToHit + sphereCastRadius + 0.1f);
            }

            if (UseAntiPopHold(currentTime))
                return ClampNearClip(_lastCollisionPos, lookTarget, nearClipMin);
            _wasColliding = false;
            return ClampNearClip(desiredPos, lookTarget, nearClipMin);
        }

        /// <summary>
        /// T-CAM17: держать замороженную точку, только если она свежая.
        /// Если лаг-цель уехала (падение, отъезд от стены) — холд протух, иначе камера
        /// стоит в воздухе/у стены, пока игрок уходит, и затем срывается рывком.
        /// </summary>
        private bool UseAntiPopHold(float currentTime)
        {
            if (!_wasColliding || currentTime - _collisionExitTime >= antiPopTime)
                return false;
            return Vector3.Distance(_lagTargetPos, _collisionLagPos) < antiPopStaleDistance;
        }

        private float GetMinimumCameraDistance()
        {
            return _isShip ? zoomMinDistanceShip : zoomMinDistance;
        }

        private bool ShouldIgnoreCollision(Collider collider)
        {
            if (collider == null) return false;

            Transform hitTransform = collider.transform;
            if (hitTransform == target || (target != null && hitTransform.IsChildOf(target)))
                return true;

            // NPC-префабы проекта используют NavMeshAgent + CharacterController.
            // Их тела не должны быть spring-arm препятствием в толпе.
            if (ignoreNavMeshAgents && collider.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null)
                return true;
            // T-CAM17: чужие игроки (CharacterController без NavMeshAgent) — тоже не
            // препятствие: иначе в толпе камера упирается в соседей и дёргается за ними.
            return collider.GetComponentInParent<CharacterController>() != null;
        }

        /// <summary>
        /// T-CAM14: near-clip constraint — единая точка применения.
        /// Если позиция ближе minDist к lookTarget — выталкиваем наружу.
        /// </summary>
        private static Vector3 ClampNearClip(Vector3 pos, Vector3 lookTarget, float minDist)
        {
            float dist = Vector3.Distance(pos, lookTarget);
            if (dist < minDist && dist > 0.001f)
                return lookTarget + (pos - lookTarget).normalized * minDist;
            return pos;
        }

        /// <summary>
        /// T-CAM17: три состояния вместо реле T-CAM16.
        /// SHRINK — дистанция не влезает (упираемся): стягивать после устойчивой коллизии.
        /// REST — у стены, но дистанция влезает: ТОЧКА ПОКОЯ, держать (таймеры в ноль).
        /// RECOVER — свободно: восстанавливать к зуму после устойчивого просвета.
        /// Переключение на условии влезания (с дедбендом), а не на ratio.
        /// T-CAM18: recovery дополнительно gated пробным лучом на _userDistance —
        /// иначе при крутом pitch/дальней стене короткий луч очищается сам и цикл
        /// «мячик» возвращается. С probe у статической геометрии есть fixed point.
        /// </summary>
        private void UpdateAdaptiveDistance()
        {
            if (!adaptiveDistanceEnabled)
            {
                _targetDistance = _userDistance;
                _shrinkTimer = 0f;
                _recoverTimer = 0f;
                return;
            }

            float actualDist = Vector3.Distance(transform.position, _lagTargetPos);

            if (_wasColliding && _targetDistance > actualDist + adaptiveFitDeadband)
            {
                // SHRINK: цель не влезает в доступное место.
                _recoverTimer = 0f;
                _shrinkTimer += Time.deltaTime;
                if (_shrinkTimer >= shrinkDelay)
                {
                    float minDist = Mathf.Max(GetMinimumCameraDistance(), actualDist - wallOffset - sphereCastRadius);
                    float collisionTarget = Mathf.Min(_targetDistance, minDist);
                    _targetDistance = Mathf.Lerp(
                        _targetDistance, collisionTarget,
                        adaptiveSpeed * Time.deltaTime);
                }
            }
            else if (!_wasColliding)
            {
                // RECOVER: свободно — но только после устойчивого просвета, иначе
                // дребезг на тонких препятствиях (столбы, листва) качает камеру.
                // T-CAM18: плюс probe на полной дистанции. При крутом pitch вниз луч
                // сам очищается на малой C (высота перевешивает), и вырастание C снова
                // втыкается в пол — цикл «мячик». То же для дальней стены. Поэтому
                // recovery — только если и на _userDistance препятствий нет.
                _shrinkTimer = 0f;
                if (adaptiveProbeEnabled && IsUserDistanceBlocked())
                {
                    _recoverTimer = 0f;
                }
                else
                {
                    _recoverTimer += Time.deltaTime;
                    if (_recoverTimer >= recoverDelay)
                    {
                        _targetDistance = Mathf.Lerp(
                            _targetDistance, _userDistance,
                            adaptiveRecoverySpeed * Time.deltaTime);
                    }
                }
            }
            else
            {
                // REST: упираемся, но дистанция уже влезает — стоим, это и есть покой.
                _shrinkTimer = 0f;
                _recoverTimer = 0f;
            }
        }

        /// <summary>
        /// T-CAM18: пробный луч на полной пользовательской дистанции вдоль текущей орбиты.
        /// Текущий короткий луч мог очиститься сам (пол при крутом pitch, дальняя стена),
        /// а на _userDistance препятствие всё ещё там — вырастать нельзя, держим T.
        /// Один SphereCast/кадр, персонажи игнорятся как в основном резолве.
        /// </summary>
        private bool IsUserDistanceBlocked()
        {
            if (target == null) return false;
            Vector3 lookTarget = _lagTargetPos + Vector3.up * _currentLookAtHeight;
            float yr = _yaw * Mathf.Deg2Rad;
            float pr = _pitch * Mathf.Deg2Rad;
            Vector3 dirAngles = new Vector3(-Mathf.Sin(yr) * Mathf.Cos(pr), Mathf.Sin(pr), -Mathf.Cos(yr) * Mathf.Cos(pr));
            Vector3 fullDesired = _lagTargetPos + dirAngles * _userDistance + Vector3.up * _currentHeight;
            Vector3 ray = fullDesired - lookTarget;
            float len = ray.magnitude;
            if (len < 0.05f) return false;
            if (Physics.SphereCast(lookTarget, sphereCastRadius, ray / len, out RaycastHit hit, len, collisionMask, QueryTriggerInteraction.Ignore))
                return !ShouldIgnoreCollision(hit.collider);
            return false;
        }

        private void SmoothPosition(Vector3 cameraTargetPos)
        {
            // Dead-zone 3mm: убиваем микро-осцилляции когда почти на месте
            if (Vector3.Distance(transform.position, cameraTargetPos) < 0.003f)
                return;

            float actualDist = Vector3.Distance(cameraTargetPos, _lagTargetPos);
            float desiredDist = _targetDistance;
            float ratio = actualDist / Mathf.Max(desiredDist, 0.1f);

            // Экспоненциальный decay (Lerp) вместо SmoothDamp:
            // SmoothDamp — critically-damped spring, может давать резонанс
            // в каскаде с UpdateLag (тоже exp). Exp+exp гарантированно без осцилляций.
            // T-CAM14: near-clip constraint вынесен в ResolveCollision — здесь
            // только чистый exp-Lerp, без дополнительных push'ей.
            float smoothTime = ratio < recoveryRatio ? positionSmoothTime * 0.3f : positionSmoothTime;
            float t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(smoothTime, 0.001f));
            Vector3 newPos = Vector3.Lerp(transform.position, cameraTargetPos, t);

            // Clamp к recoverySpeed при восстановлении после коллизии
            if (ratio < recoveryRatio)
            {
                float maxStep = recoverySpeed * Time.deltaTime;
                Vector3 step = newPos - transform.position;
                if (step.magnitude > maxStep)
                    newPos = transform.position + step.normalized * maxStep;
            }

            transform.position = newPos;
        }

        private void UpdateLookAt()
        {
            // T-CAM17: точка взгляда сглажена exp-фильтром (~2 кадра при 0.03с).
            // Мгновенный LookAt превращал любую дрожь _lagTargetPos (ступеньки
            // CharacterController, переключение vert-boost, стоп при приземлении)
            // 1:1 в дрожь горизонта — отсюда «тошнота». Позиция и так фильтруется.
            Vector3 lookPoint = _lagTargetPos + Vector3.up * _currentLookAtHeight;
            if (_smoothLookPos == Vector3.zero)
                _smoothLookPos = lookPoint;
            float t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(lookSmoothTime, 0.001f));
            _smoothLookPos = Vector3.Lerp(_smoothLookPos, lookPoint, t);
            transform.LookAt(_smoothLookPos);
        }

        private void SnapCameraToPosition()
        {
            float yr = _yaw * Mathf.Deg2Rad;
            float pr = _pitch * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(-Mathf.Sin(yr) * Mathf.Cos(pr), Mathf.Sin(pr), -Mathf.Cos(yr) * Mathf.Cos(pr));
            _lagTargetPos = target.position;
            _smoothLookPos = target.position + Vector3.up * _currentLookAtHeight;
            transform.position = target.position + dir * _currentDistance + Vector3.up * _currentHeight;
            transform.LookAt(_smoothLookPos);
        }

        private void CreateControlHintsUI()
        {
            if (_cachedControlHintsUI != null) return;
            var existing = FindObjectsByType<ProjectC.UI.ControlHintsUI>(FindObjectsInactive.Include);
            if (existing != null && existing.Length > 0) { _cachedControlHintsUI = existing[0]; return; }

            var hud = ProjectC.UI.HUDManager.EnsureExists();
            _cachedCanvas = hud.GetOrCreateHUDCanvas();
            var (_, _, tmp) = hud.CreateHUDText("ControlHintsText", null, 14, Color.white, TextAlignmentOptions.TopLeft,
                new Vector2(20, -20), new Vector2(300, 300));
            var go = new GameObject("ControlHintsUI");
            go.transform.SetParent(_cachedCanvas.transform);
            _cachedControlHintsUI = go.AddComponent<ProjectC.UI.ControlHintsUI>();
            _cachedControlHintsUI.hintsText = tmp;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (target == null) return;
            float lh = Application.isPlaying ? _currentLookAtHeight : lookAtHeightWalk;
            float d = Application.isPlaying ? _currentDistance : distance;
            float h = Application.isPlaying ? _currentHeight : height;
            float yr = _yaw * Mathf.Deg2Rad, pr = _pitch * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(-Mathf.Sin(yr) * Mathf.Cos(pr), Mathf.Sin(pr), -Mathf.Cos(yr) * Mathf.Cos(pr));
            Vector3 origin = Application.isPlaying ? _lagTargetPos : target.position;
            Vector3 from = origin + Vector3.up * lh;
            Vector3 desired = origin + dir * d + Vector3.up * h;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(from, sphereCastRadius);
            Gizmos.DrawLine(from, desired);

            Gizmos.color = _wasColliding ? Color.red : Color.green;
            Gizmos.DrawWireSphere(transform.position, sphereCastRadius);

            if (Application.isPlaying && lagEnabled && !_isShip)
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawWireSphere(_lagTargetPos, 0.3f);
                Gizmos.DrawLine(target.position, _lagTargetPos);
            }
        }
#endif
    }
}
