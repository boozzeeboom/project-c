// T-SMITH01: NpcBlacksmithConfig + BlacksmithUpgradeEngine — система Кузнеца для именных/квестовых NPC.
// Улучшает ЛЮБОЙ pickable item (оружие, броню, модули, еду, ресурсы, технику — «хоть яблоко, хоть меч»)
// на базе генератора вариаций ProjectC.Items.Variation в сторону роста характеристик.
// См. docs/NPC_quests/merchants/01_NPC_BLACKSMITH_GUIDE.md

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ProjectC.Items;
using ProjectC.Items.Variation;
using ProjectC.Items.Variation.Dto;
using ProjectC.Equipment;

namespace ProjectC.Quests
{
    /// <summary>
    /// Материал/тир обработки у кузнеца (каждый следующий тир сильнее, быстрее и дороже).
    /// </summary>
    public enum BlacksmithUpgradeTier : byte
    {
        Bronze    = 0, // Бронза
        Steel     = 1, // Сталь
        Composite = 2, // Композит
        Antigrav  = 3  // Антигравий
    }

    /// <summary>
    /// Битовая маска тиров, которые умеет обрабатывать конкретный кузнец (настраивается в Инспекторе).
    /// </summary>
    [Flags]
    public enum BlacksmithTierMask : byte
    {
        None      = 0,
        Bronze    = 1 << 0,
        Steel     = 1 << 1,
        Composite = 1 << 2,
        Antigrav  = 1 << 3,
        All       = Bronze | Steel | Composite | Antigrav
    }

    /// <summary>
    /// Способ оплаты улучшения у кузнеца.
    /// </summary>
    public enum BlacksmithPaymentMode : byte
    {
        /// <summary>Предмет + только кредиты (самая дорогая опция в деньгах).</summary>
        CreditsOnly        = 0,
        /// <summary>Предмет + ресурс тира из рюкзака игрока + доп. кредиты за работу.</summary>
        WithPlayerResource = 1
    }

    /// <summary>
    /// Настройка конкретного тира материала у кузнеца (опциональный override в NpcBlacksmithConfig).
    /// </summary>
    [Serializable]
    public class BlacksmithTierConfigEntry
    {
        public BlacksmithUpgradeTier tier = BlacksmithUpgradeTier.Bronze;

        [Tooltip("Явный предмет-ресурс для этого тира (например, бронза/сталь/композит/антигравий). Если не задан — автоматически подбирается подходящий ресурс из ItemRegistry.")]
        public ItemData customResourceItem;

        [Tooltip("Сколько единиц ресурса требуется в режиме 'Предмет + Ресурс + Доп. кредиты'.")]
        [Min(1)]
        public int requiredResourceCount = 1;

        [Tooltip("Кастомная доплата в кредитах за работу с ресурсом игрока (0 = авто-расчёт от цены предмета и тира).")]
        [Min(0)]
        public int customWorkFeeCredits = 0;

        [Tooltip("Кастомная полная стоимость в кредитах без ресурса игрока (0 = авто-расчёт, самая дорогая опция).")]
        [Min(0)]
        public int customCreditsOnlyPrice = 0;

        [Tooltip("Минимальное личное отношение с кузнецом (❤) для доступа к этому тиру.")]
        [Range(-100, 200)]
        public int minNpcAttitude = -100;

        [Tooltip("Минимальная репутация с фракцией кузнеца (⚑) для доступа к этому тиру.")]
        [Range(-100, 100)]
        public int minFactionReputation = -100;
    }

    /// <summary>
    /// ScriptableObject конфигурации Кузнеца для именного NPC.
    /// </summary>
    [CreateAssetMenu(fileName = "BlacksmithConfig_", menuName = "ProjectC/Quests/NPC Blacksmith Config", order = 106)]
    public class NpcBlacksmithConfig : ScriptableObject
    {
        [Header("Smithy Identity")]
        [Tooltip("Заголовок мастерской в UI (если пусто — 'Кузница: <Имя NPC>').")]
        public string smithyTitle = "";

