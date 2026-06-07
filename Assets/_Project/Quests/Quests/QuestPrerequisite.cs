// T-Q04: QuestPrerequisite — condition для доступности квеста.
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.3.4 (prerequisites sub-structure).
//
// Заменяет в composite-паттерне T-Q03: единичный atomic prerequisite на квест.
// Несколько prerequisite → QuestDefinition.prerequisites[] (AND на runtime).

using System;
using UnityEngine;
using ProjectC.Factions;

namespace ProjectC.Quests
{
    /// <summary>
    /// Top-level prerequisite types. Все atomic (без composite).
    /// </summary>
    public enum QuestPrerequisiteType : byte
    {
        /// <summary>Quest stringParam is Completed or TurnedIn.</summary>
        QuestCompleted = 0,
        /// <summary>Quest stringParam is in Active state (для квестов-цепочек).</summary>
        QuestActive = 1,
        /// <summary>Reputation с factionParam >= intParam.</summary>
        ReputationAtLeast = 2,
        /// <summary>NpcAttitude с NPC id stringParam >= intParam.</summary>
        NpcAttitudeAtLeast = 3,
        /// <summary>Player has itemTradeItemId in inventory (count >= requiredQuantity).</summary>
        HaveItem = 4,
        /// <summary>Global flag stringParam is set.</summary>
        FlagIsSet = 5,
        /// <summary>Player faction is stringParam (deprecated — usually use ReputationAtLeast).</summary>
        PlayerFaction = 6
    }

    /// <summary>
    /// Single atomic prerequisite. Multiple prerequisites AND-combine на server.
    /// </summary>
    [Serializable]
    public class QuestPrerequisite
    {
        public QuestPrerequisiteType type = QuestPrerequisiteType.QuestCompleted;

        [Tooltip("Primary string param: questId / npcId / itemId / flagId.")]
        public string stringParam = "";

        [Tooltip("Numeric param: reputation / NpcAttitude threshold / item count.")]
        public int intParam = 0;

        [Tooltip("Faction param (для ReputationAtLeast).")]
        public FactionId factionParam = FactionId.None;
    }
}
