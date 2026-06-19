// =====================================================================================
// ShipOwnershipRegistry.cs — server-side реестр ownership для ship telemetry
// (R2-SHIP-KEY-003, T-KEY-07)
// =====================================================================================
// Документация:
//   • docs/Ships/Key-subsystem/22_SHIP_TELEMETRY_PLAN.md §2.4
//   • docs/Ships/Key-subsystem/23_ROADMAP.md T-KEY-07
//
// Назначение: NetworkBehaviour на Bootstrap-сцене. Хранит mapping shipNetId →
// ownerClientId в NetworkList<OwnershipEntry>. Синхронизируется клиентам —
// каждый клиент фильтрует "мои" корабли локально через ShipTelemetryClientState.
//
// Подписка на KeyRodInstanceWorld.OnOwnershipChanged — автоматическое обновление
// при transfer/destroy events.
// =====================================================================================

using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectC.Ship.Key;

namespace ProjectC.Ship.Network
{
    [DisallowMultipleComponent]
    public class ShipOwnershipRegistry : NetworkBehaviour
    {
        public static ShipOwnershipRegistry Instance { get; private set; }

        [Serializable]
        public struct OwnershipEntry : INetworkSerializable, IEquatable<OwnershipEntry>
        {
            public ulong shipNetworkObjectId;
            public ulong ownerClientId;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref shipNetworkObjectId);
                serializer.SerializeValue(ref ownerClientId);
            }

            public bool Equals(OwnershipEntry other)
            {
                return shipNetworkObjectId == other.shipNetworkObjectId
                    && ownerClientId == other.ownerClientId;
            }

            public override bool Equals(object obj) => obj is OwnershipEntry o && Equals(o);
            public override int GetHashCode()
            {
                unchecked
                {
                    return shipNetworkObjectId.GetHashCode() * 31 + ownerClientId.GetHashCode();
                }
            }
        }

        // NetworkList синхронизируется клиентам (read = everyone, write = server)
        private readonly NetworkList<OwnershipEntry> _ownership = new NetworkList<OwnershipEntry>(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>Read-only список ownership (для ShipTelemetryClientState).
        /// NetworkList поддерживает итерацию через foreach, но не реализует IReadOnlyList —
        /// клиентский код использует foreach напрямую.</summary>
        public NetworkList<OwnershipEntry> OwnershipList => _ownership;

        /// <summary>T-KEY-07: event для клиентов (вызывается при изменении NetworkList на клиенте).
        /// Подписка через OnListChanged делегат ниже.</summary>
        public event System.Action OnOwnershipListChanged;

        // ===========================================================
        // Public server-side API
        // ===========================================================

        /// <summary>Server-only: добавить или обновить запись ownership.</summary>
        public void SetOwner(ulong shipNetworkObjectId, ulong ownerClientId)
        {
            if (!IsServer)
            {
                Debug.LogWarning($"[ShipOwnershipRegistry] SetOwner called on client. Skipped.");
                return;
            }

            for (int i = 0; i < _ownership.Count; i++)
            {
                if (_ownership[i].shipNetworkObjectId == shipNetworkObjectId)
                {
                    var entry = _ownership[i];
                    if (entry.ownerClientId != ownerClientId)
                    {
                        entry.ownerClientId = ownerClientId;
                        _ownership[i] = entry;
                        Debug.Log($"[ShipOwnershipRegistry] SetOwner: ship={shipNetworkObjectId} → owner={ownerClientId}");
                    }
                    return;
                }
            }

            _ownership.Add(new OwnershipEntry
            {
                shipNetworkObjectId = shipNetworkObjectId,
                ownerClientId = ownerClientId
            });
            Debug.Log($"[ShipOwnershipRegistry] SetOwner: ship={shipNetworkObjectId} owner={ownerClientId} (new entry)");
        }

        /// <summary>Server-only: удалить запись (при уничтожении корабля).</summary>
        public void RemoveOwner(ulong shipNetworkObjectId)
        {
            if (!IsServer) return;
            for (int i = 0; i < _ownership.Count; i++)
            {
                if (_ownership[i].shipNetworkObjectId == shipNetworkObjectId)
                {
                    _ownership.RemoveAt(i);
                    Debug.Log($"[ShipOwnershipRegistry] RemoveOwner: ship={shipNetworkObjectId}");
                    return;
                }
            }
        }

        // ===========================================================
        // Lifecycle
        // ===========================================================

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (Instance == null) Instance = this;

            // T-KEY-07: подписка на NetworkList.OnListChanged для клиентских уведомлений
            _ownership.OnListChanged += HandleListChanged;

            if (IsServer && KeyRodInstanceWorld.IsInitialized)
            {
                KeyRodInstanceWorld.OnOwnershipChanged += HandleOwnershipChanged;
                Debug.Log($"[ShipOwnershipRegistry] OnNetworkSpawn: subscribed to OnOwnershipChanged");
            }
            else if (IsServer && !KeyRodInstanceWorld.IsInitialized)
            {
                Debug.LogWarning($"[ShipOwnershipRegistry] OnNetworkSpawn: KeyRodInstanceWorld not initialized yet. " +
                                 $"Will retry on first ownership event.");
            }

            Debug.Log($"[ShipOwnershipRegistry] OnNetworkSpawn. IsServer={IsServer}, _ownership.Count={_ownership.Count}");
        }

        public override void OnNetworkDespawn()
        {
            _ownership.OnListChanged -= HandleListChanged;
            if (IsServer && KeyRodInstanceWorld.IsInitialized)
            {
                KeyRodInstanceWorld.OnOwnershipChanged -= HandleOwnershipChanged;
            }
            if (Instance == this) Instance = null;
            base.OnNetworkDespawn();
        }

        private void HandleListChanged(NetworkListEvent<OwnershipEntry> evt)
        {
            // Вызывается на клиенте при delta-sync от сервера
            OnOwnershipListChanged?.Invoke();
        }

        // ===========================================================
        // Server-side: подписка на изменения ownership
        // ===========================================================

        private void HandleOwnershipChanged(int instanceId, ulong newOwner)
        {
            if (!IsServer) return;

            // Lazy init: если KeyRodInstanceWorld не был готов при OnNetworkSpawn
            if (!KeyRodInstanceWorld.IsInitialized)
            {
                KeyRodInstanceWorld.CreateAndInitialize();
                KeyRodInstanceWorld.OnOwnershipChanged += HandleOwnershipChanged;
            }

            // Найти ship по instanceId
            var inst = KeyRodInstanceWorld.GetInstance(instanceId);
            if (inst == null) return;
            ulong shipNetId = inst.registeredShipId;
            if (shipNetId == 0) return;  // ключ не привязан к кораблю

            if (newOwner == KeyRodInstance.OWNER_NONE)
            {
                RemoveOwner(shipNetId);
            }
            else
            {
                SetOwner(shipNetId, newOwner);
            }
        }
    }
}