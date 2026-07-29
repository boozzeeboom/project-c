// T-Q04: QuestReward — что игрок получает при turn-in (TurnedIn state).
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.3.4 (rewards sub-structure) + §7.2.

using System;
using UnityEngine;
using ProjectC.Factions;
using ProjectC.Dialogue;
using ProjectC.Items;
using ProjectC.Trade;

namespace ProjectC.Quests
{
    /// <summary>Single item reward. Поддерживает два типа: pickupItem (ItemData, инвентарь) и cargoItem (TradeItemDefinition, груз).</summary>
    [Serializable]
    public class QuestRewardItem
    {
        [Tooltip("TradeItemDefinition.itemId (string). Оставлено для CSV-импорта. pickupItem/cargoItem приоритетнее.")]
        public string tradeItemId = "";

        [Tooltip("Pickable item (ItemData) — перетащи .asset из Resources/Items/. Для rewards.items[].")]
        public ItemData pickupItem;

        [Tooltip("Cargo item (TradeItemDefinition) — перетащи .asset из Trade/Data/Items/. Для rewards.cargoItems[].")]
        public TradeItemDefinition cargoItem;

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

        [Tooltip("ID unlocked entity (treeId, sceneId, recipeId, achievementId). Оставлено для CSV. unlockDialog приоритетнее.")]
        public string unlockId = "";

        [Tooltip("DialogTree reference (для unlockType=DialogTree). Перетащи .asset из Data/Dialogs/. Приоритетнее unlockId.")]
        public DialogTree unlockDialog;
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
