// T-DOCK-04 (refactored): DockStationDefinition — обособленный SO.
// T-DOCK-Editor: Geometry (PlatformCenter, PlatformAltitude) удалены из SO —
//   теперь читаются из transform.position объекта с DockStationController.

using UnityEngine;

namespace ProjectC.Docking.Core
{
    /// <summary>
    /// Паспорт станции. Содержит identity, voice lines, limits.
    /// Geometry (PlatformCenter, PlatformAltitude) — из transform.position
    /// объекта с DockStationController, не из SO.
    /// </summary>
    [CreateAssetMenu(fileName = "DockStation_", menuName = "ProjectC/Docking/DockStationDefinition", order = 100)]
    public class DockStationDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string stationId = "";      // "STN-PRM-001"
        [SerializeField] private string locationId = "";     // "PRIMIUM" — синк с MarketZone.LocationId
        [SerializeField] private string displayName = "";    // "Док-станция Примум"

        [Header("Dispatcher")]
        [SerializeField] private DispatcherVoiceLines voiceLines;

        [Header("Limits")]
        [SerializeField, Min(1)] private int maxConcurrentLandings = 1;
        [SerializeField, Min(10f)] private float landingWindowSeconds = 90f;

        public string StationId => stationId;
        public string LocationId => locationId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? stationId : displayName;
        public DispatcherVoiceLines VoiceLines => voiceLines;
        public int MaxConcurrentLandings => maxConcurrentLandings;
        public float LandingWindowSeconds => landingWindowSeconds;
    }
}
