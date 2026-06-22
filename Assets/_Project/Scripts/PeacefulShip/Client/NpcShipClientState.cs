// T-NS07: NpcShipClientState — клиентский singleton для UI/HUD проекции NPC-кораблей.
// Pattern: DockingClientState (Docking/Client/DockingClientState.cs), MarketClientState (Trade/Client/).
//
// M1: хранит список VisibleNpcs для HUD. Подписка на NpcShipServer BroadcastRpcs (v2).
// V2: UI диалог с NPC-капитаном, контракты, trade offers.

using System.Collections.Generic;
using ProjectC.PeacefulShip.Dto;
using UnityEngine;

namespace ProjectC.PeacefulShip.Client
{
    /// <summary>
    /// Client-side singleton для UI/HUD. Read-only список NPC-кораблей.
    /// Создаётся автоматически через RuntimeInitializeOnLoadMethod (как MarketClientState).
    /// </summary>
    public class NpcShipClientState : MonoBehaviour
    {
        public static NpcShipClientState Instance { get; private set; }

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // === Public UI projection ===

        /// <summary>
        /// Снимок видимых NPC-кораблей для HUD.
        /// Обновляется через HandleNpcSpawn / HandleNpcStatus.
        /// </summary>
        public struct NpcShipView
        {
            public ulong npcInstanceId;
            public ulong shipNetworkObjectId;
            public string displayName;
            public string currentStationId;
            public string statusDisplay;  // "Docked", "InTransit", etc.
        }

        public List<NpcShipView> VisibleNpcs { get; private set; } = new List<NpcShipView>();

        // === Lifecycle ===

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance != null) return;
            var go = new GameObject("[NpcShipClientState]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<NpcShipClientState>();
            if (Instance.debugMode)
                Debug.Log("[NpcShipClientState] Auto-created (singleton)");
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // === Public API (client-side, вызывается из RPC-хендлеров v2) ===

        /// <summary>
        /// Обработать спавн нового NPC (v2: вызывается из NpcShipServer BroadcastRpc).
        /// M1: добавляет запись в VisibleNpcs.
        /// </summary>
        public void HandleNpcSpawn(NpcShipSpawnDto dto)
        {
            if (dto.npcInstanceId == 0) return;

            // Check duplicate
            for (int i = 0; i < VisibleNpcs.Count; i++)
            {
                if (VisibleNpcs[i].npcInstanceId == dto.npcInstanceId)
                {
                    UpdateView(i, dto);
                    return;
                }
            }

            VisibleNpcs.Add(MapToView(dto));
            if (debugMode)
                Debug.Log($"[NpcShipClientState] NPC spawned: id={dto.npcInstanceId} name={dto.displayName}");
        }

        /// <summary>
        /// Обработать изменение статуса (v2: вызывается из NpcShipServer BroadcastRpc).
        /// </summary>
        public void HandleNpcStatus(NpcShipStatusDto dto)
        {
            for (int i = 0; i < VisibleNpcs.Count; i++)
            {
                if (VisibleNpcs[i].npcInstanceId == dto.npcInstanceId)
                {
                    var view = VisibleNpcs[i];
                    view.currentStationId = dto.currentStationId;
                    view.statusDisplay = StatusToString((Core.NpcShipStatus)dto.statusRaw);
                    VisibleNpcs[i] = view;
                    return;
                }
            }
            // Unknown NPC — spawn on first status
            if (debugMode)
                Debug.LogWarning($"[NpcShipClientState] Received status for unknown NPC id={dto.npcInstanceId}");
        }

        /// <summary>Удалить NPC из списка (при OnNetworkDespawn).</summary>
        public void HandleNpcDespawn(ulong npcInstanceId)
        {
            VisibleNpcs.RemoveAll(v => v.npcInstanceId == npcInstanceId);
        }

        /// <summary>Полная очистка (при scene unload).</summary>
        public void Clear()
        {
            VisibleNpcs.Clear();
        }

        // === Helpers ===

        private static NpcShipView MapToView(NpcShipSpawnDto dto)
        {
            return new NpcShipView
            {
                npcInstanceId = dto.npcInstanceId,
                shipNetworkObjectId = dto.shipNetworkObjectId,
                displayName = string.IsNullOrEmpty(dto.displayName) ? $"NPC#{dto.npcInstanceId:X}" : dto.displayName,
                currentStationId = "",
                statusDisplay = StatusToString((Core.NpcShipStatus)dto.statusRaw)
            };
        }

        private void UpdateView(int index, NpcShipSpawnDto dto)
        {
            var view = VisibleNpcs[index];
            view.displayName = string.IsNullOrEmpty(dto.displayName) ? view.displayName : dto.displayName;
            view.shipNetworkObjectId = dto.shipNetworkObjectId;
            view.statusDisplay = StatusToString((Core.NpcShipStatus)dto.statusRaw);
            VisibleNpcs[index] = view;
        }

        private static string StatusToString(Core.NpcShipStatus status)
        {
            switch (status)
            {
                case Core.NpcShipStatus.Idle: return "Idle";
                case Core.NpcShipStatus.Departing: return "Departing";
                case Core.NpcShipStatus.InTransit: return "In Transit";
                case Core.NpcShipStatus.Approaching: return "Approaching";
                case Core.NpcShipStatus.Holding: return "Holding";
                case Core.NpcShipStatus.Diverting: return "Diverting";
                case Core.NpcShipStatus.Docking: return "Docking";
                case Core.NpcShipStatus.Docked: return "Docked";
                case Core.NpcShipStatus.Loading: return "Loading";
                case Core.NpcShipStatus.Undocking: return "Undocking";
                case Core.NpcShipStatus.Done: return "Done";
                default: return $"Unknown({(byte)status})";
            }
        }
    }
}