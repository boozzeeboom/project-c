// T-DOCK-06: DockingPadTriggerBox — триггерная зона одного docking pad'а.
// Scene-placed как child DockStation. Детектит вход ShipController.
// На клиенте (владельце корабля): отправляет NotifyTouchedDownRpc на DockingServer.
//
// Q4 (принято 2026-06-19): padId и compatibleShipClasses — дизайнер задаёт в Inspector.

using ProjectC.Docking.Network; // DockStationController, DockingServer
using ProjectC.Network;          // NetworkPlayerSpawner
using ProjectC.Player;           // ShipController, NetworkPlayer
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Docking.Stations
{
    [RequireComponent(typeof(BoxCollider))]
    public class DockingPadTriggerBox : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("Должен совпадать с padId из DockPadLayout.PadDefinition.padId")]
        [SerializeField] private string padId = "PAD-001";

        [Header("Compatibility override (если задан — перекрывает PadLayout)")]
        [SerializeField] private ShipFlightClass[] compatibleShipClasses;

        public string PadId => padId;

        private BoxCollider _box;
        private DockStationController _stationController;

        private void Awake()
        {
            _box = GetComponent<BoxCollider>();
            _box.isTrigger = true;

            _stationController = GetComponentInParent<DockStationController>();
            if (_stationController == null)
                Debug.LogError($"[DockingPadTriggerBox:{padId}] no DockStationController in parent", this);
        }

        private void OnTriggerEnter(Collider other)
        {
            var ship = other.GetComponentInParent<ShipController>();
            if (ship == null) return;

            // Только владелец корабля отправляет RPC
            var localPlayer = FindLocalPlayer();
            if (localPlayer == null || !localPlayer.IsOwner) return;

            // Убеждаемся, что это корабль локального игрока
            if (!localPlayer.IsInShip) return;
            if (localPlayer.CurrentShip != ship) return;

            var server = DockingServer.Instance;
            if (server == null || !server.IsSpawned) return;

            server.NotifyTouchedDownRpc(
                ship.NetworkObjectId,
                padId,
                _stationController != null ? _stationController.StationId : ""
            );

            // Debug-лог (важно для QA)
            Debug.Log(
                $"[DockingPadTriggerBox:{padId}] OnTriggerEnter — ship={ship.name} " +
                $"owner={localPlayer.OwnerClientId}, sent NotifyTouchedDownRpc");
        }

        private static NetworkPlayer FindLocalPlayer()
        {
            var players = FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Exclude);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null || !players[i].IsOwner) continue;
                if (players[i].GetComponent<NetworkPlayerSpawner>() != null) continue;
                return players[i];
            }
            return null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var col = GetComponent<BoxCollider>();
            if (col == null) return;
            Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.3f);
            Gizmos.DrawWireCube(transform.position, col.size);
        }
#endif
    }
}
