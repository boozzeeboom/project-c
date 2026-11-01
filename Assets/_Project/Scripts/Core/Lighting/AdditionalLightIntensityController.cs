using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectC.World;

namespace ProjectC.Core
{
    /// <summary>
    /// Global client-side controller for additional lights.
    /// Discovers Point/Spot/Area lights in all loaded scenes and applies, per group, a
    /// time-of-day intensity curve plus optional property overrides (range, color, type,
    /// spot angles, shadows, bounce) without touching the authored values stored in the scenes.
    /// </summary>
    /// <remarks>
    /// T-LIGHT17b (L2b светового трека). До этой задачи каждый вызов ApplyIntensity писал
    /// Light.intensity всем 2 238 отслеживаемым источникам (5.4–6.1 мс/кадр) при 70–96
    /// включённых. Теперь применяется активный набор:
    ///   • Light.enabled — владелец PrimumLightPool; он отдаёт событие ActiveLightChanged,
    ///     версию набора (ActiveSetVersion) и копию (CopyActiveLights);
    ///   • фиктивные/неактивные источники не пишутся покадрово: пул рисует только активные,
    ///     а его день/ночь-гейт читает intensity — поэтому погашенные источники всё равно
    ///     получают запись, но только на смене «светит/не светит», а не каждый кадр;
    ///   • включённый источник получает актуальную яркость тем же кадром (нет тёмного/яркого кадра);
    ///   • кривые, группы, переопределения, сглаживание и мерцание — без изменений.
    /// Настройки группы, override и flicker продолжают применяться к источнику, как только
    /// он снова попадает в активный набор или в базовую волну.
    ///
    /// T-LIGHT21 (L6 светового трека) — плавность включения/гашения. Fade живёт здесь, потому что
    /// Light.intensity принадлежит этому компоненту (мерцание пишет её каждый кадр):
    ///   • в LightState добавлены вес fadeWeight (0…1) и цель fadeTarget (0/1); итоговая запись
    ///     value = smoothedIntensity · flicker · fadeWeight — мерцание остаётся поверх веса,
    ///     кривая суток и сглаживание не меняются;
    ///   • включение пулом (ActiveLightChanged) начинает fade-in от нуля за fadeInSeconds (0.15 с),
    ///     повторный вход/resync уже горящего источника перехода не перезапускает;
    ///   • fade-out (0.30 с) сделан ТОЛЬКО через контракт с пулом: пул перед записью
    ///     Light.enabled = false спрашивает HoldEnabled, и, если ALIC ещё гасит, источник
    ///     остаётся включённым. Владелец enabled — по-прежнему пул, intensity — ALIC;
    ///   • immediate-пути (старт сцены, правка конфига) веса не сдерживают: там fade не работает,
    ///     чтобы не было тёмного кадра;
    ///   • для замера профиля включения пишется проба FADEPROFILE (кадр/вес/яркость, t90, скачок).
    /// </remarks>
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

        [Header("T-LIGHT21: плавность включения/гашения (fade-множитель)")]
        [Tooltip("Плавно появлять/гасить источник (fade-множитель к Light.intensity). Выключено = мгновенное включение, как до T-LIGHT21 (A/B-контроль).")]
        [SerializeField] private bool fadeEnabled = true;

        [Tooltip("Время появления источника от 0 до полной яркости, с.")]
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.15f;

        [Tooltip("Время гашения источника до нуля, с. Пул держит Light.enabled, пока ALIC не закончит переход (жёсткий дедлайн fadeOut + 0.05 с).\n0 = мгновенное гашение (значение по умолчанию). T-LIGHT21 измерил fade-out 0.30 с и отклонил его как дефолт: при массовом выходе ламп из набора удержание достигало 75 источников против гейта ≤16.")]
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0f;

        [Header("Runtime Response")]
        [SerializeField, Min(0.1f)] private float transitionSpeed = 4f;
        [SerializeField] private bool logDiscovery = false;

        [Header("T-LIGHT17b: активный набор (CPU)")]
        [Tooltip("Применять Light.intensity только активным (включённым пулом) и изменённым источникам.\nВыключено = поведение до T-LIGHT17b: запись всем отслеживаемым источникам каждый кадр (A/B-контроль).")]
        [SerializeField] private bool activeSetEnabled = true;

        [Tooltip("Порог изменения группового множителя, после которого переписываются погашенные (неактивные) источники.\nПул читает их intensity в день/ночь-гейте, поэтому значение должно быть свежим, но не обязательно покадровым.")]
        [SerializeField, Min(0.0001f)] private float baseSweepEpsilon = 0.01f;

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

            // T-LIGHT17b: состояние применения для активного набора.
            /// <summary>Группа, назначенная источнику (кэш последнего разрешения групп).</summary>
            public LightGroup group;
            /// <summary>Индекс группы в массиве groups (–1 = ни одна не подошла).</summary>
            public int groupIndex = -1;
            /// <summary>Целевая яркость без мерцания на последнем применении.</summary>
            public float target;
            /// <summary>Значение, реально записанное в Light.intensity (для пропуска одинаковых записей).</summary>
            public float lastWritten = float.PositiveInfinity;
            /// <summary>Последнее записанное значение было «светит» по порогу день/ночь-гейта пула.</summary>
            public bool lastWrittenEmitting;

