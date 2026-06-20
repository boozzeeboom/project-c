// T-DOCK-00: Stub-классы (DockStationDefinition SO + DockPadLayout SO + DispatcherVoiceLines SO)
// Реальные определения (для T-DOCK-04 + T-DOCK-06).
// Эти stub'ы дают DockingWorld.cs компилироваться. Будут расширены в T-DOCK-04.

using System.Collections.Generic;
using ProjectC.Player;
using UnityEngine;

namespace ProjectC.Docking.Core
{
    /// <summary>
    /// Стилевой набор фраз диспетчера (stub в T-DOCK-00, заполнится в T-DOCK-11).
    /// </summary>
    [CreateAssetMenu(fileName = "DispatcherVoiceLines_", menuName = "ProjectC/Docking/DispatcherVoiceLines", order = 102)]
    public class DispatcherVoiceLines : ScriptableObject
    {
        [System.Serializable]
        public class PhraseSet
        {
            public string context;
            [TextArea] public string[] lines;
        }

        [SerializeField] private List<PhraseSet> phraseSets = new List<PhraseSet>();

        public string GetRandomLine(string context)
        {
            if (phraseSets == null) return string.Empty;
            foreach (var set in phraseSets)
            {
                if (set != null && set.context == context && set.lines != null && set.lines.Length > 0)
                {
                    return set.lines[Random.Range(0, set.lines.Length)];
                }
            }
            return string.Empty;
        }

        public bool HasContext(string context)
        {
            if (phraseSets == null) return false;
            foreach (var set in phraseSets) if (set != null && set.context == context) return true;
            return false;
        }
    }

    /// <summary>
    /// Layout pads на станции (stub в T-DOCK-00, расширится в T-DOCK-04).
    /// </summary>
    [CreateAssetMenu(fileName = "DockPadLayout_", menuName = "ProjectC/Docking/DockPadLayout", order = 101)]
    public class DockPadLayout : ScriptableObject
    {
        [System.Serializable]
        public class PadDefinition
        {
            public string padId = "PAD-001";
            public Vector3 localPosition;
            public Vector3 localEulerAngles;
            public ShipFlightClass[] compatibleShipClasses = { ShipFlightClass.Light };
            public Vector3 triggerBoxSize = new Vector3(8f, 3f, 8f);
        }

        [SerializeField] private List<PadDefinition> pads = new List<PadDefinition>();
        [SerializeField] private Vector3 defaultTriggerBoxSize = new Vector3(8f, 3f, 8f);

        public IReadOnlyList<PadDefinition> Pads => pads;
        public Vector3 DefaultTriggerBoxSize => defaultTriggerBoxSize;
    }

    /// <summary>
    /// Паспорт станции (stub в T-DOCK-00, расширится в T-DOCK-04).
    /// </summary>
    [CreateAssetMenu(fileName = "DockStation_", menuName = "ProjectC/Docking/DockStationDefinition", order = 100)]
    public class DockStationDefinition : ScriptableObject
    {
        [SerializeField] private string stationId = "";
        [SerializeField] private string locationId = "";
        [SerializeField] private string displayName = "";
        [SerializeField] private DockPadLayout padLayout;
        [SerializeField] private DispatcherVoiceLines voiceLines;
        [SerializeField] private float landingWindowSeconds = 90f;

        public string StationId => stationId;
        public string LocationId => locationId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? stationId : displayName;
        public DockPadLayout PadLayout => padLayout;
        public DispatcherVoiceLines VoiceLines => voiceLines;
        public float LandingWindowSeconds => landingWindowSeconds;
    }
}
