// =====================================================================================
// MetaRequirementRegistry.cs — server-side hub требований (Project C: The Clouds)
// =====================================================================================
// Документация:
//   • docs/MetaRequirement/00_OVERVIEW.md
//   • docs/Ships/Key-subsystem/SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md
//
// Назначение: NetworkBehaviour, размещается на [MetaRequirementRegistry] GameObject в
// BootstrapScene (рядом с [InventoryServer], [ContractServer]). Серверный single source
// of truth для реестра требований. Принимает RequestCanUse от клиентов, отвечает
// allowed/denied + reason. Прокидывает актуальный реестр на клиентов через Push.
//
// Паттерн скопирован с ShipKeyServer (ProjectC.Ship.Key), но generic — работает с
// любым MetaRequirement (корабль, дверь, блок, NPC и т.д.).
// =====================================================================================

using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using ProjectC.Items;
using ProjectC.Player;

namespace ProjectC.MetaRequirement
{
    [DisallowMultipleComponent]
    public class MetaRequirementRegistry : NetworkBehaviour
    {
        public static MetaRequirementRegistry Instance { get; private set; }

        // interactableNetId → MetaRequirement
        private readonly Dictionary<ulong, MetaRequirement> _requirements = new Dictionary<ulong, MetaRequirement>();

        // ===========================================================
        // Public read-only API (server-only)
        // ===========================================================

        public MetaRequirement GetRequirement(ulong netId)
        {
            _requirements.TryGetValue(netId, out var r);
            return r;
        }

        /// <summary>Server-only: авторитетная проверка доступа. Удобный wrapper для
        /// NetworkPlayer.Submit*Rpc (defense in depth на сервере).</summary>
        public bool CanPlayerUse(ulong clientId, ulong netId)
        {
            if (!IsServer) return false;
            var req = GetRequirement(netId);
            if (req == null)
            {
                // Нет MetaRequirement = "unknown interactable". Разрешаем по умолчанию
                // (тот же паттерн что в ShipKeyServer), чтобы не сломать обычные объекты.
                return true;
            }
            return req.CanPlayerUse(clientId, out _);
        }

        // ===========================================================
        // Registration (вызывается из MetaRequirement.OnNetworkSpawn)
        // ===========================================================

        public void RegisterRequirement(ulong netId, MetaRequirement req)
        {
            if (!IsServer) return;
            if (req == null) return;
            if (_requirements.TryGetValue(netId, out var existing) && existing == req) return;
            _requirements[netId] = req;

            string itemsDump = "<empty>";
            if (req.ServerItemIds != null && req.ServerItemIds.Length > 0)
            {
                var sb = new System.Text.StringBuilder("[");
                for (int i = 0; i < req.ServerItemIds.Length; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(req.ServerItemIds[i]);
                }
                sb.Append("]");
                itemsDump = sb.ToString();
            }
            Debug.Log($"[MetaRequirementRegistry] Registered requirement: netId={netId}, " +
                      $"displayName='{req.InteractableDisplayName}', logic={req.Logic}, itemIds={itemsDump}");
        }

        public void UnregisterRequirement(ulong netId)
        {
            if (!IsServer) return;
            if (_requirements.Remove(netId))
            {
                Debug.Log($"[MetaRequirementRegistry] Unregistered requirement: netId={netId}");
            }
        }

        // ===========================================================
        // CLIENT → SERVER RPC
        // ===========================================================

        /// <summary>Клиент хочет использовать interactable. Сервер проверяет требования и
        /// отвечает allowed/denied. Ответ доставляется через NetworkPlayer.ReceiveMetaRequirementResponseTargetRpc.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestCanUseRpc(ulong interactableNetworkObjectId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            var req = GetRequirement(interactableNetworkObjectId);

            bool allowed;
            string reason;
            if (req == null)
            {
                // Без MetaRequirement = "нечего проверять". Разрешаем (default behaviour).
                allowed = true;
                reason = "";
            }
            else
            {
                allowed = req.CanPlayerUse(clientId, out reason);
            }

            // Доставка ответа владельцу через NetworkPlayer.
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            var playerObj = nm.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerObj == null) return;
            var netPlayer = playerObj.GetComponent<NetworkPlayer>();
            if (netPlayer == null) return;
            netPlayer.ReceiveMetaRequirementResponseTargetRpc(interactableNetworkObjectId, allowed, reason ?? "");

            Debug.Log($"[MetaRequirementRegistry] CanUse: client={clientId}, obj={interactableNetworkObjectId}, " +
                      $"allowed={allowed}{(string.IsNullOrEmpty(reason) ? "" : $", reason='{reason}'")}");
        }

        // ===========================================================
        // Lifecycle
        // ===========================================================

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (Instance == null) Instance = this;
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
                if (NetworkManager.Singleton.IsHost) HandleClientConnected(NetworkManager.ServerClientId);
            }
            Debug.Log($"[MetaRequirementRegistry] OnNetworkSpawn. IsServer={IsServer}, existing requirements={_requirements.Count}");
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            }
            _requirements.Clear();
            base.OnNetworkDespawn();
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (!IsServer) return;
            // Задержка чтобы MetaRequirement.OnNetworkSpawn успел отработать для всех
            // interactable'ов в загруженных сценах.
            Invoke(nameof(PushRequirementsToClient), 0.5f);
        }

        private void PushRequirementsToClient()
        {
            if (!IsServer) return;
            if (_requirements.Count == 0)
            {
                // Не warning — у нас в WorldScene_0_0 пока могут быть только корабли
                // (которые регистрируются в ShipKeyServer, не здесь). MetaRequirement'ы
                // появятся когда мы добавим блоки.
                return;
            }
            int n = _requirements.Count;
            var netIds = new ulong[n];
            var displayNames = new FixedString64Bytes[n];
            var itemIdsArr = new int[n][];
            var logics = new byte[n];
            var counts = new int[n];
            var consumes = new bool[n];
            int idx = 0;
            foreach (var kvp in _requirements)
            {
                netIds[idx] = kvp.Key;
                var req = kvp.Value;
                var name = req.InteractableDisplayName;
                if (name.Length > 60) name = name.Substring(0, 60);
                displayNames[idx] = new FixedString64Bytes(name);
                itemIdsArr[idx] = req.ServerItemIds ?? System.Array.Empty<int>();
                logics[idx] = (byte)req.Logic;
                counts[idx] = req.RequiredCount;
                consumes[idx] = req.ConsumeOnUse;
                idx++;
            }

            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            if (nm.IsHost)
            {
                SendToClient(NetworkManager.ServerClientId, netIds, displayNames, itemIdsArr, logics, counts, consumes);
            }
            else
            {
                foreach (var c in nm.ConnectedClientsList)
                {
                    SendToClient(c.ClientId, netIds, displayNames, itemIdsArr, logics, counts, consumes);
                }
            }
            Debug.Log($"[MetaRequirementRegistry] PushRequirementsToClient: pushed {n} requirement(s).");
        }

        private void SendToClient(ulong clientId,
            ulong[] netIds, FixedString64Bytes[] displayNames, int[][] itemIdsArr,
            byte[] logics, int[] counts, bool[] consumes)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            var playerObj = nm.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerObj == null) return;
            var netPlayer = playerObj.GetComponent<NetworkPlayer>();
            if (netPlayer == null) return;
            // Раскрываем int[][] в плоский int[] + offsets через отдельный wrapper-метод
            netPlayer.ReceiveMetaRequirementBindingsTargetRpc(netIds, displayNames,
                itemIdsArr, logics, counts, consumes);
        }
    }
}
