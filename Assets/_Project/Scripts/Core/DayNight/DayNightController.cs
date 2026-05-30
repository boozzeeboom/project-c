using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectC.Core
{
    /// <summary>
    /// Client-side controller for the day-night cycle system.
    /// Synchronizes with ServerWeatherController to apply time-of-day phases.
    /// Controls sun, skybox, lighting, fog, and post-processing based on server time.
    /// </summary>
    public class DayNightController : MonoBehaviour
    {
        [Header("Profile")]
        [Tooltip("DayNightProfile with phase definitions")]
        public DayNightProfile profile;

        [Header("References")]
        public Light sunLight;

        [Header("Skybox")]
        public Material daySkyboxMaterial;
        public Material nightSkyboxMaterial;

        [Header("Temperature")]
        [Tooltip("Reference to TemperatureFilter component")]
        public TemperatureFilter temperatureFilter;
        public float currentTemperature = 20f;

        [Header("URP Volume")]
        public Volume globalVolume;
        public VolumeProfile dayVolumeProfile;
        public VolumeProfile nightVolumeProfile;
        public VolumeProfile twilightVolumeProfile;

        [Header("Debug")]
        public bool showDebugOverlay = true;

        // Cached state - no allocations in Update
        private float _serverTimeOfDay = 12f;
        private TimeOfDayPhase _currentPhase;
        private float _currentPhaseBlend = 0f;
        private TimeOfDayPhase _previousPhase;
        private float _phaseTransitionTime = 0f;

        // Smooth blending between phases
        private float _blendProgress = 1f;
        private float _targetBlendProgress = 1f;
        private const float DEFAULT_BLEND_SPEED = 2f; // 2 seconds to blend between phases

        // Pre-allocated color/value containers for hot-path optimization
        private readonly Color _clearColor = new Color(0f, 0f, 0f, 0f);
        private float _dayFactor = 0.5f;
        private float _starVisibility = 0f;
        private Material _currentSkybox;
        
        // Smoothed color values
        private Color _smoothedSunColor = Color.white;
        private Color _smoothedFogColor = Color.gray;
        private Color _smoothedAmbientSky = new Color(0.2f, 0.2f, 0.3f);
        private float _smoothedSunIntensity = 1f;
        private float _smoothedAmbientIntensity = 0.5f;

        // ColorGrading component for temperature filter
        private ColorAdjustments _colorAdjustments;
        private Vignette _vignette;
        private Bloom _bloom;

        // Dedicated temperature volume (CRITICAL: ensures temperature always works!)
        private Volume _temperatureVolume;
        private ColorAdjustments _temperatureColorAdjustments;

        // Runtime copies of volume profiles (CRITICAL: prevents external modifications!)
        // Original assets should never be modified at runtime
        private VolumeProfile _dayVolumeProfileInstance;
        private VolumeProfile _nightVolumeProfileInstance;
        private VolumeProfile _twilightVolumeProfileInstance;
        private VolumeProfile _currentProfileInstance;

        // Events for other systems to subscribe
        public event System.Action<TimeOfDayPhase, float> OnPhaseChanged;
        public event System.Action<float> OnDayFactorChanged;
        public event System.Action<float> OnTemperatureChanged;

        public float ServerTimeOfDay => _serverTimeOfDay;
        public float DayFactor => _dayFactor;
        public TimeOfDayPhase CurrentPhase => _currentPhase;
        public float CurrentTemperature => currentTemperature;

        void Start()
        {
            Initialize();
        }

        void OnEnable()
        {
            SubscribeToServerEvents();
            ValidateProfileInstances();
        }

        private void ValidateProfileInstances()
        {
            if (_dayVolumeProfileInstance == null || _nightVolumeProfileInstance == null || _twilightVolumeProfileInstance == null)
            {
                Debug.LogWarning("[DayNightController] Profile instances were lost (possible domain reload). Re-initializing...");
                InitializeVolumeProfileInstances();
            }
        }

        void OnDisable()
        {
            UnsubscribeFromServerEvents();
        }

        void OnDestroy()
        {
            UnsubscribeFromServerEvents();
        }

        private void Initialize()
        {
            // Initialize temperature volume if filter is attached
            if (temperatureFilter != null)
            {
                temperatureFilter.InitializeTemperatureVolume();
            }

            // Create runtime copies of volume profiles (CRITICAL: prevents external modifications!)
            InitializeVolumeProfileInstances();

            // Initialize volume components
            InitializeVolumeComponents();

            // Load initial server time
            if (ServerWeatherController.Instance != null)
            {
                _serverTimeOfDay = ServerWeatherController.Instance.TimeOfDay;
                currentTemperature = ServerWeatherController.Instance.Temperature;
            }

            // Apply initial state
            UpdateDayNight();
        }

        /// <summary>
        /// Create runtime instances of volume profiles to prevent modification of original assets.
        /// CRITICAL: Volume profiles that are assigned to Volume components get serialized as references.
        /// When play mode starts, Unity may serialize/deserialize these references in ways that reset values.
        /// Using runtime instances prevents any external code from modifying the original assets.
        /// </summary>
        private void InitializeVolumeProfileInstances()
        {
            _dayVolumeProfileInstance = dayVolumeProfile != null ? Instantiate(dayVolumeProfile) : null;
            _nightVolumeProfileInstance = nightVolumeProfile != null ? Instantiate(nightVolumeProfile) : null;
            _twilightVolumeProfileInstance = twilightVolumeProfile != null ? Instantiate(twilightVolumeProfile) : null;

            Debug.Log($"[DayNightController] Created runtime profile instances: Day={_dayVolumeProfileInstance != null}, Night={_nightVolumeProfileInstance != null}, Twilight={_twilightVolumeProfileInstance != null}");
        }

        private void InitializeVolumeComponents()
        {
            if (globalVolume != null && globalVolume.profile != null)
            {
                globalVolume.profile.TryGet(out _colorAdjustments);
                globalVolume.profile.TryGet(out _vignette);
                globalVolume.profile.TryGet(out _bloom);
            }

            // Initialize dedicated temperature volume (CRITICAL for temperature effects!)
            InitializeTemperatureVolume();
        }

        /// <summary>
        /// Initialize a dedicated volume for temperature effects.
        /// This ensures temperature ALWAYS works regardless of VolumeProfile contents.
        /// IMPORTANT: Use a SEPARATE Volume component to avoid conflicts with globalVolume!
        /// </summary>
        private void InitializeTemperatureVolume()
        {
            // CRITICAL: Create a SEPARATE child object for temperature volume
            // This avoids conflicts with globalVolume on the same GameObject
            Transform child = transform.Find("TemperatureVolume");
            if (child == null)
            {
                GameObject tempGO = new GameObject("TemperatureVolume");
                tempGO.transform.SetParent(transform);
                tempGO.transform.localPosition = Vector3.zero;
                tempGO.transform.localRotation = Quaternion.identity;
                _temperatureVolume = tempGO.AddComponent<Volume>();
            }
            else
            {
                _temperatureVolume = child.GetComponent<Volume>();
                if (_temperatureVolume == null)
                {
                    _temperatureVolume = child.gameObject.AddComponent<Volume>();
                }
            }

            _temperatureVolume.priority = 200; // Higher than global volume (50)
            _temperatureVolume.isGlobal = true;
            _temperatureVolume.weight = 1f;

            // CRITICAL: Always create a NEW profile instance to prevent external modifications
            // and ensure we're using our own managed profile
            var tempProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _temperatureVolume.profile = tempProfile;

            // Add ColorAdjustments to temperature profile
            if (!tempProfile.TryGet<ColorAdjustments>(out _temperatureColorAdjustments))
            {
                _temperatureColorAdjustments = tempProfile.Add<ColorAdjustments>(true);
            }

            // Initialize with neutral values
            _temperatureColorAdjustments.colorFilter.Override(Color.white);
            _temperatureColorAdjustments.saturation.Override(0f);
            _temperatureColorAdjustments.postExposure.Override(0f);
            _temperatureColorAdjustments.contrast.Override(0f);

            Debug.Log("[DayNightController] Temperature volume initialized (separate child object)");
        }

        private void SubscribeToServerEvents()
        {
            if (ServerWeatherController.Instance != null)
            {
                ServerWeatherController.Instance.OnTimeOfDayChanged += OnServerTimeChanged;
                ServerWeatherController.Instance.OnTemperatureChanged += OnServerTemperatureChanged;
            }
        }

        private void UnsubscribeFromServerEvents()
        {
            if (ServerWeatherController.Instance != null)
            {
                ServerWeatherController.Instance.OnTimeOfDayChanged -= OnServerTimeChanged;
                ServerWeatherController.Instance.OnTemperatureChanged -= OnServerTemperatureChanged;
            }
        }

        private void OnServerTimeChanged(float time)
        {
            _serverTimeOfDay = time;
            UpdateDayNight();
        }

        private void OnServerTemperatureChanged(float temp)
        {
            currentTemperature = temp;
            OnTemperatureChanged?.Invoke(temp);
            ApplyTemperatureFilter(temp);
        }

        void Update()
        {
            // Poll as backup (in case event didn't fire)
            if (ServerWeatherController.Instance != null)
            {
                _serverTimeOfDay = ServerWeatherController.Instance.TimeOfDay;
            }

            // Update transition time
            if (_phaseTransitionTime > 0f)
            {
                _phaseTransitionTime -= Time.deltaTime;
            }

            // Update smooth blend progress (lerp toward target)
            _blendProgress = Mathf.Lerp(_blendProgress, _targetBlendProgress, Time.deltaTime * DEFAULT_BLEND_SPEED);

            UpdateDayNight();
        }

        private void UpdateDayNight()
        {
            if (profile == null) return;

            // Get current phase from profile
            TimeOfDayPhase newPhase = profile.GetPhaseForHour(_serverTimeOfDay);

            // Check for phase change
            if (newPhase != _currentPhase)
            {
                _previousPhase = _currentPhase;
                _currentPhase = newPhase;
                _phaseTransitionTime = _currentPhase?.blendDuration ?? 0.5f;
                OnPhaseChanged?.Invoke(_currentPhase, _phaseTransitionTime);
            }

            if (_currentPhase == null) return;

            // Calculate blend factor between phases
            _currentPhaseBlend = profile.GetBlendFactor(_serverTimeOfDay);
            _targetBlendProgress = _currentPhaseBlend;

            // Update day factor for shader
            float newDayFactor = profile.GetDayFactor(_serverTimeOfDay);
            if (!Mathf.Approximately(newDayFactor, _dayFactor))
            {
                _dayFactor = newDayFactor;
                OnDayFactorChanged?.Invoke(_dayFactor);
            }

            // Calculate star visibility
            _starVisibility = CalculateStarVisibility();

            // Apply all lighting components with smooth interpolation
            ApplySunLightingSmooth();
            ApplyAmbientLightingSmooth();
            ApplySkybox();
            ApplyFogSmooth();
            ApplyVolumeBlend();
            ApplyTemperatureFilter(currentTemperature);
            UpdateExternalControllers();
        }

        private void ApplySunLighting()
        {
            if (sunLight == null || _currentPhase == null) return;

            // Calculate sun position based on time
            float t = _serverTimeOfDay;
            float angle = CalculateSunAngle(t);
            Vector3 rotation = new Vector3(angle, -30f, 0f) + _currentPhase.sunRotationOffset;

            sunLight.transform.rotation = Quaternion.Euler(rotation);

            // Apply phase-specific sun settings with variability
            Color sunColor = ApplyVariability(_currentPhase.sunColor);
            float sunIntensity = _currentPhase.sunIntensity * ApplyVariabilityIntensity();

            sunLight.color = sunColor;
            sunLight.intensity = sunIntensity;
            sunLight.shadows = _currentPhase.castShadows ? LightShadows.Soft : LightShadows.None;

            // Enable/disable based on time
            bool isNightTime = t >= 21f || t < 5f;
            sunLight.enabled = _currentPhase.phaseName.Contains("Night") ? false : true;
        }

        private float CalculateSunAngle(float hour)
        {
            // Sun rises at 6:00, peaks at 12:00, sets at 20:00
            if (hour >= 6f && hour < 20f)
            {
                float progress = (hour - 6f) / 14f;
                return progress * 180f;
            }
            else if (hour >= 20f)
            {
                return 180f;
            }
            return 0f;
        }

        private void ApplyAmbientLighting()
        {
            if (!_currentPhase.fogEnabled) return;

            Color skyColor = ApplyVariability(_currentPhase.ambientSkyColor);
            RenderSettings.ambientSkyColor = skyColor;
            RenderSettings.ambientEquatorColor = ApplyVariability(_currentPhase.ambientEquatorColor);
            RenderSettings.ambientGroundColor = ApplyVariability(_currentPhase.ambientGroundColor);
            RenderSettings.ambientIntensity = _currentPhase.ambientIntensity * ApplyVariabilityIntensity();
        }

        private void ApplySkybox()
        {
            Material targetSkybox = null;

            // Check if phase has custom skybox
            if (_currentPhase.skyboxMaterial != null)
            {
                targetSkybox = _currentPhase.skyboxMaterial;
            }
            else
            {
                // Use profile day/night materials
                bool isNight = _serverTimeOfDay >= 21f || _serverTimeOfDay < 6f;
                targetSkybox = isNight ? nightSkyboxMaterial : daySkyboxMaterial;
            }

            if (targetSkybox != null && _currentSkybox != targetSkybox)
            {
                _currentSkybox = targetSkybox;
                RenderSettings.skybox = targetSkybox;
            }

            // Apply exposure tint
            if (targetSkybox != null)
            {
                targetSkybox.SetFloat("_Exposure", _currentPhase.skyboxExposure);
                if (_currentPhase.skyboxTint != Color.clear)
                {
                    targetSkybox.SetColor("_Tint", _currentPhase.skyboxTint);
                }
            }
        }

        private void ApplyFog()
        {
            if (profile != null && !profile.enableFog)
            {
                RenderSettings.fog = false;
                return;
            }

            RenderSettings.fog = _currentPhase.fogEnabled;
            if (_currentPhase.fogEnabled)
            {
                RenderSettings.fogColor = ApplyVariability(_currentPhase.fogColor);
                RenderSettings.fogDensity = _currentPhase.fogDensity;
                RenderSettings.fogMode = FogMode.Exponential;
            }
        }

        // ========================
        // Smooth Interpolation Methods
        // ========================

        /// <summary>
        /// Apply sun lighting with smooth interpolation between phases.
        /// </summary>
        private void ApplySunLightingSmooth()
        {
            if (sunLight == null || _currentPhase == null) return;

            // Calculate sun position based on time
            float t = _serverTimeOfDay;
            float angle = CalculateSunAngle(t);
            Vector3 rotation = new Vector3(angle, -30f, 0f) + _currentPhase.sunRotationOffset;
            sunLight.transform.rotation = Quaternion.Euler(rotation);

            // Get target values with variability
            Color targetSunColor = ApplyVariability(_currentPhase.sunColor);
            float targetSunIntensity = _currentPhase.sunIntensity * ApplyVariabilityIntensity();

            // Smooth interpolation
            float smoothFactor = GetSmoothFactor();
            _smoothedSunColor = Color.Lerp(_smoothedSunColor, targetSunColor, smoothFactor * Time.deltaTime * 3f);
            _smoothedSunIntensity = Mathf.Lerp(_smoothedSunIntensity, targetSunIntensity, smoothFactor * Time.deltaTime * 3f);

            // Apply to light
            sunLight.color = _smoothedSunColor;
            sunLight.intensity = _smoothedSunIntensity;
            sunLight.shadows = _currentPhase.castShadows ? LightShadows.Soft : LightShadows.None;

            // Enable/disable based on time (snap, not smooth)
            sunLight.enabled = !_currentPhase.phaseName.Contains("Night");
        }

        /// <summary>
        /// Apply ambient lighting with smooth interpolation between phases.
        /// </summary>
        private void ApplyAmbientLightingSmooth()
        {
            if (_currentPhase == null) return;

            // Get target values
            Color targetSky = ApplyVariability(_currentPhase.ambientSkyColor);
            Color targetEquator = ApplyVariability(_currentPhase.ambientEquatorColor);
            Color targetGround = ApplyVariability(_currentPhase.ambientGroundColor);
            float targetIntensity = _currentPhase.ambientIntensity * ApplyVariabilityIntensity();

            // Smooth interpolation
            float smoothFactor = GetSmoothFactor();
            _smoothedAmbientSky = Color.Lerp(_smoothedAmbientSky, targetSky, smoothFactor * Time.deltaTime * 2f);
            _smoothedAmbientIntensity = Mathf.Lerp(_smoothedAmbientIntensity, targetIntensity, smoothFactor * Time.deltaTime * 2f);

            // Apply to render settings
            RenderSettings.ambientSkyColor = _smoothedAmbientSky;
            RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientEquatorColor, targetEquator, smoothFactor * Time.deltaTime * 2f);
            RenderSettings.ambientGroundColor = Color.Lerp(RenderSettings.ambientGroundColor, targetGround, smoothFactor * Time.deltaTime * 2f);
            RenderSettings.ambientIntensity = _smoothedAmbientIntensity;
        }

        /// <summary>
        /// Apply fog with smooth interpolation between phases.
        /// </summary>
        private void ApplyFogSmooth()
        {
            if (profile != null && !profile.enableFog)
            {
                RenderSettings.fog = false;
                return;
            }

            bool shouldEnableFog = _currentPhase.fogEnabled;
            RenderSettings.fog = shouldEnableFog;

            if (shouldEnableFog)
            {
                Color targetFogColor = ApplyVariability(_currentPhase.fogColor);
                float targetFogDensity = _currentPhase.fogDensity;

                // Smooth interpolation for fog
                float smoothFactor = GetSmoothFactor();
                _smoothedFogColor = Color.Lerp(_smoothedFogColor, targetFogColor, smoothFactor * Time.deltaTime * 2f);

                // Apply
                RenderSettings.fogColor = _smoothedFogColor;
                RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, targetFogDensity, smoothFactor * Time.deltaTime * 2f);
                RenderSettings.fogMode = FogMode.Exponential;
            }
        }

        /// <summary>
        /// Get smooth factor based on blend progress. Returns 0-1.
        /// Higher values mean faster smoothing (at start of transition).
        /// </summary>
        private float GetSmoothFactor()
        {
            // Use blend progress: 1 = fully transitioned, 0 = just started
            // Invert so we smooth faster at the start
            return Mathf.Lerp(1f, 0.3f, _blendProgress);
        }

        private void ApplyVolumeBlend()
        {
            if (globalVolume == null)
            {
                Debug.LogWarning("[VolumeBlend] globalVolume is NULL!");
                return;
            }

            // Determine target volume profile based on time (using runtime instances!)
            VolumeProfile targetProfile = null;
            string profileName = "NONE";

            float t = _serverTimeOfDay;

            // Determine which profile to use based on time (using runtime copies)
            if (t >= 5f && t < 19.5f)
            {
                // Day phases (Morning, Midday, Evening)
                targetProfile = _dayVolumeProfileInstance;
                profileName = _dayVolumeProfileInstance != null ? "Day" : "NULL";
            }
            else if (t >= 19.5f && t < 21f || t >= 5f && t < 6f)
            {
                // Twilight
                targetProfile = _twilightVolumeProfileInstance ?? _nightVolumeProfileInstance;
                profileName = _twilightVolumeProfileInstance != null ? "Twilight" : "Night";
            }
            else
            {
                // Night phases
                targetProfile = _nightVolumeProfileInstance;
                profileName = _nightVolumeProfileInstance != null ? "Night" : "NULL";
            }

            // Switch profile if different
            bool profileChanged = false;
            if (targetProfile != null && globalVolume.profile != targetProfile)
            {
                globalVolume.profile = targetProfile;
                profileChanged = true;
                Debug.Log($"[VolumeBlend] Switched to {profileName} profile (runtime instance)");
            }

            // Re-cache components if profile changed (CRITICAL: components are profile-specific!)
            if (profileChanged)
            {
                Debug.Log("[VolumeBlend] Profile changed, re-initializing volume components...");
                InitializeVolumeComponents();
            }

            // Always apply phase-specific post effects (even without volume switching)
            ApplyPostProcessing();

            // Debug: Check volume state
            if (showDebugOverlay)
            {
                bool volActive = globalVolume != null && globalVolume.isActiveAndEnabled;
                Debug.Log($"[VolumeBlend] Time={t:F1}h, Profile={profileName}, VolumeActive={volActive}");
            }
        }

        private void ApplyPostProcessing()
        {
            // DO NOT modify VolumeProfile ColorAdjustments here!
            // The VolumeProfiles (Day/Night/Twilight) already have ColorAdjustments set
            // They control saturation, contrast, exposure from the VolumeProfile itself
            
            // Only apply bloom/vignette overrides if explicitly set in phase
            // This avoids resetting the ColorAdjustments from the profile
            
            // For bloom - only override if phase explicitly wants custom bloom
            if (_bloom != null && _currentPhase.useCustomBloom)
            {
                _bloom.intensity.Override(_currentPhase.bloomIntensity);
                _bloom.threshold.Override(_currentPhase.bloomThreshold);
            }
        }

        private void ApplyTemperatureFilter(float temperature)
        {
            // Always apply temperature effect if we have a profile
            if (profile == null || !profile.enableTemperatureFilter) return;

            // Calculate temperature factor (0 = cold, 1 = hot)
            float coldThreshold = profile.temperatureConfig?.coldThreshold ?? 10f;
            float hotThreshold = profile.temperatureConfig?.hotThreshold ?? 30f;

            float tempFactor = Mathf.Clamp01(Mathf.InverseLerp(coldThreshold, hotThreshold, temperature));

            // === AGGRESSIVE TEMPERATURE EFFECTS using dedicated temperature volume ===

            // Use dedicated temperature ColorAdjustments (CRITICAL: this always works!)
            if (_temperatureColorAdjustments != null)
            {
                // Cold: Blue tint + desaturated
                // Hot: Orange/amber tint + boosted saturation
                Color coldColor = new Color(0.6f, 0.8f, 1f, 1f);   // Blue-ish (more visible)
                Color hotColor = new Color(1f, 0.8f, 0.5f, 1f);    // Orange-ish (heat haze)

                Color filterColor = Color.Lerp(coldColor, hotColor, tempFactor);
                _temperatureColorAdjustments.colorFilter.Override(filterColor);

                // Saturation: -30 (very faded) to +25 (very vivid)
                float sat = Mathf.Lerp(-30f, 25f, tempFactor);
                _temperatureColorAdjustments.saturation.Override(sat);

                // Exposure: darker in cold, brighter in heat
                float exp = Mathf.Lerp(-0.5f, 0.4f, tempFactor);
                _temperatureColorAdjustments.postExposure.Override(exp);

                // Contrast: crisp in cold, hazy in heat
                float contrast = Mathf.Lerp(35f, -15f, tempFactor);
                _temperatureColorAdjustments.contrast.Override(contrast);

                Debug.Log($"[TempFilter] Temp={temperature:F1}C, Factor={tempFactor:F2}, " +
                          $"Sat={sat:F0}, Exp={exp:F2}");
            }

            // Also apply non-volume effects
            if (profile.enableFog && _currentPhase != null)
            {
                Color coldFog = new Color(0.4f, 0.5f, 0.7f, 1f);   // Misty blue
                Color hotFog = new Color(0.8f, 0.6f, 0.4f, 1f);  // Hazy amber
                RenderSettings.fogColor = Color.Lerp(coldFog, hotFog, tempFactor);
            }

            // Ambient color shift
            Color coldAmbient = new Color(0.25f, 0.3f, 0.45f);
            Color hotAmbient = new Color(0.55f, 0.45f, 0.3f);
            RenderSettings.ambientSkyColor = Color.Lerp(coldAmbient, hotAmbient, tempFactor);
        }

        private void UpdateExternalControllers()
        {
            // Update ConstellationController
            if (profile?.constellationController != null && profile.enableSkyDome)
            {
                profile.constellationController.SetServerTimeOfDay(_serverTimeOfDay);
                if (profile.forceAllStarsVisible)
                {
                    profile.constellationController.forceFullVisibility = true;
                }
            }

            // Update MoonController
            if (profile?.moonController != null && profile.enableMoon)
            {
                // Moon intensity based on phase
                float moonIntensity = _currentPhase.phaseName.Contains("Night") ? _currentPhase.moonIntensityMultiplier : 0f;
                profile.moonController.alwaysVisible = _currentPhase.moonVisible;
            }
        }

        private float CalculateStarVisibility()
        {
            float time = _serverTimeOfDay;
            const float NIGHT_START = 21f;
            const float NIGHT_END = 5f;
            const float TWILIGHT_DURATION = 1.5f;

            if (time >= NIGHT_START || time < NIGHT_END)
            {
                return _currentPhase.starsVisible ? _currentPhase.starVisibility : 1f;
            }

            if (time >= NIGHT_START - TWILIGHT_DURATION && time < NIGHT_START)
            {
                float elapsed = time - (NIGHT_START - TWILIGHT_DURATION);
                return _currentPhase.starsVisible ? Mathf.InverseLerp(0f, TWILIGHT_DURATION, elapsed) * 0.7f : 0f;
            }

            if (time >= NIGHT_END && time < NIGHT_END + TWILIGHT_DURATION)
            {
                float elapsed = time - NIGHT_END;
                return _currentPhase.starsVisible ? 1f - Mathf.InverseLerp(0f, TWILIGHT_DURATION, elapsed) : 0f;
            }

            return _currentPhase.starsVisible ? 0f : 0f;
        }

        // ========================
        // Variability System
        // ========================

        /// <summary>
        /// Apply seeded variability based on day number for consistent results across clients.
        /// </summary>
        private float ApplyVariabilityIntensity()
        {
            if (_currentPhase == null) return 1f;
            float seed = GetDaySeed();
            return seededRand(seed, _currentPhase.intensityRange);
        }

        private Color ApplyVariability(Color baseColor)
        {
            if (_currentPhase == null) return baseColor;

            float hue, sat, val;
            Color.RGBToHSV(baseColor, out hue, out sat, out val);

            float seed = GetDaySeed();
            float hueShift = seededRand(seed, _currentPhase.hueShiftRange);
            float satMult = seededRand(seed + 0.1f, _currentPhase.saturationRange);

            hue = Mathf.Repeat(hue + hueShift, 1f);
            sat = Mathf.Clamp01(sat * satMult);

            return Color.HSVToRGB(hue, sat, val);
        }

        private float GetDaySeed()
        {
            if (ServerWeatherController.Instance != null)
            {
                return Mathf.Floor(ServerWeatherController.Instance.TotalGameDays);
            }
            return 0f;
        }

        private float seededRand(float seed, Vector2 range)
        {
            float t = Mathf.Repeat(seed * 0.618034f, 1f);
            return Mathf.Lerp(range.x, range.y, t);
        }

        // ========================
        // Public API
        // ========================

        /// <summary>
        /// Set time of day manually (bypasses server).
        /// </summary>
        public void SetTimeOfDay(float time)
        {
            _serverTimeOfDay = Mathf.Repeat(time, 24f);
            UpdateDayNight();
        }

        /// <summary>
        /// Get sun direction vector for shader use.
        /// </summary>
        public Vector3 GetSunDirection()
        {
            if (sunLight != null)
            {
                return -sunLight.transform.forward;
            }
            return Vector3.down;
        }

        /// <summary>
        /// Force immediate refresh of all lighting.
        /// </summary>
        public void ForceRefresh()
        {
            _phaseTransitionTime = 0f;
            UpdateDayNight();
        }

        // ========================
        // Debug
        // ========================

        void OnGUI()
        {
            if (!showDebugOverlay || profile == null || profile.showDebugInfo == false) return;

            GUILayout.BeginArea(new Rect(10, 10, 350, 250));
            GUILayout.BeginVertical("box");

            GUILayout.Label($"<color=white><b>Day/Night Debug</b></color>");
            GUILayout.Label($"<color=white>Time: {_serverTimeOfDay:F2}h</color>");
            GUILayout.Label($"<color=white>Day Factor: {_dayFactor:F2}</color>");

            if (_currentPhase != null)
            {
                GUILayout.Label($"<color=white>Phase: {_currentPhase.phaseName}</color>");
                GUILayout.Label($"<color=white>Blend: {_currentPhaseBlend:F2}</color>");
            }

            // Temperature Filter Status
            float coldThreshold = profile.temperatureConfig?.coldThreshold ?? 0f;
            float hotThreshold = profile.temperatureConfig?.hotThreshold ?? 40f;
            float tempFactor = Mathf.Clamp01(Mathf.InverseLerp(coldThreshold, hotThreshold, currentTemperature));

            string tempState = tempFactor > 0.7f ? "<color=red>HOT</color>" :
                               tempFactor < 0.3f ? "<color=cyan>COLD</color>" :
                               "<color=yellow>NEUTRAL</color>";

            GUILayout.Label($"<color=white>Temperature: {currentTemperature:F1}C</color>");
            GUILayout.Label($"<color=white>State: {tempState}</color>");
            GUILayout.Label($"<color=white>TempFactor: {tempFactor:F2}</color>");
            GUILayout.Label($"<color=white>Fog: {(profile.enableFog ? "ON" : "OFF")}</color>");
            GUILayout.Label($"<color=white>VolBlend: {(profile.useVolumeBlending ? "ON" : "OFF")}</color>");

            GUILayout.Label($"<color=white>Stars: {_starVisibility:F2}</color>");

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
