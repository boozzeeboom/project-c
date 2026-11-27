// T-MERCH01: NpcMerchantTable — ScriptableObject личного ассортимента торговца для именных/квестовых NPC.
// См. docs/NPC_quests/merchants/00_NPC_MERCHANT_GUIDE.md

using System;
using System.Text;
using UnityEngine;
using ProjectC.Items;
using ProjectC.Equipment;

namespace ProjectC.Quests
{
    /// <summary>
    /// T-MERCH-REP01: Ступень торговых отношений игрока с NPC и его фракцией.
    /// Определяется по сводному индексу доверия StandingScore (-100..+100) и порогам враждебности.
    /// </summary>
    public enum MerchantStandingTier : byte
    {
        /// <summary>Полный бойкот / отказ от торговли (❤ &lt;= -50, ⚑ &lt;= -60 или индекс &lt;= -50).</summary>
        Refused    = 0,
        /// <summary>Изгой / Презираемый (индекс -49..-30): наценка +50%..+100%, скупка за бесценок.</summary>
        Despised   = 1,
        /// <summary>Недружелюбный / Подозрительный (индекс -29..-5): наценка +5%..+50%.</summary>
        Unfriendly = 2,
        /// <summary>Нейтральный (индекс -5..+10): от +5% наценки до -10% скидки (при 0 — базовая цена ×1.00).</summary>
        Neutral    = 3,
        /// <summary>Приятель / Постоянный клиент (индекс +10..+30): скидка -10%..-25%.</summary>
        Friendly   = 4,
        /// <summary>Уважаемый партнёр (индекс +30..+55): скидка -25%..-45%.</summary>
        Honored    = 5,
        /// <summary>Почитаемый союзник (индекс +55..+80): скидка -45%..-62%.</summary>
        Revered    = 6,
        /// <summary>Свой человек / Легенда гильдии (индекс +80..+100): скидка -62%..-75%.</summary>
        Exalted    = 7
    }

    /// <summary>
    /// Запись одного товара в личной таблице торговца NPC.
    /// </summary>
    [Serializable]
    public class NpcMerchantItemEntry
    {
        [Tooltip("Ссылка на Pickable Item (ItemData из Resources/Items / ItemRegistry).")]
        public ItemData item;

        [Tooltip("Цена покупки у NPC в кредитах (CR). Если <= 0 — вычисляется автоматически от ItemData.basePrice или характеристик предмета.")]
        [Min(0)]
        public int customBuyPrice = 0;

        [Tooltip("Цена продажи этого предмета торговцу (CR). Если <= 0 — вычисляется как BuyPrice * sellBackPriceMultiplier.")]
        [Min(0)]
        public int customSellPrice = 0;

        [Tooltip("Запас товара у NPC на сервере. -1 = бесконечный запас; > 0 = лимитированное количество.")]
        public int stock = -1;

        [Tooltip("Минимальное личное отношение NPC (NpcAttitude, -100..200) для покупки этого предмета.")]
        [Range(-100, 200)]
        public int minNpcAttitude = -100;

        [Tooltip("Минимальная репутация с фракцией NPC (-100..100) для покупки этого предмета.")]
        [Range(-100, 100)]
        public int minFactionReputation = -100;

        [Tooltip("Опциональный квест, который должен быть выполнен (Completed/TurnedIn), чтобы товар стал доступен.")]
        public QuestDefinition requiredQuest;

        [Tooltip("Fallback ID квеста (если requiredQuest не привязан напрямую).")]
        public string requiredQuestId = "";

        public string GetRequiredQuestId()
        {
            if (requiredQuest != null && !string.IsNullOrEmpty(requiredQuest.questId))
                return requiredQuest.questId;
            return requiredQuestId ?? "";
        }
    }

    /// <summary>
    /// Персональный прайс-лист и настройки лавки для именного/квестового NPC.
    /// Назначается в <see cref="NpcDefinition.merchantTable"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "MerchantTable_", menuName = "ProjectC/Quests/NPC Merchant Table", order = 105)]
    public class NpcMerchantTable : ScriptableObject
    {
        [Header("Shop Identity")]
        [Tooltip("Опциональное название лавки в шапке правого блока диалога (если пусто — используется имя NPC).")]
        public string shopTitle = "";

