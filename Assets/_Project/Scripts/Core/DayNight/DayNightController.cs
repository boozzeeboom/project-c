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

        // SERVER TIME - authoritative source
        private float _serverTimeOfDay = 12f;

        // Phase tracking
        private int _currentPhaseIndex = -1;
        private float _daySeed = 0f;
        private float _stormIntensity = 0f;

        // Smoothing
        private const float SMOOTH_SPEED = 3f;
        private float _smoothSunIntensity = 1f;
        private Color _smoothAmbientSky = Color.gray;
        private Color _smoothFogColor = Color.gray;

        // Skybox transition smoothing
        private Material _currentSkyboxMaterial = null;
        private float _skyboxBlend = 0f; // 0 = night, 1 = day

        // Unified time boundaries (used by ALL systems)
        private const float DAY_START = 6f;
        private const float DAY_END = 20f;

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
                Debug.Log("[DayNightController] No ServerWeatherController.Instance - using local time");
            }

            GlobalStormEvents.Subscribe(HandleStormIntensityChanged);
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
            // Sync with server time
            if (ServerWeatherController.Instance != null)
            {
                float newTime = ServerWeatherController.Instance.TimeOfDay;
                if (Mathf.Abs(_serverTimeOfDay - newTime) > 0.01f)
                {
                    _serverTimeOfDay = newTime;
                    UpdateLighting();
                }
            }
            else
            {
                float gameHoursPerRealSecond = 24f / 3600f;
                _serverTimeOfDay = Mathf.Repeat(_serverTimeOfDay + gameHoursPerRealSecond * Time.deltaTime, 24f);
                UpdateLighting();
            }

            ApplySkyboxBlended();
            ApplyVolumeBlend();
            ApplyAmbient();
            ApplyFog();
            ApplyTemperatureFilter();
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

            int phaseIdx = GetCurrentPhaseIndex(_serverTimeOfDay);

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

        private void ApplySun()
        {
            if (sunLight == null || profile?.phases == null || _currentPhaseIndex < 0) return;

            var phase = profile.phases[_currentPhaseIndex];
            if (phase == null) return;

            // SUN POSITION: 06:00 = East, 20:00 = West
            // In Unity: 0° = East, 90° = South, 180° = West, 270° = North
            float t = _serverTimeOfDay;
            float angle;

            if (t >= DAY_START && t < DAY_END)
            {
                // Day: 06:00 to 20:00 - sun moves from East to West
                float dayProgress = (t - DAY_START) / (DAY_END - DAY_START); // 0 to 1
                angle = dayProgress * 180f; // 0° (East) to 180° (West)
            }
            else if (t >= DAY_END)
            {
                // Evening: 20:00 to 24:00 - continue to western horizon
                float eveningProgress = (t - DAY_END) / (24f - DAY_END);
                angle = 180f + eveningProgress * 90f; // 180° to 270°
            }
            else
            {
                // Night/Early morning: 00:00 to 06:00 - below horizon
                float nightProgress = t / DAY_START;
                angle = 270f + nightProgress * 90f; // 270° to 360° (0°)
            }

            Quaternion targetRotation = Quaternion.Euler(angle, -30f, 0f);
            sunLight.transform.rotation = Quaternion.Slerp(sunLight.transform.rotation, targetRotation, Time.deltaTime * SMOOTH_SPEED);

            // SUN COLOR
            Color targetSunColor = ApplyVariability(phase.sunColor, _currentPhaseIndex);
            targetSunColor = AdjustColorForStorm(targetSunColor, _stormIntensity);
            sunLight.color = Color.Lerp(sunLight.color, targetSunColor, Time.deltaTime * SMOOTH_SPEED);

            // SUN INTENSITY
            float baseIntensity = phase.sunIntensity;
            if (_currentPhaseIndex == 4) baseIntensity = 0.15f;

            _smoothSunIntensity = Mathf.Lerp(_smoothSunIntensity, baseIntensity, Time.deltaTime * SMOOTH_SPEED);
            _smoothSunIntensity = AdjustIntensityForStorm(_smoothSunIntensity, _stormIntensity);
            sunLight.intensity = _smoothSunIntensity;

            sunLight.shadowStrength = phase.castShadows ? 1f : 0f;
            sunLight.shadows = phase.castShadows ? LightShadows.Soft : LightShadows.None;
        }

        private void ApplyAmbient()
        {
            if (!controlAmbient || profile?.phases == null || _currentPhaseIndex < 0) return;

            var phase = profile.phases[_currentPhaseIndex];
            if (phase == null) return;

            Color targetAmbient = ApplyVariability(phase.ambientSkyColor, _currentPhaseIndex);
            _smoothAmbientSky = Color.Lerp(_smoothAmbientSky, targetAmbient, Time.deltaTime * SMOOTH_SPEED);

            RenderSettings.ambientSkyColor = _smoothAmbientSky;
            RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientEquatorColor, phase.ambientEquatorColor, Time.deltaTime * SMOOTH_SPEED);
            RenderSettings.ambientGroundColor = Color.Lerp(RenderSettings.ambientGroundColor, phase.ambientGroundColor, Time.deltaTime * SMOOTH_SPEED);
            RenderSettings.ambientIntensity = phase.ambientIntensity * (1f - _stormIntensity * 0.4f);
        }

        private void ApplyFog()
        {
            if (!controlFog || profile?.phases == null || _currentPhaseIndex < 0) return;

            var phase = profile.phases[_currentPhaseIndex];
            if (phase == null) return;

            float stormFogMod = 1f + _stormIntensity * 1f;
            float targetFogDensity = phase.fogDensity * stormFogMod;

            Color targetFogColor = AdjustColorForStorm(phase.fogColor, _stormIntensity);
            _smoothFogColor = Color.Lerp(_smoothFogColor, targetFogColor, Time.deltaTime * SMOOTH_SPEED);

            RenderSettings.fogColor = _smoothFogColor;
            RenderSettings.fogDensity = targetFogDensity;
            RenderSettings.fogMode = FogMode.Exponential;
        }

        // ========================================================================
        // SKYBOX WITH SMOOTH BLEND TRANSITION
        // Day: 06:00-20:00, Night: 20:00-06:00
        // ========================================================================
        private void ApplySkyboxBlended()
        {
            float t = _serverTimeOfDay;

            // Calculate target blend: 0 = night, 1 = day
            float targetBlend;
            if (t >= DAY_START && t < DAY_END)
            {
                targetBlend = 1f;
            }
            else
            {
                targetBlend = 0f;
            }

            // Smooth transition over 2 seconds
            _skyboxBlend = Mathf.Lerp(_skyboxBlend, targetBlend, Time.deltaTime * 0.5f);

            // Determine which material to use based on blend
            Material targetMaterial;
            if (_skyboxBlend > 0.5f)
            {
                targetMaterial = skyboxDayMaterial;
            }
            else
            {
                targetMaterial = skyboxNightMaterial;
            }

            // Only switch when fully transitioned (hysteresis)
            if (targetMaterial != null && RenderSettings.skybox != targetMaterial)
            {
                if (Mathf.Abs(_skyboxBlend - 0f) < 0.05f || Mathf.Abs(_skyboxBlend - 1f) < 0.05f)
                {
                    RenderSettings.skybox = targetMaterial;
                    _currentSkyboxMaterial = targetMaterial;
                }
            }
        }

        // ========================================================================
        // VOLUME BLEND - matches skybox boundaries exactly
        // ========================================================================
        private void ApplyVolumeBlend()
        {
            if (globalVolume == null) return;

            float t = _serverTimeOfDay;
            bool isNight = t >= DAY_END || t < DAY_START;
            VolumeProfile targetProfile = isNight ? nightVolumeProfile : dayVolumeProfile;

            if (globalVolume.profile != targetProfile)
            {
                globalVolume.profile = targetProfile;
            }

            float targetWeight = isNight ? 1f : GetDayFactor();
            globalVolume.weight = Mathf.Lerp(globalVolume.weight, targetWeight, Time.deltaTime * SMOOTH_SPEED);
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
        }

        private Color ApplyVariability(Color baseColor, int phaseIdx)
        {
            if (profile?.phases == null || phaseIdx < 0 || phaseIdx >= profile.phases.Length) return baseColor;

            var phase = profile.phases[phaseIdx];
            if (phase == null) return baseColor;

            int seed = Mathf.FloorToInt(_daySeed * 100 + phaseIdx);
            UnityEngine.Random.InitState(seed);

            float hueShift = UnityEngine.Random.Range(phase.hueShiftRange.x, phase.hueShiftRange.y);
            float satVar = UnityEngine.Random.Range(phase.saturationRange.x, phase.saturationRange.y);
            float valVar = UnityEngine.Random.Range(phase.intensityRange.x, phase.intensityRange.y);

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
            if (t >= DAY_START && t < DAY_END)
            {
                return Mathf.InverseLerp(DAY_START, DAY_END, t);
            }
            else if (t >= DAY_END)
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