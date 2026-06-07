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
        /// <summary>
        /// T-Q06: Static accessor для триггеров. Set в Awake/OnEnable, clear в OnDisable.
        /// Fallback в DayNightPhaseTrigger.cs: FindObjectOfType если null.
        /// </summary>
        public static DayNightController Instance { get; private set; }

        [Header("Profile")]
        [Tooltip("DayNightProfile with phase definitions")]
        public DayNightProfile profile;

        [Header("References")]
        public Light sunLight;

        [Header("Skybox")]
        public Material daySkyboxMaterial;
        public Material nightSkyboxMaterial;

        [Header("Temperature")]
        [Tooltip("Current ambient temperature in degrees Celsius. Driven by ServerWeatherController.")]
        public float currentTemperature = 20f;

        [Header("URP Volume")]
        public Volume globalVolume;
        public VolumeProfile dayVolumeProfile;
        public VolumeProfile nightVolumeProfile;
        public VolumeProfile twilightVolumeProfile;

        [Header("Debug")]
        public bool showDebugOverlay = true;
        public bool logInitialization = false;
        public bool logVolumeBlend = false;
        public bool logWarnings = false;

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

        // Runtime copies of volume profiles (CRITICAL: prevents external modifications!)
        // Original assets should never be modified at runtime
        private VolumeProfile _dayVolumeProfileInstance;
        private VolumeProfile _nightVolumeProfileInstance;
        private VolumeProfile _twilightVolumeProfileInstance;
        private VolumeProfile _currentProfileInstance;

        // Blend volumes: 3 child GameObjects, each with a Volume component bound to one
        // of the runtime profile instances. ApplyVolumeBlend sets their `weight` each
        // frame to create cross-fades between phases. priority=100, isGlobal=true.
        // globalVolume (priority=0) is left alone in the inspector and never used here.
        private Volume _dayBlendVolume;
        private Volume _twilightBlendVolume;
        private Volume _nightBlendVolume;

        // Temperature overlay volume: a SEPARATE 4th blend volume with priority=200
        // (above day/twilight/night = 100). It owns its OWN ColorAdjustments profile
        // and is the ONLY place ApplyTemperatureFilter writes to. This means
        // day/twilight/night volume profiles stay immutable at runtime — whatever you
        // set in their .asset files is what URP uses, period.
        private Volume _temperatureBlendVolume;
        private VolumeProfile _temperatureColorProfile;

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
            // T-Q06: set Instance для trigger accessors
            if (Instance == null) Instance = this;
            SubscribeToServerEvents();
            ValidateProfileInstances();
        }

        private void ValidateProfileInstances()
        {
            bool profilesLost = _dayVolumeProfileInstance == null || _nightVolumeProfileInstance == null || _twilightVolumeProfileInstance == null;
            bool blendVolumesLost = _dayBlendVolume == null || _twilightBlendVolume == null || _nightBlendVolume == null;
            bool tempVolLost = _temperatureBlendVolume == null || _temperatureColorProfile == null;

            if (profilesLost)
            {
                if (logWarnings) Debug.LogWarning("[DayNightController] Profile instances were lost (possible domain reload). Re-initializing...");
                InitializeVolumeProfileInstances();
            }
            if (blendVolumesLost)
            {
                if (logWarnings) Debug.LogWarning("[DayNightController] Blend volume references lost (possible domain reload). Re-initializing...");
                InitializeBlendVolumes();
            }
            if (tempVolLost)
            {
                if (logWarnings) Debug.LogWarning("[DayNightController] Temperature blend volume reference lost (possible domain reload). Re-initializing...");
                InitializeTemperatureVolume();
            }
        }

        void OnDisable()
        {
            // T-Q06: clear Instance
            if (Instance == this) Instance = null;
            UnsubscribeFromServerEvents();
        }

        void OnDestroy()
        {
            UnsubscribeFromServerEvents();
        }

        private void Initialize()
        {
            // Create runtime copies of volume profiles (CRITICAL: prevents external modifications!)
            InitializeVolumeProfileInstances();

            // Create 3 child blend volumes (one per phase profile). Their `weight` is
            // driven by ApplyVolumeBlend each frame for smooth phase cross-fades.
            InitializeBlendVolumes();

            // Create the temperature overlay volume (4th, priority=200, separate profile).
            // This is the ONLY thing ApplyTemperatureFilter writes to — day/twilight/night
            // profiles stay immutable so values you set in the .asset files are final.
            InitializeTemperatureVolume();

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

            if (logInitialization) Debug.Log($"[DayNightController] Created runtime profile instances: Day={_dayVolumeProfileInstance != null}, Night={_nightVolumeProfileInstance != null}, Twilight={_twilightVolumeProfileInstance != null}");
        }

        /// <summary>
        /// Create the 3 child GameObjects with Volume components used for cross-fading
        /// between phases. Each is bound to one of the runtime profile instances and
        /// starts with weight=0 (ApplyVolumeBlend drives weights each frame).
        /// </summary>
        private void InitializeBlendVolumes()
        {
            _dayBlendVolume = CreateOrGetBlendVolume("DayBlendVolume", _dayVolumeProfileInstance);
            _twilightBlendVolume = CreateOrGetBlendVolume("TwilightBlendVolume", _twilightVolumeProfileInstance);
            _nightBlendVolume = CreateOrGetBlendVolume("NightBlendVolume", _nightVolumeProfileInstance);

            if (logInitialization) Debug.Log("[DayNightController] Blend volumes created (Day/Twilight/Night), priority=100, weight=0 — driven by ApplyVolumeBlend");
        }

        /// <summary>
        /// Create a SEPARATE 4th volume (priority=200) that owns its own ColorAdjustments
        /// profile. ApplyTemperatureFilter writes ONLY to this profile's ColorAdjustments,
        /// so day/twilight/night profiles are never mutated — the values you set in those
        /// .asset files are exactly what URP applies at runtime.
        /// </summary>
        private void InitializeTemperatureVolume()
        {
            // Use an existing child if it survived a domain reload; otherwise create one.
            Transform child = transform.Find("TemperatureBlendVolume");
            GameObject go;
            if (child == null)
            {
                go = new GameObject("TemperatureBlendVolume");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
            }
            else
            {
                go = child.gameObject;
            }

            var vol = go.GetComponent<Volume>();
            if (vol == null) vol = go.AddComponent<Volume>();
            vol.priority = 200; // above day/twilight/night (100)
            vol.isGlobal = true;
            vol.weight = 0f; // off by default — ApplyTemperatureFilter sets it each frame

            // Always create a fresh in-memory profile (we own and mutate it).
            _temperatureColorProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.profile = _temperatureColorProfile;

            // Add ColorAdjustments and start with neutral values. The state is set to
            // "overriding" so even at neutral temperature the volume "tells" URP
            // "no effect" only by being weight=0 (not by inactive override).
            var ca = _temperatureColorProfile.Add<ColorAdjustments>(true);
            ca.colorFilter.Override(Color.white);
            ca.saturation.Override(0f);
            ca.postExposure.Override(0f);
            ca.contrast.Override(0f);
            ca.hueShift.Override(0f);

            _temperatureBlendVolume = vol;
            if (logInitialization) Debug.Log("[DayNightController] Temperature blend volume created (priority=200, owns its own ColorAdjustments profile)");
        }

        private Volume CreateOrGetBlendVolume(string goName, VolumeProfile profile)
        {
            Transform child = transform.Find(goName);
            GameObject go;
            if (child == null)
            {
                go = new GameObject(goName);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
            }
            else
            {
                go = child.gameObject;
            }

            var vol = go.GetComponent<Volume>();
            if (vol == null) vol = go.AddComponent<Volume>();

            vol.priority = 100; // above the inspector's globalVolume (priority 0) so this system wins
            vol.isGlobal = true;
            vol.weight = 0f; // off until ApplyVolumeBlend sets it
            vol.profile = profile;
            return vol;
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

                // T-Q06: publish WorldEvent for quest triggers (DayNightPhaseTrigger).
                // PlayerId = 0 (global event, не привязан к игроку).
                if (_currentPhase != null)
                {
                    ProjectC.Core.WorldEventBus.Publish(new ProjectC.Core.DayNightPhaseChangedEvent
                    {
                        PlayerId = 0,
                        TimestampUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        NewPhaseName = _currentPhase.phaseName
                    });
                }
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
        /// Lerp two colors in HSV space, taking the shortest path on the hue circle.
        /// Use this instead of Color.Lerp when interpolating between two colors with
        /// different hues (e.g. warm fog -> cool fog, orange sun -> blueish sun):
        /// RGB lerp in that case passes through a desaturated gray/white "muddy" middle.
        /// </summary>
        private static Color LerpHSV(Color a, Color b, float t)
        {
            t = Mathf.Clamp01(t);
            Color.RGBToHSV(a, out float ah, out float as_, out float av);
            Color.RGBToHSV(b, out float bh, out float bs, out float bv);

            // Shortest-path hue lerp (h is on a 0..1 ring)
            float hDiff = bh - ah;
            if (hDiff > 0.5f) hDiff -= 1f;
            else if (hDiff < -0.5f) hDiff += 1f;
            float h = Mathf.Repeat(ah + hDiff * t, 1f);

            float s = Mathf.Lerp(as_, bs, t);
            float v = Mathf.Lerp(av, bv, t);
            return Color.HSVToRGB(h, s, v);
        }

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
            // Use HSV lerp for sun color: a warm orange sun to a near-white midday sun would
            // otherwise pass through a desaturated gray in RGB lerp, which looks like a
            // "white flash" mid-transition.
            _smoothedSunColor = LerpHSV(_smoothedSunColor, targetSunColor, smoothFactor * Time.deltaTime * 3f);
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

            // Smooth interpolation. Use HSV lerp for ambient colors so hue transitions
            // (e.g. cool blue -> warm orange at dusk) don't pass through desaturated gray.
            float smoothFactor = GetSmoothFactor();
            _smoothedAmbientSky = LerpHSV(_smoothedAmbientSky, targetSky, smoothFactor * Time.deltaTime * 2f);
            _smoothedAmbientIntensity = Mathf.Lerp(_smoothedAmbientIntensity, targetIntensity, smoothFactor * Time.deltaTime * 2f);

            // Apply to render settings
            RenderSettings.ambientSkyColor = _smoothedAmbientSky;
            RenderSettings.ambientEquatorColor = LerpHSV(RenderSettings.ambientEquatorColor, targetEquator, smoothFactor * Time.deltaTime * 2f);
            RenderSettings.ambientGroundColor = LerpHSV(RenderSettings.ambientGroundColor, targetGround, smoothFactor * Time.deltaTime * 2f);
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

                // Smooth interpolation for fog. We use:
                //  - HSV lerp for color (avoids the gray/white "muddy" middle when hue changes)
                //  - gentler lerp speed for density (was 2f, Evening's 4x density jump from Midday
                //    used to whiten the screen briefly as fog ramped up)
                float smoothFactor = GetSmoothFactor();
                _smoothedFogColor = LerpHSV(_smoothedFogColor, targetFogColor, smoothFactor * Time.deltaTime * 1.5f);

                // Apply
                RenderSettings.fogColor = _smoothedFogColor;
                RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, targetFogDensity, smoothFactor * Time.deltaTime * 1.5f);
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
            if (profile == null) return;

            // Determine which phases we are between, based on server time
            TimeOfDayPhase currentPhase = profile.GetPhaseForHour(_serverTimeOfDay);
            if (currentPhase == null)
            {
                if (logVolumeBlend || logWarnings) Debug.LogWarning("[VolumeBlend] No phase matches current hour!");
                return;
            }
            TimeOfDayPhase nextPhase = profile.GetNextPhase(_serverTimeOfDay);
            if (nextPhase == null) nextPhase = currentPhase; // single-phase fallback

            // Calculate blend factor t (0..1): 0 = full current, 1 = full next
            float t = CalculateTransitionBlend(currentPhase, _serverTimeOfDay);

            // Set weights on the 3 blend volumes. The volume whose profile matches
            // the current phase gets (1-t), the one matching the next phase gets t,
            // the third gets 0 (off).
            SetBlendVolumeWeights(currentPhase, nextPhase, t);

            // Apply per-phase overrides (e.g. useCustomBloom) on top of the active blend volumes
            ApplyPostProcessing();
        }

        /// <summary>
        /// Calculate the cross-fade factor (0..1) between the current phase and the next one.
        /// t = 0 when far from the end of the current phase; t climbs to 1 over the last
        /// `phaseTransitionRatio` fraction of the phase's duration.
        /// </summary>
        private float CalculateTransitionBlend(TimeOfDayPhase currentPhase, float hour)
        {
            float ratio = Mathf.Clamp(profile != null ? profile.phaseTransitionRatio : 0f, 0f, 0.5f);
            if (ratio <= 0f) return 0f;

            float currentDuration = GetPhaseDurationHours(currentPhase);
            if (currentDuration <= 0f) return 0f;

            float transitionWindow = currentDuration * ratio;
            float hoursUntilEnd = HoursUntil(hour, currentPhase.endHour);
            return 1f - Mathf.Clamp01(hoursUntilEnd / transitionWindow);
        }

        private float GetPhaseDurationHours(TimeOfDayPhase phase)
        {
            if (phase.startHour <= phase.endHour) return phase.endHour - phase.startHour;
            return (24f - phase.startHour) + phase.endHour;
        }

        private float HoursUntil(float fromHour, float toHour)
        {
            float diff = toHour - fromHour;
            if (diff < 0f) diff += 24f;
            return diff;
        }

        /// <summary>
        /// Map a phase to one of the 3 runtime profile instances by inspecting its name.
        /// Day phases (Morning, Midday, Evening) -> day; Twilight -> twilight; Night -> night.
        /// </summary>
        private VolumeProfile GetProfileForPhase(TimeOfDayPhase phase)
        {
            if (phase == null) return null;
            string name = phase.phaseName ?? string.Empty;
            if (name.IndexOf("Night", System.StringComparison.OrdinalIgnoreCase) >= 0) return _nightVolumeProfileInstance;
            if (name.IndexOf("Twilight", System.StringComparison.OrdinalIgnoreCase) >= 0) return _twilightVolumeProfileInstance;
            return _dayVolumeProfileInstance;
        }

        private void SetBlendVolumeWeights(TimeOfDayPhase currentPhase, TimeOfDayPhase nextPhase, float t)
        {
            VolumeProfile currentProf = GetProfileForPhase(currentPhase);
            VolumeProfile nextProf = GetProfileForPhase(nextPhase);

            ApplyVolumeWeight(_dayBlendVolume, currentProf, nextProf, t);
            ApplyVolumeWeight(_twilightBlendVolume, currentProf, nextProf, t);
            ApplyVolumeWeight(_nightBlendVolume, currentProf, nextProf, t);
        }

        private void ApplyVolumeWeight(Volume vol, VolumeProfile currentProf, VolumeProfile nextProf, float t)
        {
            if (vol == null) return;

            // Special case: both neighbouring phases resolve to the same profile
            // (e.g. Midday -> Evening are both "day" phases). There is nothing to
            // cross-fade, so do NOT dim the volume as t rises. If we did, URP would
            // average the volume with the default (no-color-grading) state during
            // the transition window, producing a visible "white flash" on screen.
            if (currentProf == nextProf)
            {
                vol.weight = (vol.profile == currentProf) ? 1f : 0f;
                return;
            }

            if (vol.profile == currentProf) vol.weight = 1f - t;
            else if (vol.profile == nextProf) vol.weight = t;
            else vol.weight = 0f;
        }

        private void ApplyPostProcessing()
        {
            // Only apply bloom overrides if the current phase explicitly wants custom bloom.
            // We apply to the ColorAdjustments of each ACTIVE blend volume (weight > 0)
            // so the override participates in the cross-fade correctly.
            if (_currentPhase != null && _currentPhase.useCustomBloom)
            {
                ApplyToActiveBlendVolumes<Bloom>(b =>
                {
                    b.intensity.Override(_currentPhase.bloomIntensity);
                    b.threshold.Override(_currentPhase.bloomThreshold);
                });
            }
        }

        /// <summary>
        /// Run `apply` on the requested VolumeComponent for every blend volume that
        /// currently contributes to URP (weight > 0). Volumes with weight == 0 are skipped.
        /// </summary>
        private void ApplyToActiveBlendVolumes<T>(System.Action<T> apply) where T : VolumeComponent
        {
            if (_dayBlendVolume != null && _dayBlendVolume.weight > 0f &&
                _dayBlendVolume.profile != null && _dayBlendVolume.profile.TryGet<T>(out var d)) apply(d);
            if (_twilightBlendVolume != null && _twilightBlendVolume.weight > 0f &&
                _twilightBlendVolume.profile != null && _twilightBlendVolume.profile.TryGet<T>(out var tw)) apply(tw);
            if (_nightBlendVolume != null && _nightBlendVolume.weight > 0f &&
                _nightBlendVolume.profile != null && _nightBlendVolume.profile.TryGet<T>(out var n)) apply(n);
        }

        private void ApplyTemperatureFilter(float temperature)
        {
            // Always apply temperature effect if we have a profile
            if (profile == null || !profile.enableTemperatureFilter) return;
            if (_temperatureBlendVolume == null || _temperatureColorProfile == null) return;
            if (!_temperatureColorProfile.TryGet<ColorAdjustments>(out var ca)) return;

            // Calculate temperature factor (0 = cold, 1 = hot)
            float coldThreshold = profile.temperatureConfig?.coldThreshold ?? 10f;
            float hotThreshold = profile.temperatureConfig?.hotThreshold ?? 30f;
            float tempFactor = Mathf.Clamp01(Mathf.InverseLerp(coldThreshold, hotThreshold, temperature));

            // Volume weight acts as the master "how much temperature" knob.
            // At neutral temp (tempFactor=0) the volume is OFF — day/twilight/night
            // profiles pass through URP untouched, exactly as authored in the .asset.
            // At extreme temp, weight ramps up to `temperatureEffectStrength` (capped 0..1).
            float strength = profile.temperatureEffectStrength;
            _temperatureBlendVolume.weight = Mathf.Clamp01(tempFactor * strength);

            // --- Compute "temperature target" values (what 100% cold/hot would look like) ---
            // These are written into the temperature volume's OWN profile. They will be
            // applied by URP ON TOP of the day/twilight/night mix (priority 200 > 100).
            // Since URP's per-override blending takes the highest-priority value when
            // an override is active in both, the temperature volume replaces the
            // base values (color filter, exposure, etc.) for its active parameters.
            Color coldColor = new Color(0.6f, 0.8f, 1f, 1f);   // Blue-ish
            Color hotColor = new Color(1f, 0.8f, 0.5f, 1f);    // Orange-ish
            Color targetFilter = Color.Lerp(coldColor, hotColor, tempFactor);
            float targetSat = Mathf.Lerp(-30f, 25f, tempFactor);
            float targetExp = Mathf.Lerp(-0.5f, 0.4f, tempFactor);
            float targetContrast = Mathf.Lerp(35f, -15f, tempFactor);
            // No hue shift: temperature doesn't drive hue

            ca.colorFilter.Override(targetFilter);
            ca.saturation.Override(targetSat);
            ca.postExposure.Override(targetExp);
            ca.contrast.Override(targetContrast);
            ca.hueShift.Override(0f);

            // Non-volume effects: fog + ambient. These don't interfere with the
            // day/night/twilight ColorAdjustments and remain as-is.
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

            GUILayout.BeginArea(new Rect(10, 10, 380, 290));
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
            GUILayout.Label($"<color=white>TransitionRatio: {(profile != null ? profile.phaseTransitionRatio : 0f):F2}</color>");

            // Blend volume weights (Day / Twilight / Night)
            float dayW = _dayBlendVolume != null ? _dayBlendVolume.weight : 0f;
            float twW = _twilightBlendVolume != null ? _twilightBlendVolume.weight : 0f;
            float nightW = _nightBlendVolume != null ? _nightBlendVolume.weight : 0f;
            float tempW = _temperatureBlendVolume != null ? _temperatureBlendVolume.weight : 0f;
            GUILayout.Label($"<color=yellow>Blend  Day:{dayW:F2}  Tw:{twW:F2}  Night:{nightW:F2}  Temp:{tempW:F2}</color>");

            GUILayout.Label($"<color=white>Stars: {_starVisibility:F2}</color>");

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
