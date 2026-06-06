// =====================================================================================
// ShipKeyServer.cs — серверный hub привязок корабль↔ключ (Project C: The Clouds)
// =====================================================================================
// Документация:
//   • docs/Ships/Key-subsystem/00_OVERVIEW.md
//
// Назначение: NetworkBehaviour, размещается на [ShipKeyServer] GameObject в BootstrapScene
// (рядом с [InventoryServer], [ContractServer], [MarketServer]). Серверный single source
// of truth для связей shipId↔keyItemId. Принимает запросы CanBoard от клиентов, отвечает
// allowed/denied. Прокидывает актуальный реестр на клиентов через PushBindingsRpc.
//
// Паттерн скопирован с InventoryServer (ProjectC.Items.Network):
//   • [Rpc(SendTo.Server, RequireOwnership = true)] для CanBoard запросов
//   • [Rpc(SendTo.Owner)] через NetworkPlayer для доставки ответа
//   • Rate limit per-client (опционально; для MVP — без)
// =====================================================================================

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectC.Items;
using ProjectC.Items.Network;

namespace ProjectC.Ship.Key
{
    [DisallowMultipleComponent]
    public class ShipKeyServer : NetworkBehaviour
    {
        public static ShipKeyServer Instance { get; private set; }

        // ============================================================
        // Server-side state
        // ============================================================

        // shipNetworkObjectId → binding component
        // Заполняется на сервере через ShipKeyBinding.OnNetworkSpawn
        private readonly Dictionary<ulong, ShipKeyBinding> _bindings = new Dictionary<ulong, ShipKeyBinding>();

        // ============================================================
        // Public read-only API (server-only)
        // ============================================================

        /// <summary>Получить ShipKeyBinding по NetworkObjectId корабля. null если не зарегистрирован.</summary>
        public ShipKeyBinding GetBinding(ulong shipNetworkObjectId)
        {
            _bindings.TryGetValue(shipNetworkObjectId, out var b);
            return b;
        }

        /// <summary>Получить keyItemId корабля. -1 если ключ не требуется или корабль не зарегистрирован.</summary>
        public int GetKeyItemId(ulong shipNetworkObjectId)
        {
            var b = GetBinding(shipNetworkObjectId);
            if (b == null) return -1;
            return b.ServerKeyItemId;   // резолвит lazy если _keyItemData ещё не разрезолвнут
        }

        /// <summary>
        /// Server-only: авторитетная проверка "может ли игрок сесть в корабль".
        /// Используется клиентом (через RPC) и сервером (defense in depth в NetworkPlayer).
        /// </summary>
        public bool CanPlayerBoard(ulong clientId, ulong shipNetworkObjectId)
        {
            if (!IsServer) return false;

            var binding = GetBinding(shipNetworkObjectId);
            if (binding == null)
            {
                // Корабль без ShipKeyBinding = без блокировки. Это "unknown ship" —
                // разрешаем по умолчанию, чтобы не сломать уже-рабочие сцены без key-связей.
                return true;
            }

            int keyItemId = binding.ServerKeyItemId;
            if (keyItemId <= 0)
            {
                // ShipKeyBinding есть, но _keyItemData == null → ключ не требуется (например, тестовый корабль).
                return true;
            }

            // Серверная проверка наличия ключа в инвентаре игрока.
            if (InventoryWorld.Instance == null)
            {
                // Нет инвентаря = нет игроков = "не пускаем" (нельзя сказать "можно").
                Debug.LogWarning($"[ShipKeyServer] CanPlayerBoard: InventoryWorld.Instance==null " +
                                 $"(client={clientId}, ship={shipNetworkObjectId}). Доступ ЗАПРЕЩЁН.");
                return false;
            }

            bool has = InventoryWorld.Instance.HasItem(clientId, keyItemId);
            return has;
        }

        // ============================================================
        // Registration (вызывается из ShipKeyBinding.OnNetworkSpawn)
        // ============================================================

        public void RegisterBinding(ulong shipNetworkObjectId, ShipKeyBinding binding)
        {
            if (!IsServer) return;
            if (binding == null) return;
            if (_bindings.TryGetValue(shipNetworkObjectId, out var existing) && existing == binding) return; // идемпотентно
            _bindings[shipNetworkObjectId] = binding;
            Debug.Log($"[ShipKeyServer] Registered binding: shipNetId={shipNetworkObjectId}, " +
                      $"displayName='{binding.ShipDisplayName}', keyItemId={binding.ServerKeyItemId}, " +
                      $"keyItemData={(binding.KeyItemData != null ? binding.KeyItemData.name : "<none>")}");
        }

        public void UnregisterBinding(ulong shipNetworkObjectId)
        {
            if (!IsServer) return;
            if (_bindings.Remove(shipNetworkObjectId))
            {
                Debug.Log($"[ShipKeyServer] Unregistered binding: shipNetId={shipNetworkObjectId}");
            }
        }

