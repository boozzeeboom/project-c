using UnityEngine;

namespace ProjectC.Core
{
    public class MoonController : MonoBehaviour
    {
        [Header("Moon Settings")]
        public float moonOrbitRadius = 5000f;
        public float moonOrbitSpeed = 1f;

        [Header("Phase")]
        public float moonPhase = 0.5f;

        [Header("References")]
        public Light moonLight;
        public MeshRenderer moonMeshRenderer;
        public Material moonMaterial;

        private const float LUNAR_CYCLE_DAYS = 29.5f;
        private float _serverDayNumber = 0f;
        private const float NIGHT_START_HOUR = 21f;
        private const float NIGHT_END_HOUR = 5f;

        public float MoonPhase => moonPhase;

        public void UpdateMoon(float timeOfDay, float serverDayNumber)
        {
            _serverDayNumber = serverDayNumber;
            moonPhase = Mathf.Repeat(_serverDayNumber / LUNAR_CYCLE_DAYS, 1f);

            bool isNight = timeOfDay >= NIGHT_START_HOUR || timeOfDay < NIGHT_END_HOUR;
            float nightVisibility = CalculateNightVisibility(timeOfDay);

            if (moonLight != null)
            {
                moonLight.enabled = isNight;
                moonLight.intensity = nightVisibility * 0.3f;
            }

            if (moonMeshRenderer != null)
            {
                moonMeshRenderer.enabled = isNight;
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
            float sunAngle = (timeOfDay / 24f) * 360f;
            float moonAngle = sunAngle + 180f;

            float rad = moonAngle * Mathf.Deg2Rad;
            float x = Mathf.Cos(rad) * moonOrbitRadius;
            float y = Mathf.Sin(rad) * moonOrbitRadius * 0.5f;
            float z = Mathf.Sin(rad) * moonOrbitRadius;

            transform.position = new Vector3(x, Mathf.Abs(y), z);
            transform.LookAt(Vector3.zero);
        }

        private void UpdateMoonVisuals()
        {
            if (moonMaterial != null)
            {
                float litFraction = Mathf.Abs(Mathf.Sin(moonPhase * Mathf.PI * 2f));
                moonMaterial.SetFloat("_MoonPhase", moonPhase);
                moonMaterial.SetFloat("_LitFraction", litFraction);
            }
        }

        private float CalculateNightVisibility(float timeOfDay)
        {
            if (timeOfDay >= NIGHT_START_HOUR)
            {
                float elapsed = timeOfDay - NIGHT_START_HOUR;
                return 1f;
            }

            if (timeOfDay < NIGHT_END_HOUR)
            {
                float remaining = NIGHT_END_HOUR - timeOfDay;
                return 1f;
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
