using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// Central wind manager — single source of truth for wind direction/speed.
    /// Receives wind updates from server via ServerWeatherController.
    /// All cloud systems (NearCloudRenderer, DistantCloudManager, StormController) read from here.
    /// </summary>
    public class WindManager : MonoBehaviour
    {
        public static WindManager Instance { get; private set; }

        [Header("Current Wind State")]
        public Vector3 CurrentWindDirection = Vector3.right;
        public float CurrentWindSpeed = 0f;

        [Header("Interpolation")]
        [SerializeField] private float _interpolationSpeed = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool _logWindChanges = true;

        [Header("Влияние на геймплей (множители)")]
        [Tooltip("Глобальный множитель силы ветра, действующей на корабли (ShipController). 1 = как задано на корабле, 0 = ветер не влияет на корабли.")]
        [SerializeField] private float _shipWindMultiplier = 1f;
        [Tooltip("Глобальный множитель сноса ветром для персонажей (NetworkPlayer). 1 = базовый снос, 0 = персонажей ветром не сносит.")]
        [SerializeField] private float _characterWindMultiplier = 1f;

        /// <summary>Глобальный множитель влияния ветра на корабли (настраивается в инспекторе WindManager).</summary>
        public float ShipWindMultiplier => _shipWindMultiplier;
        /// <summary>Глобальный множитель влияния ветра на персонажей (настраивается в инспекторе WindManager).</summary>
        public float CharacterWindMultiplier => _characterWindMultiplier;


        private Vector3 _targetDirection;
        private float _targetSpeed;
        private Vector3 _lastLoggedDirection;
        private float _lastLoggedSpeed;

        public event System.Action<Vector3, float> OnWindUpdated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _targetDirection = CurrentWindDirection;
            _targetSpeed = CurrentWindSpeed;
            _lastLoggedDirection = CurrentWindDirection;
            _lastLoggedSpeed = CurrentWindSpeed;
        }

        /// <summary>
        /// Called by ServerWeatherController via ClientRpc
        /// </summary>
        public void ApplyWindUpdate(Vector3 direction, float speed)
        {
            if (float.IsNaN(speed) || float.IsInfinity(speed))
            {
                Debug.LogWarning("[WindManager] Rejected NaN/Infinity wind speed from server");
                return;
            }

            speed = Mathf.Clamp(speed, 0f, 100f);

            _targetDirection = direction.normalized;
            _targetSpeed = speed;
        }

        private void Update()
        {
            if (!float.IsNaN(_targetSpeed) && !float.IsInfinity(_targetSpeed))
            {
                CurrentWindSpeed = Mathf.Lerp(CurrentWindSpeed, _targetSpeed, _interpolationSpeed * Time.deltaTime);
            }

            if (!float.IsNaN(_targetDirection.x) && !float.IsInfinity(_targetDirection.x))
            {
                CurrentWindDirection = Vector3.Lerp(CurrentWindDirection, _targetDirection, _interpolationSpeed * Time.deltaTime);
            }

            if (CurrentWindSpeed < 1f)
                CurrentWindSpeed = 1f;

            if (_logWindChanges &&
                (CurrentWindDirection != _lastLoggedDirection || Mathf.Abs(CurrentWindSpeed - _lastLoggedSpeed) > 0.5f))
            {
                Debug.Log($"[WindManager] Wind: dir={CurrentWindDirection.normalized}, speed={CurrentWindSpeed:F1}");
                _lastLoggedDirection = CurrentWindDirection;
                _lastLoggedSpeed = CurrentWindSpeed;
            }

            if (OnWindUpdated != null &&
                (CurrentWindDirection != _lastDirBeforeEvent || Mathf.Abs(CurrentWindSpeed - _lastSpeedBeforeEvent) > 0.1f))
            {
                _lastDirBeforeEvent = CurrentWindDirection;
                _lastSpeedBeforeEvent = CurrentWindSpeed;
                OnWindUpdated(CurrentWindDirection, CurrentWindSpeed);
            }
        }

        private Vector3 _lastDirBeforeEvent = Vector3.right;
        private float _lastSpeedBeforeEvent = 0f;
    }
}