        // ============================================================
        // CLIENT RPCs
        // ============================================================

        /// <summary>Клиент хочет сесть в корабль. Сервер проверяет наличие ключа.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestCanBoardRpc(ulong shipNetworkObjectId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            bool allowed = CanPlayerBoard(clientId, shipNetworkObjectId);

            // Достаём display name для сообщения
            string reason = "";
            if (!allowed)
            {
                var binding = GetBinding(shipNetworkObjectId);
                string name = binding != null ? binding.ShipDisplayName : $"#{shipNetworkObjectId}";
                int keyId = binding != null ? binding.ServerKeyItemId : -1;
                reason = $"Нет ключа корабля ({name}) [keyItemId={keyId}]";
            }

            // Доставка ответа владельцу через NetworkPlayer.
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            var playerObj = nm.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerObj == null) return;
            var netPlayer = playerObj.GetComponent<ProjectC.Player.NetworkPlayer>();
            if (netPlayer == null) return;
            netPlayer.ReceiveShipKeyCanBoardResponseTargetRpc(shipNetworkObjectId, allowed, reason);

            Debug.Log($"[ShipKeyServer] CanBoard: client={clientId}, ship={shipNetworkObjectId}, " +
                      $"allowed={allowed}{(string.IsNullOrEmpty(reason) ? "" : $", reason='{reason}'")}");
        }

        // ============================================================
        // Lifecycle
        // ============================================================

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (Instance == null) Instance = this;
            // Подписка на подключение клиентов — пушим им биндинги после регистрации кораблей.
            // (ShipKeyBinding.OnNetworkSpawn вызывается на каждой сцене по мере загрузки —
            //  биндинги приходят с задержкой; пушим что есть сейчас + повторно через 1 сек.)
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
                // Для уже-подключённых (host = client 0) — пушим сразу
                if (NetworkManager.Singleton.IsHost) HandleClientConnected(Unity.Netcode.NetworkManager.ServerClientId);
            }
            Debug.Log($"[ShipKeyServer] OnNetworkSpawn. IsServer={IsServer}, existing bindings={_bindings.Count}");
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            }
            _bindings.Clear();
            base.OnNetworkDespawn();
        }

        /// <summary>Вызывается при подключении клиента (и на хост-старте). Пушим реестр биндингов.</summary>
        private void HandleClientConnected(ulong clientId)
        {
            if (!IsServer) return;
            // Небольшая задержка чтобы ShipKeyBinding.OnNetworkSpawn успел отработать для всех кораблей.
            Invoke(nameof(PushBindingsToClient), 0.5f);
            Debug.Log($"[ShipKeyServer] HandleClientConnected: clientId={clientId}, scheduled pushBindings.");
        }

        private void PushBindingsToClient()
        {
            if (!IsServer) return;
            // Соберём все binding'и
            if (_bindings.Count == 0)
            {
                Debug.LogWarning("[ShipKeyServer] PushBindingsToClient: bindings empty — ShipKeyBinding.OnNetworkSpawn ещё не отработал?");
                return;
            }
            int n = _bindings.Count;
            var shipNetIds = new ulong[n];
            var keyItemIds = new int[n];
            var displayNames = new Unity.Collections.FixedString64Bytes[n];
            int idx = 0;
            foreach (var kvp in _bindings)
            {
                shipNetIds[idx] = kvp.Key;
                keyItemIds[idx] = kvp.Value.ServerKeyItemId;
                var name = kvp.Value.ShipDisplayName;
                if (name.Length > 60) name = name.Substring(0, 60);
                displayNames[idx] = new Unity.Collections.FixedString64Bytes(name);
                idx++;
            }
            // Кому пушим: если хост — на server clientId; если dedicated — loop ConnectedClients
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            if (nm.IsHost)
            {
                SendBindingsToClient(Unity.Netcode.NetworkManager.ServerClientId, shipNetIds, keyItemIds, displayNames);
            }
            else
            {
                foreach (var c in nm.ConnectedClientsList)
                {
                    SendBindingsToClient(c.ClientId, shipNetIds, keyItemIds, displayNames);
                }
            }
            Debug.Log($"[ShipKeyServer] PushBindingsToClient: pushed {n} binding(s).");
        }

        private void SendBindingsToClient(ulong clientId, ulong[] shipNetIds, int[] keyItemIds, Unity.Collections.FixedString64Bytes[] displayNames)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            var playerObj = nm.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerObj == null) return;
            var netPlayer = playerObj.GetComponent<ProjectC.Player.NetworkPlayer>();
            if (netPlayer == null) return;
            netPlayer.ReceiveShipKeyBindingsTargetRpc(shipNetIds, keyItemIds, displayNames);
        }
    }
}
