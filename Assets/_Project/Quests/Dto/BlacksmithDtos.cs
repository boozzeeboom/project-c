// T-SMITH01: Сетевые DTO для сервиса Кузнеца именных NPC (улучшение любых pickable items).
// См. docs/NPC_quests/merchants/01_NPC_BLACKSMITH_GUIDE.md

using System;
using Unity.Netcode;
using ProjectC.Items.Variation.Dto;

namespace ProjectC.Quests.Dto
{
    public enum BlacksmithUpgradeResultCode : byte
    {
        Ok                  = 0,
        InvalidArgs         = 1,
        NpcNotFound         = 2,
        NotBlacksmith       = 3,
        TooFarFromNpc       = 4,
        TierNotSupported    = 5,
        ItemNotOwned        = 6,
        MissingResource     = 7,
        InsufficientCredits = 8,
        RequirementsNotMet  = 9,
        AlreadyAtOrAboveTier = 10,
        RateLimited         = 11,
        InternalError       = 12
    }

    /// <summary>
    /// Параметры и превью улучшения предмета на один конкретный тир (Бронза / Сталь / Композит / Антигравий).
    /// </summary>
    [Serializable]
    public struct BlacksmithTierOptionDto : INetworkSerializable
    {
        public byte tier;                  // BlacksmithUpgradeTier (0..3)
        public string tierName;
        public string colorHex;
        public bool isAllowedByNpc;        // умеет ли данный кузнец работать с этим металлом
        public bool isUnlockedByRep;       // выполнены ли требования отношения/репутации
        public string lockedReason;
        public int resourceItemId;         // ID ресурса для режима "Предмет + Ресурс"
        public string resourceName;        // имя ресурса (например "Оловянистая бронза")
        public int requiredResourceCount;  // сколько нужно ресурса
        public int playerResourceCount;    // сколько есть у игрока в рюкзаке
        public int withResourceCreditsCost;// доплата в CR при наличии ресурса
        public int creditsOnlyCost;        // полная цена в CR без ресурса (самая дорогая опция)
        public string previewNewName;      // "Яблоко [Улучшено бронзой]"
        public string previewDiffSummary;  // "⚔ Урон: 5 → 8 (+3) • 🔨 Прочность: 20 → 35 • 💰 Ценность: 20 → 27 CR"

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            var tName = tierName ?? "";
            var cHex = colorHex ?? "";
            var lReason = lockedReason ?? "";
            var rName = resourceName ?? "";
            var pName = previewNewName ?? "";
            var pDiff = previewDiffSummary ?? "";

            s.SerializeValue(ref tier);
            s.SerializeValue(ref tName);
            s.SerializeValue(ref cHex);
            s.SerializeValue(ref isAllowedByNpc);
            s.SerializeValue(ref isUnlockedByRep);
            s.SerializeValue(ref lReason);
            s.SerializeValue(ref resourceItemId);
            s.SerializeValue(ref rName);
            s.SerializeValue(ref requiredResourceCount);
            s.SerializeValue(ref playerResourceCount);
            s.SerializeValue(ref withResourceCreditsCost);
            s.SerializeValue(ref creditsOnlyCost);
            s.SerializeValue(ref pName);
            s.SerializeValue(ref pDiff);

