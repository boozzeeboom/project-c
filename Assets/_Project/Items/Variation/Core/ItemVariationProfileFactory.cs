// =====================================================================================
// ItemVariationProfileFactory.cs — генерация дефолтных профилей из ItemData (T-VAR01)
// =====================================================================================
// Документация: docs/pickble_items/variation_generator/03_EDITOR_TOOL.md
//
// Назначение:
//   • собрать работоспособный профиль по выбранному ItemData (тиры, аффиксы, базовые статы);
//   • вытащить базовые статы из WeaponItemData / ClothingItemData / ModuleItemData;
//   • валидировать профиль до сохранения/отправки на сервер.
//
// Здесь разрешены UnityEngine и ссылки на ProjectC.Equipment: это авторинг-слой.
// Сам генератор (ItemVariationGenerator) от ItemData не зависит и работает по снапшоту.
// =====================================================================================

using System;
using System.Collections.Generic;
using ProjectC.Equipment;
using UnityEngine;

namespace ProjectC.Items.Variation
{
    public static class ItemVariationProfileFactory
    {
        public const string DefaultProfilePrefix = "var_";

        public static ItemVariationProfile CreateDefault(ItemData item, int baseItemId = 0, string profileId = null)
        {
            var profile = new ItemVariationProfile
            {
                schemaVersion = ItemVariationProfile.CurrentSchemaVersion,
                profileId = string.IsNullOrEmpty(profileId) ? MakeProfileId(item) : profileId,
                displayName = item != null && !string.IsNullOrEmpty(item.itemName) ? item.itemName : "Вариация предмета",
                baseItemId = baseItemId,
                baseItemName = item != null && !string.IsNullOrEmpty(item.itemName) ? item.itemName : "Предмет",
                minRolls = 1,
                maxRolls = 2,
                nameStyle = ItemVariationNameStyle.PrefixSuffix,
                baseStats = CaptureBaseStats(item),
            };
            profile.seedSalt = (int)(VariationSeed.HashString(profile.profileId) & 0x7FFFFFFF);
            profile.tiers = BuildDefaultTiers();
            profile.modifiers = BuildDefaultModifiers(item);
            return profile;
        }

        public static string MakeProfileId(ItemData item)
        {
            string source = item != null && !string.IsNullOrEmpty(item.name) ? item.name : "item";
            var sb = new System.Text.StringBuilder(DefaultProfilePrefix.Length + source.Length);
            sb.Append(DefaultProfilePrefix);
            for (int i = 0; i < source.Length; i++)
            {
                char c = char.ToLowerInvariant(source[i]);
                sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            }
            return sb.ToString();
        }

        public static ItemVariationBaseStats CaptureBaseStats(ItemData item)
        {
            var stats = ItemVariationBaseStats.CreateDefault();
            if (item == null) return stats;

            stats.weightMilli = (int)Math.Round(item.weightKg * 1000f);
            stats.stackSize = Math.Max(1, item.maxStack);
            stats.price = item.basePrice > 0 ? item.basePrice : EstimateBasePrice(item);

            if (item is WeaponItemData weapon)
            {
                stats.damage = weapon.baseDamage;
                stats.critChance = weapon.critModifier;
                float rangeMeters = weapon.handling == WeaponHandling.Thrown ? weapon.throwRange : weapon.range;
                stats.rangeMilli = (int)Math.Round(rangeMeters * 1000f);
            }

            if (item is ClothingItemData clothing)
            {
                stats.armor = clothing.armorDefense;
                stats.strengthMilli = (int)Math.Round(clothing.strengthBonus * 1000f);
                stats.dexterityMilli = (int)Math.Round(clothing.dexterityBonus * 1000f);
                stats.intelligenceMilli = (int)Math.Round(clothing.intelligenceBonus * 1000f);
            }

            if (item is ModuleItemData module)
            {
                stats.strengthMilli = (int)Math.Round(module.strengthBonus * 1000f);
                stats.dexterityMilli = (int)Math.Round(module.dexterityBonus * 1000f);
                stats.intelligenceMilli = (int)Math.Round(module.intelligenceBonus * 1000f);
            }

            return stats;
        }

