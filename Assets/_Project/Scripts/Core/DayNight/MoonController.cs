using UnityEngine;

namespace ProjectC.Core
{
    public class MoonController : MonoBehaviour
    {
        [Header("Moon Settings")]
        public float moonOrbitRadius = 80000f;
        public float moonOrbitSpeed = 1f;

        [Header("Phase Settings")]
        [Tooltip("How many game days for full lunar cycle")]
        public float lunarCycleGameDays = 29.5f;
        [Tooltip("Current moon age in game days (0-29.5)")]
        public float moonAge = 0f;

        [Header("References")]
        public Light moonLight;
        public MeshRenderer moonMeshRenderer;
        public Material moonMaterial;

        private float _moonOrbitAngle = 0f;
        private const float NIGHT_START_HOUR = 21f;
        private const float NIGHT_END_HOUR = 5f;
        private const float TWILIGHT_DURATION_HOURS = 1.5f;

        public float MoonPhase => moonAge / lunarCycleGameDays;
        public float MoonAge => moonAge;

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

            _moonOrbitAngle = 210f;
            SyncMoonAgeFromGameTime();
        }

        void Update()
        {
            float orbitDegreesPerSecond = 360f / 60f * moonOrbitSpeed;
            _moonOrbitAngle = Mathf.Repeat(_moonOrbitAngle + orbitDegreesPerSecond * Time.deltaTime, 360f);

            SyncMoonAgeFromGameTime();

            bool isNight = IsNightTime();
            float nightVisibility = CalculateNightVisibility();

            if (moonMeshRenderer != null)
            {
                moonMeshRenderer.enabled = isNight;
            }

            if (moonLight != null)
            {
                moonLight.enabled = isNight;
                moonLight.intensity = nightVisibility * 0.4f;
            }

            UpdateMoonPosition();
            UpdateMoonVisuals();
        }

        private void SyncMoonAgeFromGameTime()
        {
            if (ServerWeatherController.Instance != null)
            {
                float gameTimeOfDay = ServerWeatherController.Instance.TimeOfDay;
                float totalDays = ServerWeatherController.Instance.TotalGameDays;

                float daysWithFraction = totalDays + (gameTimeOfDay / 24f);
                moonAge = Mathf.Repeat(daysWithFraction, lunarCycleGameDays);
            }
        }

        public void SetMoonAge(float age)
        {
            moonAge = Mathf.Clamp(age, 0f, lunarCycleGameDays);
            UpdateMoonVisuals();
        }

        private bool IsNightTime()
        {
            float time = GetCurrentTimeOfDay();
            return time >= NIGHT_START_HOUR || time < NIGHT_END_HOUR;
        }

        private float GetCurrentTimeOfDay()
        {
            if (ServerWeatherController.Instance != null)
            {
                return ServerWeatherController.Instance.TimeOfDay;
            }
            return 12f;
        }

        private void UpdateMoonPosition()
        {
            float rad = _moonOrbitAngle * Mathf.Deg2Rad;

            float x = Mathf.Cos(rad) * moonOrbitRadius;
            float z = Mathf.Sin(rad) * moonOrbitRadius;
            float arcHeight = Mathf.Sin(rad) * moonOrbitRadius * 0.8f;
            float y = moonOrbitRadius * 0.1f + arcHeight;

            transform.position = new Vector3(x, y, z);

            float tiltAngle = _moonOrbitAngle - 90f;
            float pitchAngle = -30f + Mathf.Sin(rad) * 40f;
            transform.rotation = Quaternion.Euler(tiltAngle, 0f, pitchAngle);

            if (moonLight != null)
            {
                moonLight.transform.position = transform.position;
                moonLight.transform.rotation = transform.rotation;
            }
        }

        private void UpdateMoonVisuals()
        {
            if (moonMaterial == null || moonMeshRenderer == null) return;

            moonMaterial.SetFloat("_MoonPhase", MoonPhase);
            moonMaterial.SetFloat("_NightVisibility", IsNightTime() ? 1f : 0f);
        }

        private float CalculateNightVisibility()
        {
            float time = GetCurrentTimeOfDay();

            if (time >= NIGHT_START_HOUR && time < NIGHT_START_HOUR + TWILIGHT_DURATION_HOURS)
            {
                float elapsed = time - NIGHT_START_HOUR;
                return Mathf.InverseLerp(0f, TWILIGHT_DURATION_HOURS, elapsed);
            }

            if (time >= NIGHT_END_HOUR - TWILIGHT_DURATION_HOURS && time < NIGHT_END_HOUR)
            {
                float remaining = time - (NIGHT_END_HOUR - TWILIGHT_DURATION_HOURS);
                return 1f - Mathf.InverseLerp(0f, TWILIGHT_DURATION_HOURS, remaining);
            }

            if (time >= NIGHT_END_HOUR && time < NIGHT_START_HOUR)
            {
                return 0f;
            }

            return 1f;
        }
    }
}