        [Header("Pricing & Buyback")]
        [Tooltip("Множитель цен продажи у этого NPC (1.0 = стандартная цена, 1.25 = дорогой торговец, 0.85 = оптовик).")]
        [Range(0.1f, 5f)]
        public float priceMultiplier = 1.0f;

        [Tooltip("Может ли игрок продавать этому NPC предметы из своего инвентаря.")]
        public bool allowPlayerSellToNpc = true;

        [Tooltip("Доля от базовой стоимости предмета, которую NPC платит при скупке у игрока (по умолчанию 0.5 = 50%).")]
        [Range(0.05f, 1.0f)]
        public float sellBackPriceMultiplier = 0.5f;

        [Tooltip("Если включено — высокое отношение с NPC и репутация фракции дают скидку на покупку (до maxReputationDiscountRatio).")]
        public bool enableReputationDiscount = true;

        [Tooltip("Максимальная скидка за максимальное отношение с NPC (+200) и репутацию фракции (+100). По умолчанию 0.75 = до 75% скидки.")]
        [Range(0f, 0.75f)]
        public float maxReputationDiscountRatio = MerchantPriceResolver.DefaultMaxDiscountRatio;

        [Tooltip("Максимальная наценка при сильно отрицательной репутации перед полным отказом от торговли (1.00 = до +100% к цене, т.е. ×2.0).")]
        [Range(0f, 2.0f)]
        public float maxReputationMarkupRatio = MerchantPriceResolver.DefaultMaxMarkupRatio;

        [Header("Trade Refusal (Negative Reputation Embargo)")]
        [Tooltip("Если включено — при сильной вражде NPC или фракции торговец полностью отказывается торговать с игроком.")]
        public bool refuseTradeOnLowReputation = true;

        [Tooltip("Порог личного отношения NPC (❤), на котором и ниже которого торговец отказывается торговать (по умолчанию -50, совпадает с порогом агрессии NpcBrain).")]
        [Range(-100f, 0f)]
        public float refuseTradeNpcAttitudeThreshold = MerchantPriceResolver.DefaultRefuseNpcAttitude;

        [Tooltip("Порог репутации фракции NPC (⚑), на котором и ниже которого объявляется фракционный бойкот (по умолчанию -60).")]
        [Range(-100f, 0f)]
        public float refuseTradeFactionRepThreshold = MerchantPriceResolver.DefaultRefuseFactionRep;

        [Tooltip("Порог сводного индекса торгового доверия (-100..+100), на котором и ниже которого торговец прекращает торговлю (по умолчанию -50).")]
        [Range(-100f, 0f)]
        public float refuseTradeStandingScoreThreshold = MerchantPriceResolver.DefaultRefuseStandingScore;

        [HideInInspector]
        [SerializeField]
        private int _pricingSchemaVersion = 2;

        /// <summary>
        /// Эффективный потолок скидки с учётом бесшовной миграции таблиц T-MERCH01/T-MERCH80 (где был старый дефолт 0.20).
        /// </summary>
        public float EffectiveMaxDiscountRatio
        {
            get
            {
                if (_pricingSchemaVersion < 2 && Mathf.Approximately(maxReputationDiscountRatio, 0.20f))
                    return MerchantPriceResolver.DefaultMaxDiscountRatio;
                return Mathf.Clamp(maxReputationDiscountRatio, 0f, MerchantPriceResolver.MaxAllowedDiscountRatio);
            }
        }

        private void OnValidate()
        {
            if (_pricingSchemaVersion < 2)
            {
                if (Mathf.Approximately(maxReputationDiscountRatio, 0.20f))
                    maxReputationDiscountRatio = MerchantPriceResolver.DefaultMaxDiscountRatio;
                _pricingSchemaVersion = 2;
            }
        }

        [Header("Assortment (Personal Item Table)")]
        [Tooltip("Список предметов, которые продаёт этот NPC.")]
        public NpcMerchantItemEntry[] items = Array.Empty<NpcMerchantItemEntry>();
    }