        /// <summary>Копирует базовые статы и идентичность из ItemData в уже существующий профиль.</summary>
        public static void CaptureInto(ItemVariationProfile profile, ItemData item, int baseItemId = -1)
        {
            if (profile == null) return;
            profile.baseStats = CaptureBaseStats(item);
            if (item == null) return;

            profile.baseItemName = item.itemName;
            if (baseItemId >= 0) profile.baseItemId = baseItemId;
            if (string.IsNullOrEmpty(profile.profileId))
                profile.profileId = MakeProfileId(item);
            if (string.IsNullOrEmpty(profile.displayName))
                profile.displayName = item.itemName;
        }

        /// <summary>Оценка цены, если ItemData.basePrice == 0. Не является источником истины экономики.</summary>
        public static int EstimateBasePrice(ItemData item)
        {
            if (item == null) return 10;
            if (item.basePrice > 0) return item.basePrice;

            if (item is WeaponItemData weapon)
            {
                int dice = (int)weapon.damageDice;
                return Math.Max(50,
                    Math.Max(1, weapon.minTier + 1) * 40
                    + Math.Max(1, weapon.baseDamage) * 25
                    + dice * 10
                    + (weapon.range >= 10f ? 60 : 0));
            }

            if (item is ClothingItemData clothing)
            {
                return Math.Max(40,
                    Math.Max(1, clothing.tier) * 60
                    + clothing.armorDefense * 20
                    + (int)Math.Round(
                        (Math.Abs(clothing.strengthBonus) + Math.Abs(clothing.dexterityBonus) + Math.Abs(clothing.intelligenceBonus)) * 30f));
            }

            if (item is ModuleItemData module)
            {
                return Math.Max(75,
                    Math.Max(1, module.tier) * 90
                    + (int)Math.Round(
                        (Math.Abs(module.strengthBonus) + Math.Abs(module.dexterityBonus) + Math.Abs(module.intelligenceBonus)) * 35f
                        + module.weaponDamageBonus * 25f));
            }

            switch (item.itemType)
            {
                case ItemType.Meziy: return 120;
                case ItemType.Antigrav: return 95;
                case ItemType.Tech: return 65;
                case ItemType.Medical: return 45;
                case ItemType.Equipment: return 50;
                case ItemType.Fuel: return 40;
                case ItemType.Resources: return 25;
                case ItemType.Food: return 20;
                case ItemType.Key: return 500;
                default: return 10;
            }
        }

        public static List<ItemVariationTier> BuildDefaultTiers()
        {
            return new List<ItemVariationTier>
            {
                new ItemVariationTier { tierId = "common",    displayName = "Обычный",    colorHex = "#B8C0CC", weight = 60, extraRolls = 0, valueMultiplierBps = 10000, priceMultiplierBps = 10000, qualityBase = 5 },
                new ItemVariationTier { tierId = "uncommon",  displayName = "Необычный",  colorHex = "#6FCF6F", weight = 25, extraRolls = 1, valueMultiplierBps = 11000, priceMultiplierBps = 12500, qualityBase = 25 },
                new ItemVariationTier { tierId = "rare",      displayName = "Редкий",     colorHex = "#4FA3FF", weight = 10, extraRolls = 2, valueMultiplierBps = 12500, priceMultiplierBps = 16000, qualityBase = 50 },
                new ItemVariationTier { tierId = "epic",      displayName = "Эпический",  colorHex = "#B36BFF", weight = 4,  extraRolls = 3, valueMultiplierBps = 14500, priceMultiplierBps = 21000, qualityBase = 70 },
                new ItemVariationTier { tierId = "legendary", displayName = "Легендарный", colorHex = "#FFB020", weight = 1,  extraRolls = 4, valueMultiplierBps = 17000, priceMultiplierBps = 28000, qualityBase = 88 },
            };
        }

