// T-DOCK-04: расширенные SOs (DockStationDefinition, DockPadLayout, DispatcherVoiceLines).
// Канон: см. Assets/_Project/Quests/NpcDefinition.cs, ItemData.cs, и др. SO в проекте.
//
// Q3 (принято 2026-06-19): без хардкода кол-ва pads — дизайнер сам расставляет.
// Q4 (принято 2026-06-19): soft-limit ≤10 на класс в OnValidate (warning, не блок).
// Q7 (принято 2026-06-19): добавлены контексты AwaitingConfirmation / WrongPad / etc.

using System.Collections.Generic;
using ProjectC.Player;
using UnityEngine;

namespace ProjectC.Docking.Core
{
    /// <summary>
    /// Набор фраз диспетчера. Используется в CommPanel (T-DOCK-07) и
    /// dispatcher флоу (T-DOCK-01).
    /// </summary>
    [CreateAssetMenu(fileName = "DispatcherVoiceLines_", menuName = "ProjectC/Docking/DispatcherVoiceLines", order = 102)]
    public class DispatcherVoiceLines : ScriptableObject
    {
        [System.Serializable]
        public class PhraseSet
        {
            [Tooltip("Context key: Greeting / Assigning / AssignedLight / AssignedMedium / AssignedHeavy / AwaitingConfirmation / Touchdown / Takeoff / Goodbye / WindowExpired / Occupied / WrongPad.")]
            public string context;
            [TextArea(2, 4)]
            public string[] lines;
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
    /// Layout pads на станции. Один SO переиспользуется многими DockStation (если они одинаковые)
    /// или у каждой станции свой (если разные). Q4 — без хардкода кол-ва.
    /// </summary>
    [CreateAssetMenu(fileName = "DockPadLayout_", menuName = "ProjectC/Docking/DockPadLayout", order = 101)]
    public class DockPadLayout : ScriptableObject
    {
        [System.Serializable]
        public class PadDefinition
        {
            [Tooltip("Stable ID для синхронизации и сериализации. Должен быть уникален в рамках layout. " +
                     "Дизайнер пишет 'PAD-001'..'PAD-N' — это отображается на самом pad'е (Q13: цифры на mesh'е).")]
            public string padId = "PAD-001";

            [Tooltip("Локальная позиция относительно DockStation root. Заполняется на сцене, не в SO.")]
            public Vector3 localPosition;

            [Tooltip("Вращение pad (обычно forward в центр станции).")]
            public Vector3 localEulerAngles;

            [Tooltip("Какие классы кораблей могут сесть на этот pad. Пустой массив = совместим со ВСЕМИ классами. " +
                     "Типичный вариант: [Light] или [Light, Medium].")]
            public ShipFlightClass[] compatibleShipClasses = { ShipFlightClass.Light };

            [Tooltip("Размер триггерной зоны (overrides global padSize если задан).")]
            public Vector3 triggerBoxSize = new Vector3(8f, 3f, 8f);
        }

        [Header("Pads (любое количество, дизайнер расставляет)")]
        [SerializeField] private List<PadDefinition> pads = new List<PadDefinition>();

        [Header("Default pad geometry (если в PadDefinition triggerBoxSize == zero)")]
        [SerializeField] private Vector3 defaultTriggerBoxSize = new Vector3(8f, 3f, 8f);

        public IReadOnlyList<PadDefinition> Pads => pads;
        public Vector3 DefaultTriggerBoxSize => defaultTriggerBoxSize;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Уникальность padId в рамках layout
            var seen = new HashSet<string>();
            foreach (var p in pads)
            {
                if (p == null || string.IsNullOrEmpty(p.padId)) continue;
                if (!seen.Add(p.padId))
                    Debug.LogError($"[DockPadLayout:{name}] duplicate padId '{p.padId}'", this);
            }

            // Q4: soft-limit ≤10 на класс. Warning, не ошибка.
            var perClass = new Dictionary<ShipFlightClass, int>();
            foreach (var p in pads)
            {
                if (p == null || p.compatibleShipClasses == null) continue;
                foreach (var cls in p.compatibleShipClasses)
                {
                    perClass.TryGetValue(cls, out int n);
                    perClass[cls] = ++n;
                    if (n > 10)
                    {
                        Debug.LogWarning($"[DockPadLayout:{name}] класс {cls} имеет {n} pads (>10). " +
                                         $"Это soft-limit для MVP, не блокирует, но усложняет UI.", this);
                    }
                }
            }
        }
#endif
    }

    /// <summary>
    /// Паспорт станции. Содержит identity, geometry, ссылки на pad layout + voice lines.
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

        [Header("Pads")]
        [Tooltip("Ссылка на общий layout pads (Light/Medium/Heavy slots).")]
        [SerializeField] private DockPadLayout padLayout;

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
        public DockPadLayout PadLayout => padLayout;
        public DispatcherVoiceLines VoiceLines => voiceLines;
        public int MaxConcurrentLandings => maxConcurrentLandings;
        public float LandingWindowSeconds => landingWindowSeconds;
    }
}
