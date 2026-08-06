// LocalizationSettingsFix.cs — Phase C: Fix LocalizationSettings config
// Menu: ProjectC → Localization → Fix LocalizationSettings
using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Settings;

namespace ProjectC.Localization.Editor
{
    public static class LocalizationSettingsFix
    {
        [MenuItem("ProjectC/Localization/Fix LocalizationSettings (Phase C)")]
        public static void Execute()
        {
            var settingsPath = "Assets/_Project/Settings/Localization/LocalizationSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(settingsPath);
            if (settings == null)
            {
                Debug.LogError("[SettingsFix] LocalizationSettings not found!");
                return;
            }

            var so = new SerializedObject(settings);

            // 9. ProjectLocaleIdentifier → ru
            var projectLocaleProp = so.FindProperty("m_ProjectLocaleIdentifier");
            if (projectLocaleProp != null)
            {
                var codeProp = projectLocaleProp.FindPropertyRelative("m_Code");
                if (codeProp != null)
                {
                    codeProp.stringValue = "ru";
                    Debug.Log("[SettingsFix] ProjectLocaleIdentifier → ru");
                }
            }

            // 10. Remove SpecificLocaleSelector(en) from StartupSelectors
            var selectorsProp = so.FindProperty("m_StartupSelectors");
            if (selectorsProp != null && selectorsProp.isArray)
            {
                // Walk array and find SpecificLocaleSelector entries (they have a managedReference
                // of type SpecificLocaleSelector)
                var toRemove = new System.Collections.Generic.List<int>();
                for (int i = 0; i < selectorsProp.arraySize; i++)
                {
                    var el = selectorsProp.GetArrayElementAtIndex(i);
                    var refValue = el.managedReferenceValue;
                    if (refValue != null && refValue.GetType().Name == "SpecificLocaleSelector")
                    {
                        toRemove.Add(i);
                    }
                }

                // Remove from end to preserve indices
                toRemove.Reverse();
                foreach (var idx in toRemove)
                {
                    selectorsProp.DeleteArrayElementAtIndex(idx);
                }
                Debug.Log($"[SettingsFix] Removed {toRemove.Count} SpecificLocaleSelector(s)");
            }

            // 11. Enable UseFallback in LocalizedStringDatabase
            var stringDbProp = so.FindProperty("m_StringDatabase");
            if (stringDbProp != null)
            {
                var fallbackProp = stringDbProp.FindPropertyRelative("m_UseFallback");
                if (fallbackProp != null)
                {
                    fallbackProp.boolValue = true;
                    Debug.Log("[SettingsFix] StringDatabase.UseFallback → true");
                }
            }

            // Also enable in LocalizedAssetDatabase
            var assetDbProp = so.FindProperty("m_AssetDatabase");
            if (assetDbProp != null)
            {
                var fallbackProp = assetDbProp.FindPropertyRelative("m_UseFallback");
                if (fallbackProp != null)
                {
                    fallbackProp.boolValue = true;
                    Debug.Log("[SettingsFix] AssetDatabase.UseFallback → true");
                }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[SettingsFix] === LocalizationSettings fix COMPLETE ===");
        }
    }
}