            // Мерцание: у каждого источника свой ГПСЧ и свой график провалов.
            public FlickerRandom flickerRandom;
            public bool flickerScheduled;
            public float flickerNextTime;
            public float flickerStartTime;
            public float flickerEndTime;
            public float flickerDepth;

            // T-LIGHT21: плавность. Вес — множитель к уже посчитанной яркости, поэтому
            // flicker и кривая суток остаются нетронутыми.
            /// <summary>Множитель плавности 0…1 (0 = источник погашен, 1 = полная яркость). Старт — 0: первое включение плавное.</summary>
            public float fadeWeight;
            /// <summary>Цель веса: 1 = горит/разгорается, 0 = гаснет.</summary>
            public float fadeTarget = 1f;
            /// <summary>Время начала текущего fade-in (Time.time) — для t90 и ошибки профиля.</summary>
            public float fadeInStartTime;
            /// <summary>Время начала текущего fade-out — основа жёсткого дедлайна удержания enabled.</summary>
            public float fadeOutStartTime;
            /// <summary>fade-out уже начат: таймер и счётчик не перезапускаются повторным вопросом пула.</summary>
            public bool fadeOutActive;
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

        // ---------------------------------------------------------------- T-LIGHT17b
        // Активный набор. Пул — владелец enabled, поэтому его событие/версия — единственный источник
        // истины о том, какие источники реально в кадре. Мировые Vector3 не кэшируются (правило FO).
        private PrimumLightPool _pool;
        private bool _poolEventHooked;
        private int _poolVersionSeen = -1;
        private readonly HashSet<Light> _activeLights = new HashSet<Light>();
        private readonly List<Light> _syncScratch = new List<Light>(512);
        // Множители групп считаются один раз на кадр (4 кривые) и переиспользуются всеми проходами.
        private float[] _groupMultipliers = new float[0];
        private float[] _groupMultipliersApplied = new float[0];
        private bool _multipliersInitialized;

        // ---------------------------------------------------------------- T-LIGHT16
        // Диагностика L1 светового трека: маркер Profiler и счётчики. Ни одна ветка решения
        // (группа, кривая, сглаживание, мерцание, переопределения) не изменена — новые счётчики
        // только читают то, что и так применяется в этом же вызове.
        public const string ApplyMarkerName = "ALIC.ApplyIntensity";
        private static readonly ProfilerMarker ApplyMarker = new ProfilerMarker(ApplyMarkerName);
        private const float IntensityZeroEpsilon = 0.001f;
        /// <summary>T-LIGHT17b: меньше этого значения запись в Light.intensity не выполняется.</summary>
        private const float IntensityWriteEpsilon = 1e-4f;
        /// <summary>T-LIGHT17b: относительный порог сдвига группового множителя (доля от множителя).</summary>
        private const float BaseSweepRelativeEpsilon = 0.005f;
        /// <summary>T-LIGHT21: ниже этого веса источник считается полностью погашенным.</summary>
        private const float FadeWeightEpsilon = 0.001f;
        /// <summary>T-LIGHT21: запас жёсткого дедлайна удержания enabled сверх fadeOutSeconds.</summary>
        private const float FadeOutDeadlineSlack = 0.05f;
        /// <summary>T-LIGHT21: сколько кадров пробы профиля хранится для отчёта.</summary>
        private const int FadeProfileCapacity = 64;

        /// <summary>Сколько источников в списке отслеживаемых на последнем рескне.</summary>
        public int TrackedLightCount => _trackedLights.Count;
        /// <summary>Сколько источников получило запись intensity в последнем Apply.</summary>
        public int AppliedLastCall { get; private set; }
        /// <summary>Из них включённых (не погашенных пулом) — реально влияют на кадр.</summary>
        public int ActiveAppliedLastCall { get; private set; }
        /// <summary>Сколько отслеживаемых источников имеют intensity ≈ 0 после Apply.</summary>
        public int ZeroIntensityTracked { get; private set; }
        /// <summary>Длительность последнего Apply, мс (Stopwatch, только диагностика).</summary>
        public double LastApplyMs { get; private set; }
        public double MaxApplyMs { get; private set; }
        /// <summary>Сколько раз ApplyIntensity вызван в текущем кадре.</summary>
        public int ApplyCallsThisFrame { get; private set; }
        public int ApplyCallCount { get; private set; }
        /// <summary>Текущее время суток, которое применяет контроллер.</summary>
        public float CurrentTimeOfDay => _currentTimeOfDay;
        public bool FlickerAnyEnabled => _anyFlickerEnabled;
        public int GroupCount => groups != null ? groups.Length : 0;

