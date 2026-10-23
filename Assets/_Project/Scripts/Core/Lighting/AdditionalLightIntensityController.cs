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

            [Header("Мерцание (неисправные лампы)")]
            [Tooltip("Мерцание только для этой группы. Каждый источник мерцает по своему графику: общей синхронной пульсации группы нет.")]
            public bool flickerEnabled;

            [Tooltip("Нижняя граница частоты событий мерцания, Гц (событий в секунду). Для каждого источника и каждого события частота выбирается заново из диапазона [min…max].")]
            [Min(0.01f)] public float flickerHzMin = 0.5f;

            [Tooltip("Верхняя граница частоты событий мерцания, Гц.")]
            [Min(0.01f)] public float flickerHzMax = 3f;

            [Tooltip("Минимальная глубина провала яркости — доля от текущей интенсивности источника.")]
            [Range(0f, 1f)] public float flickerDepthMin = 0.15f;

            [Tooltip("Максимальная глубина провала яркости. 1 = лампа гаснет полностью.")]
            [Range(0f, 1f)] public float flickerDepthMax = 0.6f;

            [Tooltip("Минимальная длительность одного провала, с.")]
            [Min(0.005f)] public float flickerDurationMin = 0.04f;

            [Tooltip("Максимальная длительность одного провала, с.")]
            [Min(0.005f)] public float flickerDurationMax = 0.12f;

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

            /// <summary>
            /// Мерцание добавлено позже первой сериализации сцен, поэтому нулевые/перевёрнутые
            /// границы восстанавливаются к осмысленным значениям, а не ломают расчёт частоты.
            /// </summary>
            public void SanitizeFlicker()
            {
                if (flickerHzMin <= 0f) flickerHzMin = 0.5f;
                if (flickerHzMax <= 0f) flickerHzMax = 3f;

                if (flickerHzMax < flickerHzMin)
                {
                    float swap = flickerHzMin;
                    flickerHzMin = flickerHzMax;
                    flickerHzMax = swap;
                }

                if (flickerDepthMin <= 0f && flickerDepthMax <= 0f)
                {
                    flickerDepthMin = 0.15f;
                    flickerDepthMax = 0.6f;
                }

                if (flickerDurationMin <= 0f) flickerDurationMin = 0.04f;
                if (flickerDurationMax <= 0f) flickerDurationMax = 0.12f;
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

#if UNITY_EDITOR
        /// <summary>
        /// Доступ только для editor-инструментов (`LightGroupPlayModeCapture`): перенос настроек
        /// из Play Mode в редактируемую сцену. В рантайме этого свойства нет.
        /// </summary>
        public LightGroup[] EditorGroups
        {
            get => groups;
            set => groups = value;
        }
#endif

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

            /// <summary>Ревизия настроек на момент применения: правка в инспекторе поднимает ревизию.</summary>
            public int appliedRevision = -1;

            /// <summary>Интенсивность без мерцания — сюда идёт плавный переход.</summary>
            public float smoothedIntensity;

            // Мерцание: у каждого источника свой ГПСЧ и свой график провалов.
            public FlickerRandom flickerRandom;
            public bool flickerScheduled;
            public float flickerNextTime;
            public float flickerStartTime;
            public float flickerEndTime;
            public float flickerDepth;
        }

        /// <summary>
        /// Детерминированный xorshift-ГПСЧ на источник. Не трогает UnityEngine.Random
        /// (общий поток) и не даёт группе мерцать синхронно: у каждой лампы свои числа.
        /// </summary>
        private struct FlickerRandom
        {
            private uint _state;

            public FlickerRandom(uint seed)
            {
                _state = seed != 0u ? seed : 0x9E3779B9u;
            }

            public float Next01()
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return (_state & 0x00FFFFFFu) / 16777216f;
            }

            public float Range(float min, float max)
            {
                return min + (max - min) * Next01();
            }
        }

        private readonly List<Light> _trackedLights = new List<Light>();
        private readonly Dictionary<Light, LightState> _states = new Dictionary<Light, LightState>();

        private DayNightController _dayNightController;
        private float _currentTimeOfDay = 12f;
        private float _nextRescanTime;
        private bool _hasAppliedTime;
        private bool _anyFlickerEnabled;

        // Живое применение правок из инспектора. Поля не сериализуются — они отражают
        // только текущую сессию и не попадают в сцену.
        private int _settingsRevision;
        private bool _forceApply;

        private void Reset()
        {
            groups = CreateDefaultGroups();
        }

        private void Awake()
        {
            EnsureDefaultGroups();
            EnsureGroupData(true);
            _dayNightController = GetComponent<DayNightController>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Правка любого поля в инспекторе (в том числе в Play Mode) поднимает ревизию настроек
        /// и ставит флаг немедленного применения: переопределения группы должны работать как
        /// живая ручка, а не только после перезапуска Play Mode.
        /// </summary>
        private void OnValidate()
        {
            // Только EnsureGroupData(false): блок переопределений создаётся для старой сериализации,
            // но SanitizeFlicker здесь не вызывается — он перебивал бы ноль, пока значение набирают.
            EnsureGroupData(false);
            RefreshFlickerFlag();
            _settingsRevision++;
            _forceApply = true;
        }
#endif

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
            bool timeChanged = !_hasAppliedTime || !Mathf.Approximately(timeOfDay, _currentTimeOfDay);

            if (timeChanged)
            {
                _currentTimeOfDay = timeOfDay;
            }

            // Мерцание живёт в собственном времени: при замороженном времени суток
            // (paused day-night) провалы всё равно должны идти каждый кадр.
            // Свежая правка в инспекторе даёт немедленное применение.
            bool forceApply = _forceApply;
            _forceApply = false;

            if (forceApply || timeChanged || _anyFlickerEnabled)
            {
                ApplyIntensity(timeOfDay, forceApply);
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

            // Флаг пересчитывается вместе с рескном: включение мерцания в инспекторе в Play Mode
            // подхватывается не позже rescanInterval, без перезапуска.
            RefreshFlickerFlag();
        }

        private void RefreshFlickerFlag()
        {
            _anyFlickerEnabled = false;

            if (groups == null)
            {
                return;
            }

            foreach (LightGroup group in groups)
            {
                if (group != null && group.flickerEnabled)
                {
                    _anyFlickerEnabled = true;
                    return;
                }
            }
        }

        private void ApplyIntensity(float timeOfDay, bool immediate)
        {
            float blend = immediate ? 1f : 1f - Mathf.Exp(-transitionSpeed * Time.deltaTime);
            float now = Time.time;

            foreach (Light light in _trackedLights)
            {
                if (light == null || !_states.TryGetValue(light, out LightState state))
                {
                    continue;
                }

                LightGroup group = FindGroup(light);

                if (!ReferenceEquals(group, state.appliedGroup) || state.appliedRevision != _settingsRevision)
                {
                    // Группа сменилась или настройки правили в инспекторе (в т.ч. в Play Mode):
                    // сначала вернуть авторские значения, потом применить актуальные переопределения.
                    RestoreBaseline(light, state);
                    ApplyOverrides(light, group);
                    state.appliedGroup = group;
                    state.appliedRevision = _settingsRevision;
                }

                float multiplier = group != null ? group.Evaluate(timeOfDay) : 1f;
                float targetIntensity = Mathf.Max(0f, state.baseIntensity * multiplier);

                state.smoothedIntensity = immediate
                    ? targetIntensity
                    : Mathf.Lerp(state.smoothedIntensity, targetIntensity, blend);

                // Мерцание накладывается поверх сглаженной яркости: короткий провал не должен
                // проходить через Lerp, иначе сглаживание его съедает и лампы «не мерцают».
                float flicker = EvaluateFlickerFactor(light, state, group, now);
                light.intensity = state.smoothedIntensity * flicker;
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
                smoothedIntensity = light.intensity,
                flickerRandom = new FlickerRandom(HashName(light.name) ^ (uint)EntityId.ToULong(light.GetEntityId()))
            };
        }

        private static uint HashName(string name)
        {
            uint hash = 2166136261u;

            if (!string.IsNullOrEmpty(name))
            {
                for (int i = 0; i < name.Length; i++)
                {
                    hash ^= name[i];
                    hash *= 16777619u;
                }
            }

            return hash;
        }

        /// <summary>
        /// Множитель мерцания 0…1 поверх уже сглаженной яркости. Провал — короткий треугольный
        /// импульс; частота, глубина и длительность перебрасываются заново на каждое событие,
        /// поэтому синхронной пульсации группы не возникает.
        /// </summary>
        private static float EvaluateFlickerFactor(Light light, LightState state, LightGroup group, float now)
        {
            if (group == null || !group.flickerEnabled || !light.enabled || state.baseIntensity <= 0f)
            {
                // Пул источников (PrimumLightPool) выключает дальние лампы: не планируем события
                // для невидимого света, чтобы при включении не сработал провал «из прошлого».
                state.flickerScheduled = false;
                return 1f;
            }

            float hzMin = Mathf.Clamp(Mathf.Min(group.flickerHzMin, group.flickerHzMax), 0.01f, 60f);
            float hzMax = Mathf.Clamp(Mathf.Max(group.flickerHzMin, group.flickerHzMax), hzMin, 60f);

            if (!state.flickerScheduled)
            {
                state.flickerScheduled = true;
                state.flickerStartTime = now;
                state.flickerEndTime = now;
                state.flickerNextTime = now + NextFlickerInterval(ref state.flickerRandom, hzMin, hzMax);
            }

            if (now >= state.flickerNextTime)
            {
                float durationMin = Mathf.Min(group.flickerDurationMin, group.flickerDurationMax);
                float durationMax = Mathf.Max(group.flickerDurationMin, group.flickerDurationMax);
                float depthMin = Mathf.Min(group.flickerDepthMin, group.flickerDepthMax);
                float depthMax = Mathf.Max(group.flickerDepthMin, group.flickerDepthMax);

                state.flickerStartTime = now;
                state.flickerEndTime = now + Mathf.Max(0.005f, state.flickerRandom.Range(durationMin, durationMax));
                state.flickerDepth = Mathf.Clamp01(state.flickerRandom.Range(depthMin, depthMax));

                // Пауза до следующего события тоже случайная и со своей частотой из [min…max].
                state.flickerNextTime = state.flickerEndTime + NextFlickerInterval(ref state.flickerRandom, hzMin, hzMax);
            }

            if (now >= state.flickerEndTime)
            {
                return 1f;
            }

            float u = Mathf.InverseLerp(state.flickerStartTime, state.flickerEndTime, now);
            float shape = 1f - Mathf.Abs(2f * u - 1f);
            return 1f - state.flickerDepth * shape;
        }

        private static float NextFlickerInterval(ref FlickerRandom random, float hzMin, float hzMax)
        {
            float hz = random.Range(hzMin, hzMax);
            return 1f / Mathf.Max(0.01f, hz);
        }

        /// <summary>
        /// Возврат к авторским значениям источника (снимаются при первом обнаружении).
        /// Запись идёт только если значение реально отличается: правка в инспекторе
        /// прогоняет этот метод по всем источникам, и лишние обращения к нативному
        /// свойству (особенно к Light.type) дёргают рендер-пайплайн зря.
        /// </summary>
        private static void RestoreBaseline(Light light, LightState state)
        {
            if (light == null || state == null)
            {
                return;
            }

            if (light.type != state.baseType) light.type = state.baseType;
            if (!Mathf.Approximately(light.range, state.baseRange)) light.range = state.baseRange;
            if (light.color != state.baseColor) light.color = state.baseColor;
            if (!Mathf.Approximately(light.spotAngle, state.baseSpotAngle)) light.spotAngle = state.baseSpotAngle;
            if (!Mathf.Approximately(light.innerSpotAngle, state.baseInnerSpotAngle)) light.innerSpotAngle = state.baseInnerSpotAngle;
            if (light.useColorTemperature != state.baseUseColorTemperature) light.useColorTemperature = state.baseUseColorTemperature;
            if (!Mathf.Approximately(light.colorTemperature, state.baseColorTemperature)) light.colorTemperature = state.baseColorTemperature;
            if (light.shadows != state.baseShadows) light.shadows = state.baseShadows;
            if (!Mathf.Approximately(light.shadowStrength, state.baseShadowStrength)) light.shadowStrength = state.baseShadowStrength;
            if (!Mathf.Approximately(light.bounceIntensity, state.baseBounceIntensity)) light.bounceIntensity = state.baseBounceIntensity;
        }

        /// <summary>Применение включённых переопределений группы. Тип ставится первым: от него зависят углы Spot.</summary>
        private static void ApplyOverrides(Light light, LightGroup group)
        {
            if (light == null || group == null)
            {
                return;
            }

            LightOverrides o = group.EnsureOverrides();

            if (o.overrideType && o.type != LightType.Directional && light.type != o.type)
            {
                light.type = o.type;
            }

            if (o.overrideRange)
            {
                float range = Mathf.Max(0.01f, o.range);
                if (!Mathf.Approximately(light.range, range)) light.range = range;
            }

            if (light.type == LightType.Spot)
            {
                if (o.overrideSpotAngle)
                {
                    float spotAngle = Mathf.Clamp(o.spotAngle, 1f, 179f);
                    if (!Mathf.Approximately(light.spotAngle, spotAngle)) light.spotAngle = spotAngle;
                }

                if (o.overrideInnerSpotAngle)
                {
                    float innerSpotAngle = Mathf.Clamp(o.innerSpotAngle, 0f, Mathf.Max(0f, light.spotAngle - 0.1f));
                    if (!Mathf.Approximately(light.innerSpotAngle, innerSpotAngle)) light.innerSpotAngle = innerSpotAngle;
                }
            }

            if (o.overrideColor)
            {
                Color color = new Color(o.color.r, o.color.g, o.color.b, 1f);
                if (light.color != color) light.color = color;
            }

            if (o.overrideUseColorTemperature && light.useColorTemperature != o.useColorTemperature)
            {
                light.useColorTemperature = o.useColorTemperature;
            }

            if (o.overrideColorTemperature)
            {
                float temperature = Mathf.Clamp(o.colorTemperature, 1000f, 20000f);
                if (!Mathf.Approximately(light.colorTemperature, temperature)) light.colorTemperature = temperature;
            }

            if (o.overrideShadows && light.shadows != o.shadows)
            {
                light.shadows = o.shadows;
            }

            if (o.overrideShadowStrength)
            {
                float shadowStrength = Mathf.Clamp01(o.shadowStrength);
                if (!Mathf.Approximately(light.shadowStrength, shadowStrength)) light.shadowStrength = shadowStrength;
            }

            if (o.overrideBounceIntensity)
            {
                float bounceIntensity = Mathf.Max(0f, o.bounceIntensity);
                if (!Mathf.Approximately(light.bounceIntensity, bounceIntensity)) light.bounceIntensity = bounceIntensity;
            }
        }

        private void EnsureDefaultGroups()
        {
            if (groups == null || groups.Length == 0)
            {
                groups = CreateDefaultGroups();
            }
        }

        private void EnsureGroupData(bool sanitizeFlicker)
        {
            if (groups == null)
            {
                return;
            }

            foreach (LightGroup group in groups)
            {
                if (group == null)
                {
                    continue;
                }

                group.EnsureOverrides();

                if (sanitizeFlicker)
                {
                    group.SanitizeFlicker();
                }
            }

            RefreshFlickerFlag();
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
