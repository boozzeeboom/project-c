using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Player
{
    /// <summary>
    /// Сетевой компонент игрока
    /// Синхронизирует позицию и состояние игрока между клиентами
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour
    {
        // NetworkObject для этого игрока
        private NetworkObject networkObject;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            networkObject = GetComponent<NetworkObject>();
            
            if (IsOwner)
            {
                Debug.Log($"[Player] Локальный игрок spawned. OwnerClientId: {OwnerClientId}");
            }
            else
            {
                Debug.Log($"[Player] Удалённый игрок spawned. OwnerClientId: {OwnerClientId}");
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            Debug.Log($"[Player] Игрок despawned. OwnerClientId: {OwnerClientId}");
        }

        /// <summary>
        /// Проверка: это локальный игрок?
        /// </summary>
        public new bool IsLocalPlayer => IsOwner;

        /// <summary>
        /// Получить ClientId владельца
        /// </summary>
        public ulong GetOwnerId()
        {
            return OwnerClientId;
        }
    }
}
