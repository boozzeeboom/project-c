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

        /// <summary>T-DOCK-SRV-5: публичный геттер для override совместимости (используется в DockingWorld.AssignPad).</summary>
        public ShipFlightClass[] CompatibleShipClasses => compatibleShipClasses;

        /// <summary>T-DOCK-13 v2.2: true если внутри trigger-бокса есть ShipController (любой).</summary>
        public bool IsShipInside { get; private set; }

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
            // T-PERF08: статическая геометрия города не может быть кораблём.
            // T-PERF14: ShipController гарантирован на корне attachedRigidbody — не сканируем иерархию.
            Rigidbody body = other.attachedRigidbody;
            if (body == null) return;
            var ship = body.GetComponent<ShipController>();
            if (ship == null) return;

            // T-DOCK-13 v2.2: флаг для DockPadVisualMarker
            IsShipInside = true;

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

        private void OnTriggerExit(Collider other)
        {
            Rigidbody body = other.attachedRigidbody;
            if (body == null) return;
            var ship = body.GetComponent<ShipController>();
            if (ship != null)
            {
                // T-DOCK-13 v2.2: снимаем флаг для DockPadVisualMarker
                IsShipInside = false;
            }
        }

        private static NetworkPlayer FindLocalPlayer()
        {
            // T-PERF14: локальный PlayerObject уже доступен в NGO, полный поиск по сцене не нужен.
            var manager = NetworkManager.Singleton;
            if (manager == null || manager.LocalClient == null) return null;
            var playerObject = manager.LocalClient.PlayerObject;
            return playerObject != null ? playerObject.GetComponent<NetworkPlayer>() : null;
        }
    }
}