        public static List<ItemVariationModifier> BuildDefaultModifiers(ItemData item)
        {
            var modifiers = new List<ItemVariationModifier>
            {
                // Базовая тройка — подходит любому pickable-предмету.
                Percent("valuable", "Ценный", true, ItemStatId.Price, 500, 2500),
                Flat("light", "Лёгкий", true, ItemStatId.Weight, -200, -50),
            };

            bool isEquipment = item != null && item.itemType == ItemType.Equipment;
            if (isEquipment)
                modifiers.Add(Flat("durable", "Прочный", false, ItemStatId.Durability, 10, 40));

            if (item is WeaponItemData)
            {
                modifiers.Add(Percent("sharpened", "Закалённый", false, ItemStatId.Damage, 500, 1500));
                modifiers.Add(Flat("accurate", "Меткий", false, ItemStatId.CritChance, 1, 5));
                modifiers.Add(Percent("longrange", "Дальнобойный", false, ItemStatId.Range, 500, 2000));
            }

            if (item is ClothingItemData)
            {
                modifiers.Add(Flat("reinforced", "Укреплённый", false, ItemStatId.Armor, 1, 5));
                modifiers.Add(Flat("strong", "Сильный", false, ItemStatId.StrengthBonus, 100, 500));
                modifiers.Add(Flat("nimble", "Ловкий", false, ItemStatId.DexterityBonus, 100, 500));
                modifiers.Add(Flat("clever", "Сметливый", false, ItemStatId.IntelligenceBonus, 100, 500));
            }

            if (item is ModuleItemData)
            {
                modifiers.Add(Flat("strong", "Сильный", false, ItemStatId.StrengthBonus, 100, 500));
                modifiers.Add(Flat("nimble", "Ловкий", false, ItemStatId.DexterityBonus, 100, 500));
                modifiers.Add(Flat("clever", "Сметливый", false, ItemStatId.IntelligenceBonus, 100, 500));
            }

            return modifiers;
        }