        [Header("Allowed Material Tiers")]
        [Tooltip("Какие тиры материалов этот кузнец умеет обрабатывать.")]
        public BlacksmithTierMask allowedTiers = BlacksmithTierMask.Bronze | BlacksmithTierMask.Steel;

        [Header("Pricing & Reputation")]
        [Tooltip("Общий множитель стоимости работы этого кузнеца.")]
        [Range(0.2f, 5.0f)]
        public float priceMultiplier = 1.0f;

        [Tooltip("Во сколько раз опция 'Только за кредиты (без ресурса)' дороже работы со своим ресурсом.")]
        [Range(1.3f, 5.0f)]
        public float creditsOnlyMarkupMultiplier = 2.35f;

        [Tooltip("Влияют ли отношение с кузнецом и репутация фракции на скидку.")]
        public bool enableReputationDiscount = true;

        [Tooltip("Максимальная скидка за отношение и репутацию (0.20 = до 20%).")]
        [Range(0f, 0.5f)]
        public float maxReputationDiscountRatio = 0.20f;

        [Header("Per-Tier Custom Overrides (Optional)")]
        [Tooltip("Опциональные переопределения ресурса, цены и порогов отношения для конкретных тиров.")]
        public BlacksmithTierConfigEntry[] tierOverrides = Array.Empty<BlacksmithTierConfigEntry>();

        public BlacksmithTierConfigEntry FindOverride(BlacksmithUpgradeTier tier)
        {
            if (tierOverrides == null) return null;
            for (int i = 0; i < tierOverrides.Length; i++)
            {
                if (tierOverrides[i] != null && tierOverrides[i].tier == tier)
                    return tierOverrides[i];
            }
            return null;
        }
    }

    /// <summary>
    /// Движок расчёта улучшений кузнеца поверх детерминированного генератора <see cref="ItemVariationGenerator"/>.
    /// Работает с ЛЮБЫМ pickable-предметом (оружие, броня, модули, еда, медицина, ресурсы, техника).
    /// </summary>
    public static class BlacksmithUpgradeEngine
    {
        /// <summary>
        /// Скорость роста репутации у кузнеца: +0.2 за каждые 100 CR, потраченных игроком (= 0.002 за 1 CR).
        /// </summary>
        public const float ReputationPer100Credits = 0.2f;
        public const float ReputationPerCredit = ReputationPer100Credits / 100f; // 0.002f

        public static float CalculateReputationGain(int creditsSpentByPlayer)
        {
            if (creditsSpentByPlayer <= 0) return 0f;
            return creditsSpentByPlayer * ReputationPerCredit;
        }

        public static bool IsTierAllowed(BlacksmithTierMask mask, BlacksmithUpgradeTier tier)
        {
            byte bit = (byte)(1 << (int)tier);
            return ((byte)mask & bit) != 0;
        }

        public static string GetTierDisplayName(BlacksmithUpgradeTier tier)
        {
            return tier switch
            {
                BlacksmithUpgradeTier.Bronze    => "Бронза",
                BlacksmithUpgradeTier.Steel     => "Сталь",
                BlacksmithUpgradeTier.Composite => "Композит",
                BlacksmithUpgradeTier.Antigrav  => "Антигравий",
                _                               => tier.ToString()
            };
        }

        public static string GetTierSuffixTag(BlacksmithUpgradeTier tier)
        {
            return tier switch
            {
                BlacksmithUpgradeTier.Bronze    => "Улучшено бронзой",
                BlacksmithUpgradeTier.Steel     => "Улучшено сталью",
                BlacksmithUpgradeTier.Composite => "Улучшено композитом",
                BlacksmithUpgradeTier.Antigrav  => "Улучшено антигравием",
                _                               => "Улучшено"
            };
        }

        public static string GetTierColorHex(BlacksmithUpgradeTier tier)
        {
            return tier switch
            {
                BlacksmithUpgradeTier.Bronze    => "#CD7F32",
                BlacksmithUpgradeTier.Steel     => "#7FA6C4",
                BlacksmithUpgradeTier.Composite => "#52C48A",
                BlacksmithUpgradeTier.Antigrav  => "#55B8FF",
                _                               => "#FFFFFF"
            };
        }

