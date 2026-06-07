// T-Q03: DialogueCondition — single-class atomic pattern.
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.3.10.
//
// Design choice: НЕТ composite (And/Or/Not children) в v1 — Unity
// serialization depth limit 10 не позволяет рекурсивный [Serializable]
// class без [SerializeReference]. Вместо этого Edge.condition —
// ОДИН атомарный condition. Для AND-комбинации нескольких atomic
// (типичный кейс) используется поле `conditions: DialogueCondition[]`
// в Edge — все atomic AND-комбинируются на runtime.
//
// OR-логика: создай два edges с разными atomic conditions.
// NOT-логика: используй инвертированные atomic типы (ReputationAtMost,
//   !QuestStateEquals → отдельный атомарный). Полный NOT — future v2.

using System;
using UnityEngine;
using ProjectC.Factions;

namespace ProjectC.Dialogue
{
    /// <summary>
    /// Atomic condition types. Каждое условие — single field, никакой рекурсии.
    /// Composite (And/Or/Not) обрабатываются на уровне Edge/Node через массив
    /// atomic-условий.
    /// </summary>
    public enum DialogueConditionType : byte
    {
        // ============ Inventory ============
        /// <summary>Player has at least <see cref="DialogueCondition.intParam"/> of item with id <see cref="DialogueCondition.stringParam"/>.</summary>
        HasItem = 10,
        /// <summary>Ship cargo has at least intParam of trade item stringParam (TradeItemDefinition id).</summary>
        CargoHasItem = 11,

        // ============ Quest state ============
        /// <summary>Quest stringParam is in state questStateParam.</summary>
        QuestStateEquals = 20,
        /// <summary>Quest stringParam is on stage stageIdParam.</summary>
        QuestStageEquals = 21,
        /// <summary>Quest stringParam has been completed (Completed or TurnedIn).</summary>
        QuestCompleted = 22,
        /// <summary>Quest stringParam is in Discovered state (EventDriven, §K).</summary>
        QuestDiscovered = 23,

        // ============ Reputation / Attitude ============
        /// <summary>Reputation with factionParam >= intParam.</summary>
        ReputationAtLeast = 30,
        /// <summary>Reputation with factionParam &lt;= intParam.</summary>
        ReputationAtMost = 31,
        /// <summary>NpcAttitude with NPC id stringParam >= intParam.</summary>
        NpcAttitudeAtLeast = 32,

        // ============ World state ============
        /// <summary>Time of day matches stringParam (TimeOfDayPhase.phaseName).</summary>
        TimeOfDayIn = 40,
        /// <summary>Player is in scene stringParam (matches SceneRegistry).</summary>
        PlayerInZone = 41,
        /// <summary>Global flag stringParam is set (server-side flag store).</summary>
        FlagIsSet = 42,
        /// <summary>Dialogue node stageIdParam in tree stringParam was visited at least once.</summary>
        WasNodeVisited = 43
    }

    /// <summary>
    /// Quest states mirrored from server (для атомарных conditions).
    /// Local copy — server is source of truth, this is just for Inspector display.
    /// Когда T-Q04 введёт <c>ProjectC.Quests.QuestState</c>, заменим reference.
    /// </summary>
    public enum QuestStateMirror : byte
    {
        Discovered = 0,  // §K: событийный квест, ещё не принят
        Offered = 1,     // dialog предложил, не принят
        Active = 2,
        Completed = 3,
        Failed = 4,
        TurnedIn = 5
    }

    /// <summary>
    /// A single atomic gate evaluated server-side when an edge is shown.
    /// Combine multiple via <c>DialogueEdge.conditions</c> (AND semantics).
    /// </summary>
    /// <remarks>
    /// Это [Serializable] POCO, не ScriptableObject — сериализуется inline
    /// в DialogTree.asset. Один инспектор на condition, простой UX.
    /// </remarks>
    [Serializable]
    public class DialogueCondition
    {
        [Tooltip("Тип условия (atomic). См. enum docs.")]
        public DialogueConditionType type = DialogueConditionType.HasItem;

        [Tooltip("Primary string param: itemId / questId / npcId / treeId / sceneId / flagId / phaseName.")]
        public string stringParam = "";

        [Tooltip("Secondary string param: stageId (для QuestStageEquals, WasNodeVisited).")]
        public string stageIdParam = "";

        [Tooltip("Numeric param: quantity (HasItem) / reputation value / NpcAttitude value.")]
        public int intParam = 0;

        [Tooltip("Faction param (для ReputationAtLeast/AtMost).")]
        public FactionId factionParam = FactionId.None;

        [Tooltip("Quest state param (для QuestStateEquals).")]
        public QuestStateMirror questStateParam = QuestStateMirror.Active;
    }
}
