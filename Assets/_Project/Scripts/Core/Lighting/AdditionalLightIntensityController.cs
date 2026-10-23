using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectC.Core
{
    /// <summary>
    /// Global client-side controller for additional lights.
    /// Discovers Point/Spot/Area lights in all loaded scenes and applies, per group, a
    /// time-of-day intensity curve plus optional property overrides (range, color, type,
    /// spot angles, shadows, bounce) without touching the authored values stored in the scenes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AdditionalLightIntensityController : MonoBehaviour
    {
        /// <summary>
        /// Необязательные переопределения свойств источника — «как на самой лампочке».
        /// Каждая строка включается своим флагом: выключенная строка не трогает авторское
        /// значение, поэтому добавление новых строк не меняет поведение уже настроенных сцен.
        /// При смене группы значения возвращаются к авторским (базовым) и применяются заново.
        /// </summary>
        [Serializable]
        public sealed class LightOverrides
        {
            [Header("Форма и тип")]
            [Tooltip("Переопределять радиус действия Point/Spot (м).")]
            public bool overrideRange;
            [Min(0f)] public float range = 10f;

            [Tooltip("Переопределять тип источника. Directional не применяется: направленные источники группами не управляются.")]
            public bool overrideType;
            public LightType type = LightType.Point;

            [Tooltip("Переопределять угол конуса Spot (градусы). Применяется только к источникам типа Spot.")]
            public bool overrideSpotAngle;
            [Range(1f, 179f)] public float spotAngle = 30f;

            [Tooltip("Переопределять внутренний угол Spot (градусы). Применяется только к Spot и ограничивается внешним углом.")]
            public bool overrideInnerSpotAngle;
            [Range(0f, 179f)] public float innerSpotAngle = 21f;

            [Header("Цвет")]
            [Tooltip("Переопределять цвет источника. Альфа не используется.")]
            public bool overrideColor;
            public Color color = Color.white;

            [Tooltip("Включать/выключать цветовую температуру источника.")]
            public bool overrideUseColorTemperature;
            public bool useColorTemperature;

            [Tooltip("Абсолютная цветовая температура, K. Действует только при включённой цветовой температуре.")]
            public bool overrideColorTemperature;
            [Range(1000f, 20000f)] public float colorTemperature = 6500f;

            [Header("Тени и непрямой свет")]
            [Tooltip("Переопределять режим теней источника. При выключенных additional light shadows в URP Asset тени не рисуются.")]
            public bool overrideShadows;
            public LightShadows shadows = LightShadows.None;

            [Tooltip("Переопределять плотность тени 0…1. 0 = тень полностью прозрачна.")]
            public bool overrideShadowStrength;
            [Range(0f, 1f)] public float shadowStrength = 1f;

            [Tooltip("Переопределять bounce intensity (вклад источника в GI; при выключенном realtime GI влияет только на bake).")]
            public bool overrideBounceIntensity;
            [Min(0f)] public float bounceIntensity = 1f;

            public bool AnyEnabled =>
                overrideRange || overrideType || overrideSpotAngle || overrideInnerSpotAngle ||
                overrideColor || overrideUseColorTemperature || overrideColorTemperature ||
                overrideShadows || overrideShadowStrength || overrideBounceIntensity;
        }

        [Serializable]
        public sealed class LightGroup
        {
            [Tooltip("Stable identifier used in logs and inspector.")]
            public string groupId = "AdditionalLightGroup";

            [Tooltip("Case-insensitive substring matched against the Light GameObject name. Leave empty to match every additional light.")]
            public string nameContains = string.Empty;

            [Tooltip("Числовой множитель группы. К нему применяется кривая ниже: 1 = полный множитель, 0 = выключено.\nИтог = базовая интенсивность источника × множитель × значение кривой.")]
            public float intensityMultiplier = 1f;

            [Tooltip("Кривая по времени суток 0–24. По Y — доля 0…1 от числового множителя выше (1 = полный, 0 = выключено).")]
            public AnimationCurve intensityByHour = AnimationCurve.Linear(0f, 1f, 24f, 1f);

            [Tooltip("Переопределения свойств источника для всей группы: range, тип, углы Spot, цвет, температура, тени, bounce.\nПустое значение = новых настроек ещё нет, авторские значения источников сохраняются.")]
            public LightOverrides overrides = new LightOverrides();

            public bool Matches(Light light)
            {
                if (light == null || light.type == LightType.Directional)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(nameContains))
                {
                    return true;
                }

                return light.name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            public float Evaluate(float timeOfDay)
            {
                float scale = Mathf.Max(0f, intensityMultiplier);

                if (intensityByHour == null || intensityByHour.length == 0)
                {
                    return scale;
                }

                // Кривая — доля 0…1 от числового множителя, а не абсолютная яркость:
                // чтобы поднять/опустить всю группу, правится одно число, форма перепадов остаётся.
                return Mathf.Max(0f, intensityByHour.Evaluate(Mathf.Clamp(timeOfDay, 0f, 24f))) * scale;
            }

            /// <summary>Гарантирует, что блок переопределений существует (новое поле в старом сериализованном ассете).</summary>
            public LightOverrides EnsureOverrides()
            {
                if (overrides == null)
                {
                    overrides = new LightOverrides();
                }

                return overrides;
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

        /// <summary>
        /// Авторские (базовые) значения источника плюс текущее состояние применения.
        /// Базовые значения снимаются один раз при первом обнаружении, поэтому
        /// переопределения группы никогда не «съедают» исходные настройки сцены.
        /// </summary>
        private sealed class LightState
        {
            public float baseIntensity;
            public float baseRange;
            public Color baseColor;
            public LightType baseType;
            public float baseSpotAngle;
            public float baseInnerSpotAngle;
            public bool baseUseColorTemperature;
            public float baseColorTemperature;
            public LightShadows baseShadows;
            public float baseShadowStrength;
            public float baseBounceIntensity;

            /// <summary>Группа, чьи переопределения сейчас применены (null = применены базовые значения).</summary>
            public LightGroup appliedGroup;

            /// <summary>Интенсивность без мерцания — сюда идёт плавный переход.</summary>
            public float smoothedIntensity;
        }

        private readonly List<Light> _trackedLights = new List<Light>();
        private readonly Dictionary<Light, LightState> _states = new Dictionary<Light, LightState>();

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
            EnsureGroupData();
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
                if (!_states.ContainsKey(light))
                {
                    _states.Add(light, CaptureState(light));
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
                if (light == null || !_states.TryGetValue(light, out LightState state))
                {
                    continue;
                }

                LightGroup group = FindGroup(light);

                if (!ReferenceEquals(group, state.appliedGroup))
                {
                    // Группа сменилась: сначала вернуть авторские значения, потом применить новые.
                    RestoreBaseline(light, state);
                    ApplyOverrides(light, group);
                    state.appliedGroup = group;
                }

                float multiplier = group != null ? group.Evaluate(timeOfDay) : 1f;
                float targetIntensity = Mathf.Max(0f, state.baseIntensity * multiplier);

                state.smoothedIntensity = immediate
                    ? targetIntensity
                    : Mathf.Lerp(state.smoothedIntensity, targetIntensity, blend);

                light.intensity = state.smoothedIntensity;
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

        private static LightState CaptureState(Light light)
        {
            return new LightState
            {
                baseIntensity = light.intensity,
                baseRange = light.range,
                baseColor = light.color,
                baseType = light.type,
                baseSpotAngle = light.spotAngle,
                baseInnerSpotAngle = light.innerSpotAngle,
                baseUseColorTemperature = light.useColorTemperature,
                baseColorTemperature = light.colorTemperature,
                baseShadows = light.shadows,
                baseShadowStrength = light.shadowStrength,
                baseBounceIntensity = light.bounceIntensity,
                smoothedIntensity = light.intensity
            };
        }

        /// <summary>Возврат к авторским значениям источника (снимаются при первом обнаружении).</summary>
        private static void RestoreBaseline(Light light, LightState state)
        {
            if (light == null || state == null)
            {
                return;
            }

            light.type = state.baseType;
            light.range = state.baseRange;
            light.color = state.baseColor;
            light.spotAngle = state.baseSpotAngle;
            light.innerSpotAngle = state.baseInnerSpotAngle;
            light.useColorTemperature = state.baseUseColorTemperature;
            light.colorTemperature = state.baseColorTemperature;
            light.shadows = state.baseShadows;
            light.shadowStrength = state.baseShadowStrength;
            light.bounceIntensity = state.baseBounceIntensity;
        }

        /// <summary>Применение включённых переопределений группы. Тип ставится первым: от него зависят углы Spot.</summary>
        private static void ApplyOverrides(Light light, LightGroup group)
        {
            if (light == null || group == null)
            {
                return;
            }

            LightOverrides o = group.EnsureOverrides();

            if (o.overrideType && o.type != LightType.Directional)
            {
                light.type = o.type;
            }

            if (o.overrideRange)
            {
                light.range = Mathf.Max(0.01f, o.range);
            }

            if (light.type == LightType.Spot)
            {
                if (o.overrideSpotAngle)
                {
                    light.spotAngle = Mathf.Clamp(o.spotAngle, 1f, 179f);
                }

                if (o.overrideInnerSpotAngle)
                {
                    light.innerSpotAngle = Mathf.Clamp(o.innerSpotAngle, 0f, Mathf.Max(0f, light.spotAngle - 0.1f));
                }
            }

            if (o.overrideColor)
            {
                light.color = new Color(o.color.r, o.color.g, o.color.b, 1f);
            }

            if (o.overrideUseColorTemperature)
            {
                light.useColorTemperature = o.useColorTemperature;
            }

            if (o.overrideColorTemperature)
            {
                light.colorTemperature = Mathf.Clamp(o.colorTemperature, 1000f, 20000f);
            }

            if (o.overrideShadows)
            {
                light.shadows = o.shadows;
            }

            if (o.overrideShadowStrength)
            {
                light.shadowStrength = Mathf.Clamp01(o.shadowStrength);
            }

            if (o.overrideBounceIntensity)
            {
                light.bounceIntensity = Mathf.Max(0f, o.bounceIntensity);
            }
        }

        private void EnsureDefaultGroups()
        {
            if (groups == null || groups.Length == 0)
            {
                groups = CreateDefaultGroups();
            }
        }

        private void EnsureGroupData()
        {
            if (groups == null)
            {
                return;
            }

            foreach (LightGroup group in groups)
            {
                if (group != null)
                {
                    group.EnsureOverrides();
                }
            }
        }

        private static LightGroup CreatePrimumOutdoorGroup()
        {
            return new LightGroup
            {
                groupId = "PRIMUM_OUTLIGHT",
                nameContains = "PRIMUM_OUTLIGHT",
                intensityByHour = CreateCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(5f, 1f),
                    new Keyframe(6f, 0f),
                    new Keyframe(17f, 0f),
                    new Keyframe(19f, 0.25f),
                    new Keyframe(21f, 1f),
                    new Keyframe(24f, 1f))
            };
        }

        private static LightGroup CreatePrimumIndoorGroup()
        {
            return new LightGroup
            {
                groupId = "PRIMUM_INLIGHT",
                nameContains = "PRIMUM_INLIGHT",
                intensityByHour = CreateCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(5f, 1f),
                    new Keyframe(6f, 0f),
                    new Keyframe(17f, 0f),
                    new Keyframe(19f, 0.25f),
                    new Keyframe(21f, 1f),
                    new Keyframe(24f, 1f))
            };
        }

        private static LightGroup[] CreateDefaultGroups()
        {
            return new[]
            {
                CreatePrimumOutdoorGroup(),
                CreatePrimumIndoorGroup(),
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
