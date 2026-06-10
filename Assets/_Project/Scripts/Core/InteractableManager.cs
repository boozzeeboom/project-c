using System.Collections.Generic;
using UnityEngine;
using ProjectC.Items;
using ProjectC.Player;

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

        /// <summary>
        /// Clear all cached references. Call when scene changes.
        /// T-Q19: _npcs clear removed.
        /// </summary>
        public static void ClearAll()
        {
            _pickups.Clear();
            _chests.Clear();
            _ships.Clear();
            _resourceNodes.Clear();
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
        /// Find nearest ship within range. Zero allocations.
        /// Учитывает размер корабля: если у корабля есть Collider, используем Bounds.ClosestPoint
        /// (иначе дистанция считается от центра transform.position и для больших кораблей
        /// игрок не может "подойти" в зону посадки у края).
        /// </summary>
        public static ShipController FindNearestShip(Vector3 position, float range)
        {
            ShipController nearest = null;
            float minDist = float.MaxValue;

            for (int i = 0; i < _ships.Count; i++)
            {
                var ship = _ships[i];
                if (ship == null || !ship.gameObject.activeSelf) continue;

                float dist;
                // Ищем ближайший Collider — учитываем ВСЕ коллайдеры (включая дочерние)
                // чтобы визуально увеличенные корабли (transform.localScale > 1) корректно
                // определяли свою "зону посадки"
                var collider = ship.GetComponentInChildren<Collider>();
                if (collider != null)
                {
                    Vector3 closest = collider.bounds.ClosestPoint(position);
                    dist = Vector3.Distance(position, closest);
                }
                else
                {
                    dist = Vector3.Distance(position, ship.transform.position);
                }

                if (dist < range && dist < minDist)
                {
                    minDist = dist;
                    nearest = ship;
                }
            }

            return nearest;
        }
    }
}
