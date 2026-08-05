// Project C: Localization Helper (LOC-03)
// Runtime facade over Unity Localization: Get/Format/Bind + auto-routing by key prefix.
using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;
using TMPro;

namespace ProjectC.Localization
{
    /// <summary>
    /// Core localization helper. All client-facing string access goes through Loc.
    /// Key prefix determines the table: static.* / ui.* / dialogue.* / sys.*
    /// Fallback chain: translation -> ru -> passed literal -> key itself.
    /// </summary>
    public static class Loc
    {
        public static event Action OnLocaleChanged;

        private static bool _subscribed;

        private static void EnsureSubscribed()
        {
            if (_subscribed) return;
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
            _subscribed = true;
        }

        private static void OnSelectedLocaleChanged(Locale locale)
        {
            var code = locale != null ? locale.Identifier.Code : "null";
            Debug.Log($"[Loc] Locale changed to: {code}");
            OnLocaleChanged?.Invoke();
        }

        /// <summary>Get localized string by key. Auto-routes to correct table by prefix.</summary>
        public static string Get(string key, string fallback = null)
        {
            if (string.IsNullOrEmpty(key)) return fallback ?? key ?? "";
            EnsureSubscribed();

            var (tableName, entryKey) = ParseKey(key);
            return GetFromTable(tableName, entryKey, fallback ?? key);
        }

        /// <summary>Get localized string with Smart String formatting.</summary>
        public static string Format(string key, params object[] args)
        {
            var str = Get(key);
            if (args == null || args.Length == 0) return str;
            try { return string.Format(str, args); }
            catch (Exception) { return str; }
        }

        /// <summary>Get with explicit table name (bypasses prefix routing).</summary>
        public static string Get(string tableName, string key, string fallback = null)
        {
            return GetFromTable(tableName, key, fallback ?? key);
        }

        /// <summary>Try to get localized string, returns false if no translation found.</summary>
        public static bool TryGet(string key, out string value)
        {
            value = Get(key, null);
            return value != null && value != key;
        }

        /// <summary>Bind a UI Toolkit Label to a localization key. Updates on locale change.</summary>
        public static void Bind(Label label, string key, string fallback = null)
        {
            if (label == null) return;
            EnsureSubscribed();
            label.text = Get(key, fallback ?? label.text);
            Action handler = () => label.text = Get(key, fallback ?? label.text);
            OnLocaleChanged += handler;
            label.RegisterCallback<DetachFromPanelEvent>(_ => OnLocaleChanged -= handler);
        }

        /// <summary>Bind a TextMeshProUGUI label to a localization key.</summary>
        public static void Bind(TMP_Text tmpText, string key, string fallback = null)
        {
            if (tmpText == null) return;
            EnsureSubscribed();
            tmpText.text = Get(key, fallback ?? tmpText.text);
            Action handler = () => tmpText.text = Get(key, fallback ?? tmpText.text);
            OnLocaleChanged += handler;
        }

        /// <summary>Walk all Labels whose 'name' matches a loc key prefix and bind them.</summary>
        public static void BindAll(VisualElement root)
        {
            if (root == null) return;
            EnsureSubscribed();
            WalkAndBind(root);
        }

        private static void WalkAndBind(VisualElement el)
        {
            if (el is Label label && !string.IsNullOrEmpty(el.name))
            {
                var name = el.name;
                if (name.StartsWith("ui.") || name.StartsWith("static.") ||
                    name.StartsWith("dialogue.") || name.StartsWith("sys."))
                {
                    Bind(label, name, label.text);
                }
            }
            foreach (var child in el.Children())
                WalkAndBind(child);
        }

        /// <summary>Convert PascalCase to snake_case (e.g. InventoryFull -> inventory_full).</summary>
        public static string ToSnakeCase(string pascal)
        {
            if (string.IsNullOrEmpty(pascal)) return pascal;
            var result = char.ToLower(pascal[0]).ToString();
            for (int i = 1; i < pascal.Length; i++)
            {
                if (char.IsUpper(pascal[i]))
                    result += "_" + char.ToLower(pascal[i]);
                else
                    result += pascal[i];
            }
            return result;
        }

        // ===== Internal =====

        private static string GetFromTable(string tableName, string entryKey, string fallback)
        {
            var locale = LocalizationSettings.SelectedLocale;
            if (locale == null)
                return fallback;

            try
            {
                var db = LocalizationSettings.StringDatabase;
                if (db == null) return fallback;

                var table = db.GetTable(tableName);
                if (table == null) return fallback;

                var entry = table.GetEntry(entryKey);
                if (entry == null) return fallback;

                var value = entry.GetLocalizedString();
                if (!string.IsNullOrEmpty(value)) return value;

                return fallback;
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        private static (string table, string key) ParseKey(string fullKey)
        {
            if (string.IsNullOrEmpty(fullKey)) return ("UI_Table", fullKey);

            if (fullKey.StartsWith("static.", StringComparison.OrdinalIgnoreCase))
                return ("Static_Table", fullKey);
            if (fullKey.StartsWith("ui.", StringComparison.OrdinalIgnoreCase))
                return ("UI_Table", fullKey);
            if (fullKey.StartsWith("dialogue.", StringComparison.OrdinalIgnoreCase))
                return ("Dialogue_Table", fullKey);
            if (fullKey.StartsWith("sys.", StringComparison.OrdinalIgnoreCase))
                return ("System_Table", fullKey);

            return ("UI_Table", fullKey);
        }
    }
}