        public static List<ItemVariationIssue> Validate(ItemVariationProfile profile)
        {
            var issues = new List<ItemVariationIssue>();
            if (profile == null)
            {
                issues.Add(new ItemVariationIssue(ItemVariationSeverity.Error, "Профиль == null."));
                return issues;
            }

            if (string.IsNullOrWhiteSpace(profile.profileId))
                issues.Add(new ItemVariationIssue(ItemVariationSeverity.Error, "profileId пуст — сервер не сможет найти профиль."));
            else if (profile.profileId.IndexOf(' ') >= 0)
                issues.Add(new ItemVariationIssue(ItemVariationSeverity.Warning, "profileId содержит пробелы; лучше использовать ASCII slug."));

            if (profile.tiers == null || profile.tiers.Count == 0)
            {
                issues.Add(new ItemVariationIssue(ItemVariationSeverity.Error, "Нет тиров — ролл невозможен."));
            }
            else
            {
                long totalWeight = 0L;
                for (int i = 0; i < profile.tiers.Count; i++)
                {
                    ItemVariationTier tier = profile.tiers[i];
                    if (tier == null)
                    {
                        issues.Add(new ItemVariationIssue(ItemVariationSeverity.Warning, $"Тир [{i}] == null."));
                        continue;
                    }
                    if (tier.weight > 0) totalWeight += tier.weight;
                    if (tier.valueMultiplierBps <= 0)
                        issues.Add(new ItemVariationIssue(ItemVariationSeverity.Warning, $"Тир '{tier.tierId}': valueMultiplierBps <= 0."));
                    if (tier.priceMultiplierBps <= 0)
                        issues.Add(new ItemVariationIssue(ItemVariationSeverity.Warning, $"Тир '{tier.tierId}': priceMultiplierBps <= 0 — предмет будет стоить 0."));
                    if (tier.extraRolls < 0)
                        issues.Add(new ItemVariationIssue(ItemVariationSeverity.Warning, $"Тир '{tier.tierId}': extraRolls < 0."));
                }
                if (totalWeight <= 0L)
                    issues.Add(new ItemVariationIssue(ItemVariationSeverity.Error, "У всех тиров weight <= 0."));
            }

            if (profile.modifiers == null || profile.modifiers.Count == 0)
            {
                issues.Add(new ItemVariationIssue(ItemVariationSeverity.Error, "Нет модификаторов — вариация будет пустой."));
            }
            else
            {
                int usable = 0;
                for (int i = 0; i < profile.modifiers.Count; i++)
                {
                    ItemVariationModifier modifier = profile.modifiers[i];
                    if (modifier == null)
                    {
                        issues.Add(new ItemVariationIssue(ItemVariationSeverity.Warning, $"Модификатор [{i}] == null."));
                        continue;
                    }

                    if (!modifier.HasFlatRange && !modifier.HasPercentRange)
                    {
                        issues.Add(new ItemVariationIssue(ItemVariationSeverity.Warning, $"Аффикс '{modifier.affixId}' не имеет диапазона — никогда не выпадет."));
                        continue;
                    }
                    usable++;

                    if (modifier.flatMin > modifier.flatMax)
                        issues.Add(new ItemVariationIssue(ItemVariationSeverity.Warning, $"Аффикс '{modifier.affixId}': flatMin > flatMax (генератор нормализует)."));
                    if (modifier.percentMinBps > modifier.percentMaxBps)
                        issues.Add(new ItemVariationIssue(ItemVariationSeverity.Warning, $"Аффикс '{modifier.affixId}': percentMinBps > percentMaxBps (генератор нормализует)."));
                    if (modifier.weight <= 0)
                        issues.Add(new ItemVariationIssue(ItemVariationSeverity.Warning, $"Аффикс '{modifier.affixId}': weight <= 0 — выпадает только при отсутствии других."));
                    if (string.IsNullOrEmpty(modifier.affixName))
                        issues.Add(new ItemVariationIssue(ItemVariationSeverity.Info, $"Аффикс '{modifier.affixId}' без имени — не попадёт в название."));
                    if (modifier.minTierIndex > modifier.maxTierIndex)
                        issues.Add(new ItemVariationIssue(ItemVariationSeverity.Warning, $"Аффикс '{modifier.affixId}': minTierIndex > maxTierIndex."));
                }

                if (usable == 0)
                    issues.Add(new ItemVariationIssue(ItemVariationSeverity.Error, "Ни один модификатор не имеет ролл-диапазона."));
            }

            if (profile.minRolls > profile.maxRolls)
                issues.Add(new ItemVariationIssue(ItemVariationSeverity.Warning, "minRolls > maxRolls (генератор нормализует)."));
            if (profile.minRolls < 0)
                issues.Add(new ItemVariationIssue(ItemVariationSeverity.Warning, "minRolls < 0."));

            ItemVariationBaseStats b = profile.baseStats;
            if (b.damage == 0 && b.critChance == 0 && b.rangeMilli == 0 && b.weightMilli == 0
                && b.price == 0 && b.durability == 0 && b.armor == 0 && b.strengthMilli == 0
                && b.dexterityMilli == 0 && b.intelligenceMilli == 0 && b.moveSpeed == 0)
            {
                issues.Add(new ItemVariationIssue(ItemVariationSeverity.Warning, "Все базовые статы нулевые — проверьте, что профиль снят с ItemData."));
            }

            return issues;
        }

        public static bool HasErrors(List<ItemVariationIssue> issues)
        {
            if (issues == null) return false;
            for (int i = 0; i < issues.Count; i++)
                if (issues[i].severity == ItemVariationSeverity.Error) return true;
            return false;
        }

        private static ItemVariationModifier Flat(
            string affixId,
            string affixName,
            bool isSuffix,
            ItemStatId stat,
            int min,
            int max)
        {
            return new ItemVariationModifier
            {
                affixId = affixId,
                affixName = affixName,
                isSuffix = isSuffix,
                stat = stat,
                flatMin = min,
                flatMax = max,
                percentMinBps = 0,
                percentMaxBps = 0,
                weight = 100,
                minTierIndex = 0,
                maxTierIndex = 99,
                unique = true,
            };
        }

        private static ItemVariationModifier Percent(
            string affixId,
            string affixName,
            bool isSuffix,
            ItemStatId stat,
            int minBps,
            int maxBps)
        {
            return new ItemVariationModifier
            {
                affixId = affixId,
                affixName = affixName,
                isSuffix = isSuffix,
                stat = stat,
                flatMin = 0,
                flatMax = 0,
                percentMinBps = minBps,
                percentMaxBps = maxBps,
                weight = 100,
                minTierIndex = 0,
                maxTierIndex = 99,
                unique = true,
            };
        }
    }
}
