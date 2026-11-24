using System.Collections.Generic;
using UnityEngine;
using ProjectC.Items;
using ProjectC.AI;  // T-NPC-03: NpcLootPickup
using ProjectC.Player;
using ProjectC.Ship;
using ProjectC.Ship.Cargo;  // T-CARGO-UI-02: ShipCargoConsole

namespace ProjectC.Core
{
    /// <summary>
    /// Static manager for tracking IInteractable objects in the scene.
    /// Replaces FindObjectsByType calls with trigger-based registration.
    /// Zero allocations in hot paths.
    /// T-Q19: NPC detection (v1) removed — v2 NpcController handles NPC pickup via
    /// NetworkPlayer.RequestTalkToNpc.
    /// </summary>
    public static class InteractableManager
    {
        // Pre-allocated lists to avoid GC in hot paths
        private static readonly List<PickupItem> _pickups = new List<PickupItem>(32);
        private static readonly List<ChestContainer> _chests = new List<ChestContainer>(16);
        private static readonly List<ShipController> _ships = new List<ShipController>(8);
        private static readonly List<ProjectC.ResourceNode.ResourceNode> _resourceNodes = new List<ProjectC.ResourceNode.ResourceNode>(16);
        private static readonly List<ProjectC.Crafting.CraftingStation> _craftingStations = new List<ProjectC.Crafting.CraftingStation>(8);
        private static readonly List<ProjectC.Ship.Cargo.ShipCargoConsole> _shipCargoConsoles = new List<ProjectC.Ship.Cargo.ShipCargoConsole>(8);
        private static readonly List<ProjectC.Ship.RepairManager> _repairManagers = new List<ProjectC.Ship.RepairManager>(4);

        // T-PERF06: реестр дверей. До правки NetworkPlayer дважды за кадр делал
        // FindObjectsByType<DoorController> (2 800 дверей в мире = массив 21.94 KB) —
        // в замере G это 5 000 KB за сессию (18.9 KB на вызов Update).
        // Регистрация — из DoorController.OnEnable/OnDisable, т.е. ровно "active and enabled".
        private static readonly List<DoorController> _doors = new List<DoorController>(4096);

        // T-PERF20: повторный отрицательный запрос в почти той же позиции не обходит
        // тысячи дверей. Регистрация/снятие двери немедленно инвалидирует кэш.
        private static int _doorsVersion;
        private static int _emptyDoorQueryVersion = -1;
        private static Vector3 _emptyDoorQueryPosition;
        private static float _emptyDoorQueryRange;
        private const float EmptyDoorQueryReuseRadius = 0.25f;

        // T-PERF21: отдельный bool-кэш только для observation-подсказки.
        // Фактический FindNearestDoor при нажатии продолжает выбирать ближайшую дверь.
        private static int _hintDoorVersion = -1;
        private static Vector3 _hintDoorPosition;
        private static float _hintDoorRange;
        private static DoorController _hintDoor;

        // T-PERF24: пространственный индекс дверей ТОЛЬКО для observation-подсказки.
        // Замер 24.11 19-11-11: NetworkPlayer.InteractionHint = 0.448 мс/кадр — самый
        // дорогой project-side участок. Запрос идёт на 5 Гц, а игрок за 0.2 с успевает
        // сдвинуться больше 0.25 м, поэтому кэш промахивался и каждый запрос заново
        // обходил все ~2 800 дверей (transform.position + InteractionBoundingRadius).
        // Сетка XZ по 24 м сводит запрос к клеткам вокруг игрока (единицы дверей).
        // Фактический FindNearestDoor при нажатии по-прежнему полный и точный.
        private const float DoorGridCellSize = 24f;
        // Пересборка сетки: смена состава реестра (version) или мировой сдвиг (rebase).
        // Мировой сдвиг детектируется по одной «пробирной» двери — O(1) на запрос.
        private const float DoorGridResyncRadius = 0.5f;
        private static readonly Dictionary<long, List<DoorController>> _doorGrid =
            new Dictionary<long, List<DoorController>>(256);
        // Двери, которые нельзя держать в статической сетке: на движущихся кораблях
        // и с не-боксовым коллайдером (InteractionBoundingRadius = +inf). Их немного.
        private static readonly List<DoorController> _doorGridDynamic = new List<DoorController>(64);
        private static int _doorGridVersion = -1;
        private static bool _doorGridBuilt;
        private static float _doorGridMaxRadius;
        private static DoorController _doorGridProbe;
        private static Vector3 _doorGridProbePosition;

