using System.Collections;
using System.Collections.Generic;
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
    ///  • спавнит NPC батчами, тикает их по фазовому расписанию;
    ///  • следит за сдвигом WorldRoot_0_0 (Floating Origin) и пере-выдаёт агентам цели.
    ///
    /// Никаких NetworkObject/Rigidbody/CharacterController у NPC нет.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)]
    public sealed class PrimumNpcCrowdDirector : MonoBehaviour
    {
        [Header("Конфиг")]
        [SerializeField] private PrimumCrowdConfig config;

        [Tooltip("Переопределение количества для эксперимента. 0 = взять из конфига.")]
        [SerializeField] private int overrideTargetCount = 0;

        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private bool logStats = true;

        private const string WorldRootName = "WorldRoot_0_0";
        private const string ContainerName = "Residents";

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

        private Transform _worldRoot;
        private Transform _cameraTransform;
        private Transform _container;
        private Vector3 _lastWorldRootPosition;
        private float _cameraRefreshTime;
        private bool _spawning;
        private int _spawnFailed;
        private int _housesUsed;

        public Transform WorldRoot => _worldRoot;
        public Transform CameraTransform => _cameraTransform;
        public int AliveCount => _npcs.Count;
        public int SpawnFailed => _spawnFailed;
        public int HousesUsed => _housesUsed;
        public PrimumCrowdConfig Config => config;

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
        }

        private void Start()
        {
            if (spawnOnStart) SpawnNow();
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
            ClearNpcs();
            BuildHouseIndex();

            int target = overrideTargetCount > 0 ? overrideTargetCount : config.targetCount;
            if (target <= 0) { _spawning = false; yield break; }

            var houseOrder = BuildHouseOrder();
            int houseCursor = 0;
            int spawned = 0;
            int batch = Mathf.Max(1, config.spawnBatchPerFrame);
            var catalog = config.modelCatalog;
            var defs = config.residentDefinitions;

            while (spawned < target)
            {
                int inBatch = 0;
                while (inBatch < batch && spawned < target)
                {
                    inBatch++;
                    string houseId = null, chunkId = null;
                    Vector3 homeCenter = Vector3.zero;

                    // Ищем дом со свободной ёмкостью и навмешем вокруг него.
                    bool placedOnHouse = false;
                    for (int probe = 0; probe < houseOrder.Count; probe++)
                    {
                        if (houseCursor >= houseOrder.Count) houseCursor = 0;
                        string candidateId = houseOrder[houseCursor++];
                        var aggregate = _houses[candidateId];
                        if (aggregate.residents >= config.maxResidentsPerHouse) continue;

                        if (!TrySampleHouse(aggregate.center, out var spawnPos))
                        {
                            // Дом не стоит на навмеше: закрываем его ёмкость и идём дальше.
                            aggregate.residents = config.maxResidentsPerHouse;
                            _houses[candidateId] = aggregate;
                            _spawnFailed++;
                            continue;
                        }

                        aggregate.residents++;
                        _houses[candidateId] = aggregate;
                        _residentsByHouse[candidateId] = aggregate.residents;
                        if (aggregate.residents == 1) _housesUsed++;

                        houseId = candidateId;
                        chunkId = aggregate.chunkId;
                        homeCenter = aggregate.center;
                        placedOnHouse = true;
                        break;
                    }

                    if (!placedOnHouse)
                    {
                        // Домов не хватило (тир 10000): NPC без дома, уличная толпа.
                        // На T1–T3 недостижимо (362 дома × 4 = 1448).
                        houseId = string.Empty;
                        chunkId = string.Empty;
                        if (!TrySampleAnywhere(out homeCenter)) { _spawnFailed++; continue; }
                    }

                    if (!TrySampleHouse(homeCenter, out var finalSpawn)) { _spawnFailed++; continue; }

                    var model = (catalog != null && catalog.Length > 0) ? catalog[spawned % catalog.Length] : null;
                    var definition = (defs != null && defs.Length > 0) ? defs[spawned % defs.Length] : null;
                    var controller = ResolveController(model);

                    var go = Instantiate(config.npcTemplate, _container, false);
                    go.name = definition != null && !string.IsNullOrEmpty(definition.npcId)
                        ? definition.npcId
                        : "primum_crowd_" + spawned.ToString("D4");

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
                    _tickIntervals.Add(Random.Range(config.decisionIntervalMin, config.decisionIntervalMax));
                    spawned++;
                }

                yield return null;
            }

            _lastWorldRootPosition = _worldRoot.position;
            _spawning = false;
            if (logStats) LogStats();
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

        private bool TrySampleAnywhere(out Vector3 position)
        {
            // Резерв для тиров, где домов не хватает: берём случайный дом как «район обитания».
            if (_houses.Count == 0) { position = default; return false; }
            int index = Random.Range(0, _houses.Count);
            int i = 0;
            foreach (var kv in _houses)
            {
                if (i++ != index) continue;
                return TrySampleHouse(kv.Value.center, out position);
            }
            position = default;
            return false;
        }

        private void BuildHouseIndex()
        {
            _houses.Clear();
            _residentsByHouse.Clear();
            _housesUsed = 0;

            var markers = FindObjectsByType<HouseMarker>(FindObjectsInactive.Exclude);
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

            for (int i = 0; i < _npcs.Count; i++)
            {
                var npc = _npcs[i];
                if (npc == null || !npc.ReadyForTick(now)) continue;
                npc.Tick(now, cameraPosition);
                npc.SetNextTick(now + _tickIntervals[i]);
            }
        }

        private void RefreshCamera()
        {
            if (_cameraTransform != null) return;
            float now = Time.unscaledTime;
            if (now - _cameraRefreshTime < 1f) return;
            _cameraRefreshTime = now;
            var cam = Camera.main;
            if (cam != null) _cameraTransform = cam.transform;
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

            Debug.Log($"[PrimumCrowd] spawned={_npcs.Count} houses={_houses.Count} housesUsed={_housesUsed} " +
                      $"samplesFailed={_spawnFailed} maxPerHouse={maxPerHouse} (house='{maxHouse}') " +
                      $"target={(overrideTargetCount > 0 ? overrideTargetCount : config.targetCount)}");
        }
    }
}
