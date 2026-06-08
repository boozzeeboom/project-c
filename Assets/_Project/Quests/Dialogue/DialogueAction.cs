// T-Q03: DialogueAction — single-class atomic pattern.
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.3.11.
//
// Design choice: НЕТ composite (Sequence/Parallel children) в v1 —
// та же причина что в DialogueCondition (Unity serialization depth limit).
// Вместо этого: Edge.action — ОДИН atomic action. Для последовательности
// действий используется массив actions в QuestStage.onEnterActions/
// onCompleteActions (T-Q04) — они выполняются в порядке массива.
//
// Это компромисс: теряем Parallel (но в MMO quests он редок — почти
// всё линейно). Sequence эмулируется через упорядоченный массив.

using System;
using UnityEngine;
using ProjectC.Factions;

namespace ProjectC.Dialogue
{
    /// <summary>
    /// Atomic action types. Каждое действие — single field, никакой рекурсии.
    /// Multiple actions на одном stage выполняются в порядке массива (Sequence).
    /// </summary>
    public enum DialogueActionType : byte
    {
        // ============ Quest (server) ============
        /// <summary>Server: QuestServer.TryOffer(playerId, questId=stringParam).</summary>
        OfferQuest = 10,
        /// <summary>Server: mark objective (questId=stringParam, objectiveId=stageIdParam) complete.</summary>
        CompleteObjective = 11,
        /// <summary>Server: fail quest (questId=stringParam, reason=stageIdParam).</summary>
        FailQuest = 12,
        /// <summary>Server: add quest to log in Discovered state (EventDriven, §K).</summary>
        DiscoverQuest = 13,
        /// <summary>Server: auto-accept quest (stringParam) — TryOffer+TryAccept. For dialog-driven immediate acceptance.</summary>
        AcceptQuest = 14,

        // ============ Inventory / Cargo ============
        /// <summary>Add intParam of item stringParam to character inventory.</summary>
        GiveItem = 20,
        /// <summary>Remove intParam of item stringParam from character inventory.</summary>
        TakeItem = 21,
        /// <summary>Add intParam of cargo item stringParam (TradeItemId) to active ship.</summary>
        GiveCargoItem = 22,
        /// <summary>Remove intParam of cargo item from active ship.</summary>
        TakeCargoItem = 23,

        // ============ Currency / Reputation / Attitude ============
        /// <summary>Add intParam credits to player wallet.</summary>
        GiveCredits = 30,
        /// <summary>Add intParam reputation delta with factionParam.</summary>
        AddReputation = 31,
        /// <summary>Add intParam NpcAttitude delta with NPC id stringParam.</summary>
        AddNpcAttitude = 32,

        // ============ Market / Service ============
        /// <summary>Open MarketWindow for zone stringParam. Sub-flow: dialog resumes on close.</summary>
        OpenMarket = 40,
        /// <summary>Open service UI (Repair/Refuel) at current zone. Sub-flow.</summary>
        OpenService = 41,

        // ============ World state ============
        /// <summary>Set global flag stringParam (server-side flag store).</summary>
        SetFlag = 50,
        /// <summary>Emit custom event stringParam через WorldEventBus (T-Q06).</summary>
        EmitEvent = 51,
        /// <summary>Switch active DialogTree (NPC.runtime.currentTree) to stringParam.</summary>
        SwitchDialogTree = 52,
        /// <summary>Close the dialog window. No params. Use on edge with targetNodeId == "" as "Goodbye" option.</summary>
        EndConversation = 60
    }

    /// <summary>
    /// A single atomic server-side effect. Multiple actions на stage/
    /// edge выполняются в порядке (Sequence, через массив в caller).
    /// </summary>
    [Serializable]
    public class DialogueAction
    {
        [Tooltip("Тип действия (atomic).")]
        public DialogueActionType type = DialogueActionType.EndConversation;

        [Tooltip("Primary string param: questId / itemId / npcId / eventId / treeId / market zoneId / flag id.")]
        public string stringParam = "";

        [Tooltip("Secondary string param: objectiveId / service id / dialog nodeId.")]
        public string stageIdParam = "";

        [Tooltip("Numeric param: quantity / credits / reputation delta / attitude delta.")]
        public int intParam = 0;

        [Tooltip("Faction param (для AddReputation).")]
        public FactionId factionParam = FactionId.None;
    }
}
