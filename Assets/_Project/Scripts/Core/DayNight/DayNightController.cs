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

        [Header("Moon")]
        public MoonController moonController;

        private float _serverTimeOfDay = 12f;
        private int _currentPhaseIndex = -1;
        private float _phaseBlend = 0f;
        private float _daySeed = 0f;
        private float _stormIntensity = 0f;

        private float _smoothTimeOfDay = 12f;
        private float _smoothDayFactor = 0.5f;
        private float _smoothFogDensity = 0.0003f;
        private Color _smoothAmbientSky = Color.gray;
        private Color _smoothFogColor = Color.gray;

        private const float SMOOTH_LERP_SPEED = 2f;

        void Start()
        {
            if (ServerWeatherController.Instance != null)
            {
                ServerWeatherController.Instance.OnTimeOfDayChanged += HandleTimeOfDayChanged;
                ServerWeatherController.Instance.OnTemperatureChanged += HandleTemperatureChanged;
                _serverTimeOfDay = ServerWeatherController.Instance.TimeOfDay;
            }
            else
            {
                Debug.Log("[DayNightController] No ServerWeatherController.Instance - using local time for testing");
            }

            GlobalStormEvents.Subscribe(HandleStormIntensityChanged);

            _smoothTimeOfDay = _serverTimeOfDay;
            _smoothDayFactor = 0.5f;

            UpdateLighting();
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
            if (ServerWeatherController.Instance != null)
            {
                bool timeChanged = Mathf.Abs(_serverTimeOfDay - ServerWeatherController.Instance.TimeOfDay) > 0.01f;
                if (timeChanged)
                {
                    _serverTimeOfDay = ServerWeatherController.Instance.TimeOfDay;
                }

                if (timeChanged)
                {
                    UpdateLighting();
                }
            }
            else
            {
                float gameHoursPerRealSecond = 24f / 3600f;
                _serverTimeOfDay = Mathf.Repeat(_serverTimeOfDay + gameHoursPerRealSecond * Time.deltaTime, 24f);
                UpdateLighting();
            }

            _smoothTimeOfDay = Mathf.Lerp(_smoothTimeOfDay, _serverTimeOfDay, Time.deltaTime * SMOOTH_LERP_SPEED * 2f);

            ApplySkybox();
            ApplyVolumeBlend();
            ApplyAmbient();
            ApplyFog();
            ApplyTemperatureFilter();
        }

        public void SetTimeOfDay(float time)
        {
            _serverTimeOfDay = Mathf.Repeat(time, 24f);
            _smoothTimeOfDay = _serverTimeOfDay;
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

            if (phaseIdx != _currentPhaseIndex)
            {
                _currentPhaseIndex = phaseIdx;
                _daySeed = Mathf.Floor(_serverTimeOfDay / 24f);
            }

            ApplySun();
            ApplyVeilShader();
            ApplyMoon();
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

            float dayProgress = (_smoothTimeOfDay - 6f) / 14f;
            float angle = dayProgress * 180f;

            Quaternion targetRotation = Quaternion.Euler(angle, -30f, 0f);
            sunLight.transform.rotation = Quaternion.Lerp(sunLight.transform.rotation, targetRotation, Time.deltaTime * SMOOTH_LERP_SPEED);

            Color sunColor = ApplyVariability(phase.sunColor, _currentPhaseIndex);
            sunColor = AdjustColorForStorm(sunColor, _stormIntensity);

            sunLight.color = Color.Lerp(sunLight.color, sunColor, Time.deltaTime * SMOOTH_LERP_SPEED);

            float baseIntensity = phase.sunIntensity;
            if (_currentPhaseIndex == 4)
            {
                baseIntensity = 0.15f;
            }
            sunLight.intensity = baseIntensity;
            sunLight.intensity = AdjustIntensityForStorm(sunLight.intensity, _stormIntensity);
            sunLight.shadowStrength = phase.castShadows ? 1f : 0f;
            sunLight.shadows = phase.castShadows ? LightShadows.Soft : LightShadows.None;
        }

        private void ApplyAmbient()
        {
            if (!controlAmbient || profile?.phases == null || _currentPhaseIndex < 0) return;

            var phase = profile.phases[_currentPhaseIndex];
            if (phase == null) return;

            Color targetAmbient = ApplyVariability(phase.ambientSkyColor, _currentPhaseIndex);
            _smoothAmbientSky = Color.Lerp(_smoothAmbientSky, targetAmbient, Time.deltaTime * SMOOTH_LERP_SPEED);

            RenderSettings.ambientSkyColor = _smoothAmbientSky;
            RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientEquatorColor, phase.ambientEquatorColor, Time.deltaTime * SMOOTH_LERP_SPEED);
            RenderSettings.ambientGroundColor = Color.Lerp(RenderSettings.ambientGroundColor, phase.ambientGroundColor, Time.deltaTime * SMOOTH_LERP_SPEED);
            RenderSettings.ambientIntensity = phase.ambientIntensity * (1f - _stormIntensity * 0.4f);
        }

        private void ApplyFog()
        {
            if (!controlFog || profile?.phases == null || _currentPhaseIndex < 0) return;

            var phase = profile.phases[_currentPhaseIndex];
            if (phase == null) return;

            float stormFogMod = 1f + _stormIntensity * 1f;
            float targetFogDensity = phase.fogDensity * stormFogMod;
            _smoothFogDensity = Mathf.Lerp(_smoothFogDensity, targetFogDensity, Time.deltaTime * SMOOTH_LERP_SPEED);

            Color targetFogColor = AdjustColorForStorm(phase.fogColor, _stormIntensity);
            _smoothFogColor = Color.Lerp(_smoothFogColor, targetFogColor, Time.deltaTime * SMOOTH_LERP_SPEED);

            RenderSettings.fogColor = _smoothFogColor;
            RenderSettings.fogDensity = _smoothFogDensity;
            RenderSettings.fogMode = FogMode.Exponential;
        }

        private void ApplySkybox()
        {
            float t = _smoothTimeOfDay;
            Material targetSkybox = (t >= 6f && t < 20f) ? skyboxDayMaterial : skyboxNightMaterial;

            if (targetSkybox == null)
            {
                if (skyboxDayMaterial != null) targetSkybox = skyboxDayMaterial;
                else if (skyboxNightMaterial != null) targetSkybox = skyboxNightMaterial;
            }

            if (targetSkybox != null && RenderSettings.skybox != targetSkybox)
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
            if (temperatureFilter != null && temperatureConfig != null)
            {
                temperatureFilter.Apply(currentTemperature);
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

        private void ApplyMoon()
        {
            if (moonController == null) return;
            float dayNumber = Time.realtimeSinceStartup / 86400f;
            moonController.UpdateMoon(_serverTimeOfDay, dayNumber);
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
            if (t >= 6f && t < 20f)
            {
                return Mathf.InverseLerp(6f, 20f, t);
            }
            else if (t >= 20f)
            {
                return 0f;
            }
            else
            {
                return 1f;
            }
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
            UpdateLighting();
        }
    }
}
