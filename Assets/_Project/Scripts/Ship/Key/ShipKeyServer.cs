// =====================================================================================
// ShipKeyServer.cs — DEPRECATED АЛИАС → ProjectC.MetaRequirement.MetaRequirementRegistry
// =====================================================================================
// Документация:
//   • docs/Ships/Key-subsystem/SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md
//
// Этот файл сохранён для backward-compat со сценами/префабами И с другими скриптами
// (NetworkPlayer.SubmitSwitchModeRpc, где есть второй guard через CanPlayerBoard).
// Поведение: старый API (CanPlayerBoard / RegisterBinding) делегирует в новый
// MetaRequirementRegistry, который регистрирует MetaRequirement (в т.ч. наши алиасы
// ShipKeyBinding).
//
// Через 1-2 релиз-цикла: удалить.
// =====================================================================================

using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectC.Items;
using ProjectC.MetaRequirement;
using ProjectC.Player;

namespace ProjectC.Ship.Key
{
    /// <summary>
    /// DEPRECATED: устаревший алиас. Используйте ProjectC.MetaRequirement.MetaRequirementRegistry.
    /// Сохранены публичные методы старого API с делегированием в новый registry,
    /// чтобы не править NetworkPlayer.SubmitSwitchModeRpc и прочие callers.
    /// </summary>
    [Obsolete("Use ProjectC.MetaRequirement.MetaRequirementRegistry. ShipKeyServer kept as alias for backward compat.")]
    [DisallowMultipleComponent]
    public class ShipKeyServer : NetworkBehaviour
    {
        public static ShipKeyServer Instance { get; private set; }

        // Внутри: реальный реестр — MetaRequirementRegistry (тоже NetworkBehaviour).
        // Сцена-placed [ShipKeyServer] GameObject в BootstrapScene — оставлен для совместимости
        // с уже-засетапленным scenes. На OnNetworkSpawn — НЕ создаём свой реестр, а
        // ретранслируем регистрации существующих ShipKeyBinding в MetaRequirementRegistry,
        // когда он появится (через Invoke-цикл).
        private readonly Dictionary<ulong, ShipKeyBinding> _bindings = new Dictionary<ulong, ShipKeyBinding>();
        private bool _pushedOnce = false;

        public ShipKeyBinding GetBinding(ulong shipNetworkObjectId)
        {
            _bindings.TryGetValue(shipNetworkObjectId, out var b);
            return b;
        }

        public int GetKeyItemId(ulong shipNetworkObjectId)
        {
            var b = GetBinding(shipNetworkObjectId);
            if (b == null) return -1;
            // ShipKeyBinding (alias) → MetaRequirement: ServerKeyItemId → ServerItemIds[0]
            var ids = b.ServerItemIds;
            return (ids != null && ids.Length > 0) ? ids[0] : -1;
        }

        /// <summary>Legacy API. Делегирует в MetaRequirementRegistry.</summary>
        public bool CanPlayerBoard(ulong clientId, ulong shipNetworkObjectId)
        {
            if (!IsServer) return false;
            // Сначала пробуем новый registry (если работает)
            if (MetaRequirementRegistry.Instance != null)
            {
                return MetaRequirementRegistry.Instance.CanPlayerUse(clientId, shipNetworkObjectId);
            }
            // Fallback: проверяем наши старые биндинги
            var b = GetBinding(shipNetworkObjectId);
            if (b == null) return true; // legacy: unknown ship → allow
            var keyIds = b.ServerItemIds;
            if (keyIds == null || keyIds.Length == 0 || keyIds[0] <= 0) return true;
            if (InventoryWorld.Instance == null) return false;
            return InventoryWorld.Instance.HasItem(clientId, keyIds[0]);
        }

        public void RegisterBinding(ulong shipNetworkObjectId, ShipKeyBinding binding)
        {
            if (!IsServer) return;
            if (binding == null) return;
            if (_bindings.TryGetValue(shipNetworkObjectId, out var existing) && existing == binding) return;
            _bindings[shipNetworkObjectId] = binding;
            // ShipKeyBinding (alias) → MetaRequirement: ShipDisplayName → InteractableDisplayName,
            // ServerKeyItemId → ServerItemIds[0]
            var aliasIds = binding.ServerItemIds;
            int aliasKeyId = (aliasIds != null && aliasIds.Length > 0) ? aliasIds[0] : -1;
            Debug.Log($"[ShipKeyServer-ALIAS] Registered binding (legacy): shipNetId={shipNetworkObjectId}, " +
                      $"displayName='{binding.InteractableDisplayName}', keyItemId={aliasKeyId}");
        }