        // T-PERF06: кэш коллайдера посадки на корабль. GetComponentInChildren<T>(true) аллоцирует
        // массив на каждый вызов, а FindNearestShip зовётся из подсказки взаимодействия каждый кадр
        // и обходит все живые корабли (20 штук).
        private struct ShipBoardingTarget
        {
            public PilotSeatController Seat;
            public Collider Collider;
            public int ResolvedFrame;
        }

        private static readonly Dictionary<ShipController, ShipBoardingTarget> _shipBoardingTargets =
            new Dictionary<ShipController, ShipBoardingTarget>(8);

        // Повторная попытка резолва для корабля, у которого кресла/коллайдера пока нет
        // (префаб догружается). Уже разрезолвленный коллайдер держится без ограничений.
        private const int BoardingTargetRetryFrames = 60;

        /// <summary>
                /// Register a pickup item when it enters player's trigger.
                /// </summary>
                public static void RegisterPickup(PickupItem pickup)
                {
                    if (pickup != null && !_pickups.Contains(pickup))
                    {
                        _pickups.Add(pickup);
                    }
                }

                // T-NPC-03: NpcLootPickup uses IInteractable (not PickupItem). Separate pool.
                private static readonly List<NpcLootPickup> _npcLootPickups = new List<NpcLootPickup>(16);

                /// <summary>
                /// T-NPC-03: Register NPC loot pickup (uses IInteractable; not in standard pickup pool).
                /// </summary>
                public static void RegisterNpcLoot(NpcLootPickup pickup)
                {
                    if (pickup != null && !_npcLootPickups.Contains(pickup))
                    {
                        _npcLootPickups.Add(pickup);
                    }
                }

                /// <summary>
                /// T-NPC-03: Unregister NPC loot pickup.
                /// </summary>
                public static void UnregisterNpcLoot(NpcLootPickup pickup)
                {
                    if (pickup != null)
                    {
                        _npcLootPickups.Remove(pickup);
                    }
                }

                /// <summary>
                /// T-NPC-03: Find nearest NPC loot pickup within range.
                /// </summary>
                public static NpcLootPickup FindNearestNpcLoot(Vector3 position, float range)
                {
                    NpcLootPickup nearest = null;
                    float minDistSq = float.MaxValue;
                    for (int i = 0; i < _npcLootPickups.Count; i++)
                    {
                        var l = _npcLootPickups[i];
                        if (l == null || !l.isActiveAndEnabled || !l.IsSpawned) { _npcLootPickups.RemoveAt(i--); continue; }
                        float d = (l.transform.position - position).sqrMagnitude;
                        if (d < minDistSq && d <= range * range)
                        {
                            minDistSq = d;
                            nearest = l;
                        }
                    }
                    return nearest;
                }

        /// <summary>
        /// Unregister a pickup item when it exits player's trigger.
        /// </summary>
        public static void UnregisterPickup(PickupItem pickup)
        {
            if (pickup != null)
            {
                _pickups.Remove(pickup);
            }
        }

        /// <summary>
        /// Register a chest when it enters player's trigger.
        /// </summary>
        public static void RegisterChest(ChestContainer chest)
        {
            if (chest != null && !_chests.Contains(chest))
            {
                _chests.Add(chest);
            }
        }

