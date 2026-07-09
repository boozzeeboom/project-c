// =====================================================================================
// GatheringServer.cs — server-side NetworkBehaviour hub для сбора ресурсов (T-G03)
// =====================================================================================
// Документация:
//   • docs/Mining/00_OVERVIEW.md
//   • docs/Mining/10_DESIGN.md §1.3
//   • docs/Mining/ROADMAP.md T-G03
//
// Назначение: Singleton-серверный NetworkBehaviour. Принимает RPC от клиентов на
// старт/отмену сбора, тикает активные сборы каждые 0.5 сек, отправляет результаты
// (InProgress/Completed/Interrupted) через TargetRPC на NetworkPlayer клиента.
//
// Поток:
//   1. Клиент нажал F → ResourceNode.OnMetaAccessAllowed → GatheringClientState.RequestStartGather
//      → RequestStartGatherRpc (этот файл, server) → TryStartGather на ResourceNode
//   2. Сервер: TryStartGather → _state=Occupied → register в _activeGathers
//   3. Update (server) каждые 0.5 сек: tick все active jobs → node.TickGather → результат
//   4. SendGatherResultTargetRpc на NetworkPlayer клиента → GatheringClientState
//
// Размещение: scene-placed в BootstrapScene (создадим в T-G07). DontDestroyOnLoad.
//
// MVP-граница (T-G03):
//   - Rate limit: 10 ops/min/client (настраиваемо)
//   - Cooldown tick: раз в секунду (Depleted → Idle)
//   - Нет persistence (state сервера сбрасывается при рестарте — post-MVP)
//   - Нет мультиплеера на одном узле (один активный сбор на сервере)
// =====================================================================================

using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectC.Core;
using ProjectC.Stats;
using NetworkPlayer = ProjectC.Player.NetworkPlayer;

namespace ProjectC.ResourceNode
{
    [RequireComponent(typeof(NetworkObject))]
    public class GatheringServer : NetworkBehaviour
    {
        public static GatheringServer Instance { get; private set; }

        [Header("Tick")]
        [Tooltip("Интервал тика активных сборов (сек). 0.5 = каждые 500ms.")]
        [SerializeField] private float _tickInterval = 0.5f;

        [Tooltip("Интервал проверки cooldown (Depleted → Idle), в секундах. 1.0 = раз в секунду.")]
        [SerializeField] private float _cooldownCheckInterval = 1.0f;

        [Header("Rate Limiting")]
        [Tooltip("Макс RPC-запросов на клиента в минуту (0 = без лимита).")]
        [SerializeField] private int _maxOpsPerMinute = 30;

        [Header("Debug")]
        [SerializeField] private bool _debugMode = true;

        // ==========================================================
        // Server-only state
        // ==========================================================

        // netId → ResourceNode (registry)
        private readonly Dictionary<ulong, ResourceNode> _nodes = new Dictionary<ulong, ResourceNode>(32);

        // clientId → active job
        private struct ActiveGatherJob
        {
            public ulong NodeNetId;
            public ulong ClientId;
        }
        private readonly Dictionary<ulong, ActiveGatherJob> _activeGathers = new Dictionary<ulong, ActiveGatherJob>(8);

        // Per-client rate limiting
        private readonly Dictionary<ulong, List<float>> _opTimestamps = new Dictionary<ulong, List<float>>();

        // Tick timing
        private float _nextTickTime;
        private float _nextCooldownCheckTime;

        // ==========================================================
        // Lifecycle
        // ==========================================================

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (Instance == null) Instance = this;

            if (!IsServer)
            {
                enabled = false; // server-only
                return;
            }

            // AUDIT_2026-07-12 CRITICAL 3: disconnect cleanup — освобождаем узел при отключении клиента
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

            if (_debugMode) Debug.Log("[GatheringServer] OnNetworkSpawn — IsServer=true, tickInterval=" + _tickInterval + "s");
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (IsServer)
            {
                // AUDIT_2026-07-12 CRITICAL 3: unhook disconnect callback
                if (NetworkManager != null)
                    NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
                _activeGathers.Clear();
                _nodes.Clear();
                _opTimestamps.Clear();
            }
            if (Instance == this) Instance = null;
        }

        // ==========================================================
        // Server Update — tick active gathers + cooldown
        // ==========================================================

        private void Update()
        {
            if (!IsServer) return;

            float now = Time.realtimeSinceStartup; // единые часы с _gatherStartServerTime

            // Tick active gathers
            if (now >= _nextTickTime)
            {
                _nextTickTime = now + _tickInterval;
                TickActiveGathers(now);
            }

            // Tick cooldown (Depleted → Idle)
            if (now >= _nextCooldownCheckTime)
            {
                _nextCooldownCheckTime = now + _cooldownCheckInterval;
                TickCooldowns(now);
            }
        }

