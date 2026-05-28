using UnityEngine;

namespace ProjectC.Core
{
    public class DayNightController : MonoBehaviour
    {
        [Header("Sun Light")]
        public Light sunLight;

        [Header("Skybox")]
        public Material daySkyboxMaterial;
        public Material nightSkyboxMaterial;

        private const float DAY_START = 6f;
        private const float DAY_END = 20f;
        private float _serverTimeOfDay = 12f;
        private Material _currentSkybox;

        void Start()
        {
            if (ServerWeatherController.Instance != null)
            {
                ServerWeatherController.Instance.OnTimeOfDayChanged += OnServerTimeChanged;
                _serverTimeOfDay = ServerWeatherController.Instance.TimeOfDay;
            }
        }

        void OnDestroy()
        {
            if (ServerWeatherController.Instance != null)
            {
                ServerWeatherController.Instance.OnTimeOfDayChanged -= OnServerTimeChanged;
            }
        }

        private void OnServerTimeChanged(float time)
        {
            _serverTimeOfDay = time;
        }

        void Update()
        {
            // Direct read as backup
            if (ServerWeatherController.Instance != null)
            {
                _serverTimeOfDay = ServerWeatherController.Instance.TimeOfDay;
            }

            UpdateSunOnly();
            UpdateSkybox();
        }

        private void UpdateSunOnly()
        {
            if (sunLight == null) return;

            float t = _serverTimeOfDay;

            // Sun position: 06:00 = East (0°), 20:00 = West (180°)
            float angle = 0f;
            if (t >= 6f && t < 20f)
            {
                float progress = (t - 6f) / 14f;
                angle = progress * 180f;
            }
            else if (t >= 20f)
            {
                angle = 180f;
            }

            sunLight.transform.rotation = Quaternion.Euler(angle, -30f, 0f);

            // Sun intensity
            float intensity = (t >= 6f && t < 20f) ? 1f : 0.05f;
            sunLight.intensity = intensity;

            // Sun color
            sunLight.color = (t >= 6f && t < 20f) ? Color.white : new Color(0.4f, 0.4f, 0.6f);
        }

        private void UpdateSkybox()
        {
            if (daySkyboxMaterial == null || nightSkyboxMaterial == null) return;

            bool isDay = _serverTimeOfDay >= DAY_START && _serverTimeOfDay < DAY_END;
            Material targetSkybox = isDay ? daySkyboxMaterial : nightSkyboxMaterial;

            if (_currentSkybox != targetSkybox)
            {
                _currentSkybox = targetSkybox;
                RenderSettings.skybox = targetSkybox;
            }
        }
    }
}