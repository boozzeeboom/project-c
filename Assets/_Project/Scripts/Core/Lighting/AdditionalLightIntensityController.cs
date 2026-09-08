using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectC.Core
{
    /// <summary>
    /// Global client-side controller for additional light intensity.
    /// Discovers Point/Spot/Area lights in all loaded scenes and applies
    /// time-of-day intensity curves per group without changing their authored base intensity.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AdditionalLightIntensityController : MonoBehaviour
    {
        [Serializable]
        public sealed class LightGroup
        {
            [Tooltip("Stable identifier used in logs and inspector.")]
            public string groupId = "AdditionalLightGroup";

            [Tooltip("Case-insensitive substring matched against the Light GameObject name. Leave empty to match every additional light.")]
            public string nameContains = string.Empty;

            [Tooltip("Multiplier evaluated over the 0-24 hour cycle. X is time of day, Y is intensity multiplier.")]
            public AnimationCurve intensityByHour = AnimationCurve.Linear(0f, 1f, 24f, 1f);

            public bool Matches(Light light)
            {
                if (light == null || light.type == LightType.Directional)
                {
                    return false;
                }

                return string.IsNullOrWhiteSpace(nameContains) ||
                       light.name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            public float Evaluate(float timeOfDay)
            {
                if (intensityByHour == null || intensityByHour.length == 0)
                {
                    return 1f;
                }

                return Mathf.Max(0f, intensityByHour.Evaluate(Mathf.Clamp(timeOfDay, 0f, 24f)));
            }
        }

        [Header("Discovery")]
        [SerializeField, Min(0.1f)] private float rescanInterval = 2f;
        [SerializeField] private bool includeInactiveLights = true;
        [SerializeField] private bool controlOnlyAdditionalLights = true;

        [Header("Runtime Response")]
        [SerializeField, Min(0.1f)] private float transitionSpeed = 4f;
        [SerializeField] private bool logDiscovery = false;

        [Header("Group Order")]
        [Tooltip("Groups are evaluated from top to bottom. The first matching group controls a light. Keep the catch-all group last.")]
        [SerializeField] private LightGroup[] groups;

        private readonly Dictionary<Light, float> _baseIntensityByLight = new Dictionary<Light, float>();
        private readonly List<Light> _trackedLights = new List<Light>();

        private DayNightController _dayNightController;
        private float _currentTimeOfDay = 12f;
        private float _nextRescanTime;
        private bool _hasAppliedTime;

        private void Reset()
        {
            groups = CreateDefaultGroups();
        }

        private void Awake()
        {
            EnsureDefaultGroups();
            _dayNightController = GetComponent<DayNightController>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            RefreshLights();
            ApplyIntensity(_currentTimeOfDay, true);
        }

        private void Update()
        {
            if (Time.time >= _nextRescanTime)
            {
                RefreshLights();
                _nextRescanTime = Time.time + rescanInterval;
            }

            float timeOfDay = ReadTimeOfDay();
            if (!_hasAppliedTime || !Mathf.Approximately(timeOfDay, _currentTimeOfDay))
            {
                _currentTimeOfDay = timeOfDay;
                ApplyIntensity(timeOfDay, false);
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshLights();
            ApplyIntensity(_currentTimeOfDay, true);
        }

        private float ReadTimeOfDay()
        {
            if (_dayNightController == null)
            {
                _dayNightController = GetComponent<DayNightController>();
            }

            if (_dayNightController != null)
            {
                return Mathf.Repeat(_dayNightController.ServerTimeOfDay, 24f);
            }

            if (ServerWeatherController.Instance != null)
            {
                return Mathf.Repeat(ServerWeatherController.Instance.TimeOfDay, 24f);
            }

            return _currentTimeOfDay;
        }

        private void RefreshLights()
        {
            Light[] allLights = FindObjectsByType<Light>(
                includeInactiveLights ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            _trackedLights.Clear();

            foreach (Light light in allLights)
            {
                if (light == null || (controlOnlyAdditionalLights && light.type == LightType.Directional))
                {
                    continue;
                }

                _trackedLights.Add(light);
                if (!_baseIntensityByLight.ContainsKey(light))
                {
                    _baseIntensityByLight.Add(light, light.intensity);
                }
            }

            if (logDiscovery)
            {
                Debug.Log($"[AdditionalLightIntensityController] Tracking {_trackedLights.Count} additional lights across loaded scenes.");
            }
        }

        private void ApplyIntensity(float timeOfDay, bool immediate)
        {
            float blend = immediate ? 1f : 1f - Mathf.Exp(-transitionSpeed * Time.deltaTime);

            foreach (Light light in _trackedLights)
            {
                if (light == null || !_baseIntensityByLight.TryGetValue(light, out float baseIntensity))
                {
                    continue;
                }

                LightGroup group = FindGroup(light);
                float multiplier = group != null ? group.Evaluate(timeOfDay) : 1f;
                float targetIntensity = baseIntensity * multiplier;

                light.intensity = immediate
                    ? targetIntensity
                    : Mathf.Lerp(light.intensity, targetIntensity, blend);
            }

            _hasAppliedTime = true;
        }

        private LightGroup FindGroup(Light light)
        {
            if (groups == null)
            {
                return null;
            }

            foreach (LightGroup group in groups)
            {
                if (group != null && group.Matches(light))
                {
                    return group;
                }
            }

            return null;
        }

        private void EnsureDefaultGroups()
        {
            if (groups == null || groups.Length == 0)
            {
                groups = CreateDefaultGroups();
            }
        }

        private static LightGroup[] CreateDefaultGroups()
        {
            return new[]
            {
                new LightGroup
                {
                    groupId = "CityLamps",
                    nameContains = "MD2_Lamp_",
                    intensityByHour = CreateCurve(
                        new Keyframe(0f, 1f),
                        new Keyframe(5f, 1f),
                        new Keyframe(6f, 0f),
                        new Keyframe(17f, 0f),
                        new Keyframe(19f, 0.25f),
                        new Keyframe(21f, 1f),
                        new Keyframe(24f, 1f))
                },
                new LightGroup
                {
                    groupId = "AllAdditionalLights",
                    nameContains = string.Empty,
                    intensityByHour = CreateCurve(
                        new Keyframe(0f, 1f),
                        new Keyframe(5f, 1f),
                        new Keyframe(6f, 0.15f),
                        new Keyframe(17f, 0.15f),
                        new Keyframe(19f, 0.4f),
                        new Keyframe(21f, 1f),
                        new Keyframe(24f, 1f))
                }
            };
        }

        private static AnimationCurve CreateCurve(params Keyframe[] keys)
        {
            return new AnimationCurve(keys);
        }
    }
}