        /// <summary>
        /// Unregister a chest when it exits player's trigger.
        /// </summary>
        public static void UnregisterChest(ChestContainer chest)
        {
            if (chest != null)
            {
                _chests.Remove(chest);
            }
        }

        /// <summary>
        /// Register a ship when it enters player's trigger.
        /// </summary>
        public static void RegisterShip(ShipController ship)
        {
            if (ship != null && !_ships.Contains(ship))
            {
                _ships.Add(ship);
            }
        }

        /// <summary>
        /// Unregister a ship when it exits player's trigger.
        /// </summary>
        public static void UnregisterShip(ShipController ship)
        {
            if (ship != null)
            {
                _ships.Remove(ship);
                _shipBoardingTargets.Remove(ship);
            }
        }

        // ==========================================================
        // ResourceNode (T-G02)
        // ==========================================================

        /// <summary>Register a resource node when it enters player's trigger.</summary>
        public static void RegisterResourceNode(ProjectC.ResourceNode.ResourceNode node)
        {
            if (node != null && !_resourceNodes.Contains(node))
            {
                _resourceNodes.Add(node);
            }
        }

        /// <summary>Unregister a resource node when it exits player's trigger.</summary>
        public static void UnregisterResourceNode(ProjectC.ResourceNode.ResourceNode node)
        {
            if (node != null)
            {
                _resourceNodes.Remove(node);
            }
        }

        /// <summary>Find nearest resource node within range. Zero allocations.</summary>
        public static ProjectC.ResourceNode.ResourceNode FindNearestResourceNode(Vector3 position, float range)
        {
            ProjectC.ResourceNode.ResourceNode nearest = null;
            float minDist = float.MaxValue;
            for (int i = 0; i < _resourceNodes.Count; i++)
            {
                var node = _resourceNodes[i];
                if (node == null || !node.gameObject.activeSelf) continue;
                float dist = Vector3.Distance(position, node.transform.position);
                if (dist < range && dist < minDist)
                {
                    minDist = dist;
                    nearest = node;
                }
            }
            return nearest;
        }

        // T-Q19: FindNearestNpc/RegisterNpc/UnregisterNpc removed. v1 World.Npc.NpcInteraction
        // is gone; v2 NpcController handles NPC detection in NetworkPlayer (RequestTalkToNpcRpc).

        /// <summary>
        /// Get cached list of pickups. DO NOT modify this list.
        /// </summary>
        public static List<PickupItem> GetPickups() => _pickups;

        /// <summary>
        /// Get cached list of chests. DO NOT modify this list.
        /// </summary>
        public static List<ChestContainer> GetChests() => _chests;

        /// <summary>
        /// Get cached list of ships. DO NOT modify this list.
        /// </summary>
        public static List<ShipController> GetShips() => _ships;

        // ==========================================================
        // CraftingStation (T-C04)
        // ==========================================================

        public static void RegisterCraftingStation(ProjectC.Crafting.CraftingStation station)
        {
            if (station != null && !_craftingStations.Contains(station)) _craftingStations.Add(station);
        }

        public static void UnregisterCraftingStation(ProjectC.Crafting.CraftingStation station)
        {
            if (station != null) _craftingStations.Remove(station);
        }

        public static List<ProjectC.Crafting.CraftingStation> GetCraftingStations() => _craftingStations;

        public static ProjectC.Crafting.CraftingStation FindNearestCraftingStation(Vector3 position, float range)
        {
            ProjectC.Crafting.CraftingStation nearest = null;
            float minDist = float.MaxValue;
            for (int i = 0; i < _craftingStations.Count; i++)
            {
                var st = _craftingStations[i];
                if (st == null || !st.gameObject.activeSelf) continue;
                float dist = Vector3.Distance(position, st.transform.position);
                if (dist < range && dist < minDist) { minDist = dist; nearest = st; }
            }
            return nearest;
        }

        // ==========================================================
        // ShipCargoConsole (T-CARGO-UI-02)
        // ==========================================================

