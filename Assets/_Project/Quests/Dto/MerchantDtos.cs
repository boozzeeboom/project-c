// T-MERCH01: DTOs для торговли с именными/квестовыми NPC (правый блок в DialogWindow).
// Все операции валидируются на сервере (QuestServer + QuestWorld).

using System;
using Unity.Netcode;

namespace ProjectC.Quests.Dto
{
    public enum MerchantTradeResultCode : byte
    {
        Ok                  = 0,
        InvalidArgs         = 1,
        NpcNotFound         = 2,
        NotMerchant         = 3,
        TooFarFromNpc       = 4,
        ItemNotInShop       = 5,
        OutOfStock          = 6,
        InsufficientCredits = 7,
        InventoryFull       = 8,
        ItemNotOwned        = 9,
        RequirementsNotMet  = 10,
        SellDisabled        = 11,
        RateLimited         = 12,
        InternalError       = 13
    }

    /// <summary>
    /// Одна позиция в ассортименте торговца NPC (вкладка «Купить»).
    /// </summary>
    [Serializable]
    public struct MerchantItemDto : INetworkSerializable
    {
        public int itemId;
        public byte itemType;          // (byte)ItemType
        public string itemName;
        public int buyPrice;           // цена покупки 1 шт. у NPC (CR)
        public int sellPrice;          // цена скупки 1 шт. у игрока (CR)
        public int stock;              // -1 = бесконечный, >= 0 = остаток на сервере
        public int playerOwnedCount;   // сколько уже есть у игрока в рюкзаке
        public bool isUnlocked;        // выполнены ли требования отношения/репутации/квеста
        public string lockedReason;    // причина блокировки (если !isUnlocked)

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            var nameStr = itemName ?? "";
            var reasonStr = lockedReason ?? "";
            s.SerializeValue(ref itemId);
            s.SerializeValue(ref itemType);
            s.SerializeValue(ref nameStr);
            s.SerializeValue(ref buyPrice);
            s.SerializeValue(ref sellPrice);
            s.SerializeValue(ref stock);
            s.SerializeValue(ref playerOwnedCount);
            s.SerializeValue(ref isUnlocked);
            s.SerializeValue(ref reasonStr);
            if (s.IsReader)
            {
                itemName = nameStr ?? "";
                lockedReason = reasonStr ?? "";
            }
        }
    }

    /// <summary>
    /// Одна позиция из рюкзака игрока, которую можно продать этому NPC (вкладка «Продать»).
    /// </summary>
    [Serializable]
    public struct MerchantPlayerSellItemDto : INetworkSerializable
    {
        public int itemId;
        public byte itemType;
        public string itemName;
        public int ownedCount;
        public int sellPrice;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            var nameStr = itemName ?? "";
            s.SerializeValue(ref itemId);
            s.SerializeValue(ref itemType);
            s.SerializeValue(ref nameStr);
            s.SerializeValue(ref ownedCount);
            s.SerializeValue(ref sellPrice);
            if (s.IsReader)
            {
                itemName = nameStr ?? "";
            }
        }
    }

    /// <summary>
    /// Полный серверный снимок состояния магазина NPC для правого блока DialogWindow.
    /// </summary>
    [Serializable]
    public struct MerchantShopSnapshotDto : INetworkSerializable
    {
        public string npcId;
        public string shopTitle;
        public string factionDisplayName;
        public bool isMerchant;
        public bool isTestSellAllMode;
        public bool allowPlayerSell;
        public float playerCredits;
        public int inventoryUsedSlots;
        public int inventoryMaxSlots;
        public float npcAttitudeExact;
        public float factionReputationExact;
        public int discountPercent; // <0 = скидка, >0 = наценка
        public MerchantItemDto[] shopItems;
        public MerchantPlayerSellItemDto[] playerSellableItems;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            var npcStr = npcId ?? "";
            var titleStr = shopTitle ?? "";
            var facStr = factionDisplayName ?? "";
            s.SerializeValue(ref npcStr);
            s.SerializeValue(ref titleStr);
            s.SerializeValue(ref facStr);
            s.SerializeValue(ref isMerchant);
            s.SerializeValue(ref isTestSellAllMode);
            s.SerializeValue(ref allowPlayerSell);
            s.SerializeValue(ref playerCredits);
            s.SerializeValue(ref inventoryUsedSlots);
            s.SerializeValue(ref inventoryMaxSlots);
            s.SerializeValue(ref npcAttitudeExact);
            s.SerializeValue(ref factionReputationExact);
            s.SerializeValue(ref discountPercent);
            if (s.IsReader)
            {
                npcId = npcStr ?? "";
                shopTitle = titleStr ?? "";
                factionDisplayName = facStr ?? "";
            }

            int shopLen = shopItems != null ? shopItems.Length : 0;
            s.SerializeValue(ref shopLen);
            if (s.IsReader) shopItems = shopLen > 0 ? new MerchantItemDto[shopLen] : Array.Empty<MerchantItemDto>();
            for (int i = 0; i < shopLen; i++)
            {
                var entry = shopItems != null && i < shopItems.Length ? shopItems[i] : default;
                entry.NetworkSerialize(s);
                if (shopItems != null) shopItems[i] = entry;
            }

            int sellLen = playerSellableItems != null ? playerSellableItems.Length : 0;
            s.SerializeValue(ref sellLen);
            if (s.IsReader) playerSellableItems = sellLen > 0 ? new MerchantPlayerSellItemDto[sellLen] : Array.Empty<MerchantPlayerSellItemDto>();
            for (int i = 0; i < sellLen; i++)
            {
                var entry = playerSellableItems != null && i < playerSellableItems.Length ? playerSellableItems[i] : default;
                entry.NetworkSerialize(s);
                if (playerSellableItems != null) playerSellableItems[i] = entry;
            }
        }
    }

    /// <summary>
    /// Результат одной операции покупки или продажи в магазине NPC.
    /// </summary>
    [Serializable]
    public struct MerchantTradeResultDto : INetworkSerializable
    {
        public byte code;                  // MerchantTradeResultCode
        public bool isBuy;                 // true = покупка у NPC, false = продажа NPC
        public string npcId;
        public int itemId;
        public string itemName;
        public int quantity;
        public int totalCreditsDelta;      // сумма сделки в CR
        public float newCredits;
        public float reputationGain;       // +0.1 за каждые 100 CR
        public float newNpcAttitudeExact;
        public float newFactionReputationExact;
        public string message;

        public bool IsSuccess => code == (byte)MerchantTradeResultCode.Ok;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            var npcStr = npcId ?? "";
            var itemStr = itemName ?? "";
            var msgStr = message ?? "";
            s.SerializeValue(ref code);
            s.SerializeValue(ref isBuy);
            s.SerializeValue(ref npcStr);
            s.SerializeValue(ref itemId);
            s.SerializeValue(ref itemStr);
            s.SerializeValue(ref quantity);
            s.SerializeValue(ref totalCreditsDelta);
            s.SerializeValue(ref newCredits);
            s.SerializeValue(ref reputationGain);
            s.SerializeValue(ref newNpcAttitudeExact);
            s.SerializeValue(ref newFactionReputationExact);
            s.SerializeValue(ref msgStr);
            if (s.IsReader)
            {
                npcId = npcStr ?? "";
                itemName = itemStr ?? "";
                message = msgStr ?? "";
            }
        }
    }
}