    /// <summary>
    /// Единый калькулятор цен и описания характеристик для магазинов NPC (сервер + редактор + клиент).
    /// </summary>
    public static class MerchantPriceResolver
    {
        /// <summary>
        /// Константа скорости роста репутации с NPC и его фракцией:
        /// +0.1 за каждые 100 CR, полученных NPC от игрока (= 0.001 за 1 CR).
        /// </summary>
        public const float ReputationPer100Credits = 0.1f;
        public const float ReputationPerCredit = ReputationPer100Credits / 100f; // 0.001f

        /// <summary>Максимальная скидка по умолчанию при пиковой репутации (75%).</summary>
        public const float DefaultMaxDiscountRatio = 0.75f;
        public const float MaxAllowedDiscountRatio = 0.75f;

        /// <summary>Максимальная наценка по умолчанию на грани бойкота (+100%, т.е. ×2.0).</summary>
        public const float DefaultMaxMarkupRatio = 1.00f;

        /// <summary>Канонические пороги отказа от торговли при отрицательной репутации.</summary>
        public const float DefaultRefuseNpcAttitude   = -50f;
        public const float DefaultRefuseFactionRep    = -60f;
        public const float DefaultRefuseStandingScore = -50f;

        /// <summary>Веса личного отношения NPC (60%) и репутации фракции (40%) в сводном индексе доверия.</summary>
        public const float NpcAttitudeWeight = 0.60f;
        public const float FactionRepWeight  = 0.40f;

        /// <summary>
        /// Вычислить точный прирост репутации/отношения за потраченные игроком кредиты.
        /// </summary>
        public static float CalculateReputationGain(int creditsSpentByPlayer)
        {
            if (creditsSpentByPlayer <= 0) return 0f;
            return creditsSpentByPlayer * ReputationPerCredit;
        }

        /// <summary>
        /// Оценить базовую цену предмета в кредитах (если customBuyPrice == 0).
        /// </summary>
        public static int EstimateBaseItemPrice(ItemData item, int fallbackPrice = 25)
        {
            if (item == null) return Mathf.Max(1, fallbackPrice);
            if (item.basePrice > 0) return item.basePrice;

            // Оружие: оцениваем по классу, кубику, базовому урону и тиру
            if (item is WeaponItemData weapon)
            {
                int diceVal = (int)weapon.damageDice;
                int tierBonus = Mathf.Max(1, weapon.minTier + 1) * 40;
                int dmgBonus = Mathf.Max(1, weapon.baseDamage) * 25 + diceVal * 10;
                int rangeBonus = weapon.range >= 10f ? 60 : 0;
                return Mathf.Max(50, tierBonus + dmgBonus + rangeBonus);
            }

            // Одежда и броня: оцениваем по тиру, броне и бонусам характеристик
            if (item is ClothingItemData clothing)
            {
                int tierBase = Mathf.Max(1, clothing.tier) * 60;
                int armorVal = clothing.armorDefense * 20;
                int statVal = Mathf.RoundToInt(
                    (Mathf.Abs(clothing.strengthBonus) + Mathf.Abs(clothing.dexterityBonus) + Mathf.Abs(clothing.intelligenceBonus)) * 30f +
                    (clothing.strengthMultiplier + clothing.dexterityMultiplier + clothing.intelligenceMultiplier) * 150f);
                return Mathf.Max(40, tierBase + armorVal + statVal);
            }

            // Персонажные модули/импланты: оцениваем по тиру и бонусам
            if (item is ModuleItemData module)
            {
                int tierBase = Mathf.Max(1, module.tier) * 90;
                int statVal = Mathf.RoundToInt(
                    (Mathf.Abs(module.strengthBonus) + Mathf.Abs(module.dexterityBonus) + Mathf.Abs(module.intelligenceBonus)) * 35f +
                    module.weaponDamageBonus * 25f +
                    module.craftingSpeedMultiplier * 100f +
                    module.sensorRangeBonus * 5f);
                return Mathf.Max(75, tierBase + statVal);
            }

            // Обычные предметы по категории ItemType
            int categoryBase = item.itemType switch
            {
                ItemType.Meziy     => 120,
                ItemType.Antigrav  => 95,
                ItemType.Tech      => 65,
                ItemType.Medical   => 45,
                ItemType.Equipment => 50,
                ItemType.Fuel      => 40,
                ItemType.Resources => 25,
                ItemType.Food      => 18,
                ItemType.Key       => 500,
                _                  => fallbackPrice > 0 ? fallbackPrice : 25
            };

            int weightBonus = Mathf.Clamp(Mathf.RoundToInt(item.weightKg * 8f), 0, 80);
            return Mathf.Max(5, categoryBase + weightBonus);
        }

