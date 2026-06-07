// T-Q06: IQuestTrigger interface.
// См. docs/NPC_quests/06_TRIGGERS_AND_INTEGRATION.md §6.2.
//
// Trigger = condition, evaluated by QuestTriggerService при получении event
// из WorldEventBus (event-driven, full bus per D2). Trigger'ы НЕ имеют
// Update метода — все evaluation через service.Evaluate(playerId, hint).

namespace ProjectC.Quests.Triggers
{
    /// <summary>
    /// Single quest trigger. Хранит target data (itemId, faction, etc.) и
    /// проверяет IsSatisfied против текущего world state.
    /// </summary>
    /// <remarks>
    /// OnAttach/OnDetach — optional hooks для триггеров, которые хотят
    /// подписаться/отписаться от каких-то локальных event'ов (большинство
    /// просто polled через WorldEventBus subscriber в QuestServer).
    /// </remarks>
    public interface IQuestTrigger
    {
        /// <summary>Unique key, e.g. "HaveItem:42", "TalkedToNpc:mira_01", "ReputationAtLeast:GuildOfThoughts".</summary>
        string TriggerId { get; }

        /// <summary>Оценить trigger для конкретного quest instance + player. True = objective satisfied.</summary>
        bool IsSatisfied(QuestInstance instance, ulong playerId);

        /// <summary>Optional: подписаться на bus events (default no-op).</summary>
        void OnAttach(QuestInstance instance) { }

        /// <summary>Optional: отписаться (default no-op).</summary>
        void OnDetach(QuestInstance instance) { }
    }
}
