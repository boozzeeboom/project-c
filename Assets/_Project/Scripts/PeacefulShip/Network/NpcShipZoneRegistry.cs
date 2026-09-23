// T-NS02: NpcShipZoneRegistry — статический lookup NpcInstanceId → NpcShipController.
// Pattern: DockingZoneRegistry (Docking/Network/DockingZoneRegistry.cs), ShipCargoRegistry (Ship/ShipCargoRegistry.cs:35).
// Используется DockingWorld и NpcShipWorld для быстрого поиска NPC по id.

using System.Collections.Generic;
using UnityEngine;

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
            _cells.Clear();
            _cellsBuiltAt = -1000f;
        }

        // === T-NS-WF02: spatial hash broadphase (задел на 200+ кораблей) ===
        // Ячейка XZ 500 м (больше max awarenessRadius) — запрос покрывает 3×3 ячейки.
        // Перестроение не чаще cellRebuildSec, вызывается лениво из QueryNearby.
        // Server-only по построению (реестр живёт только на сервере).
        private const float CellSize = 500f;
        private const float CellRebuildSec = 0.2f;
        private static float _cellsBuiltAt = -1000f;
        private static readonly Dictionary<Vector2Int, List<Stations.NpcShipController>> _cells
            = new Dictionary<Vector2Int, List<Stations.NpcShipController>>();

        private static Vector2Int CellOf(Vector3 pos)
            => new Vector2Int(Mathf.FloorToInt(pos.x / CellSize), Mathf.FloorToInt(pos.z / CellSize));

        private static void RebuildCellsIfStale()
        {
            if (Time.time - _cellsBuiltAt < CellRebuildSec) return;
            _cellsBuiltAt = Time.time;
            foreach (var list in _cells.Values) list.Clear();
            foreach (var kv in _byNpcInstanceId)
            {
                var npc = kv.Value;
                if (npc == null) continue;
                var cell = CellOf(npc.transform.position);
                if (!_cells.TryGetValue(cell, out var list))
                {
                    list = new List<Stations.NpcShipController>(8);
                    _cells[cell] = list;
                }
                list.Add(npc);
            }
        }

        /// <summary>
        /// Кандидаты в радиусе (broadphase): своя + соседние ячейки, точная дистанция —
        /// на вызывающей стороне. Пустой результат = рядом никого.
        /// </summary>
        public static List<Stations.NpcShipController> QueryNearby(Vector3 pos, float radius)
        {
            RebuildCellsIfStale();
            // Переиспользуемый буфер вызвающей стороны невозможен в static без аллокаций
            // на всех — собираем короткий список (ячейки уже отфильтровали грубо).
            var result = new List<Stations.NpcShipController>(16);
            var center = CellOf(pos);
            int range = Mathf.Max(1, Mathf.CeilToInt(radius / CellSize));
            for (int dx = -range; dx <= range; dx++)
                for (int dz = -range; dz <= range; dz++)
                {
                    var key = new Vector2Int(center.x + dx, center.y + dz);
                    if (!_cells.TryGetValue(key, out var list)) continue;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var npc = list[i];
                        if (npc == null) continue;
                        result.Add(npc);
                    }
                }
            return result;
        }
    }
}