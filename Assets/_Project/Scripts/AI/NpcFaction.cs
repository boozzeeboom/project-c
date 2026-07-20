// Project C: Real-Time Combat Engine — T-NPC-S19
// NpcFaction: ScriptableObject с factionId и отношениями между фракциями.
// Design: docs/Character/Skills/real-time-combat/npc-enemy/04_UNIFIED_BEHAVIOR_ARCHITECTURE.md §4 T-NPC-S19
//          + 02_SOCIAL_HUMAN_BEHAVIOR.md §2.5.1

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.AI
{
    /// <summary>
    /// Тип отношения между двумя фракциями.
    /// </summary>
    public enum FactionRelation
    {
        /// <summary>Союзники: помогают в бою, не атакуют, делят alarm.</summary>
        Allied,
        /// <summary>Нейтральные: игнорируют, не атакуют, не помогают.</summary>
        Neutral,
        /// <summary>Враждебные: атакуют при обнаружении.</summary>
        Hostile,
    }

    /// <summary>
    /// T-NPC-S19: Фракция NPC.
    /// Определяет factionId и список отношений с другими фракциями.
    /// NPC одной фракции считаются Allied автоматически.
    /// Используется NpcSocialBrain для определения «свой/чужой» и replacement
    /// для NpcGroupController (помощь только Allied).
    /// </summary>
    [Obsolete("Use FactionDefinition instead. T-FACTION-UNIFY")]
    [CreateAssetMenu(fileName = "NpcFaction_", menuName = "Project C/AI/Npc Faction")]
    public class NpcFaction : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Уникальный ID фракции (например: 'bandits', 'guards', 'villagers').")]
        public string factionId = "neutral";

        [TextArea(2, 4)]
        [Tooltip("Описание фракции для дизайнеров.")]
        public string description;

        [Header("Default Attitude")]
        [Tooltip("Отношение по умолчанию к неизвестным фракциям.")]
        public FactionRelation defaultRelation = FactionRelation.Neutral;

        [Header("Relations")]
        [Tooltip("Список отношений с конкретными фракциями.")]
        public List<FactionRelationEntry> relations = new List<FactionRelationEntry>();

        // Runtime cache: factionId -> FactionRelation
        private Dictionary<string, FactionRelation> _relationCache;

        /// <summary>
        /// Получить отношение к другой фракции.
        /// </summary>
        public FactionRelation GetRelation(NpcFaction other)
        {
            if (other == null) return defaultRelation;
            if (other == this) return FactionRelation.Allied; // Одна фракция = союзники.

            BuildCache();
            if (_relationCache.TryGetValue(other.factionId, out var rel))
                return rel;
            return defaultRelation;
        }

        /// <summary>
        /// Получить отношение по factionId (строка).
        /// </summary>
        public FactionRelation GetRelation(string otherFactionId)
        {
            if (otherFactionId == factionId) return FactionRelation.Allied;
            BuildCache();
            if (_relationCache.TryGetValue(otherFactionId, out var rel))
                return rel;
            return defaultRelation;
        }

        /// <summary>
        /// Проверить, является ли другая фракция враждебной.
        /// </summary>
        public bool IsHostile(NpcFaction other)
        {
            return GetRelation(other) == FactionRelation.Hostile;
        }

        /// <summary>
        /// Проверить, является ли другая фракция союзной.
        /// </summary>
        public bool IsAllied(NpcFaction other)
        {
            var rel = GetRelation(other);
            return rel == FactionRelation.Allied;
        }

        /// <summary>
        /// Установить отношение к другой фракции (runtime, не сохраняется в .asset).
        /// </summary>
        public void SetRelation(string otherFactionId, FactionRelation relation)
        {
            BuildCache();
            _relationCache[otherFactionId] = relation;
        }

        private void BuildCache()
        {
            if (_relationCache != null) return;
            _relationCache = new Dictionary<string, FactionRelation>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in relations)
            {
                if (!string.IsNullOrEmpty(entry.factionId))
                    _relationCache[entry.factionId] = entry.relation;
            }
        }

        private void OnEnable()
        {
            _relationCache = null;
        }
    }

    /// <summary>
    /// Запись отношения к конкретной фракции.
    /// </summary>
    [Serializable]
    public struct FactionRelationEntry
    {
        [Tooltip("ID целевой фракции.")]
        public string factionId;

        [Tooltip("Тип отношения.")]
        public FactionRelation relation;
    }
}
