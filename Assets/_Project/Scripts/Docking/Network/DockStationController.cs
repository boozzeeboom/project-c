// T-DOCK-06: DockStationController — NetworkBehaviour на корне DockStation.
// Scene-placed в WorldScene_X_Z (центр тестовой зоны ~(40500, 2510, 40500)).
// Auto-spawn через ScenePlacedObjectSpawner (как QuestServer, ExchangeServer).
//
// Q4 (принято 2026-06-19): dockStationDefinition SO — без хардкода кол-ва pads.

using ProjectC.Docking.Core;     // DockStationDefinition
using ProjectC.Docking.Stations; // PadStateSync
using ProjectC.Docking.Zones;    // OuterCommZone
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Docking.Network
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(OuterCommZone))]
    [RequireComponent(typeof(PadStateSync))]
    public class DockStationController : NetworkBehaviour
    {
        [Header("Definition")]
        [Tooltip("Паспорт станции. Должен быть назначен в инспекторе. " +
                 "Создаётся через Assets > Create > ProjectC > Docking > DockStationDefinition.")]
        [SerializeField] private DockStationDefinition dockStationDefinition;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // NetworkBehaviour — не может быть static Instance (multi-station)
        // DockingZoneRegistry держит словарь stationId → DockStationController.

        public DockStationDefinition StationDefinition => dockStationDefinition;

        public string StationId => dockStationDefinition != null ? dockStationDefinition.StationId : "";
        public string LocationId => dockStationDefinition != null ? dockStationDefinition.LocationId : "";
        public string DisplayName => dockStationDefinition != null ? dockStationDefinition.DisplayName : "";

        /// <summary>Центр платформы — позиция этого объекта в мире.</summary>
        public Vector3 PlatformCenter => transform.position;

        /// <summary>Высота платформы — Y-координата этого объекта.</summary>
        public float PlatformAltitude => transform.position.y;

        public bool IsDockStation => true;  // маркер для внешних систем

        private void Awake()
        {
            if (dockStationDefinition == null)
            {
                Debug.LogError(
                    $"[DockStationController:{gameObject.name}] dockStationDefinition is null! " +
                    "Назначь SO в инспекторе.", this);
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            // Регистрация происходит в OuterCommZone.OnEnable (T-DOCK-05).
            if (debugMode)
                Debug.Log($"[DockStationController:{StationId}] OnNetworkSpawn — IsServer={IsServer}, " +
                          $"stationDef={dockStationDefinition != null}");
        }
    }
}
