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

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                enabled = false;
                return;
            }

            ApplyWindToLocal(_windDirection, _windSpeed);
            Debug.Log("[ServerWeatherController] Server started, will broadcast wind at 0.5 Hz");
        }

        private void Update()
        {
            if (!IsServer) return;

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
    }
}