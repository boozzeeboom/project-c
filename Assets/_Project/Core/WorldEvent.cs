// T-X0: WorldEvent — base class for full event bus (per D2 / 09_OPEN_QUESTIONS.md §J).
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.3.12, 06_TRIGGERS_AND_INTEGRATION.md §6.3.
//
// T-X0 scope: base + ItemAddedEvent + ItemRemovedEvent (для T-Q06 QuestTriggerService).
// Остальные event типы (ReputationChanged, QuestStateChanged, CustomEvent, etc.) — T-Q06+.

using System;
using ProjectC.Quests;

namespace ProjectC.Core
{
    /// <summary>
    /// Базовый класс для server-side world events. Static singleton (WorldEventBus)
    /// рассылает подписчикам. Содержит минимум метаданных для трассировки.
    /// </summary>
    /// <remarks>
    /// PlayerId = ulong, удобно для quest/reputation/inventory scoped handlers.
    /// Timestamp — серверное время, не Time.time (Time.time = float, неточно).
    /// </remarks>
    public abstract class WorldEvent
    {
        /// <summary>ClientId игрока, к которому относится событие. 0 = global event (не привязан к игроку).</summary>
        public ulong PlayerId { get; set; }

        /// <summary>Unix-time seconds (UTC) когда событие было опубликовано.</summary>
        public long TimestampUnix { get; set; }
    }

    // ============================================================
    // Inventory events
    // ============================================================

    /// <summary>
    /// Опубликован после успешного добавления item в character inventory.
    /// Subscribers: HaveItemTrigger (T-Q06), UI notifications.
    /// </summary>
    public sealed class ItemAddedEvent : WorldEvent
    {
        /// <summary>itemId (int, matches InventoryData list elements). -1 если не int-id-based.</summary>
        public int ItemId;
        /// <summary>Количество добавленных юнитов (для stackable items; v1 всегда 1).</summary>
        public int Count;
        /// <summary>Optional: tradeItemId (string, для Trade/Quest interop). Пусто если N/A.</summary>
        public string TradeItemId;
    }

    /// <summary>
    /// Опубликован после успешного удаления item из character inventory.
    /// Subscribers: HaveItemTrigger (T-Q06), quest turn-in tracking.
    /// </summary>
    public sealed class ItemRemovedEvent : WorldEvent
    {
        public int ItemId;
        public int Count;
        public string TradeItemId;
    }

    // ============================================================
    // Reputation / Attitude events
    // ============================================================

    /// <summary>
    /// Faction reputation изменилась. Опубликован после QuestWorld.ModifyReputation (T-Q13).
    /// Subscribers: ReputationAtLeastTrigger.
    /// </summary>
    public sealed class ReputationChangedEvent : WorldEvent
    {
        /// <summary>Фракция, репутация с которой изменилась.</summary>
        public Factions.FactionId Faction;
        /// <summary>Новое значение reputation (после delta).</summary>
        public int NewValue;
        /// <summary>Дельта (может быть отрицательной).</summary>
        public int Delta;
    }

    /// <summary>
    /// NpcAttitude изменилась (per-NPC personal relationship).
    /// Опубликован после QuestWorld.ModifyNpcAttitude (T-Q13).
    /// Subscribers: NpcAttitudeAtLeastTrigger.
    /// </summary>
    public sealed class NpcAttitudeChangedEvent : WorldEvent
    {
        /// <summary>ID NPC, отношение к которому изменилось.</summary>
        public string NpcId;
        /// <summary>Новое значение attitude.</summary>
        public int NewValue;
        /// <summary>Дельта.</summary>
        public int Delta;
    }

    // ============================================================
    // Quest / Dialog events
    // ============================================================

    /// <summary>
    /// Опубликован когда QuestInstance state transitions (Discovered→Active, Active→Completed, etc).
    /// Опубликован после QuestWorld.TryAdvanceObjective / TryAccept / TryTurnIn.
    /// Subscribers: UI notifications, quest log update, etc.
    /// </summary>
    public sealed class QuestStateChangedEvent : WorldEvent
    {
        public string QuestId;
        public QuestState OldState;
        public QuestState NewState;
    }

    /// <summary>
    /// Опубликован когда dialog node показан игроку. Используется TalkedToNpcTrigger
    /// (проверяет, что игрок хотя бы раз посетил ноду с целевым NPC).
    /// Publisher: QuestServer (в RequestAdvanceDialogueRpc).
    /// </summary>
    public sealed class DialogVisitedEvent : WorldEvent
    {
        public string TreeId;
        public string NodeId;
        /// <summary>ID NPC с которым игрок говорит (для удобства триггеров).</summary>
        public string NpcId;
    }

    // ============================================================
    // World events
    // ============================================================

    /// <summary>
    /// Опубликован после DialogueAction.EmitEvent (или других internal publishers).
    /// Subscribers: EventTrigger (EventDriven objectives per §K).
    /// </summary>
    public sealed class CustomEvent : WorldEvent
    {
        /// <summary>Уникальный ID события (e.g. "player_visited_smuggler_lookout").</summary>
        public string EventId;
    }

    /// <summary>
    /// Опубликован когда DayNightController меняет фазу.
    /// Publisher: DayNightController (T-Q06, добавлен хук).
    /// </summary>
    public sealed class DayNightPhaseChangedEvent : WorldEvent
    {
        /// <summary>Новая фаза. Используем string (phaseName), не enum — TimeOfDayPhase в проекте это SO, не enum.</summary>
        public string NewPhaseName;
    }
}