        // ---------------------------------------------------------------- T-LIGHT17b
        /// <summary>Активный набор включён (T-LIGHT17b): запись только активным/изменённым.</summary>
        public bool ActiveSetEnabled => activeSetEnabled;
        /// <summary>Сколько источников в активном наборе (последняя синхронизация с пулом).</summary>
        public int ActiveSetSize => _activeLights.Count;
        /// <summary>Пул найден и отдаёт активный набор.</summary>
        public bool PoolBound => _pool != null;
        /// <summary>Сколько раз активный набор пересобирался по версии пула.</summary>
        public int ActiveSetResyncs { get; private set; }
        /// <summary>Записей Light.intensity за последний кадр.</summary>
        public int WritesThisFrame { get; private set; }
        /// <summary>Из них включённым источникам.</summary>
        public int ActiveWritesThisFrame { get; private set; }
        /// <summary>Источников просмотрено за последний кадр (включая неактивные в базовой волне).</summary>
        public int IteratedThisFrame { get; private set; }
        /// <summary>Базовая волна (все группы) выполнялась в этом кадре.</summary>
        public bool BaseSweepThisFrame { get; private set; }
        /// <summary>Групповых множителей изменено в последнем кадре (1/0) — для CSV.</summary>
        public bool MultipliersMovedThisFrame { get; private set; }

        // ---------------------------------------------------------------- T-LIGHT21

        /// <summary>Плавность включена (иначе — поведение до L6: мгновенная запись).</summary>
        public bool FadeEnabled => fadeEnabled;
        /// <summary>Длительность fade-in, с.</summary>
        public float FadeInSeconds => fadeInSeconds;
        /// <summary>Длительность fade-out, с.</summary>
        public float FadeOutSeconds => fadeOutSeconds;
        /// <summary>Сколько активных источников сейчас в переходе (0 &lt; вес &lt; 1).</summary>
        public int FadeActiveCount { get; private set; }
        /// <summary>Сколько fade-in'ов начато с запуска (нарастающий итог).</summary>
        public int FadeInCount { get; private set; }
        /// <summary>Сколько fade-out'ов начато с запуска (нарастающий итог).</summary>
        public int FadeOutCount { get; private set; }
        /// <summary>Максимальное расхождение фактического веса с линейной кривой fade-in (0 = строго линейно).</summary>
        public float FadeMaxWeightError { get; private set; }
        /// <summary>Длительность последнего покадрового прохода активного набора (fade + мерцание), мс.</summary>
        public double LastFadeMs { get; private set; }
        /// <summary>Сколько кадров записано в пробе профиля включения.</summary>
        public int FadeProfileCount => _fadeProfileCount;
        /// <summary>Имя источника, на котором снят профиль включения.</summary>
        public string FadeProfileLightName => _fadeProfileName;
        /// <summary>Кадры до 90 % яркости в пробе (–1 = не достигнуто/профиль пуст).</summary>
        public int FadeProfileT90Frames => _fadeProfileT90Frames;
        /// <summary>Секунды до 90 % яркости в пробе (–1 = не достигнуто/профиль пуст).</summary>
        public float FadeProfileT90Sec => _fadeProfileT90Sec;
        /// <summary>Максимальный скачок яркости за кадр, доля от целевой (1.0 = мгновенное включение).</summary>
        public float FadeProfileMaxJumpRatio => _fadeProfileMaxJumpRatio;

        private Light _fadeProfileLight;
        private string _fadeProfileName = "none";
        private float _fadeProfileStartTime;
        private int _fadeProfileStartFrame;
        private float _fadeProfileTarget;
        private float _fadeProfilePrevValue;
        private int _fadeProfileT90Frames = -1;
        private float _fadeProfileT90Sec = -1f;
        private float _fadeProfileMaxJumpRatio;
        private int _fadeProfileCount;
        private readonly int[] _fadeProfileFrames = new int[FadeProfileCapacity];
        private readonly float[] _fadeProfileWeights = new float[FadeProfileCapacity];
        private readonly float[] _fadeProfileValues = new float[FadeProfileCapacity];

        /// <summary>Строка конфигурации fade для raw-лога/отчёта (рядом с ConfigLine пула).</summary>
        public string FadeConfigLine
        {
            get
            {
                var c = System.Globalization.CultureInfo.InvariantCulture;
                return "fade;enabled=" + (fadeEnabled ? "on" : "off") +
                       ";in=" + fadeInSeconds.ToString("F2", c) +
                       ";out=" + fadeOutSeconds.ToString("F2", c) +
                       ";gracePool=" + (_pool != null ? _pool.GateGraceSeconds.ToString("F2", c) : "-1") +
                       ";active=" + FadeActiveCount +
                       ";inCount=" + FadeInCount +
                       ";outCount=" + FadeOutCount +
                       ";heldForFade=" + (_pool != null ? _pool.HeldForFade : -1) +
                       ";gateGraceHeld=" + (_pool != null ? _pool.GateGraceHeld : -1) +
                       ";owner=intensity=AdditionalLightIntensityController;enabled=PrimumLightPool";
            }
        }