        private void TickActiveGathers(float now)
        {
            // Copy keys to list to avoid modification during iteration
            var clientIds = new List<ulong>(_activeGathers.Keys);
            for (int i = 0; i < clientIds.Count; i++)
            {
                ulong clientId = clientIds[i];
                if (!_activeGathers.TryGetValue(clientId, out var job)) continue;
                if (!_nodes.TryGetValue(job.NodeNetId, out var node)) continue;
                if (node == null)
                {
                    // Node despawned — cancel
                    _activeGathers.Remove(clientId);
                    continue;
                }

                var result = node.TickGather(now);
                switch (result.Type)
                {
                    case GatherTickResult.ResultType.InProgress:
                        SendGatherResultToClient(clientId, GatherResult.InProgress(result.Progress));
                        break;
                    case GatherTickResult.ResultType.Completed:
                        // ResourceNode уже переключил _replicatedState на Idle/Depleted.
                        // Если Depleted — серверный tick уберёт сбор из active и начнёт кулдаун.
                        // Если просто _currentHarvests++ < _maxHarvests — возвращаем в Idle, новый F может начать новый сбор.
                        _activeGathers.Remove(clientId);
                        SendGatherResultToClient(clientId, GatherResult.Completed(result.ItemName, result.Quantity, result.IsDepleted));

                        // AUDIT_2026-07-12 CRITICAL 2, 4: публикуем MiningCompletedEvent через WorldEventBus
                        // StatsServer подписан и начислит XP (единый паттерн с Crafting/Exchange/Market)
                        try {
                            WorldEventBus.Publish(new MiningCompletedEvent
                            {
                                PlayerId = clientId,
                                ItemName = result.ItemName,
                                Quantity = result.Quantity,
                                IsDepleted = result.IsDepleted,
                                TimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            });
                        } catch (Exception ex) {
                            if (_debugMode) Debug.LogWarning("[GatheringServer] Failed to publish MiningCompletedEvent: " + ex.Message);
                        }
                        if (_debugMode) Debug.Log("[GatheringServer] Gather COMPLETED: client=" + clientId + " item=" + result.ItemName + " qty=" + result.Quantity + " depleted=" + result.IsDepleted);
                        break;
                    case GatherTickResult.ResultType.Interrupted:
                        node.CancelGather();
                        _activeGathers.Remove(clientId);
                        SendGatherResultToClient(clientId, GatherResult.Interrupted(result.Message));
                        if (_debugMode) Debug.Log("[GatheringServer] Gather INTERRUPTED: client=" + clientId + " reason=" + result.Message);
                        break;
                }
            }
        }

        private void TickCooldowns(float now)
        {
            // Проходим по всем нодам и проверяем Depleted → Idle
            var nodeNetIds = new List<ulong>(_nodes.Keys);
            for (int i = 0; i < nodeNetIds.Count; i++)
            {
                if (_nodes.TryGetValue(nodeNetIds[i], out var node) && node != null)
                {
                    node.TickCooldown(now);
                }
            }
        }

        // ==========================================================
        // Registry (для ResourceNode.OnNetworkSpawn / OnNetworkDespawn)
        // ==========================================================

        public void RegisterNode(ulong netId, ResourceNode node)
        {
            if (node != null) _nodes[netId] = node;
        }

        public void UnregisterNode(ulong netId)
        {
            _nodes.Remove(netId);
        }

        // ==========================================================
        // RPCs
        // ==========================================================

        /// <summary>Клиент → сервер: запрос на старт сбора на указанном ноде.
        /// Клиент должен предварительно пройти MetaRequirement check (OnAccessAllowed).
        /// Сервер дополнительно проверяет state и distance.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestStartGatherRpc(ulong nodeNetId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            if (!CheckRateLimit(clientId)) return;

            if (_activeGathers.ContainsKey(clientId))
            {
                SendGatherResultToClient(clientId, GatherResult.Denied("Вы уже собираете другой ресурс"));
                return;
            }

            if (!_nodes.TryGetValue(nodeNetId, out var node) || node == null)
            {
                SendGatherResultToClient(clientId, GatherResult.Denied("Ресурс не найден"));
                return;
            }

            // Дополнительная проверка distance (клиент не врёт о дистанции)
            if (!CheckDistance(clientId, node))
            {
                SendGatherResultToClient(clientId, GatherResult.Denied("Слишком далеко от ресурса"));
                return;
            }

            float serverTime = Time.realtimeSinceStartup; // единые часы с TickActiveGathers
            if (!node.TryStartGather(clientId, serverTime))
            {
                // node.CanStartGather() уже провалилась (состояние не Idle, или tool check fail)
                string reason;
                node.CanStartGather(clientId, out reason);
                SendGatherResultToClient(clientId, GatherResult.Denied(reason));
                return;
            }

            _activeGathers[clientId] = new ActiveGatherJob
            {
                NodeNetId = nodeNetId,
                ClientId = clientId,
            };

            // Сразу шлём первый InProgress(0) — клиент знает, что сбор начался
            SendGatherResultToClient(clientId, GatherResult.InProgress(0f));

