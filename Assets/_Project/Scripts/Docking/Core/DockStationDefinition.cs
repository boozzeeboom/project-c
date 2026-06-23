// T-DOCK-04 (refactored): DockStationDefinition — обособленный SO.
// PadLayout удалён: пады читаются из DockingPadTriggerBox в сцене.

using UnityEngine;

namespace ProjectC.Docking.Core
{
    /// <summary>
    /// Паспорт станции. Содержит identity, geometry, voice lines.
    /// Pad'ы — из DockingPadTriggerBox в сцене.
    /// </summary>
    [CreateAssetMenu(fileName = "DockStation_", menuName = "ProjectC/Docking/DockStationDefinition", order = 100)]
    public class DockStationDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string stationId = "";      // "STN-PRM-001"
        [SerializeField] private string locationId = "";     // "PRIMIUM" — синк с MarketZone.LocationId
        [SerializeField] private string displayName = "";    // "Док-станция Примум"

        [Header("Geometry")]
        [SerializeField] private Vector3 platformCenter = Vector3.zero;
        [SerializeField, Tooltip("Из GDD-10 §2.2 — высота города (например 4348 для Примум).")]
        private float platformAltitude = 4348f;

        [Header("Dispatcher")]
        [SerializeField] private DispatcherVoiceLines voiceLines;

        [Header("Limits")]
        [SerializeField, Min(1)] private int maxConcurrentLandings = 1;
        [SerializeField, Min(10f)] private float landingWindowSeconds = 90f;

        public string StationId => stationId;
        public string LocationId => locationId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? stationId : displayName;
        public Vector3 PlatformCenter => platformCenter;
        public float PlatformAltitude => platformAltitude;
        public DispatcherVoiceLines VoiceLines => voiceLines;
        public int MaxConcurrentLandings => maxConcurrentLandings;
        public float LandingWindowSeconds => landingWindowSeconds;
    }
}