        public static string GetTierEffectShortDescription(BlacksmithUpgradeTier tier)
        {
            return tier switch
            {
                BlacksmithUpgradeTier.Bronze    => "Бронзовое укрепление: +15% урон/статы, +15 прочность, +2% крит, +35% ценность",
                BlacksmithUpgradeTier.Steel     => "Стальная закалка: +30% урон/статы, +35 прочность, +4% крит, +2% скорость, +75% ценность",
                BlacksmithUpgradeTier.Composite => "Композитное армирование: +50% урон/статы, +15% дальность, −18% вес, +5% скорость, ×2.3 ценность",
                BlacksmithUpgradeTier.Antigrav  => "Антигравиевый резонанс: +80% урон/статы, +25% дальность, −35% вес, +10% скорость, ×3.2 ценность",
                _                               => ""
            };
        }

        /// <summary>
        /// Подобрать подходящий ресурс для тира (либо явно заданный в конфиге, либо из рюкзака игрока / базы ItemRegistry).
        /// Возвращает (resourceItemId, resourceDisplayName, requiredCount, playerOwnedCount).
        /// </summary>
        public static (int resourceItemId, string resourceName, int requiredCount, int playerOwnedCount) ResolveTierResourceForPlayer(
            ulong clientId,
            BlacksmithUpgradeTier tier,
            NpcBlacksmithConfig config)
        {
            var inv = InventoryWorld.Instance;
            var entryOverride = config != null ? config.FindOverride(tier) : null;
            int reqCount = entryOverride != null && entryOverride.requiredResourceCount > 0
                ? entryOverride.requiredResourceCount
                : 1;

            // 1. Если дизайнер явно указал ресурс в override
            if (entryOverride != null && entryOverride.customResourceItem != null)
            {
                int explicitId = QuestWorld.ResolveItemId(null, entryOverride.customResourceItem);
                int owned = (inv != null && explicitId > 0) ? inv.CountOf(clientId, explicitId) : 0;
                string name = !string.IsNullOrEmpty(entryOverride.customResourceItem.itemName)
                    ? entryOverride.customResourceItem.itemName
                    : entryOverride.customResourceItem.name;
                return (explicitId, name, reqCount, owned);
            }

            // 2. Авто-поиск подходящего ресурса тира сначала среди предметов в рюкзаке игрока, затем во всём ItemRegistry
            int bestInBagId = 0;
            string bestInBagName = null;
            int bestInBagCount = 0;

            int fallbackCatalogId = 0;
            string fallbackCatalogName = null;

            if (inv != null)
            {
                foreach (var kvp in inv.GetAllItems())
                {
                    int id = kvp.Key;
                    var def = kvp.Value;
                    if (def == null || def.itemType == ItemType.Key) continue;
                    if (!MatchesTierResourceKeyword(def, tier)) continue;

                    if (fallbackCatalogId == 0)
                    {
                        fallbackCatalogId = id;
                        fallbackCatalogName = def.itemName ?? def.name;
                    }

                    int owned = inv.CountOf(clientId, id);
                    if (owned > bestInBagCount)
                    {
                        bestInBagId = id;
                        bestInBagName = def.itemName ?? def.name;
                        bestInBagCount = owned;
                    }
                }
            }

            if (bestInBagId > 0)
                return (bestInBagId, bestInBagName, reqCount, bestInBagCount);

            if (fallbackCatalogId > 0)
                return (fallbackCatalogId, fallbackCatalogName, reqCount, 0);

            string defaultLabel = tier switch
            {
                BlacksmithUpgradeTier.Bronze    => "Бронза / Медный слиток",
                BlacksmithUpgradeTier.Steel     => "Сталь / Железный слиток",
                BlacksmithUpgradeTier.Composite => "Композит / Углеволокно",
                BlacksmithUpgradeTier.Antigrav  => "Антигравий (любой)",
                _                               => "Ресурс"
            };
            return (0, defaultLabel, reqCount, 0);
        }