            if (_debugMode) Debug.Log("[GatheringServer] RequestStartGather OK: client=" + clientId + " node=" + nodeNetId);
        }

        /// <summary>Клиент → сервер: отмена активного сбора.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestCancelGatherRpc(RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!_activeGathers.TryGetValue(clientId, out var job)) return;

            if (_nodes.TryGetValue(job.NodeNetId, out var node) && node != null)
            {
                node.CancelGather();
            }
            _activeGathers.Remove(clientId);
            SendGatherResultToClient(clientId, GatherResult.Cancelled());
            if (_debugMode) Debug.Log("[GatheringServer] RequestCancelGather: client=" + clientId);
        }

        // ==========================================================
        // Senders (server → client TargetRPC)
        // ==========================================================

        private void SendGatherResultToClient(ulong clientId, GatherResult result)
        {
            var netPlayer = FindNetworkPlayer(clientId);
            if (netPlayer == null)
            {
                if (_debugMode) Debug.LogWarning("[GatheringServer] SendGatherResultToClient: no NetworkPlayer for client " + clientId);
                return;
            }
            netPlayer.ReceiveGatherResultTargetRpc(result);
        }

        private NetworkPlayer FindNetworkPlayer(ulong clientId)
        {
            if (NetworkManager == null) return null;
            if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var cc)) return null;
            return cc.PlayerObject != null ? cc.PlayerObject.GetComponent<NetworkPlayer>() : null;
        }

        // ==========================================================
        // Disconnect cleanup (AUDIT_2026-07-12 CRITICAL 3)
        // ==========================================================

        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;

            if (_activeGathers.TryGetValue(clientId, out var job))
            {
                if (_nodes.TryGetValue(job.NodeNetId, out var node) && node != null)
                {
                    node.CancelGather();
                }
                _activeGathers.Remove(clientId);
                if (_debugMode) Debug.Log($"[GatheringServer] Client {clientId} disconnected during gather — cancelled, node released");
            }

            // Clean up rate-limit data
            _opTimestamps.Remove(clientId);
        }

        // ==========================================================
        // Helpers
        // ==========================================================

        private bool CheckDistance(ulong clientId, ResourceNode node)
        {
            if (node == null || node.Config == null) return false;
            var netPlayer = FindNetworkPlayer(clientId);
            if (netPlayer == null) return false;
            float dist = Vector3.Distance(netPlayer.transform.position, node.transform.position);
            return dist <= node.Config.GatherRange + 0.5f; // +0.5 tolerance
        }

        private bool CheckRateLimit(ulong clientId)
        {
            if (_maxOpsPerMinute <= 0) return true;

            if (!_opTimestamps.TryGetValue(clientId, out var list))
            {
                list = new List<float>(8);
                _opTimestamps[clientId] = list;
            }

            // Удаляем старые (старше 60 сек)
            float now = Time.realtimeSinceStartup;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (now - list[i] > 60f) list.RemoveAt(i);
            }

            if (list.Count >= _maxOpsPerMinute)
            {
                if (_debugMode) Debug.LogWarning("[GatheringServer] Rate limit hit for client " + clientId + " (" + list.Count + " ops/min)");
                return false;
            }

            list.Add(now);
            return true;
        }
    }

    // ==========================================================
    // GatherResult DTO (для RPC: server → client)
    // ==========================================================

    public enum GatherResultCode : byte
    {
        InProgress = 0,
        Completed = 1,
        Interrupted = 2,
        Denied = 3,
        Cancelled = 4,
    }

    public struct GatherResult : INetworkSerializable
    {
        public byte code;
        public float progress;     // 0..1 (InProgress)
        public string itemName;    // для Completed
        public int quantity;       // для Completed
        public bool isDepleted;    // для Completed: узел ушёл в Depleted
        public string reason;      // для Interrupted/Denied: human-readable причина

        public GatherResultCode Result => (GatherResultCode)code;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref code);
            s.SerializeValue(ref progress);
            s.SerializeValue(ref quantity);
            s.SerializeValue(ref isDepleted);
            // NGO 2.x null-string pitfall (P16d): force non-null перед SerializeValue
            var n = itemName ?? "";
            var r = reason ?? "";
            s.SerializeValue(ref n);
            if (s.IsReader) itemName = n;
            s.SerializeValue(ref r);
            if (s.IsReader) reason = r;
        }

        public static GatherResult InProgress(float p) => new GatherResult
        {
            code = (byte)GatherResultCode.InProgress,
            progress = Mathf.Clamp01(p),
        };

        public static GatherResult Completed(string name, int qty, bool depleted) => new GatherResult
        {
            code = (byte)GatherResultCode.Completed,
            itemName = name ?? "",
            quantity = qty,
            isDepleted = depleted,
        };

        public static GatherResult Interrupted(string reason) => new GatherResult
        {
            code = (byte)GatherResultCode.Interrupted,
            reason = reason ?? "",
        };

        public static GatherResult Denied(string reason) => new GatherResult
        {
            code = (byte)GatherResultCode.Denied,
            reason = reason ?? "",
        };

        public static GatherResult Cancelled() => new GatherResult
        {
            code = (byte)GatherResultCode.Cancelled,
        };
    }
}