        /// <summary>
        /// T-MERCH-REP01: Нормализовать личное отношение NPC (-100..+200) к шкале [-100..+100].
        /// При положительном отношении +200 даёт +100; при отрицательном -100 даёт -100.
        /// </summary>
        public static float NormalizeNpcAttitude(float npcAttitude)
        {
            if (npcAttitude >= 0f)
                return Mathf.Clamp(npcAttitude / 200f, 0f, 1f) * 100f;
            return Mathf.Clamp(npcAttitude, -100f, 0f);
        }

        /// <summary>
        /// T-MERCH-REP01: Вычислить сводный индекс торгового доверия в диапазоне [-100..+100].
        /// Учитывает 60% личного отношения с NPC (нормированного к -100..+100) и 40% репутации фракции (-100..+100).
        /// </summary>
        public static float CalculateStandingScore(float npcAttitude, float factionRep)
        {
            float normNpc = NormalizeNpcAttitude(npcAttitude);
            float normFac = Mathf.Clamp(factionRep, -100f, 100f);
            return Mathf.Clamp(normNpc * NpcAttitudeWeight + normFac * FactionRepWeight, -100f, 100f);
        }

        /// <summary>
        /// T-MERCH-REP01: Проверить, отказывается ли торговец/кузнец вести дела с игроком из-за сильной вражды.
        /// Отказ наступает при пробитии любого из трёх порогов:
        /// 1) Личное отношение ❤ &lt;= refuseNpcAttitudeThreshold (по умолчанию -50);
        /// 2) Репутация фракции ⚑ &lt;= refuseFactionRepThreshold (по умолчанию -60);
        /// 3) Сводный индекс доверия &lt;= refuseStandingScoreThreshold (по умолчанию -50).
        /// </summary>
        public static (bool isRefused, string reason) EvaluateTradeRefusal(
            float npcAttitude,
            float factionRep,
            string factionDisplayName = "",
            bool enableRefusal = true,
            float refuseNpcAttitudeThreshold = DefaultRefuseNpcAttitude,
            float refuseFactionRepThreshold = DefaultRefuseFactionRep,
            float refuseStandingScoreThreshold = DefaultRefuseStandingScore)
        {
            if (!enableRefusal)
                return (false, "");

            float eps = 1e-3f;
            if (npcAttitude <= refuseNpcAttitudeThreshold + eps)
            {
                return (true,
                    $"Торговец враждебен к вам (отношение ❤ {npcAttitude:0.0} ≤ {refuseNpcAttitudeThreshold:0}). Торговля прекращена.");
            }

            if (factionRep <= refuseFactionRepThreshold + eps)
            {
                string facLabel = string.IsNullOrEmpty(factionDisplayName) ? "фракции" : $"фракции «{factionDisplayName}»";
                return (true,
                    $"Бойкот {facLabel} (репутация ⚑ {factionRep:0.0} ≤ {refuseFactionRepThreshold:0}). Торговец отказывается иметь с вами дело.");
            }

            float standingScore = CalculateStandingScore(npcAttitude, factionRep);
            if (standingScore <= refuseStandingScoreThreshold + eps)
            {
                return (true,
                    $"Слишком дурная репутация (индекс доверия {standingScore:0.0} ≤ {refuseStandingScoreThreshold:0}). Торговец не ведёт с вами дел.");
            }

            return (false, "");
        }