        public static bool MatchesTierResourceKeyword(ItemData def, BlacksmithUpgradeTier tier)
        {
            if (def == null) return false;
            string n = ((def.itemName ?? "") + " " + (def.name ?? "")).ToLowerInvariant();
            switch (tier)
            {
                case BlacksmithUpgradeTier.Bronze:
                    return n.Contains("bronze") || n.Contains("бронз") ||
                           n.Contains("brass") || n.Contains("латун") ||
                           n.Contains("медный_слиток") || n.Contains("медный слиток") || n.Contains("copper");
                case BlacksmithUpgradeTier.Steel:
                    return (n.Contains("steel") || n.Contains("сталь") ||
                            n.Contains("железный_слиток") || n.Contains("железный слиток") ||
                            n.Contains("galvanized_sheet_steel") || n.Contains("forged_steel"))
                           && def.equipSlot == EquipSlot.None;
                case BlacksmithUpgradeTier.Composite:
                    return n.Contains("composite") || n.Contains("композит") ||
                           n.Contains("carbon") || n.Contains("угле") ||
                           n.Contains("plexi") || n.Contains("polymer") || n.Contains("полимер");
                case BlacksmithUpgradeTier.Antigrav:
                    return (def.itemType == ItemType.Antigrav || n.Contains("antigrav") || n.Contains("антиграв"))
                           && def.equipSlot == EquipSlot.None;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Рассчитать стоимость улучшения в кредитах для обоих режимов оплаты:
        /// 1) withResourceCredits — когда игрок даёт предмет + свой ресурс тира + доплачивает кредиты;
        /// 2) creditsOnlyPrice — когда игрок даёт только предмет и платит полную стоимость деньгами (самая дорогая опция).
        /// </summary>
        public static (int withResourceCredits, int creditsOnlyPrice) CalculateUpgradeCosts(
            ItemData targetItem,
            BlacksmithUpgradeTier tier,
            NpcBlacksmithConfig config,
            float npcAttitude,
            float factionRep)
        {
            int itemBasePrice = MerchantPriceResolver.EstimateBaseItemPrice(targetItem, 30);
            float tierWorkFactor = tier switch
            {
                BlacksmithUpgradeTier.Bronze    => 0.45f,
                BlacksmithUpgradeTier.Steel     => 0.85f,
                BlacksmithUpgradeTier.Composite => 1.45f,
                BlacksmithUpgradeTier.Antigrav  => 2.30f,
                _                               => 0.50f
            };
            int tierFlatFee = tier switch
            {
                BlacksmithUpgradeTier.Bronze    => 25,
                BlacksmithUpgradeTier.Steel     => 60,
                BlacksmithUpgradeTier.Composite => 120,
                BlacksmithUpgradeTier.Antigrav  => 220,
                _                               => 25
            };

            var entryOverride = config != null ? config.FindOverride(tier) : null;
            float configMult = config != null ? Mathf.Max(0.1f, config.priceMultiplier) : 1.0f;
            float markupMult = config != null ? Mathf.Max(1.3f, config.creditsOnlyMarkupMultiplier) : 2.35f;
            bool enableDisc = config != null ? config.enableReputationDiscount : true;
            float maxDisc = config != null ? config.maxReputationDiscountRatio : 0.20f;

            float attFactor = MerchantPriceResolver.CalculateAttitudePriceFactor(npcAttitude, factionRep, enableDisc, maxDisc);
            float finalMult = configMult * attFactor;

            int rawWithResource = (entryOverride != null && entryOverride.customWorkFeeCredits > 0)
                ? entryOverride.customWorkFeeCredits
                : Mathf.RoundToInt(itemBasePrice * tierWorkFactor + tierFlatFee);

            int withResourceCredits = Mathf.Max(5, Mathf.RoundToInt(rawWithResource * finalMult));

            int rawCreditsOnly = (entryOverride != null && entryOverride.customCreditsOnlyPrice > 0)
                ? entryOverride.customCreditsOnlyPrice
                : Mathf.RoundToInt(rawWithResource * markupMult + tierFlatFee * 0.8f);

            int creditsOnlyPrice = Mathf.Max(withResourceCredits + 15, Mathf.RoundToInt(rawCreditsOnly * finalMult));
            return (withResourceCredits, creditsOnlyPrice);
        }

        /// <summary>
        /// Сгенерировать улучшенный экземпляр ЛЮБОГО предмета через ItemVariationGenerator.
        /// Все характеристики гарантированно изменяются в сторону улучшения сообразно выбранному тиру.
        /// </summary>
        public static ItemVariationResult GenerateUpgradedItemResult(
            ItemData item,
            int itemId,
            BlacksmithUpgradeTier tier,
            long seed,
            ItemVariationBaseStats? existingStatsOverride = null)
        {
            ItemVariationBaseStats baseStats = existingStatsOverride ?? ItemVariationProfileFactory.CaptureBaseStats(item);
            if (baseStats.price <= 0)
                baseStats.price = MerchantPriceResolver.EstimateBaseItemPrice(item, 25);
            if (baseStats.weightMilli <= 0)
                baseStats.weightMilli = Mathf.Max(50, Mathf.RoundToInt((item != null ? item.weightKg : 0.1f) * 1000f));
            if (baseStats.durability <= 0)
                baseStats.durability = 20;

            // Для не-боевых предметов (например, яблоко, еда, ресурс, инструмент), у которых damage/armor = 0,
            // создаём профиль улучшения универсальных и полезных статов + гарантированные прибавки тира.
            var profile = BuildBlacksmithVariationProfile(item, itemId, tier, baseStats);

            if (!ItemVariationGenerator.TryRoll(profile, baseStats, seed, out var result, out _))
            {
                // Fallback, если ролл по какой-то причине не сработал
                result = new ItemVariationResult
                {
                    schemaVersion = ItemVariationProfile.CurrentSchemaVersion,
                    profileId = profile.profileId,
                    baseItemId = itemId,
                    baseItemName = profile.baseItemName,
                    seed = seed,
                    baseStats = baseStats,
                    finalStats = baseStats
                };
            }

            // Гарантируем детерминированный бонус выбранного материала кузнеца поверх ролла вариаций,
            // чтобы каждый тир (Бронза < Сталь < Композит < Антигравий) был строго сильнее предыдущего!
            ApplyGuaranteedMaterialBoosts(item, tier, baseStats, ref result.finalStats, out int finalPrice, out int quality);

            string baseCleanName = StripExistingUpgradeSuffix(item != null && !string.IsNullOrEmpty(item.itemName) ? item.itemName : $"Item #{itemId}");
            string tierSuffix = GetTierSuffixTag(tier);
            result.baseItemId = itemId;
            result.baseItemName = baseCleanName;
            result.tierIndex = (int)tier;
            result.tierId = tier.ToString().ToLowerInvariant();
            result.tierName = GetTierDisplayName(tier);
            result.tierColorHex = GetTierColorHex(tier);
            result.displayName = $"{baseCleanName} [{tierSuffix}]";
            result.quality = quality;
            result.finalPrice = finalPrice;
            result.finalStats.price = finalPrice;

            return result;
        }

        public static string StripExistingUpgradeSuffix(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Предмет";
            int idx = name.IndexOf(" [Улучшено", StringComparison.OrdinalIgnoreCase);
            if (idx > 0) return name.Substring(0, idx).TrimEnd();
            return name;
        }

        private static ItemVariationProfile BuildBlacksmithVariationProfile(
            ItemData item,
            int itemId,
            BlacksmithUpgradeTier tier,
            ItemVariationBaseStats baseStats)
        {
            int tierIdx = (int)tier;
            int valMultBps = tier switch
            {
                BlacksmithUpgradeTier.Bronze    => 11500,
                BlacksmithUpgradeTier.Steel     => 13500,
                BlacksmithUpgradeTier.Composite => 16000,
                BlacksmithUpgradeTier.Antigrav  => 19500,
                _                               => 11500
            };
            int priceMultBps = tier switch
            {
                BlacksmithUpgradeTier.Bronze    => 13500,
                BlacksmithUpgradeTier.Steel     => 17500,
                BlacksmithUpgradeTier.Composite => 23000,
                BlacksmithUpgradeTier.Antigrav  => 32000,
                _                               => 13500
            };
            int qualBase = tier switch
            {
                BlacksmithUpgradeTier.Bronze    => 30,
                BlacksmithUpgradeTier.Steel     => 55,
                BlacksmithUpgradeTier.Composite => 75,
                BlacksmithUpgradeTier.Antigrav  => 92,
                _                               => 30
            };

            var profile = new ItemVariationProfile
            {
                schemaVersion = ItemVariationProfile.CurrentSchemaVersion,
                profileId = $"smith_{tier.ToString().ToLowerInvariant()}_{itemId}",
                displayName = item != null ? item.itemName : $"Item #{itemId}",
                baseItemId = itemId,
                baseItemName = item != null ? item.itemName : $"Item #{itemId}",
                seedSalt = (itemId * 397) ^ ((tierIdx + 1) * 7919),
                nameStyle = ItemVariationNameStyle.BaseWithAffixList,
                minRolls = 2,
                maxRolls = 3 + tierIdx,
                baseStats = baseStats,
                tiers = new List<ItemVariationTier>
                {
                    new ItemVariationTier
                    {
                        tierId = tier.ToString().ToLowerInvariant(),
                        displayName = GetTierDisplayName(tier),
                        colorHex = GetTierColorHex(tier),
                        weight = 100,
                        extraRolls = tierIdx,
                        valueMultiplierBps = valMultBps,
                        priceMultiplierBps = priceMultBps,
                        qualityBase = qualBase
                    }
                },
                modifiers = BuildPositiveModifiersForSmith(item, tier)
            };
            return profile;
        }

        private static List<ItemVariationModifier> BuildPositiveModifiersForSmith(ItemData item, BlacksmithUpgradeTier tier)
        {
            var list = new List<ItemVariationModifier>
            {
                new ItemVariationModifier
                {
                    affixId = "smith_value",
                    affixName = GetTierSuffixTag(tier),
                    isSuffix = true,
                    stat = ItemStatId.Price,
                    percentMinBps = 1000,
                    percentMaxBps = 2500,
                    weight = 120,
                    unique = true
                },
                new ItemVariationModifier
                {
                    affixId = "smith_durability",
                    affixName = "Укреплённый",
                    isSuffix = false,
                    stat = ItemStatId.Durability,
                    flatMin = 10,
                    flatMax = 25,
                    weight = 100,
                    unique = true
                },
                new ItemVariationModifier
                {
                    affixId = "smith_str",
                    affixName = "Силовой",
                    isSuffix = false,
                    stat = ItemStatId.StrengthBonus,
                    flatMin = 100,
                    flatMax = 350,
                    weight = 80,
                    unique = true
                },
                new ItemVariationModifier
                {
                    affixId = "smith_dex",
                    affixName = "Сбалансированный",
                    isSuffix = false,
                    stat = ItemStatId.DexterityBonus,
                    flatMin = 100,
                    flatMax = 350,
                    weight = 80,
                    unique = true
                },
                new ItemVariationModifier
                {
                    affixId = "smith_int",
                    affixName = "Резонансный",
                    isSuffix = false,
                    stat = ItemStatId.IntelligenceBonus,
                    flatMin = 100,
                    flatMax = 350,
                    weight = 75,
                    unique = true
                }
            };

            if (tier == BlacksmithUpgradeTier.Composite || tier == BlacksmithUpgradeTier.Antigrav)
            {
                list.Add(new ItemVariationModifier
                {
                    affixId = "smith_light",
                    affixName = "Облегчённый",
                    isSuffix = true,
                    stat = ItemStatId.Weight,
                    percentMinBps = -2500,
                    percentMaxBps = -1000,
                    weight = 110,
                    unique = true
                });
                list.Add(new ItemVariationModifier
                {
                    affixId = "smith_speed",
                    affixName = "Стремительный",
                    isSuffix = false,
                    stat = ItemStatId.MoveSpeed,
                    flatMin = 2,
                    flatMax = 6,
                    weight = 90,
                    unique = true
                });
            }

            if (item is WeaponItemData)
            {
                list.Add(new ItemVariationModifier
                {
                    affixId = "smith_dmg",
                    affixName = "Закалённый",
                    isSuffix = false,
                    stat = ItemStatId.Damage,
                    flatMin = 1,
                    flatMax = 4,
                    percentMinBps = 1000,
                    percentMaxBps = 2500,
                    weight = 150,
                    unique = true
                });
                list.Add(new ItemVariationModifier
                {
                    affixId = "smith_crit",
                    affixName = "Точный",
                    isSuffix = false,
                    stat = ItemStatId.CritChance,
                    flatMin = 2,
                    flatMax = 6,
                    weight = 120,
                    unique = true
                });
            }
            else if (item is ClothingItemData)
            {
                list.Add(new ItemVariationModifier
                {
                    affixId = "smith_armor",
                    affixName = "Бронированный",
                    isSuffix = false,
                    stat = ItemStatId.Armor,
                    flatMin = 1,
                    flatMax = 5,
                    weight = 150,
                    unique = true
                });
            }

            return list;
        }

        private static void ApplyGuaranteedMaterialBoosts(
            ItemData item,
            BlacksmithUpgradeTier tier,
            ItemVariationBaseStats baseStats,
            ref ItemVariationBaseStats finalStats,
            out int finalPrice,
            out int quality)
        {
            int t = (int)tier + 1; // 1=Bronze, 2=Steel, 3=Composite, 4=Antigrav

            // Прочность всегда растёт
            int durBonus = tier switch
            {
                BlacksmithUpgradeTier.Bronze    => 15,
                BlacksmithUpgradeTier.Steel     => 35,
                BlacksmithUpgradeTier.Composite => 65,
                BlacksmithUpgradeTier.Antigrav  => 110,
                _                               => 15
            };
            finalStats.durability = Mathf.Max(finalStats.durability, baseStats.durability + durBonus);

            // Оружие: гарантированный прирост урона, крита и дальности (для композита/антигравия)
            if (item is WeaponItemData || baseStats.damage > 0)
            {
                int minDmgGain = t * 2;
                int pctDmg = Mathf.RoundToInt(baseStats.damage * (0.15f * t));
                finalStats.damage = Mathf.Max(finalStats.damage, baseStats.damage + Mathf.Max(minDmgGain, pctDmg));
                finalStats.critChance = Mathf.Max(finalStats.critChance, baseStats.critChance + t * 2);
                if (tier >= BlacksmithUpgradeTier.Composite && baseStats.rangeMilli > 0)
                {
                    float rangeFactor = tier == BlacksmithUpgradeTier.Antigrav ? 1.25f : 1.15f;
                    finalStats.rangeMilli = Mathf.Max(finalStats.rangeMilli, Mathf.RoundToInt(baseStats.rangeMilli * rangeFactor));
                }
            }

            // Одежда/броня (а также лёгкое укрепление любого предмета)
            if (item is ClothingItemData || baseStats.armor > 0)
            {
                int armorGain = tier switch
                {
                    BlacksmithUpgradeTier.Bronze    => 2,
                    BlacksmithUpgradeTier.Steel     => 4,
                    BlacksmithUpgradeTier.Composite => 7,
                    BlacksmithUpgradeTier.Antigrav  => 11,
                    _                               => 2
                };
                finalStats.armor = Mathf.Max(finalStats.armor, baseStats.armor + armorGain);
            }
            else
            {
                // Даже обычный предмет (хоть яблоко, хоть инструмент) получает укрепление + бонус характеристик
                finalStats.armor = Mathf.Max(finalStats.armor, baseStats.armor + t);
            }

            // Бонусы статов (в milli-единицах: 1000 = +1.0)
            int statBoostMilli = tier switch
            {
                BlacksmithUpgradeTier.Bronze    => 150,
                BlacksmithUpgradeTier.Steel     => 350,
                BlacksmithUpgradeTier.Composite => 650,
                BlacksmithUpgradeTier.Antigrav  => 1200,
                _                               => 150
            };
            finalStats.strengthMilli = Mathf.Max(finalStats.strengthMilli, baseStats.strengthMilli + statBoostMilli);
            if (tier >= BlacksmithUpgradeTier.Steel)
                finalStats.dexterityMilli = Mathf.Max(finalStats.dexterityMilli, baseStats.dexterityMilli + statBoostMilli);
            if (tier >= BlacksmithUpgradeTier.Composite)
                finalStats.intelligenceMilli = Mathf.Max(finalStats.intelligenceMilli, baseStats.intelligenceMilli + statBoostMilli);

            // Скорость и облегчение веса для высоких тиров
            int speedGain = tier switch
            {
                BlacksmithUpgradeTier.Bronze    => 0,
                BlacksmithUpgradeTier.Steel     => 2,
                BlacksmithUpgradeTier.Composite => 5,
                BlacksmithUpgradeTier.Antigrav  => 10,
                _                               => 0
            };
            finalStats.moveSpeed = Mathf.Max(finalStats.moveSpeed, baseStats.moveSpeed + speedGain);

            if (tier == BlacksmithUpgradeTier.Composite)
                finalStats.weightMilli = Mathf.Max(20, Mathf.RoundToInt(baseStats.weightMilli * 0.82f));
            else if (tier == BlacksmithUpgradeTier.Antigrav)
                finalStats.weightMilli = Mathf.Max(10, Mathf.RoundToInt(baseStats.weightMilli * 0.65f));

            float priceFactor = tier switch
            {
                BlacksmithUpgradeTier.Bronze    => 1.35f,
                BlacksmithUpgradeTier.Steel     => 1.75f,
                BlacksmithUpgradeTier.Composite => 2.30f,
                BlacksmithUpgradeTier.Antigrav  => 3.20f,
                _                               => 1.35f
            };
            finalPrice = Mathf.Max(baseStats.price + 10 * t, Mathf.RoundToInt(baseStats.price * priceFactor));
            quality = Mathf.Clamp(25 + t * 18, 1, 100);
        }

        /// <summary>
        /// Сформировать наглядную строку сравнения статов «Было → Станет» для UI Кузнеца.
        /// </summary>
        public static string BuildUpgradeDiffSummary(ItemVariationBaseStats before, ItemVariationBaseStats after)
        {
            var parts = new List<string>();
            if (after.damage != before.damage)
                parts.Add($"⚔ Урон: {before.damage} → {after.damage} (+{after.damage - before.damage})");
            if (after.critChance != before.critChance)
                parts.Add($"🎯 Крит: {before.critChance}% → {after.critChance}% (+{after.critChance - before.critChance}%)");
            if (after.rangeMilli != before.rangeMilli)
                parts.Add($"📏 Дальность: {before.rangeMilli / 1000f:0.#}м → {after.rangeMilli / 1000f:0.#}м");
            if (after.armor != before.armor)
                parts.Add($"🛡 Защита: {before.armor} → {after.armor} (+{after.armor - before.armor})");
            if (after.durability != before.durability)
                parts.Add($"🔨 Прочность: {before.durability} → {after.durability} (+{after.durability - before.durability})");
            if (after.strengthMilli != before.strengthMilli)
                parts.Add($"💪 СИЛ: +{before.strengthMilli / 1000f:0.##} → +{after.strengthMilli / 1000f:0.##}");
            if (after.dexterityMilli != before.dexterityMilli)
                parts.Add($"🏃 ЛОВ: +{before.dexterityMilli / 1000f:0.##} → +{after.dexterityMilli / 1000f:0.##}");
            if (after.intelligenceMilli != before.intelligenceMilli)
                parts.Add($"🧠 ИНТ: +{before.intelligenceMilli / 1000f:0.##} → +{after.intelligenceMilli / 1000f:0.##}");
            if (after.moveSpeed != before.moveSpeed)
                parts.Add($"⚡ Скорость: +{before.moveSpeed}% → +{after.moveSpeed}%");
            if (after.weightMilli != before.weightMilli)
                parts.Add($"⚖ Вес: {before.weightMilli / 1000f:0.##}кг → {after.weightMilli / 1000f:0.##}кг");
            if (after.price != before.price)
                parts.Add($"💰 Ценность: {before.price} → {after.price} CR");

            return string.Join("  •  ", parts);
        }
    }
}
