// T-NS02: NpcShipZoneRegistry — статический lookup NpcInstanceId → NpcShipController.
// Pattern: DockingZoneRegistry (Docking/Network/DockingZoneRegistry.cs), ShipCargoRegistry (Ship/ShipCargoRegistry.cs:35).
// Используется DockingWorld и NpcShipWorld для быстрого поиска NPC по id.

using System.Collections.Generic;

namespace ProjectC.PeacefulShip.Network
{
    /// <summary>
    /// Статический реестр: NpcInstanceId → NpcShipController.
    /// Регистрация в NpcShipController.OnNetworkSpawn, удаление в OnNetworkDespawn.
    /// Pattern: DockingZoneRegistry (Docking/Network/DockingZoneRegistry.cs:12).
    /// </summary>
    public static class NpcShipZoneRegistry
    {
        private static readonly Dictionary<ulong, Stations.NpcShipController> _byNpcInstanceId
            = new Dictionary<ulong, Stations.NpcShipController>();

        /// <summary>Read-only dictionary. Для отладки и внешних итераций (например, NpcShipWorld.Update).</summary>
        public static IReadOnlyDictionary<ulong, Stations.NpcShipController> All => _byNpcInstanceId;

        /// <summary>Регистрация NPC. Idempotent — повторный Register перезаписывает.</summary>
        public static void Register(Stations.NpcShipController npc)
        {
            if (npc == null) return;
            ulong id = npc.NpcInstanceId;
            if (id == 0) return; // sentinel not set yet
            _byNpcInstanceId[id] = npc;
        }

        /// <summary>Удаление NPC из реестра (при OnNetworkDespawn или UnregisterNpc).</summary>
        public static void Unregister(Stations.NpcShipController npc)
        {
            if (npc == null) return;
            ulong id = npc.NpcInstanceId;
            if (id == 0) return;
            if (_byNpcInstanceId.TryGetValue(id, out var existing) && existing == npc)
            {
                _byNpcInstanceId.Remove(id);
            }
        }

        /// <summary>Lookup NPC по NpcInstanceId. Returns null если не найден.</summary>
        public static Stations.NpcShipController Get(ulong npcInstanceId)
            => _byNpcInstanceId.TryGetValue(npcInstanceId, out var n) ? n : null;

        /// <summary>Полная очистка (для shutdown / scene unload).</summary>
        public static void Clear()
        {
            _byNpcInstanceId.Clear();
        }
    }
}