        /// <summary>
        /// T-MERCH-REP01: Определить текущую ступень отношений с торговцем по отношению NPC и репутации фракции.
        /// </summary>
        public static MerchantStandingTier ResolveStandingTier(
            float npcAttitude,
            float factionRep,
            bool enableRefusal = true,
            float refuseNpcAttitudeThreshold = DefaultRefuseNpcAttitude,
            float refuseFactionRepThreshold = DefaultRefuseFactionRep,
            float refuseStandingScoreThreshold = DefaultRefuseStandingScore)
        {
            var (isRefused, _) = EvaluateTradeRefusal(
                npcAttitude,
                factionRep,
                "",
                enableRefusal,
                refuseNpcAttitudeThreshold,
                refuseFactionRepThreshold,
                refuseStandingScoreThreshold);

            if (isRefused)
                return MerchantStandingTier.Refused;

            float score = CalculateStandingScore(npcAttitude, factionRep);
            if (score <= -30f) return MerchantStandingTier.Despised;
            if (score <= -5f)  return MerchantStandingTier.Unfriendly;
            if (score < 10f)   return MerchantStandingTier.Neutral;
            if (score < 30f)   return MerchantStandingTier.Friendly;
            if (score < 55f)   return MerchantStandingTier.Honored;
            if (score < 80f)   return MerchantStandingTier.Revered;
            return MerchantStandingTier.Exalted;
        }

        /// <summary>
        /// T-MERCH-REP01: Человекочитаемое название ступени отношений с торговцем.
        /// </summary>
        public static string GetStandingTierDisplayName(MerchantStandingTier tier)
        {
            return tier switch
            {
                MerchantStandingTier.Refused    => "🚫 Бойкот (Отказ)",
                MerchantStandingTier.Despised   => "⚠ Изгой",
                MerchantStandingTier.Unfriendly => "Недружелюбный",
                MerchantStandingTier.Neutral    => "Нейтральный",
                MerchantStandingTier.Friendly   => "Приятель",
                MerchantStandingTier.Honored    => "Уважаемый",
                MerchantStandingTier.Revered    => "Почитаемый",
                MerchantStandingTier.Exalted    => "★ Свой человек",
                _                               => "Нейтральный"
            };
        }

        /// <summary>
        /// Рассчитать коэффициент скидки/наценки от отношения NPC и репутации фракции.
        /// T-MERCH-REP01: Кусочно-линейная кривая по ступеням доверия:
        /// - При положительном индексе (0..+100): плавная скидка по тирам вплоть до -75% (множитель 0.25):
        ///   Neutral (0..10) → 0%..-10% | Friendly (10..30) → -10%..-25% | Honored (30..55) → -25%..-45% |
        ///   Revered (55..80) → -45%..-62% | Exalted (80..100) → -62%..-75%.
        /// - При отрицательном индексе (0..-50): плавная наценка по тирам вплоть до +100% (множитель 2.00):
        ///   Neutral (0..-5) → 0%..+5% | Unfriendly (-5..-30) → +5%..+50% | Despised (-30..-50) → +50%..+100%.
        /// </summary>
        public static float CalculateAttitudePriceFactor(
            float npcAttitude,
            float factionRep,
            bool enableDiscount,
            float maxDiscountRatio = DefaultMaxDiscountRatio,
            float maxMarkupRatio = DefaultMaxMarkupRatio)
        {
            if (!enableDiscount) return 1.0f;

            float score = CalculateStandingScore(npcAttitude, factionRep);
            if (score >= 0f)
            {
                // Каноническая кривая скидки (при эталонном потолке 0.75):
                //   0  -> 0.00 (0%)
                //   10 -> 0.10 (-10%, Приятель)
                //   30 -> 0.25 (-25%, Уважаемый)
                //   55 -> 0.45 (-45%, Почитаемый)
                //   80 -> 0.62 (-62%, Свой человек)
                //   100-> 0.75 (-75%, Максимум)
                float canonicalDiscount;
                if (score <= 10f)
                    canonicalDiscount = Mathf.Lerp(0f, 0.10f, score / 10f);
                else if (score <= 30f)
                    canonicalDiscount = Mathf.Lerp(0.10f, 0.25f, (score - 10f) / 20f);
                else if (score <= 55f)
                    canonicalDiscount = Mathf.Lerp(0.25f, 0.45f, (score - 30f) / 25f);
                else if (score <= 80f)
                    canonicalDiscount = Mathf.Lerp(0.45f, 0.62f, (score - 55f) / 25f);
                else
                    canonicalDiscount = Mathf.Lerp(0.62f, DefaultMaxDiscountRatio, (score - 80f) / 20f);

                float clampedMaxDisc = Mathf.Clamp(maxDiscountRatio, 0f, MaxAllowedDiscountRatio);
                float scale = DefaultMaxDiscountRatio > 1e-4f ? (clampedMaxDisc / DefaultMaxDiscountRatio) : 0f;
                float finalDiscount = Mathf.Clamp(canonicalDiscount * scale, 0f, MaxAllowedDiscountRatio);
                return Mathf.Max(1f - MaxAllowedDiscountRatio, 1f - finalDiscount);
            }
            else
            {
                // Каноническая кривая наценки при плохой репутации (0..-50, до порога бойкота):
                //   0   -> +0%
                //  -5   -> +5%  (Недружелюбный)
                //  -30  -> +50% (Изгой / Презираемый)
                //  -50  -> +100% (×2.0 на пороге отказа от торговли)
                float neg = Mathf.Clamp(-score, 0f, 50f);
                float canonicalMarkup;
                if (neg <= 5f)
                    canonicalMarkup = Mathf.Lerp(0f, 0.05f, neg / 5f);
                else if (neg <= 30f)
                    canonicalMarkup = Mathf.Lerp(0.05f, 0.50f, (neg - 5f) / 25f);
                else
                    canonicalMarkup = Mathf.Lerp(0.50f, DefaultMaxMarkupRatio, (neg - 30f) / 20f);

                float clampedMaxMarkup = Mathf.Clamp(maxMarkupRatio, 0f, 2.0f);
                float scale = DefaultMaxMarkupRatio > 1e-4f ? (clampedMaxMarkup / DefaultMaxMarkupRatio) : 0f;
                float finalMarkup = Mathf.Clamp(canonicalMarkup * scale, 0f, 2.0f);
                return 1f + finalMarkup;
            }
        }

