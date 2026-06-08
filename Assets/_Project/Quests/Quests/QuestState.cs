// T-Q04: Quest state enum (canonical). Discovered = 0 per §K (EventDriven).
// Заменяет временный ProjectC.Dialogue.QuestStateMirror (T-Q03 stub).
// T-Q09 refactor: переключить DialogueCondition.questStateParam на этот тип
// (он уже имеет identical numeric values, сериализация совместима).

namespace ProjectC.Quests
{
    /// <summary>
    /// Quest lifecycle state. Server is source of truth; client mirrors via snapshot.
    /// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.4 + 09_OPEN_QUESTIONS.md §K.
    /// </summary>
    public enum QuestState : byte
    {
        /// <summary>EventDriven quest: trigger fired, записан в журнал, но игрок ещё не нажал Accept. (T-Q04+)</summary>
        Discovered = 0,

        /// <summary>Dialog предложил (OfferQuest action), игрок ещё не принял. MVA: auto-transition Discovered/Offered → Active on first advance. (T-Q15)</summary>
        Offered = 1,

        /// <summary>Принят, objectives в работе.</summary>
        Active = 2,

        /// <summary>Все required objectives выполнены, но ещё не turn-in'нут (awaiting return-to-NPC).</summary>
        Completed = 3,

        /// <summary>Провален (FailQuest action или failed condition). Финальное состояние.</summary>
        Failed = 4,

        /// <summary>Сдан NPC, rewards выданы. Финальное состояние.</summary>
        TurnedIn = 5
    }
}