        public static void RegisterShipCargoConsole(ProjectC.Ship.Cargo.ShipCargoConsole console)
        {
            if (console != null && !_shipCargoConsoles.Contains(console)) _shipCargoConsoles.Add(console);
        }

        public static void UnregisterShipCargoConsole(ProjectC.Ship.Cargo.ShipCargoConsole console)
        {
            if (console != null) _shipCargoConsoles.Remove(console);
        }

        public static ProjectC.Ship.Cargo.ShipCargoConsole FindNearestShipCargoConsole(Vector3 position, float range)
        {
            ProjectC.Ship.Cargo.ShipCargoConsole nearest = null;
            float minDist = float.MaxValue;
            for (int i = 0; i < _shipCargoConsoles.Count; i++)
            {
                var c = _shipCargoConsoles[i];
                if (c == null || !c.gameObject.activeSelf) continue;
                float dist = Vector3.Distance(position, c.transform.position);
                if (dist < range && dist < minDist) { minDist = dist; nearest = c; }
            }
            return nearest;
        }

        /// <summary>
        /// Clear all cached references. Call when scene changes.
        /// T-Q19: _npcs clear removed.
        /// </summary>
        public static void RegisterRepairManager(ProjectC.Ship.RepairManager rm)
        {
            if (rm != null && !_repairManagers.Contains(rm)) _repairManagers.Add(rm);
        }

        public static void UnregisterRepairManager(ProjectC.Ship.RepairManager rm)
        {
            if (rm != null) _repairManagers.Remove(rm);
        }

        public static ProjectC.Ship.RepairManager FindNearestRepairManager(Vector3 position, float range)
        {
            ProjectC.Ship.RepairManager nearest = null;
            float minDist = float.MaxValue;
            for (int i = 0; i < _repairManagers.Count; i++)
            {
                var rm = _repairManagers[i];
                if (rm == null || !rm.gameObject.activeSelf) continue;
                float dist = Vector3.Distance(position, rm.transform.position);
                if (dist < range && dist < minDist) { minDist = dist; nearest = rm; }
            }
            return nearest;
        }

        // ==========================================================
        // DoorController (T-PERF06)
        // ==========================================================

        /// <summary>Register a door while it is active and enabled.</summary>
        public static void RegisterDoor(DoorController door)
        {
            if (door == null) return;
            _doors.Add(door);
            _doorsVersion++;
        }

        /// <summary>Unregister a door when it is disabled or destroyed.</summary>
        public static void UnregisterDoor(DoorController door)
        {
            if (door != null && _doors.Remove(door))
                _doorsVersion++;
        }

        /// <summary>
        /// Find nearest door within range: distance to the closest point of its interaction collider
        /// (fallback — distance to the transform). Same rule the two FindObjectsByType call sites used.
        /// Zero allocations.
        /// </summary>
        public static DoorController FindNearestDoor(Vector3 position, float range, out float distance)
        {
            if (_emptyDoorQueryVersion == _doorsVersion
                && Mathf.Approximately(_emptyDoorQueryRange, range)
                && (position - _emptyDoorQueryPosition).sqrMagnitude
                    <= EmptyDoorQueryReuseRadius * EmptyDoorQueryReuseRadius)
            {
                distance = 0f;
                return null;
            }

            DoorController nearest = null;
            float minDist = float.MaxValue;
            for (int i = 0; i < _doors.Count; i++)
            {
                var door = _doors[i];
                if (door == null) { _doors.RemoveAt(i--); continue; }
                if (!door.isActiveAndEnabled) continue;

                // T-PERF09: сначала отсекаем двери консервативной сферой,
                // чтобы не читать bounds у всех ~2 800 коллайдеров на каждый запрос.
                float broadphaseRange = range + door.InteractionBoundingRadius;
                if ((position - door.transform.position).sqrMagnitude > broadphaseRange * broadphaseRange)
                    continue;

                float dist = door.DistanceToInteraction(position);
                if (dist < range && dist < minDist) { minDist = dist; nearest = door; }
            }

            if (nearest == null)
            {
                _emptyDoorQueryVersion = _doorsVersion;
                _emptyDoorQueryPosition = position;
                _emptyDoorQueryRange = range;
            }
            else
            {
                _emptyDoorQueryVersion = -1;
            }

            distance = nearest != null ? minDist : 0f;
            return nearest;
        }

