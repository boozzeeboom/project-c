using UnityEngine;
using UnityEngine.Rendering;
using ProjectC.World.Clouds;

namespace ProjectC.Core
{
    public class DayNightController : MonoBehaviour
    {
        [Header("Profile")]
        public DayNightProfile profile;

        [Header("Sun")]
        public Light sunLight;

        [Header("Ambient")]
        public bool controlAmbient = true;

        [Header("Fog")]
        public bool controlFog = true;

        [Header("Volume")]
        public Volume globalVolume;
        public VolumeProfile dayVolumeProfile;
        public VolumeProfile nightVolumeProfile;

        [Header("Temperature Filter")]
        public TemperatureFilterConfig temperatureConfig;
        public TemperatureFilter temperatureFilter;
        public float currentTemperature = 20f;

        [Header("Skybox")]
        public Material skyboxDayMaterial;
        public Material skyboxNightMaterial;

        [Header("Veil Shader Integration")]
        public bool updateVeilShader = true;
        public VeilRaymarchMeshController veilController;

        private float _serverTimeOfDay = 12f;
        private int _currentPhaseIndex = -1;
        private float _phaseBlend = 0f;
        private float _daySeed = 0f;
        private float _stormIntensity = 0f;

        void Start()
        {
            if (ServerWeatherController.Instance != null)
            {
                ServerWeatherController.Instance.OnTimeOfDayChanged += HandleTimeOfDayChanged;
                ServerWeatherController.Instance.OnTemperatureChanged += HandleTemperatureChanged;
            }

            GlobalStormEvents.Subscribe(HandleStormIntensityChanged);

            ApplyAll();
        }

        void OnDestroy()
        {
            if (ServerWeatherController.Instance != null)
            {
                ServerWeatherController.Instance.OnTimeOfDayChanged -= HandleTimeOfDayChanged;
                ServerWeatherController.Instance.OnTemperatureChanged -= HandleTemperatureChanged;
            }

            GlobalStormEvents.Unsubscribe(HandleStormIntensityChanged);
        }

        private void HandleStormIntensityChanged(float intensity)
        {
            SetStormIntensity(intensity);
        }

        void Update()
        {
            if (ServerWeatherController.Instance != null && _serverTimeOfDay != ServerWeatherController.Instance.TimeOfDay)
            {
                _serverTimeOfDay = ServerWeatherController.Instance.TimeOfDay;
                UpdateLighting();
            }
        }

        public void SetTimeOfDay(float time)
        {
            _serverTimeOfDay = Mathf.Repeat(time, 24f);
            UpdateLighting();
        }

        public void SetTemperature(float temp)
        {
            currentTemperature = temp;
        }

        public void SetStormIntensity(float intensity)
        {
            _stormIntensity = Mathf.Clamp01(intensity);
        }

        private void HandleTimeOfDayChanged(float time)
        {
            _serverTimeOfDay = time;
            UpdateLighting();
        }

        private void HandleTemperatureChanged(float temp)
        {
            currentTemperature = temp;
        }

        private void UpdateLighting()
        {
            if (profile == null || profile.phases == null || profile.phases.Length == 0) return;

            float time = _serverTimeOfDay;
            int phaseIdx = GetCurrentPhaseIndex(time);
            float blend = GetPhaseBlend(time, phaseIdx);

            if (phaseIdx != _currentPhaseIndex)
            {
                _currentPhaseIndex = phaseIdx;
                _daySeed = Mathf.Floor(_serverTimeOfDay / 24f);
            }

            _phaseBlend = blend;

            ApplySun();
            ApplyAmbient();
            ApplyFog();
            ApplySkybox();
            ApplyVolumeBlend();
            ApplyTemperatureFilter();
            ApplyVeilShader();
        }

        private int GetCurrentPhaseIndex(float time)
        {
            if (profile?.phases == null) return 0;

            for (int i = 0; i < profile.phases.Length; i++)
            {
                var phase = profile.phases[i];
                if (phase == null) continue;

                // Night phase (index 4) wraps from 21:00 to 5:00
                if (i == 4)
                {
                    if (time >= phase.startHour || time < phase.endHour) return i;
                    continue;
                }

                if (time >= phase.startHour && time < phase.endHour) return i;
            }
            return 0;
        }

        private float GetPhaseBlend(float time, int phaseIdx)
        {
            if (profile?.phases == null || phaseIdx < 0 || phaseIdx >= profile.phases.Length) return 0f;
            var phase = profile.phases[phaseIdx];
            if (phase == null) return 0f;

            float duration = phase.endHour - phase.startHour;
            if (duration <= 0f) duration += 24f;

            float elapsed = time - phase.startHour;
            if (elapsed < 0f) elapsed += 24f;

            float t = Mathf.Clamp01(elapsed / duration);
            return phase.transitionCurve.Evaluate(t);
        }

