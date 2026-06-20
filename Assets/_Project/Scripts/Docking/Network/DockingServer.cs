// T-DOCK-01: DockingServer — server hub для подсистемы стыковочных портов.
// NetworkBehaviour singleton, scene-placed в BootstrapScene. Auto-spawn через ScenePlacedObjectSpawner.
// Паттерн: см. Assets/_Project/Quests/Network/QuestServer.cs (rate limit, OnNetworkSpawn, RPCs).
//
// Q3 (принято 2026-06-19): Сервер — SOT. Клиент не хранит занятость, получает push по RPC.
// Q7 (принято 2026-06-19): RequestConfirmAssignmentRpc — двусторонняя связь обязательна.
// Q8 (2026-06-19): RequestTakeoffRpc ТОЛЬКО для отстыковки из Docked. Вылет из OuterCommZone
//      через запрос разрешения — это отдельная подсистема Departure (T-DEPART-*), не docking.

using System.Collections.Generic;
using ProjectC.Docking.Core;     // DockingWorld
using ProjectC.Docking.Dto;      // DTO + DockingStatus
using ProjectC.Docking.Network;  // DockStationController, DockingZoneRegistry
using ProjectC.Player;            // ShipController, NetworkPlayer
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Docking.Network
{
    [RequireComponent(typeof(NetworkObject))]
    public class DockingServer : NetworkBehaviour
    {
        public static DockingServer Instance { get; private set; }

        [Header("Rate Limiting")]
        [Tooltip("Макс операций в минуту на клиента (0 = без лимита).")]
        [SerializeField, Min(0)] private int maxOpsPerMinute = 30;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // Per-client rate limiting (copy-paste из QuestServer:54-55)
        private readonly Dictionary<ulong, List<float>> _opTimestamps = new Dictionary<ulong, List<float>>();

        // ============================================================
        // LIFECYCLE
        // ============================================================

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (Instance == null) Instance = this;
            else if (Instance != this)
            {
                Debug.LogWarning("[DockingServer] Second instance detected, ignoring", this);
                return;
            }

            if (!IsServer)
            {
                enabled = false;
                return;
            }

            // Init server state machine
            DockingWorld.CreateAndInitialize();

            // Cleanup on disconnect
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }

            if (debugMode)
            {
                Debug.Log($"[DockingServer] OnNetworkSpawn — IsServer=true, maxOps/min={maxOpsPerMinute}");
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (IsServer)
            {
                if (NetworkManager.Singleton != null)
                {
                    NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
                }
                DockingWorld.Shutdown();
            }
            if (Instance == this) Instance = null;
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (DockingWorld.Instance != null)
            {
                // ReleaseAssignment чистит и pending, и confirmed
                DockingWorld.Instance.ReleaseAssignment(clientId, 0);
            }
        }

        // ============================================================
        // RATE LIMITING (copy-paste из QuestServer:464-481)
        // ============================================================

        private bool CheckRateLimit(ulong clientId)
        {
            if (maxOpsPerMinute <= 0) return true;
            if (!_opTimestamps.TryGetValue(clientId, out var timestamps))
            {
                timestamps = new List<float>();
                _opTimestamps[clientId] = timestamps;
            }
            timestamps.RemoveAll(t => Time.unscaledTime - t > 60f);
            if (timestamps.Count >= maxOpsPerMinute)
            {
                if (debugMode) Debug.LogWarning($"[DockingServer] Rate limit hit for client {clientId}");
                return false;
            }
            timestamps.Add(Time.unscaledTime);
            return true;
        }

        // ============================================================
        // CLIENT → SERVER RPCs
        // ============================================================

        /// <summary>
        /// Q7: пилот нажал "Запросить посадку". Сервер назначает pad, шлёт
        /// DockingAssignmentDto (через TargetRpc). Клиент должен подтвердить
        /// (RequestConfirmAssignmentRpc) перед тем как pad будет заблокирован.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestDockingRpc(string stationId, ulong shipNetworkObjectId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (debugMode) Debug.Log($"[DockingServer] RequestDockingRpc client={clientId} station={stationId} ship={shipNetworkObjectId}");

            if (!CheckRateLimit(clientId))
            {
                SendDockingAssignmentTargetRpc(clientId, MakeFail("RATE_LIMITED", shipNetworkObjectId));
                return;
            }
            if (DockingWorld.Instance == null)
            {
                Debug.LogError("[DockingServer] DockingWorld.Instance is null");
                return;
            }

            var station = DockingZoneRegistry.GetStation(stationId);
            if (station == null)
            {
                if (debugMode) Debug.LogWarning($"[DockingServer] station not found: {stationId}");
                SendDockingAssignmentTargetRpc(clientId, MakeFail("STATION_NOT_FOUND", shipNetworkObjectId));
                return;
            }

            var ship = FindNetworkObject(shipNetworkObjectId)?.GetComponent<ShipController>();
            if (ship == null || !ship.IsSpawned)
            {
                if (debugMode) Debug.LogWarning($"[DockingServer] ship not found: {shipNetworkObjectId}");
                SendDockingAssignmentTargetRpc(clientId, MakeFail("SHIP_NOT_FOUND", shipNetworkObjectId));
                return;
            }

            // Назначить pad
            var assignment = DockingWorld.Instance.AssignPad(station, ship, ship.ShipFlightClass);
            if (assignment.success)
            {
                // Q7: НЕ занимаем pad сразу. Регистрируем как pending — клиент
                // должен подтвердить за 30 сек.
                DockingWorld.Instance.RegisterPendingAssignment(clientId, shipNetworkObjectId, assignment);
                if (debugMode) Debug.Log($"[DockingServer] Assigned pending: pad={assignment.padId} client={clientId}");
            }
            else
            {
                if (debugMode) Debug.LogWarning($"[DockingServer] AssignPad failed: {assignment.failReason}");
            }
            SendDockingAssignmentTargetRpc(clientId, assignment);
        }

        /// <summary>
        /// Q7: игрок подтверждает назначение (Хорошо) или отказывается (Отбой).
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestConfirmAssignmentRpc(ulong shipNetworkObjectId, bool accept, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (debugMode) Debug.Log($"[DockingServer] RequestConfirmAssignmentRpc client={clientId} ship={shipNetworkObjectId} accept={accept}");

            if (!CheckRateLimit(clientId)) return;
            if (DockingWorld.Instance == null) return;

            if (accept)
            {
                DockingWorld.Instance.ConfirmAssignment(clientId, shipNetworkObjectId);
                var assignment = DockingWorld.Instance.GetAssignment(clientId, shipNetworkObjectId);
                if (assignment.HasValue)
                {
                    var status = new DockingStatusDto
                    {
                        status = DockingStatus.Assigned,
                        stationId = assignment.Value.stationId,
                        padId = assignment.Value.padId,
                        timestamp = Time.time
                    };
                    if (debugMode) Debug.Log($"[DockingServer] Confirmed: pad={status.padId} client={clientId}");
                    SendDockingStatusTargetRpc(clientId, status);
                }
            }
            else
            {
                // Отбой — освобождаем pending assignment
                DockingWorld.Instance.CancelPendingAssignment(clientId, shipNetworkObjectId);
                var status = new DockingStatusDto
                {
                    status = DockingStatus.Cancelled,
                    stationId = "",
                    padId = "",
                    timestamp = Time.time
                };
                if (debugMode) Debug.Log($"[DockingServer] Cancelled pending for client {clientId}");
                SendDockingStatusTargetRpc(clientId, status);
            }
        }

        /// <summary>
        /// Q8: пилот хочет отстыковаться (из Docked состояния). ТОЛЬКО для отстыковки.
        /// Вылет из OuterCommZone — подсистема Departure (T-DEPART-*).
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestTakeoffRpc(ulong shipNetworkObjectId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (debugMode) Debug.Log($"[DockingServer] RequestTakeoffRpc client={clientId} ship={shipNetworkObjectId}");

            if (!CheckRateLimit(clientId)) return;
            if (DockingWorld.Instance == null) return;

            DockingWorld.Instance.ReleaseAssignment(clientId, shipNetworkObjectId);
            if (debugMode) Debug.Log($"[DockingServer] Released assignment for client {clientId}");

            // T-DOCK-09: снимаем флаг IsDocked
            var takingOffShip = FindNetworkObject(shipNetworkObjectId)?.GetComponent<ShipController>();
            if (takingOffShip != null) takingOffShip.ExitDocked();

            SendTakeoffApprovedTargetRpc(clientId, shipNetworkObjectId);
        }

        /// <summary>
        /// Пилот коснулся pad'а (любого). Сервер определяет правильный/чужой
        /// и отвечает DockingStatusDto.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void NotifyTouchedDownRpc(ulong shipNetworkObjectId, string padId, string stationId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (debugMode) Debug.Log($"[DockingServer] NotifyTouchedDownRpc client={clientId} ship={shipNetworkObjectId} pad={padId}");

            if (!CheckRateLimit(clientId)) return;
            if (DockingWorld.Instance == null) return;

            var status = DockingWorld.Instance.ConfirmTouchdown(clientId, shipNetworkObjectId, padId, stationId);
            if (debugMode) Debug.Log($"[DockingServer] Touchdown status: {status.status} client={clientId}");

            // T-DOCK-09: если стыковка успешна — IsDocked = true
            if (status.status == DockingStatus.Docked)
            {
                var dockedShip = FindNetworkObject(shipNetworkObjectId)?.GetComponent<ShipController>();
                if (dockedShip != null) dockedShip.EnterDocked();
            }

            SendDockingStatusTargetRpc(clientId, status);
        }

        // ============================================================
        // TARGET RPCs (server → specific client)
        // ============================================================

        [Rpc(SendTo.SpecifiedInParams)]
        private void SendDockingAssignmentTargetRpc(ulong clientId, DockingAssignmentDto assignment, RpcParams rpcParams = default)
        {
            // Клиент: получить назначение → DockingClientState
            var state = ProjectC.Docking.Client.DockingClientState.Instance;
            if (state != null) state.HandleAssignmentReceived(assignment);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void SendDockingStatusTargetRpc(ulong clientId, DockingStatusDto status, RpcParams rpcParams = default)
        {
            var state = ProjectC.Docking.Client.DockingClientState.Instance;
            if (state != null) state.HandleStatusReceived(status);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void SendTakeoffApprovedTargetRpc(ulong clientId, ulong shipNetId, RpcParams rpcParams = default)
        {
            var state = ProjectC.Docking.Client.DockingClientState.Instance;
            if (state != null) state.HandleTakeoffApproved(shipNetId);
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private static DockingAssignmentDto MakeFail(string reason, ulong shipNetId) =>
            new DockingAssignmentDto
            {
                success = false,
                failReason = reason,
                shipNetworkObjectId = shipNetId
            };

        private static NetworkObject FindNetworkObject(ulong networkObjectId)
        {
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.SpawnManager == null) return null;
            return NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var no) ? no : null;
        }
    }
}
