// =====================================================================================
// ShipObservationCamera.cs — камера наблюдения корабля для RepairManagerWindow
// =====================================================================================
// Создаёт собственную Camera. При FlyToShip отключает камеру игрока и включает свою.
// При ReturnToPlayer — возвращает управление камере игрока.
// Поддерживает орбитальное вращение вокруг корабля через Rotate().
// =====================================================================================

using UnityEngine;

namespace ProjectC.Ship.UI
{
    public class ShipObservationCamera : MonoBehaviour
    {
        [Header("Настройки орбиты")]
        [Tooltip("Дистанция от корабля")]
        [SerializeField] private float _distance = 20f;

        [Tooltip("Скорость вращения (градусов в секунду при зажатой стрелке)")]
        [SerializeField] private float _rotateSpeed = 60f;

        [Tooltip("Минимальный вертикальный угол (pitch)")]
        [SerializeField] private float _minPitch = 5f;

        [Tooltip("Максимальный вертикальный угол (pitch)")]
        [SerializeField] private float _maxPitch = 80f;

        // Компоненты
        private Camera _cam;
        private Transform _target;
        private Camera _playerCam;

        // Текущие углы орбиты
        private float _yaw;
        private float _pitch;

        // Публичное состояние
        public bool IsActive => _cam != null && _cam.enabled;

        // ============================================================
        // Lifecycle
        // ============================================================

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null)
                _cam = gameObject.AddComponent<Camera>();

            _cam.enabled = false;
            _cam.clearFlags = CameraClearFlags.Skybox;
            _cam.farClipPlane = 1000000f;
            _cam.nearClipPlane = 0.5f;

            // Аудиолистенер — только один должен быть активен.
            var listener = GetComponent<AudioListener>();
            if (listener == null)
                listener = gameObject.AddComponent<AudioListener>();
            listener.enabled = false;

            // Скрываем флаги, чтобы не мешать
            gameObject.hideFlags = HideFlags.DontSave;
        }

        private void LateUpdate()
        {
            if (_target == null || _cam == null || !_cam.enabled)
                return;

            float yawRad = _yaw * Mathf.Deg2Rad;
            float pitchRad = _pitch * Mathf.Deg2Rad;

            Vector3 dir = new Vector3(
                Mathf.Cos(yawRad) * Mathf.Cos(pitchRad),
                Mathf.Sin(pitchRad),
                Mathf.Sin(yawRad) * Mathf.Cos(pitchRad)
            );

            _cam.transform.position = _target.position + dir * _distance;
            _cam.transform.LookAt(_target.position);
        }

        private void OnDestroy()
        {
            // Безопасность: если уничтожаемся — возвращаем камеру игроку
            if (_playerCam != null)
            {
                _playerCam.enabled = true;
                var playerListener = _playerCam.GetComponent<AudioListener>();
                if (playerListener != null) playerListener.enabled = true;
            }
        }

        // ============================================================
        // Public API
        // ============================================================

        /// <summary>
        /// Переключить камеру на наблюдение корабля.
        /// Отключает камеру игрока, включает свою.
        /// </summary>
        public void FlyToShip(Transform shipTarget, Camera playerCam)
        {
            if (shipTarget == null)
            {
                Debug.LogWarning("[ShipObservationCamera] FlyToShip: target is null");
                return;
            }

            _target = shipTarget;
            _playerCam = playerCam;

            // Начальный угол: сверху-сбоку (pitch ~45°, yaw ~45° от forward корабля)
            _yaw = shipTarget.eulerAngles.y + 45f;
            _pitch = 45f;
            _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);

            // Отключаем камеру игрока
            if (_playerCam != null)
            {
                _playerCam.enabled = false;
                var playerListener = _playerCam.GetComponent<AudioListener>();
                if (playerListener != null) playerListener.enabled = false;
            }

            // Включаем свою камеру
            if (_cam != null)
            {
                _cam.enabled = true;
                var listener = GetComponent<AudioListener>();
                if (listener != null) listener.enabled = true;
            }

            Debug.Log($"[ShipObservationCamera] FlyToShip: target='{shipTarget.name}', yaw={_yaw:F1}, pitch={_pitch:F1}");
        }

        /// <summary>
        /// Вернуть камеру к игроку. Выключает свою камеру, включает камеру игрока.
        /// </summary>
        public void ReturnToPlayer()
        {
            if (_cam != null)
            {
                _cam.enabled = false;
                var listener = GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
            }

            if (_playerCam != null)
            {
                _playerCam.enabled = true;
                var playerListener = _playerCam.GetComponent<AudioListener>();
                if (playerListener != null) playerListener.enabled = true;
            }

            _target = null;
            _playerCam = null;

            Debug.Log("[ShipObservationCamera] ReturnToPlayer");
        }

        /// <summary>
        /// Повернуть камеру вокруг корабля.
        /// </summary>
        /// <param name="yawDelta">Горизонтальное вращение (градусы)</param>
        /// <param name="pitchDelta">Вертикальное вращение (градусы)</param>
        public void Rotate(float yawDelta, float pitchDelta)
        {
            if (!IsActive) return;

            _yaw += yawDelta;
            _pitch += pitchDelta;
            _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);

            // Нормализуем yaw в [0, 360)
            _yaw = _yaw % 360f;
            if (_yaw < 0f) _yaw += 360f;
        }

        /// <summary>
        /// Текущий целевой корабль (null = не активно).
        /// </summary>
        public Transform CurrentTarget => _target;
    }
}
