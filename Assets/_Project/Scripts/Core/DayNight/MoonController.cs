using UnityEngine;

namespace ProjectC.Core
{
    public class MoonController : MonoBehaviour
    {
        [Header("Moon Settings")]
        public float moonOrbitRadius = 800f;
        public float moonOrbitSpeed = 1f;

        [Header("Phase")]
        public float moonPhase = 0.5f;

        [Header("References")]
        public Light moonLight;
        public MeshRenderer moonMeshRenderer;
        public Material moonMaterial;

        private const float LUNAR_CYCLE_DAYS = 29.5f;
        private float _serverDayNumber = 0f;
        private float _localTimeOfDay = 12f;
        private const float NIGHT_START_HOUR = 21f;
        private const float NIGHT_END_HOUR = 5f;
        private const float TWILIGHT_DURATION_HOURS = 1.5f;

        public float MoonPhase => moonPhase;

        void Start()
        {
            if (moonMeshRenderer != null && moonMaterial != null)
            {
                moonMeshRenderer.material = moonMaterial;
            }

            if (moonLight != null)
            {
                moonLight.type = LightType.Directional;
            }

            if (ServerWeatherController.Instance != null)
            {
                _localTimeOfDay = ServerWeatherController.Instance.TimeOfDay;
            }

            UpdateMoon(_localTimeOfDay, Time.realtimeSinceStartup / 86400f);
        }

        void Update()
        {
            float dayNumber = Time.realtimeSinceStartup / 86400f;

            if (ServerWeatherController.Instance != null)
            {
                _localTimeOfDay = ServerWeatherController.Instance.TimeOfDay;
            }
            else
            {
                float gameHoursPerRealSecond = 24f / 3600f;
                _localTimeOfDay = Mathf.Repeat(_localTimeOfDay + gameHoursPerRealSecond * Time.deltaTime, 24f);
            }

            if (moonMeshRenderer != null)
            {
                bool isNight = _localTimeOfDay >= NIGHT_START_HOUR || _localTimeOfDay < NIGHT_END_HOUR;
                moonMeshRenderer.enabled = isNight;
            }

            UpdateMoon(_localTimeOfDay, dayNumber);
        }

        public void UpdateMoon(float timeOfDay, float serverDayNumber)
        {
            _serverDayNumber = serverDayNumber;
            _localTimeOfDay = timeOfDay;
            moonPhase = Mathf.Repeat(timeOfDay / 24f + serverDayNumber, 1f);

            bool isNight = timeOfDay >= NIGHT_START_HOUR || timeOfDay < NIGHT_END_HOUR;
            float nightVisibility = CalculateNightVisibility(timeOfDay);

            if (moonLight != null)
            {
                moonLight.enabled = isNight;
                moonLight.intensity = nightVisibility * 0.4f;
            }

            if (moonMeshRenderer != null)
            {
                moonMeshRenderer.enabled = isNight;
                moonMeshRenderer.material = moonMaterial;
            }

            UpdateMoonPosition(timeOfDay);
            UpdateMoonVisuals();
        }

        public void SetMoonPhase(float phase)
        {
            moonPhase = Mathf.Clamp01(phase);
            UpdateMoonVisuals();
        }

        private void UpdateMoonPosition(float timeOfDay)
        {
            float sunAngle = ((timeOfDay - 6f) / 24f) * 360f;
            float moonAngle = sunAngle + 180f;

            float rad = moonAngle * Mathf.Deg2Rad;
            float x = Mathf.Cos(rad) * moonOrbitRadius;
            float y = Mathf.Sin(rad) * moonOrbitRadius * 0.3f;
            float z = Mathf.Sin(rad) * moonOrbitRadius;

            Vector3 cameraPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            transform.position = cameraPos + new Vector3(x, y + 200f, z);
            transform.LookAt(cameraPos);
        }

        private void UpdateMoonVisuals()
        {
            if (moonMaterial != null && moonMeshRenderer != null)
            {
                moonMeshRenderer.material = moonMaterial;
                float litFraction = Mathf.Abs(Mathf.Sin(moonPhase * Mathf.PI * 2f));
                moonMaterial.SetFloat("_MoonPhase", moonPhase);
                moonMaterial.SetFloat("_LitFraction", litFraction);
            }
        }

        private float CalculateNightVisibility(float timeOfDay)
        {
            if (timeOfDay >= NIGHT_START_HOUR && timeOfDay < NIGHT_START_HOUR + TWILIGHT_DURATION_HOURS)
            {
                float elapsed = timeOfDay - NIGHT_START_HOUR;
                return Mathf.InverseLerp(0f, TWILIGHT_DURATION_HOURS, elapsed);
            }

            if (timeOfDay >= NIGHT_END_HOUR - TWILIGHT_DURATION_HOURS && timeOfDay < NIGHT_END_HOUR)
            {
                float remaining = timeOfDay - (NIGHT_END_HOUR - TWILIGHT_DURATION_HOURS);
                return 1f - Mathf.InverseLerp(0f, TWILIGHT_DURATION_HOURS, remaining);
            }

            if (timeOfDay >= NIGHT_END_HOUR && timeOfDay < NIGHT_START_HOUR)
            {
                return 0f;
            }

            return 1f;
        }

        public void SetVisibility(float visibility)
        {
            visibility = Mathf.Clamp01(visibility);

            if (moonLight != null)
            {
                moonLight.intensity = visibility * 0.3f;
            }

            if (moonMeshRenderer != null)
            {
                Color c = moonMeshRenderer.material.color;
                c.a = visibility;
                moonMeshRenderer.material.color = c;
            }
        }
    }
}
