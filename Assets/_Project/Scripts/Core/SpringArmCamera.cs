using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using ProjectC.UI;

namespace ProjectC.Core
{
    /// <summary>
    /// Spring Arm камера от третьего лица.
    /// Архитектура: независимый корневой объект (НЕ дочерний игроку — FloatingOriginMP).
    /// Pipeline: ReadInput → ModeTransition → CameraLag → ComputeDesired
    ///        → ResolveCollision(chain-cast+AntiPop+nearClip) → AdaptiveDistance
    ///        → SmoothPosition(dead-zone+Recovery) → LookAt
    /// Lag = инерция (walk 0.15s, ship откл). SmoothDamp = anti-jitter (0.04s).
    /// При падении — вертикальный lag ускоряется в 2.5x.
    /// Dead-zone 3mm — убивает микро-осцилляции.
    /// T-CAM14: near-clip constraint вынесен в ResolveCollision (единый источник),
    /// AdaptiveDistance использует _targetDistance вместо базовой дистанции,
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
        [SerializeField] private float wallOffset = 0.3f;

        [Header("Anti-Pop")]
        [Tooltip("Гистерезис при выходе из коллизии (сек)")]
        [SerializeField] private float antiPopTime = 0.2f;

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

        [Header("Adaptive Distance")]
        [Tooltip("Авто-уменьшение дистанции в узких пространствах")]
        [SerializeField] private bool adaptiveDistanceEnabled = true;
        [Tooltip("Порог срабатывания: отношение actualDist/desiredDist")]
        [SerializeField] private float adaptiveThreshold = 0.7f;
        [Tooltip("Задержка перед уменьшением (гистерезис, сек)")]
        [SerializeField] private float adaptiveDelay = 0.5f;
        [Tooltip("Скорость уменьшения дистанции")]
        [SerializeField] private float adaptiveSpeed = 3f;
        [Tooltip("Скорость восстановления дистанции")]
        [SerializeField] private float adaptiveRecoverySpeed = 2f;

        [Header("Smoothing")]
        [Tooltip("Anti-jitter сглаживание позиции камеры (быстрое — инерция в Lag)")]
        [SerializeField] private float positionSmoothTime = 0.04f;
        [SerializeField] private float modeSwitchSmoothTime = 0.5f;

        [Header("LookAt")]
        [SerializeField] private float lookAtHeightWalk = 1.5f;
        [SerializeField] private float lookAtHeightShip = 4f;

        [Header("Zoom")]
        [Tooltip("Минимальная дистанция камеры (зум колёсиком)")]
        [SerializeField] private float zoomMinDistance = 2f;
        [Tooltip("Максимальная дистанция камеры (зум колёсиком)")]
        [SerializeField] private float zoomMaxDistance = 12f;
        [Tooltip("Минимальная дистанция в режиме корабля")]
        [SerializeField] private float zoomMinDistanceShip = 6f;
        [Tooltip("Максимальная дистанция в режиме корабля")]
        [SerializeField] private float zoomMaxDistanceShip = 35f;

        private float _yaw, _pitch;
        private float _currentDistance, _currentHeight, _currentLookAtHeight;
        private float _targetDistance, _targetHeight, _targetLookAtHeight;
        private bool _isShip;

        private float _distanceVelocity, _heightVelocity, _lookAtVelocity;

        private Vector3 _lagTargetPos;
        private float _lastClearTime;

