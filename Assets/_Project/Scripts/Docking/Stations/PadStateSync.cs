// T-DOCK-14a: PadStateSync — NetworkBehaviour синхронизации состояния падов.
// Размещается на корне DockStation (рядом с DockStationController).
// Сервер обновляет состояние → ClientRpc рассылает снапшот всем клиентам.
//
// Клиентские DockPadVisualMarker (на pad-детях) читают состояние через
// GetComponentInParent<PadStateSync>() → GetState(padId).
//
// Причина ClientRpc вместо NetworkList: NGO 2.13 NetworkList<T> требует
// T : unmanaged, IEquatable<T> — INetworkSerializable struct не подходит.

using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Docking.Stations
{
    /// <summary>
    /// Запись состояния одного пада в снапшоте.
    /// </summary>
    public struct PadStateEntry : INetworkSerializable
    {
        public FixedString32Bytes padId;
        public bool isOccupied;
        public bool isPending;
        public bool isAssigned;
        public ulong occupiedByClientId;   // 0 = никто
        public ulong assignedToClientId;   // 0 = никто

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref padId);
            s.SerializeValue(ref isOccupied);
            s.SerializeValue(ref isPending);
            s.SerializeValue(ref isAssigned);
            s.SerializeValue(ref occupiedByClientId);
            s.SerializeValue(ref assignedToClientId);
        }
    }

    /// <summary>
    /// Снапшот всех падов станции — передаётся через ClientRpc.
    /// </summary>
    public struct PadStateSnapshot : INetworkSerializable
    {
        public FixedString32Bytes stationId;
        public int padCount;
        // Сериализуем как массив фиксированной длины (макс 16 падов)
        public PadStateEntry pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7;
        public PadStateEntry pad8, pad9, pad10, pad11, pad12, pad13, pad14, pad15;

        public PadStateEntry GetPad(int index)
        {
            return index switch
            {
                0 => pad0, 1 => pad1, 2 => pad2, 3 => pad3,
                4 => pad4, 5 => pad5, 6 => pad6, 7 => pad7,
                8 => pad8, 9 => pad9, 10 => pad10, 11 => pad11,
                12 => pad12, 13 => pad13, 14 => pad14, 15 => pad15,
                _ => default
            };
        }

        public void SetPad(int index, PadStateEntry entry)
        {
            switch (index)
            {
                case 0: pad0 = entry; break; case 1: pad1 = entry; break;
                case 2: pad2 = entry; break; case 3: pad3 = entry; break;
                case 4: pad4 = entry; break; case 5: pad5 = entry; break;
                case 6: pad6 = entry; break; case 7: pad7 = entry; break;
                case 8: pad8 = entry; break; case 9: pad9 = entry; break;
                case 10: pad10 = entry; break; case 11: pad11 = entry; break;
                case 12: pad12 = entry; break; case 13: pad13 = entry; break;
                case 14: pad14 = entry; break; case 15: pad15 = entry; break;
            }
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref stationId);
            s.SerializeValue(ref padCount);
            s.SerializeValue(ref pad0); s.SerializeValue(ref pad1);
            s.SerializeValue(ref pad2); s.SerializeValue(ref pad3);
            s.SerializeValue(ref pad4); s.SerializeValue(ref pad5);
            s.SerializeValue(ref pad6); s.SerializeValue(ref pad7);
            s.SerializeValue(ref pad8); s.SerializeValue(ref pad9);
            s.SerializeValue(ref pad10); s.SerializeValue(ref pad11);
            s.SerializeValue(ref pad12); s.SerializeValue(ref pad13);
            s.SerializeValue(ref pad14); s.SerializeValue(ref pad15);
        }
    }

    /// <summary>
    /// Синхронизирует состояние всех падов станции между сервером и клиентами.
    /// Требует NetworkObject на том же GameObject (корень DockStation).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PadStateSync : NetworkBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        /// <summary>Серверный кеш состояний падов.</summary>
        private readonly Dictionary<string, PadStateEntry> _serverState = new Dictionary<string, PadStateEntry>();

        /// <summary>Клиентский кеш (обновляется из ClientRpc).</summary>
        private readonly Dictionary<string, PadStateEntry> _clientState = new Dictionary<string, PadStateEntry>();

        /// <summary>Порядок падов (padId → индекс для снапшота).</summary>
        private string[] _padOrder;

        private DockingPadTriggerBox[] _triggerBoxes;
        private bool _initialized;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                InitializeFromScene();
            }
        }

        // ============================================================
        // INIT
        // ============================================================

        private void InitializeFromScene()
        {
            _triggerBoxes = GetComponentsInChildren<DockingPadTriggerBox>();
            if (_triggerBoxes == null || _triggerBoxes.Length == 0)
            {
                if (debugMode)
                    Debug.LogWarning($"[PadStateSync:{name}] No DockingPadTriggerBox in children");
                return;
            }

            if (_triggerBoxes.Length > 16)
            {
                Debug.LogError($"[PadStateSync:{name}] Too many pads ({_triggerBoxes.Length}), max 16 supported");
                return;
            }

            _padOrder = new string[_triggerBoxes.Length];
            _serverState.Clear();

            for (int i = 0; i < _triggerBoxes.Length; i++)
            {
                string pid = _triggerBoxes[i].PadId;
                _padOrder[i] = pid;
                _serverState[pid] = new PadStateEntry
                {
                    padId = pid,
                    isOccupied = false,
                    isPending = false,
                    isAssigned = false,
                    occupiedByClientId = 0,
                    assignedToClientId = 0
                };
            }

            _initialized = true;

            if (debugMode)
                Debug.Log($"[PadStateSync:{name}] Initialized {_triggerBoxes.Length} pads");

            // Первичная рассылка клиентам
            BroadcastState();
        }

        // ============================================================
        // SERVER API
        // ============================================================

        /// <summary>
        /// Обновить состояние конкретного пада. Только сервер.
        /// После обновления автоматически рассылает снапшот клиентам.
        /// </summary>
        public void UpdatePadState(
            string padId,
            bool? isOccupied = null,
            bool? isPending = null,
            bool? isAssigned = null,
            ulong? occupiedByClientId = null,
            ulong? assignedToClientId = null)
        {
            if (!IsServer)
            {
                Debug.LogError("[PadStateSync] UpdatePadState called on client — ignored");
                return;
            }
            if (!_serverState.TryGetValue(padId, out var entry))
            {
                if (debugMode)
                    Debug.Log($"[PadStateSync:{name}] Pad '{padId}' not found");
                return;
            }

            if (isOccupied.HasValue) entry.isOccupied = isOccupied.Value;
            if (isPending.HasValue) entry.isPending = isPending.Value;
            if (isAssigned.HasValue) entry.isAssigned = isAssigned.Value;
            if (occupiedByClientId.HasValue) entry.occupiedByClientId = occupiedByClientId.Value;
            if (assignedToClientId.HasValue) entry.assignedToClientId = assignedToClientId.Value;
            _serverState[padId] = entry;

            BroadcastState();
        }

        /// <summary>
        /// Сбросить все пады станции. Сервер.
        /// </summary>
        public void ClearAll()
        {
            if (!IsServer) return;
            foreach (var key in _serverState.Keys)
            {
                var entry = _serverState[key];
                entry.isOccupied = false;
                entry.isPending = false;
                entry.isAssigned = false;
                entry.occupiedByClientId = 0;
                entry.assignedToClientId = 0;
                _serverState[key] = entry;
            }
            BroadcastState();
        }

        /// <summary>
        /// Разослать текущий снапшот всем клиентам.
        /// </summary>
        private void BroadcastState()
        {
            if (!IsServer || !_initialized) return;

            var snapshot = new PadStateSnapshot
            {
                stationId = name,
                padCount = _padOrder.Length
            };

            for (int i = 0; i < _padOrder.Length; i++)
            {
                if (_serverState.TryGetValue(_padOrder[i], out var entry))
                    snapshot.SetPad(i, entry);
            }

            SyncStateClientRpc(snapshot);
        }

        // ============================================================
        // CLIENT RPC
        // ============================================================

        [Rpc(SendTo.NotServer)]
        private void SyncStateClientRpc(PadStateSnapshot snapshot)
        {
            _clientState.Clear();
            for (int i = 0; i < snapshot.padCount; i++)
            {
                var entry = snapshot.GetPad(i);
                _clientState[entry.padId.ToString()] = entry;
            }
            _initialized = true;
        }

        // ============================================================
        // CLIENT + SERVER API
        // ============================================================

        /// <summary>
        /// Получить состояние пада по padId. На клиенте — из снапшота,
        /// на сервере — из server-state.
        /// </summary>
        public PadStateEntry? GetState(string padId)
        {
            if (!_initialized) return null;

            if (IsServer)
            {
                if (_serverState.TryGetValue(padId, out var entry))
                    return entry;
            }
            else
            {
                if (_clientState.TryGetValue(padId, out var entry))
                    return entry;
            }
            return null;
        }

        /// <summary>
        /// Для отладки: получить все padId в порядке индекса.
        /// </summary>
        public string[] GetPadIds()
        {
            if (_padOrder != null) return _padOrder;
            if (!_initialized) return System.Array.Empty<string>();
            // Клиент: извлекаем из clientState
            var ids = new string[_clientState.Count];
            int idx = 0;
            foreach (var kv in _clientState)
                ids[idx++] = kv.Key;
            return ids;
        }
    }
}
