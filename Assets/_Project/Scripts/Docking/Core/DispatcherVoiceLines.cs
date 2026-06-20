// T-DOCK-04: DispatcherVoiceLines — обособленный SO.
// Вынесен из DockingDefinitions.cs (T-DOCK-13c fix: один класс = один .cs файл,
// иначе Unity 6 не создаёт MonoScript для второго/третьего класса).

using System.Collections.Generic;
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
}
