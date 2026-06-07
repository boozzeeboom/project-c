// T-Q04: QuestStage — одно stage в quest progression graph.
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.3.5 + §7.2 (FindArtifact).
//
// Stage = node в linear-or-branched quest progression. Objectives AND-combine
// (all required = true). onEnterActions / onCompleteActions — массивы
// atomic DialogueAction (см. T-Q03, нет composite).

using System;
using UnityEngine;
using ProjectC.Dialogue;

namespace ProjectC.Quests
{
    /// <summary>
    /// One stage in a quest. Linear progression: nextStageId == "" or null == quest end.
    /// </summary>
    [Serializable]
    public class QuestStage
    {
        [Tooltip("Unique id в пределах квеста. Также используется как ключ для QuestStageEquals condition.")]
        public string stageId = "";

        [Tooltip("Текст для журнала (loc key в будущем).")]
        public string description = "";

        [Tooltip("Objectives в этом stage. Все required = true AND-комбинируются для завершения. " +
                 "Optional objectives — не блокируют completion, показываются приглушённо.")]
        public QuestObjective[] objectives = Array.Empty<QuestObjective>();

        [Tooltip("Atomic actions, fired ONCE при entering stage (server evaluates, в порядке массива). " +
                 "Типичные: EmitEvent, GiveItem, AddReputation, SetFlag.")]
        public DialogueAction[] onEnterActions = Array.Empty<DialogueAction>();

        [Tooltip("Atomic actions, fired ONCE при completing stage. " +
                 "Типичные: GiveCredits, GiveItem, AddReputation, CompleteObjective, EmitEvent.")]
        public DialogueAction[] onCompleteActions = Array.Empty<DialogueAction>();

        [Tooltip("stageId следующего stage. Empty/null = quest end. " +
                 "Завершение этого stage переводит currentStageId в этот value + срабатывают onEnterActions следующего.")]
        public string nextStageId = "";
    }
}
