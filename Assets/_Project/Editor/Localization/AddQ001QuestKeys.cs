using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace ProjectC.Localization.Editor
{
    /// <summary>
    /// T-Q001-LOC: Добавляет 55 ключей Q001 (main + 3 ветки) в UI_Table.
    /// Паттерн: idempotent, abort-before-save, SetDirty + SaveAssets.
    /// </summary>
    public static class AddQ001QuestKeys
    {
        private const string SharedPath = "Assets/_Project/Settings/Localization/UI_Table Shared Data.asset";
        private const string RuPath = "Assets/_Project/Settings/Localization/UI_Table_ru.asset";
        private const string EnPath = "Assets/_Project/Settings/Localization/UI_Table_en.asset";

        // 55 уникальных ключей Q001 (main 22 + A 11 + B 12 + C 10).
        private static readonly string[] Q001Keys =
        {
            // Main quest (22)
            "quest.q001.name",
            "quest.q001.description",
            "quest.q001.stage.follow_note",
            "quest.q001.stage.collect_first_evidence",
            "quest.q001.stage.sela_testimony",
            "quest.q001.stage.resonance_check",
            "quest.q001.stage.choose_route",
            "quest.q001.stage.wait_branch",
            "quest.q001.stage.final_report",
            "quest.q001.objective.reach_old_dock",
            "quest.q001.objective.talk_bram",
            "quest.q001.objective.fragment_dock",
            "quest.q001.objective.fragment_archive",
            "quest.q001.objective.reach_salt_archive",
            "quest.q001.objective.talk_sela",
            "quest.q001.objective.fragment_sela",
            "quest.q001.objective.have_vault_key",
            "quest.q001.objective.reach_glass_well",
            "quest.q001.objective.talk_veska",
            "quest.q001.objective.talk_lyra_choice",
            "quest.q001.objective.branch_resolved",
            "quest.q001.objective.final_talk_lyra",
            // Branch A (11)
            "quest.q001a.name",
            "quest.q001a.description",
            "quest.q001a.stage.lens_calibration",
            "quest.q001a.stage.decode_lighthouse",
            "quest.q001a.stage.report_veska",
            "quest.q001a.objective.talk_veska",
            "quest.q001a.objective.lighthouse_have_lens",
            "quest.q001a.objective.reach_lighthouse",
            "quest.q001a.objective.have_lens",
            "quest.q001a.objective.talk_sela",
            "quest.q001a.objective.talk_veska_final",
            // Branch B (12)
            "quest.q001b.name",
            "quest.q001b.description",
            "quest.q001b.stage.bram_method",
            "quest.q001b.stage.recover_blackbox",
            "quest.q001b.stage.check_broker_story",
            "quest.q001b.stage.return_blackbox",
            "quest.q001b.objective.talk_bram",
            "quest.q001b.objective.reach_quarantine_cove",
            "quest.q001b.objective.have_blackbox",
            "quest.q001b.objective.talk_noll",
            "quest.q001b.objective.talk_bram_final",
            "quest.q001b.objective.deliver_blackbox",
            // Branch C (10)
            "quest.q001c.name",
            "quest.q001c.description",
            "quest.q001c.stage.offer_silence",
            "quest.q001c.stage.take_blackbox",
            "quest.q001c.stage.exchange",
            "quest.q001c.objective.talk_kael",
            "quest.q001c.objective.reach_cache",
            "quest.q001c.objective.have_blackbox",
            "quest.q001c.objective.talk_kael_final",
            "quest.q001c.objective.deliver_blackbox"
        };

        [MenuItem("ProjectC/Localization/Q001/Add Quest Keys (55)")]
        public static void Execute()
        {
            var shared = AssetDatabase.LoadAssetAtPath<SharedTableData>(SharedPath);
            var ru = AssetDatabase.LoadAssetAtPath<StringTable>(RuPath);
            var en = AssetDatabase.LoadAssetAtPath<StringTable>(EnPath);
            if (shared == null || ru == null || en == null)
            {
                Debug.LogError("[Q001 QuestKeys] Required UI_Table assets were not found.");
                return;
            }

            // Validate: all 55 keys must start with "quest."
            var invalidPrefix = Q001Keys.Where(k => !k.StartsWith("quest.", StringComparison.Ordinal)).ToList();
            if (invalidPrefix.Count > 0)
            {
                Debug.LogError($"[Q001 QuestKeys] ABORTED: {invalidPrefix.Count} keys do not start with 'quest.'");
                return;
            }

            // Check for duplicates
            var duplicateFree = Q001Keys.Distinct(StringComparer.Ordinal).Count() == Q001Keys.Length;
            if (!duplicateFree)
            {
                Debug.LogError("[Q001 QuestKeys] ABORTED before save: duplicate keys detected.");
                return;
            }

            var missingRu = Q001Keys.Where(k => ru.GetEntry(k) == null).ToList();
            var missingEn = Q001Keys.Where(k => en.GetEntry(k) == null).ToList();
            if (missingRu.Count != missingEn.Count)
            {
                Debug.LogError($"[Q001 QuestKeys] ABORTED before save: missingRU={missingRu.Count}; missingEN={missingEn.Count}.");
                return;
            }

            var sharedBefore = shared.Entries.Count;
            var ruBefore = ru.Values.Count(v => v != null);
            var enBefore = en.Values.Count(v => v != null);
            var added = 0;

            foreach (var key in Q001Keys)
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

            // Post-save validation
            var invalid = Q001Keys.Where(k =>
            {
                var r = ru.GetEntry(k)?.LocalizedValue;
                var e = en.GetEntry(k)?.LocalizedValue;
                return string.IsNullOrWhiteSpace(r) || string.IsNullOrWhiteSpace(e) || r == k || e == k;
            }).ToList();

            if (invalid.Count > 0 || shared.Entries.Count != sharedBefore + added)
            {
                Debug.LogError($"[Q001 QuestKeys] ABORTED before save: invalidTranslations={invalid.Count}; sharedDelta={shared.Entries.Count - sharedBefore}; expectedDelta={added}.");
                return;
            }

            EditorUtility.SetDirty(shared);
            EditorUtility.SetDirty(ru);
            EditorUtility.SetDirty(en);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Q001 QuestKeys] PASS: q001Keys={Q001Keys.Length}; sharedAdded={added}; ruAdded={ru.Values.Count(v => v != null) - ruBefore}; enAdded={en.Values.Count(v => v != null) - enBefore}; sharedTotal={shared.Entries.Count}; ruTotal={ru.Values.Count(v => v != null)}; enTotal={en.Values.Count(v => v != null)}.");
        }

        private static string Translate(string key, bool english)
        {
            // Main quest
            if (key == "quest.q001.name") return english ? "Ash Under Glass" : "Пепел под стеклом";
            if (key == "quest.q001.description") return english ? "Investigate the signal from the glass archive and discover what it concealed." : "Расследуй сигнал из стеклянного архива и установи, что он скрывал.";
            if (key == "quest.q001.stage.follow_note") return english ? "Find the place indicated in Lyra's note." : "Найти место, указанное в записке Лиры.";
            if (key == "quest.q001.stage.collect_first_evidence") return english ? "Gather the first independent traces of the expedition and talk to Bram." : "Собрать первые независимые следы экспедиции и поговорить с Брамом.";
            if (key == "quest.q001.stage.sela_testimony") return english ? "Find Sela in the salt archive and restore the missing piece of the protocol." : "Найти Селу в соляном архиве и восстановить пропущенный кусок протокола.";
            if (key == "quest.q001.stage.resonance_check") return english ? "Check whether the signal is linked to the glass well, not the military beacon." : "Проверить, связан ли сигнал со стеклянной шахтой, а не с военным маяком.";
            if (key == "quest.q001.stage.choose_route") return english ? "Decide who to entrust the continuation of the investigation to." : "Решить, кому доверить продолжение расследования.";
            if (key == "quest.q001.stage.wait_branch") return english ? "Complete the chosen investigation route." : "Завершить выбранный маршрут расследования.";
            if (key == "quest.q001.stage.final_report") return english ? "Return to Lyra and finalize the investigation." : "Вернуться к Лире и оформить итог расследования.";
            if (key == "quest.q001.objective.reach_old_dock") return english ? "Reach the old dock" : "Дойти до старого пирса";
            if (key == "quest.q001.objective.talk_bram") return english ? "Talk to Bram" : "Поговорить с Брамом";
            if (key == "quest.q001.objective.fragment_dock") return english ? "Find the dock fragment" : "Найти фрагмент с пирса";
            if (key == "quest.q001.objective.fragment_archive") return english ? "Find the archive fragment" : "Найти фрагмент из архива";
            if (key == "quest.q001.objective.reach_salt_archive") return english ? "Reach the salt archive" : "Дойти до соляного архива";
            if (key == "quest.q001.objective.talk_sela") return english ? "Talk to Sela" : "Поговорить с Селой";
            if (key == "quest.q001.objective.fragment_sela") return english ? "Obtain Sela's fragment" : "Получить фрагмент Селы";
            if (key == "quest.q001.objective.have_vault_key") return english ? "Have the vault key" : "Иметь ключ хранилища";
            if (key == "quest.q001.objective.reach_glass_well") return english ? "Reach the glass well" : "Дойти до стеклянной шахты";
            if (key == "quest.q001.objective.talk_veska") return english ? "Talk to Veska" : "Поговорить с Веской";
            if (key == "quest.q001.objective.talk_lyra_choice") return english ? "Talk to Lyra about the route choice" : "Поговорить с Лирой о выборе пути";
            if (key == "quest.q001.objective.branch_resolved") return english ? "Resolve the investigation branch" : "Завершить ветку расследования";
            if (key == "quest.q001.objective.final_talk_lyra") return english ? "Talk to Lyra (final)" : "Поговорить с Лирой (финал)";

            // Branch A
            if (key == "quest.q001a.name") return english ? "Signal Reconstruction" : "Восстановление сигнала";
            if (key == "quest.q001a.description") return english ? "Prove that the signal is an emergency protocol, not a military call." : "Докажи, что сигнал является аварийным протоколом, а не военным вызовом.";
            if (key == "quest.q001a.stage.lens_calibration") return english ? "Calibrate the resonance lens" : "Откалибровать резонансную линзу";
            if (key == "quest.q001a.stage.decode_lighthouse") return english ? "Decode the signal at the silent lighthouse" : "Расшифровать сигнал в глухом маяке";
            if (key == "quest.q001a.stage.report_veska") return english ? "Submit the report to Veska" : "Передать отчёт Веске";
            if (key == "quest.q001a.objective.talk_veska") return english ? "Talk to Veska" : "Поговорить с Веской";
            if (key == "quest.q001a.objective.lighthouse_have_lens") return english ? "Have the resonance lens" : "Иметь резонансную линзу";
            if (key == "quest.q001a.objective.reach_lighthouse") return english ? "Reach the lighthouse" : "Дойти до маяка";
            if (key == "quest.q001a.objective.have_lens") return english ? "Have the lens" : "Иметь линзу";
            if (key == "quest.q001a.objective.talk_sela") return english ? "Talk to Sela" : "Поговорить с Селой";
            if (key == "quest.q001a.objective.talk_veska_final") return english ? "Talk to Veska (final)" : "Поговорить с Веской (финал)";

            // Branch B
            if (key == "quest.q001b.name") return english ? "Black Box Salvage" : "Добыча чёрного ящика";
            if (key == "quest.q001b.description") return english ? "Extract the black box and obtain proof through Bram." : "Извлеки чёрный ящик и получи доказательство через Брама.";
            if (key == "quest.q001b.stage.bram_method") return english ? "Bram's Method" : "Метод Брама";
            if (key == "quest.q001b.stage.recover_blackbox") return english ? "Recover the black box" : "Извлечь чёрный ящик";
            if (key == "quest.q001b.stage.check_broker_story") return english ? "Check the broker's story" : "Проверить версию брокера";
            if (key == "quest.q001b.stage.return_blackbox") return english ? "Return the black box" : "Вернуть чёрный ящик";
            if (key == "quest.q001b.objective.talk_bram") return english ? "Talk to Bram" : "Поговорить с Брамом";
            if (key == "quest.q001b.objective.reach_quarantine_cove") return english ? "Reach the quarantine cove" : "Дойти до карантинной бухты";
            if (key == "quest.q001b.objective.have_blackbox") return english ? "Have the black box" : "Иметь чёрный ящик";
            if (key == "quest.q001b.objective.talk_noll") return english ? "Talk to Noll" : "Поговорить с Ноллом";
            if (key == "quest.q001b.objective.talk_bram_final") return english ? "Talk to Bram (final)" : "Поговорить с Брамом (финал)";
            if (key == "quest.q001b.objective.deliver_blackbox") return english ? "Deliver the black box to Bram" : "Передать чёрный ящик Браму";

            // Branch C
            if (key == "quest.q001c.name") return english ? "Silent Exchange" : "Тихий обмен";
            if (key == "quest.q001c.description") return english ? "Sell the black box to the highest bidder." : "Продай чёрный ящик тому, кто заплатит больше.";
            if (key == "quest.q001c.stage.offer_silence") return english ? "Offer of Silence" : "Предложение о тишине";
            if (key == "quest.q001c.stage.take_blackbox") return english ? "Take the black box" : "Забрать чёрный ящик";
            if (key == "quest.q001c.stage.exchange") return english ? "Exchange" : "Обмен";
            if (key == "quest.q001c.objective.talk_kael") return english ? "Talk to Kael" : "Поговорить с Каэлем";
            if (key == "quest.q001c.objective.reach_cache") return english ? "Reach the cache" : "Дойти до кэша";
            if (key == "quest.q001c.objective.have_blackbox") return english ? "Have the black box" : "Иметь чёрный ящик";
            if (key == "quest.q001c.objective.talk_kael_final") return english ? "Talk to Kael (final)" : "Поговорить с Каэлем (финал)";
            if (key == "quest.q001c.objective.deliver_blackbox") return english ? "Deliver the black box to Kael" : "Передать чёрный ящик Каэлю";

            // Fallback (should never hit if all 55 are covered)
            return english ? key : key;
        }
    }
}
