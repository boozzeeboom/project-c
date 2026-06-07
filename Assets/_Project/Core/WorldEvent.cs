// T-X0: WorldEvent — base class for full event bus (per D2 / 09_OPEN_QUESTIONS.md §J).
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.3.12, 06_TRIGGERS_AND_INTEGRATION.md §6.3.
//
// T-X0 scope: base + ItemAddedEvent + ItemRemovedEvent (для T-Q06 QuestTriggerService).
// Остальные event типы (ReputationChanged, QuestStateChanged, CustomEvent, etc.) — T-Q06+.

using System;

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
}
