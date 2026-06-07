// T-Q04: QuestDefinition ScriptableObject — главный квестовый asset.
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.3.4 + §7.2 (FindArtifact example).
//
// Quests: server-authoritative state. Этот SO = static data (definition),
// runtime state (QuestInstance с текущим stage, progress) живёт в
// QuestWorld (POCO singleton, T-Q05).

using System;
using UnityEngine;
using ProjectC.Factions;

namespace ProjectC.Quests
{
    /// <summary>
    /// Canonical quest asset. Authored в ProjectC/Quests/Data/Quests/ как
    /// .asset file. Used by QuestServer (T-Q05) and QuestDatabaseWindow (T-Q09).
    /// </summary>
    [CreateAssetMenu(fileName = "Quest_", menuName = "ProjectC/Quests/Quest Definition", order = 130)]
    public class QuestDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Уникальный id квеста (например: 'find_artifact', 'find_artifact_followup'). " +
                 "Используется сервером как ключ в _questsByPlayer. НЕ МЕНЯТЬ после релиза.")]
        public string questId = "";

        [Tooltip("Отображаемое имя (loc key в будущем).")]
        public string displayName = "";

        [Tooltip("Краткое описание (loc key). Показывается в журнале при discover/accept.")]
        [TextArea(2, 4)]
        public string description = "";

        [Header("Faction gating")]
        [Tooltip("Фракция, с которой ассоциирован квест (для rep gates и UI группировки). " +
                 "None = нейтральный, доступен всем.")]
        public FactionId faction = FactionId.None;

        [Tooltip("Минимальная репутация с faction для доступа к квесту. " +
                 "Используется как pre-prerequisite (в дополнение к prerequisites[]).")]
        public int minReputation = 0;

        [Header("Stages")]
        [Tooltip("Все stages квеста в порядке выполнения. Обычно linear, но nextStageId " +
                 "позволяет branching (например, 'success' vs 'failure' branches).")]
        public QuestStage[] stages = Array.Empty<QuestStage>();

        [Header("Rewards (on TurnedIn)")]
        [Tooltip("Награды, выдаваемые при TurnedIn transition. " +
                 "Этот же rewards выдаётся при CompleteObjective(action) если задан (T-Q15-T-Q17).")]
        public QuestReward rewards = new QuestReward();

        [Header("Prerequisites (AND)")]
        [Tooltip("Atomic prerequisites, все должны быть true для доступности квеста. " +
                 "Сервер проверяет в QuestServer.TryOffer.")]
        public QuestPrerequisite[] prerequisites = Array.Empty<QuestPrerequisite>();

        [Header("Behavior flags")]
        [Tooltip("Если true — квест может быть получен только ОДИН раз за playthrough (idempotent). " +
                 "Если false — repeatable (e.g. daily contract-style).")]
        public bool oneShot = true;

        [Tooltip("Если true — квест виден в журнале до accept (но в состоянии Discovered). " +
                 "Обычно false; EventDriven (§K) квесты могут иметь true.")]
        public bool discoverable = true;

        // ============ Runtime helpers ============

        /// <summary>
        /// Find stage by id. O(N). Используется QuestServer при stage transition.
        /// </summary>
        public QuestStage GetStage(string stageId)
        {
            if (string.IsNullOrEmpty(stageId)) return null;
            for (int i = 0; i < stages.Length; i++)
            {
                if (stages[i] != null && stages[i].stageId == stageId) return stages[i];
            }
            return null;
        }

        /// <summary>
        /// First stage (entry point). Обычно stages[0], но explicit ordering matters.
        /// </summary>
        public QuestStage GetEntryStage()
        {
            return stages != null && stages.Length > 0 ? stages[0] : null;
        }

        /// <summary>
        /// Validate stage graph reachability. Returns unreachable stageId list.
        /// BFS from entry stage, following nextStageId edges.
        /// </summary>
        public string[] GetUnreachableStages()
        {
            if (stages == null || stages.Length == 0) return Array.Empty<string>();

            var entry = GetEntryStage();
            if (entry == null) return new[] { "<entry stage missing>" };

            var visited = new System.Collections.Generic.HashSet<string>();
            var queue = new System.Collections.Generic.Queue<string>();
            queue.Enqueue(entry.stageId);
            visited.Add(entry.stageId);

            while (queue.Count > 0)
            {
                var current = GetStage(queue.Dequeue());
                if (current == null) continue;
                if (string.IsNullOrEmpty(current.nextStageId)) continue;
                if (visited.Add(current.nextStageId)) queue.Enqueue(current.nextStageId);
            }

            var unreachable = new System.Collections.Generic.List<string>();
            for (int i = 0; i < stages.Length; i++)
            {
                if (stages[i] != null && !visited.Contains(stages[i].stageId))
                {
                    unreachable.Add(stages[i].stageId);
                }
            }
            return unreachable.ToArray();
        }
    }
}
