// LOC-01, LOC-02: One-shot setup script for Localization infrastructure
// Creates: Locale assets (9 languages), LocalizationSettings, 4 StringTableCollections
using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using System.Collections.Generic;

namespace ProjectC.Localization.Editor
{
    public static class LocalizationSetup
    {
        // 9 supported languages
        private static readonly (string code, string name)[] LOCALES = new[]
        {
            ("ru", "Russian"),
            ("zh", "Chinese (Simplified)"),
            ("en", "English"),
            ("es", "Spanish"),
            ("de", "German"),
            ("fr", "French"),
            ("pt", "Portuguese (Brazilian)"),
            ("ja", "Japanese"),
            ("hi", "Hindi"),
        };

        private const string SETTINGS_PATH = "Assets/_Project/Settings/Localization";
        private const string EXPORT_PATH = "Assets/_Project/Localization/Export";

        [MenuItem("ProjectC/Localization/Setup Infrastructure (LOC-01/02)")]
        public static void Execute()
        {
            Debug.Log("[LocalizationSetup] Starting Phase 0+1 infrastructure setup...");

            // Ensure directories
            EnsureDirectory(SETTINGS_PATH);
            EnsureDirectory(EXPORT_PATH);
            EnsureDirectory("Assets/_Project/Scripts/Localization");
            EnsureDirectory("Assets/_Project/Editor/Localization");

            // ----- Step 1: Create Locale assets -----
            var locales = new List<Locale>();
            foreach (var (code, name) in LOCALES)
            {
                var localePath = $"{SETTINGS_PATH}/Locale_{code}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<Locale>(localePath);
                if (existing != null)
                {
                    Debug.Log($"[LocalizationSetup] Locale '{code}' already exists, skipping.");
                    locales.Add(existing);
                    continue;
                }

                var locale = Locale.CreateLocale(System.Globalization.CultureInfo.GetCultureInfo(code));
                locale.name = name;
                AssetDatabase.CreateAsset(locale, localePath);
                locales.Add(locale);
                Debug.Log($"[LocalizationSetup] Created Locale: {localePath} ({name})");
            }

            // ----- Step 2: Create LocalizationSettings -----
            var settingsPath = $"{SETTINGS_PATH}/LocalizationSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(settingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings, settingsPath);

                // Set available locales
                var availableLocales = new List<Locale>();
                foreach (var (code, _) in LOCALES)
                {
                    var loc = AssetDatabase.LoadAssetAtPath<Locale>($"{SETTINGS_PATH}/Locale_{code}.asset");
                    if (loc != null) availableLocales.Add(loc);
                }

                var so = new SerializedObject(settings);
                var availProp = so.FindProperty("m_AvailableLocales");

                // The property is m_Locales on the LocalesProvider
                var localesProvider = so.FindProperty("m_LocalesProvider");
                if (localesProvider != null)
                {
                    var localeList = localesProvider.FindPropertyRelative("m_Locales");
                    if (localeList != null)
                    {
                        localeList.ClearArray();
                        for (int i = 0; i < availableLocales.Count; i++)
                        {
                            localeList.InsertArrayElementAtIndex(i);
                            localeList.GetArrayElementAtIndex(i).objectReferenceValue = availableLocales[i];
                        }
                    }
                }

                so.ApplyModifiedProperties();
                Debug.Log($"[LocalizationSetup] Created LocalizationSettings: {settingsPath}");
            }
            else
            {
                Debug.Log($"[LocalizationSetup] LocalizationSettings already exists, skipping.");
            }

            // Set as active settings
            if (LocalizationEditorSettings.ActiveLocalizationSettings != settings)
            {
                LocalizationEditorSettings.ActiveLocalizationSettings = settings;
                Debug.Log("[LocalizationSetup] Set as active LocalizationSettings.");
            }

            // ----- Step 3: Create 4 StringTableCollections -----
            string[] tableNames = { "Static_Table", "UI_Table", "Dialogue_Table", "System_Table" };
            foreach (var tableName in tableNames)
            {
                var tablePath = $"{SETTINGS_PATH}/{tableName}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<StringTableCollection>(tablePath);
                if (existing != null)
                {
                    Debug.Log($"[LocalizationSetup] StringTableCollection '{tableName}' already exists, skipping.");
                    continue;
                }

                var collection = LocalizationEditorSettings.CreateStringTableCollection(tableName, SETTINGS_PATH, locales);
                Debug.Log($"[LocalizationSetup] Created StringTableCollection: {tablePath}");
            }

            // ----- Step 4: Save & Refresh -----
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[LocalizationSetup] Phase 0+1 infrastructure setup COMPLETE.");
            Debug.Log($"[LocalizationSetup] Locales: {LOCALES.Length}");
            Debug.Log($"[LocalizationSetup] Tables: {tableNames.Length}");
            Debug.Log($"[LocalizationSetup] Next: create Loc.cs, LocaleSelector.cs, SettingsManager.Locale, GameplaySettings dropdown.");
        }

        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parts = path.Split('/');
                var parent = parts[0]; // "Assets"
                for (int i = 1; i < parts.Length; i++)
                {
                    var current = $"{parent}/{parts[i]}";
                    if (!AssetDatabase.IsValidFolder(current))
                    {
                        AssetDatabase.CreateFolder(parent, parts[i]);
                    }
                    parent = current;
                }
            }
        }
    }
}