        private float _collisionExitTime;
        private bool _wasColliding;
        private Vector3 _lastCollisionPos;

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
            }
        }

        public void SetTargetMode(Transform newTarget, bool isShip)
        {
            SetTarget(newTarget);
            SetShipMode(isShip);
        }

        public void SetShipMode(bool isShip)
        {
            _isShip = isShip;
            _targetDistance = isShip ? shipDistance : distance;
            _targetHeight = isShip ? shipHeight : height;
            _targetLookAtHeight = isShip ? lookAtHeightShip : lookAtHeightWalk;

            if (target != null)
                _lagTargetPos = target.position;
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
            _currentDistance = _targetDistance = distance;
            _currentHeight = _targetHeight = height;
            _currentLookAtHeight = _targetLookAtHeight = lookAtHeightWalk;
            _lagTargetPos = target.position;
            _lastClearTime = Time.time;

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
                _camera.farClipPlane = 1000000f;
                _camera.nearClipPlane = 0.5f;
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
            if (target == null || Cursor.lockState != CursorLockMode.Locked) return;

            ReadInput();
            UpdateModeTransition();
            UpdateZoom();

            // T-JITTER03: зажимаем _currentDistance чтобы орбита никогда
            // не заходила внутрь near-clip (nearClipPlane + sphereCastRadius + buffer).
            // Без этого ComputeDesiredPosition выдаёт позицию внутри minDist,
            // SmoothPosition выталкивает → push-Lerp-push цикл → осцилляция вблизи.
            float minDist = Mathf.Max(0.1f, _camera.nearClipPlane + sphereCastRadius + 0.2f);
            float heightDiff = _currentHeight - _currentLookAtHeight;
            float minHorizontalDist = Mathf.Sqrt(Mathf.Max(0f, minDist * minDist - heightDiff * heightDiff));
            _currentDistance = Mathf.Max(_currentDistance, minHorizontalDist);

            UpdateLag();
            Vector3 desiredPos = ComputeDesiredPosition();
            Vector3 resolvedPos = ResolveCollision(desiredPos);
            UpdateAdaptiveDistance();
            SmoothPosition(resolvedPos);
            UpdateLookAt();
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
            _currentDistance = Mathf.SmoothDamp(_currentDistance, _targetDistance, ref _distanceVelocity, modeSwitchSmoothTime);
            _currentHeight = Mathf.SmoothDamp(_currentHeight, _targetHeight, ref _heightVelocity, modeSwitchSmoothTime);
            _currentLookAtHeight = Mathf.SmoothDamp(_currentLookAtHeight, _targetLookAtHeight, ref _lookAtVelocity, modeSwitchSmoothTime);
        }

        private void UpdateZoom()
        {
            _zoomInput = _zoomAction.ReadValue<float>();

            // Dead-zone: отсекаем шум скролла
            if (Mathf.Abs(_zoomInput) < 0.001f) return;

            float zoomDelta = _zoomInput * _cachedZoomSensitivity * 0.5f;
            float newTarget = _targetDistance - zoomDelta;

            float minDist = _isShip ? zoomMinDistanceShip : zoomMinDistance;
            float maxDist = _isShip ? zoomMaxDistanceShip : zoomMaxDistance;
            _targetDistance = Mathf.Clamp(newTarget, minDist, maxDist);
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
                float speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
                float speedFactor = Mathf.InverseLerp(0f, 10f, speed);
                float dynamicMul = Mathf.Lerp(1f, 0.3f, speedFactor);
                float effXZ = lagHorizontalTime * dynamicMul;
                float effY = lagVerticalTime * dynamicMul;

                // При быстром падении/взлёте (>5 m/s) — ускоряем вертикальный отклик
                float vertSpeed = Mathf.Abs(delta.y) / Mathf.Max(Time.deltaTime, 0.0001f);
                if (vertSpeed > 5f)
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
            float nearClipMin = Mathf.Max(0.1f, _camera.nearClipPlane + sphereCastRadius + 0.2f);

            for (int i = 0; i < 2; i++)
            {
                if (!Physics.SphereCast(castOrigin, sphereCastRadius, dir, out RaycastHit hitInfo, remainingDist, collisionMask))
                {
                    if (_wasColliding && currentTime - _collisionExitTime < antiPopTime)
                        return ClampNearClip(_lastCollisionPos, lookTarget, nearClipMin);
                    _wasColliding = false;
                    return ClampNearClip(desiredPos, lookTarget, nearClipMin);
                }

                bool hitSelf = hitInfo.transform == target || (target != null && hitInfo.transform.IsChildOf(target));

                if (!hitSelf)
                {
                    _wasColliding = true;
                    _collisionExitTime = currentTime;
                    _lastCollisionPos = hitInfo.point + hitInfo.normal * (sphereCastRadius + wallOffset);
                    return ClampNearClip(_lastCollisionPos, lookTarget, nearClipMin);
                }

                float distToHit = hitInfo.distance;
                remainingDist -= (distToHit + sphereCastRadius + 0.1f);
                if (remainingDist <= 0f) break;
                castOrigin = castOrigin + dir * (distToHit + sphereCastRadius + 0.1f);
            }

            if (_wasColliding && currentTime - _collisionExitTime < antiPopTime)
                return ClampNearClip(_lastCollisionPos, lookTarget, nearClipMin);
            _wasColliding = false;
            return ClampNearClip(desiredPos, lookTarget, nearClipMin);
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

        private void UpdateAdaptiveDistance()
        {
            if (!adaptiveDistanceEnabled) return;

            float actualDist = Vector3.Distance(transform.position, _lagTargetPos);
            // T-CAM14 fix: использовать _targetDistance (текущую цель), а не базовую дистанцию.
            // Иначе при уже уменьшенной дистанции ratio всегда < threshold → восстановление невозможно.
            float desiredDist = _targetDistance;
            float ratio = actualDist / Mathf.Max(desiredDist, 0.1f);
            float currentTime = Time.time;

            if (ratio < adaptiveThreshold && _wasColliding)
            {
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
                float baseDist = _isShip ? shipDistance : distance;
                _targetDistance = Mathf.Lerp(
                    _targetDistance, baseDist,
                    adaptiveRecoverySpeed * Time.deltaTime);

                if (ratio > 0.95f)
                    _lastClearTime = currentTime;
            }
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
            transform.LookAt(_lagTargetPos + Vector3.up * _currentLookAtHeight);
        }

        private void SnapCameraToPosition()
        {
            float yr = _yaw * Mathf.Deg2Rad;
            float pr = _pitch * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(-Mathf.Sin(yr) * Mathf.Cos(pr), Mathf.Sin(pr), -Mathf.Cos(yr) * Mathf.Cos(pr));
            _lagTargetPos = target.position;
            transform.position = target.position + dir * _currentDistance + Vector3.up * _currentHeight;
            transform.LookAt(target.position + Vector3.up * _currentLookAtHeight);
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