        /// <summary>
        /// T-MERCH-REP01: Коэффициент влияния репутации на цену скупки предметов у игрока.
        /// При отрицательной репутации торговец занижает выплату вплоть до ×0.50 от базовой скупки;
        /// при высокой репутации повышает ставку скупки до ×1.30 (но итоговая цена скупки всегда
        /// остаётся строго ниже текущей цены покупки этого предмета у торговца, исключая абуз перепродажи).
        /// </summary>
        public static float CalculateAttitudeSellBackFactor(
            float npcAttitude,
            float factionRep,
            bool enableDiscount)
        {
            if (!enableDiscount) return 1.0f;
            float score = CalculateStandingScore(npcAttitude, factionRep);
            if (score >= 0f)
            {
                return Mathf.Lerp(1.0f, 1.30f, Mathf.Clamp01(score / 100f));
            }
            else
            {
                float negT = Mathf.Clamp01((-score) / 50f);
                return Mathf.Lerp(1.0f, 0.50f, negT);
            }
        }

        /// <summary>
        /// Вычислить итоговую цену покупки 1 шт. предмета у NPC.
        /// </summary>
        public static int ResolveBuyPrice(
            ItemData item,
            int customBuyPrice,
            float tablePriceMultiplier,
            int fallbackBasePrice,
            float npcAttitude,
            float factionRep,
            bool enableReputationDiscount,
            float maxDiscountRatio = DefaultMaxDiscountRatio,
            float maxMarkupRatio = DefaultMaxMarkupRatio)
        {
            int rawBase = customBuyPrice > 0
                ? customBuyPrice
                : EstimateBaseItemPrice(item, fallbackBasePrice);

            float mult = Mathf.Max(0.1f, tablePriceMultiplier) *
                         CalculateAttitudePriceFactor(npcAttitude, factionRep, enableReputationDiscount, maxDiscountRatio, maxMarkupRatio);

            return Mathf.Max(1, Mathf.RoundToInt(rawBase * mult));
        }

