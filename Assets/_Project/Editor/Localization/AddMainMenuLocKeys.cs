using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace ProjectC.Localization.Editor
{
    public static class AddMainMenuLocKeys
    {
        [MenuItem("ProjectC/Localization/Add MainMenu UI Keys")]
        public static void Execute()
        {
            var sharedPath = "Assets/_Project/Settings/Localization/UI_Table Shared Data.asset";
            var ruPath = "Assets/_Project/Settings/Localization/UI_Table_ru.asset";
            var enPath = "Assets/_Project/Settings/Localization/UI_Table_en.asset";

            var shared = AssetDatabase.LoadAssetAtPath<SharedTableData>(sharedPath);
            var ru = AssetDatabase.LoadAssetAtPath<StringTable>(ruPath);
            var en = AssetDatabase.LoadAssetAtPath<StringTable>(enPath);

            if (shared == null) { Debug.LogError("[AddMainMenuLocKeys] SharedTableData not found"); return; }
            if (ru == null) { Debug.LogError("[AddMainMenuLocKeys] ru table not found"); return; }
            if (en == null) { Debug.LogError("[AddMainMenuLocKeys] en table not found"); return; }

            int added = 0;
            added += AddIfMissing(shared, ru, en, "ui.main_menu.title", "PROJECT C: THE CLOUDS", "PROJECT C: THE CLOUDS");
            added += AddIfMissing(shared, ru, en, "ui.main_menu.subtitle", "Версия Alpha 0.1", "Alpha 0.1");
            added += AddIfMissing(shared, ru, en, "ui.main_menu.button.host", "ОДИНОЧНАЯ ИГРА", "SOLO GAME");
            added += AddIfMissing(shared, ru, en, "ui.main_menu.button.connect", "ПОДКЛЮЧИТЬСЯ К СЕРВЕРУ", "CONNECT TO SERVER");
            added += AddIfMissing(shared, ru, en, "ui.main_menu.button.settings", "НАСТРОЙКИ", "SETTINGS");
            added += AddIfMissing(shared, ru, en, "ui.main_menu.button.quit", "ВЫХОД", "QUIT");
            added += AddIfMissing(shared, ru, en, "ui.main_menu.button.ip_connect", "ПОДКЛЮЧИТЬСЯ", "CONNECT");
            added += AddIfMissing(shared, ru, en, "ui.main_menu.button.back", "← НАЗАД", "← BACK");
            added += AddIfMissing(shared, ru, en, "ui.main_menu.ip_label", "Введите IP-адрес сервера:", "Enter server IP:");

            EditorUtility.SetDirty(shared);
            EditorUtility.SetDirty(ru);
            EditorUtility.SetDirty(en);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AddMainMenuLocKeys] Done. Added {added} entries.");
        }

        private static int AddIfMissing(SharedTableData shared, StringTable ru, StringTable en, string key, string ruVal, string enVal)
        {
            if (shared.GetEntry(key) != null)
            {
                return 0;
            }
            shared.AddKey(key);
            ru.AddEntry(key, ruVal);
            en.AddEntry(key, enVal);
            Debug.Log($"[AddMainMenuLocKeys] ADD: {key}");
            return 1;
        }
    }
}
