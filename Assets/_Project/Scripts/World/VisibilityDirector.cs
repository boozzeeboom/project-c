// Project C: директор видимости (T-CULL01 / Stage 6, H6) — фундамент управляемой видимости.
//
// Зачем: к Stage 4c видимостью мира управляют шесть независимых систем (бюджеты теней города,
// дверей, ферм и корабля, culler руин, пул ламп), и у каждой свой набор камер и своя выборка.
// Директор — единый слой поверх них: иерархия групп (мир → чанк → дом; двери, руины, фермы,
// спутники, корабли, интерьеры, лампы), единый мультикамерный union и реестр владения
// («кто какое свойство какой группы пишет»), чтобы новые политики не включали/выключали то, что
// уже ведёт существующий бюджет.
//
// Владение (ownership map, подробно — docs/dev/T-CULL01_STAGE6_REPORT.md §1):
//   shadowCastingMode  — CityShadowBudget (город), DoorVisibilityBudget (слой 10), ShadowBudget (фермы),
//                        ShipShadowBudget (LODGroup heavy-корабля). Директор тени НЕ пишет нигде.
//   Renderer.enabled   — DetailDistanceCuller (руины). Директор enabled НЕ пишет нигде.
//   layerCullDistances — DoorVisibilityBudget (слой 10); camera.far — ViewDistanceApplier.
//   Light.enabled      — PrimumLightPool.
//   forceRenderingOff  — ТОЛЬКО директор (больше никто в проекте это свойство не пишет), и только
//                        у направляемых групп: дома города, фермы, спутники. Двери/руины/корабли/
//                        интерьеры/лампы/сетевые объекты — только наблюдаются (счётчики), не трогаются.
//
// Политика MVP: «под-пиксельный» culling — рендерер направляемой группы гасится через
// forceRenderingOff, когда его габарит на экране меньше minScreenPixels во ВСЕХ камерах union,
// и возвращается выше minScreenPixels·(1 + hysteresis). Тик 3 Гц, гистерезис 12 %.
// Это не трогает видимую геометрию: объект ~2 px на экране не читается, но платит draw call.
//
// Камеры union: тип Game, активна, без RenderTexture, far ≥ 50, рисует слой мира (10, та же
// конвенция, что в DoorVisibilityBudget/CityShadowBudget) и дотягивается до мира (якорь хотя бы
// одной направляемой группы ближе far). Так отсекаются AtmosphereCamera (маска только атмосферы)
// и парковочная MainCamera BootstrapScene (сотни км от города); ThirdPersonCamera_0 и WorldCamera
// (far 1e6, если активна) проходят. Camera.main не используется.
//
// Floating Origin: мировые позиции не кэшируются. Все направляемые группы — статика под WorldRoot_0_0,
// а ребаз переносит именно WorldRoot_0_0; поэтому у участника хранится только смещение центра bounds
// ОТНОСИТЕЛЬНО живого Transform WorldRoot_0_0 (и габарит — он инвариантен к переносу), а мировая
// точка считается каждый тик как WorldRoot.position + offset. После ребаза (смена frameGeneration у
// GlobalMotionControlledRebaseSlice — success) и после отката (WorldRoot «прыгнул» без смены
// frameGeneration — rollback) директор пересчитывает смещения, пересобирает union и видимость сразу;
// скачок камеры дальше порога (телепорт) — только немедленный пересчёт видимости.
//
// Чего НЕ делает: не трогает SetActive, Renderer.enabled, shadowCastingMode, коллайдеры, DoorController,
// Light, PrimumLightPool, сеть, физику, VFX, корабли, префабы и сцену. Только Play Mode: вне его
// ничего не собирает и не пишет. OnDisable возвращает forceRenderingOff = false всем, кого гасил.
// Хостер — тот же скрытый DontDestroyOnLoad-объект ViewDistanceRuntime.
//
// Stage 7 (H7) — HLOD города: L0 (0–3 км) полный город, L1 (3–12 км) один запечённый меш на блок
// (= чанк PRIMUM/<район>), L2 (12+ км) один camera-facing квад-импостор на город. Данные запечены в
// редакторе (PrimumHlodBaker → Resources/HLOD/PRIMUM_HLOD); здесь только размещение и переключение.
// Владелец переключения — тот же директор и то же единственное свойство forceRenderingOff:
//   L0-участник гасится, если его чанк не на L0 (или по под-пиксельной политике — что раньше);
//   L1-рендерер блока виден только на L1; импостор — только на L2. Дистанция — до AABB блока/города
//   от ближайшей камеры union, гистерезис обратного перехода. Корень PRIMUM_HLOD создаётся под
//   WorldRoot_0_0 (HideFlags.DontSave: в сцену не сохраняется), поэтому ребаз переносит его вместе с
//   миром; состояние — только скаляры (уровень блока, уровень города). Тени у HLOD выключены по
//   построению (бюджеты теней их не собирают: CityShadowBudget пропускает Off). Глобальный скаляр
//   _PrimumHlodNight (0..1, по солнцу DayNightController) включает окна/огни ночью.
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using ProjectC.Ship;
using ProjectC.World.FloatingOrigin.Network;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace ProjectC.World
{
    [DisallowMultipleComponent]
    public class VisibilityDirector : MonoBehaviour
    {
        /// <summary>Класс группы: определяет владельца и то, может ли директор её направлять.</summary>
        public enum GroupClass { World, Chunk, House, Doors, Ruins, Farm, Satellite, Ship, Interior, Lamps, Misc }

        /// <summary>Кто пишет свойство видимости группы.</summary>
        public enum Owner { None, Director, CityShadowBudget, DoorVisibilityBudget, ShadowBudget, ShipShadowBudget, DetailDistanceCuller, PrimumLightPool }

        public const string ProfilerMarkerName = "VisibilityDirector.Tick";

        [Header("Политика под-пиксельного culling (forceRenderingOff)")]
        [Tooltip("Выключить, чтобы директор только наблюдал (группы/union/статистика) и ничего не гасил.")]
        [SerializeField] private bool policyEnabled = true;

        [Tooltip("Габарит на экране (диаметр bounds, px), ниже которого рендерер гасится во всех камерах union.")]
        [SerializeField] private float minScreenPixels = 2f;

        [Tooltip("Гистерезис: обратно включается выше minScreenPixels · (1 + hysteresis). 10–15 %.")]
        [SerializeField] private float hysteresis = 0.12f;

        [SerializeField] private bool directCityHouses = true;
        [SerializeField] private bool directFarms = true;
        [SerializeField] private bool directSatellites = true;

        [Header("Частоты")]
        [Tooltip("Тик политики, с. 2–4 Гц.")]
        [SerializeField] private float updateInterval = 0.33f;

        [Tooltip("Пере-проверка камер union, с.")]
        [SerializeField] private float cameraRescanInterval = 1f;

        [Tooltip("Как часто пробовать собрать группы, если мира ещё нет.")]
        [SerializeField] private float rebuildInterval = 2f;

        [Header("HLOD / L1 + L2-импостор (Stage 7, H7)")]
        [Tooltip("Выключить, чтобы город всегда рисовался полным L0 (A/B Stage 7).")]
        [SerializeField] private bool hlodEnabled = true;

        [Tooltip("L0 → L1 дальше этой дистанции до AABB блока, м. 0 — из манифеста (3 000).")]
        [SerializeField] private float hlodL1Distance = 0f;

        [Tooltip("L1 → L2 дальше этой дистанции до AABB города, м. 0 — из манифеста (12 000).")]
        [SerializeField] private float hlodL2Distance = 0f;

        [Tooltip("Гистерезис обратного перехода (доля). < 0 — из манифеста (0.1).")]
        [SerializeField] private float hlodHysteresis = -1f;

        [Header("Камеры и Floating Origin")]
        [Tooltip("Слой-маркер мира: камера в union, только если рисует этот слой.")]
        [SerializeField] private int worldCameraLayer = DoorVisibilityBudget.DoorLayer;

        [Tooltip("Скачок камеры за кадр больше этого (м) = ребаз/откат/телепорт: немедленный пересчёт.")]
        [SerializeField] private float teleportThreshold = 50f;

        [SerializeField] private bool logOnBuild = true;

        // ВЫКЛЮЧАТЕЛЬ АВТО-ЗАПУСКА: false — директор не создаётся сам (поведение до Stage 6).
        private const bool AutoBootstrap = true;

        private const string WorldRootName = "WorldRoot_0_0";

        /// <summary>Runtime-корень HLOD под WorldRoot_0_0 (HideFlags.DontSave, в сцену не пишется).</summary>
        public const string HlodRootName = "PRIMUM_HLOD";

        /// <summary>Глобальный скаляр ночи для шейдеров HLOD/импостора.</summary>
        public const string NightGlobalName = "_PrimumHlodNight";
        private static readonly int NightGlobalId = Shader.PropertyToID(NightGlobalName);
        private const string CityRootName = "PRIMUM";
        private const string FarmsRootName = "Primum_farms";
        private const string RuinsRootName = "RuinsValleys";
        private static readonly string[] SatelliteRootNames = { "Secund", "Tertius", "Quartus" };

        public sealed class Group
        {
            public string name;
            public GroupClass cls;
            public int parent = -1;
            public Owner visibilityOwner;
            public Owner shadowOwner;
            public bool directed;
            public int rendererCount;
            public int memberStart;
            public int memberCount;
            public int culled;
        }

        private struct Member
        {
            public Renderer renderer;
            public int group;
            public int chunk;        // группа-чанк (родитель группы-дома): уровень HLOD берётся по нему
            public Vector3 offset;   // центр bounds относительно WorldRoot_0_0 (FO-инвариантно)
            public float diameter;
            public bool culled;
        }

        // HLOD-блок: L1-рендерер чанка (Stage 7). offset/extents — AABB L1-меша в пространстве WorldRoot_0_0.
        private struct HlodBlock
        {
            public int chunk;
            public Renderer renderer;
            public Vector3 offset;
            public Vector3 extents;
            public int level;
        }

        private static VisibilityDirector _instance;
        private static readonly ProfilerMarker TickMarker = new ProfilerMarker(ProfilerMarkerName);

        private readonly List<Group> _groups = new List<Group>();
        private readonly List<Member> _members = new List<Member>();
        private readonly List<int> _anchors = new List<int>();
        private readonly List<Camera> _cameras = new List<Camera>();
        private readonly List<Vector3> _lastCameraPositions = new List<Vector3>();
        private readonly StringBuilder _cameraReport = new StringBuilder();
        private readonly Stopwatch _watch = new Stopwatch();

        private GlobalMotionControlledRebaseSlice _rebaseSlice;
        private Transform _worldRoot;
        private Vector3 _lastWorldRootPosition;
        private ulong _lastFrameGeneration;
        private float _cameraTimer;
        private float _updateTimer;
        private float _rebuildTimer;
        private bool _needRebuild;
        private bool _forceTick;

        private int _culledTotal;
        private int _ticks;
        private double _tickMsSum;

        // Stage 7: HLOD
        private readonly Dictionary<string, int> _chunkByName = new Dictionary<string, int>();
        private readonly List<HlodBlock> _hlod = new List<HlodBlock>();
        private int[] _chunkLevel = new int[0];
        private PrimumHlodManifest _manifest;
        private GameObject _hlodRoot;
        private Renderer _impostor;
        private Vector3 _cityOffset;
        private Vector3 _cityExtents;
        private int _cityLevel;
        private int _hlodHidden;
        private readonly int[] _levelCounts = new int[3];

        // Двери блока (слой 10): GPU Resident Drawer игнорирует Camera.layerCullDistances (InstanceCuller смотрит
        // только cullingLayerMask), поэтому 150 м DoorVisibilityBudget для GRD-дверей не срабатывает и около
        // 25 тыс. инстансов (~137 DC) рисуются на любой дистанции. Когда блок на L1/L2, его двери — часть
        // L0-представления и гасятся вместе с ним (только forceRenderingOff; DoorController, коллайдеры,
        // shadowCastingMode и layerCullDistances не трогаются). На L0 (< 3 км) двери не меняются.
        private struct HlodDoor
        {
            public Renderer renderer;
            public int block;
            public bool hidden;
        }
        private readonly List<HlodDoor> _hlodDoors = new List<HlodDoor>();
        private int _hlodDoorsHidden;
        private const string CityDoorsRootName = "PRIMUM_DOORS";

        // ------------------------------------------------------------- публичный API

        /// <summary>Живой директор (null, если не запущен).</summary>
        public static VisibilityDirector Instance { get { return _instance; } }

        /// <summary>Камеры мультикамерного union (только чтение).</summary>
        public IReadOnlyList<Camera> WorldCameras { get { return _cameras; } }

        public IReadOnlyList<Group> Groups { get { return _groups; } }
        public int GroupCount { get { return _groups.Count; } }
        public int DirectedMemberCount { get { return _members.Count; } }
        public int CulledCount { get { return _culledTotal; } }
        public int CameraCount { get { return _cameras.Count; } }
        public bool PolicyEnabled { get { return policyEnabled; } }
        public float MinScreenPixels { get { return minScreenPixels; } }
        public float LastTickMs { get; private set; }
        public float MaxTickMs { get; private set; }
        public float AvgTickMs { get { return _ticks > 0 ? (float)(_tickMsSum / _ticks) : 0f; } }
        public int TickCount { get { return _ticks; } }
        public int RebaseResets { get; private set; }
        public int RollbackResets { get; private set; }
        public int TeleportResets { get; private set; }
        public string CameraReport { get { return _cameraReport.ToString(); } }
        public string ProviderReport { get; private set; }
        public string OwnershipConflicts { get; private set; }

        // ---- Stage 7: HLOD ----

        /// <summary>HLOD включён (A/B: false — город всегда полный L0, L1/L2 скрыты).</summary>
        public bool HlodEnabled
        {
            get { return hlodEnabled; }
            set
            {
                if (hlodEnabled == value) return;
                hlodEnabled = value;
                _forceTick = true;
            }
        }

        public int HlodBlockCount { get { return _hlod.Count; } }
        public bool HlodImpostorPresent { get { return _impostor != null; } }
        /// <summary>Уровень города: 0/1 — по блокам, 2 — импостор.</summary>
        public int CityLevel { get { return _cityLevel; } }
        /// <summary>L0-участников, погашенных потому, что их чанк на L1/L2.</summary>
        public int HlodHiddenCount { get { return _hlodHidden; } }
        /// <summary>Дверных рендереров, привязанных к HLOD-блокам / сейчас погашенных (блок на L1/L2).</summary>
        public int HlodDoorCount { get { return _hlodDoors.Count; } }
        public int HlodDoorsHidden { get { return _hlodDoorsHidden; } }
        public float NightFactor { get; private set; }
        public string HlodReport { get; private set; }
        public Transform HlodRoot { get { return _hlodRoot != null ? _hlodRoot.transform : null; } }
        public float L1Distance { get { return hlodL1Distance > 0f ? hlodL1Distance : (_manifest != null ? _manifest.l1Distance : 3000f); } }
        public float L2Distance { get { return hlodL2Distance > 0f ? hlodL2Distance : (_manifest != null ? _manifest.l2Distance : 12000f); } }
        public float LodHysteresis { get { return hlodHysteresis >= 0f ? hlodHysteresis : (_manifest != null ? _manifest.hysteresis : 0.1f); } }

        /// <summary>Сколько блоков на L0/L1/L2 после последнего тика.</summary>
        public string HlodLevelCounts
        {
            get { return "L0=" + _levelCounts[0] + ",L1=" + _levelCounts[1] + ",L2=" + _levelCounts[2]; }
        }

        /// <summary>Ближайшая дистанция от точки до камеры union; false, если камер нет.</summary>
        public bool TryGetNearestCameraDistance(Vector3 worldPoint, out float distance)
        {
            float best = float.MaxValue;
            for (int i = 0; i < _cameras.Count; i++)
            {
                var cam = _cameras[i];
                if (cam == null) continue;
                float d = (worldPoint - cam.transform.position).sqrMagnitude;
                if (d < best) best = d;
            }
            distance = best == float.MaxValue ? -1f : Mathf.Sqrt(best);
            return best != float.MaxValue;
        }

        /// <summary>Сколько групп каждого класса и сколько из них направляет директор.</summary>
        public string DescribeGroups()
        {
            var counts = new int[(int)GroupClass.Misc + 1];
            var renderers = new int[counts.Length];
            var directed = new int[counts.Length];
            for (int i = 0; i < _groups.Count; i++)
            {
                var g = _groups[i];
                counts[(int)g.cls]++;
                renderers[(int)g.cls] += g.rendererCount;
                if (g.directed) directed[(int)g.cls]++;
            }
            var sb = new StringBuilder();
            for (int c = 0; c < counts.Length; c++)
            {
                if (counts[c] == 0 && c != (int)GroupClass.Interior) continue;
                if (sb.Length > 0) sb.Append('|');
                sb.Append((GroupClass)c).Append('=').Append(counts[c]).Append("g/").Append(renderers[c])
                  .Append("r/dir").Append(directed[c]);
            }
            return sb.ToString();
        }

        // --------------------------------------------------------------- жизненный цикл

        private void OnEnable()
        {
            _instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
            // Только Play Mode: вне его директор ничего не собирает и не пишет.
            if (!Application.isPlaying) return;
            Build();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            RestoreAll();
            if (_instance == this) _instance = null;
        }

        private void OnDestroy()
        {
            DestroyHlodRoot();
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            _needRebuild = true;
            _cameraTimer = cameraRescanInterval;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            _rebuildTimer += Time.unscaledDeltaTime;
            if ((_needRebuild || _groups.Count == 0) && _rebuildTimer >= rebuildInterval)
                Build();

            DetectFrameShift();

            _cameraTimer += Time.unscaledDeltaTime;
            if (_cameraTimer >= cameraRescanInterval || _forceTick)
            {
                _cameraTimer = 0f;
                RefreshCameras();
            }

            if (_members.Count == 0 || _cameras.Count == 0) return;

            _updateTimer += Time.unscaledDeltaTime;
            if (_updateTimer < updateInterval && !_forceTick) return;
            _updateTimer = 0f;
            _forceTick = false;
            Tick();
        }

        // ------------------------------------------------------------------- сборка

        private void Build()
        {
            _needRebuild = false;
            _rebuildTimer = 0f;
            RestoreAll();
            _groups.Clear();
            _members.Clear();
            _anchors.Clear();

            Transform worldRoot = FindWorldRoot();
            _worldRoot = worldRoot;
            if (worldRoot == null)
            {
                _needRebuild = true;
                return;
            }
            _lastWorldRootPosition = worldRoot.position;

            int world = AddGroup(WorldRootName, GroupClass.World, -1, Owner.None, Owner.None, false);

            for (int i = 0; i < worldRoot.childCount; i++)
            {
                Transform child = worldRoot.GetChild(i);
                string n = child.name;

                if (n == HlodRootName) continue; // свой runtime-корень HLOD — не группа мира
                if (n == CityRootName) BuildCity(child, world);
                else if (n == FarmsRootName) BuildChildrenAs(child, world, GroupClass.Farm, Owner.Director, Owner.ShadowBudget, directFarms);
                else if (n == RuinsRootName) AddObservedGroup(child, world, GroupClass.Ruins, Owner.DetailDistanceCuller, Owner.CityShadowBudget);
                else if (IsSatellite(n)) BuildSatellite(child, world);
                else if (n.EndsWith("_DOORS")) AddObservedGroup(child, world, GroupClass.Doors, Owner.DoorVisibilityBudget, Owner.DoorVisibilityBudget);
                else if (n.EndsWith("_LIGHTS")) AddLampGroup(child, world);
                else AddObservedGroup(child, world, GroupClass.Misc, Owner.None, Owner.CityShadowBudget);
            }

            BuildShips();

            _chunkLevel = new int[_groups.Count];
            BuildHlod(worldRoot);

            _rebaseSlice = FindAnyObjectByType<GlobalMotionControlledRebaseSlice>();
            _lastFrameGeneration = _rebaseSlice != null ? _rebaseSlice.FrameGeneration : 0ul;

            ProviderReport = DescribeProviders();
            OwnershipConflicts = DescribeOwnershipConflicts();
            RefreshCameras();

            if (logOnBuild)
                Debug.Log("[VisibilityDirector] Групп: " + _groups.Count + " (" + DescribeGroups() + "), направляемых рендереров: " +
                          _members.Count + ", порог " + minScreenPixels.ToString("F1") + " px, гистерезис " +
                          (hysteresis * 100f).ToString("F0") + " %, камер в union: " + _cameras.Count + " [" + CameraReport +
                          "], провайдеры: " + ProviderReport + ", пересечения владения: " + OwnershipConflicts +
                          ", HLOD: " + HlodReport + ".");
        }

        private int AddGroup(string name, GroupClass cls, int parent, Owner vis, Owner shadow, bool directed)
        {
            _groups.Add(new Group
            {
                name = name, cls = cls, parent = parent, visibilityOwner = vis, shadowOwner = shadow,
                directed = directed, memberStart = _members.Count
            });
            return _groups.Count - 1;
        }

        /// <summary>Город: чанк = район (PRIMUM/&lt;район&gt;), ячейка = дом (ребёнок района).</summary>
        private void BuildCity(Transform city, int world)
        {
            _chunkByName.Clear();
            for (int c = 0; c < city.childCount; c++)
            {
                Transform district = city.GetChild(c);
                int chunk = AddGroup(district.name, GroupClass.Chunk, world, Owner.Director, Owner.CityShadowBudget, false);
                if (!_chunkByName.ContainsKey(district.name)) _chunkByName.Add(district.name, chunk);
                int anchorBefore = _members.Count;

                if (district.childCount == 0)
                {
                    AddDirectedGroup(district, chunk, GroupClass.House, Owner.CityShadowBudget, directCityHouses);
                }
                else
                {
                    for (int h = 0; h < district.childCount; h++)
                        AddDirectedGroup(district.GetChild(h), chunk, GroupClass.House, Owner.CityShadowBudget, directCityHouses);
                }

                _groups[chunk].rendererCount = CountRenderers(district);
                if (_members.Count > anchorBefore) _anchors.Add(anchorBefore);
            }
        }

        private void BuildSatellite(Transform root, int world)
        {
            int chunk = AddGroup(root.name, GroupClass.Chunk, world, Owner.Director, Owner.CityShadowBudget, false);
            int anchorBefore = _members.Count;
            AddDirectedGroup(root, chunk, GroupClass.Satellite, Owner.CityShadowBudget, directSatellites);
            _groups[chunk].rendererCount = CountRenderers(root);
            if (_members.Count > anchorBefore) _anchors.Add(anchorBefore);
        }

        private void BuildChildrenAs(Transform root, int world, GroupClass cls, Owner vis, Owner shadow, bool directed)
        {
            int chunk = AddGroup(root.name, GroupClass.Chunk, world, vis, shadow, false);
            int anchorBefore = _members.Count;
            for (int i = 0; i < root.childCount; i++)
                AddDirectedGroup(root.GetChild(i), chunk, cls, shadow, directed);
            _groups[chunk].rendererCount = CountRenderers(root);
            if (_members.Count > anchorBefore) _anchors.Add(anchorBefore);
        }

        /// <summary>Группа, которую директор может направлять: собирает её рендереры-участники.</summary>
        private void AddDirectedGroup(Transform root, int parent, GroupClass cls, Owner shadow, bool directed)
        {
            // includeInactive: рендереры, которые стриминг включит позже, тоже попадают в политику.
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            int g = AddGroup(root.name, cls, parent, directed ? Owner.Director : Owner.None, shadow, directed);
            var group = _groups[g];
            group.rendererCount = renderers.Length;
            group.memberStart = _members.Count;

            if (directed)
            {
                Vector3 origin = _worldRoot != null ? _worldRoot.position : Vector3.zero;
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (!IsDirectable(r)) continue;
                    var b = r.bounds;
                    _members.Add(new Member
                    {
                        renderer = r,
                        group = g,
                        chunk = parent,
                        offset = b.center - origin,
                        diameter = b.extents.magnitude * 2f,
                        culled = false
                    });
                }
            }
            group.memberCount = _members.Count - group.memberStart;
        }

        /// <summary>
        /// Участник политики: только MeshRenderer статической геометрии. Слой дверей (10), всё
        /// под DoorController, частицы/линии/скиннинг и уже погашенные кем-то рендереры не берутся.
        /// </summary>
        private static bool IsDirectable(Renderer r)
        {
            if (r == null) return false;
            if (!(r is MeshRenderer)) return false;
            if (r.gameObject.layer == DoorVisibilityBudget.DoorLayer) return false;
            if (r.forceRenderingOff) return false;
            if (r.GetComponentInParent<DoorController>(true) != null) return false;
            return true;
        }

        private void AddObservedGroup(Transform root, int parent, GroupClass cls, Owner vis, Owner shadow)
        {
            int n = CountRenderers(root);
            if (n == 0 && cls == GroupClass.Misc) return;
            int g = AddGroup(root.name, cls, parent, vis, shadow, false);
            _groups[g].rendererCount = n;
        }

        private void AddLampGroup(Transform root, int parent)
        {
            int g = AddGroup(root.name, GroupClass.Lamps, parent, Owner.PrimumLightPool, Owner.None, false);
            // Для ламп «рендерерами» считаем источники света: их Light.enabled ведёт PrimumLightPool.
            _groups[g].rendererCount = root.GetComponentsInChildren<Light>(true).Length;
        }

        /// <summary>Корабли и их интерьеры (Int_*): только наблюдение — корабли в Stage 6 не трогаются.</summary>
        private void BuildShips()
        {
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                var roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    var go = roots[i];
                    if (go == null || go.name == WorldRootName) continue;
                    if (go.GetComponentInChildren<ShipRootReference>(true) == null) continue;

                    var rs = go.GetComponentsInChildren<Renderer>(true);
                    bool heavy = go.GetComponentInChildren<ShipShadowBudget>(true) != null;
                    int g = AddGroup(go.name, GroupClass.Ship, 0, Owner.None, heavy ? Owner.ShipShadowBudget : Owner.None, false);
                    _groups[g].rendererCount = rs.Length;

                    int interior = 0;
                    for (int r = 0; r < rs.Length; r++)
                        if (rs[r] != null && rs[r].name.StartsWith("Int_")) interior++;
                    if (interior > 0)
                    {
                        int ig = AddGroup(go.name + "/Int_*", GroupClass.Interior, g, Owner.None,
                                          heavy ? Owner.ShipShadowBudget : Owner.None, false);
                        _groups[ig].rendererCount = interior;
                    }
                }
            }
        }

        private static int CountRenderers(Transform root)
        {
            return root != null ? root.GetComponentsInChildren<Renderer>(false).Length : 0;
        }

        private static bool IsSatellite(string name)
        {
            for (int i = 0; i < SatelliteRootNames.Length; i++)
                if (SatelliteRootNames[i] == name) return true;
            return false;
        }

        private static Transform FindWorldRoot()
        {
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                var roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                    if (roots[i] != null && roots[i].name == WorldRootName) return roots[i].transform;
            }
            return null;
        }

        // -------------------------------------------------------------- провайдеры

        /// <summary>
        /// Реестр провайдеров — обнаружение, а не перепривязка: бюджеты работают как раньше, директор
        /// только фиксирует, кто из них жив. При отсутствии любого из них директор работает так же.
        /// </summary>
        private static string DescribeProviders()
        {
            var sb = new StringBuilder();
            AppendProvider<CityShadowBudget>(sb, "CityShadowBudget");
            AppendProvider<DoorVisibilityBudget>(sb, "DoorVisibilityBudget");
            AppendProvider<ShadowBudget>(sb, "ShadowBudget");
            AppendProvider<ShipShadowBudget>(sb, "ShipShadowBudget");
            AppendProvider<DetailDistanceCuller>(sb, "DetailDistanceCuller");
            AppendProvider<PrimumLightPool>(sb, "PrimumLightPool");
            return sb.ToString();
        }

        private static void AppendProvider<T>(StringBuilder sb, string label) where T : Behaviour
        {
            var all = FindObjectsByType<T>(FindObjectsSortMode.None);
            int on = 0;
            for (int i = 0; i < all.Length; i++) if (all[i] != null && all[i].isActiveAndEnabled) on++;
            if (sb.Length > 0) sb.Append('|');
            sb.Append(label).Append('=').Append(on).Append('/').Append(all.Length);
        }

        /// <summary>
        /// Пересечения владения, известные по конструкции бюджетов. Директор их НЕ исправляет
        /// (правило H6: рефакторинг бюджетов — только после согласования), а только сообщает.
        /// </summary>
        private string DescribeOwnershipConflicts()
        {
            bool farms = false;
            for (int i = 0; i < _groups.Count; i++) if (_groups[i].cls == GroupClass.Farm) { farms = true; break; }
            bool city = FindAnyObjectByType<CityShadowBudget>() != null;
            bool farm = FindAnyObjectByType<ShadowBudget>() != null;
            if (farms && city && farm)
                return "Primum_farms.shadowCastingMode=CityShadowBudget+ShadowBudget(pre-existing,not_H6)";
            return "none";
        }

        // ------------------------------------------------------------------ камеры

        private void RefreshCameras()
        {
            _cameras.Clear();
            _cameraReport.Length = 0;

            int worldMask = 1 << worldCameraLayer;
            var all = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var cam = all[i];
                if (cam == null || !cam.isActiveAndEnabled) continue;
                if (cam.cameraType != CameraType.Game) continue;

                string reason = null;
                if (cam.targetTexture != null) reason = "rt";
                else if (cam.farClipPlane < 50f) reason = "far<50";
                else if ((cam.cullingMask & worldMask) == 0) reason = "mask_no_world";
                else if (!ReachesWorld(cam)) reason = "out_of_reach";

                if (_cameraReport.Length > 0) _cameraReport.Append(',');
                _cameraReport.Append(cam.name).Append(reason == null ? "+" : "-" + reason);

                if (reason == null) _cameras.Add(cam);
            }

            _lastCameraPositions.Clear();
            for (int i = 0; i < _cameras.Count; i++) _lastCameraPositions.Add(_cameras[i].transform.position);
        }

        /// <summary>Дотягивается ли камера хотя бы до одного якоря направляемых групп (позиции живые).</summary>
        private bool ReachesWorld(Camera cam)
        {
            if (_anchors.Count == 0 || _worldRoot == null) return true;
            Vector3 p = cam.transform.position;
            Vector3 origin = _worldRoot.position;
            float farSqr = cam.farClipPlane * cam.farClipPlane;
            for (int i = 0; i < _anchors.Count; i++)
            {
                int m = _anchors[i];
                if (m < 0 || m >= _members.Count) continue;
                if ((origin + _members[m].offset - p).sqrMagnitude <= farSqr) return true;
            }
            return false;
        }

        /// <summary>Сброс кэша смещений после ребаза/отката: заново от живых bounds и WorldRoot.</summary>
        private void RecomputeOffsets()
        {
            if (_worldRoot == null) return;
            Vector3 origin = _worldRoot.position;
            for (int i = 0; i < _members.Count; i++)
            {
                var m = _members[i];
                if (m.renderer == null) continue;
                m.offset = m.renderer.bounds.center - origin;
                _members[i] = m;
            }
        }

        // ------------------------------------------------------------ Floating Origin

        /// <summary>
        /// Ребаз (success) меняет frameGeneration; откат (rollback) и телепорт его не меняют, но
        /// двигают мир/камеру скачком. В обоих случаях: пересобрать union и пересчитать сразу.
        /// </summary>
        private void DetectFrameShift()
        {
            if (_rebaseSlice == null && _groups.Count > 0 && Time.frameCount % 60 == 0)
                _rebaseSlice = FindAnyObjectByType<GlobalMotionControlledRebaseSlice>();

            if (_rebaseSlice != null)
            {
                ulong gen = _rebaseSlice.FrameGeneration;
                if (gen != _lastFrameGeneration)
                {
                    _lastFrameGeneration = gen;
                    RebaseResets++;
                    RecomputeOffsets();
                    _forceTick = true;
                }
            }

            if (_worldRoot != null)
            {
                Vector3 wr = _worldRoot.position;
                if ((wr - _lastWorldRootPosition).sqrMagnitude > 0.0001f)
                {
                    // Сдвиг WorldRoot без смены frameGeneration = откат (rollback) или внешний перенос.
                    if (!_forceTick) RollbackResets++;
                    _lastWorldRootPosition = wr;
                    RecomputeOffsets();
                    _forceTick = true;
                }
            }
            else if (_groups.Count > 0)
            {
                _needRebuild = true;
            }

            float thrSqr = teleportThreshold * teleportThreshold;
            int n = Mathf.Min(_cameras.Count, _lastCameraPositions.Count);
            for (int i = 0; i < n; i++)
            {
                var cam = _cameras[i];
                if (cam == null) continue;
                Vector3 p = cam.transform.position;
                if ((p - _lastCameraPositions[i]).sqrMagnitude > thrSqr)
                {
                    TeleportResets++;
                    _forceTick = true;
                }
                _lastCameraPositions[i] = p;
            }
        }

        // -------------------------------------------------------------------- тик

        private void Tick()
        {
            using (TickMarker.Auto())
            {
                _watch.Restart();

                float pxOff = Mathf.Max(0.01f, minScreenPixels);
                float pxOn = pxOff * (1f + Mathf.Max(0f, hysteresis));
                int culledTotal = 0;
                Vector3 origin = _worldRoot != null ? _worldRoot.position : Vector3.zero;
                PrepareCameraSnapshot();

                for (int g = 0; g < _groups.Count; g++) _groups[g].culled = 0;

                UpdateNight();
                UpdateHlodLevels(origin);
                int hlodHidden = 0;

                for (int i = 0; i < _members.Count; i++)
                {
                    var m = _members[i];
                    var r = m.renderer;
                    if (r == null) { _needRebuild = true; continue; }

                    bool want = false;
                    bool hiddenByHlod = m.chunk >= 0 && m.chunk < _chunkLevel.Length && _chunkLevel[m.chunk] != 0;
                    if (hiddenByHlod)
                    {
                        want = true;
                        hlodHidden++;
                    }
                    else if (policyEnabled)
                    {
                        float px = MaxScreenPixels(origin + m.offset, m.diameter);
                        want = m.culled ? px < pxOn : px < pxOff;
                    }

                    if (want != m.culled)
                    {
                        m.culled = want;
                        r.forceRenderingOff = want;
                        _members[i] = m;
                    }

                    if (m.culled && !hiddenByHlod)
                    {
                        culledTotal++;
                        _groups[m.group].culled++;
                    }
                }

                _culledTotal = culledTotal;
                _hlodHidden = hlodHidden;
                _watch.Stop();
                float ms = (float)_watch.Elapsed.TotalMilliseconds;
                LastTickMs = ms;
                if (ms > MaxTickMs) MaxTickMs = ms;
                _ticks++;
                _tickMsSum += ms;
            }
        }

        // Снимок камер union на тик: нативные вызовы (transform/fov/pixelHeight) — один раз на камеру,
        // а не на каждого участника. Позиции живые (читаются в начале каждого тика).
        private Vector3[] _snapPos = new Vector3[4];
        private float[] _snapScale = new float[4];
        private int _snapCount;
        private bool _snapOrtho;

        private void PrepareCameraSnapshot()
        {
            if (_snapPos.Length < _cameras.Count)
            {
                _snapPos = new Vector3[_cameras.Count];
                _snapScale = new float[_cameras.Count];
            }
            _snapCount = 0;
            _snapOrtho = false;
            for (int c = 0; c < _cameras.Count; c++)
            {
                var cam = _cameras[c];
                if (cam == null || !cam.isActiveAndEnabled) continue;
                if (cam.orthographic) { _snapOrtho = true; continue; }
                float fovRad = Mathf.Max(0.001f, cam.fieldOfView * Mathf.Deg2Rad);
                _snapPos[_snapCount] = cam.transform.position;
                _snapScale[_snapCount] = cam.pixelHeight / fovRad;
                _snapCount++;
            }
        }

        /// <summary>Максимальный габарит на экране (px) по камерам union — union «видимо хоть где-то».</summary>
        private float MaxScreenPixels(Vector3 center, float diameter)
        {
            // Нет ни одной живой перспективной камеры (риг уничтожен до пере-скана) или есть
            // ортокамера — ничего не гасим: frustum/far отсекает движок.
            if (_snapCount == 0 || _snapOrtho) return float.MaxValue;
            float best = 0f;
            for (int c = 0; c < _snapCount; c++)
            {
                float d = (center - _snapPos[c]).magnitude;
                if (d <= diameter) return float.MaxValue; // камера внутри/вплотную
                float px = diameter / d * _snapScale[c];
                if (px > best) best = px;
            }
            return best;
        }

        private void RestoreAll()
        {
            for (int i = 0; i < _members.Count; i++)
            {
                var m = _members[i];
                if (!m.culled) continue;
                if (m.renderer != null) m.renderer.forceRenderingOff = false;
                m.culled = false;
                _members[i] = m;
            }
            _culledTotal = 0;
            _hlodHidden = 0;
            for (int g = 0; g < _groups.Count; g++) _groups[g].culled = 0;

            // HLOD: L1/L2 скрыты, все чанки на L0 — город снова полный.
            for (int i = 0; i < _hlod.Count; i++)
            {
                var b = _hlod[i];
                if (b.renderer != null) b.renderer.forceRenderingOff = true;
                b.level = 0;
                _hlod[i] = b;
            }
            if (_impostor != null) _impostor.forceRenderingOff = true;
            RestoreHlodDoors();
            _cityLevel = 0;
            for (int i = 0; i < _chunkLevel.Length; i++) _chunkLevel[i] = 0;
            _levelCounts[0] = _hlod.Count;
            _levelCounts[1] = 0;
            _levelCounts[2] = 0;
        }

        // --------------------------------------------------------------- Stage 7: HLOD

        /// <summary>
        /// Размещает запечённые L1-меши блоков и L2-импостор под WorldRoot_0_0 (только Play Mode).
        /// Ничего не генерирует: меши/материалы — из манифеста, собранного в редакторе.
        /// </summary>
        private void BuildHlod(Transform worldRoot)
        {
            _hlod.Clear();
            _impostor = null;
            _cityLevel = 0;
            for (int i = 0; i < _levelCounts.Length; i++) _levelCounts[i] = 0;

            if (_manifest == null) _manifest = Resources.Load<PrimumHlodManifest>(PrimumHlodManifest.ResourcePath);
            if (_manifest == null || _manifest.blocks == null || _manifest.blocks.Count == 0 || _manifest.l1Material == null)
            {
                DestroyHlodRoot();
                HlodReport = _manifest == null ? "no_manifest" : "empty_manifest";
                return;
            }

            if (_hlodRoot == null || _hlodRoot.transform.parent != worldRoot)
            {
                DestroyHlodRoot();
                _hlodRoot = new GameObject(HlodRootName) { hideFlags = HideFlags.DontSave };
                _hlodRoot.transform.SetParent(worldRoot, false);
            }
            else
            {
                for (int i = _hlodRoot.transform.childCount - 1; i >= 0; i--)
                    Destroy(_hlodRoot.transform.GetChild(i).gameObject);
            }
            var rootT = _hlodRoot.transform;
            rootT.localPosition = Vector3.zero;
            rootT.localRotation = Quaternion.identity;
            rootT.localScale = Vector3.one;

            int missing = 0;
            int triangles = 0;
            bool cityHasBounds = false;
            Bounds city = default(Bounds);
            for (int i = 0; i < _manifest.blocks.Count; i++)
            {
                var b = _manifest.blocks[i];
                int chunk;
                if (b == null || b.mesh == null || !_chunkByName.TryGetValue(b.name, out chunk)) { missing++; continue; }

                var r = CreateHlodRenderer(rootT, b.name + "_L1", b.mesh, _manifest.l1Material, b.pivot, b.layer);
                _hlod.Add(new HlodBlock { chunk = chunk, renderer = r, offset = b.pivot, extents = b.size * 0.5f, level = 0 });
                triangles += b.triangles;

                var bb = new Bounds(b.pivot, b.size);
                if (!cityHasBounds) { city = bb; cityHasBounds = true; }
                else city.Encapsulate(bb);
            }

            if (_manifest.impostorValid && _manifest.impostorMesh != null && _manifest.impostorMaterial != null && _hlod.Count > 0)
            {
                int layer = _hlod[0].renderer != null ? _hlod[0].renderer.gameObject.layer : 0;
                _impostor = CreateHlodRenderer(rootT, "PRIMUM_L2_Impostor", _manifest.impostorMesh, _manifest.impostorMaterial,
                                               _manifest.cityCenter, layer);
                _cityOffset = _manifest.cityCenter;
                _cityExtents = _manifest.citySize * 0.5f;
            }
            else
            {
                _cityOffset = city.center;
                _cityExtents = city.extents;
            }

            _levelCounts[0] = _hlod.Count;
            CollectHlodDoors(worldRoot);
            HlodReport = "blocks=" + _hlod.Count + "/" + _manifest.blocks.Count + ";missing=" + missing + ";l1Tris=" + triangles +
                         ";doors=" + _hlodDoors.Count +
                         ";impostor=" + (_impostor != null ? "yes" : "no") + ";l1=" + L1Distance.ToString("F0") + ";l2=" +
                         L2Distance.ToString("F0") + ";hyst=" + LodHysteresis.ToString("F2") + ";bakeHash=" +
                         (string.IsNullOrEmpty(_manifest.bakeHash) ? "-" : _manifest.bakeHash.Substring(0, Mathf.Min(8, _manifest.bakeHash.Length)));
        }

        /// <summary>
        /// Двери блоков: PRIMUM_DOORS/&lt;блок&gt; (имена совпадают с чанками PRIMUM) и двери внутри PRIMUM
        /// (ADMIN_DOOR_PREFABS, ADMIN_ENTRANCE_PORTAL → блок ADMIN). Без блока — не трогаются.
        /// </summary>
        private void CollectHlodDoors(Transform worldRoot)
        {
            RestoreHlodDoors();
            _hlodDoors.Clear();
            if (_hlod.Count == 0 || worldRoot == null) return;

            int admin = -1;
            var byName = new Dictionary<string, int>();
            for (int i = 0; i < _hlod.Count; i++)
            {
                string n = _groups[_hlod[i].chunk].name;
                if (!byName.ContainsKey(n)) byName.Add(n, i);
                if (admin < 0 && n.Contains("_ADMIN_")) admin = i;
            }

            var doorsRoot = worldRoot.Find(CityDoorsRootName);
            if (doorsRoot != null)
            {
                for (int c = 0; c < doorsRoot.childCount; c++)
                {
                    var child = doorsRoot.GetChild(c);
                    int block;
                    if (byName.TryGetValue(child.name, out block)) AddHlodDoors(child, block, false);
                }
            }

            var city = worldRoot.Find(CityRootName);
            if (city != null)
            {
                for (int c = 0; c < city.childCount; c++)
                {
                    var child = city.GetChild(c);
                    int block;
                    if (!byName.TryGetValue(child.name, out block))
                    {
                        // чанк без своего L1 (двери/портал ADMIN) — к блоку ADMIN
                        if (admin >= 0 && child.name.StartsWith("ADMIN_")) AddHlodDoors(child, admin, true);
                        continue;
                    }
                    AddHlodDoors(child, block, true); // двери внутри самого блока (не участники L0)
                }
            }
        }

        private void AddHlodDoors(Transform root, int block, bool doorsOnly)
        {
            var rs = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                var r = rs[i];
                if (r == null || r.forceRenderingOff) continue;
                if (doorsOnly && r.gameObject.layer != DoorVisibilityBudget.DoorLayer &&
                    r.GetComponentInParent<DoorController>(true) == null) continue;
                _hlodDoors.Add(new HlodDoor { renderer = r, block = block, hidden = false });
            }
        }

        private void RestoreHlodDoors()
        {
            for (int i = 0; i < _hlodDoors.Count; i++)
            {
                var d = _hlodDoors[i];
                if (!d.hidden) continue;
                if (d.renderer != null) d.renderer.forceRenderingOff = false;
                d.hidden = false;
                _hlodDoors[i] = d;
            }
            _hlodDoorsHidden = 0;
        }

        private void UpdateHlodDoors()
        {
            int hidden = 0;
            for (int i = 0; i < _hlodDoors.Count; i++)
            {
                var d = _hlodDoors[i];
                bool want = d.block >= 0 && d.block < _hlod.Count && _hlod[d.block].level != 0;
                if (want != d.hidden)
                {
                    if (d.renderer != null) d.renderer.forceRenderingOff = want;
                    d.hidden = want;
                    _hlodDoors[i] = d;
                }
                if (d.hidden) hidden++;
            }
            _hlodDoorsHidden = hidden;
        }

        private static Renderer CreateHlodRenderer(Transform parent, string name, Mesh mesh, Material material, Vector3 localPosition, int layer)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave, layer = layer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            mr.allowOcclusionWhenDynamic = true;
            mr.forceRenderingOff = true; // до первого тика — скрыт, город на L0
            return mr;
        }

        private void DestroyHlodRoot()
        {
            RestoreHlodDoors();
            if (_hlodRoot == null) return;
            if (Application.isPlaying) Destroy(_hlodRoot);
            else DestroyImmediate(_hlodRoot);
            _hlodRoot = null;
            _impostor = null;
            _hlod.Clear();
        }

        /// <summary>
        /// Уровни L0/L1/L2 по ближайшей камере union (снимок тика): город на L2 дальше l2 от AABB города,
        /// иначе блок на L1 дальше l1 от своего AABB. Обратный переход — с гистерезисом.
        /// Ортокамера или нет камер — всё на L0 (как и под-пиксельная политика).
        /// </summary>
        private void UpdateHlodLevels(Vector3 origin)
        {
            _levelCounts[0] = 0;
            _levelCounts[1] = 0;
            _levelCounts[2] = 0;
            if (_hlod.Count == 0) return;

            bool active = hlodEnabled && _snapCount > 0 && !_snapOrtho;
            float l1 = L1Distance, l2 = L2Distance, back = 1f - Mathf.Clamp(LodHysteresis, 0f, 0.5f);

            int city = 0;
            if (active && _impostor != null)
            {
                float d = NearestDistanceToBox(origin + _cityOffset, _cityExtents);
                city = _cityLevel == 2 ? (d > l2 * back ? 2 : 0) : (d > l2 ? 2 : 0);
            }
            if (city != _cityLevel || (_impostor != null && _impostor.forceRenderingOff == (city == 2)))
            {
                _cityLevel = city;
                if (_impostor != null) _impostor.forceRenderingOff = city != 2;
            }

            for (int i = 0; i < _hlod.Count; i++)
            {
                var b = _hlod[i];
                int level = 0;
                if (active)
                {
                    if (city == 2) level = 2;
                    else
                    {
                        float d = NearestDistanceToBox(origin + b.offset, b.extents);
                        level = b.level >= 1 ? (d > l1 * back ? 1 : 0) : (d > l1 ? 1 : 0);
                    }
                }

                if (level != b.level)
                {
                    b.level = level;
                    _hlod[i] = b;
                }
                if (b.renderer != null && b.renderer.forceRenderingOff != (level != 1))
                    b.renderer.forceRenderingOff = level != 1;
                if (b.chunk >= 0 && b.chunk < _chunkLevel.Length) _chunkLevel[b.chunk] = level;
                _levelCounts[level]++;
            }

            UpdateHlodDoors();
        }

        /// <summary>Ближайшая дистанция от камер снимка до AABB (центр, полугабарит).</summary>
        private float NearestDistanceToBox(Vector3 center, Vector3 extents)
        {
            float best = float.MaxValue;
            for (int c = 0; c < _snapCount; c++)
            {
                Vector3 d = _snapPos[c] - center;
                float dx = Mathf.Max(Mathf.Abs(d.x) - extents.x, 0f);
                float dy = Mathf.Max(Mathf.Abs(d.y) - extents.y, 0f);
                float dz = Mathf.Max(Mathf.Abs(d.z) - extents.z, 0f);
                float s = dx * dx + dy * dy + dz * dz;
                if (s < best) best = s;
            }
            return best == float.MaxValue ? 0f : Mathf.Sqrt(best);
        }

        /// <summary>Ночь 0..1 для окон/огней HLOD: солнце выключено или ниже горизонта = ночь.</summary>
        private void UpdateNight()
        {
            Light sun = null;
            var dnc = ProjectC.Core.DayNightController.Instance;
            if (dnc != null) sun = dnc.sunLight;
            if (sun == null) sun = RenderSettings.sun;

            float night;
            if (sun == null) night = 0f;
            else if (!sun.isActiveAndEnabled || sun.intensity <= 0.001f) night = 1f;
            else
            {
                float up = -sun.transform.forward.y;
                night = Mathf.Clamp01((0.08f - up) / 0.16f);
            }
            NightFactor = night;
            Shader.SetGlobalFloat(NightGlobalId, night);
        }

        // ---------------------------------------------------------------- хостинг

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!AutoBootstrap) return;
            if (FindAnyObjectByType<VisibilityDirector>() != null) return;

            var host = GameObject.Find(DoorVisibilityBudget.HostName);
            if (host == null)
            {
                host = new GameObject(DoorVisibilityBudget.HostName);
                DontDestroyOnLoad(host);
            }
            if (host.GetComponent<VisibilityDirector>() == null)
                host.AddComponent<VisibilityDirector>();
        }

        private void OnValidate()
        {
            if (minScreenPixels < 0.1f) minScreenPixels = 0.1f;
            hysteresis = Mathf.Clamp(hysteresis, 0f, 0.5f);
            if (updateInterval < 0.1f) updateInterval = 0.1f;
            if (cameraRescanInterval < 0.1f) cameraRescanInterval = 0.1f;
            if (rebuildInterval < 0.1f) rebuildInterval = 0.1f;
            if (teleportThreshold < 1f) teleportThreshold = 1f;
        }
    }
}
