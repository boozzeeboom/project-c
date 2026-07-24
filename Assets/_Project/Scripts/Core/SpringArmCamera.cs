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
    /// Минималистичная версия: старая ThirdPersonCamera + SphereCast коллизии + SmoothDamp.
    /// Без лага, адаптивной дистанции, occlusion, FOV, auto-center.
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
        [SerializeField] private float sphereCastRadius = 0.3f;
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private float wallOffset = 0.2f;

        [Header("Smoothing")]
        [SerializeField] private float positionSmoothTime = 0.05f;
        [SerializeField] private float modeSwitchSmoothTime = 0.5f;

        [Header("LookAt")]
        [SerializeField] private float lookAtHeightWalk = 1.5f;
        [SerializeField] private float lookAtHeightShip = 4f;

        // State
        private float _yaw, _pitch;
        private float _currentDistance, _currentHeight, _currentLookAtHeight;
        private float _targetDistance, _targetHeight, _targetLookAtHeight;
        private bool _isShip;
        private Vector3 _positionVelocity;
        private float _distanceVelocity, _heightVelocity, _lookAtVelocity;

        private InputAction _lookAction;
        private Vector2 _lookInput;
        private bool _cameraInitialized;
        private Camera _camera;

        private ProjectC.UI.ControlHintsUI _cachedControlHintsUI;
        private Canvas _cachedCanvas;
        private float _cachedMouseSensitivity = 3f;
        private bool _cachedInvertY = false;

        // Public API
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
            if (newTarget != null) target = newTarget;
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
        }

        private void RefreshSettings()
        {
            _cachedMouseSensitivity = SettingsManager.MouseSensitivity;
            _cachedInvertY = SettingsManager.InvertY;
        }

        // Lifecycle
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
            if (!_cameraInitialized && target != null)
                InitializeCamera();
        }

        private void LateUpdate()
        {
            if (target == null || Cursor.lockState != CursorLockMode.Locked) return;

            ReadInput();
            UpdateModeTransition();
            Vector3 desiredPos = ComputeDesiredPosition();
            Vector3 resolvedPos = ResolveCollision(desiredPos);
            SmoothPosition(resolvedPos);
            UpdateLookAt();
        }

        // Pipeline
        private void ReadInput()
        {
            _lookInput = _lookAction.ReadValue<Vector2>();
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

        private Vector3 ComputeDesiredPosition()
        {
            float yr = _yaw * Mathf.Deg2Rad;
            float pr = _pitch * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(-Mathf.Sin(yr) * Mathf.Cos(pr), Mathf.Sin(pr), -Mathf.Cos(yr) * Mathf.Cos(pr));
            return target.position + dir * _currentDistance + Vector3.up * _currentHeight;
        }

        private Vector3 ResolveCollision(Vector3 desiredPos)
        {
            Vector3 from = target.position + Vector3.up * _currentLookAtHeight;
            Vector3 dir = (desiredPos - from).normalized;
            float maxDist = Vector3.Distance(from, desiredPos);
            if (maxDist < 0.01f) return desiredPos;

            int mask = collisionMask;
            if (target != null) mask &= ~(1 << target.gameObject.layer);

            if (Physics.SphereCast(from, sphereCastRadius, dir, out RaycastHit hit, maxDist, mask))
                return hit.point + hit.normal * (sphereCastRadius + wallOffset);

            return desiredPos;
        }

        private void SmoothPosition(Vector3 targetPos)
        {
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _positionVelocity, positionSmoothTime);
        }

        private void UpdateLookAt()
        {
            transform.LookAt(target.position + Vector3.up * _currentLookAtHeight);
        }

        private void SnapCameraToPosition()
        {
            float yr = _yaw * Mathf.Deg2Rad;
            float pr = _pitch * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(-Mathf.Sin(yr) * Mathf.Cos(pr), Mathf.Sin(pr), -Mathf.Cos(yr) * Mathf.Cos(pr));
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
            Vector3 from = target.position + Vector3.up * lh;
            Vector3 desired = target.position + dir * d + Vector3.up * h;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(from, sphereCastRadius);
            Gizmos.DrawLine(from, desired);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, sphereCastRadius);
        }
#endif
    }
}