        /// <summary>
        /// Вычислить итоговую цену продажи 1 шт. предмета игроком этому NPC.
        /// Всегда не выше цены покупки (защита от эксплойта бесконечной перепродажи).
        /// </summary>
        public static int ResolveSellPrice(
            ItemData item,
            int customBuyPrice,
            int customSellPrice,
            float sellBackMultiplier,
            int fallbackBasePrice,
            int currentBuyPriceLimit = 0,
            float npcAttitude = 0f,
            float factionRep = 0f,
            bool enableReputationDiscount = false)
        {
            int sellPrice;
            float repSellFactor = CalculateAttitudeSellBackFactor(npcAttitude, factionRep, enableReputationDiscount);
            if (customSellPrice > 0)
            {
                sellPrice = Mathf.Max(1, Mathf.RoundToInt(customSellPrice * repSellFactor));
            }
            else
            {
                int baseVal = customBuyPrice > 0
                    ? customBuyPrice
                    : EstimateBaseItemPrice(item, fallbackBasePrice);
                float ratio = Mathf.Clamp(sellBackMultiplier, 0.05f, 0.95f);
                sellPrice = Mathf.Max(1, Mathf.RoundToInt(baseVal * ratio * repSellFactor));
            }

            if (currentBuyPriceLimit > 0 && sellPrice >= currentBuyPriceLimit)
            {
                sellPrice = Mathf.Max(1, currentBuyPriceLimit - 1);
            }
            return Mathf.Max(1, sellPrice);
        }

        /// <summary>
        /// Сформировать строку характеристик предмета для тултипа при наведении в магазине.
        /// </summary>
        public static string BuildItemSpecsSummary(ItemData item)
        {
            if (item == null) return "";
            var sb = new StringBuilder();
            sb.Append($"Категория: {ItemTypeNames.GetDisplayName(item.itemType)}  •  Вес: {item.weightKg:0.##} кг");
            if (item.equipSlot != EquipSlot.None)
                sb.Append($"  •  Слот: {item.equipSlot}");

            if (item is WeaponItemData weapon)
            {
                sb.AppendLine();
                sb.Append($"⚔ Оружие ({weapon.weaponClass}): урон 1{weapon.damageDice}+{weapon.baseDamage} [{weapon.damageType}], дальность {weapon.range:0.#} м");
                if (weapon.critModifier != 0)
                    sb.Append($", крит {(weapon.critModifier > 0 ? "+" : "")}{weapon.critModifier}%");
                if (weapon.minTier > 0)
                    sb.Append($", мин. тир {weapon.minTier}");
            }
            else if (item is ClothingItemData clothing)
            {
                sb.AppendLine();
                sb.Append($"🛡 Экипировка (Тир {clothing.tier}): защита {clothing.armorDefense}");
                if (clothing.strengthBonus != 0) sb.Append($", СИЛ {(clothing.strengthBonus > 0 ? "+" : "")}{clothing.strengthBonus:0.#}");
                if (clothing.dexterityBonus != 0) sb.Append($", ЛОВ {(clothing.dexterityBonus > 0 ? "+" : "")}{clothing.dexterityBonus:0.#}");
                if (clothing.intelligenceBonus != 0) sb.Append($", ИНТ {(clothing.intelligenceBonus > 0 ? "+" : "")}{clothing.intelligenceBonus:0.#}");
            }
            else if (item is ModuleItemData mod)
            {
                sb.AppendLine();
                sb.Append($"⚙ Имплант ({mod.moduleType}, Тир {mod.tier})");
                if (mod.strengthBonus != 0) sb.Append($", СИЛ +{mod.strengthBonus:0.#}");
                if (mod.dexterityBonus != 0) sb.Append($", ЛОВ +{mod.dexterityBonus:0.#}");
                if (mod.intelligenceBonus != 0) sb.Append($", ИНТ +{mod.intelligenceBonus:0.#}");
                if (mod.weaponDamageBonus != 0) sb.Append($", урон +{mod.weaponDamageBonus:0.#}");
                if (mod.craftingSpeedMultiplier != 0) sb.Append($", крафт x{1f + mod.craftingSpeedMultiplier:0.##}");
                if (mod.sensorRangeBonus != 0) sb.Append($", сенсор +{mod.sensorRangeBonus:0.#}м");
            }

            return sb.ToString();
        }
    }
}
