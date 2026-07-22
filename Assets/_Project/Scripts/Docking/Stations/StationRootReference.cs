// T-DOCK-04: StationRootReference — marker MonoBehaviour на любой части DockStation.
// Кеширует DockStationController + NetworkObject от корня. По канону ShipRootReference.
// Паттерн: см. Assets/_Project/Ship/ShipRootReference.cs.
//
// Q4: DockStation — композитный объект (root + OuterCommZone + Pads). Внешние системы
// (DockingWorld, NetworkPlayer) находят DockStationController через этот маркер.

using Unity.Netcode;
using UnityEngine;
using ProjectC.Docking.Network; // DockStationController

namespace ProjectC.Docking.Stations
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class StationRootReference : MonoBehaviour
    {
        public DockStationController StationController { get; private set; }
        public NetworkObject StationNetworkObject { get; private set; }
        public Transform StationRoot => transform.root;

        private void Awake()
        {
            var root = transform.root;
            StationController = root.GetComponent<DockStationController>();
            StationNetworkObject = root.GetComponent<NetworkObject>();
            if (StationController == null)
            {
                // Expected for RepairManager/non-station objects — silent.
                return;
            }
        }
    }
}
