// =====================================================================================
// ItemVariationSelfTest.cs — проверка генератора без Play Mode (T-VAR01)
// =====================================================================================
// Документация: docs/pickble_items/variation_generator/03_EDITOR_TOOL.md §5
//
// Проверяет:
//   1. Один seed → одинаковый JSON (детерминизм).
//   2. Все роллы попадают в диапазоны аффиксов с учётом множителя тира.
//   3. Итоговые статы неотрицательны, качество 0..100, цена неотрицательна.
//   4. Разные seeds дают разные названия (если аффиксов достаточно).
//   5. Структура профиля валидна (нет ошибок).
//
// Запуск: кнопка Self-test в окне или Tools → Project C → Items → Variation Self-Test.
// =====================================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using ProjectC.Equipment;
using ProjectC.Items.Variation.Dto;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Items.Variation.EditorTools
{
    public static class ItemVariationSelfTest
    {
        private const int DefaultIterations = 200;

        [MenuItem("Tools/Project C/Items/Variation Smoke Test (in-memory)", priority = 212)]
        public static void RunSyntheticFromMenu()
        {
            string report = RunSyntheticReport();
            Debug.Log("[ItemVariationSmoke]\n" + report);
            EditorUtility.DisplayDialog(
                report.Contains("[FAIL]") ? "Variation Smoke Test: FAIL" : "Variation Smoke Test: PASS",
                report.Length > 3500 ? report.Substring(0, 3500) + "\n…" : report,
                "OK");
        }

        /// <summary>Смоук-тест без ассетов и без диалога: возвращает отчёт (для CI/execute_code).</summary>
        public static string RunSyntheticReport(int iterations = 120)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponItemData>();
            weapon.itemName = "Smoke Test Blade";
            weapon.itemType = ItemType.Equipment;
            weapon.baseDamage = 12;
            weapon.critModifier = 5;
            weapon.range = 2f;
            weapon.weightKg = 1.5f;
            weapon.maxStack = 1;

            try
            {
                ItemVariationProfile profile = ItemVariationProfileFactory.CreateDefault(weapon, 900001);
                var report = new StringBuilder();
                report.AppendLine(Run(profile, iterations));

                bool profileRoundtrip = ItemVariationJson.TryProfileFromJson(
                    ItemVariationJson.ProfileToJson(profile), out ItemVariationProfile loaded, out string profileError);
                report.AppendLine(profileRoundtrip ? "PASS: profile JSON round-trip." : "FAIL: profile JSON: " + profileError);

                var service = new ItemVariationService();
                bool registered = service.RegisterProfile(profileRoundtrip ? loaded : profile, out string registerError);
                report.AppendLine(registered ? "PASS: service.RegisterProfile." : "FAIL: RegisterProfile: " + registerError);

                var created = new List<ItemInstanceDto>();
                for (int i = 0; i < 5 && registered; i++)
                {
                    if (service.TryCreate(profile.profileId, 7000L + i, out ItemInstanceDto dto, out string createError))
                        created.Add(dto);
                    else
                        report.AppendLine("FAIL: TryCreate: " + createError);
                }
                report.AppendLine(created.Count == 5 ? "PASS: service.TryCreate x5." : "FAIL: created " + created.Count + "/5.");

                string instancesJson = service.ExportInstancesJson();
                var restored = new ItemVariationService();
                bool imported = restored.ImportInstancesJson(instancesJson, out string importError);
                report.AppendLine(imported ? "PASS: instances JSON import." : "FAIL: import: " + importError);

                if (imported)
                {
                    bool same = restored.InstanceCount == created.Count;
                    for (int i = 0; i < created.Count && same; i++)
                    {
                        if (!restored.TryGetInstance(created[i].instanceId, out ItemInstanceDto dto))
                            same = false;
                        else if (dto.seed != created[i].seed
                            || dto.finalPrice != created[i].finalPrice
                            || dto.stats.damage != created[i].stats.damage
                            || dto.displayName != created[i].displayName)
                            same = false;
                    }
                    report.AppendLine(same ? "PASS: restored instances match." : "FAIL: restored instances differ.");
                }

                return report.ToString();
            }
            finally
            {
                Object.DestroyImmediate(weapon);
            }
        }

        [MenuItem("Tools/Project C/Items/Variation Self-Test (all profiles)", priority = 211)]
        public static void RunAllFromMenu()
        {
            string[] guids = AssetDatabase.FindAssets("t:ItemVariationProfileAsset");
            if (guids == null || guids.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Variation Self-Test",
                    "В проекте нет ни одного ItemVariationProfileAsset.\nСоздайте профиль в Tools → Project C → Items → Variation Generator.",
                    "OK");
                return;
            }

            var report = new StringBuilder();
            int failed = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<ItemVariationProfileAsset>(path);
                if (asset == null || asset.profile == null) continue;

                string result = Run(asset.profile);
                bool isFail = result.Contains("[FAIL]");
                if (isFail) failed++;

                report.AppendLine("=== " + path + " ===");
                report.AppendLine(result);
                report.AppendLine();
            }

            string text = report.ToString();
            Debug.Log("[ItemVariationSelfTest]\n" + text);

            string dialog = text.Length > 3500 ? text.Substring(0, 3500) + "\n… (полный отчёт в Console)" : text;
            EditorUtility.DisplayDialog(
                failed == 0 ? "Variation Self-Test: PASS" : $"Variation Self-Test: {failed} FAIL",
                dialog,
                "OK");
        }

        public static string Run(ItemVariationProfile profile, int iterations = DefaultIterations)
        {
            var report = new StringBuilder();
            var failures = new List<string>();
            var warnings = new List<string>();

            if (profile == null)
            {
                return "[FAIL] Профиль == null.";
            }

            List<ItemVariationIssue> issues = ItemVariationProfileFactory.Validate(profile);
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].severity == ItemVariationSeverity.Error)
                    failures.Add("Валидация: " + issues[i].message);
                else if (issues[i].severity == ItemVariationSeverity.Warning)
                    warnings.Add("Валидация: " + issues[i].message);
            }

            long seed = 4242424242L;
            bool firstOk = ItemVariationGenerator.TryRoll(profile, seed, out ItemVariationResult first, out string firstError);
            bool secondOk = ItemVariationGenerator.TryRoll(profile, seed, out ItemVariationResult second, out string secondError);

            if (!firstOk || !secondOk)
            {
                failures.Add("Ролл не удался: " + (firstError ?? secondError));
            }
            else
            {
                string firstJson = ItemVariationJson.ResultToJson(first);
                string secondJson = ItemVariationJson.ResultToJson(second);
                if (firstJson != secondJson)
                    failures.Add("Детерминизм: один seed дал разный JSON.");
                if (!first.baseStats.Equals(profile.baseStats))
                    failures.Add("Базовые статы изменились во время ролла.");
            }

            int maxExtraRolls = 0;
            int normalizedMinRolls = 0;
            int normalizedMaxRolls = 0;
            if (profile.tiers != null && profile.tiers.Count > 0)
            {
                normalizedMinRolls = Mathf.Max(0, Mathf.Min(profile.minRolls, profile.maxRolls));
                normalizedMaxRolls = Mathf.Max(normalizedMinRolls, profile.maxRolls);
                for (int i = 0; i < profile.tiers.Count; i++)
                {
                    ItemVariationTier tier = profile.tiers[i];
                    if (tier != null && tier.extraRolls > maxExtraRolls) maxExtraRolls = tier.extraRolls;
                }
            }
            int expectedMaxRolls = Mathf.Min(64, normalizedMaxRolls + maxExtraRolls);

            var names = new HashSet<string>();
            int rolled = 0;

            for (int i = 0; i < iterations; i++)
            {
                long iterationSeed = unchecked(1000003L + (long)i * 7919L);
                if (!ItemVariationGenerator.TryRoll(
                    profile, iterationSeed, out ItemVariationResult result, out string error))
                {
                    failures.Add($"Seed {iterationSeed}: {error}");
                    break;
                }
                rolled++;

                if (result.finalPrice < 0) failures.Add($"Seed {iterationSeed}: negative price.");
                if (result.finalPrice != result.finalStats.price)
                    failures.Add($"Seed {iterationSeed}: finalPrice != finalStats.price.");
                if (result.quality < 0 || result.quality > 100)
                    failures.Add($"Seed {iterationSeed}: quality {result.quality} вне 0..100.");
                if (result.rolls.Count > expectedMaxRolls)
                    failures.Add($"Seed {iterationSeed}: {result.rolls.Count} роллов > максимума {expectedMaxRolls}.");
                if (string.IsNullOrEmpty(result.displayName))
                    failures.Add($"Seed {iterationSeed}: пустое имя.");
                names.Add(result.displayName);

                if (result.tierIndex < 0 || result.tierIndex >= profile.tiers.Count)
                {
                    failures.Add($"Seed {iterationSeed}: tierIndex {result.tierIndex} вне диапазона.");
                    continue;
                }

                ItemVariationTier tier = profile.tiers[result.tierIndex];
                int multiplier = Mathf.Clamp(tier.valueMultiplierBps, 1, 100000);
                for (int r = 0; r < result.rolls.Count; r++)
                {
                    ItemStatRoll roll = result.rolls[r];
                    if (roll.modifierIndex < 0 || roll.modifierIndex >= profile.modifiers.Count)
                    {
                        failures.Add($"Seed {iterationSeed}: roll.modifierIndex {roll.modifierIndex} вне диапазона.");
                        continue;
                    }

                    ItemVariationModifier modifier = profile.modifiers[roll.modifierIndex];
                    if (modifier == null)
                    {
                        failures.Add($"Seed {iterationSeed}: modifier[{roll.modifierIndex}] == null.");
                        continue;
                    }
                    if (!modifier.IsAvailableForTier(result.tierIndex))
                        failures.Add($"Seed {iterationSeed}: аффикс '{modifier.affixId}' недоступен для тира {tier.tierId}.");

                    if (modifier.HasFlatRange)
                    {
                        int lo = ItemStatCatalog.MultiplyBps(Mathf.Min(modifier.flatMin, modifier.flatMax), multiplier);
                        int hi = ItemStatCatalog.MultiplyBps(Mathf.Max(modifier.flatMin, modifier.flatMax), multiplier);
                        if (roll.flat < lo - 1 || roll.flat > hi + 1)
                            failures.Add($"Seed {iterationSeed}: '{modifier.affixId}' flat {roll.flat} вне [{lo}..{hi}].");
                    }
                    else if (roll.flat != 0)
                    {
                        failures.Add($"Seed {iterationSeed}: '{modifier.affixId}' flat {roll.flat}, но диапазона нет.");
                    }

                    if (modifier.HasPercentRange)
                    {
                        int lo = ItemStatCatalog.MultiplyBps(Mathf.Min(modifier.percentMinBps, modifier.percentMaxBps), multiplier);
                        int hi = ItemStatCatalog.MultiplyBps(Mathf.Max(modifier.percentMinBps, modifier.percentMaxBps), multiplier);
                        if (roll.percentBps < lo - 1 || roll.percentBps > hi + 1)
                            failures.Add($"Seed {iterationSeed}: '{modifier.affixId}' percent {roll.percentBps} вне [{lo}..{hi}].");
                    }
                    else if (roll.percentBps != 0)
                    {
                        failures.Add($"Seed {iterationSeed}: '{modifier.affixId}' percent {roll.percentBps}, но диапазона нет.");
                    }
                }
            }

            if (names.Count < 2 && rolled >= 20)
                warnings.Add("Все seeds дали одинаковое имя — аффиксов/диапазонов слишком мало для вариативности.");
            if (firstOk && secondOk)
            {
                bool different = ItemVariationGenerator.TryRoll(
                    profile, seed + 1L, out ItemVariationResult other, out _);
                if (different && ItemVariationJson.ResultToJson(first) == ItemVariationJson.ResultToJson(other))
                    warnings.Add("Соседний seed дал тот же результат — проверьте веса/диапазоны.");
            }

            report.AppendLine(failures.Count == 0 ? "[PASS]" : "[FAIL]");
            report.AppendLine($"profile: {profile.profileId}");
            report.AppendLine($"iterations: {rolled}/{iterations}, distinct names: {names.Count}");
            report.AppendLine($"rolls range: {normalizedMinRolls}..{expectedMaxRolls}");

            for (int i = 0; i < failures.Count && i < 40; i++) report.AppendLine("FAIL: " + failures[i]);
            for (int i = 0; i < warnings.Count && i < 20; i++) report.AppendLine("WARN: " + warnings[i]);
            if (failures.Count > 40) report.AppendLine($"… ещё {failures.Count - 40} FAIL (см. консоль/профиль).");

            return report.ToString();
        }
    }
}
#endif
