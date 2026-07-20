// T-FACTION-UNIFY: FactionRelation + FactionCombatRelation for combat AI.
// Extracted into a separate file in ProjectC.Factions namespace so it survives
// the removal of NpcFaction.cs (which also defines FactionRelation in ProjectC.AI).
// 
// See: docs/NPC_quests/Complete_v2/03_FACTION_UNIFICATION_PLAN.md

using System;
using UnityEngine;

namespace ProjectC.Factions
{
    /// <summary>
    /// Тип боевого отношения между двумя фракциями.
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
    /// Запись боевого отношения к конкретной фракции.
    /// Используется в FactionDefinition.combatRelations[].
    /// </summary>
    [Serializable]
    public struct FactionCombatRelation
    {
        [Tooltip("Целевая фракция.")]
        public FactionId targetFaction;

        [Tooltip("Тип боевого отношения.")]
        public FactionRelation relation;
    }
}
