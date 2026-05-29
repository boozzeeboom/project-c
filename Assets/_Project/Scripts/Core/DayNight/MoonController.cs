using UnityEngine;

namespace ProjectC.Core
{
    public class MoonController : MonoBehaviour
    {
    [Header("Moon Settings")]
    public float moonOrbitRadius = 80000f;
    [Tooltip("1.0 = same speed as sun. >1 = slower, <1 = faster")]
    public float moonOrbitSpeed = 1f;

        [Header("Visibility")]
        [Tooltip("If true, moon is always visible regardless of time")]
        public bool alwaysVisible = false;

        [Header("Phase Settings")]
        [Tooltip("How many game days for full lunar cycle")]
        public float lunarCycleGameDays = 29.5f;
        [Tooltip("Moon phase visual rotation offset in degrees")]
        public float moonPhaseRotation = 90f;
        [Tooltip("Self rotation axis (normalized) - controls which side of moon faces Earth")]
        public Vector3 selfRotationAxis = Vector3.up;
        [Tooltip("Self rotation speed multiplier - tune to match phase to real lunar cycle")]
        [Range(0.1f, 3f)]
        public float selfRotationSpeed = 1f;
        [Tooltip("Current moon age in game days (0-29.5)")]
        public float moonAge = 0f;

        [Header("References")]
        public Light moonLight;
        public MeshRenderer moonMeshRenderer;
        public Material moonMaterial;

        private float _moonOrbitAngle = 0f;
        private float _moonSelfRotation = 0f;
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
            // Synced with ServerWeatherController._dayCycleRealHours
            // Moon orbits at same angular speed as sun (moonOrbitSpeed = 1.0 default)
            // Sun: 360° per game day
            // Moon: 360° * moonOrbitSpeed per game day
            float dayCycleRealHours = GetDayCycleRealHours();
            float realSecondsPerGameDay = dayCycleRealHours * 3600f;
            float orbitDegreesPerSecond = (360f / realSecondsPerGameDay) * moonOrbitSpeed;

            float orbitDelta = orbitDegreesPerSecond * Time.deltaTime;
            _moonOrbitAngle = Mathf.Repeat(_moonOrbitAngle + orbitDelta, 360f);

            // Moon is tidally locked - same face always toward Earth
            // As it orbits, it must rotate around its own axis to compensate
            float selfRotationDelta = orbitDelta * selfRotationSpeed;
            _moonSelfRotation = Mathf.Repeat(_moonSelfRotation + selfRotationDelta, 360f);

        SyncMoonAgeFromGameTime();

            bool isNight = IsNightTime();
            float nightVisibility = CalculateNightVisibility();
            bool showMoon = alwaysVisible || isNight;

            if (moonMeshRenderer != null)
            {
                moonMeshRenderer.enabled = showMoon;
            }

            if (moonLight != null)
            {
                moonLight.enabled = showMoon;
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

        private float GetDayCycleRealHours()
        {
            if (ServerWeatherController.Instance != null)
            {
                return ServerWeatherController.Instance.DayCycleRealHours;
            }
            return 1f; // Default fallback
        }

        private void UpdateMoonPosition()
        {
            float rad = _moonOrbitAngle * Mathf.Deg2Rad;

            float x = Mathf.Cos(rad) * moonOrbitRadius;
            float z = Mathf.Sin(rad) * moonOrbitRadius;
            float arcHeight = Mathf.Sin(rad) * moonOrbitRadius * 0.8f;
            float y = moonOrbitRadius * 0.1f + arcHeight;

            transform.position = new Vector3(x, y, z);

            // Tidal locking: moon rotates to keep same face toward Earth
            // Apply self-rotation on top of orbital orientation
            float tiltAngle = _moonOrbitAngle - 90f;
            float pitchAngle = -30f + Mathf.Sin(rad) * 40f;
            Quaternion orbitalRotation = Quaternion.Euler(tiltAngle, 0f, pitchAngle);
            Quaternion selfRotation = Quaternion.AngleAxis(_moonSelfRotation, selfRotationAxis.normalized);
            transform.rotation = orbitalRotation * selfRotation;

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
            moonMaterial.SetFloat("_PhaseRotation", moonPhaseRotation);
            moonMaterial.SetFloat("_NightVisibility", alwaysVisible || IsNightTime() ? 1f : 0f);
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
