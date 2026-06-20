// T-DOCK-04: DockPadLayout — обособленный SO.
// Вынесен из DockingDefinitions.cs (T-DOCK-13c fix: один класс = один .cs файл).

using System.Collections.Generic;
using ProjectC.Player;
using UnityEngine;

namespace ProjectC.Docking.Core
{
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
}
