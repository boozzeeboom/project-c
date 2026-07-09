// Project C: Character Progression — T-P01 refactor (P4)
// StatDebugConfig: debug logging + distance thresholds + announce tier-up.
// Вынесен из StatsConfig согласно аудиту 12_STATS_ARCHITECTURE_AUDIT_V2.md §4, Q2.6.

using UnityEngine;

namespace ProjectC.Stats
{
    [CreateAssetMenu(fileName = "StatDebugConfig", menuName = "Project C/Stats/Stat Debug Config", order = 13)]
    public class StatDebugConfig : ScriptableObject
    {
        [Header("Distance thresholds (для batched XP)")]
        [Tooltip("Walked distance accumulator threshold (meters).")]
        [SerializeField, Min(1f)] private float _walkDistanceXpThreshold = 1f;

        [Tooltip("Piloted distance accumulator threshold (meters).")]
        [SerializeField, Min(1f)] private float _pilotDistanceXpThreshold = 10f;

        [Header("Track total distance (для ачивок/трекеров)")]
        [SerializeField] private bool _trackTotalDistance = true;

        [Header("Tier-up уведомление")]
        [Tooltip("Показывать toast при tier-up.")]
        [SerializeField] private bool _announceTierUp = true;

        [Header("Debug")]
        [Tooltip("Verbose logging.")]
        [SerializeField] private bool _debugLogging = false;

        public float WalkDistanceXpThreshold   => _walkDistanceXpThreshold;
        public float PilotDistanceXpThreshold  => _pilotDistanceXpThreshold;
        public bool  TrackTotalDistance        => _trackTotalDistance;
        public bool  AnnounceTierUp            => _announceTierUp;
        public bool  DebugLogging              => _debugLogging;
    }
}
