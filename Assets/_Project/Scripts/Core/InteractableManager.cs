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

        public static void ClearAll()
        {
            _pickups.Clear();
            _chests.Clear();
            _ships.Clear();
            _resourceNodes.Clear();
            _craftingStations.Clear();
            _shipCargoConsoles.Clear();
            _repairManagers.Clear();
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
                var pilotSeat = ship.GetComponentInChildren<PilotSeatController>(true);
                Collider targetCollider = pilotSeat != null ? pilotSeat.GetComponent<Collider>() : null;

                // COMPOSITE SHIP (Phase 1): если есть PilotSeat — радиус посадки = размер
                // коллайдера кресла (обычно ~1-2м). Без PilotSeat (старые корабли) —
                // используем переданный range (boardDistance, ~5м).
                float effectiveRange = range;
                if (pilotSeat != null && targetCollider != null)
                {
                    // Радиус от кресла: половина диагонали коллайдера + запас 0.3м
                    Vector3 colSize = targetCollider.bounds.size;
                    float seatRadius = Mathf.Max(colSize.x, colSize.y, colSize.z) * 0.6f + 0.3f;
                    effectiveRange = Mathf.Min(range, Mathf.Max(seatRadius, 1.5f));
                }

                if (targetCollider == null)
                {
                    // Fallback: первый collider в детях (старое поведение для префабов без PilotSeat)
                    targetCollider = ship.GetComponentInChildren<Collider>();
                }
                if (targetCollider == null) continue;

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
