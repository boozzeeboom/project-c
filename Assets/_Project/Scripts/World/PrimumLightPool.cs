// Project C: бюджет источников света города PRIMUM (Э3 «Свет»).
// Хостер: корень WorldRoot_0_0/PRIMUM_LIGHTS (создаёт PrimumLampLighting).
// Источники города — префабы PM_LIGHT_* с ОТКЛЮЧЁННЫМ Light-компонентом; пул включает
// не более maxActive ближайших к камере и гасит остальные, поэтому Forward-путь
// (Forward, per-object limit 4, дополнительные тени выкл.) не зависит от 1 900 ламп.
// Позиции читает живьём каждый чек (transform.position) — между кадрами мировой Vector3
// не хранится, хук Floating Origin не нужен (как в DetailDistanceCuller).
// Гистерезис по радиусу против мигания на границе; уже включённый свет имеет приоритет.
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.World
{
    public class PrimumLightPool : MonoBehaviour
    {
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
            ActiveCount = 0;
            if (_lights.Length == 0 && !_warned)
            {
                _warned = true;
                Debug.LogWarning("[PRIMUM/Lights] Под " + name + " нет ни одного Light — пул простаивает.", this);
            }
            // На старте всё выключено: 1 900 источников не должны попасть в первый кадр.
            for (int i = 0; i < _lights.Length; i++)
            {
                if (_lights[i] != null) _lights[i].enabled = false;
            }
        }

        private void Check()
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

            if (_cand.Count > 0)
            {
                _cand.Sort(CompareByDistance);
                for (int i = 0; i < _cand.Count && _desired.Count < cap; i++) _desired.Add(_cand[i]);
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
            for (int i = 0; i < _lights.Length; i++)
            {
                Light l = _lights[i];
                if (l == null) continue;
                if (l.enabled != _on[i]) l.enabled = _on[i];
                if (_on[i]) active++;
            }
            ActiveCount = active;

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
                return false;
            }

            position = _focusCamera.transform.position;
            return true;
        }

        /// <summary>Активная камера, у которой ближайший светильник ближе всех остальных камер.</summary>
        private Camera FindFocusCamera()
        {
            Camera[] cams = Camera.allCameras;
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

            return best;
        }

        private int CompareByDistance(int a, int b) => _sqr[a].CompareTo(_sqr[b]);

        /// <summary>Принудительно пересобрать список (после добавления/удаления ламп в рантайме).</summary>
        public void Refresh() => Rebuild();
    }
}