        private void ApplySun()
        {
            if (sunLight == null || profile?.phases == null || _currentPhaseIndex < 0) return;

            var phase = profile.phases[_currentPhaseIndex];
            if (phase == null) return;

            // Rotate sun around X axis: 360° over 24h, offset so midnight is at -90°
            float angle = (_serverTimeOfDay / 24f) * 360f - 90f;
            sunLight.transform.rotation = Quaternion.Euler(angle, -30f, 0f);

            Color sunColor = ApplyVariability(phase.sunColor, _currentPhaseIndex);
            sunColor = AdjustColorForStorm(sunColor, _stormIntensity);

            sunLight.color = sunColor;
            sunLight.intensity = phase.sunIntensity * (phase.sunIntensity * 0.5f + 0.5f);
            sunLight.intensity = AdjustIntensityForStorm(sunLight.intensity, _stormIntensity);
            sunLight.shadowStrength = 1f;
            sunLight.shadows = phase.castShadows ? LightShadows.Soft : LightShadows.None;
        }

        private void ApplyAmbient()
        {
            if (!controlAmbient || profile?.phases == null || _currentPhaseIndex < 0) return;

            var phase = profile.phases[_currentPhaseIndex];
            if (phase == null) return;

            RenderSettings.ambientSkyColor = ApplyVariability(phase.ambientSkyColor, _currentPhaseIndex);
            RenderSettings.ambientEquatorColor = phase.ambientEquatorColor;
            RenderSettings.ambientGroundColor = phase.ambientGroundColor;
            RenderSettings.ambientIntensity = phase.ambientIntensity * (1f - _stormIntensity * 0.4f);
        }

        private void ApplyFog()
        {
            if (!controlFog || profile?.phases == null || _currentPhaseIndex < 0) return;

            var phase = profile.phases[_currentPhaseIndex];
            if (phase == null) return;

            float stormFogMod = 1f + _stormIntensity * 1f; // ×2.0 at full storm
            RenderSettings.fogColor = phase.fogColor;
            RenderSettings.fogDensity = phase.fogDensity * stormFogMod;
            RenderSettings.fogMode = FogMode.Exponential;
        }

        private void ApplySkybox()
        {
            float dayFactor = GetDayFactor();
            Material targetSkybox = dayFactor > 0.5f ? skyboxDayMaterial : skyboxNightMaterial;
            if (targetSkybox != null)
            {
                RenderSettings.skybox = targetSkybox;
            }
        }

        private void ApplyVolumeBlend()
        {
            if (globalVolume == null) return;

            float weight = GetDayFactor();

            if (globalVolume.profile == null || globalVolume.profile != dayVolumeProfile)
            {
                globalVolume.profile = dayVolumeProfile;
            }

            globalVolume.weight = weight;
        }

        private void ApplyTemperatureFilter()
        {
            if (temperatureFilter != null && temperatureFilter.config != null)
            {
                // TemperatureFilter handles its own overlay blending internally
                // The currentTemperature value is already updated via HandleTemperatureChanged
                // This method exists for future expansion (e.g., driving post-processing
                // or material parameters from temperature)
            }
        }

        private void ApplyVeilShader()
        {
            if (!updateVeilShader || veilController == null) return;

            float dayFactor = GetDayFactor();
            Vector3 sunDir = sunLight != null ? -sunLight.transform.forward : Vector3.down;

            veilController.SetLightDirection(sunDir);
            veilController.SetDayNightFactor(dayFactor);
        }

        private Color ApplyVariability(Color baseColor, int phaseIdx)
        {
            if (profile?.phases == null || phaseIdx < 0 || phaseIdx >= profile.phases.Length) return baseColor;

            var phase = profile.phases[phaseIdx];
            if (phase == null) return baseColor;

            // Seeded deterministic randomness per day per phase
            int seed = Mathf.FloorToInt(_daySeed * 100 + phaseIdx);
            UnityEngine.Random.InitState(seed);

            float hueShift = UnityEngine.Random.Range(phase.hueShiftRange.x, phase.hueShiftRange.y);
            float satVar = UnityEngine.Random.Range(phase.saturationRange.x, phase.saturationRange.y);
            float valVar = UnityEngine.Random.Range(phase.intensityRange.x, phase.intensityRange.y);

            // Restore global randomness state
            UnityEngine.Random.InitState((int)System.DateTimeOffset.Now.Ticks);

            Color.RGBToHSV(baseColor, out float h, out float s, out float v);
            h = (h + hueShift + 1f) % 1f;
            s = Mathf.Clamp01(s * satVar);
            v = Mathf.Clamp01(v * valVar);

            return Color.HSVToRGB(h, s, v);
        }

        private float GetDayFactor()
        {
            float t = _serverTimeOfDay;
            if (t >= 6f && t < 20f) return Mathf.InverseLerp(6f, 20f, t);
            if (t >= 20f) return 0f;
            return 1f;
        }

        private Color AdjustColorForStorm(Color color, float storm)
        {
            float h, s, v;
            Color.RGBToHSV(color, out h, out s, out v);
            s = Mathf.Lerp(s, s * 0.7f, storm);
            v = Mathf.Lerp(v, v * 0.6f, storm);
            return Color.HSVToRGB(h, s, v);
        }

        private float AdjustIntensityForStorm(float intensity, float storm)
        {
            return intensity * Mathf.Lerp(1f, 0.5f, storm);
        }

        private void ApplyAll()
        {
            ApplySun();
            ApplyAmbient();
            ApplyFog();
            ApplySkybox();
            ApplyVolumeBlend();
            ApplyTemperatureFilter();
            ApplyVeilShader();
        }
    }
}
