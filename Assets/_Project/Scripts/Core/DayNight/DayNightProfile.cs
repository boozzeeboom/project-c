using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectC.Core
{
    /// <summary>
    /// Master configuration for the day-night cycle system.
    /// Contains all phase definitions and server time synchronization settings.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDayNightProfile", menuName = "ProjectC/DayNight/DayNightProfile")]
    public class DayNightProfile : ScriptableObject
    {
        [Header("Phases")]
        [Tooltip("Array of time-of-day phases (Morning, Midday, Evening, Twilight, Night)")]
        public TimeOfDayPhase[] phases = new TimeOfDayPhase[5];

        [Header("Server Time Synchronization")]
        [Tooltip("Enable automatic phase detection based on server time")]
        public bool enableAutoPhaseDetection = true;
        [Tooltip("Fallback zone when no phases match (use Night phase as default)")]
        public TimeOfDayPhase fallbackPhase;

        [Header("Global Settings")]
        [Tooltip("Enable sky dome (stars and constellations)")]
        public bool enableSkyDome = true;
        [Tooltip("Enable moon controller")]
        public bool enableMoon = true;
        [Tooltip("Enable temperature filter post-processing")]
        public bool enableTemperatureFilter = true;
        [Tooltip("Enable fog")]
        public bool enableFog = true;

        [Header("Day/Night Skybox Materials")]
        [Tooltip("Skybox material for day phases")]
        public Material daySkyboxMaterial;
        [Tooltip("Skybox material for night phases")]
        public Material nightSkyboxMaterial;
        [Tooltip("Enable skybox blending between day and night")]
        public bool enableSkyboxBlending = true;

        [Header("URP Volume Profiles")]
        [Tooltip("Volume profile for daytime (Morning, Midday, Evening)")]
        public VolumeProfile dayVolumeProfile;
        [Tooltip("Volume profile for nighttime (Twilight, Night)")]
        public VolumeProfile nightVolumeProfile;
        [Tooltip("Volume profile for twilight transition")]
        public VolumeProfile twilightVolumeProfile;
        [Tooltip("Use volume blending based on blend factor")]
        public bool useVolumeBlending = true;

        [Header("Color Grading")]
        [Tooltip("Reference to temperature filter configuration")]
        public TemperatureFilterConfig temperatureConfig;
        [Tooltip("How strongly temperature overlay blends on top of the active day/night/twilight ColorAdjustments baseline. 0 = no temperature effect, 1 = full replacement of baseline by temperature. Recommended: 0.2 - 0.4 for a subtle overlay.")]
        [Range(0f, 1f)]
        public float temperatureEffectStrength = 0.3f;
        [Tooltip("Global saturation multiplier")]
        [Range(-1f, 1f)]
        public float globalSaturationOffset = 0f;
        [Tooltip("Global exposure multiplier")]
        [Range(-2f, 2f)]
        public float globalExposureOffset = 0f;
        [Tooltip("Global contrast multiplier")]
        [Range(-1f, 1f)]
        public float globalContrastOffset = 0f;

        [Header("Reference Controllers")]
        [Tooltip("Reference to ConstellationController for star sync")]
        public ConstellationController constellationController;
        [Tooltip("Reference to MoonController for moon sync")]
        public MoonController moonController;

        [Header("Debug")]
        public bool showDebugInfo = false;
        public bool forceAllStarsVisible = false;

        /// <summary>
        /// Get the phase that contains the given hour.
        /// Returns null if no phase contains the hour.
        /// </summary>
        public TimeOfDayPhase GetPhaseForHour(float hour)
        {
            if (phases == null || phases.Length == 0)
            {
                return fallbackPhase;
            }

            TimeOfDayPhase bestPhase = null;
            int bestPriority = int.MinValue;
            float bestProgress = 0f;

            foreach (var phase in phases)
            {
                if (phase == null) continue;

                if (phase.ContainsHour(hour))
                {
                    // If priorities are tied, prefer the one with higher progress (deeper into phase)
                    if (bestPhase == null || phase.priority > bestPriority ||
                        (phase.priority == bestPriority && phase.GetPhaseProgress(hour) > bestProgress))
                    {
                        bestPhase = phase;
                        bestPriority = phase.priority;
                        bestProgress = phase.GetPhaseProgress(hour);
                    }
                }
            }

            return bestPhase ?? fallbackPhase;
        }

        /// <summary>
        /// Get the next phase after the current one.
        /// </summary>
        public TimeOfDayPhase GetNextPhase(float hour)
        {
            if (phases == null || phases.Length == 0)
            {
                return null;
            }

            float minDistance = float.MaxValue;
            TimeOfDayPhase nextPhase = null;

            foreach (var phase in phases)
            {
                if (phase == null) continue;

                float distance;
                if (phase.startHour > hour)
                {
                    distance = phase.startHour - hour;
                }
                else
                {
                    distance = (24f - hour) + phase.startHour;
                }

                if (distance < minDistance)
                {
                    minDistance = distance;
                    nextPhase = phase;
                }
            }

            return nextPhase;
        }

        /// <summary>
        /// Get blend factor between current and next phase (0-1).
        /// </summary>
        public float GetBlendFactor(float hour)
        {
            var current = GetPhaseForHour(hour);
            var next = GetNextPhase(hour);

            if (current == null || next == null)
            {
                return current != null ? 1f : 0f;
            }

            // Calculate time until next phase
            float distanceToNext;
            if (next.startHour > hour)
            {
                distanceToNext = next.startHour - hour;
            }
            else
            {
                distanceToNext = (24f - hour) + next.startHour;
            }

            float totalPhaseDuration = 24f / phases.Length;
            float blendRange = current.blendDuration > 0 ? current.blendDuration : totalPhaseDuration * 0.1f;

            return Mathf.InverseLerp(0f, blendRange, distanceToNext);
        }

        /// <summary>
        /// Get day/night factor (0 = night, 1 = day).
        /// Used for legacy compatibility and shader uniforms.
        /// </summary>
        public float GetDayFactor(float hour)
        {
            if (phases == null || phases.Length == 0)
            {
                return 0.5f;
            }

            TimeOfDayPhase phase = GetPhaseForHour(hour);
            if (phase == null)
            {
                return 0.5f;
            }

            // Day phases return closer to 1, night phases return closer to 0
            if (phase.phaseName.Contains("Night"))
            {
                return 0f;
            }
            else if (phase.phaseName.Contains("Midday") || phase.phaseName.Contains("Day"))
            {
                return 1f;
            }

            // For transition phases, use progress in phase
            return 1f - phase.GetPhaseProgress(hour) * 0.5f;
        }

        /// <summary>
        /// Validate profile and log warnings for missing configurations.
        /// </summary>
        public void Validate()
        {
            if (phases == null || phases.Length == 0)
            {
                Debug.LogWarning($"[{GetType().Name}] No phases defined in profile '{name}'");
            }

            if (daySkyboxMaterial == null && nightSkyboxMaterial != null)
            {
                Debug.LogWarning($"[{GetType().Name}] Night skybox material set but no day skybox in profile '{name}'");
            }

            if (enableTemperatureFilter && temperatureConfig == null)
            {
                Debug.LogWarning($"[{GetType().Name}] Temperature filter enabled but no config set in profile '{name}'");
            }

            foreach (var phase in phases)
            {
                if (phase == null)
                {
                    Debug.LogWarning($"[{GetType().Name}] Profile '{name}' contains null phase reference");
                }
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Validate Profile")]
        private void EditorValidate()
        {
            Validate();
        }

        [ContextMenu("Log All Phases")]
        private void LogAllPhases()
        {
            Debug.Log($"=== Profile: {name} ===");
            foreach (var phase in phases)
            {
                if (phase != null)
                {
                    Debug.Log($"  {phase.phaseName}: {phase.startHour:F2}h - {phase.endHour:F2}h");
                }
                else
                {
                    Debug.Log($"  [NULL PHASE]");
                }
            }
        }
#endif
    }
}
