// T-Q06: Concrete IQuestTrigger implementations.
// См. docs/NPC_quests/06_TRIGGERS_AND_INTEGRATION.md §6.4 + §6.7 (full bus).
//
// T-Q06 scope: 6 trigger'ов. Остальные (CargoHasItem, LocationReached, KilledEntity,
// ShipDocked, ContractCompleted) — stubs / out of scope T-Q15+.
//
// Все триггеры event-driven: не имеют Update метода. Subscribers на WorldEventBus
// в QuestServer (T-Q06) вызывают QuestTriggerService.Evaluate(playerId, hint).

using System;
using System.Collections.Generic;
using ProjectC.Items;
using ProjectC.Core;
using ProjectC.Quests;

namespace ProjectC.Quests.Triggers
{
    // ============================================================
    // TalkedToNpcTrigger
    // ============================================================

    /// <summary>
    /// Fires когда игрок посетил dialog node с указанным NPC.
    /// Проверяет QuestWorld._npcTalkedTo[(playerId, npcId)] флаг.
    /// </summary>
    /// <remarks>
    /// Set в QuestWorld.MarkNpcTalked (вызывается из QuestServer.RequestAdvanceDialogueRpc).
    /// Альтернатива — подписка на DialogVisitedEvent и match по NpcId из speaker ref.
    /// </remarks>
    public sealed class TalkedToNpcTrigger : IQuestTrigger
    {
        public string TargetNpcId { get; set; } = "";

        public string TriggerId => $"TalkedToNpc:{TargetNpcId}";

        public bool IsSatisfied(QuestInstance instance, ulong playerId)
        {
            return QuestWorld.Instance?.HasNpcTalkedTo(playerId, TargetNpcId) ?? false;
        }
    }

    // ============================================================
    // HaveItemTrigger
    // ============================================================

    /// <summary>
    /// Fires когда игрок имеет достаточно items в инвентаре.
    /// Подписка на ItemAddedEvent / ItemRemovedEvent (через QuestServer subscriber).
    /// </summary>
    public sealed class HaveItemTrigger : IQuestTrigger
    {
        public int ItemDataId { get; set; } = -1;
        public int RequiredQuantity { get; set; } = 1;

        public string TriggerId => $"HaveItem:{ItemDataId}";

        public bool IsSatisfied(QuestInstance instance, ulong playerId)
        {
            if (InventoryWorld.Instance == null) return false;
            return InventoryWorld.Instance.CountOf(playerId, ItemDataId) >= RequiredQuantity;
        }
    }

    // ============================================================
    // ReputationAtLeastTrigger
    // ============================================================

    /// <summary>
    /// Fires когда reputation игрока с фракцией >= required value.
    /// </summary>
    public sealed class ReputationAtLeastTrigger : IQuestTrigger
    {
        public Factions.FactionId Faction { get; set; } = Factions.FactionId.None;
        public int RequiredValue { get; set; } = 0;

        public string TriggerId => $"ReputationAtLeast:{Faction}";

        public bool IsSatisfied(QuestInstance instance, ulong playerId)
        {
            return (QuestWorld.Instance?.GetReputation(playerId, Faction) ?? 0) >= RequiredValue;
        }
    }

    // ============================================================
    // NpcAttitudeAtLeastTrigger
    // ============================================================

    public sealed class NpcAttitudeAtLeastTrigger : IQuestTrigger
    {
        public string NpcId { get; set; } = "";
        public int RequiredValue { get; set; } = 0;

        public string TriggerId => $"NpcAttitudeAtLeast:{NpcId}";

        public bool IsSatisfied(QuestInstance instance, ulong playerId)
        {
            return (QuestWorld.Instance?.GetNpcAttitude(playerId, NpcId) ?? 0) >= RequiredValue;
        }
    }

    // ============================================================
    // DayNightPhaseTrigger
    // ============================================================

    /// <summary>
    /// Fires когда DayNightController.CurrentPhase.phaseName == required.
    /// Использует string-match (TimeOfDayPhase это SO, не enum).
    /// </summary>
    public sealed class DayNightPhaseTrigger : IQuestTrigger
    {
        public string RequiredPhaseName { get; set; } = "";

        public string TriggerId => $"DayNightPhase:{RequiredPhaseName}";

        public bool IsSatisfied(QuestInstance instance, ulong playerId)
        {
            // T-Q06: читаем DayNightController.Instance.CurrentPhase.phaseName.
            // T-Q06: DayNightController в BootstrapScene — статический Instance accessor есть?
            // Если нет — фоллбэк: FindObjectOfType.
            var ctrl = DayNightController.Instance;
            if (ctrl == null)
            {
                ctrl = UnityEngine.Object.FindObjectOfType<DayNightController>();
            }
            if (ctrl == null) return false;
            return ctrl.CurrentPhase != null && ctrl.CurrentPhase.phaseName == RequiredPhaseName;
        }
    }

    // ============================================================
    // EventTrigger (custom)
    // ============================================================

    /// <summary>
    /// Fires когда custom event (CustomEvent с matching eventId) был опубликован.
    /// Used for EventDriven objectives (per §K): квест в Discovered state when event fires.
    /// </summary>
    public sealed class EventTrigger : IQuestTrigger
    {
        public string EventId { get; set; } = "";

        public string TriggerId => $"Event:{EventId}";

        public bool IsSatisfied(QuestInstance instance, ulong playerId)
        {
            return QuestWorld.Instance?.HasEventOccurred(playerId, EventId) ?? false;
        }
    }

    // ============================================================
    // Stubs (T-Q15+ to fill)
    // ============================================================

    /// <summary>
    /// STUB: cargo trigger — TradeWorld.GetOrLoadCargo integration. Заполняется в T-Q15.
    /// T-Q06: всегда возвращает false.
    /// </summary>
    public sealed class CargoHasItemTrigger : IQuestTrigger
    {
        public ulong ShipNetId { get; set; }
        public string TradeItemId { get; set; } = "";
        public int RequiredQuantity { get; set; } = 1;

        public string TriggerId => $"CargoHasItem:{ShipNetId}:{TradeItemId}";

        public bool IsSatisfied(QuestInstance instance, ulong playerId) => false;
    }

    /// <summary>
    /// STUB: location trigger — PlayerChunkTracker integration. Заполняется в T-Q15.
    /// </summary>
    public sealed class LocationReachedTrigger : IQuestTrigger
    {
        public string SceneId { get; set; } = "";
        public UnityEngine.Vector3 Position { get; set; }
        public float Radius { get; set; } = 50f;

        public string TriggerId => $"LocationReached:{SceneId}:{Position}";

        public bool IsSatisfied(QuestInstance instance, ulong playerId) => false;
    }

    /// <summary>
    /// STUB: combat trigger — TBD когда combat появится. Всегда false.
    /// </summary>
    public sealed class KilledEntityTrigger : IQuestTrigger
    {
        public string EntityType { get; set; } = "";
        public int RequiredCount { get; set; } = 1;

        public string TriggerId => $"KilledEntity:{EntityType}";

        public bool IsSatisfied(QuestInstance instance, ulong playerId) => false;
    }
}