        public void UnregisterBinding(ulong shipNetworkObjectId)
        {
            if (!IsServer) return;
            if (_bindings.Remove(shipNetworkObjectId))
            {
                Debug.Log($"[ShipKeyServer-ALIAS] Unregistered binding: shipNetId={shipNetworkObjectId}");
            }
        }

        /// <summary>Legacy API. Делегирует в MetaRequirementRegistry.RequestCanUseRpc.</summary>
        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void RequestCanBoardRpc(ulong shipNetworkObjectId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            bool allowed = CanPlayerBoard(clientId, shipNetworkObjectId);

            string reason = "";
            if (!allowed)
            {
                var b = GetBinding(shipNetworkObjectId);
                string name = b != null ? b.InteractableDisplayName : $"#{shipNetworkObjectId}";
                reason = $"Нет ключа корабля ({name})";
            }

            // Доставка ответа владельцу через NetworkPlayer (старый Target RPC).
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            var playerObj = nm.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerObj == null) return;
            var netPlayer = playerObj.GetComponent<NetworkPlayer>();
            if (netPlayer == null) return;
            netPlayer.ReceiveShipKeyCanBoardResponseTargetRpc(shipNetworkObjectId, allowed, reason);

            Debug.Log($"[ShipKeyServer-ALIAS] CanBoard: client={clientId}, ship={shipNetworkObjectId}, " +
                      $"allowed={allowed}{(string.IsNullOrEmpty(reason) ? "" : $", reason='{reason}'")}");
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (Instance == null) Instance = this;
            // Шлём bindings на клиентов (старый протокол) при подключении — чтобы
            // ShipKeyClientState (legacy) мог продолжать работать.
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
                if (NetworkManager.Singleton.IsHost) HandleClientConnected(NetworkManager.ServerClientId);
            }
            Debug.Log($"[ShipKeyServer-ALIAS] OnNetworkSpawn. IsServer={IsServer}, existing bindings={_bindings.Count}");
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

        private void HandleClientConnected(ulong clientId)
        {
            if (!IsServer) return;
            Invoke(nameof(PushBindingsToClient), 0.5f);
        }

        private void PushBindingsToClient()
        {
            if (!IsServer) return;
            if (_bindings.Count == 0) return;
            if (_pushedOnce) return; // single push — клиент использует только legacy bindings
            _pushedOnce = true;

            int n = _bindings.Count;
            var shipNetIds = new ulong[n];
            var keyItemIds = new int[n];
            var displayNames = new Unity.Collections.FixedString64Bytes[n];
            int idx = 0;
            foreach (var kvp in _bindings)
            {
                shipNetIds[idx] = kvp.Key;
                // ShipKeyBinding (alias) → MetaRequirement: ServerKeyItemId → ServerItemIds[0]
                var aliasIds = kvp.Value.ServerItemIds;
                keyItemIds[idx] = (aliasIds != null && aliasIds.Length > 0) ? aliasIds[0] : -1;
                var name = kvp.Value.InteractableDisplayName;
                if (name.Length > 60) name = name.Substring(0, 60);
                displayNames[idx] = new Unity.Collections.FixedString64Bytes(name);
                idx++;
            }
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            if (nm.IsHost)
            {
                SendBindingsToClient(NetworkManager.ServerClientId, shipNetIds, keyItemIds, displayNames);
            }
            else
            {
                foreach (var c in nm.ConnectedClientsList)
                {
                    SendBindingsToClient(c.ClientId, shipNetIds, keyItemIds, displayNames);
                }
            }
            Debug.Log($"[ShipKeyServer-ALIAS] PushBindingsToClient: pushed {n} binding(s).");
        }

        private void SendBindingsToClient(ulong clientId, ulong[] shipNetIds, int[] keyItemIds, Unity.Collections.FixedString64Bytes[] displayNames)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            var playerObj = nm.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerObj == null) return;
            var netPlayer = playerObj.GetComponent<NetworkPlayer>();
            if (netPlayer == null) return;
            netPlayer.ReceiveShipKeyBindingsTargetRpc(shipNetIds, keyItemIds, displayNames);
        }
    }
}