        /// <summary>
        /// T-LIGHT21: A/B-переключатель плавности в памяти. Сцена и ассеты не пишутся; следующий Apply
        /// выполняется немедленно (immediate-путь не сдерживается весом, чтобы не было тёмного кадра).
        /// </summary>
        public void ApplyFadeConfig(bool enabled, float inSeconds, float outSeconds)
        {
            fadeEnabled = enabled;
            fadeInSeconds = Mathf.Max(0f, inSeconds);
            fadeOutSeconds = Mathf.Max(0f, outSeconds);
            _forceApply = true;
        }

        /// <summary>Сбросить пробу профиля включения (бенч делает это перед разворотом камеры).</summary>
        public void ResetFadeProfile()
        {
            _fadeProfileLight = null;
            _fadeProfileName = "none";
            _fadeProfileCount = 0;
            _fadeProfileT90Frames = -1;
            _fadeProfileT90Sec = -1f;
            _fadeProfileMaxJumpRatio = 0f;
            _fadeProfileTarget = 0f;
            _fadeProfilePrevValue = 0f;
            FadeMaxWeightError = 0f;
        }

        /// <summary>Сэмпл пробы профиля включения: кадр / вес / записанная яркость.</summary>
        public void GetFadeProfileSample(int index, out int frame, out float weight, out float intensity)
        {
            if (index < 0 || index >= _fadeProfileCount)
            {
                frame = -1;
                weight = -1f;
                intensity = -1f;
                return;
            }

            frame = _fadeProfileFrames[index];
            weight = _fadeProfileWeights[index];
            intensity = _fadeProfileValues[index];
        }

        /// <summary>
        /// Строка конфигурации ALIC для raw-лога/отчёта: тот же формат, что ConfigLine у пула,
        /// чтобы владение и режим были зафиксированы рядом с числами.
        /// </summary>
        public string ConfigLine
        {
            get
            {
                var c = System.Globalization.CultureInfo.InvariantCulture;
                return "alic;activeSet=" + (activeSetEnabled ? "on" : "off") +
                       ";poolBound=" + (_pool != null) +
                       ";tracked=" + _trackedLights.Count +
                       ";active=" + _activeLights.Count +
                       ";baseSweepEps=" + baseSweepEpsilon.ToString("F4", c) +
                       ";transitionSpeed=" + transitionSpeed.ToString("F2", c) +
                       ";flicker=" + (_anyFlickerEnabled ? "on" : "off") +
                       ";enabledOwner=PrimumLightPool;intensityOwner=AdditionalLightIntensityController";
            }
        }

        /// <summary>
        /// Переключить режим активного набора на лету (A/B-замер). Меняется только поле в памяти:
        /// сцена и ассеты не пишутся. Следующий Apply выполнит полную волну, чтобы сравнение было честным.
        /// </summary>
        public void ApplyActiveSetConfig(bool enabled)
        {
            if (activeSetEnabled == enabled) return;
            activeSetEnabled = enabled;
            _forceApply = true;
        }

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
            ApplyCallsThisFrame = 0;
            WritesThisFrame = 0;
            ActiveWritesThisFrame = 0;
            IteratedThisFrame = 0;
            BaseSweepThisFrame = false;

            if (Time.time >= _nextRescanTime)
            {
                RefreshLights();
                _nextRescanTime = Time.time + rescanInterval;
            }

            // T-LIGHT17b: пул может появиться после загрузки WorldScene — привязка ленивая.
            EnsurePoolBinding();
            SyncActiveSet();

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

            EvaluateGroupMultipliers(timeOfDay);
            MultipliersMovedThisFrame = MultipliersMoved();

            // С активным набором покадровая работа нужна только пока в кадре есть включённые
            // источники (сглаживание + мерцание); погашенные обновляются базовой волной по изменению.
            bool perFrame = UsesActiveSet ? _activeLights.Count > 0 : _anyFlickerEnabled;

            if (forceApply || timeChanged || perFrame || MultipliersMovedThisFrame)
            {
                ApplyIntensity(timeOfDay, forceApply);
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnhookPool();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnhookPool();
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

            // T-LIGHT17b: новый набор отслеживаемых источников — повод пересобрать активную копию.
            EnsurePoolBinding();
            SyncActiveSet();
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
            long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            using (ApplyMarker.Auto())
            {
                ApplyIntensityCore(timeOfDay, immediate);
            }
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - startTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            LastApplyMs = ms;
            if (ms > MaxApplyMs) MaxApplyMs = ms;
            ApplyCallsThisFrame++;
            ApplyCallCount++;
        }

        private void ApplyIntensityCore(float timeOfDay, bool immediate)
        {
            float now = Time.time;
            float blend = immediate ? 1f : 1f - Mathf.Exp(-transitionSpeed * Time.deltaTime);

            EvaluateGroupMultipliers(timeOfDay);

            if (!UsesActiveSet)
            {
                // Поведение до T-LIGHT17b (A/B-контроль и сцены без пула): каждый вызов
                // пишет intensity всем отслеживаемым источникам.
                ApplyAllCore(timeOfDay, blend, immediate, now);
                CommitGroupMultipliers();
                _hasAppliedTime = true;
                return;
            }

            if (immediate || MultipliersMoved())
            {
                ApplyBaseSweep(immediate);
                CommitGroupMultipliers();
                BaseSweepThisFrame = true;
            }

            // T-LIGHT21: immediate-путь (старт сцены, правка конфига/инспектора) весами не сдерживается —
            // иначе первый кадр после перезагрузки/правки был бы тёмным. Fade остаётся только на включении пулом.
            if (immediate && fadeEnabled)
            {
                foreach (Light active in _activeLights)
                {
                    if (active == null || !_states.TryGetValue(active, out LightState state)) continue;
                    state.fadeWeight = 1f;
                    state.fadeTarget = 1f;
                    state.fadeOutActive = false;
                }
            }

            ApplyActiveCore(blend, now);
            _hasAppliedTime = true;
        }

