// T-Q04: QuestObjective — single-class atomic pattern (same as T-Q03).
// Docs: docs/NPC_quests/02_V2_ARCHITECTURE.md section 2.3.6 + 09_OPEN_QUESTIONS.md section K.
// EventDriven = 7: per section K, kvest popadaet v Discovered state kogda CustomEvent eventId publikuetsya.

using System;
using UnityEngine;
using ProjectC.Factions;

namespace ProjectC.Quests
{
    /// <summary>
    /// Quest objective types. Atomic: kazhdyy ispolzuet svoy nabor poley v QuestObjective.
    /// </summary>
    public enum QuestObjectiveType : byte
    {
        TalkToNpc = 0,
        DeliverItem = 1,
        ReachLocation = 2,
        KillEntity = 3,
        HaveItem = 4,
        ReputationAtLeast = 5,
        WaitForEvent = 6,
        EventDriven = 7
    }

    [Serializable]
    public class QuestObjective
    {
        [Tooltip("Unikalnyy id v predelah kvesta.")]
        public string objectiveId = "";

        [Tooltip("Tip objective.")]
        public QuestObjectiveType objectiveType = QuestObjectiveType.TalkToNpc;

        [Tooltip("Tekst dlya zhurnala/trackera.")]
        public string description = "";

        [Tooltip("NPC id (dlya TalkToNpc, DeliverItem).")]
        public string targetNpcId = "";

        [Tooltip("Trade item id (dlya DeliverItem, HaveItem).")]
        public string itemTradeItemId = "";

        [Tooltip("Required quantity.")]
        public int requiredQuantity = 1;

        [Tooltip("Scene id (dlya ReachLocation).")]
        public string targetSceneId = "";

        [Tooltip("World position (dlya ReachLocation).")]
        public Vector3 targetPosition = Vector3.zero;

        [Tooltip("Radius ot position v metrah.")]
        [Min(0f)]
        public float targetRadius = 10f;

        [Tooltip("Entity type tag (dlya KillEntity, STUB).")]
        public string targetEntityType = "";

        [Tooltip("Faction (dlya ReputationAtLeast).")]
        public FactionId targetFaction = FactionId.None;

        [Tooltip("Reputation threshold.")]
        public int reputationValue = 0;

        [Tooltip("Custom event id (dlya WaitForEvent, EventDriven).")]
        public string eventId = "";

        [Tooltip("Optional: ne blokiruet stage completion.")]
        public bool optional = false;

        [Tooltip("Required (default true).")]
        public bool required = true;
    }
}
