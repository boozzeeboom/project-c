// Project C: бюджет источников света города PRIMUM (Э3 «Свет»).
// Хостер: корень WorldRoot_0_0/PRIMUM_LIGHTS (создаёт PrimumLampLighting).
// Источники города — префабы PM_LIGHT_* с ОТКЛЮЧЁННЫМ Light-компонентом; пул включает
// не более maxActive ближайших к камере и гасит остальные, поэтому Forward-путь
// (Forward, per-object limit 4, дополнительные тени выкл.) не зависит от 2 238 ламп.
// Позиции читает живьём каждый чек (transform.position) — между кадрами мировой Vector3
// не хранится, хук Floating Origin не нужен (как в DetailDistanceCuller).
// Гистерезис по радиусу против мигания на границе; уже включённый свет имеет приоритет.
//
// T-LIGHT16 (L1 светового трека): добавлена ТОЛЬКО диагностика — счётчики, buckets и
// маркеры Profiler. Ни enabled/intensity/focus/maxActive/activeRadius/порядок сортировки,
// ни одна ветка решения не изменены: все новые чтения идут после того, как набор `_on`
// уже вычислен, и ничего не пишут в Light. Поля — int/float/имена, мировые Vector3 не
// кэшируются (правило Floating Origin).
//
// T-LIGHT17 (L2 светового трека) — карта владения и дешёвые победы:
//   Light.enabled   — ТОЛЬКО этот пул (VisibilityDirector для ламп пишет Owner=PrimumLightPool,
//                     но сам Light.enabled не трогает).
//   Light.intensity — ТОЛЬКО AdditionalLightIntensityController (ALIC) и его flicker.
//                     Пул intensity НЕ пишет никогда — он её читает как признак «источник
//                     реально светит». Пункт 1.4 брифа: intensity не пишется из пула.
//   focus/камера    — только чтение.
// Три изменения поведения L2 (все — сериализованные параметры, A/B через ApplyConfig):
//   1. day/night-гейт: погашенный по кривой суток источник (intensity ≈ 0) не включается и
//      не держится, поэтому днём невидимые уличные лампы не занимают слоты бюджета;
//   2. резерв бюджета под улицу (OUT): интерьеры не могут занять cap − outdoorMinSlots,
//      пока уличные кандидаты ещё есть; остаток слотов они добирают второй фазой;
//   3. радиус L2 (по умолчанию 100 м) вместо устаревшего activeRadius=140 м из сцены —
//      выключается флагом useRadiusL2, сцена при этом не требуется к перезаписи.
// Мировых Vector3 пул по-прежнему не кэширует: гейт читает скаляр intensity, а не позицию.
//
// T-LIGHT17b (L2b светового трека) — контракт активного набора для ALIC:
//   ActiveLightChanged   — событие «источник включён/выключен пулом»; ALIC по нему немедленно
//                          применяет актуальную intensity, чтобы включение не стоило кадра;
//   ActiveSetVersion     — версия набора (== SetVersion), ALIC сверяет её и пересобирает копию;
//   CopyActiveLights     — копия набора для синхронизации/диагностики.
// Политика (dayGate / split OUT-IN / radius) и владение Light.enabled не изменены: пул
// по-прежнему единственный писатель enabled и НЕ пишет intensity.
//
// T-LIGHT18 (L3 светового трека) — только фокус/камеры, политика света не тронута:
//   1. Кэш камер вместо Camera.allCameras каждую секунду. Пересборка только по инвалидации
//      (камера null/выключена/сменился состав камер/загружена сцена) и не чаще раза в секунду;
//      между пересборками состав проверяется счётчиком Camera.allCamerasCount.
//   2. Фильтры: CameraType.Game, активно, targetTexture == null, farClipPlane ≥ focusMinFarClip,
//      в cullingMask есть слой зарегистрированных ламп и камера дотягивается до ближайшей лампы
//      (nearestSqr ≤ far²). Так отсекаются AtmosphereCamera меню (маска только PCMM_Atmosphere)
//      и парковочная MainCamera BootstrapScene (сотни км от города). Оба фильтра проверяются ДО
//      прохода по 2 238 лампам, поэтому неудачный поиск стоит O(камер), а не O(камеры×лампы).
//   3. Anchor — камера (в режиме корабля она до ~350 м от игрока). Явный focus (Transform)
//      имеет приоритет; локальный игрок по умолчанию НЕ используется.
//   4. Адаптивный триггер: сдвиг фокуса > focusMoveThreshold (2 м) или поворот > focusRotateThreshold
//      (3°) пересчитывает набор немедленно, но не чаще adaptiveMinInterval (0.1 с) — реакция
//      на телепорт/разворот укладывается в 0.15 с вместо прежних 0.25–1.0 с.
//   5. Мировые позиции по-прежнему не кэшируются: сохранённая точка сравнения — только дельта
//      к следующему кадру и переинициализируется при смене камеры (правило Floating Origin).
// enabled / intensity / dayGate / split / radius / maxActive не изменены.
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectC.World
{
    public class PrimumLightPool : MonoBehaviour
    {
        /// <summary>Имя маркера Profiler вокруг одного чека пула (T-LIGHT16).</summary>
        public const string CheckMarkerName = "PRIMUM/LightPool.Check";
        /// <summary>Имя маркера Profiler вокруг перепоиска камеры фокуса (T-LIGHT16).</summary>
        public const string FocusMarkerName = "PRIMUM/LightPool.FindFocusCamera";

        private static readonly ProfilerMarker CheckMarker = new ProfilerMarker(CheckMarkerName);
        private static readonly ProfilerMarker FocusMarker = new ProfilerMarker(FocusMarkerName);

        // Диагностика T-LIGHT16: разбивка по префиксу имени источника. Считается один раз
        // в Rebuild, чтобы чек не делал строковых операций.
        private const byte KindOther = 0;
        private const byte KindOut = 1;
        private const byte KindIn = 2;
        private const byte KindFarm = 3;
        private const float IntensityZeroEpsilon = 0.001f;

        [Header("Бюджет источников (Э3)")]
        [Tooltip("Корень световых префабов. Пусто = объект, на котором висит компонент.")]
        [SerializeField] private Transform lightsRoot;

        [Tooltip("Интервал перепроверки (с).")]
        [SerializeField] private float checkInterval = 0.25f;

        [Tooltip("Максимум одновременно включённых источников.")]
        [SerializeField] private int maxActive = 96;

        [Tooltip("Радиус включения вокруг камеры, м.")]
        [SerializeField] private float activeRadius = 140f;

        [Tooltip("Гистерезис (доля радиуса) — уже включённый свет гаснет позже, чем включается новый.")]
        [SerializeField] private float hysteresis = 0.1f;

        [Header("L2 (T-LIGHT17): день/ночь-гейт, бюджет OUT/IN, радиус")]
        [Tooltip("Не включать и не держать источник, у которого текущая Light.intensity ≈ 0 (ALIC уже выставил ноль по кривой суток). Пул intensity не пишет — только читает.")]
        [SerializeField] private bool dayGateEnabled = true;

        [Tooltip("Порог яркости, ниже которого источник считается погашенным по времени суток.")]
        [SerializeField, Min(0f)] private float dayGateEpsilon = 0.002f;

        [Tooltip("Держать резерв слотов под уличные источники (PRIMUM_OUTLIGHT): интерьеры не занимают больше maxActive − резерв, пока уличные кандидаты ещё есть.")]
        [SerializeField] private bool kindSplitEnabled = true;

        [Tooltip("Резерв слотов под улицу, шт. При maxActive=96 и 40 интерьеры ограничены 56 слотами до второй фазы.")]
        [SerializeField, Min(0)] private int outdoorMinSlots = 40;

        [Tooltip("Использовать радиус L2 вместо устаревшего activeRadius из сцены (140 м). Выключить = вернуть поведение L1.")]
        [SerializeField] private bool useRadiusL2 = true;

        [Tooltip("Радиус включения в режиме L2, м.")]
        [SerializeField, Min(0f)] private float activeRadiusL2 = 100f;

        [Header("Точка отсчёта")]
        [Tooltip("Явный фокус (например Transform игрока). Пусто — активная камера, ближайшая к городу.\nCamera.main в этом проекте не годится: на тег MainCamera висят ДВЕ камеры — статичная MainCamera из BootstrapScene и спавнимая ThirdPersonCamera, и Camera.main отдаёт статичную (в 233 км от города).")]
        [SerializeField] private Transform focus;

        [Header("T-LIGHT18 (L3): кэш камер и адаптивный триггер")]
        [Tooltip("Фильтры L3: тип Game, активно, без RenderTexture, far ≥ порога, в маске есть слой ламп и камера дотягивается до лампы. Выключено = поведение до L3 (любая активная камера + ближайший светильник, перепоиск раз в секунду) — A/B-контроль.")]
        [SerializeField] private bool cameraFiltersEnabled = true;

        [Tooltip("Адаптивный триггер: сдвиг/поворот фокуса пересчитывает набор немедленно (не чаще adaptiveMinInterval). Выключено = только обычный checkInterval.")]
        [SerializeField] private bool adaptiveTriggerEnabled = true;

        [Tooltip("Минимальный farClipPlane камеры-фокуса, м.")]
        [SerializeField, Min(0f)] private float focusMinFarClip = 50f;

        [Tooltip("Сдвиг фокус-точки для немедленного пересчёта набора, м.")]
        [SerializeField, Min(0f)] private float focusMoveThreshold = 2f;

        [Tooltip("Поворот фокус-точки для немедленного пересчёта набора, градусы.")]
        [SerializeField, Min(0f)] private float focusRotateThreshold = 3f;

        [Tooltip("Минимальный интервал между чеками при адаптивном триггере, с.")]
        [SerializeField, Min(0.01f)] private float adaptiveMinInterval = 0.1f;

        [Tooltip("Интервал дешёвой проверки состава камер (Camera.allCamerasCount), с. Пересборка кэша — не чаще этого интервала.")]
        [SerializeField, Min(0.1f)] private float focusPoolPollInterval = 1f;

        private Camera _focusCamera;
        private bool _loggedFocus;

        // T-LIGHT18: состояние кэша камер и адаптивного триггера.
        private int _lightLayerMask;
        private int _focusPoolSize = -1;
        private float _nextFocusSearchUnscaled;
        private float _nextFocusPoolPollUnscaled;
        private float _lastCheckUnscaled;
        private bool _anchorAppliedValid;
        private Vector3 _anchorAppliedPos;
        private Quaternion _anchorAppliedRot;
        private string _lastLoggedFocus = "\u0000";
        private readonly System.Text.StringBuilder _focusReport = new System.Text.StringBuilder(192);

        private Light[] _lights = new Light[0];
        private float[] _sqr = new float[0];
        private bool[] _on = new bool[0];
        private byte[] _kind = new byte[0];
        private readonly List<int> _cand = new List<int>(256);
        private readonly List<int> _desired = new List<int>(256);
        // T-LIGHT17: «уже горит» и отложенные по резерву интерьеры (второй проход добора).
        private readonly List<int> _keep = new List<int>(256);
        private readonly List<int> _deferred = new List<int>(64);
        private float _timer;
        private bool _warned;

        /// <summary>Сколько источников включено при последнем чеке (для отчётов и отладки).</summary>
        public int ActiveCount { get; private set; }
        /// <summary>Сколько источников зарегистрировано под корнем.</summary>
        public int RegisteredCount => _lights != null ? _lights.Length : 0;
        public int MaxActive => maxActive;
        /// <summary>Фактический радиус включения: L2-значение либо устаревшее из сцены (140 м).</summary>
        public float ActiveRadius => useRadiusL2 ? activeRadiusL2 : activeRadius;

        // ---------------------------------------------------------------- T-LIGHT17
        // A/B-конфиг L2. Значения живут только в памяти: ApplyConfig не пишет сцену и не
        // сохраняет ассеты, поэтому прогон A/B не требует сохранения сцены.

        public bool DayGateEnabled => dayGateEnabled;
        public bool KindSplitEnabled => kindSplitEnabled;
        public int OutdoorMinSlots => outdoorMinSlots;
        public float DayGateEpsilon => dayGateEpsilon;
        /// <summary>Значение поля активного радиуса, доставшееся от L1 (в сцене — 140 м).</summary>
        public float LegacyActiveRadius => activeRadius;
        /// <summary>Сколько источников выключено в последнем чеке только из-за нулевой яркости.</summary>
        public int DayGateDropped { get; private set; }
        /// <summary>Сколько интерьеров отложено первой фазой ради резерва под улицу.</summary>
        public int KindQuotaDeferred { get; private set; }
        /// <summary>Сколько из отложенных вернулось в бюджет второй фазой (резерв не понадобился).</summary>
        public int KindQuotaFilled { get; private set; }
        /// <summary>Резерв под улицу, применённый в последнем чеке.</summary>
        public int OutdoorMinSlotsUsed { get; private set; }

        /// <summary>
        /// Карта владения светом (пункт 1.4 брифа). Строка попадает в отчёт/CSV прогона,
        /// чтобы владение было зафиксировано рядом с числами, а не только в комментарии.
        /// </summary>
        public const string OwnershipLine =
            "enabled=PrimumLightPool;intensity=AdditionalLightIntensityController;poolWritesIntensity=no;" +
            "poolWritesRange=no;poolWritesShadows=no;directorWritesLightEnabled=no;focusWrites=no";

        // ---------------------------------------------------------------- T-LIGHT17b
        // Контракт активного набора. enabled — по-прежнему только пул, поэтому именно он знает,
        // какие источники реально участвуют в кадре. ALIC (владелец intensity) подписан на событие
        // и держит копию набора; версия набора защищает от потерянного события.

        /// <summary>Источник включён (true) или выключен (false) пулом. Вызывается в том же кадре, что и запись Light.enabled.</summary>
        public event System.Action<Light, bool> ActiveLightChanged;

        /// <summary>Версия активного набора: растёт при любом изменении набора, включая пересборку (Rebuild).</summary>
        public int ActiveSetVersion => SetVersion;

        /// <summary>Копия текущего активного набора (синхронизация/диагностика ALIC).</summary>
        public int CopyActiveLights(List<Light> into)
        {
            if (into == null) return 0;
            into.Clear();
            for (int i = 0; i < _lights.Length; i++)
            {
                if (_lights[i] != null && _on[i]) into.Add(_lights[i]);
            }
            return into.Count;
        }

        /// <summary>Строка параметров пула для отчёта (pool/dayGate/split/radius).</summary>
        public string ConfigLine
        {
            get
            {
                return "pool;dayGate=" + (dayGateEnabled ? "on" : "off") +
                       ";dayGateEps=" + dayGateEpsilon.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) +
                       ";split=" + (kindSplitEnabled ? "on" : "off") +
                       ";outMin=" + outdoorMinSlots +
                       ";radiusMode=" + (useRadiusL2 ? "L2" : "L1_legacy") +
                       ";radius=" + ActiveRadius.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) +
                       ";legacyRadius=" + activeRadius.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) +
                       ";maxActive=" + maxActive +
                       ";hysteresis=" + hysteresis.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                       ";checkInterval=" + checkInterval.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Переключить конфиг L2 на лету (A/B-замер). Меняются только поля компонента в памяти:
        /// сцена, префабы и ассеты не пишутся. Следующий чек выполняется сразу.
        /// </summary>
        public void ApplyConfig(bool dayGate, bool splitOutIn, int outMinSlots, bool useL2Radius, float radiusMeters)
        {
            dayGateEnabled = dayGate;
            kindSplitEnabled = splitOutIn;
            outdoorMinSlots = Mathf.Max(0, outMinSlots);
            useRadiusL2 = useL2Radius;
            float r = Mathf.Max(0f, radiusMeters);
            if (useL2Radius) activeRadiusL2 = r;
            else activeRadius = r;
            _timer = float.MaxValue; // пересчитать набор в ближайшем кадре
        }

        // ---------------------------------------------------------------- T-LIGHT16
        // Снимок состояния последнего чека. Только чтение снаружи; поведение не меняет.

        /// <summary>Кандидаты в радиусе activeRadius, ещё не прошедшие cap (новые, не горящие).</summary>
        public int CandidateCount { get; private set; }
        /// <summary>Сколько источников попало в желаемый набор после cap (== activeTotal).</summary>
        public int DesiredCount { get; private set; }
        /// <summary>Значение cap, применённое в чеке.</summary>
        public int CapValue { get; private set; }
        /// <summary>Сколько радиусных кандидатов отсеяно из-за заполненного cap.</summary>
        public int CapCutCount { get; private set; }
        /// <summary>Желаемый набор упёрся в cap (1 — да).</summary>
        public bool CapSaturated { get; private set; }

        public int ActiveOut { get; private set; }
        public int ActiveIn { get; private set; }
        public int ActiveFarm { get; private set; }
        public int ActiveOther { get; private set; }

        public int Bucket0To70 { get; private set; }
        public int Bucket70To100 { get; private set; }
        public int Bucket100To140 { get; private set; }
        public int Bucket140To154 { get; private set; }
        public int BucketOver154 { get; private set; }

        /// <summary>Активные источники с intensity ≈ 0 (день: слот занят, света нет).</summary>
        public int ActiveIntensityZero { get; private set; }
        /// <summary>Сколько Light.enabled реально переключилось в последнем чеке.</summary>
        public int LastSwitchCount { get; private set; }
        /// <summary>Растёт, когда чек изменил набор включённых источников (задержка реакции).</summary>
        public int SetVersion { get; private set; }
        /// <summary>Номер кадра последнего чека.</summary>
        public int LastCheckFrame { get; private set; }
        public int CheckCount { get; private set; }

        /// <summary>Длительность последнего Check, мс (Stopwatch, только диагностика).</summary>
        public double LastCheckMs { get; private set; }
        public double MaxCheckMs { get; private set; }
        public double LastFocusSearchMs { get; private set; }
        public int FocusSearchCount { get; private set; }
        /// <summary>Сколько активных камер вернул Camera.allCameras на момент перепоиска.</summary>
        public int FocusCameraPool { get; private set; }

        /// <summary>Имя источника фокуса (явный focus / имя камеры / "?" ).</summary>
        public string FocusSourceName { get; private set; } = "?";
        /// <summary>Тип источника фокуса: explicit / camera / none.</summary>
        public string FocusSourceKind { get; private set; } = "none";

        // ---------------------------------------------------------------- T-LIGHT18

        /// <summary>Растёт, когда пул применил сдвинувшуюся/повернувшуюся фокус-точку (реакция набора).</summary>
        public int FocusPositionVersion { get; private set; }
        /// <summary>Кадр, в котором применена новая фокус-точка (для замера реакции в бенче).</summary>
        public int FocusPositionVersionFrame { get; private set; } = -1;
        /// <summary>Сколько чеков запущено адаптивным триггером.</summary>
        public int FocusAdaptiveChecks { get; private set; }
        /// <summary>Сколько чеков запущено обычным интервалом.</summary>
        public int FocusNormalChecks { get; private set; }
        /// <summary>Размер последнего снимка Camera.allCameras.</summary>
        public int FocusPoolSize => _focusPoolSize;
        /// <summary>Имя камеры-фокуса ("none", если подходящей камеры нет).</summary>
        public string FocusCameraName => _focusCamera != null ? _focusCamera.name : "none";
        public bool CameraFiltersEnabled => cameraFiltersEnabled;
        public bool AdaptiveTriggerEnabled => adaptiveTriggerEnabled;
        /// <summary>Слой(и) зарегистрированных ламп — критерий «камера рисует мир».</summary>
        public int LightLayerMask => _lightLayerMask;

        /// <summary>Строка конфига L3 для отчёта/CSV.</summary>
        public string CameraConfigLine
        {
            get
            {
                return "focus;filters=" + (cameraFiltersEnabled ? "on" : "off") +
                       ";adaptive=" + (adaptiveTriggerEnabled ? "on" : "off") +
                       ";minFar=" + focusMinFarClip.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) +
                       ";moveM=" + focusMoveThreshold.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                       ";rotDeg=" + focusRotateThreshold.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                       ";minInterval=" + adaptiveMinInterval.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                       ";poolPoll=" + focusPoolPollInterval.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                       ";camera=" + FocusCameraName +
                       ";source=" + FocusSourceKind + ":" + FocusSourceName +
                       ";poolSize=" + _focusPoolSize +
                       ";searches=" + FocusSearchCount +
                       ";positionVersion=" + FocusPositionVersion +
                       ";adaptiveChecks=" + FocusAdaptiveChecks +
                       ";lightMask=0x" + _lightLayerMask.ToString("X");
            }
        }

        /// <summary>
        /// Переключить конфиг L3 на лету (A/B-замер). Меняются только поля компонента в памяти:
        /// сцена и ассеты не пишутся. Следующий чек работает уже в новом режиме.
        /// </summary>
        public void ApplyCameraConfig(bool filters, bool adaptive, float moveThreshold, float rotateThreshold, float minInterval)
        {
            cameraFiltersEnabled = filters;
            adaptiveTriggerEnabled = adaptive;
            focusMoveThreshold = Mathf.Max(0f, moveThreshold);
            focusRotateThreshold = Mathf.Max(0f, rotateThreshold);
            adaptiveMinInterval = Mathf.Max(0.01f, minInterval);
            _focusCamera = null;
            _focusPoolSize = -1;
            _nextFocusSearchUnscaled = 0f;
            _nextFocusPoolPollUnscaled = 0f;
            _lastLoggedFocus = "\u0000";
            _timer = float.MaxValue;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Rebuild();
            _timer = float.MaxValue; // первый чек сразу
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>
        /// T-LIGHT18: загрузка сцены меняет состав камер (меню/риг) — кэш камер недействителен.
        /// Фокус-точка — позиция, а не сцена: сами источники пула от этого не пересобираются.
        /// </summary>
        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            _focusCamera = null;
            _focusPoolSize = -1;
            _anchorAppliedValid = false;
            _nextFocusSearchUnscaled = 0f;
            _nextFocusPoolPollUnscaled = 0f;
        }

        private void Update()
        {
            _timer += Time.deltaTime;

            bool adaptive = adaptiveTriggerEnabled && AdaptiveFocusTriggered();
            if (adaptive)
            {
                // Минимальный интервал между чеками при адаптивном триггере: набор не дрожит,
                // но реакция на телепорт/разворот укладывается в 0.15 с.
                if (Time.unscaledTime - _lastCheckUnscaled < Mathf.Max(0.01f, adaptiveMinInterval)) return;
            }
            else if (_timer < checkInterval)
            {
                return;
            }

            _timer = 0f;
            if (_lights.Length == 0) Rebuild();
            if (adaptive) FocusAdaptiveChecks++;
            else FocusNormalChecks++;
            Check();
        }

        /// <summary>
        /// T-LIGHT18: фокус-точка сдвинулась/повернулась сверх порогов L3 — набор надо пересчитать
        /// немедленно, не дожидаясь checkInterval. Дешёвая проверка: два чтения трансформа и угол.
        /// </summary>
        private bool AdaptiveFocusTriggered()
        {
            if (!_anchorAppliedValid) return false;
            Transform anchor = CurrentAnchorTransform();
            if (anchor == null) return false;
            if ((anchor.position - _anchorAppliedPos).sqrMagnitude > focusMoveThreshold * focusMoveThreshold) return true;
            return Quaternion.Angle(_anchorAppliedRot, anchor.rotation) > focusRotateThreshold;
        }

        private Transform CurrentAnchorTransform()
        {
            if (focus != null) return focus;
            return _focusCamera != null ? _focusCamera.transform : null;
        }

        private void Rebuild()
        {
            Transform root = lightsRoot != null ? lightsRoot : transform;
            _lights = root != null ? root.GetComponentsInChildren<Light>(includeInactive: true) : new Light[0];
            _sqr = new float[_lights.Length];
            _on = new bool[_lights.Length];
            _kind = new byte[_lights.Length];
            ActiveCount = 0;
            // T-LIGHT18: слой ламп — критерий «камера рисует мир». Считается один раз здесь,
            // чтобы фильтр камер не делал строковых/слоевых операций в чеке.
            int lightMask = 0;
            for (int i = 0; i < _lights.Length; i++)
            {
                Light l = _lights[i];
                _kind[i] = l != null ? ClassifyName(l.name) : KindOther;
                if (l != null) lightMask |= 1 << l.gameObject.layer;
            }
            if (lightMask != _lightLayerMask)
            {
                _lightLayerMask = lightMask;
                _focusCamera = null;      // критерий выбора камеры изменился — кэш недействителен
                _focusPoolSize = -1;
                _anchorAppliedValid = false;
            }
            if (_lights.Length == 0 && !_warned)
            {
                _warned = true;
                Debug.LogWarning("[PRIMUM/Lights] Под " + name + " нет ни одного Light — пул простаивает.", this);
            }
            // На старте всё выключено: 2 238 источников не должны попасть в первый кадр.
            // T-LIGHT17b: если пересборка реально погасила источники, набор изменился — версия
            // должна вырасти, иначе подписчик (ALIC) не узнает, что активный набор стал пустым.
            int switchedOff = 0;
            for (int i = 0; i < _lights.Length; i++)
            {
                if (_lights[i] == null) continue;
                if (_lights[i].enabled) switchedOff++;
                _lights[i].enabled = false;
            }
            if (switchedOff > 0) SetVersion++;
        }

        /// <summary>T-LIGHT16: группа источника по префиксу имени (OUT / IN / FARM).</summary>
        private static byte ClassifyName(string lightName)
        {
            if (string.IsNullOrEmpty(lightName)) return KindOther;
            if (lightName.IndexOf("PRIMUM_OUTLIGHT", System.StringComparison.Ordinal) >= 0) return KindOut;
            if (lightName.IndexOf("PRIMUM_INLIGHT", System.StringComparison.Ordinal) >= 0) return KindIn;
            if (lightName.IndexOf("PRIMUM_FARMLIGHT", System.StringComparison.Ordinal) >= 0) return KindFarm;
            return KindOther;
        }

        private void Check()
        {
            long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            using (CheckMarker.Auto())
            {
                CheckCore();
            }
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - startTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            LastCheckMs = ms;
            if (ms > MaxCheckMs) MaxCheckMs = ms;
            LastCheckFrame = Time.frameCount;
            _lastCheckUnscaled = Time.unscaledTime;
            CheckCount++;
        }

        private void CheckCore()
        {
            if (_lights.Length == 0) return;

            Vector3 camPos;
            if (!TryGetFocusPosition(out camPos)) return;

            float onDist = Mathf.Max(0f, ActiveRadius);
            float offDist = onDist * (1f + Mathf.Max(0f, hysteresis));
            float onSqr = onDist * onDist;
            float offSqr = offDist * offDist;
            int cap = Mathf.Max(1, maxActive);

            // T-LIGHT17: day/night-гейт. intensity ЧИТАЕТСЯ (её владелец — ALIC) и никогда не пишется:
            // погашенный по кривой суток источник не включается и не держится в бюджете.
            float gateEps = dayGateEnabled ? Mathf.Max(0f, dayGateEpsilon) : -1f;
            bool split = kindSplitEnabled;

            _desired.Clear();
            _cand.Clear();
            _keep.Clear();
            _deferred.Clear();

            int gateDropped = 0;
            for (int i = 0; i < _lights.Length; i++)
            {
                Light l = _lights[i];
                if (l == null) { _sqr[i] = float.MaxValue; continue; }
                Vector3 p = l.transform.position;
                float dx = p.x - camPos.x, dy = p.y - camPos.y, dz = p.z - camPos.z;
                float d = dx * dx + dy * dy + dz * dz;
                _sqr[i] = d;

                bool emitting = gateEps < 0f || l.intensity > gateEps;

                if (_on[i])
                {
                    // уже горит: держим до внешнего радиуса, но погашенный источник не держим —
                    // иначе днём половина бюджета занята невидимым светом.
                    if (d <= offSqr && emitting) _keep.Add(i);
                    else if (!emitting) gateDropped++;
                }
                else if (d <= onSqr && emitting)
                {
                    _cand.Add(i);
                }
            }

            // Порядок приёма: сначала уже горящие (стабильность набора), потом новые — оба по дистанции.
            _keep.Sort(CompareByDistance);
            _cand.Sort(CompareByDistance);

            // T-LIGHT17: резерв бюджета под улицу. Интерьеры не занимают больше cap − outMin
            // слотов, пока уличные кандидаты ещё есть; остаток они добирают второй фазой.
            int outMin = split ? Mathf.Clamp(outdoorMinSlots, 0, cap) : 0;
            int inSoft = cap - outMin;
            int inCount = 0;

            for (int pass = 0; pass < 2; pass++)
            {
                List<int> src = pass == 0 ? _keep : _cand;
                for (int k = 0; k < src.Count; k++)
                {
                    int i = src[k];
                    if (_desired.Count >= cap) continue;
                    if (split && _kind[i] == KindIn && inCount >= inSoft)
                    {
                        // Отложен, а не отброшен: если улица резерв не заберёт, слоты вернутся интерьерам.
                        _deferred.Add(i);
                        continue;
                    }
                    _desired.Add(i);
                    if (_kind[i] == KindIn) inCount++;
                }
            }

            int deferredTotal = _deferred.Count;
            int filled = 0;
            for (int k = 0; k < _deferred.Count && _desired.Count < cap; k++) { _desired.Add(_deferred[k]); filled++; }

            // применяем разницу
            for (int i = 0; i < _lights.Length; i++) _on[i] = false;
            for (int i = 0; i < _desired.Count; i++) _on[_desired[i]] = true;

            // T-LIGHT17: capCut сохраняет смысл L1 — сколько НОВЫХ радиусных кандидатов не попало
            // в бюджет (из-за cap и из-за резерва под улицу); считается по уже принятому набору.
            int capCut = 0;
            for (int k = 0; k < _cand.Count; k++) if (!_on[_cand[k]]) capCut++;

            int active = 0;
            int switches = 0;
            int aOut = 0, aIn = 0, aFarm = 0, aOther = 0;
            int b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0;
            int zero = 0;
            for (int i = 0; i < _lights.Length; i++)
            {
                Light l = _lights[i];
                if (l == null) continue;
                if (l.enabled != _on[i])
                {
                    l.enabled = _on[i];
                    switches++;
                    // T-LIGHT17b: владелец enabled сообщает о смене набора, чтобы ALIC применил
                    // intensity включённому источнику в этом же кадре.
                    ActiveLightChanged?.Invoke(l, _on[i]);
                }
                if (!_on[i]) continue;

                active++;

                // T-LIGHT16: только чтение — группы, дистанционные buckets, «горит с нулём».
                switch (_kind[i])
                {
                    case KindOut: aOut++; break;
                    case KindIn: aIn++; break;
                    case KindFarm: aFarm++; break;
                    default: aOther++; break;
                }

                if (l.intensity <= IntensityZeroEpsilon) zero++;

                float d = Mathf.Sqrt(_sqr[i]);
                if (d <= 70f) b0++;
                else if (d <= 100f) b1++;
                else if (d <= 140f) b2++;
                else if (d <= 154f) b3++;
                else b4++;
            }
            ActiveCount = active;

            // T-LIGHT16: публикация снимка (поведение уже посчитано выше и не меняется).
            CandidateCount = _cand.Count;
            DesiredCount = _desired.Count;
            CapValue = cap;
            CapCutCount = capCut;
            CapSaturated = _desired.Count >= cap;
            ActiveOut = aOut;
            ActiveIn = aIn;
            ActiveFarm = aFarm;
            ActiveOther = aOther;
            Bucket0To70 = b0;
            Bucket70To100 = b1;
            Bucket100To140 = b2;
            Bucket140To154 = b3;
            BucketOver154 = b4;
            ActiveIntensityZero = zero;
            DayGateDropped = gateDropped;
            KindQuotaDeferred = deferredTotal;
            KindQuotaFilled = filled;
            OutdoorMinSlotsUsed = outMin;
            LastSwitchCount = switches;
            if (switches > 0) SetVersion++;

            if (!_loggedFocus)
            {
                _loggedFocus = true;
                string src = focus != null ? focus.name : (_focusCamera != null ? _focusCamera.name : "?");
                Debug.Log("[PRIMUM/Lights] фокус: " + src + " @ " + camPos.ToString("F0") + ", активных источников: " + active + " из " + _lights.Length, this);
            }
        }

        /// <summary>
        /// Точка отсчёта пула. Camera.main здесь не годится: тег MainCamera есть и у статичной
        /// камеры BootstrapScene (она далеко от города), и у спавнимой ThirdPersonCamera.
        /// T-LIGHT18: anchor — камера, выбранная кэшем (см. EnsureFocusCamera); явный focus имеет приоритет.
        /// Позиции читаются живьём, мировые Vector3 между кадрами не хранятся (правило Floating Origin).
        /// </summary>
        private bool TryGetFocusPosition(out Vector3 position)
        {
            if (focus != null)
            {
                position = focus.position;
                FocusSourceName = focus.name;
                FocusSourceKind = "explicit";
                NoteAnchorApplied(focus);
                return true;
            }

            EnsureFocusCamera();

            if (_focusCamera == null)
            {
                position = Vector3.zero;
                FocusSourceName = "none";
                FocusSourceKind = "none";
                _anchorAppliedValid = false;
                return false;
            }

            position = _focusCamera.transform.position;
            FocusSourceName = _focusCamera.name;
            FocusSourceKind = "camera";
            NoteAnchorApplied(_focusCamera.transform);
            return true;
        }

        /// <summary>
        /// T-LIGHT18: зафиксировать применённую фокус-точку. Версия растёт, когда точка сдвинулась
        /// &gt; focusMoveThreshold или повернулась &gt; focusRotateThreshold — тем же критерием
        /// пользуется адаптивный триггер, поэтому замер реакции сравнивает одно и то же событие.
        /// Хранится только дельта к следующему чеку; после смены камеры/промаха она переинициализируется,
        /// так что накопленного мирового кэша (который ломает Floating Origin) здесь нет.
        /// </summary>
        private void NoteAnchorApplied(Transform anchor)
        {
            Vector3 p = anchor.position;
            Quaternion r = anchor.rotation;
            bool changed = !_anchorAppliedValid ||
                           (p - _anchorAppliedPos).sqrMagnitude > focusMoveThreshold * focusMoveThreshold ||
                           Quaternion.Angle(_anchorAppliedRot, r) > focusRotateThreshold;
            _anchorAppliedPos = p;
            _anchorAppliedRot = r;
            _anchorAppliedValid = true;
            if (!changed) return;
            FocusPositionVersion++;
            FocusPositionVersionFrame = Time.frameCount;
        }

        /// <summary>
        /// T-LIGHT18: кэш камеры-фокуса. Пересборка только по инвалидации (камера пропала/выключилась,
        /// сменился состав камер, загрузилась сцена) и не чаще раза в секунду; между пересборками
        /// состав проверяется счётчиком Camera.allCamerasCount, а не массивом Camera.allCameras.
        /// </summary>
        private void EnsureFocusCamera()
        {
            float now = Time.unscaledTime;

            // Контроль A/B: поведение до L3 — любая активная камера, перепоиск раз в секунду.
            if (!cameraFiltersEnabled)
            {
                if (_focusCamera == null || !_focusCamera.isActiveAndEnabled || now >= _nextFocusSearchUnscaled)
                {
                    _focusCamera = FindFocusCamera();
                    _nextFocusSearchUnscaled = now + 1f;
                }
                return;
            }

            bool valid = _focusCamera != null && _focusCamera.isActiveAndEnabled &&
                         _focusCamera.cameraType == CameraType.Game && _focusCamera.targetTexture == null;

            if (!valid)
            {
                // Инвалидация: камеры нет. До спавна рига (главное меню) поиск просто возвращает
                // пусто и стоит O(камер): фильтры отсекают камеру до прохода по 2 238 лампам.
                _focusCamera = null;
                if (now < _nextFocusSearchUnscaled) return;
                _nextFocusSearchUnscaled = now + Mathf.Max(0.1f, focusPoolPollInterval);
                _focusCamera = FindFocusCamera();
                LogFocusResolved();
                return;
            }

            if (now < _nextFocusPoolPollUnscaled) return;
            _nextFocusPoolPollUnscaled = now + Mathf.Max(0.1f, focusPoolPollInterval);
            if (Camera.allCamerasCount == _focusPoolSize) return;

            _focusCamera = FindFocusCamera();
            LogFocusResolved();
        }

        /// <summary>Лог только при смене камеры-фокуса — не спамим консоль каждую пересборку.</summary>
        private void LogFocusResolved()
        {
            string name = _focusCamera != null ? _focusCamera.name : "none";
            if (name == _lastLoggedFocus) return;
            _lastLoggedFocus = name;
            Debug.Log("[PRIMUM/Lights] FocusCamera=" + name +
                      " FocusSource=" + FocusSourceKind +
                      " pool=" + _focusPoolSize +
                      " searchMs=" + LastFocusSearchMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                      " searches=" + FocusSearchCount +
                      " | " + _focusReport, this);
        }

        /// <summary>
        /// Камера-фокус: ближайшая к лампам среди прошедших фильтры L3. Фильтры проверяются ДО
        /// прохода по лампам, поэтому отклонённая камера (AtmosphereCamera меню, парковочная
        /// MainCamera BootstrapScene) стоит O(1), а не O(2 238).
        /// </summary>
        private Camera FindFocusCamera()
        {
            long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            using (FocusMarker.Auto())
            {
                Camera[] cams = Camera.allCameras;
                _focusPoolSize = cams.Length;
                FocusCameraPool = cams.Length;
                _focusReport.Length = 0;

                Camera best = null;
                float bestNearestSqr = float.MaxValue;
                bool filtered = cameraFiltersEnabled;

                for (int c = 0; c < cams.Length; c++)
                {
                    Camera cam = cams[c];
                    if (cam == null || !cam.isActiveAndEnabled) continue;

                    string reason = null;
                    if (filtered && !CameraPassesFilters(cam, out reason))
                    {
                        AppendCameraReport(cam.name, reason);
                        continue;
                    }

                    Vector3 camPos = cam.transform.position;
                    float nearestSqr = float.MaxValue;
                    for (int i = 0; i < _lights.Length; i++)
                    {
                        Light l = _lights[i];
                        if (l == null) continue;
                        float d = (l.transform.position - camPos).sqrMagnitude;
                        if (d < nearestSqr) nearestSqr = d;
                    }

                    // «Дотягивается до города/ламп»: ближайшая лампа должна быть в пределах far.
                    if (filtered && nearestSqr > cam.farClipPlane * cam.farClipPlane)
                    {
                        AppendCameraReport(cam.name, "out_of_reach");
                        continue;
                    }

                    AppendCameraReport(cam.name, null);
                    if (nearestSqr < bestNearestSqr)
                    {
                        bestNearestSqr = nearestSqr;
                        best = cam;
                    }
                }

                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - startTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                LastFocusSearchMs = ms;
                FocusSearchCount++;
                return best;
            }
        }

        /// <summary>Фильтры L3 для камеры-фокуса (без прохода по лампам).</summary>
        private bool CameraPassesFilters(Camera cam, out string reason)
        {
            if (cam == null) { reason = "null"; return false; }
            if (cam.cameraType != CameraType.Game) { reason = "type_" + cam.cameraType; return false; }
            if (cam.targetTexture != null) { reason = "rendertexture"; return false; }
            if (cam.farClipPlane < focusMinFarClip) { reason = "far_lt_" + focusMinFarClip.ToString("F0", System.Globalization.CultureInfo.InvariantCulture); return false; }
            if (_lightLayerMask != 0 && (cam.cullingMask & _lightLayerMask) == 0) { reason = "mask_no_lights"; return false; }
            reason = null;
            return true;
        }

        private void AppendCameraReport(string cameraName, string reason)
        {
            if (_focusReport.Length > 0) _focusReport.Append(',');
            _focusReport.Append(cameraName).Append(reason == null ? "+" : "-" + reason);
        }

        private int CompareByDistance(int a, int b) => _sqr[a].CompareTo(_sqr[b]);

        /// <summary>Принудительно пересобрать список (после добавления/удаления ламп в рантайме).</summary>
        public void Refresh() => Rebuild();

        /// <summary>
        /// T-LIGHT16: копия зарегистрированных источников для диагностического бенча
        /// (посчитать активные впереди/позади камеры). Только чтение массива, ссылки наружу не отдаются.
        /// </summary>
        public int CopyRegisteredLights(List<Light> into)
        {
            if (into == null) return 0;
            into.Clear();
            for (int i = 0; i < _lights.Length; i++)
            {
                if (_lights[i] != null) into.Add(_lights[i]);
            }
            return into.Count;
        }
    }
}