        /// <summary>Активный набор используется: включён флагом и пул отдаёт набор.</summary>
        private bool UsesActiveSet => activeSetEnabled && _pool != null;

        /// <summary>Порог «светит» — тот же, что у день/ночь-гейта пула (он читает intensity).</summary>
        private float EmitEpsilon
        {
            get
            {
                float poolEps = _pool != null ? _pool.DayGateEpsilon : 0f;
                return Mathf.Max(IntensityZeroEpsilon, poolEps);
            }
        }

        /// <summary>
        /// Контрольный путь T-LIGHT17b: ровно прежнее поведение — группа, кривая, сглаживание,
        /// мерцание и безусловная запись intensity каждому отслеживаемому источнику.
        /// Нужен для честного A/B: отличается от нового пути только отсутствием фильтра по набору.
        /// </summary>
        private void ApplyAllCore(float timeOfDay, float blend, bool immediate, float now)
        {
            int iterated = 0;
            int writes = 0;
            int activeWrites = 0;
            int zeroTracked = 0;

            foreach (Light light in _trackedLights)
            {
                if (light == null || !_states.TryGetValue(light, out LightState state))
                {
                    continue;
                }

                iterated++;

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

                state.group = group;

                float multiplier = group != null ? group.Evaluate(timeOfDay) : 1f;
                float targetIntensity = Mathf.Max(0f, state.baseIntensity * multiplier);

                state.target = targetIntensity;
                state.smoothedIntensity = immediate
                    ? targetIntensity
                    : Mathf.Lerp(state.smoothedIntensity, targetIntensity, blend);

                // Мерцание накладывается поверх сглаженной яркости: короткий провал не должен
                // проходить через Lerp, иначе сглаживание его съедает и лампы «не мерцают».
                float flicker = EvaluateFlickerFactor(light, state, group, now);
                float value = state.smoothedIntensity * flicker;
                light.intensity = value;
                state.lastWritten = value;
                state.lastWrittenEmitting = value > EmitEpsilon;

                // T-LIGHT16: счётчики (только чтение уже применённого значения).
                writes++;
                if (light.enabled) activeWrites++;
                if (value <= IntensityZeroEpsilon) zeroTracked++;
            }

            AddCounters(iterated, writes, activeWrites, zeroTracked);
        }

        /// <summary>
        /// Базовая волна T-LIGHT17b: применяется, когда сдвинулась кривая суток / группа /
        /// переопределения. Активные источники здесь не трогаются — у них своё покадровое
        /// сглаживание и мерцание. Погашенным сглаживание не нужно (их не рисуют), но пул ЧИТАЕТ
        /// их intensity в день/ночь-гейте, поэтому запись делается на смене «светит/не светит»
        /// и на заметном изменении, а не каждый кадр (70–96 пишутся, 2 100+ — нет).
        /// </summary>
        private void ApplyBaseSweep(bool immediate)
        {
            float emitEps = EmitEpsilon;
            int iterated = 0;
            int writes = 0;
            int zeroTracked = 0;

            foreach (Light light in _trackedLights)
            {
                if (light == null || !_states.TryGetValue(light, out LightState state))
                {
                    continue;
                }

                iterated++;
                EnsureGroupAssigned(light, state);

                float targetIntensity = Mathf.Max(0f, state.baseIntensity * MultiplierFor(state));
                state.target = targetIntensity;
                if (targetIntensity <= IntensityZeroEpsilon) zeroTracked++;

                if (_activeLights.Contains(light))
                {
                    continue; // активными занимается покадровый проход
                }

                state.smoothedIntensity = targetIntensity;

                bool emitting = targetIntensity > emitEps;
                float writeEps = Mathf.Max(IntensityZeroEpsilon, 0.02f * Mathf.Abs(state.baseIntensity));
                bool mustWrite = immediate ||
                                 emitting != state.lastWrittenEmitting ||
                                 Mathf.Abs(targetIntensity - state.lastWritten) > writeEps;

                if (mustWrite && WriteIntensity(light, state, targetIntensity, emitEps, true))
                {
                    writes++;
                }
            }

            AddCounters(iterated, writes, 0, zeroTracked);
        }

