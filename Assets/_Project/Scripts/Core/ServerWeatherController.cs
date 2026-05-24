using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// Server-authoritative weather controller.
    /// Broadcasts wind updates to all clients at 0.5 Hz (every 2 seconds).
    /// Must be on a NetworkObject with server authority.
    /// </summary>
    public class ServerWeatherController : NetworkBehaviour
    {
        [Header("Wind Settings")]
        [SerializeField] private Vector3 _windDirection = Vector3.right;
        [SerializeField] private float _windSpeed = 0f;

        [Header("Broadcast")]
        [SerializeField] private float _broadcastInterval = 2f;

        [Header("Variation")]
        [SerializeField] private bool _enableWindVariation = true;
        [SerializeField] private float _directionVariationAngle = 15f;
        [SerializeField] private float _speedVariationPercent = 0.2f;

        private float _timer = 0f;

        public static ServerWeatherController Instance { get; private set; }

        [Header("Time of Day")]
        [SerializeField] private float _timeOfDay = 12f;
        [SerializeField] private float _dayCycleRealHours = 1f;
        [SerializeField] private bool _enableTimeAutoAdvance = true;
        [SerializeField] private float _timeBroadcastInterval = 5f;
        private float _timeTimer = 0f;

        [Header("Temperature")]
        [SerializeField] private float _temperature = 20f;
        [SerializeField] private float _tempBroadcastInterval = 10f;
        private float _tempTimer = 0f;

        // Events for clients to subscribe
        public event System.Action<float> OnTimeOfDayChanged;
        public event System.Action<float> OnTemperatureChanged;

        public float TimeOfDay => _timeOfDay;
        public float Temperature => _temperature;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Instance = this;
            }

            if (!IsServer)
            {
                enabled = false;
                return;
            }

            ApplyWindToLocal(_windDirection, _windSpeed);
            BroadcastTimeOfDayClientRpc(_timeOfDay);
            BroadcastTemperatureClientRpc(_temperature);
            Debug.Log("[ServerWeatherController] Server started, will broadcast wind at 0.5 Hz");
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (!IsServer) return;

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                return;
            }

            _timer += Time.deltaTime;
            if (_timer >= _broadcastInterval)
            {
                BroadcastWindClientRpc(_windDirection, _windSpeed);
                _timer = 0f;
            }

            if (_enableWindVariation)
            {
                ApplyWindVariation();
            }

            if (_enableTimeAutoAdvance && IsServer)
            {
                float gameHoursPerRealSecond = 24f / (_dayCycleRealHours * 3600f);
                _timeOfDay += gameHoursPerRealSecond * Time.deltaTime;
                if (_timeOfDay >= 24f) _timeOfDay -= 24f;
            }

            _timeTimer += Time.deltaTime;
            if (_timeTimer >= _timeBroadcastInterval)
            {
                BroadcastTimeOfDayClientRpc(_timeOfDay);
                _timeTimer = 0f;
            }

            _tempTimer += Time.deltaTime;
            if (_tempTimer >= _tempBroadcastInterval)
            {
                BroadcastTemperatureClientRpc(_temperature);
                _tempTimer = 0f;
            }
        }

        private void ApplyWindToLocal(Vector3 direction, float speed)
        {
            if (WindManager.Instance != null)
            {
                WindManager.Instance.ApplyWindUpdate(direction, speed);
            }
            else
            {
                Debug.LogError("[ServerWeatherController] WindManager.Instance is NULL on server! Check script execution order.");
            }
        }

        private void ApplyWindVariation()
        {
            float angleOffset = Mathf.Sin(Time.time * 0.1f) * _directionVariationAngle;
            Quaternion rot = Quaternion.Euler(0, angleOffset, 0);
            _windDirection = (rot * _windDirection).normalized;

            float speedMod = 1f + Mathf.Sin(Time.time * 0.15f) * _speedVariationPercent;
            float newSpeed = _windSpeed * speedMod;

            _windSpeed = Mathf.Clamp(newSpeed, 1f, 100f);

            ApplyWindToLocal(_windDirection, _windSpeed);
        }

        [ClientRpc]
        private void BroadcastWindClientRpc(Vector3 direction, float speed)
        {
            if (WindManager.Instance != null)
            {
                WindManager.Instance.ApplyWindUpdate(direction, speed);
            }
            else
            {
                Debug.LogWarning("[ServerWeatherController] WindManager.Instance is null on client");
            }
        }

        /// <summary>
        /// Called by server-side systems to change wind
        /// </summary>
        public void SetWind(Vector3 direction, float speed)
        {
            if (!IsServer) return;
            _windDirection = direction.normalized;
            _windSpeed = Mathf.Max(0f, speed);
            ApplyWindToLocal(_windDirection, _windSpeed);
        }

        /// <summary>
        /// Change wind over time (for weather transitions)
        /// </summary>
        public void TransitionWind(Vector3 targetDirection, float targetSpeed, float duration)
        {
            if (!IsServer) return;
            StartCoroutine(TransitionWindCoroutine(targetDirection, targetSpeed, duration));
        }

        private System.Collections.IEnumerator TransitionWindCoroutine(Vector3 targetDirection, float targetSpeed, float duration)
        {
            Vector3 startDir = _windDirection;
            float startSpeed = _windSpeed;
            float elapsed = 0f;

            targetDirection = targetDirection.normalized;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                _windDirection = Vector3.Lerp(startDir, targetDirection, smoothT).normalized;
                _windSpeed = Mathf.Lerp(startSpeed, targetSpeed, smoothT);

                ApplyWindToLocal(_windDirection, _windSpeed);

                yield return null;
            }

            _windDirection = targetDirection.normalized;
            _windSpeed = Mathf.Max(0f, targetSpeed);
            ApplyWindToLocal(_windDirection, _windSpeed);
        }

        [ClientRpc]
        private void BroadcastTimeOfDayClientRpc(float time)
        {
            _timeOfDay = time;
            OnTimeOfDayChanged?.Invoke(time);
        }

        [ClientRpc]
        private void BroadcastTemperatureClientRpc(float temp)
        {
            _temperature = temp;
            OnTemperatureChanged?.Invoke(temp);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetTimeOfDayServerRpc(float time)
        {
            _timeOfDay = Mathf.Repeat(time, 24f);
            BroadcastTimeOfDayClientRpc(_timeOfDay);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetTemperatureServerRpc(float temp)
        {
            _temperature = temp;
            BroadcastTemperatureClientRpc(_temperature);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetDayCycleSpeedServerRpc(float realHoursForFullCycle)
        {
            _dayCycleRealHours = realHoursForFullCycle;
        }
    }
}