// T-Q04: Allowed state transitions for QuestInstance.
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.4.
// Не [Serializable] — это server-side runtime helper, не данные для инспектора.

using System.Collections.Generic;

namespace ProjectC.Quests
{
    /// <summary>
    /// Whitelist of legal QuestState transitions. Server validates before applying.
    /// UI использует для отображения кнопок (Accept / Turn in / Fail нельзя без server confirm).
    /// </summary>
    public static class QuestStateTransition
    {
        /// <summary>
        /// All allowed (from → to) pairs. Backed by HashSet для O(1) lookup.
        /// Любой не-whitelisted transition → QuestResultCode.InvalidTransition.
        /// </summary>
        public static readonly IReadOnlyDictionary<(QuestState from, QuestState to), bool> Allowed = new Dictionary<(QuestState, QuestState), bool>
        {
            // Discovered → Active (player clicked Accept в CharacterWindow → RequestAcceptQuestRpc)
            { (QuestState.Discovered, QuestState.Active), true },
            // Discovered → Failed (если timeout / worldstate invalidates event)
            { (QuestState.Discovered, QuestState.Failed), true },

            // Offered → Active (player accepted через dialog OfferQuest action)
            { (QuestState.Offered, QuestState.Active), true },
            // Offered → Failed (player declined or timeout)
            { (QuestState.Offered, QuestState.Failed), true },

            // Active → Completed (все required objectives satisfied)
            { (QuestState.Active, QuestState.Completed), true },
            // Active → Failed (FailQuest action, death, timeout)
            { (QuestState.Active, QuestState.Failed), true },

            // Completed → TurnedIn (player talked to turnIn NPC + CompleteObjective action)
            { (QuestState.Completed, QuestState.TurnedIn), true },
            // Failed / TurnedIn — terminal, no further transitions.
        };

        /// <summary>True если from → to — legal.</summary>
        public static bool IsAllowed(QuestState from, QuestState to)
        {
            return Allowed.ContainsKey((from, to));
        }
    }
}