            if (s.IsReader)
            {
                tierName = tName ?? "";
                colorHex = cHex ?? "";
                lockedReason = lReason ?? "";
                resourceName = rName ?? "";
                previewNewName = pName ?? "";
                previewDiffSummary = pDiff ?? "";
            }
        }
    }

    /// <summary>
    /// Один предмет из рюкзака игрока, доступный для улучшения у кузнеца.
    /// </summary>
    [Serializable]
    public struct BlacksmithTargetItemDto : INetworkSerializable
    {
        public int itemId;
        public byte itemType;
        public string displayName;
        public int ownedCount;
        public int currentUpgradeTier;     // -1 = базовый, 0..3 = Бронза..Антигравий
        public string currentStatsSummary;
        public BlacksmithTierOptionDto[] tierOptions;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            var dName = displayName ?? "";
            var curStats = currentStatsSummary ?? "";

            s.SerializeValue(ref itemId);
            s.SerializeValue(ref itemType);
            s.SerializeValue(ref dName);
            s.SerializeValue(ref ownedCount);
            s.SerializeValue(ref currentUpgradeTier);
            s.SerializeValue(ref curStats);

            if (s.IsReader)
            {
                displayName = dName ?? "";
                currentStatsSummary = curStats ?? "";
            }

            int len = tierOptions != null ? tierOptions.Length : 0;
            s.SerializeValue(ref len);
            if (s.IsReader) tierOptions = len > 0 ? new BlacksmithTierOptionDto[len] : Array.Empty<BlacksmithTierOptionDto>();
            for (int i = 0; i < len; i++)
            {
                var opt = tierOptions != null && i < tierOptions.Length ? tierOptions[i] : default;
                opt.NetworkSerialize(s);
                if (tierOptions != null) tierOptions[i] = opt;
            }
        }
    }

    /// <summary>
    /// Полный серверный снимок Кузницы NPC для правого блока DialogWindow.
    /// </summary>
    [Serializable]
    public struct BlacksmithShopSnapshotDto : INetworkSerializable
    {
        public string npcId;
        public string smithyTitle;
        public string factionDisplayName;
        public bool isBlacksmith;
        public bool isTestAllTiersMode;
        public byte allowedTiersMask;
        public float playerCredits;
        public float npcAttitudeExact;
        public float factionReputationExact;
        public int discountPercent;
        public BlacksmithTargetItemDto[] playerItems;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            var npcStr = npcId ?? "";
            var titleStr = smithyTitle ?? "";
            var facStr = factionDisplayName ?? "";

            s.SerializeValue(ref npcStr);
            s.SerializeValue(ref titleStr);
            s.SerializeValue(ref facStr);
            s.SerializeValue(ref isBlacksmith);
            s.SerializeValue(ref isTestAllTiersMode);
            s.SerializeValue(ref allowedTiersMask);
            s.SerializeValue(ref playerCredits);
            s.SerializeValue(ref npcAttitudeExact);
            s.SerializeValue(ref factionReputationExact);
            s.SerializeValue(ref discountPercent);

            if (s.IsReader)
            {
                npcId = npcStr ?? "";
                smithyTitle = titleStr ?? "";
                factionDisplayName = facStr ?? "";
            }

            int len = playerItems != null ? playerItems.Length : 0;
            s.SerializeValue(ref len);
            if (s.IsReader) playerItems = len > 0 ? new BlacksmithTargetItemDto[len] : Array.Empty<BlacksmithTargetItemDto>();
            for (int i = 0; i < len; i++)
            {
                var entry = playerItems != null && i < playerItems.Length ? playerItems[i] : default;
                entry.NetworkSerialize(s);
                if (playerItems != null) playerItems[i] = entry;
            }
        }
    }

    /// <summary>
    /// Результат одной операции улучшения предмета у кузнеца.
    /// </summary>
    [Serializable]
    public struct BlacksmithUpgradeResultDto : INetworkSerializable
    {
        public byte code;                  // BlacksmithUpgradeResultCode
        public string npcId;
        public int itemId;
        public byte upgradedToTier;        // BlacksmithUpgradeTier
        public byte paymentMode;           // BlacksmithPaymentMode
        public string upgradedDisplayName;
        public string statsDiffSummary;
        public int creditsSpent;
        public float newCredits;
        public float reputationGain;       // +0.2 за каждые 100 CR
        public float newNpcAttitudeExact;
        public float newFactionReputationExact;
        public ItemInstanceDto upgradedInstance;
        public string message;

        public bool IsSuccess => code == (byte)BlacksmithUpgradeResultCode.Ok;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            var npcStr = npcId ?? "";
            var upName = upgradedDisplayName ?? "";
            var diffStr = statsDiffSummary ?? "";
            var msgStr = message ?? "";

            s.SerializeValue(ref code);
            s.SerializeValue(ref npcStr);
            s.SerializeValue(ref itemId);
            s.SerializeValue(ref upgradedToTier);
            s.SerializeValue(ref paymentMode);
            s.SerializeValue(ref upName);
            s.SerializeValue(ref diffStr);
            s.SerializeValue(ref creditsSpent);
            s.SerializeValue(ref newCredits);
            s.SerializeValue(ref reputationGain);
            s.SerializeValue(ref newNpcAttitudeExact);
            s.SerializeValue(ref newFactionReputationExact);
            s.SerializeValue(ref msgStr);

            var inst = upgradedInstance;
            inst.NetworkSerialize(s);
            upgradedInstance = inst;

            if (s.IsReader)
            {
                npcId = npcStr ?? "";
                upgradedDisplayName = upName ?? "";
                statsDiffSummary = diffStr ?? "";
                message = msgStr ?? "";
            }
        }
    }
}