        /// <summary>Быстрый observation-запрос для contextual hint, без изменения выбора цели действия.</summary>
        public static bool HasDoorInRange(Vector3 position, float range)
        {
            if (_hintDoorVersion == _doorsVersion && Mathf.Approximately(_hintDoorRange, range))
            {
                // T-PERF22: одной активной двери в радиусе достаточно для bool-подсказки
                // на любом расстоянии между запросами. Ближайшую цель действие выбирает отдельно.
                if (_hintDoor != null && _hintDoor.isActiveAndEnabled
                    && _hintDoor.DistanceToInteraction(position) < range)
                    return true;
                if (_hintDoor == null
                    && (position - _hintDoorPosition).sqrMagnitude
                        <= EmptyDoorQueryReuseRadius * EmptyDoorQueryReuseRadius)
                    return false;
            }

            // T-PERF24: вместо полного обхода реестра — клетки сетки вокруг игрока.
            // Условие «дверь в радиусе» то же (broadphase по transform + DistanceToInteraction),
            // поэтому bool-ответ не меняется; пары отрицательный/положительный кэш выше сохранены.
            _hintDoor = FindDoorInGrid(position, range);
            _hintDoorVersion = _doorsVersion;
            _hintDoorPosition = position;
            _hintDoorRange = range;
            return _hintDoor != null;
        }

        private static long DoorCellKey(int x, int z) => ((long)x << 32) ^ (uint)z;

        /// <summary>
        /// T-PERF24: индекс по клеткам XZ для observation-подсказки. Пересобирается только
        /// при смене состава реестра или при мировом сдвиге (Floating Origin rebase),
        /// который ловится по смещению одной «пробирной» двери.
        /// </summary>
        private static void EnsureDoorGrid()
        {
            if (_doorGridBuilt && _doorGridVersion == _doorsVersion)
            {
                if (_doorGridProbe == null) return;
                if ((_doorGridProbe.transform.position - _doorGridProbePosition).sqrMagnitude
                    <= DoorGridResyncRadius * DoorGridResyncRadius) return;
            }

            BuildDoorGrid();
        }

        private static void BuildDoorGrid()
        {
            _doorGrid.Clear();
            _doorGridDynamic.Clear();
            _doorGridMaxRadius = 0f;
            _doorGridProbe = null;

            for (int i = 0; i < _doors.Count; i++)
            {
                var door = _doors[i];
                if (door == null) continue;

                float radius = door.InteractionBoundingRadius;
                // Корабельные двери уезжают вместе с кораблём, а не-боксовый коллайдер
                // отключает broadphase (радиус = +inf): и те, и другие — линейно.
                if (door.ShipController != null || float.IsInfinity(radius))
                {
                    _doorGridDynamic.Add(door);
                    continue;
                }

                Vector3 p = door.transform.position;
                long key = DoorCellKey(Mathf.FloorToInt(p.x / DoorGridCellSize), Mathf.FloorToInt(p.z / DoorGridCellSize));
                if (!_doorGrid.TryGetValue(key, out var bucket))
                {
                    bucket = new List<DoorController>(8);
                    _doorGrid.Add(key, bucket);
                }
                bucket.Add(door);

                if (radius > _doorGridMaxRadius) _doorGridMaxRadius = radius;
                if (_doorGridProbe == null) { _doorGridProbe = door; _doorGridProbePosition = p; }
            }

            _doorGridVersion = _doorsVersion;
            _doorGridBuilt = true;
        }

