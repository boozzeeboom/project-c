// T-Q05: QuestInstance — server-side runtime state of a single quest for one player.
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.4 + §2.3.4 (data model).
//
// Pairs with QuestDefinition (static data, ScriptableObject) — same shape, but
// stores progression (currentStageId, currentObjectives, state).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Quests
{
    /// <summary>
    /// Single quest state для одного игрока. Server-side, owned by QuestWorld.
    /// Mirrors (subset of) QuestDefinition + runtime progression.
    /// </summary>
    /// <remarks>
    /// Сериализуется для IQuestStateRepository (T-Q18). Persisted on every state
    /// change (per §H, fire-and-forget, no debounce).
    /// </remarks>
    [Serializable]
    public class QuestInstance
    {
        /// <summary>Static quest id (matches QuestDefinition.questId).</summary>
        public string questId = "";

        /// <summary>Current lifecycle state. См. <see cref="QuestState"/>.</summary>
        public QuestState state = QuestState.Discovered;

        /// <summary>Current stage id (matches one of QuestDefinition.stages[].stageId). Empty in Discovered.</summary>
        public string currentStageId = "";

        /// <summary>Counter map: objectiveId → progress (для HaveItem etc.). Optional objectives здесь тоже, просто ignored при stage completion check.</summary>
        public List<ObjectiveProgress> objectiveProgress = new List<ObjectiveProgress>();

        /// <summary>Quest accepted at (Unix seconds). Для "completed within last 24h" prerequisite checks.</summary>
        public long acceptedAtUnix = 0;

        /// <summary>Quest completed at (Unix seconds). Только для Completed/TurnedIn.</summary>
        public long completedAtUnix = 0;

        /// <summary>Tracked by tracker HUD (1 quest per player typically).</summary>
        public bool isTracked = false;

        /// <summary>
        /// One objective's progress counter. Resets to 0 on stage transition.
        /// Counter type depends on objective type (HaveItem → items count, etc.).
        /// </summary>
        [Serializable]
        public class ObjectiveProgress
        {
            public string objectiveId = "";
            public int currentCount = 0;
            public bool completed = false;
        }

        // ============ Helpers ============

        /// <summary>
        /// Find or create progress entry. Used by server when trigger fires.
        /// </summary>
        public ObjectiveProgress GetOrCreateProgress(string objectiveId)
        {
            for (int i = 0; i < objectiveProgress.Count; i++)
            {
                if (objectiveProgress[i].objectiveId == objectiveId) return objectiveProgress[i];
            }
            var p = new ObjectiveProgress { objectiveId = objectiveId, currentCount = 0, completed = false };
            objectiveProgress.Add(p);
            return p;
        }

        /// <summary>
        /// All required objectives completed for current stage? Server evaluates.
        /// </summary>
        public bool AreAllRequiredComplete(QuestStage stage)
        {
            if (stage == null || stage.objectives == null) return true;
            for (int i = 0; i < stage.objectives.Length; i++)
            {
                var obj = stage.objectives[i];
                if (obj == null || !obj.required || obj.optional) continue;
                var p = FindProgress(obj.objectiveId);
                if (p == null || !p.completed) return false;
            }
            return true;
        }

        /// <summary>Lookup by id (no creation). Returns null if not tracked.</summary>
        public ObjectiveProgress FindProgress(string objectiveId)
        {
            for (int i = 0; i < objectiveProgress.Count; i++)
            {
                if (objectiveProgress[i].objectiveId == objectiveId) return objectiveProgress[i];
            }
            return null;
        }
    }
}
