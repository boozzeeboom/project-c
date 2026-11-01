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
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

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

        [Header("Точка отсчёта")]
        [Tooltip("Явный фокус (например Transform игрока). Пусто — активная камера, ближайшая к городу.\nCamera.main в этом проекте не годится: на тег MainCamera висят ДВЕ камеры — статичная MainCamera из BootstrapScene и спавнимая ThirdPersonCamera, и Camera.main отдаёт статичную (в 233 км от города).")]
        [SerializeField] private Transform focus;

        private Camera _focusCamera;
        private float _nextFocusSearch;
        private bool _loggedFocus;

        private Light[] _lights = new Light[0];
        private float[] _sqr = new float[0];
        private bool[] _on = new bool[0];
        private byte[] _kind = new byte[0];
        private readonly List<int> _cand = new List<int>(256);
        private readonly List<int> _desired = new List<int>(256);
        private float _timer;
        private bool _warned;

        /// <summary>Сколько источников включено при последнем чеке (для отчётов и отладки).</summary>
        public int ActiveCount { get; private set; }
        /// <summary>Сколько источников зарегистрировано под корнем.</summary>
        public int RegisteredCount => _lights != null ? _lights.Length : 0;
        public int MaxActive => maxActive;
        public float ActiveRadius => activeRadius;

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

        private void OnEnable()
        {
            Rebuild();
            _timer = float.MaxValue; // первый чек сразу
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < checkInterval) return;
            _timer = 0f;
            if (_lights.Length == 0) Rebuild();
            Check();
        }

        private void Rebuild()
        {
            Transform root = lightsRoot != null ? lightsRoot : transform;
            _lights = root != null ? root.GetComponentsInChildren<Light>(includeInactive: true) : new Light[0];
            _sqr = new float[_lights.Length];
            _on = new bool[_lights.Length];
            _kind = new byte[_lights.Length];
            ActiveCount = 0;
            for (int i = 0; i < _lights.Length; i++)
            {
                Light l = _lights[i];
                _kind[i] = l != null ? ClassifyName(l.name) : KindOther;
            }
            if (_lights.Length == 0 && !_warned)
            {
                _warned = true;
                Debug.LogWarning("[PRIMUM/Lights] Под " + name + " нет ни одного Light — пул простаивает.", this);
            }
            // На старте всё выключено: 2 238 источников не должны попасть в первый кадр.
            for (int i = 0; i < _lights.Length; i++)
            {
                if (_lights[i] != null) _lights[i].enabled = false;
            }
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
            CheckCount++;
        }

        private void CheckCore()
        {
            if (_lights.Length == 0) return;

            Vector3 camPos;
            if (!TryGetFocusPosition(out camPos)) return;

            float onDist = Mathf.Max(0f, activeRadius);
            float offDist = onDist * (1f + Mathf.Max(0f, hysteresis));
            float onSqr = onDist * onDist;
            float offSqr = offDist * offDist;
            int cap = Mathf.Max(1, maxActive);

            _desired.Clear();
            _cand.Clear();

            for (int i = 0; i < _lights.Length; i++)
            {
                Light l = _lights[i];
                if (l == null) { _sqr[i] = float.MaxValue; continue; }
                Vector3 p = l.transform.position;
                float dx = p.x - camPos.x, dy = p.y - camPos.y, dz = p.z - camPos.z;
                float d = dx * dx + dy * dy + dz * dz;
                _sqr[i] = d;

                if (_on[i])
                {
                    // уже горит: держим до внешнего радиуса
                    if (d <= offSqr) _desired.Add(i);
                }
                else if (d <= onSqr)
                {
                    _cand.Add(i);
                }
            }

            int capCut = 0;
            if (_cand.Count > 0)
            {
                _cand.Sort(CompareByDistance);
                int admitted = 0;
                for (int i = 0; i < _cand.Count && _desired.Count < cap; i++) { _desired.Add(_cand[i]); admitted++; }
                capCut = _cand.Count - admitted;
            }

            if (_desired.Count > cap)
            {
                _desired.Sort(CompareByDistance);
                _desired.RemoveRange(cap, _desired.Count - cap);
            }

            // применяем разницу
            for (int i = 0; i < _lights.Length; i++) _on[i] = false;
            for (int i = 0; i < _desired.Count; i++) _on[_desired[i]] = true;

            int active = 0;
            int switches = 0;
            int aOut = 0, aIn = 0, aFarm = 0, aOther = 0;
            int b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0;
            int zero = 0;
            for (int i = 0; i < _lights.Length; i++)
            {
                Light l = _lights[i];
                if (l == null) continue;
                if (l.enabled != _on[i]) { l.enabled = _on[i]; switches++; }
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
        /// Поэтому берётся активная камера, ближайшая к светильникам — она и рендерит город.
        /// Позиции читаются живьём, мировые Vector3 между кадрами не хранятся (правило Floating Origin).
        /// </summary>
        private bool TryGetFocusPosition(out Vector3 position)
        {
            if (focus != null)
            {
                position = focus.position;
                FocusSourceName = focus.name;
                FocusSourceKind = "explicit";
                return true;
            }

            if (_focusCamera == null || !_focusCamera.isActiveAndEnabled || Time.unscaledTime >= _nextFocusSearch)
            {
                _focusCamera = FindFocusCamera();
                _nextFocusSearch = Time.unscaledTime + 1f;
            }

            if (_focusCamera == null)
            {
                position = Vector3.zero;
                FocusSourceName = "none";
                FocusSourceKind = "none";
                return false;
            }

            position = _focusCamera.transform.position;
            FocusSourceName = _focusCamera.name;
            FocusSourceKind = "camera";
            return true;
        }

        /// <summary>Активная камера, у которой ближайший светильник ближе всех остальных камер.</summary>
        private Camera FindFocusCamera()
        {
            long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            using (FocusMarker.Auto())
            {
                Camera[] cams = Camera.allCameras;
                FocusCameraPool = cams.Length;
                Camera best = null;
                float bestNearestSqr = float.MaxValue;

                for (int c = 0; c < cams.Length; c++)
                {
                    Camera cam = cams[c];
                    if (cam == null || !cam.isActiveAndEnabled) continue;
                    Vector3 camPos = cam.transform.position;

                    float nearestSqr = float.MaxValue;
                    for (int i = 0; i < _lights.Length; i++)
                    {
                        Light l = _lights[i];
                        if (l == null) continue;
                        float d = (l.transform.position - camPos).sqrMagnitude;
                        if (d < nearestSqr) nearestSqr = d;
                    }

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