        /// <summary>
        /// Ближайшая дверь в радиусе по сетке. Правило отбора дословно совпадает с
        /// FindNearestDoor: broadphase по transform.position + InteractionBoundingRadius,
        /// затем точное DistanceToInteraction. Позиция двери читается живьём (FO-safe).
        /// </summary>
        private static DoorController FindDoorInGrid(Vector3 position, float range)
        {
            EnsureDoorGrid();

            DoorController nearest = null;
            float minDist = float.MaxValue;

            for (int i = 0; i < _doorGridDynamic.Count; i++)
            {
                var door = _doorGridDynamic[i];
                if (door == null || !door.isActiveAndEnabled) continue;
                float broad = range + door.InteractionBoundingRadius;
                if ((position - door.transform.position).sqrMagnitude > broad * broad) continue;
                float dist = door.DistanceToInteraction(position);
                if (dist < range && dist < minDist) { minDist = dist; nearest = door; }
            }

            // Клетки, которые может задеть радиус: расширены на максимальный
            // InteractionBoundingRadius, иначе дверь с далёким transform и большим
            // коллайдером потерялась бы между клетками.
            float reach = Mathf.Max(0f, range) + _doorGridMaxRadius;
            int minX = Mathf.FloorToInt((position.x - reach) / DoorGridCellSize);
            int maxX = Mathf.FloorToInt((position.x + reach) / DoorGridCellSize);
            int minZ = Mathf.FloorToInt((position.z - reach) / DoorGridCellSize);
            int maxZ = Mathf.FloorToInt((position.z + reach) / DoorGridCellSize);

            for (int cx = minX; cx <= maxX; cx++)
            {
                for (int cz = minZ; cz <= maxZ; cz++)
                {
                    if (!_doorGrid.TryGetValue(DoorCellKey(cx, cz), out var bucket)) continue;
                    for (int k = 0; k < bucket.Count; k++)
                    {
                        var door = bucket[k];
                        if (door == null || !door.isActiveAndEnabled) continue;
                        float broad = range + door.InteractionBoundingRadius;
                        if ((position - door.transform.position).sqrMagnitude > broad * broad) continue;
                        float dist = door.DistanceToInteraction(position);
                        if (dist < range && dist < minDist) { minDist = dist; nearest = door; }
                    }
                }
            }

            return nearest;
        }

        /// <summary>
        /// T-PERF06: collider the player boards a ship through (PilotSeat, fallback — first collider),
        /// resolved once per ship instead of GetComponentInChildren&lt;T&gt;(true) on every query.
        /// <paramref name="seat"/> — null, когда коллайдера кресла нет и сработал fallback:
        /// тогда радиус посадки — переданный range (ровно как до правки).
        /// </summary>
        private static Collider ResolveBoardingCollider(ShipController ship, out PilotSeatController seat)
        {
            if (_shipBoardingTargets.TryGetValue(ship, out var cached))
            {
                if (cached.Collider != null) { seat = cached.Seat; return cached.Collider; }
                if (Time.frameCount - cached.ResolvedFrame < BoardingTargetRetryFrames) { seat = null; return null; }
            }

            seat = ship.GetComponentInChildren<PilotSeatController>(true);
            Collider collider = seat != null ? seat.SeatCollider : null;
            // Кресло есть, но его Awake ещё не проставил кэш — берём коллайдер напрямую
            // (GetComponent не аллоцирует), иначе кэш навсегда зафиксирует чужой коллайдер.
            if (collider == null && seat != null) collider = seat.GetComponent<Collider>();
            if (collider == null) seat = null;
            if (collider == null) collider = ship.GetComponentInChildren<Collider>();

            _shipBoardingTargets[ship] = new ShipBoardingTarget
            {
                Seat = seat,
                Collider = collider,
                ResolvedFrame = Time.frameCount
            };
            return collider;
        }

