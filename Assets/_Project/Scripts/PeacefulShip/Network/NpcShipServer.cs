// T-NS06: NpcShipServer — server hub для подсистемы мирных NPC-кораблей.
// NetworkBehaviour singleton, scene-placed в BootstrapScene.
// Auto-spawn через ScenePlacedObjectSpawner (как DockingServer, MarketServer, QuestServer).
//
// Pattern: DockingServer (Docking/Network/DockingServer.cs), QuestServer (Quests/Network/QuestServer.cs).
// Docs: docs/NPC_others_peacfull/pc_ship/03_V2_ARCHITECTURE.md §2.7.

using System.Collections;
using ProjectC.PeacefulShip.Core;
using ProjectC.PeacefulShip.Stations; // NpcShipSchedule
using ProjectC.Player;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.PeacefulShip.Network
{
    [RequireComponent(typeof(NetworkObject))]
    public class NpcShipServer : NetworkBehaviour
    {
        public static NpcShipServer Instance { get; private set; }

        [Header("Setup")]
        [Tooltip("Все доступные расписания для NPC (опционально — для дебаг-инициализации). " +
                 "Обычно NPC-корабли получают schedule через NpcShipController.Schedule на сцене.")]
        [SerializeField] private NpcShipSchedule[] allSchedules;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // ============================================================
        // LIFECYCLE
        // ============================================================

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (Instance == null) Instance = this;
            else if (Instance != this)
            {
                Debug.LogWarning("[NpcShipServer] Second instance detected, ignoring", this);
                return;
            }

            if (!IsServer)
            {
                enabled = false;
                return;
            }

            // Init server state
            NpcShipWorld.CreateAndInitialize();
            NpcShipTrafficManager.CreateAndInitialize();
            NpcCargoService.CreateAndInitialize(); // T-CARGO-NPC-01: NPC trader service

            // Подписка на загрузку сцен (через уже существующий механизм ScenePlacedObjectSpawner)
            // NPC-корабли на сцене спавнятся автоматически через ScenePlacedObjectSpawner.
            // Нам нужно зарегистрировать их после того как все NpcShipController.OnNetworkSpawn отработали.
            StartCoroutine(DiscoverNpcShipsDelayed());

            if (debugMode)
                Debug.Log("[NpcShipServer] OnNetworkSpawn — NpcShipWorld + NpcShipTrafficManager + NpcCargoService initialized");
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (IsServer)
            {
                NpcCargoService.Shutdown(); // T-CARGO-NPC-01
                NpcShipTrafficManager.Shutdown();
                NpcShipWorld.Shutdown();
                NpcShipZoneRegistry.Clear();

                if (debugMode)
                    Debug.Log("[NpcShipServer] OnNetworkDespawn — systems shut down");
            }

            if (Instance == this) Instance = null;
        }

        // ============================================================
        // NPC DISCOVERY
        // ============================================================

        /// <summary>
        /// Через 2 секунды после OnNetworkSpawn (все сцены должны быть загружены)
        /// сканируем все NpcShipController на сцене.
        /// </summary>
        private IEnumerator DiscoverNpcShipsDelayed()
        {
            yield return new WaitForSeconds(2f);

            var npcs = FindObjectsByType<Stations.NpcShipController>(FindObjectsInactive.Exclude);
            int count = 0;
            foreach (var npc in npcs)
            {
                if (npc == null || npc.Schedule == null)
                {
                    Debug.LogWarning($"[NpcShipServer] NPC {npc?.name} has no Schedule assigned", npc);
                    continue;
                }

                ulong id = npc.NpcInstanceId;
                if (id == 0)
                {
                    Debug.LogWarning($"[NpcShipServer] NPC {npc.name} has npcInstanceId = 0 (not spawned yet?)", npc);
                    continue;
                }

                // Ленивая регистрация (если NpcShipController уже зарегистрировал себя в OnNetworkSpawn — игнорируем)
                var existing = NpcShipWorld.Instance?.GetNpc(id);
                if (existing != null)
                {
                    count++;
                    continue;
                }

                var ship = npc.GetComponent<ShipController>();
                if (ship == null)
                {
                    Debug.LogWarning($"[NpcShipServer] NPC {npc.name} has no ShipController", npc);
                    continue;
                }

                NpcShipWorld.Instance?.RegisterNpc(id, ship, npc.Schedule);
                count++;
            }

            if (debugMode)
                Debug.Log($"[NpcShipServer] DiscoverNpcShipsDelayed — found {npcs.Length} NpcShipController(s), registered {count} new");
        }

        // ============================================================
        // PUBLIC API (server-side)
        // ============================================================

        /// <summary>Количество зарегистрированных NPC-кораблей.</summary>
        public int NpcCount => NpcShipWorld.Instance != null ? NpcShipWorld.Instance.AllNpcCount : 0;

        /// <summary>Заставить NPC ре-дискверить сцену (для debug).</summary>
        [ContextMenu("Re-Discover NPC Ships")]
        public void DebugRediscover()
        {
            if (!IsServer) return;
            StartCoroutine(DiscoverNpcShipsDelayed());
        }
    }
}