        /// <summary>
        /// Покадровый проход T-LIGHT17b: сглаживание перехода и мерцание — только включённым
        /// источникам (70–96 вместо 2 238). Значение пишется, только если реально изменилось.
        /// T-LIGHT21: здесь же меняется fade-вес — flicker остаётся поверх веса (амплитуда провалов та же),
        /// а лишние записи появляются только у гаснущих/разгорающихся источников.
        /// </summary>
        private void ApplyActiveCore(float blend, float now)
        {
            if (_activeLights.Count == 0)
            {
                FadeActiveCount = 0;
                return;
            }

            long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            float emitEps = EmitEpsilon;
            float dt = Time.deltaTime;
            float fadeInRate = fadeInSeconds > 0f ? 1f / fadeInSeconds : float.MaxValue;
            float fadeOutRate = fadeOutSeconds > 0f ? 1f / fadeOutSeconds : float.MaxValue;
            int iterated = 0;
            int writes = 0;
            int fading = 0;

            foreach (Light light in _activeLights)
            {
                if (light == null || !_states.TryGetValue(light, out LightState state))
                {
                    continue;
                }

                iterated++;

                float targetIntensity = Mathf.Max(0f, state.baseIntensity * MultiplierFor(state));
                state.target = targetIntensity;
                state.smoothedIntensity = Mathf.Lerp(state.smoothedIntensity, targetIntensity, blend);

                // T-LIGHT21: вес идёт к цели своей скоростью (включение — fadeIn, гашение — fadeOut).
                // MoveTowards не перескакивает цель, поэтому профиль монотонный и без overshoot.
                if (fadeEnabled && !Mathf.Approximately(state.fadeWeight, state.fadeTarget))
                {
                    float rate = state.fadeTarget > state.fadeWeight ? fadeInRate : fadeOutRate;
                    state.fadeWeight = Mathf.MoveTowards(state.fadeWeight, state.fadeTarget, rate * dt);
                }

                if (state.fadeWeight > FadeWeightEpsilon && state.fadeWeight < 1f - FadeWeightEpsilon) fading++;

                float flicker = EvaluateFlickerFactor(light, state, state.group, now);
                float value = state.smoothedIntensity * flicker * state.fadeWeight;
                if (WriteIntensity(light, state, value, emitEps, false))
                {
                    writes++;
                }

                NoteFadeSample(light, state, value);
            }

            FadeActiveCount = fading;
            LastFadeMs = (System.Diagnostics.Stopwatch.GetTimestamp() - startTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            AddCounters(iterated, writes, writes, -1);
        }

        // ---------------------------------------------------------------- T-LIGHT21: fade

        /// <summary>
        /// Начать fade-in источника, который только что включён пулом. Если вес уже не ноль (resync/re-notify
        /// горящей лампы или гаснущая лампа вернулась в набор), переход НЕ перезапускается — только цель ставится в 1.
        /// </summary>
        private void BeginFadeIn(Light light, LightState state, float targetIntensity)
        {
            state.fadeTarget = 1f;
            state.fadeOutActive = false;

            if (!fadeEnabled || fadeInSeconds <= 0f)
            {
                state.fadeWeight = 1f;
                BeginFadeProfile(light, targetIntensity);
                return;
            }

            if (state.fadeWeight > FadeWeightEpsilon) return;

            state.fadeWeight = 0f;
            state.fadeInStartTime = Time.time;
            FadeInCount++;
            BeginFadeProfile(light, targetIntensity);
        }

        /// <summary>
        /// T-LIGHT21: пул спрашивает разрешение на запись Light.enabled = false (контракт пула, владение не нарушено).
        /// Возврат true — источник остаётся включённым, пока ALIC гасит яркость. Дедлайн fadeOut + 0.05 с
        /// страхует от залипания, если событие/кадры потеряются.
        /// </summary>
        private bool ShouldHoldEnabledForFade(Light light)
        {
            if (!fadeEnabled || fadeOutSeconds <= 0f) return false;
            if (light == null) return false;
            if (!_states.TryGetValue(light, out LightState state)) return false;
            if (!_activeLights.Contains(light)) return false;
            if (state.fadeWeight <= FadeWeightEpsilon) return false;

            if (!state.fadeOutActive)
            {
                state.fadeOutActive = true;
                state.fadeOutStartTime = Time.time;
                FadeOutCount++;
            }
            else if (Time.time - state.fadeOutStartTime > fadeOutSeconds + FadeOutDeadlineSlack)
            {
                // Дедлайн вышел — не держим: пул выключает источник, следующий вход снова начнётся с нуля.
                state.fadeOutActive = false;
                state.fadeTarget = 0f;
                return false;
            }

            state.fadeTarget = 0f;
            return true;
        }

        /// <summary>
        /// T-LIGHT21: проба профиля включения. Запоминается один (первый после сброса) источник, чтобы
        /// отчёт мог показать кадр/вес/яркость, t90 и максимальный скачок за кадр. Только чтение.
        /// </summary>
        private void BeginFadeProfile(Light light, float targetValue)
        {
            if (_fadeProfileLight != null) return;

            _fadeProfileLight = light;
            _fadeProfileName = light != null ? light.name : "none";
            _fadeProfileCount = 0;
            _fadeProfileT90Frames = -1;
            _fadeProfileT90Sec = -1f;
            _fadeProfileMaxJumpRatio = 0f;
            _fadeProfileStartTime = Time.time;
            _fadeProfileStartFrame = Time.frameCount;
            _fadeProfileTarget = targetValue;
            _fadeProfilePrevValue = 0f;
            FadeMaxWeightError = 0f;
        }

        /// <summary>Кадровый сэмпл пробы профиля включения (пишется только для источника пробы).</summary>
        private void NoteFadeSample(Light light, LightState state, float value)
        {
            if (_fadeProfileLight == null || !ReferenceEquals(light, _fadeProfileLight)) return;

            if (_fadeProfileCount < FadeProfileCapacity)
            {
                _fadeProfileFrames[_fadeProfileCount] = Time.frameCount;
                _fadeProfileWeights[_fadeProfileCount] = state.fadeWeight;
                _fadeProfileValues[_fadeProfileCount] = value;
                _fadeProfileCount++;
            }

            if (_fadeProfileTarget > 1e-5f)
            {
                float jump = Mathf.Abs(value - _fadeProfilePrevValue) / _fadeProfileTarget;
                if (jump > _fadeProfileMaxJumpRatio) _fadeProfileMaxJumpRatio = jump;
            }

            _fadeProfilePrevValue = value;

            if (_fadeProfileT90Frames < 0 && state.fadeWeight >= 0.9f)
            {
                _fadeProfileT90Frames = Time.frameCount - _fadeProfileStartFrame;
                _fadeProfileT90Sec = Time.time - _fadeProfileStartTime;
            }

            if (fadeEnabled && fadeInSeconds > 0f && state.fadeTarget > 0.5f)
            {
                float expected = Mathf.Clamp01((Time.time - _fadeProfileStartTime) / fadeInSeconds);
                float error = Mathf.Abs(state.fadeWeight - expected);
                if (error > FadeMaxWeightError) FadeMaxWeightError = error;
            }
        }

        private bool WriteIntensity(Light light, LightState state, float value, float emitEps, bool force)
        {
            if (!force && Mathf.Abs(value - state.lastWritten) < IntensityWriteEpsilon) return false;

            light.intensity = value;
            state.lastWritten = value;
            state.lastWrittenEmitting = value > emitEps;
            return true;
        }

        /// <summary>Сброс накопленных за кадр счётчиков в публичные свойства (только диагностика).</summary>
        /// <param name="zeroTracked">Сколько источников погашено; –1 = не пересчитывалось в этом проходе.</param>
        private void AddCounters(int iterated, int writes, int activeWrites, int zeroTracked)
        {
            IteratedThisFrame += iterated;
            WritesThisFrame += writes;
            ActiveWritesThisFrame += activeWrites;
            AppliedLastCall = writes;
            ActiveAppliedLastCall = activeWrites;
            if (zeroTracked >= 0) ZeroIntensityTracked = zeroTracked;
        }

        // ---------------------------------------------------------------- T-LIGHT17b: группы

        private void EvaluateGroupMultipliers(float timeOfDay)
        {
            int count = groups != null ? groups.Length : 0;

            if (_groupMultipliers.Length != count)
            {
                _groupMultipliers = new float[count];
                _groupMultipliersApplied = new float[count];
                _multipliersInitialized = false;
            }

            for (int i = 0; i < count; i++)
            {
                _groupMultipliers[i] = groups[i] != null ? groups[i].Evaluate(timeOfDay) : 1f;
            }
        }

        private bool MultipliersMoved()
        {
            if (!_multipliersInitialized) return true;

            for (int i = 0; i < _groupMultipliers.Length; i++)
            {
                float value = _groupMultipliers[i];
                float applied = _groupMultipliersApplied[i];
                float threshold = Mathf.Max(baseSweepEpsilon, Mathf.Abs(value) * BaseSweepRelativeEpsilon);
                if (Mathf.Abs(value - applied) > threshold) return true;
            }

            return false;
        }

        private void CommitGroupMultipliers()
        {
            if (_groupMultipliersApplied.Length != _groupMultipliers.Length)
            {
                _groupMultipliersApplied = new float[_groupMultipliers.Length];
            }

            for (int i = 0; i < _groupMultipliers.Length; i++)
            {
                _groupMultipliersApplied[i] = _groupMultipliers[i];
            }

            _multipliersInitialized = true;
        }

        private float MultiplierFor(LightState state)
        {
            int index = state.groupIndex;
            if (index < 0 || index >= _groupMultipliers.Length) return 1f;
            return _groupMultipliers[index];
        }

        private int FindGroupIndex(Light light)
        {
            if (groups == null) return -1;

            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] != null && groups[i].Matches(light)) return i;
            }

            return -1;
        }

        private LightGroup GroupAt(int index)
        {
            return index >= 0 && groups != null && index < groups.Length ? groups[index] : null;
        }

        /// <summary>Разрешение группы + возврат/применение переопределений — только при смене группы или ревизии.</summary>
        private void EnsureGroupAssigned(Light light, LightState state)
        {
            int index = FindGroupIndex(light);
            LightGroup group = GroupAt(index);

            if (index != state.groupIndex ||
                !ReferenceEquals(group, state.appliedGroup) ||
                state.appliedRevision != _settingsRevision)
            {
                RestoreBaseline(light, state);
                ApplyOverrides(light, group);
                state.appliedGroup = group;
                state.groupIndex = index;
                state.appliedRevision = _settingsRevision;
            }

            state.group = group;
        }

        // ------------------------------------------------------- T-LIGHT17b: активный набор

        private void EnsurePoolBinding()
        {
            if (_pool == null)
            {
                _pool = FindAnyObjectByType<PrimumLightPool>();
                if (_pool == null) return;

                _poolVersionSeen = -1;
            }

            if (_poolEventHooked) return;

            _pool.ActiveLightChanged += OnPoolLightActiveChanged;
            // T-LIGHT21: пул спрашивает разрешение на выключение — так fade-out остаётся внутри ALIC,
            // а Light.enabled по-прежнему пишет только пул.
            _pool.HoldEnabled = ShouldHoldEnabledForFade;
            _poolEventHooked = true;
        }

        private void UnhookPool()
        {
            if (_pool != null)
            {
                if (_poolEventHooked) _pool.ActiveLightChanged -= OnPoolLightActiveChanged;
                _pool.HoldEnabled = null;
            }

            _poolEventHooked = false;
            _pool = null;
            _poolVersionSeen = -1;
            _activeLights.Clear();
        }

        /// <summary>
        /// Пересборка копии активного набора по версии пула. Защищает от потерянного события
        /// (пул мог пересобраться до привязки) и синхронизирует счётчики после Rebuild.
        /// </summary>
        private void SyncActiveSet()
        {
            if (_pool == null)
            {
                if (_activeLights.Count > 0) _activeLights.Clear();
                return;
            }

            int version = _pool.ActiveSetVersion;
            // Дополнительная защита: если версия не менялась, а пул уже кого-то держит включённым,
            // значит копия потерялась (например, событие пришло до подписки) — пересобираем.
            bool stale = _activeLights.Count == 0 && _pool.ActiveCount > 0;
            if (version == _poolVersionSeen && !stale) return;

            _poolVersionSeen = version;
            ActiveSetResyncs++;
            _activeLights.Clear();

            int count = _pool.CopyActiveLights(_syncScratch);
            for (int i = 0; i < count; i++)
            {
                Light light = _syncScratch[i];
                if (light == null) continue;
                if (_activeLights.Add(light)) ApplyImmediate(light, EnsureState(light));
            }
        }

        private LightState EnsureState(Light light)
        {
            if (_states.TryGetValue(light, out LightState state)) return state;

            state = CaptureState(light);
            if (!_trackedLights.Contains(light)) _trackedLights.Add(light);
            _states.Add(light, state);
            return state;
        }

        /// <summary>
        /// Пул включил/выключил источник. enabled — его владение; здесь мы только держим копию набора
        /// и, если источник включён, сразу выставляем актуальную яркость (иначе был бы тёмный/яркий кадр).
        /// T-LIGHT21: включение начинает fade-in от нуля (если лампа была погашена), а выключение обнуляет вес,
        /// чтобы следующее появление снова шло плавно.
        /// </summary>
        private void OnPoolLightActiveChanged(Light light, bool active)
        {
            if (light == null) return;

            LightState state = null;
            if (_states.TryGetValue(light, out LightState found)) state = found;

            if (!active)
            {
                _activeLights.Remove(light);

                // Источник уже не рисуется. Если это был не fade-out (кончилось удержание/выключил пул),
                // вес обнуляется — иначе следующее включение стартовало бы с середины и дало скачок.
                if (state != null && state.fadeTarget > 0.5f)
                {
                    state.fadeWeight = 0f;
                    state.fadeOutActive = false;
                }

                return;
            }

            _activeLights.Add(light);
            ApplyImmediate(light, state ?? EnsureState(light));
        }

        /// <summary>
        /// Немедленная яркость без мерцания и без сглаживания: источник только что появился в кадре,
        /// его smoothedIntensity мог устареть (пока он был погашен, его никто не обновлял).
        /// T-LIGHT21: сразу выставляется только «полная» цель, а весовой множитель применяется к записи —
        /// поэтому включение идёт плавно от 0 и не зависит от предыдущего сглаживания.
        /// </summary>
        private void ApplyImmediate(Light light, LightState state)
        {
            if (_groupMultipliers.Length == 0 && groups != null && groups.Length > 0)
            {
                EvaluateGroupMultipliers(_currentTimeOfDay);
            }

            EnsureGroupAssigned(light, state);

            float targetIntensity = Mathf.Max(0f, state.baseIntensity * MultiplierFor(state));
            state.target = targetIntensity;
            state.smoothedIntensity = targetIntensity;
            // Провал «из прошлого» не должен сработать в момент включения.
            state.flickerScheduled = false;

            BeginFadeIn(light, state, targetIntensity);

            float value = targetIntensity * state.fadeWeight;
            if (WriteIntensity(light, state, value, EmitEpsilon, true))
            {
                WritesThisFrame++;
                ActiveWritesThisFrame++;
            }

            NoteFadeSample(light, state, value);
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
