// =====================================================================================
// ItemVariationGenerator.cs — детерминированный ролл вариаций предметов (T-VAR01)
// =====================================================================================
// Документация:
//   • docs/pickble_items/variation_generator/01_DESIGN.md §3 (алгоритм)
//   • docs/pickble_items/variation_generator/02_API.md (вызов с сервера)
//
// Вход:  профиль + базовые статы + seed (+ удача).
// Выход: ItemVariationResult — тир, аффиксы, итоговые статы, имя, качество, цена.
//
// Гарантии:
//   • Один и тот же (профиль, базовые статы, seed, luck) → одинаковый результат.
//   • Порядок тиров/модификаторов в профиле — часть контракта seed'а. Меняя порядок,
//     вы меняете уже выданные роллы; добавляйте новые аффиксы в конец.
//   • Генератор не бросает исключений на "плохих" данных: TryRoll возвращает false
//     и текст ошибки. Структурно невалидный профиль не должен попасть на сервер.
// =====================================================================================

using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectC.Items.Variation
{
    public static class ItemVariationGenerator
    {
        private const int MaxRollsPerInstance = 64;
        private const int MaxQualityBonus = 12;

        public static bool TryRoll(
            ItemVariationProfile profile,
            ItemVariationBaseStats baseStats,
            long seed,
            out ItemVariationResult result,
            out string error,
            int luckBps = 0)
        {
            result = null;
            error = null;

            if (profile == null)
            {
                error = "Профиль вариации не задан (null).";
                return false;
            }

            if (profile.tiers == null || profile.tiers.Count == 0)
            {
                error = $"Профиль '{profile.profileId}': нет ни одного тира.";
                return false;
            }

            if (profile.modifiers == null)
            {
                error = $"Профиль '{profile.profileId}': список модификаторов null.";
                return false;
            }
            if (profile.modifiers.Count == 0)
            {
                error = $"Профиль '{profile.profileId}': нет ни одного модификатора.";
                return false;
            }

            DeterministicRandom rng = new DeterministicRandom(
                VariationSeed.Derive(seed, profile.profileId, profile.seedSalt));

            // Порядок потребления RNG — часть контракта seed'а: тир → количество роллов →
            // выбор аффиксов → роллы значений. Не менять без версии схемы.
            int tierIndex = SelectTier(profile, luckBps, ref rng, out error);
            if (tierIndex < 0) return false;

            ItemVariationTier tier = profile.tiers[tierIndex];
            int tierMultiplierBps = Math.Max(1, Math.Min(100000, tier.valueMultiplierBps));
            int tierPriceMultiplierBps = Math.Max(0, tier.priceMultiplierBps);

            int minRolls = Math.Max(0, Math.Min(profile.minRolls, profile.maxRolls));
            int maxRolls = Math.Max(minRolls, profile.maxRolls);
            int rollCount = rng.NextIntInclusive(minRolls, maxRolls) + Math.Max(0, tier.extraRolls);
            rollCount = Math.Min(rollCount, MaxRollsPerInstance);

            List<int> rolledModifierIndices = SelectModifiers(profile, tierIndex, rollCount, ref rng);

            var rolls = new List<ItemStatRoll>(rolledModifierIndices.Count);
            for (int i = 0; i < rolledModifierIndices.Count; i++)
            {
                int modifierIndex = rolledModifierIndices[i];
                ItemVariationModifier modifier = profile.modifiers[modifierIndex];

                int flat = 0;
                int percentBps = 0;
                if (modifier.HasFlatRange)
                {
                    int lo = Math.Min(modifier.flatMin, modifier.flatMax);
                    int hi = Math.Max(modifier.flatMin, modifier.flatMax);
                    flat = rng.NextIntInclusive(lo, hi);
                    flat = ItemStatCatalog.MultiplyBps(flat, tierMultiplierBps);
                }
                if (modifier.HasPercentRange)
                {
                    int lo = Math.Min(modifier.percentMinBps, modifier.percentMaxBps);
                    int hi = Math.Max(modifier.percentMinBps, modifier.percentMaxBps);
                    percentBps = rng.NextIntInclusive(lo, hi);
                    percentBps = ItemStatCatalog.MultiplyBps(percentBps, tierMultiplierBps);
                }

                rolls.Add(new ItemStatRoll
                {
                    stat = modifier.stat,
                    flat = flat,
                    percentBps = percentBps,
                    modifierIndex = modifierIndex,
                    tierMultiplierBps = tierMultiplierBps,
                });
            }

            ItemVariationBaseStats finalStats = ApplyRolls(baseStats, rolls);
            int finalPrice = ApplyTierPrice(finalStats.price, tierPriceMultiplierBps);
            finalStats.price = finalPrice;

            string prefix;
            string suffix;
            string displayName = BuildDisplayName(profile, rolls, out prefix, out suffix);

            result = new ItemVariationResult
            {
                schemaVersion = ItemVariationProfile.CurrentSchemaVersion,
                profileId = profile.profileId,
                baseItemId = profile.baseItemId,
                baseItemName = profile.baseItemName,
                seed = seed,
                luckBps = luckBps,
                tierIndex = tierIndex,
                tierId = tier?.tierId,
                tierName = tier?.displayName,
                tierColorHex = tier?.colorHex,
                displayName = displayName,
                prefix = prefix,
                suffix = suffix,
                quality = ComputeQuality(profile, tier, rolls, tierMultiplierBps),
                priceMultiplierBps = tierPriceMultiplierBps,
                finalPrice = finalPrice,
                baseStats = baseStats,
                finalStats = finalStats,
                rolls = rolls,
            };

            return true;
        }

        public static bool TryRoll(
            ItemVariationProfile profile,
            long seed,
            out ItemVariationResult result,
            out string error,
            int luckBps = 0)
        {
            ItemVariationBaseStats baseStats = profile != null
                ? profile.baseStats
                : ItemVariationBaseStats.CreateDefault();
            return TryRoll(profile, baseStats, seed, out result, out error, luckBps);
        }

        public static ItemVariationResult RollOrThrow(
            ItemVariationProfile profile,
            ItemVariationBaseStats baseStats,
            long seed,
            int luckBps = 0)
        {
            if (!TryRoll(profile, baseStats, seed, out ItemVariationResult result, out string error, luckBps))
                throw new InvalidOperationException(error);
            return result;
        }

        /// <summary>Seed, с которым реально работает RNG (для логов/аудита).</summary>
        public static ulong DeriveRngSeed(ItemVariationProfile profile, long seed)
        {
            return profile == null ? 0UL : VariationSeed.Derive(seed, profile.profileId, profile.seedSalt);
        }

        private static int SelectTier(ItemVariationProfile profile, int luckBps, ref DeterministicRandom rng, out string error)
        {
            error = null;
            int count = profile.tiers.Count;
            var weights = new long[count];
            long total = 0L;

            for (int i = 0; i < count; i++)
            {
                ItemVariationTier tier = profile.tiers[i];
                if (tier == null || tier.weight <= 0)
                {
                    weights[i] = 0L;
                    continue;
                }

                int factor = 10000 + luckBps * i;
                if (factor < 0) factor = 0;
                if (factor > 40000) factor = 40000;
                weights[i] = (long)tier.weight * factor / 10000L;
                total += weights[i];
            }

            if (total <= 0L)
            {
                total = 0L;
                for (int i = 0; i < count; i++)
                {
                    ItemVariationTier tier = profile.tiers[i];
                    weights[i] = tier != null && tier.weight > 0 ? tier.weight : 0L;
                    total += weights[i];
                }
            }

            if (total <= 0L)
            {
                error = $"Профиль '{profile.profileId}': у всех тиров вес <= 0.";
                return -1;
            }

            long roll = rng.NextLong(total);
            for (int i = 0; i < count; i++)
            {
                roll -= weights[i];
                if (roll < 0L) return i;
            }
            return count - 1;
        }

        private static List<int> SelectModifiers(
            ItemVariationProfile profile,
            int tierIndex,
            int rollCount,
            ref DeterministicRandom rng)
        {
            var result = new List<int>(rollCount);
            if (rollCount <= 0) return result;

            var pool = new List<int>();
            for (int i = 0; i < profile.modifiers.Count; i++)
            {
                ItemVariationModifier modifier = profile.modifiers[i];
                if (modifier == null) continue;
                if (!modifier.IsAvailableForTier(tierIndex)) continue;
                if (!modifier.HasFlatRange && !modifier.HasPercentRange) continue;
                pool.Add(i);
            }

            if (pool.Count == 0) return result;

            while (result.Count < rollCount && pool.Count > 0)
            {
                long totalWeight = 0L;
                for (int i = 0; i < pool.Count; i++)
                    totalWeight += Math.Max(0, profile.modifiers[pool[i]].weight);

                int pickedPoolIndex;
                if (totalWeight <= 0L)
                {
                    pickedPoolIndex = rng.NextInt(0, pool.Count);
                }
                else
                {
                    long roll = rng.NextLong(totalWeight);
                    pickedPoolIndex = pool.Count - 1;
                    for (int i = 0; i < pool.Count; i++)
                    {
                        int weight = Math.Max(0, profile.modifiers[pool[i]].weight);
                        roll -= weight;
                        if (roll < 0L)
                        {
                            pickedPoolIndex = i;
                            break;
                        }
                    }
                }

                int modifierIndex = pool[pickedPoolIndex];
                result.Add(modifierIndex);

                if (profile.modifiers[modifierIndex].unique)
                    pool.RemoveAt(pickedPoolIndex);
            }

            return result;
        }

        private static ItemVariationBaseStats ApplyRolls(
            ItemVariationBaseStats baseStats,
            List<ItemStatRoll> rolls)
        {
            var flatTotals = new long[ItemStatCatalog.Count];
            var percentTotals = new long[ItemStatCatalog.Count];

            for (int i = 0; i < rolls.Count; i++)
            {
                int index = (int)rolls[i].stat;
                if (index < 0 || index >= ItemStatCatalog.Count) continue;
                flatTotals[index] += rolls[i].flat;
                percentTotals[index] += rolls[i].percentBps;
            }

            ItemVariationBaseStats final = baseStats;
            for (int i = 0; i < ItemStatCatalog.Count; i++)
            {
                ItemStatId stat = (ItemStatId)i;
                final.Set(stat, ItemStatCatalog.Apply(baseStats.Get(stat), flatTotals[i], percentTotals[i]));
            }
            return final;
        }

        private static int ApplyTierPrice(int price, int priceMultiplierBps)
        {
            long scaled = (long)price * Math.Max(0, priceMultiplierBps);
            return ItemStatCatalog.ClampToInt((scaled + 5000L) / 10000L);
        }

        private static int ComputeQuality(
            ItemVariationProfile profile,
            ItemVariationTier tier,
            List<ItemStatRoll> rolls,
            int tierMultiplierBps)
        {
            int baseQuality = tier != null ? tier.qualityBase : 0;
            if (rolls.Count == 0) return Math.Max(0, Math.Min(100, baseQuality));

            double multiplier = Math.Max(1, tierMultiplierBps) / 10000.0;
            double strengthSum = 0.0;

            for (int i = 0; i < rolls.Count; i++)
            {
                int modifierIndex = rolls[i].modifierIndex;
                ItemVariationModifier modifier = modifierIndex >= 0 && modifierIndex < profile.modifiers.Count
                    ? profile.modifiers[modifierIndex]
                    : null;
                if (modifier == null) continue;

                double partSum = 0.0;
                int parts = 0;
                if (modifier.HasFlatRange)
                {
                    double lo = Math.Min(modifier.flatMin, modifier.flatMax) * multiplier;
                    double hi = Math.Max(modifier.flatMin, modifier.flatMax) * multiplier;
                    partSum += Normalized(rolls[i].flat, lo, hi);
                    parts++;
                }
                if (modifier.HasPercentRange)
                {
                    double lo = Math.Min(modifier.percentMinBps, modifier.percentMaxBps) * multiplier;
                    double hi = Math.Max(modifier.percentMinBps, modifier.percentMaxBps) * multiplier;
                    partSum += Normalized(rolls[i].percentBps, lo, hi);
                    parts++;
                }

                if (parts > 0) strengthSum += partSum / parts;
            }

            double average = strengthSum / rolls.Count;
            int bonus = (int)Math.Round(average * MaxQualityBonus, MidpointRounding.AwayFromZero);
            return Math.Max(0, Math.Min(100, baseQuality + bonus));
        }

        private static double Normalized(int value, double lo, double hi)
        {
            if (hi <= lo) return 1.0;
            double t = (value - lo) / (hi - lo);
            return t < 0.0 ? 0.0 : (t > 1.0 ? 1.0 : t);
        }

        private static string BuildDisplayName(
            ItemVariationProfile profile,
            List<ItemStatRoll> rolls,
            out string prefix,
            out string suffix)
        {
            prefix = null;
            suffix = null;

            var affixNames = new List<string>();
            for (int i = 0; i < rolls.Count; i++)
            {
                int modifierIndex = rolls[i].modifierIndex;
                if (modifierIndex < 0 || modifierIndex >= profile.modifiers.Count) continue;
                ItemVariationModifier modifier = profile.modifiers[modifierIndex];
                if (modifier == null || string.IsNullOrEmpty(modifier.affixName)) continue;

                if (!affixNames.Contains(modifier.affixName)) affixNames.Add(modifier.affixName);
                if (modifier.isSuffix)
                {
                    if (suffix == null) suffix = modifier.affixName;
                }
                else if (prefix == null)
                {
                    prefix = modifier.affixName;
                }
            }

            string baseName = !string.IsNullOrEmpty(profile.baseItemName)
                ? profile.baseItemName
                : (!string.IsNullOrEmpty(profile.displayName) ? profile.displayName : "Предмет");

            if (profile.nameStyle == ItemVariationNameStyle.BaseWithAffixList)
            {
                if (affixNames.Count == 0) return baseName;
                var sb = new StringBuilder(baseName.Length + 32);
                sb.Append(baseName).Append(" [");
                for (int i = 0; i < affixNames.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(affixNames[i]);
                }
                sb.Append(']');
                return sb.ToString();
            }

            var builder = new StringBuilder(baseName.Length + 48);
            if (!string.IsNullOrEmpty(prefix)) builder.Append(prefix).Append(' ');
            builder.Append(baseName);
            if (!string.IsNullOrEmpty(suffix)) builder.Append(" · ").Append(suffix);
            return builder.ToString();
        }
    }
}
