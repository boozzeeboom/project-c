// T-Q02: FactionDefinition ScriptableObject.
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.3.1 и §7.4 (пример GuildOfThoughts).
//
// RepTier — POCO struct (не enum) потому что дизайнеру нужно гибко настраивать
// пороги per-faction. Хранится прямо в FactionDefinition — нет нужды в
// отдельном ReputationDefinition SO для v1 (см. TODO ниже).

using System;
using UnityEngine;

namespace ProjectC.Factions
{
    /// <summary>
    /// Default inter-faction attitude (used when no NpcAttitude is recorded yet).
    /// Drives initial dialog tone and which edges are visible.
    /// </summary>
    public enum FactionAttitude
    {
        /// <summary>Hostile by default — many edges greyed, some NPC flee.</summary>
        Hostile = 0,
        /// <summary>Neutral default — standard edges visible.</summary>
        Neutral = 1,
        /// <summary>Friendly default — bonus edges / vendor prices unlocked.</summary>
        Friendly = 2
    }

    /// <summary>
    /// A single reputation threshold. Tier label becomes the badge text in UI
    /// (DialogWindow header, CharacterWindow reputation list, QuestTracker).
    /// Value is the lower bound — first tier whose <c>value &lt;= reputation</c> wins.
    /// </summary>
    [Serializable]
    public class ReputationTier
    {
        [Tooltip("Отображаемое имя tier'а (например: 'Недруг', 'Друг', 'Уважаемый')")]
        public string tier;

        [Tooltip("Нижняя граница значения репутации, при которой tier активен (включительно)")]
        public int value;

        [Tooltip("Цвет badge'а в UI (для рендера в DialogWindow/CharacterWindow)")]
        public Color color = Color.white;

        [Tooltip("USS класс для badge'а (например 'rep-negative', 'rep-positive', 'rep-neutral')")]
        public string ussClass = "rep-neutral";
    }

    /// <summary>
    /// Lore faction for Project C (replaces the v1 NpcFaction runtime usage).
    /// Each <c>FactionId</c> value should have exactly one <see cref="FactionDefinition"/>
    /// asset; the Quest Database Explorer (T-Q09) indexes assets by <c>factionId</c>.
    /// </summary>
    /// <remarks>
    /// TODO: split out a separate <c>ReputationDefinition : SO</c> if per-faction
    /// decay / tier scaling grows beyond what one asset can hold comfortably.
    /// For v1 a single asset per faction is enough — keeps the count low and
    /// edit-friendly. Migration path is non-breaking (just move <c>reputationThresholds</c>
    /// into the new SO and reference it).
    /// </remarks>
    [CreateAssetMenu(fileName = "Faction_", menuName = "ProjectC/Factions/Faction Definition", order = 110)]
    public class FactionDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Enum key, должен совпадать с одним из значений ProjectC.Factions.FactionId")]
        public FactionId factionId = FactionId.None;

        [Tooltip("Отображаемое имя (loc key в будущем, пока — литерал)")]
        public string displayName = "";

        [Header("Visuals")]
        [Tooltip("Цвет faction badge'а (для DialogWindow header, QuestTracker)")]
        public Color color = Color.white;

        [Tooltip("Иконка фракции (24x24, для UI списков)")]
        public Sprite iconSprite;

        [Header("Lore")]
        [Tooltip("Краткое описание фракции (для tooltip в UI / GDD cross-ref)")]
        [TextArea(2, 5)]
        public string loreDescription = "";

        [Header("Defaults")]
        [Tooltip("Начальное отношение фракции к новому игроку (если нет recorded reputation)")]
        public FactionAttitude defaultAttitude = FactionAttitude.Neutral;

        [Tooltip("Пороги tier'ов репутации. Должны быть отсортированы по возрастанию value. " +
                 "Первый tier, чей value <= reputation, побеждает (lower-bound inclusive).")]
        public ReputationTier[] reputationThresholds = Array.Empty<ReputationTier>();

        // ============================================================
        // T-FACTION-UNIFY: Combat fields (replaces NpcFaction)
        // ============================================================

        [Header("Combat (T-FACTION-UNIFY)")]
        [Tooltip("Боевое отношение по умолчанию к фракциям, не указанным в combatRelations.")]
        public FactionRelation defaultCombatRelation = FactionRelation.Neutral;

        [Tooltip("Боевые отношения с конкретными фракциями (кто враг, кто союзник).")]
        public FactionCombatRelation[] combatRelations = Array.Empty<FactionCombatRelation>();

        /// <summary>
        /// T-FACTION-UNIFY: ключ для VengeanceMemory (PascalCase, напр. "Bandits").
        /// Используется вместо NpcFaction.factionId (который был lowercase "bandits").
        /// VengeanceMemory runtime-only — пересоздаётся при старте сервера, persisted-ключей нет.
        /// </summary>
        public string CombatKey => factionId.ToString();

        // Runtime cache: FactionId -> FactionRelation
        private System.Collections.Generic.Dictionary<FactionId, FactionRelation> _combatRelationCache;

        /// <summary>
        /// Получить боевое отношение к другой фракции.
        /// </summary>
        public FactionRelation GetCombatRelation(FactionId other)
        {
            if (other == factionId) return FactionRelation.Allied;
            BuildCombatCache();
            if (_combatRelationCache.TryGetValue(other, out var rel))
                return rel;
            return defaultCombatRelation;
        }

        /// <summary>
        /// Проверить, является ли другая фракция враждебной.
        /// </summary>
        public bool IsHostileTowards(FactionId other)
            => GetCombatRelation(other) == FactionRelation.Hostile;

        /// <summary>
        /// Проверить, является ли другая фракция союзной.
        /// </summary>
        public bool IsAlliedWith(FactionId other)
            => GetCombatRelation(other) == FactionRelation.Allied;

        private void BuildCombatCache()
        {
            if (_combatRelationCache != null) return;
            _combatRelationCache = new System.Collections.Generic.Dictionary<FactionId, FactionRelation>();
            foreach (var entry in combatRelations)
                _combatRelationCache[entry.targetFaction] = entry.relation;
        }

        private void OnEnable()
        {
            _combatRelationCache = null; // T-FACTION-UNIFY: очистка при domain reload
        }

        /// <summary>
        /// Найти tier по значению репутации. O(N) — обычно 5-7 tier'ов, не страшно.
        /// </summary>
        public ReputationTier GetTier(int reputation)
        {
            // Идём с конца, чтобы найти самый "высокий" tier, чей value <= rep.
            for (int i = reputationThresholds.Length - 1; i >= 0; i--)
            {
                if (reputation >= reputationThresholds[i].value)
                {
                    return reputationThresholds[i];
                }
            }
            // Ни один не подошёл — отдаём первый (на случай если дизайнер поставил value > 0 у первого).
            return reputationThresholds.Length > 0 ? reputationThresholds[0] : null;
        }
    }
}
