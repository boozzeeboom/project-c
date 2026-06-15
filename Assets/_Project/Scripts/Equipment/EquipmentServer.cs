// Project C: Character Progression — T-P09
// EquipmentServer: NetworkBehaviour RPC hub для equip/unequip. Scene-placed в BootstrapScene.
// Design: docs/Character/05_CLOTHING_AND_MODULES.md §4, docs/Character/08_ROADMAP.md T-P09
//
// Pattern: копия GatheringServer (T-G03) + InventoryServer для RPC hub + rate limit.
//
// RPCs (client→server):
//   - RequestEquipRpc(int itemId, EquipSlot slot) — RequestEquip
//   - RequestUnequipRpc(EquipSlot slot)            — RequestUnequip
// Server→client (TargetRPCs через NetworkPlayer):
//   - ReceiveEquipResultTargetRpc(EquipResultDto result)   — T-P10 stub
//   - ReceiveEquipmentSnapshotTargetRpc(EquipmentSnapshotDto snapshot) — T-P10 stub
// Stub: используем reflection для null-safe call (T-P10 NMC auto-spawn + actual RPCs).
//
// Cross-NetworkObject deps:
//   - EquipmentWorld (T-P09, same script)             — primary
//   - InventoryWorld (existing)                       — item ownership check
//   - StatsServer.Instance (T-P05)                    — RecomputeAndSendSnapshot после equip
//   - SkillsWorld (T-P13)                             — skill requirements (null-safe в T-P09)

using System;
using System.Collections.Generic;
using ProjectC.Equipment.Dto;
using ProjectC.Player;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Equipment
{
    public class EquipmentServer : NetworkBehaviour
    {
        public static EquipmentServer Instance { get; private set; }

        [Header("Rate limit")]
        [Tooltip("Max equip/unequip ops per second per client. Anti-spam.")]
        [SerializeField, Min(1)] private int _maxOpsPerSec = 5;

        // Rate limit state: clientId → next allowed time
        private readonly Dictionary<ulong, float> _nextAllowedTimePerClient = new Dictionary<ulong, float>();

        private EquipmentWorld _world;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[EquipmentServer] Duplicate instance detected, replacing.");
            }
            Instance = this;
            _world = new EquipmentWorld();

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[EquipmentServer] OnNetworkSpawn — IsServer=true, rateLimit={_maxOpsPerSec}ops/sec");
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;
            EquipmentWorld.Reset();
            if (Instance == this) Instance = null;
        }

        // === Rate limit ===

        private bool RateLimit(ulong clientId)
        {
            float now = Time.unscaledTime;
            if (_nextAllowedTimePerClient.TryGetValue(clientId, out var next) && now < next)
            {
                if (Debug.isDebugBuild)
                {
                    Debug.Log($"[EquipmentServer] Rate limit hit for client {clientId}, retry in {next - now:F2}s");
                }
                return false;
            }
            _nextAllowedTimePerClient[clientId] = now + 1f / _maxOpsPerSec;
            return true;
        }

        // === Client → Server RPCs ===

        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void RequestEquipRpc(int itemId, EquipSlot slot, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!RateLimit(clientId))
            {
                SendEquipResult(clientId, EquipResultDto.Denied("Слишком быстро"));
                return;
            }

            if (!_world.TryEquip(clientId, itemId, slot, out var reason))
            {
                SendEquipResult(clientId, EquipResultDto.Denied(reason));
                return;
            }

            SendEquipResult(clientId, EquipResultDto.Equipped(itemId, slot));
            // Recompute effective stats (after equip) — StatsServer T-P05 hook
            TriggerStatsRecompute(clientId);
            SendEquipmentSnapshotToOwner(clientId);
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void RequestUnequipRpc(EquipSlot slot, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!RateLimit(clientId))
            {
                SendEquipResult(clientId, EquipResultDto.Denied("Слишком быстро"));
                return;
            }

            if (!_world.TryUnequip(clientId, slot, out var reason))
            {
                SendEquipResult(clientId, EquipResultDto.Denied(reason));
                return;
            }

            SendEquipResult(clientId, EquipResultDto.Unequipped(slot));
            TriggerStatsRecompute(clientId);
            SendEquipmentSnapshotToOwner(clientId);
        }

        // === Server → Client (TargetRPCs через NetworkPlayer) ===

        private void SendEquipResult(ulong clientId, EquipResultDto result)
        {
            if (NetworkManager.Singleton == null) return;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
            var netPlayer = client.PlayerObject?.GetComponent<NetworkPlayer>();
            if (netPlayer == null) return;

            // Stub-RPC: T-P10 NMC auto-spawn + actual RPC. В T-P09 ищем через reflection (null-safe).
            var mi = typeof(NetworkPlayer).GetMethod("ReceiveEquipResultTargetRpc");
            if (mi != null)
            {
                var defaultRpcParams = System.Activator.CreateInstance(typeof(RpcParams));
                mi.Invoke(netPlayer, new object[] { result, defaultRpcParams });
            }
            else if (Debug.isDebugBuild)
            {
                Debug.Log($"[EquipmentServer] (T-P09 stub) equip result for {clientId}: code={result.code} slot={result.slot} reason='{result.reason}'");
            }
        }

        private void SendEquipmentSnapshotToOwner(ulong clientId)
        {
            if (NetworkManager.Singleton == null) return;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
            var netPlayer = client.PlayerObject?.GetComponent<NetworkPlayer>();
            if (netPlayer == null) return;

            var equip = _world.GetEquipment(clientId);
            var snap = new EquipmentSnapshotDto { equip = equip };

            var mi = typeof(NetworkPlayer).GetMethod("ReceiveEquipmentSnapshotTargetRpc");
            if (mi != null)
            {
                var defaultRpcParams = System.Activator.CreateInstance(typeof(RpcParams));
                mi.Invoke(netPlayer, new object[] { snap, defaultRpcParams });
            }
            else if (Debug.isDebugBuild)
            {
                int occupied = 0;
                foreach (var _ in snap.equip.EnumerateOccupiedSlots()) occupied++;
                Debug.Log($"[EquipmentServer] (T-P09 stub) equipment snapshot for {clientId}: {occupied} slots occupied");
            }
        }

        /// <summary>
        /// Cross-NetworkObject: после equip/unequip — recompute effective stats в StatsServer.
        /// Null-safe (T-P09 standalone, StatsServer из M1 уже на месте — но reflection fallback).
        /// </summary>
        private void TriggerStatsRecompute(ulong clientId)
        {
            var ssType = System.Type.GetType("ProjectC.Stats.StatsServer, Assembly-CSharp");
            if (ssType == null) return;
            var instProp = ssType.GetProperty("Instance");
            var inst = instProp?.GetValue(null);
            if (inst == null) return;
            var method = ssType.GetMethod("RecomputeAndSendSnapshot");
            method?.Invoke(inst, new object[] { clientId });
        }
    }
}
