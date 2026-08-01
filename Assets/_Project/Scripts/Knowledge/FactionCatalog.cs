// Project C: Knowledge System V2
// FactionCatalog: загружает FactionDefinition из Resources и предоставляет lookup.
// Использует существующий ProjectC.Factions.FactionDefinition (не Knowledge-дубликат).
// Design: docs/Character/Knowledges/05_KNOWLEDGE_SYSTEM_V2_RESEARCH_REVIEW.md §4.6

using System.Collections.Generic;
using UnityEngine;
using ProjectC.Factions;

namespace ProjectC.Knowledge
{
    /// <summary>
    /// Singleton-каталог определений фракций. Загружается один раз из Resources/Data/Factions/.
    /// Используется CharacterWindow для отображения фракций во вкладке «Знания».
    /// Замена хардкода FindFactionFallback().
    /// </summary>
    public class FactionCatalog
    {
        public static FactionCatalog Instance { get; private set; }

        private readonly Dictionary<FactionId, FactionDefinition> _byFactionId = new();

        public FactionCatalog()
        {
            if (Instance != null)
            {
                Debug.LogWarning("[FactionCatalog] Replacing existing instance.");
            }
            Instance = this;
            Load();
        }

        public static void Reset() => Instance = null;

        private void Load()
        {
            _byFactionId.Clear();
            var all = Resources.LoadAll<FactionDefinition>("Data/Factions");
            foreach (var def in all)
            {
                if (def == null) continue;
                if (def.factionId == FactionId.None)
                {
                    Debug.LogWarning($"[FactionCatalog] Skipping FactionDefinition '{def.name}' with FactionId.None.");
                    continue;
                }
                if (_byFactionId.ContainsKey(def.factionId))
                {
                    Debug.LogWarning($"[FactionCatalog] Duplicate FactionId {def.factionId} — overwriting with '{def.name}'.");
                }
                _byFactionId[def.factionId] = def;
            }
            if (Debug.isDebugBuild)
                Debug.Log($"[FactionCatalog] Loaded {_byFactionId.Count} faction definitions from Resources/Data/Factions/");
        }

        public bool TryGet(FactionId factionId, out FactionDefinition def)
            => _byFactionId.TryGetValue(factionId, out def);

        public FactionDefinition Get(FactionId factionId)
        {
            _byFactionId.TryGetValue(factionId, out var def);
            return def;
        }

        public string GetDisplayName(FactionId factionId)
        {
            if (TryGet(factionId, out var def) && !string.IsNullOrEmpty(def.displayName))
                return def.displayName;
            return factionId.ToString();
        }

        public Color GetColor(FactionId factionId)
        {
            if (TryGet(factionId, out var def))
                return def.color;
            return new Color(0.5f, 0.5f, 0.5f);
        }

        public int Count => _byFactionId.Count;
    }
}
