using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// Represents a single time-of-day phase with comprehensive lighting, skybox, and post-processing settings.
    /// Used by DayNightController to interpolate between phases based on server time.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPhase", menuName = "ProjectC/DayNight/TimeOfDayPhase")]
    public class TimeOfDayPhase : ScriptableObject
    {
        [Header("Identity")]
        public string phaseName = "New Phase";
        [Tooltip("Start hour (0-24)")]
        public float startHour = 0f;
        [Tooltip("End hour (0-24)")]
        public float endHour = 24f;
        [Tooltip("Priority for overlapping phases (higher = chosen first)")]
        public int priority = 0;

        [Header("Sun Light")]
        public Color sunColor = Color.white;
        public float sunIntensity = 1f;
        public float sunTemperature = 5500f;
        public bool castShadows = true;
        [Tooltip("Sun rotation offset (degrees)")]
        public Vector3 sunRotationOffset = Vector3.zero;

        [Header("Ambient Light")]
        public Color ambientSkyColor = new Color(0.2f, 0.2f, 0.3f);
        public Color ambientEquatorColor = new Color(0.3f, 0.3f, 0.4f);
        public Color ambientGroundColor = new Color(0.1f, 0.1f, 0.15f);
        public float ambientIntensity = 0.5f;

        [Header("Skybox")]
        public Gradient skyHorizonGradient;
        public float skyboxExposure = 1f;
        public Color skyboxTint = Color.white;
        [Tooltip("Skybox material to use (optional, will blend if set)")]
        public Material skyboxMaterial;

        [Header("Fog")]
        public Color fogColor = Color.gray;
        public float fogDensity = 0.0003f;
        public bool fogEnabled = true;

        [Header("Post-Processing Volume")]
        [Tooltip("Reference to URP VolumeProfile for this phase")]
        public UnityEngine.Rendering.VolumeProfile volumeProfile;
        
        [Header("Bloom Settings")]
        [Tooltip("Override bloom intensity for this phase")]
        public bool useCustomBloom = false;
        public float bloomIntensity = 0.3f;
        public float bloomThreshold = 0.8f;

        [Header("Color Grading")]
        [Tooltip("Saturation adjustment for this phase")]
        public float saturationOffset = 0f;
        [Tooltip("Exposure adjustment (EV)")]
        public float exposureOffset = 0f;
        [Tooltip("Contrast adjustment")]
        public float contrastOffset = 0f;
        [Tooltip("Color tint overlay (applied after other adjustments)")]
        public Color colorTintOverlay = Color.clear;

        [Header("Temperature Filter")]
        [Tooltip("Enable temperature-based color filter")]
        public bool useTemperatureFilter = true;
        [Tooltip("Strength of temperature effect (0-1)")]
        public float temperatureFilterStrength = 1f;

        [Header("Variability (randomization ranges)")]
        public Vector2 hueShiftRange = new Vector2(-0.05f, 0.05f);
        public Vector2 saturationRange = new Vector2(0.8f, 1.2f);
        public Vector2 intensityRange = new Vector2(0.85f, 1.15f);

        [Header("Transition")]
        [Tooltip("Curve for smooth interpolation within this phase")]
        public AnimationCurve transitionCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [Tooltip("Blend duration when entering this phase (seconds)")]
        public float blendDuration = 0.5f;

        [Header("Additional Effects")]
        [Tooltip("Enable star visibility during this phase")]
        public bool starsVisible = false;
        [Tooltip("Star visibility alpha (0-1) when this phase is active")]
        public float starVisibility = 0f;
        [Tooltip("Moon visible during this phase")]
        public bool moonVisible = true;
        [Tooltip("Moon intensity multiplier")]
        public float moonIntensityMultiplier = 1f;

        /// <summary>
        /// Check if a given hour falls within this phase, handling wrap-around (e.g., night phase 21-5).
        /// </summary>
        public bool ContainsHour(float hour)
        {
            if (startHour <= endHour)
            {
                return hour >= startHour && hour < endHour;
            }
            else
            {
                // Wrapping phase (e.g., 21-5 for night)
                return hour >= startHour || hour < endHour;
            }
        }

        /// <summary>
        /// Get normalized progress (0-1) within this phase.
        /// </summary>
        public float GetPhaseProgress(float hour)
        {
            float duration;
            if (startHour <= endHour)
            {
                duration = endHour - startHour;
                return Mathf.InverseLerp(startHour, endHour, hour);
            }
            else
            {
                // Wrapping phase
                if (hour >= startHour)
                {
                    duration = (24f - startHour) + endHour;
                    return (hour - startHour) / duration;
                }
                else
                {
                    duration = (24f - startHour) + endHour;
                    return ((24f - startHour) + hour) / duration;
                }
            }
        }
    }
}
