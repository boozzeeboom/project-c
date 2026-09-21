using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.World.Parom
{
    /// <summary>
    /// T-PAROM-01: паромная ветка — гражданский тросовый транспорт между фермами.
    /// Одна челночная кабинка идёт от начальной станции через промежуточные
    /// к конечной (остановка на каждой), затем назад. Веток может быть несколько —
    /// каждая ветка = свой объект ParomRoute со своей кабинкой.
    ///
    /// Дизайн: docs/world/parom_road/00_PAROM_DESIGN.md.
    /// Сервер считает (скаляр s вдоль полилинии), клиенты видят (3 NetworkVariable).
    /// Пассажир едет стоя на кабинке через стандартный moving-platform carry
    /// (NetworkPlayer.ApplyPlatformCarry / NpcBrain / PickupDeckRide).
    ///
    /// Floating Origin: объект целиком под корнем WorldScene_*, тросы и кабинка —
    /// дети в ЛОКАЛЬНЫХ координатах, мировые позиции якорей НЕ кэшируются
    /// (читаются с трансформов каждый кадр), кэшируются только длины сегментов.
    /// Поэтому ApplyRebaseTranslation НЕ нужен — сдвиг мира подхватывается сам.
    /// Все якоря одной ветки должны быть в пределах одного WorldScene_*.
    ///
    /// Спавн: scene-placed NetworkObject, подхватывается ScenePlacedObjectSpawner
    /// (как DockStation / NpcShipController). Второй NetworkManager не создавать.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class ParomRoute : NetworkBehaviour
    {
        [Header("Станции (якоря-пустышки в мире)")]
        [Tooltip("Начальная станция ветки (Transform-пустышка под корнем той же сцены).")]
        [SerializeField] private Transform _startAnchor;

        [Tooltip("Промежуточные станции по порядку от начальной к конечной.")]
        [SerializeField] private List<Transform> _intermediateAnchors = new List<Transform>();

        [Tooltip("Конечная станция ветки.")]
        [SerializeField] private Transform _endAnchor;

        [Header("Кабинка")]
        [Tooltip("Префаб кабинки (можно FBX). Должен иметь пол-коллайдер; Rigidbody докинет ParomTrolley. Пусто = fallback-примитив для прототипа.")]
        [SerializeField] private GameObject _trolleyPrefab;

        [Tooltip("Насколько кабинка висит ниже линии тросов (м).")]
        [Min(0f)] [SerializeField] private float _cabinHangDepth = 1.2f;

        [Header("Движение")]
        [Tooltip("Скорость кабинки (м/с). Лор-потолок паромов — 40 км/ч ≈ 11.1 м/с.")]
        [Range(0.5f, 11.5f)] [SerializeField] private float _speedMetersPerSecond = 9f;

        [Tooltip("Ожидание на каждой станции (с). Перекрывается массивом ниже.")]
        [Min(0f)] [SerializeField] private float _dwellSeconds = 6f;

        [Tooltip("По одному значению на станцию (порядок: старт, промежуточные, конец). -1 = взять Dwell Seconds. Пусто/другая длина = всё по дефолту.")]
        [SerializeField] private float[] _stationDwellOverrides = new float[0];

        [Header("Тросы (2 шт, LineRenderer + провис)")]
        [Tooltip("Материал тросов. Пусто = дефолтный материал линий.")]
        [SerializeField] private Material _cableMaterial;

        [Tooltip("Разнос тросов в стороны от оси маршрута (м, каждый).")]
        [Min(0f)] [SerializeField] private float _cableLateralOffset = 0.6f;

        [Tooltip("Провисание троса: доля от длины сегмента (0.05 = 5%).")]
        [Range(0f, 0.2f)] [SerializeField] private float _sagRatio = 0.05f;

        [Tooltip("Точек LineRenderer на сегмент между станциями.")]
        [Range(2, 32)] [SerializeField] private int _pointsPerSegment = 12;

        [Tooltip("Толщина троса (ширина линии, м).")]
        [Min(0.01f)] [SerializeField] private float _cableWidth = 0.15f;

        [Header("Прочее")]
        [Tooltip("Без сети (IsSpawned=false) симулировать локально — для проверки маршрута без запуска NGO.")]
        [SerializeField] private bool _previewWhenOffline = true;

        [Tooltip("Подробные логи (прибытия/отправления).")]
        [SerializeField] private bool _debugLog = false;

        // === Сеть (пишет только сервер) ===
        private readonly NetworkVariable<float> _netS = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _netDir = new NetworkVariable<int>(
            1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _netDwelling = new NetworkVariable<bool>(
            true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _netStation = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // === Runtime ===
        private readonly List<Transform> _stations = new List<Transform>();
        private readonly List<float> _cumLen = new List<float>(); // рубежи станций, метры от старта
        private float _totalLength;
        private LineRenderer _cableL;
        private LineRenderer _cableR;
        private Transform _trolley;
        private ParomTrolley _trolleyComp;

        // Симуляция сервера (или локального превью).
        private float _serverS;
        private int _serverDir = 1;
        private bool _serverDwelling = true;
        private int _serverStation;
        private float _serverDwellTimer;

        // Применение на кабинку (общее для сервера и клиентов).
        private Vector3 _lastMoveDir = Vector3.forward;
        private readonly List<Vector3> _lastAnchorPos = new List<Vector3>();
        private float _lastSag;
        private float _lastLateral;
        private int _lastPps;
        private bool _warnedInvalid;

        // === Публичное API (только чтение, для UI/дверей/квизов) ===

        /// <summary>Число станций ветки (старт + промежуточные + конец, без null).</summary>
        public int StationCount => _stations.Count;

        /// <summary>Индекс текущей/целевой станции (0 = старт). На клиенте — из сети.</summary>
        public int CurrentStationIndex => IsSpawned && !IsServer ? _netStation.Value : _serverStation;

        /// <summary>Кабинка стоит на станции (а не едет).</summary>
        public bool IsDwelling => IsSpawned && !IsServer ? _netDwelling.Value : _serverDwelling;

        /// <summary>Прогресс вдоль маршрута, метры от старта.</summary>
        public float ProgressMeters => IsSpawned && !IsServer ? _netS.Value : _serverS;

        /// <summary>Полная длина ветки, метры.</summary>
        public float TotalLength => _totalLength;

        /// <summary>Направление: +1 к концу, -1 к старту.</summary>
        public int Direction => IsSpawned && !IsServer ? _netDir.Value : _serverDir;

        /// <summary>Имя станции для табло (имя якоря или «Станция i»).</summary>
        public string GetStationName(int index)
        {
            if (index < 0 || index >= _stations.Count) return "—";
            Transform t = _stations[index];
            return t != null ? t.name : $"Станция {index}";
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                // Старт ветки: стоим на начальной.
                _serverS = 0f;
                _serverDir = 1;
                _serverDwelling = true;
                _serverStation = 0;
                _serverDwellTimer = DwellFor(0);
                PublishNet();
            }
        }

        private void Awake()
        {
            EnsureCableObjects();
            EnsureTrolley();
        }

        private void Update()
        {
            RebuildStationList();
            if (_stations.Count < 2)
            {
                if (!_warnedInvalid)
                {
                    Debug.LogWarning($"[ParomRoute:{name}] нужно минимум 2 станции (старт + конец). Кабинка и тросы скрыты.", this);
                    _warnedInvalid = true;
                }
                SetVisualsActive(false);
                return;
            }
            _warnedInvalid = false;
            SetVisualsActive(true);

            RebuildLengthCache();

            bool simulate = IsSpawned ? IsServer : _previewWhenOffline;
            float s;
            int dir;
            if (simulate)
            {
                SimulateServer(Time.deltaTime);
                if (IsSpawned) PublishNet();
                s = _serverS;
                dir = _serverDir;
            }
            else
            {
                s = _netS.Value;
                dir = _netDir.Value;
            }

            ApplyTrolley(s, dir);
            MaybeRebuildCables();
        }

        // --- Симуляция (пинг-понг с остановками, только сервер/превью) ---

        private void SimulateServer(float dt)
        {
            if (_serverDwelling)
            {
                _serverDwellTimer -= dt;
                if (_serverDwellTimer <= 0f)
                {
                    _serverDwelling = false;
                    if (_debugLog)
                        Debug.Log($"[ParomRoute:{name}] отправление со станции {GetStationName(_serverStation)} (dir={_serverDir})", this);
                }
                return;
            }

            _serverS += _serverDir * _speedMetersPerSecond * dt;

            if (_serverDir > 0)
            {
                int next = Mathf.Min(_serverStation + 1, _stations.Count - 1);
                if (_serverS >= _cumLen[next])
                    Arrive(next);
            }
            else
            {
                int next = Mathf.Max(_serverStation - 1, 0);
                if (_serverS <= _cumLen[next])
                    Arrive(next);
            }
        }

        private void Arrive(int index)
        {
            _serverS = _cumLen[index];
            _serverStation = index;
            _serverDwelling = true;
            _serverDwellTimer = DwellFor(index);
            if (index >= _stations.Count - 1) _serverDir = -1;
            else if (index <= 0) _serverDir = 1;
            if (_debugLog)
                Debug.Log($"[ParomRoute:{name}] прибытие: {GetStationName(index)} (стоянка {_serverDwellTimer:0.#} с)", this);
        }

        private float DwellFor(int stationIndex)
        {
            if (_stationDwellOverrides != null
                && _stationDwellOverrides.Length == _stations.Count
                && _stationDwellOverrides[stationIndex] >= 0f)
                return _stationDwellOverrides[stationIndex];
            return _dwellSeconds;
        }

        private void PublishNet()
        {
            _netS.Value = _serverS;
            _netDir.Value = _serverDir;
            _netDwelling.Value = _serverDwelling;
            _netStation.Value = _serverStation;
        }

        // --- Путь: живые позиции якорей (FO-safe, без кэша Vector3) ---

        private void RebuildStationList()
        {
            _stations.Clear();
            if (_startAnchor != null) _stations.Add(_startAnchor);
            if (_intermediateAnchors != null)
                for (int i = 0; i < _intermediateAnchors.Count; i++)
                    if (_intermediateAnchors[i] != null) _stations.Add(_intermediateAnchors[i]);
            if (_endAnchor != null && _endAnchor != _startAnchor) _stations.Add(_endAnchor);
        }

        private void RebuildLengthCache()
        {
            _cumLen.Clear();
            _cumLen.Add(0f);
            float acc = 0f;
            for (int i = 1; i < _stations.Count; i++)
            {
                acc += Vector3.Distance(_stations[i - 1].position, _stations[i].position);
                _cumLen.Add(acc);
            }
            _totalLength = acc;
            _serverS = Mathf.Clamp(_serverS, 0f, Mathf.Max(_totalLength, 0.001f));
        }

        /// <summary>Мировая точка пути на дистанции s метров от старта.</summary>
        private Vector3 EvaluatePath(float s, out Vector3 segmentDir)
        {
            s = Mathf.Clamp(s, 0f, Mathf.Max(_totalLength, 0.001f));
            int seg = 0;
            while (seg < _stations.Count - 2 && _cumLen[seg + 1] < s) seg++;
            float segLen = Mathf.Max(_cumLen[seg + 1] - _cumLen[seg], 0.0001f);
            float t = Mathf.Clamp01((s - _cumLen[seg]) / segLen);
            Vector3 a = _stations[seg].position;
            Vector3 b = _stations[seg + 1].position;
            segmentDir = (b - a).normalized;
            if (segmentDir.sqrMagnitude < 0.0001f) segmentDir = _lastMoveDir;
            return Vector3.Lerp(a, b, t);
        }

        // --- Кабинка ---

        private void EnsureTrolley()
        {
            if (_trolley != null) return;
            // Подхватить уже запечённую в сцене кабинку (создана в edit-mode):
            // поле _trolley не сериализовано, после перезагрузки сцены оно null.
            // Без этого каждый Play плодил бы дубликаты кабинки.
            ParomTrolley existing = GetComponentInChildren<ParomTrolley>(true);
            if (existing != null)
            {
                _trolley = existing.transform;
                _trolleyComp = existing;
                return;
            }
            GameObject go;
            if (_trolleyPrefab != null)
            {
                go = Instantiate(_trolleyPrefab, transform);
                go.name = _trolleyPrefab.name + "_Parom";
            }
            else
            {
                go = BuildFallbackTrolley();
            }
            _trolley = go.transform;
            _trolleyComp = go.GetComponent<ParomTrolley>();
            if (_trolleyComp == null) _trolleyComp = go.AddComponent<ParomTrolley>();
        }

        private GameObject BuildFallbackTrolley()
        {
            // Прототипная кабинка из примитивов: подвес + кузов + пол + 2 винта.
            var root = new GameObject("Trolley_Fallback");
            root.transform.SetParent(transform, false);

            var hanger = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hanger.name = "Hanger";
            hanger.transform.SetParent(root.transform, false);
            hanger.transform.localPosition = new Vector3(0f, -0.4f, 0f);
            hanger.transform.localScale = new Vector3(0.3f, 0.8f, 0.3f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Cabin";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, -1.3f, 0f);
            body.transform.localScale = new Vector3(2.4f, 1.2f, 4f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(root.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.75f, 0f);
            floor.transform.localScale = new Vector3(2.6f, 0.1f, 4.2f);

            for (int i = -1; i <= 1; i += 2)
            {
                var prop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                prop.name = "Prop_" + (i < 0 ? "L" : "R");
                prop.transform.SetParent(root.transform, false);
                prop.transform.localPosition = new Vector3(i * 1.5f, -1.1f, -2.2f);
                prop.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                prop.transform.localScale = new Vector3(0.9f, 0.08f, 0.9f);
            }
            return root;
        }

        private void ApplyTrolley(float s, int dir)
        {
            if (_trolley == null) return;
            Vector3 pathPoint = EvaluatePath(s, out Vector3 segDir);
            Vector3 moveDir = segDir * dir;
            if (moveDir.sqrMagnitude > 0.0001f) _lastMoveDir = moveDir.normalized;
            Vector3 worldPos = pathPoint + Vector3.down * _cabinHangDepth;
            _trolley.position = worldPos;
            if (_lastMoveDir.sqrMagnitude > 0.0001f)
                _trolley.rotation = Quaternion.LookRotation(_lastMoveDir, Vector3.up);
        }

        private void SetVisualsActive(bool active)
        {
            if (_trolley != null && _trolley.gameObject.activeSelf != active)
                _trolley.gameObject.SetActive(active);
            if (_cableL != null && _cableL.gameObject.activeSelf != active)
                _cableL.gameObject.SetActive(active);
            if (_cableR != null && _cableR.gameObject.activeSelf != active)
                _cableR.gameObject.SetActive(active);
        }

        // --- Тросы ---

        private void EnsureCableObjects()
        {
            _cableL = EnsureOneCable("_CableL");
            _cableR = EnsureOneCable("_CableR");
        }

        private LineRenderer EnsureOneCable(string childName)
        {
            Transform child = transform.Find(childName);
            LineRenderer lr;
            if (child != null)
            {
                lr = child.GetComponent<LineRenderer>();
                if (lr == null) lr = child.gameObject.AddComponent<LineRenderer>();
            }
            else
            {
                var go = new GameObject(childName);
                go.transform.SetParent(transform, false);
                lr = go.AddComponent<LineRenderer>();
            }
            lr.useWorldSpace = false; // локальные точки → едут с миром бесплатно (FO 🟢)
            lr.loop = false;
            if (_cableMaterial != null) lr.sharedMaterial = _cableMaterial;
            lr.startWidth = _cableWidth;
            lr.endWidth = _cableWidth;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        /// <summary>Перестроить тросы вручную (кнопка в инспекторе / контекстное меню).</summary>
        [ContextMenu("Rebuild Cables")]
        public void RebuildCables()
        {
            RebuildStationList();
            if (_stations.Count < 2) return;
            EnsureCableObjects();
            BuildCable(_cableL, -1f);
            BuildCable(_cableR, +1f);
            SnapshotCableState();
        }

        private void MaybeRebuildCables()
        {
            if (_cableL == null || _cableR == null)
            {
                EnsureCableObjects();
                BuildCable(_cableL, -1f);
                BuildCable(_cableR, +1f);
                SnapshotCableState();
                return;
            }
            if (CableParamsChanged() || AnchorsMoved())
            {
                BuildCable(_cableL, -1f);
                BuildCable(_cableR, +1f);
                SnapshotCableState();
                ApplyCableStyle();
            }
        }

        private void ApplyCableStyle()
        {
            for (int i = 0; i < 2; i++)
            {
                LineRenderer lr = i == 0 ? _cableL : _cableR;
                if (lr == null) continue;
                if (_cableMaterial != null) lr.sharedMaterial = _cableMaterial;
                lr.startWidth = _cableWidth;
                lr.endWidth = _cableWidth;
            }
        }

        private bool CableParamsChanged()
        {
            return !Mathf.Approximately(_lastSag, _sagRatio)
                || !Mathf.Approximately(_lastLateral, _cableLateralOffset)
                || _lastPps != _pointsPerSegment;
        }

        private bool AnchorsMoved()
        {
            if (_lastAnchorPos.Count != _stations.Count) return true;
            const float eps = 0.001f;
            for (int i = 0; i < _stations.Count; i++)
                if ((_stations[i].position - _lastAnchorPos[i]).sqrMagnitude > eps * eps)
                    return true;
            return false;
        }

        private void SnapshotCableState()
        {
            _lastAnchorPos.Clear();
            for (int i = 0; i < _stations.Count; i++)
                _lastAnchorPos.Add(_stations[i].position);
            _lastSag = _sagRatio;
            _lastLateral = _cableLateralOffset;
            _lastPps = _pointsPerSegment;
        }

        private void BuildCable(LineRenderer lr, float side)
        {
            int segs = _stations.Count - 1;
            int total = segs * _pointsPerSegment + 1;
            lr.positionCount = total;
            int idx = 0;
            for (int sgi = 0; sgi < segs; sgi++)
            {
                Vector3 aW = _stations[sgi].position;
                Vector3 bW = _stations[sgi + 1].position;
                Vector3 dirW = bW - aW;
                float segLen = dirW.magnitude;
                Vector3 dirN = segLen > 0.0001f ? dirW / segLen : Vector3.forward;
                // Боковой вектор в горизонтальной плоскости (тросы рядом, не друг над другом).
                Vector3 perp = Vector3.Cross(Vector3.up, dirN);
                if (perp.sqrMagnitude < 0.0001f) perp = Vector3.right;
                perp.Normalize();
                for (int j = 0; j < _pointsPerSegment; j++)
                {
                    float t = j / (float)_pointsPerSegment;
                    Vector3 p = Vector3.Lerp(aW, bW, t);
                    p.y -= Mathf.Sin(Mathf.PI * t) * segLen * _sagRatio; // парабола провиса
                    p += perp * (side * _cableLateralOffset);
                    lr.SetPosition(idx++, transform.InverseTransformPoint(p));
                }
            }
            // Последняя точка — точно конечная станция.
            Vector3 endW = _stations[_stations.Count - 1].position;
            {
                Vector3 aW = _stations[_stations.Count - 2].position;
                Vector3 dirN = (endW - aW).normalized;
                if (dirN.sqrMagnitude < 0.0001f) dirN = Vector3.forward;
                Vector3 perp = Vector3.Cross(Vector3.up, dirN);
                if (perp.sqrMagnitude < 0.0001f) perp = Vector3.right;
                perp.Normalize();
                endW += perp * (side * _cableLateralOffset);
            }
            lr.SetPosition(idx, transform.InverseTransformPoint(endW));
        }

        private void OnValidate()
        {
            _speedMetersPerSecond = Mathf.Clamp(_speedMetersPerSecond, 0.5f, 11.5f);
            _dwellSeconds = Mathf.Max(_dwellSeconds, 0f);
            _cabinHangDepth = Mathf.Max(_cabinHangDepth, 0f);
            _cableLateralOffset = Mathf.Max(_cableLateralOffset, 0f);
            _sagRatio = Mathf.Clamp(_sagRatio, 0f, 0.2f);
            _pointsPerSegment = Mathf.Clamp(_pointsPerSegment, 2, 32);
            _cableWidth = Mathf.Max(_cableWidth, 0.01f);
            if (_stationDwellOverrides != null)
                for (int i = 0; i < _stationDwellOverrides.Length; i++)
                    _stationDwellOverrides[i] = Mathf.Max(_stationDwellOverrides[i], -1f);
        }

        private void OnDrawGizmos()
        {
            // Превью ветки в редакторе: жёлтая полилиния + сферы станций.
            var pts = new List<Transform>();
            if (_startAnchor != null) pts.Add(_startAnchor);
            if (_intermediateAnchors != null)
                for (int i = 0; i < _intermediateAnchors.Count; i++)
                    if (_intermediateAnchors[i] != null) pts.Add(_intermediateAnchors[i]);
            if (_endAnchor != null && _endAnchor != _startAnchor) pts.Add(_endAnchor);
            if (pts.Count < 2) return;
            Gizmos.color = Color.yellow;
            for (int i = 1; i < pts.Count; i++)
                Gizmos.DrawLine(pts[i - 1].position, pts[i].position);
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(pts[0].position, 1f);
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(pts[pts.Count - 1].position, 1f);
            Gizmos.color = Color.cyan;
            for (int i = 1; i < pts.Count - 1; i++)
                Gizmos.DrawSphere(pts[i].position, 0.7f);
        }
    }
}
