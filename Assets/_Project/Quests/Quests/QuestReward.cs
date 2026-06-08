// T-Q04: QuestReward — что игрок получает при turn-in (TurnedIn state).
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.3.4 (rewards sub-structure) + §7.2.

using System;
using UnityEngine;
using ProjectC.Factions;
using ProjectC.Dialogue;

namespace ProjectC.Quests
{
    /// <summary>Single item reward (id = TradeItemDefinition.itemId, NOT v1 ItemData.int).</summary>
    [Serializable]
    public class QuestRewardItem
    {
        [Tooltip("TradeItemDefinition.itemId (string).")]
        public string tradeItemId = "";

        [Tooltip("Количество.")]
        [Min(1)]
        public int count = 1;
    }

    /// <summary>Single reputation reward.</summary>
    [Serializable]
    public class QuestRewardReputation
    {
        public FactionId faction = FactionId.None;

        [Tooltip("Дельта репутации (может быть отрицательной).")]
        public int value = 0;
    }

    /// <summary>Unlock type for reward unlocks[] (dialogs, zones, items).</summary>
    public enum QuestUnlockType : byte
    {
        DialogTree = 0,    // unlock new DialogTree (stringParam = treeId)
        Zone = 1,          // unlock new zone (stringParam = sceneId)
        Recipe = 2,        // (future) crafting recipe
        Achievement = 3    // (future) achievement
    }

    /// <summary>Single unlock reward.</summary>
    [Serializable]
    public class QuestRewardUnlock
    {
        public QuestUnlockType unlockType = QuestUnlockType.DialogTree;

        [Tooltip("ID unlocked entity (treeId, sceneId, recipeId, achievementId).")]
        public string unlockId = "";
    }

    /// <summary>
    /// Rewards bundle. Fire-and-forget список, выдаётся при TurnedIn transition.
    /// </summary>
    [Serializable]
    public class QuestReward
    {
        [Tooltip("Кредиты, добавляются в кошелёк игрока.")]
        public int credits = 0;

        [Tooltip("Предметы в character inventory (использует InventoryServer.AddItem в T-Q14-T-Q15).")]
        public QuestRewardItem[] items = Array.Empty<QuestRewardItem>();

        [Tooltip("Cargo items (добавляются в активный корабль, T-Q15).")]
        public QuestRewardItem[] cargoItems = Array.Empty<QuestRewardItem>();

        [Tooltip("Reputation deltas per faction.")]
        public QuestRewardReputation[] reputation = Array.Empty<QuestRewardReputation>();

        [Tooltip("Unlocks: dialogs / zones / recipes.")]
        public QuestRewardUnlock[] unlocks = Array.Empty<QuestRewardUnlock>();
    }
}
