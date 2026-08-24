// Project C: Knowledge System V2
// FactionCatalog: загружает FactionDefinition из Resources и предоставляет lookup.
// Использует существующий ProjectC.Factions.FactionDefinition (не Knowledge-дубликат).
// Design: docs/Character/Knowledges/05_KNOWLEDGE_SYSTEM_V2_RESEARCH_REVIEW.md §4.6

using System;
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
        private readonly Dictionary<string, FactionDefinition> _byFactionKey = new(StringComparer.Ordinal);
        private readonly Dictionary<int, FactionDefinition> _byWireId = new();
        private readonly List<FactionDefinition> _all = new();

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
            _byFactionKey.Clear();
            _byWireId.Clear();
            _all.Clear();

            var all = Resources.LoadAll<FactionDefinition>("Data/Factions");
            foreach (var def in all)
            {
                if (def == null) continue;

                string key = def.EffectiveFactionKey;
                int wireId = def.EffectiveWireId;

                if (string.IsNullOrWhiteSpace(key))
                {
                    Debug.LogError($"[FactionCatalog] FactionDefinition '{def.name}' has no factionKey or legacy factionId.");
                    continue;
                }

                if (wireId <= 0 || wireId > byte.MaxValue)
                {
                    Debug.LogError($"[FactionCatalog] FactionDefinition '{def.name}' has invalid wireId {wireId}. Expected 1..255.");
                    continue;
                }

                if (def.wireId > 0 && def.factionId != FactionId.None && def.wireId != (int)def.factionId)
                {
                    Debug.LogError($"[FactionCatalog] FactionDefinition '{def.name}' has wireId {def.wireId} inconsistent with legacy factionId {def.factionId}.");
                    continue;
                }

                if (def.wireId > 0 && def.wireId < 16 && def.factionId == FactionId.None)
                {
                    Debug.LogError($"[FactionCatalog] FactionDefinition '{def.name}' uses reserved legacy wireId {def.wireId} without a legacy factionId.");
                    continue;
                }

                if (_byFactionKey.ContainsKey(key))
                {
                    Debug.LogError($"[FactionCatalog] Duplicate factionKey '{key}' in '{def.name}'.");
                    continue;
                }

                if (_byWireId.ContainsKey(wireId))
                {
                    Debug.LogError($"[FactionCatalog] Duplicate wireId {wireId} in '{def.name}'.");
                    continue;
                }

                if (def.factionId != FactionId.None && _byFactionId.ContainsKey(def.factionId))
                {
                    Debug.LogError($"[FactionCatalog] Duplicate legacy factionId {def.factionId} in '{def.name}'.");
                    continue;
                }

                _all.Add(def);
                _byFactionKey.Add(key, def);
                _byWireId.Add(wireId, def);
                if (def.factionId != FactionId.None)
                    _byFactionId.Add(def.factionId, def);
            }

            if (Debug.isDebugBuild)
                Debug.Log($"[FactionCatalog] Loaded {_all.Count} faction definitions from Resources/Data/Factions/");
        }

        public bool TryGet(FactionId factionId, out FactionDefinition def)
            => _byFactionId.TryGetValue(factionId, out def);

        public FactionDefinition Get(FactionId factionId)
        {
            _byFactionId.TryGetValue(factionId, out var def);
            return def;
        }

        public bool TryGetByFactionKey(string factionKey, out FactionDefinition def)
        {
            def = null;
            if (string.IsNullOrWhiteSpace(factionKey)) return false;
            return _byFactionKey.TryGetValue(factionKey.Trim(), out def);
        }

        public FactionDefinition GetByFactionKey(string factionKey)
        {
            TryGetByFactionKey(factionKey, out var def);
            return def;
        }

        public bool TryGetByWireId(int wireId, out FactionDefinition def)
            => _byWireId.TryGetValue(wireId, out def);

        public FactionDefinition GetByWireId(int wireId)
        {
            _byWireId.TryGetValue(wireId, out var def);
            return def;
        }

        public IReadOnlyList<FactionDefinition> Definitions => _all;

        public string GetDisplayName(FactionId factionId)
        {
            if (TryGet(factionId, out var def) && !string.IsNullOrEmpty(def.displayName))
                return def.displayName;
            return factionId.ToString();
        }

        public string GetDisplayName(int wireId)
        {
            if (TryGetByWireId(wireId, out var def) && !string.IsNullOrEmpty(def.displayName))
                return def.displayName;
            return wireId.ToString();
        }

        public Color GetColor(FactionId factionId)
        {
            if (TryGet(factionId, out var def))
                return def.color;
            return new Color(0.5f, 0.5f, 0.5f);
        }

        public Color GetColor(int wireId)
        {
            if (TryGetByWireId(wireId, out var def))
                return def.color;
            return new Color(0.5f, 0.5f, 0.5f);
        }

        public int Count => _all.Count;
    }
}
