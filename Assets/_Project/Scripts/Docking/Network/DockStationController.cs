// T-DOCK-00: Stub DockStationController (полная реализация в T-DOCK-06).
// Этот stub нужен чтобы DockingWorld.cs компилировался.

using UnityEngine;
using ProjectC.Docking.Core;

namespace ProjectC.Docking.Network
{
    /// <summary>
    /// NetworkBehaviour на корне DockStation. Stub в T-DOCK-00.
    /// В T-DOCK-06: добавится [RequireComponent(typeof(NetworkObject))] + ScenePlacedObjectSpawner wiring.
    /// </summary>
    public class DockStationController : MonoBehaviour
    {
        [SerializeField] private DockStationDefinition stationDefinition;

        public DockStationDefinition StationDefinition => stationDefinition;
    }
}
