using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.World
{
    /// <summary>
    /// Источник записи трека. Давность + источник хранятся с первой записи
    /// (концепт карты §4: «знаю точно / примерно / слышал»).
    /// </summary>
    public enum ChartTrackSource
    {
        Self,       // свой пройденный путь (v1 — единственный пишущий)
        Purchased,  // чужая съёмка с купленной карты (будущее)
        Crew,       // трек сокомандника (будущее)
    }

    /// <summary>Одна точка журнала плаваний: где + когда + чья съёмка.</summary>
    [Serializable]
    public struct ChartTrackPoint
    {
        public Vector3 worldPos;
        public double utcSeconds; // Unix-время — для выцветания по давности
        public ChartTrackSource source;
    }

    /// <summary>
    /// Журнал плаваний: прореженная запись пути локального игрока (пешком и в
    /// корабле — позиция игрока; сидящий едет с кораблём). v1: сессионный,
    /// per-player, источник всегда Self. Персист между сессиями — следующий шаг.
    ///
    /// 🟡 Floating Origin: точки — мировые Vector3 между кадрами, поэтому
    /// ApplyRebaseTranslation сдвигает журнал вместе с миром. Вызывается из
    /// GlobalMotionControlledRebaseSlice (ОБА пути: success и rollback) +
    /// клиентский broadcast-handler — как шторма/коридоры. Без хука после F8
    /// трек уедет на величину сдвига.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChartTrackRecorder : MonoBehaviour
    {
        public static ChartTrackRecorder Instance { get; private set; }

        /// <summary>
        /// Создать рекордер, если нет (журнал пишется всегда, а не только
        /// при открытой карте — вызывать на старте сессии, не по M).
        /// </summary>
        public static void EnsureExists()
        {
            if (Instance != null || !Application.isPlaying) return;
            var go = new GameObject("ChartTrackRecorder");
            DontDestroyOnLoad(go);
            go.AddComponent<ChartTrackRecorder>();
        }

        [Header("Прореживание")]
        [Tooltip("Пишем точку, если ушли дальше этого от прошлой (м).")]
        [SerializeField] private float _minDistanceMeters = 25f;
        [Tooltip("...и прошло не меньше этого (с).")]
        [SerializeField] private float _minIntervalSeconds = 5f;

        [Header("Память")]
        [Tooltip("Кольцо: старше этого вытесняется (точки).")]
        [SerializeField] private int _maxPoints = 2000;

        private readonly List<ChartTrackPoint> _points = new List<ChartTrackPoint>();
        private Vector3 _lastRecorded;
        private bool _hasLast;
        private float _lastTime;
        private ProjectC.Player.NetworkPlayer _localPlayer;

        // WORLD-MAP-PERSIST: ленивая загрузка + грязный автосейв
        private bool _loaded;
        private ulong _clientId;
        private bool _dirty;
        private float _lastSaveTime;
        private const float SaveIntervalSeconds = 30f;

        /// <summary>Все точки (только чтение, для отрисовки карты).</summary>
        public IReadOnlyList<ChartTrackPoint> Points => _points;

        public int Count => _points.Count;

        /// <summary>Unix-время (с) — единый источник давности для карты.</summary>
        public static double NowUtcSeconds() =>
            (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null && Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            EnsureLoaded();
            if (_localPlayer == null)
            {
                var nm = Unity.Netcode.NetworkManager.Singleton;
                if (nm == null || !nm.IsListening) return;
                var players = nm.SpawnManager?.PlayerObjects;
                if (players == null) return;
                foreach (var no in players)
                {
                    if (no == null) continue;
                    var np = no.GetComponent<ProjectC.Player.NetworkPlayer>();
                    if (np != null && np.IsOwner) { _localPlayer = np; break; }
                }
                if (_localPlayer == null) return;
            }

            Vector3 p = _localPlayer.transform.position;
            float now = Time.unscaledTime;
            if (!_hasLast)
            {
                Append(p);
                return;
            }
            if (Vector3.Distance(p, _lastRecorded) >= _minDistanceMeters
                && now - _lastTime >= _minIntervalSeconds)
            {
                Append(p);
            }

            // Грязный автосейв журнала (треки + метки разом)
            if (_dirty && now - _lastSaveTime >= SaveIntervalSeconds)
                SaveJournal();
        }

        private void OnApplicationQuit()
        {
            if (_dirty) SaveJournal();
        }

        /// <summary>
        /// Ленивая загрузка журнала (нужен слушающий NGO ради clientId).
        /// Один раз за сессию; дальше точки дописываются живьём.
        /// </summary>
        private void EnsureLoaded()
        {
            if (_loaded) return;
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return;
            _clientId = nm.LocalClientId;
            _loaded = true;
            _lastSaveTime = Time.unscaledTime;
            if (ChartJournalStore.TryLoad(_clientId, out var data))
            {
                RestoreFromDtos(data.tracks);
                ChartMarkManager.Instance?.RestoreFromDtos(data.marks, data.nextMarkId);
                Debug.Log($"[ChartJournal] Loaded: {Count} точек, " +
                          $"{(ChartMarkManager.Instance != null ? ChartMarkManager.Instance.Count : 0)} меток.");
            }
        }

        private void RestoreFromDtos(System.Collections.Generic.List<ChartTrackDto> dtos)
        {
            _points.Clear();
            _hasLast = false;
            if (dtos == null) return;
            int skip = Mathf.Max(0, dtos.Count - _maxPoints);
            for (int i = skip; i < dtos.Count; i++)
            {
                var d = dtos[i];
                if (d == null) continue;
                _points.Add(new ChartTrackPoint
                {
                    worldPos = new Vector3(d.x, d.y, d.z),
                    utcSeconds = d.utc,
                    source = (ChartTrackSource)Mathf.Clamp(d.source, 0, 2),
                });
            }
        }

        private void ExportToDtos(System.Collections.Generic.List<ChartTrackDto> outDtos)
        {
            outDtos.Clear();
            foreach (var p in _points)
            {
                outDtos.Add(new ChartTrackDto
                {
                    x = p.worldPos.x, y = p.worldPos.y, z = p.worldPos.z,
                    utc = p.utcSeconds,
                    source = (int)p.source,
                });
            }
        }

        /// <summary>
        /// Сохранить журнал целиком (треки + метки + живой кумулятив сдвига).
        /// Метки зовут сразу при add/remove, треки — по таймеру и на выходе.
        /// </summary>
        public void SaveJournal()
        {
            if (!_loaded) return;
            var data = new ChartJournalData();
            ExportToDtos(data.tracks);
            var marks = ChartMarkManager.Instance;
            if (marks != null)
            {
                marks.ExportToDtos(data.marks);
                data.nextMarkId = marks.ExportNextId();
            }
            ChartJournalStore.Save(_clientId, data);
            _dirty = false;
            _lastSaveTime = Time.unscaledTime;
        }

        private void Append(Vector3 worldPos)
        {
            while (_points.Count >= _maxPoints && _points.Count > 0)
                _points.RemoveAt(0);
            _points.Add(new ChartTrackPoint
            {
                worldPos = worldPos,
                utcSeconds = NowUtcSeconds(),
                source = ChartTrackSource.Self,
            });
            _lastRecorded = worldPos;
            _lastTime = Time.unscaledTime;
            _hasLast = true;
            _dirty = true;
        }

        /// <summary>
        /// Сдвинуть весь журнал вместе с миром (см. шапку — ОБА пути слайса).
        /// </summary>
        public int ApplyRebaseTranslation(Vector3 translation)
        {
            for (int i = 0; i < _points.Count; i++)
            {
                var pt = _points[i];
                pt.worldPos += translation;
                _points[i] = pt;
            }
            if (_hasLast) _lastRecorded += translation;
            return _points.Count;
        }

        /// <summary>Очистить журнал (сброс, отладка).</summary>
        public void Clear()
        {
            _points.Clear();
            _hasLast = false;
        }
    }
}