        public static void ClearAll()
        {
            _pickups.Clear();
            _chests.Clear();
            _ships.Clear();
            _resourceNodes.Clear();
            _craftingStations.Clear();
            _shipCargoConsoles.Clear();
            _repairManagers.Clear();
            _doors.Clear();
            _doorGrid.Clear();
            _doorGridDynamic.Clear();
            _doorGridBuilt = false;
            _doorGridVersion = -1;
            _shipBoardingTargets.Clear();
        }

        /// <summary>
        /// Find nearest pickup within range. Zero allocations.
        /// </summary>
        public static PickupItem FindNearestPickup(Vector3 position, float range)
        {
            PickupItem nearest = null;
            float minDist = float.MaxValue;

            for (int i = 0; i < _pickups.Count; i++)
            {
                var pickup = _pickups[i];
                if (pickup == null || !pickup.gameObject.activeSelf) continue;

                float dist = Vector3.Distance(position, pickup.transform.position);
                if (dist < range && dist < minDist)
                {
                    minDist = dist;
                    nearest = pickup;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Find nearest chest within range. Zero allocations.
        /// </summary>
        public static ChestContainer FindNearestChest(Vector3 position, float range)
        {
            ChestContainer nearest = null;
            float minDist = float.MaxValue;

            for (int i = 0; i < _chests.Count; i++)
            {
                var chest = _chests[i];
                if (chest == null || !chest.gameObject.activeSelf) continue;

                float dist = Vector3.Distance(position, chest.transform.position);
                if (dist < range && dist < minDist)
                {
                    minDist = dist;
                    nearest = chest;
                }
            }

            return nearest;
        }

        // T-Q19: FindNearestNpc removed (v1 dead code, see top of file).

        /// <summary>
        /// COMPOSITE SHIP (Phase 1): Find nearest ship within range. Zero allocations.
        /// Считаем distance только до PilotSeat collider'а (IsTrigger=true).
        /// Платформа корабля (BoxCollider IsTrigger=false) НЕ участвует в поиске —
        /// это даёт игроку чёткую зону посадки, а не "всю палубу".
        ///
        /// Fallback на старый путь (любой Collider) если PilotSeat не найден —
        /// чтобы старые префабы без PilotSeat продолжали работать.
        /// </summary>
        public static ShipController FindNearestShip(Vector3 position, float range)
        {
            ShipController nearest = null;
            float minDist = float.MaxValue;

            for (int i = 0; i < _ships.Count; i++)
            {
                var ship = _ships[i];
                if (ship == null || !ship.gameObject.activeSelf) continue;

                // COMPOSITE SHIP: distance до PilotSeat (если есть), иначе fallback.
                // T-PERF06: коллайдер кресла резолвится один раз на корабль (кэш), а не
                // GetComponentInChildren<T>(true) на каждый запрос.
                Collider targetCollider = ResolveBoardingCollider(ship, out PilotSeatController pilotSeat);
                if (targetCollider == null) continue;

                // COMPOSITE SHIP (Phase 1): если есть PilotSeat — радиус посадки = размер
                // коллайдера кресла (обычно ~1-2м). Без PilotSeat (старые корабли) —
                // используем переданный range (boardDistance, ~5м).
                float effectiveRange = range;
                if (pilotSeat != null)
                {
                    // Радиус от кресла: половина диагонали коллайдера + запас 0.3м
                    Vector3 colSize = targetCollider.bounds.size;
                    float seatRadius = Mathf.Max(colSize.x, colSize.y, colSize.z) * 0.6f + 0.3f;
                    effectiveRange = Mathf.Min(range, Mathf.Max(seatRadius, 1.5f));
                }

                Vector3 closest = targetCollider.bounds.ClosestPoint(position);
                float dist = Vector3.Distance(position, closest);

                if (dist < effectiveRange && dist < minDist)
                {
                    minDist = dist;
                    nearest = ship;
                }
            }

            return nearest;
        }
    }
}
