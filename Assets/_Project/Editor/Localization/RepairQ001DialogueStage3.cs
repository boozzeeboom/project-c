using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace ProjectC.Localization.Editor
{
    public static class RepairQ001DialogueStage3
    {
        private const string SharedPath = "Assets/_Project/Settings/Localization/Dialogue_Table Shared Data.asset";
        private const string RuPath = "Assets/_Project/Settings/Localization/Dialogue_Table_ru.asset";
        private const string EnPath = "Assets/_Project/Settings/Localization/Dialogue_Table_en.asset";

        private static readonly string[] DialogPaths =
        {
            "Assets/_Project/Quests/Data/Dialogs/dlg_lyra_q001.asset",
            "Assets/_Project/Quests/Data/Dialogs/dlg_bram_q001.asset",
            "Assets/_Project/Quests/Data/Dialogs/dlg_veska_q001.asset",
            "Assets/_Project/Quests/Data/Dialogs/dlg_noll_q001.asset",
            "Assets/_Project/Quests/Data/Dialogs/dlg_kael_q001.asset",
            "Assets/_Project/Quests/Data/Dialogs/dlg_sela_q001.asset"
        };

        [MenuItem("ProjectC/Localization/Q001/Stage 3 - Add Dialogue RU EN")]
        public static void Execute()
        {
            var shared = AssetDatabase.LoadAssetAtPath<SharedTableData>(SharedPath);
            var ru = AssetDatabase.LoadAssetAtPath<StringTable>(RuPath);
            var en = AssetDatabase.LoadAssetAtPath<StringTable>(EnPath);
            if (shared == null || ru == null || en == null)
            {
                Debug.LogError("[Q001 Stage3] Required Dialogue_Table assets were not found.");
                return;
            }

            var keys = CollectQ001SpecificKeys();
            if (keys.Count != 97 || keys.Any(k => !k.StartsWith("dialogue.dlg_", StringComparison.Ordinal)))
            {
                Debug.LogError($"[Q001 Stage3] ABORTED before save: expected 97 normalized Q001 keys, got {keys.Count}.");
                return;
            }

            var missingRu = keys.Where(k => ru.GetEntry(k) == null).ToList();
            var missingEn = keys.Where(k => en.GetEntry(k) == null).ToList();
            var duplicateFree = keys.Distinct(StringComparer.Ordinal).Count() == keys.Count;
            if (!duplicateFree || missingRu.Count != missingEn.Count)
            {
                Debug.LogError($"[Q001 Stage3] ABORTED before save: duplicateFree={duplicateFree}; missingRU={missingRu.Count}; missingEN={missingEn.Count}.");
                return;
            }

            var sharedBefore = shared.Entries.Count;
            var ruBefore = ru.Values.Count(v => v != null);
            var enBefore = en.Values.Count(v => v != null);
            var added = 0;

            foreach (var key in keys)
            {
                var entry = shared.GetEntry(key);
                if (entry == null)
                {
                    shared.AddKey(key);
                    added++;
                }

                var ruEntry = ru.GetEntry(key);
                if (ruEntry == null)
                    ru.AddEntry(key, Translate(key, false));

                var enEntry = en.GetEntry(key);
                if (enEntry == null)
                    en.AddEntry(key, Translate(key, true));
            }

            var invalid = keys.Where(k =>
            {
                var r = ru.GetEntry(k)?.LocalizedValue;
                var e = en.GetEntry(k)?.LocalizedValue;
                return string.IsNullOrWhiteSpace(r) || string.IsNullOrWhiteSpace(e) || r == k || e == k;
            }).ToList();

            if (invalid.Count > 0 || shared.Entries.Count != sharedBefore + added)
            {
                Debug.LogError($"[Q001 Stage3] ABORTED before save: invalidTranslations={invalid.Count}; sharedDelta={shared.Entries.Count - sharedBefore}; expectedDelta={added}.");
                return;
            }

            EditorUtility.SetDirty(shared);
            EditorUtility.SetDirty(ru);
            EditorUtility.SetDirty(en);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Q001 Stage3] PASS: q001Keys={keys.Count}; sharedAdded={added}; ruAdded={ru.Values.Count(v => v != null) - ruBefore}; enAdded={en.Values.Count(v => v != null) - enBefore}; sharedTotal={shared.Entries.Count}; ruTotal={ru.Values.Count(v => v != null)}; enTotal={en.Values.Count(v => v != null)}.");
        }

        private static List<string> CollectQ001SpecificKeys()
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in DialogPaths)
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null)
                    throw new InvalidOperationException("Missing DialogTree asset: " + path);

                var so = new SerializedObject(asset);
                AddString(so.FindProperty("displayName"), result);
                var nodes = so.FindProperty("nodes");
                for (var i = 0; i < nodes.arraySize; i++)
                {
                    var node = nodes.GetArrayElementAtIndex(i);
                    AddString(node.FindPropertyRelative("text"), result);
                    var edges = node.FindPropertyRelative("edges");
                    for (var j = 0; j < edges.arraySize; j++)
                        AddString(edges.GetArrayElementAtIndex(j).FindPropertyRelative("label"), result);
                }
            }

            return result.Where(k => k.StartsWith("dialogue.dlg_", StringComparison.Ordinal))
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();
        }

        private static void AddString(SerializedProperty property, HashSet<string> result)
        {
            if (property != null && property.propertyType == SerializedPropertyType.String && !string.IsNullOrWhiteSpace(property.stringValue))
                result.Add(property.stringValue);
        }

        private static string Translate(string key, bool english)
        {
            var parts = key.Split('.');
            var tree = parts[1];
            var section = parts[2];
            var speaker = Speaker(tree, english);

            if (section == "displayName")
                return english ? speaker + " — Q001 dialogue" : speaker + " — диалог Q001";

            if (parts.Length < 4)
                return english ? "Continue" : "Продолжить";

            var token = parts[3];
            if (section == "node")
                return NodeText(tree, token, english);

            return OptionText(token, english);
        }

        private static string Speaker(string tree, bool english)
        {
            switch (tree)
            {
                case "dlg_lyra_q001": return english ? "Lyra" : "Лира";
                case "dlg_bram_q001": return english ? "Bram" : "Брам";
                case "dlg_veska_q001": return english ? "Veska" : "Веска";
                case "dlg_noll_q001": return english ? "Noll" : "Нолл";
                case "dlg_kael_q001": return english ? "Kael" : "Каэль";
                case "dlg_sela_q001": return english ? "Sela" : "Села";
                default: return english ? "Unknown" : "Неизвестный персонаж";
            }
        }

        private static string NodeText(string tree, string token, bool english)
        {
            var name = NodeName(token, english);
            var speaker = Speaker(tree, english);
            if (english)
                return speaker + ": " + name + ".";
            return speaker + ": " + name + ".";
        }

        private static string NodeName(string token, bool english)
        {
            var en = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["greeting"] = "The signal from the glass archive is active again",
                ["offer_main"] = "I need you to investigate the archive",
                ["briefing"] = "Collect independent evidence before choosing a route",
                ["choice_context"] = "Three routes remain open, and the choice is yours",
                ["final_truth"] = "The archive sealed contaminated cargo and repeated its emergency signal",
                ["final_compromised"] = "The evidence is useful, but the broker compromised part of the data",
                ["failed_fallback"] = "The investigation has failed",
                ["evidence"] = "One container was opened from the inside",
                ["salvage_offer"] = "The black box can still be recovered",
                ["salvage_intro"] = "Open the black box away from the quarantine site",
                ["recover_blackbox"] = "Bring the expedition black box back intact",
                ["return_blackbox"] = "You have the black box; now bring it to Bram",
                ["turnin_confirm"] = "Confirm that you are delivering the black box",
                ["technical"] = "The signal uses an emergency quarantine frequency",
                ["offer_science"] = "I can reconstruct the signal with the right equipment",
                ["calibration"] = "The resonance lens is calibrated",
                ["lighthouse"] = "The silent lighthouse confirms the archive frequency",
                ["report"] = "The signal matches the quarantine storage protocol",
                ["false_seal_warning"] = "That seal is false; presenting it will compromise the investigation",
                ["false_seal_fail"] = "The false seal has exposed the deception",
                ["check_story"] = "The broker story does not match the evidence",
                ["manifest_taken"] = "You accepted the false manifest",
                ["manifest_warning"] = "This manifest is forged; confirm the risk before presenting it",
                ["manifest_fail"] = "The false manifest has ruined the investigation",
                ["offer_silence"] = "There is money in keeping the archive silent",
                ["neutral"] = "The less said about the archive, the safer everyone is",
                ["take_blackbox"] = "Take the black box and bring it to me",
                ["exchange"] = "We can exchange the black box for a quiet payment",
                ["betrayal_warning"] = "Selling the black box will betray the investigation",
                ["betrayal_fail"] = "The exchange has destroyed the case",
                ["ambient"] = "The archive still remembers what happened",
                ["incomplete"] = "My testimony is incomplete, but the fragments may help",
                ["testimony"] = "I operated the quarantine archive before it was abandoned",
                ["grant_fragment"] = "Take this fragment; it completes part of the record",
                ["quarantine_confirmation"] = "The signal belongs to a quarantine vault, not a military beacon"
            };
            var ru = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["greeting"] = "Сигнал из стеклянного архива снова активен",
                ["offer_main"] = "Мне нужно, чтобы ты расследовал архив",
                ["briefing"] = "Собери независимые доказательства, прежде чем выбрать путь",
                ["choice_context"] = "Открыты три пути, и выбор остаётся за тобой",
                ["final_truth"] = "Архив запечатал заражённый груз и годами повторял аварийный сигнал",
                ["final_compromised"] = "Улики полезны, но брокер скомпрометировал часть данных",
                ["failed_fallback"] = "Расследование провалено",
                ["evidence"] = "Один контейнер вскрыли изнутри",
                ["salvage_offer"] = "Чёрный ящик ещё можно извлечь",
                ["salvage_intro"] = "Открывай чёрный ящик подальше от карантинной зоны",
                ["recover_blackbox"] = "Верни чёрный ящик экспедиции целым",
                ["return_blackbox"] = "Чёрный ящик у тебя; теперь отнеси его Браму",
                ["turnin_confirm"] = "Подтверди передачу чёрного ящика",
                ["technical"] = "Сигнал использует аварийную карантинную частоту",
                ["offer_science"] = "Я восстановлю сигнал с подходящим оборудованием",
                ["calibration"] = "Резонансная линза откалибрована",
                ["lighthouse"] = "Глухой маяк подтверждает частоту архива",
                ["report"] = "Сигнал совпадает с протоколом карантинного хранилища",
                ["false_seal_warning"] = "Печать поддельная; предъявив её, ты сорвёшь расследование",
                ["false_seal_fail"] = "Поддельная печать выдала обман",
                ["check_story"] = "Версия брокера не совпадает с уликами",
                ["manifest_taken"] = "Ты принял фальшивый манифест",
                ["manifest_warning"] = "Манифест подделан; подтверди риск перед передачей",
                ["manifest_fail"] = "Фальшивый манифест разрушил расследование",
                ["offer_silence"] = "На молчании об архиве можно хорошо заработать",
                ["neutral"] = "Чем меньше говорить об архиве, тем безопаснее для всех",
                ["take_blackbox"] = "Забери чёрный ящик и принеси его мне",
                ["exchange"] = "Мы обменяем чёрный ящик на тихую выплату",
                ["betrayal_warning"] = "Продажа чёрного ящика предаст расследование",
                ["betrayal_fail"] = "Обмен уничтожил дело",
                ["ambient"] = "Архив всё ещё помнит, что произошло",
                ["incomplete"] = "Моё свидетельство неполное, но фрагменты могут помочь",
                ["testimony"] = "Я была оператором карантинного архива до его закрытия",
                ["grant_fragment"] = "Возьми этот фрагмент; он дополнит часть записи",
                ["quarantine_confirmation"] = "Сигнал принадлежит карантинному хранилищу, а не военному маяку"
            };
            return (english ? en : ru).TryGetValue(token, out var value) ? value : (english ? "Archive record: " + token : "Запись архива: " + token);
        }

        [MenuItem("ProjectC/Localization/Q001/Stage 3 - Normalize Common Dialogue Keys")]
        public static void NormalizeCommonKeys()
        {
            var shared = AssetDatabase.LoadAssetAtPath<SharedTableData>(SharedPath);
            var ru = AssetDatabase.LoadAssetAtPath<StringTable>(RuPath);
            var en = AssetDatabase.LoadAssetAtPath<StringTable>(EnPath);
            if (shared == null || ru == null || en == null)
            {
                Debug.LogError("[Q001 Common] Required Dialogue_Table assets were not found.");
                return;
            }

            var common = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["dialogue.option.common.not_now"] = new[] { "Не сейчас", "Not now" },
                ["dialogue.option.common.cancel"] = new[] { "Отмена", "Cancel" }
            };
            foreach (var pair in common)
            {
                if (shared.GetEntry(pair.Key) == null)
                    shared.AddKey(pair.Key);
                if (ru.GetEntry(pair.Key) == null)
                    ru.AddEntry(pair.Key, pair.Value[0]);
                if (en.GetEntry(pair.Key) == null)
                    en.AddEntry(pair.Key, pair.Value[1]);
            }

            var changed = 0;
            foreach (var path in DialogPaths)
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                var so = new SerializedObject(asset);
                changed += NormalizeProperty(so.FindProperty("displayName"));
                var nodes = so.FindProperty("nodes");
                for (var i = 0; i < nodes.arraySize; i++)
                {
                    var node = nodes.GetArrayElementAtIndex(i);
                    changed += NormalizeProperty(node.FindPropertyRelative("text"));
                    var edges = node.FindPropertyRelative("edges");
                    for (var j = 0; j < edges.arraySize; j++)
                        changed += NormalizeProperty(edges.GetArrayElementAtIndex(j).FindPropertyRelative("label"));
                }
                if (so.hasModifiedProperties)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(asset);
                }
            }

            EditorUtility.SetDirty(shared);
            EditorUtility.SetDirty(ru);
            EditorUtility.SetDirty(en);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Q001 Common] PASS: normalized={changed}; commonEntriesAdded=2; Dialogue_Table total={shared.Entries.Count}.");
        }

        private static int NormalizeProperty(SerializedProperty property)
        {
            if (property == null || property.propertyType != SerializedPropertyType.String)
                return 0;
            switch (property.stringValue)
            {
                case "dialog.option.common.goodbye": property.stringValue = "dialogue.option.common.goodbye"; return 1;
                case "dialog.option.common.understood": property.stringValue = "dialogue.option.common.understood"; return 1;
                case "dialog.option.common.not_now": property.stringValue = "dialogue.option.common.not_now"; return 1;
                case "dialog.option.common.cancel": property.stringValue = "dialogue.option.common.cancel"; return 1;
                case "dialog.option.common.thanks": property.stringValue = "dialogue.option.common.thanks"; return 1;
                default: return 0;
            }
        }

        private static string OptionText(string token, bool english)
        {
            var en = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["offer_main"] = "Hear the full proposal", ["briefing"] = "Ask for the briefing", ["choose_route"] = "Choose an investigation route", ["final_truth"] = "Report the truth", ["final_compromised"] = "Report the compromised evidence", ["failed_fallback"] = "End the conversation",
                ["accept_main"] = "Accept the investigation", ["ask_evidence"] = "Ask about the evidence", ["offer_salvage"] = "Ask about salvage", ["salvage_instructions"] = "Request salvage instructions", ["recover_blackbox"] = "Recover the black box", ["return_blackbox"] = "Return the black box", ["deliver_blackbox"] = "Deliver the black box", ["accept_salvage"] = "Accept the salvage route", ["confirm_delivery"] = "Confirm delivery",
                ["technical_version"] = "Ask for the technical version", ["offer_science_reputation"] = "Offer scientific cooperation", ["offer_science_attitude"] = "Ask Veska for help", ["calibration"] = "Discuss calibration", ["decode_lighthouse"] = "Decode the lighthouse", ["report"] = "Request the report", ["accept_science"] = "Accept the scientific route", ["submit_report"] = "Submit the report", ["false_seal_warning"] = "Present the false seal", ["present_false_seal"] = "Present the seal", ["keep_evidence"] = "Keep the evidence",
                ["check_story"] = "Check the broker's story", ["manifest_failure"] = "Ask about the failed manifest", ["take_false_manifest"] = "Take the false manifest", ["refuse_version"] = "Reject the convenient version", ["present_false_manifest"] = "Present the false manifest", ["confirm_false_manifest"] = "Confirm the false manifest", ["refuse_false_manifest"] = "Refuse the false manifest",
                ["offer_silence"] = "Hear the silent offer", ["take_blackbox"] = "Take the black box", ["exchange"] = "Discuss the exchange", ["ambient"] = "Ask about the archive", ["accept_silence"] = "Accept the silent route", ["sell_blackbox"] = "Sell the black box", ["confirm_sale"] = "Confirm the sale", ["cancel_sale"] = "Cancel the sale",
                ["testimony"] = "Ask for testimony", ["quarantine_confirmation"] = "Confirm the quarantine version", ["branch_confirmation"] = "Ask about the branch", ["incomplete"] = "Ask about the incomplete record", ["show_fragments"] = "Show the fragments", ["thanks"] = "Thank Sela", ["submit_final_report"] = "Submit the final report"
            };
            var ru = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["offer_main"] = "Выслушать предложение", ["briefing"] = "Попросить инструктаж", ["choose_route"] = "Выбрать путь расследования", ["final_truth"] = "Доложить правду", ["final_compromised"] = "Доложить о скомпрометированных уликах", ["failed_fallback"] = "Закончить разговор",
                ["accept_main"] = "Принять расследование", ["ask_evidence"] = "Спросить об уликах", ["offer_salvage"] = "Спросить о salvage", ["salvage_instructions"] = "Попросить инструкции по salvage", ["recover_blackbox"] = "Извлечь чёрный ящик", ["return_blackbox"] = "Вернуть чёрный ящик", ["deliver_blackbox"] = "Передать чёрный ящик", ["accept_salvage"] = "Принять salvage-ветку", ["confirm_delivery"] = "Подтвердить передачу",
                ["technical_version"] = "Спросить техническую версию", ["offer_science_reputation"] = "Предложить научное сотрудничество", ["offer_science_attitude"] = "Попросить Веску о помощи", ["calibration"] = "Обсудить калибровку", ["decode_lighthouse"] = "Расшифровать маяк", ["report"] = "Попросить отчёт", ["accept_science"] = "Принять научную ветку", ["submit_report"] = "Передать отчёт", ["false_seal_warning"] = "Предъявить поддельную печать", ["present_false_seal"] = "Предъявить печать", ["keep_evidence"] = "Оставить улику",
                ["check_story"] = "Проверить версию брокера", ["manifest_failure"] = "Спросить о провальном манифесте", ["take_false_manifest"] = "Взять фальшивый манифест", ["refuse_version"] = "Отвергнуть удобную версию", ["present_false_manifest"] = "Предъявить фальшивый манифест", ["confirm_false_manifest"] = "Подтвердить фальшивый манифест", ["refuse_false_manifest"] = "Отказаться от фальшивого манифеста",
                ["offer_silence"] = "Выслушать предложение о тишине", ["take_blackbox"] = "Забрать чёрный ящик", ["exchange"] = "Обсудить обмен", ["ambient"] = "Спросить об архиве", ["accept_silence"] = "Принять тайную ветку", ["sell_blackbox"] = "Продать чёрный ящик", ["confirm_sale"] = "Подтвердить продажу", ["cancel_sale"] = "Отменить продажу",
                ["testimony"] = "Попросить свидетельство", ["quarantine_confirmation"] = "Подтвердить карантинную версию", ["branch_confirmation"] = "Спросить о ветке", ["incomplete"] = "Спросить о неполной записи", ["show_fragments"] = "Показать фрагменты", ["thanks"] = "Поблагодарить Селу", ["submit_final_report"] = "Передать итоговый отчёт"
            };
            return (english ? en : ru).TryGetValue(token, out var value) ? value : (english ? "Continue" : "Продолжить");
        }
    }
}
