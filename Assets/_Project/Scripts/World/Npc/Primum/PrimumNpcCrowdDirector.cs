using System.Collections;
using System.Collections.Generic;
using ProjectC.Core;
using ProjectC.Quests;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectC.World.NpcPrimum
{
    /// <summary>
    /// Директор пилотной толпы PRIMUM.
    ///
    ///  • живёт в сцене на объекте PRIMUM_NPC_CROWD (ребёнок WorldRoot_0_0);
    ///  • собирает дома по HouseMarker (группировка по houseId) и выдаёт резидентов ≤ maxResidentsPerHouse;
    ///  • спавнит NPC батчами по бюджету времени и тикает их по фазовому расписанию с кадровым бюджетом;
    ///  • следит за сдвигом WorldRoot_0_0 (Floating Origin) и пере-выдаёт агентам цели.
    ///
    /// Стресс-тест: поле tier переключает размер толпы (100 / 300 / 1000 / 5000 / 10000) прямо в Play Mode —
    /// толпа пере-спавнится. Домовых мест 362 × maxResidentsPerHouse; всё, что выше, — уличная толпа,
    /// рассеянная вокруг домов.
    ///
    /// Никаких NetworkObject/Rigidbody/CharacterController у NPC нет.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)]
    public sealed class PrimumNpcCrowdDirector : MonoBehaviour
    {
        /// <summary>Размер толпы для стресс-теста. Config = взять targetCount из конфига.</summary>
        public enum CrowdTier
        {
            Config = 0,
            T100 = 100,
            T300 = 300,
            T1000 = 1000,
            T5000 = 5000,
            T10000 = 10000
        }

        [Header("Конфиг")]
        [SerializeField] private PrimumCrowdConfig config;

        [Header("Стресс-тест")]
        [Tooltip("Размер толпы. Config = targetCount из конфига. Переключение в Play Mode пере-спавнит толпу.")]
        [SerializeField] private CrowdTier tier = CrowdTier.Config;

        [Tooltip("Ручное переопределение, если tier = Config. 0 = взять targetCount из конфига.")]
        [SerializeField] private int overrideTargetCount = 0;

        [Tooltip("Бюджет времени на спавн, мс на кадр. Больше — быстрее спавн, но просадки кадра.")]
        [SerializeField] private float spawnBudgetMs = 4f;

        [Tooltip("Бюджет времени на тики поведения, мс на кадр.")]
        [SerializeField] private float tickBudgetMs = 2f;

        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private bool logStats = true;

        private const string WorldRootName = "WorldRoot_0_0";
        private const string ContainerName = "Residents";

        /// <summary>Радиус рассева «уличной толпы» вокруг дома, м.</summary>
        private const float StreetScatterRadius = 22f;

        /// <summary>Радиус рассева вторых-четвёртых резидентов одного дома, м (иначе стоят стопкой в одной точке).</summary>
        private const float ResidentScatterRadius = 6f;

        /// <summary>Целевое число тиков поведения в секунду: от него считается растяжка интервала решений.</summary>
        private const float TicksPerSecondTarget = 800f;

        /// <summary>Как часто писать сводку стресс-теста, сек.</summary>
        private const float StatsInterval = 5f;

        private struct HouseAggregate
        {
            public string houseId;
            public string chunkId;
            public Vector3 center;
            public int markers;
            public int residents;
        }

        private readonly List<PrimumCrowdNpc> _npcs = new List<PrimumCrowdNpc>();
        private readonly List<float> _tickIntervals = new List<float>();
        private readonly Dictionary<string, HouseAggregate> _houses = new Dictionary<string, HouseAggregate>();
        private readonly Dictionary<string, int> _residentsByHouse = new Dictionary<string, int>();

        private readonly System.Diagnostics.Stopwatch _tickClock = new System.Diagnostics.Stopwatch();

        private Transform _worldRoot;
        private Transform _cameraTransform;
        private SpringArmCamera _cameraRig;
        private Transform _container;
        private Vector3 _lastWorldRootPosition;
        private float _cameraRefreshTime;
        private bool _spawning;
        private int _spawnFailed;
        private int _housesUsed;
        private int _tickCursor;

        private CrowdTier _appliedTier;
        private bool _tierDirty;

        private float _statsTime;
        private int _statsTicks;
        private float _statsTickMs;

        public Transform WorldRoot => _worldRoot;
        public Transform CameraTransform => _cameraTransform;
        public int AliveCount => _npcs.Count;
        public int SpawnFailed => _spawnFailed;
        public int HousesUsed => _housesUsed;
        public PrimumCrowdConfig Config => config;
        public CrowdTier Tier => tier;

        /// <summary>Целевое число NPC: tier → ручное переопределение → конфиг.</summary>
        public int TargetCount
        {
            get
            {
                if (tier != CrowdTier.Config) return (int)tier;
                if (overrideTargetCount > 0) return overrideTargetCount;
                return config != null ? config.targetCount : 0;
            }
        }

        /// <summary>Радиус аниматора: на больших тирах сужается, иначе анимация съедает кадр.</summary>
        public float AnimatorRadius
        {
            get
            {
                if (config == null) return 120f;
                switch (tier)
                {
                    case CrowdTier.T10000: return 60f;
                    case CrowdTier.T5000: return 80f;
                    case CrowdTier.T1000: return 100f;
                    default: return config.animatorRadius;
                }
            }
        }

        /// <summary>Avoidance отключается с 300+ NPC: иначе агенты душат друг друга и CPU.</summary>
        public bool AvoidanceDisabled => (config != null && config.disableAvoidance) || TargetCount >= 300;

        private void Awake()
        {
            _worldRoot = ResolveWorldRoot();
            _container = transform.Find(ContainerName);
            if (_container == null)
            {
                var go = new GameObject(ContainerName);
                go.transform.SetParent(transform, false);
                _container = go.transform;
            }
            if (_worldRoot != null) _lastWorldRootPosition = _worldRoot.position;
            _appliedTier = tier;
        }

        private void Start()
        {
            if (spawnOnStart) SpawnNow();
        }

        private void OnValidate()
        {
            // Переключатель тира в Play Mode пере-спавнит толпу (без рекомпиляции и рестарта).
            if (Application.isPlaying && _appliedTier != tier) _tierDirty = true;
        }

        private Transform ResolveWorldRoot()
        {
            if (transform.parent != null && transform.parent.name == WorldRootName) return transform.parent;
            var found = GameObject.Find(WorldRootName);
            if (found != null) return found.transform;
            Debug.LogError("[PrimumCrowd] WorldRoot_0_0 не найден: NPC не поедут вместе с миром при Floating Origin.");
            return transform.parent;
        }

        /// <summary>Публичный запуск (из инспектора, тестов, будущих тиров).</summary>
        public void SpawnNow()
        {
            if (_spawning) return;
            if (config == null) { Debug.LogError("[PrimumCrowd] config не назначен."); return; }
            if (config.npcTemplate == null) { Debug.LogError("[PrimumCrowd] npcTemplate не назначен."); return; }
            if (_worldRoot == null) return;
            StartCoroutine(SpawnRoutine());
        }

        public void ClearCrowd()
        {
            StopAllCoroutines();
            _spawning = false;
            ClearNpcs();
        }

        private void ClearNpcs()
        {
            for (int i = 0; i < _npcs.Count; i++)
                if (_npcs[i] != null) Destroy(_npcs[i].gameObject);
            _npcs.Clear();
            _tickIntervals.Clear();
            _residentsByHouse.Clear();
            _spawnFailed = 0;
            _housesUsed = 0;
            _tickCursor = 0;
            _statsTime = 0f;
            _statsTicks = 0;
            _statsTickMs = 0f;
        }

        [ContextMenu("Primum Crowd / Respawn")]
        private void RespawnFromContextMenu()
        {
            ClearCrowd();
            SpawnNow();
        }

        private IEnumerator SpawnRoutine()
        {
            _spawning = true;
            _appliedTier = tier;
            ClearNpcs();
            BuildHouseIndex();

            int target = TargetCount;
            if (target <= 0) { _spawning = false; yield break; }

            var houseOrder = BuildHouseOrder();
            int houseCursor = 0;
            int spawned = 0;
            int consecutiveFails = 0;
            int failLimit = Mathf.Max(500, target * 2);
            int logStep = Mathf.Max(100, target / 10);
            int nextLog = logStep;

            var catalog = config.modelCatalog;
            var defs = config.residentDefinitions;

            // На больших тирах решения принимаем реже: 800 тиков/с суммарно — уже предел кадра.
            float intervalScale = Mathf.Clamp(target / TicksPerSecondTarget, 1f, 4f);
            float spawnBudget = Mathf.Max(0.5f, spawnBudgetMs);
            var budget = new System.Diagnostics.Stopwatch();

            if (logStats)
                Debug.Log($"[PrimumCrowd] spawn start: target={target} tier={tier} houses={_houses.Count} " +
                          $"cap={_houses.Count * config.maxResidentsPerHouse} intervalScale={intervalScale:F2}");

            while (spawned < target)
            {
                budget.Reset();
                budget.Start();
                int inBatch = 0;

                while (spawned < target && inBatch < 4096 && budget.Elapsed.TotalMilliseconds < spawnBudget)
                {
                    inBatch++;

                    if (!TryPlace(ref houseCursor, houseOrder, out var houseId, out var chunkId,
                                  out var homeCenter, out var finalSpawn))
                    {
                        _spawnFailed++;
                        if (++consecutiveFails >= failLimit)
                        {
                            Debug.LogError($"[PrimumCrowd] спавн прерван: {consecutiveFails} промахов подряд " +
                                           $"(spawned={spawned}/{target}). Проверь навмеш WorldRoot_0_0.");
                            _spawning = false;
                            yield break;
                        }
                        continue;
                    }
                    consecutiveFails = 0;

                    var model = (catalog != null && catalog.Length > 0) ? catalog[spawned % catalog.Length] : null;
                    var definition = (defs != null && defs.Length > 0) ? defs[spawned % defs.Length] : null;
                    var controller = ResolveController(model);

                    var go = Instantiate(config.npcTemplate, _container, false);
                    go.name = definition != null && !string.IsNullOrEmpty(definition.npcId)
                        ? (spawned < (defs != null ? defs.Length : 0) ? definition.npcId : definition.npcId + "_" + spawned.ToString("D5"))
                        : "primum_crowd_" + spawned.ToString("D5");

                    var npc = go.GetComponent<PrimumCrowdNpc>();
                    if (npc == null)
                    {
                        Debug.LogError("[PrimumCrowd] В шаблоне нет PrimumCrowdNpc.");
                        Destroy(go);
                        _spawning = false;
                        yield break;
                    }

                    Vector3 homeLocal = _worldRoot.InverseTransformPoint(homeCenter);
                    float phase = (spawned * 0.137f) % 1f;
                    npc.Initialize(this, config, definition, houseId, chunkId, homeLocal, finalSpawn, model, controller, phase);

                    _npcs.Add(npc);
                    _tickIntervals.Add(Random.Range(config.decisionIntervalMin, config.decisionIntervalMax) * intervalScale);
                    spawned++;

                    if (logStats && spawned >= nextLog)
                    {
                        Debug.Log($"[PrimumCrowd] spawn {spawned}/{target} (housesUsed={_housesUsed}, failed={_spawnFailed})");
                        nextLog += logStep;
                    }
                }

                budget.Stop();
                yield return null;
            }

            _lastWorldRootPosition = _worldRoot.position;
            _spawning = false;
            if (logStats) LogStats();
        }

        /// <summary>
        /// Выбор точки спавна: сначала дома со свободной ёмкостью (резиденты), затем уличная толпа —
        /// рассев вокруг случайного дома (иначе все лишние встают в один центр навмеша).
        /// </summary>
        private bool TryPlace(ref int houseCursor, List<string> houseOrder, out string houseId, out string chunkId,
                              out Vector3 homeCenter, out Vector3 spawnPos)
        {
            houseId = string.Empty;
            chunkId = string.Empty;
            homeCenter = Vector3.zero;
            spawnPos = Vector3.zero;
            if (houseOrder == null || houseOrder.Count == 0) return false;

            int count = houseOrder.Count;
            for (int probe = 0; probe < count; probe++)
            {
                if (houseCursor >= count) houseCursor = 0;
                string candidateId = houseOrder[houseCursor++];
                var aggregate = _houses[candidateId];
                if (aggregate.residents >= config.maxResidentsPerHouse) continue;

                // Первый резидент встаёт у самого дома, следующие — в радиусе ResidentScatterRadius:
                // без этого 2–4 жителя одного дома спавнятся в одну точку навмеша и стоят стопкой.
                bool sampled;
                Vector3 pos;
                if (aggregate.residents == 0) sampled = TrySampleHouse(aggregate.center, out pos);
                else sampled = TrySampleNear(aggregate.center, ResidentScatterRadius, out pos);

                if (!sampled)
                {
                    // Дом не стоит на навмеше: закрываем его ёмкость и идём дальше.
                    aggregate.residents = config.maxResidentsPerHouse;
                    _houses[candidateId] = aggregate;
                    continue;
                }

                aggregate.residents++;
                _houses[candidateId] = aggregate;
                _residentsByHouse[candidateId] = aggregate.residents;
                if (aggregate.residents == 1) _housesUsed++;

                houseId = candidateId;
                chunkId = aggregate.chunkId;
                homeCenter = aggregate.center;
                spawnPos = pos;
                return true;
            }

            // Домовых мест не осталось: уличная толпа, рассеянная вокруг случайного дома.
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var key = houseOrder[Random.Range(0, count)];
                var aggregate = _houses[key];
                if (!TrySampleNear(aggregate.center, StreetScatterRadius, out var pos)) continue;

                houseId = key;
                chunkId = aggregate.chunkId;
                homeCenter = aggregate.center;
                spawnPos = pos;
                return true;
            }

            return false;
        }

        private RuntimeAnimatorController ResolveController(GameObject model)
        {
            if (model == null) return config.animatorControllerFemale;
            string n = model.name;
            bool female = n.Contains("Female") || n.Contains("_Girl") || n.Contains("_Boy");
            // Boy/Girl — детские модели: женский набор клипов ближе по росту.
            return female || !n.Contains("Male")
                ? (config.animatorControllerFemale != null ? config.animatorControllerFemale : config.animatorControllerMale)
                : (config.animatorControllerMale != null ? config.animatorControllerMale : config.animatorControllerFemale);
        }

        private bool TrySampleHouse(Vector3 worldCenter, out Vector3 position)
        {
            if (NavMesh.SamplePosition(worldCenter, out var hit, config.houseSampleRadius, NavMesh.AllAreas))
            {
                position = hit.position;
                return true;
            }
            if (NavMesh.SamplePosition(worldCenter, out hit, config.houseSampleFallbackRadius, NavMesh.AllAreas))
            {
                position = hit.position;
                return true;
            }
            position = default;
            return false;
        }

        /// <summary>Рассев вокруг точки: случайная точка в диске radius, ближайшая к навмешу.</summary>
        private bool TrySampleNear(Vector3 worldCenter, float radius, out Vector3 position)
        {
            for (int i = 0; i < 6; i++)
            {
                Vector2 disc = Random.insideUnitCircle * radius;
                Vector3 candidate = worldCenter + new Vector3(disc.x, 0f, disc.y);
                if (NavMesh.SamplePosition(candidate, out var hit, 6f, NavMesh.AllAreas))
                {
                    position = hit.position;
                    return true;
                }
            }
            return TrySampleHouse(worldCenter, out position);
        }

        private void BuildHouseIndex()
        {
            _houses.Clear();
            _residentsByHouse.Clear();
            _housesUsed = 0;

            var markers = FindObjectsByType<HouseMarker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                if (m == null || string.IsNullOrEmpty(m.houseId)) continue;
                if (_houses.TryGetValue(m.houseId, out var aggregate))
                {
                    aggregate.center += m.MapPosition;
                    aggregate.markers++;
                    _houses[m.houseId] = aggregate;
                }
                else
                {
                    _houses[m.houseId] = new HouseAggregate
                    {
                        houseId = m.houseId,
                        chunkId = m.chunkId,
                        center = m.MapPosition,
                        markers = 1,
                        residents = 0
                    };
                }
            }

            var keys = new List<string>(_houses.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                var a = _houses[keys[i]];
                if (a.markers > 1) a.center /= a.markers;
                _houses[keys[i]] = a;
            }

            if (logStats)
                Debug.Log($"[PrimumCrowd] house markers={markers.Length}, уникальных домов={_houses.Count}.");
        }

        private List<string> BuildHouseOrder()
        {
            var order = new List<string>(_houses.Keys);
            order.Sort((a, b) =>
            {
                int ha = StableHash(a), hb = StableHash(b);
                if (ha != hb) return ha.CompareTo(hb);
                return string.CompareOrdinal(a, b);
            });
            return order;
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
                return hash & 0x7FFFFFFF;
            }
        }

        private void LateUpdate()
        {
            if (_tierDirty && !_spawning)
            {
                _tierDirty = false;
                if (logStats) Debug.Log($"[PrimumCrowd] tier → {tier} (target={TargetCount}), пере-спавн.");
                ClearCrowd();
                SpawnNow();
            }

            RefreshCamera();

            if (_worldRoot != null)
            {
                Vector3 rootPosition = _worldRoot.position;
                if ((rootPosition - _lastWorldRootPosition).sqrMagnitude > 1e-6f)
                {
                    _lastWorldRootPosition = rootPosition;
                    for (int i = 0; i < _npcs.Count; i++)
                        if (_npcs[i] != null) _npcs[i].ApplyWorldRebase();
                    if (logStats)
                        Debug.Log($"[PrimumCrowd] rebase: корень сдвинулся на {rootPosition}, NPC пере-выдали цели.");
                }
            }

            if (_npcs.Count == 0) return;

            float now = Time.time;
            Vector3 cameraPosition = _cameraTransform != null ? _cameraTransform.position : Vector3.zero;

            // Тики по кадровому бюджету с курсором: на 5000–10000 нельзя тикать всех в одном кадре.
            int count = _npcs.Count;
            int visited = 0;
            int ticks = 0;
            _tickClock.Reset();
            _tickClock.Start();

            while (visited < count)
            {
                if (_tickCursor >= count) _tickCursor = 0;
                int i = _tickCursor++;
                visited++;

                var npc = _npcs[i];
                if (npc == null || !npc.ReadyForTick(now)) continue;

                npc.Tick(now, cameraPosition);
                npc.SetNextTick(now + _tickIntervals[i]);
                ticks++;

                if (_tickClock.Elapsed.TotalMilliseconds >= tickBudgetMs) break;
            }

            _tickClock.Stop();

            if (!logStats) return;
            _statsTime += Time.unscaledDeltaTime;
            _statsTicks += ticks;
            _statsTickMs += (float)_tickClock.Elapsed.TotalMilliseconds;
            if (_statsTime >= StatsInterval)
            {
                float fps = 1f / Mathf.Max(0.0001f, Time.smoothDeltaTime);
                Debug.Log($"[PrimumCrowd] n={count} target={TargetCount} tier={tier} " +
                          $"ticks/s={_statsTicks / _statsTime:F0} tickLoop={_statsTickMs / Mathf.Max(0.0001f, _statsTime):F2} мс/кадр " +
                          $"fps={fps:F0}");
                _statsTime = 0f;
                _statsTicks = 0;
                _statsTickMs = 0f;
            }
        }

        private void RefreshCamera()
        {
            float now = Time.unscaledTime;
            if (now - _cameraRefreshTime < 1f) return;
            _cameraRefreshTime = now;

            // Камера игрока — риг SpringArmCamera (его спавнит NetworkPlayer уже после Bootstrap).
            // Camera.main здесь не годится: в этот момент он отдаёт статичную MainCamera BootstrapScene,
            // которая висит в сотнях километров от города, и LOD толпы гасит визуал/аниматор/нейм-плейт
            // у всех NPC (visualRadius 400 / animatorRadius 120 / nameplateRadius 28).
            // Та же конвенция, что в GazeAutofocus / FarFocusController / ShipShadowBudget.
            if (_cameraRig == null) _cameraRig = FindAnyObjectByType<SpringArmCamera>();

            var rigCamera = _cameraRig != null ? _cameraRig.CameraComponent : null;
            if (rigCamera != null)
            {
                _cameraTransform = rigCamera.transform;
                return;
            }

            // Рига ещё нет (или он выключен) — держим запас Camera.main до его появления.
            var main = Camera.main;
            if (main != null) _cameraTransform = main.transform;
        }

        private void LogStats()
        {
            int maxPerHouse = 0;
            string maxHouse = string.Empty;
            foreach (var kv in _residentsByHouse)
            {
                if (kv.Value <= maxPerHouse) continue;
                maxPerHouse = kv.Value;
                maxHouse = kv.Key;
            }

            Debug.Log($"[PrimumCrowd] spawned={_npcs.Count} target={TargetCount} tier={tier} houses={_houses.Count} " +
                      $"housesUsed={_housesUsed} samplesFailed={_spawnFailed} maxPerHouse={maxPerHouse} (house='{maxHouse}') " +
                      $"cap={_houses.Count * config.maxResidentsPerHouse}");
        }
    }
}
