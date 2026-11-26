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

        [Tooltip("Максимальная скидка за максимальное отношение/репутацию (0.20 = до 20% скидки).")]
        [Range(0f, 0.5f)]
        public float maxReputationDiscountRatio = 0.20f;

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
        /// Рассчитать коэффициент скидки/наценки от отношения NPC и репутации фракции.
        /// При положительном отношении цена снижается до (1 - maxDiscountRatio),
        /// при отрицательном — увеличивается до +25%.
        /// </summary>
        public static float CalculateAttitudePriceFactor(
            float npcAttitude,
            float factionRep,
            bool enableDiscount,
            float maxDiscountRatio = 0.20f)
        {
            if (!enableDiscount) return 1.0f;
            // Комбинированный рейтинг: отношение NPC (до 200) + репутация фракции (до 100)
            float combinedNormalized = Mathf.Clamp((npcAttitude / 200f) * 0.65f + (factionRep / 100f) * 0.35f, -1f, 1f);
            if (combinedNormalized >= 0f)
            {
                float discount = combinedNormalized * Mathf.Clamp(maxDiscountRatio, 0f, 0.5f);
                return Mathf.Max(0.5f, 1f - discount);
            }
            else
            {
                float penalty = (-combinedNormalized) * 0.25f;
                return 1f + penalty;
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
            float maxDiscountRatio = 0.20f)
        {
            int rawBase = customBuyPrice > 0
                ? customBuyPrice
                : EstimateBaseItemPrice(item, fallbackBasePrice);

            float mult = Mathf.Max(0.1f, tablePriceMultiplier) *
                         CalculateAttitudePriceFactor(npcAttitude, factionRep, enableReputationDiscount, maxDiscountRatio);

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
            int currentBuyPriceLimit = 0)
        {
            int sellPrice;
            if (customSellPrice > 0)
            {
                sellPrice = customSellPrice;
            }
            else
            {
                int baseVal = customBuyPrice > 0
                    ? customBuyPrice
                    : EstimateBaseItemPrice(item, fallbackBasePrice);
                float ratio = Mathf.Clamp(sellBackMultiplier, 0.05f, 0.95f);
                sellPrice = Mathf.Max(1, Mathf.RoundToInt(baseVal * ratio));
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
