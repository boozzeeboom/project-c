// Project C: Localization CSV Tool (LOC-10)
// Editor window: Export all tables → CSV, Import CSV → tables.
using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;

namespace ProjectC.Localization.Editor
{
    public class LocalizationToolWindow : EditorWindow
    {
        private string _exportDir;
        private string _importPath;
        private string _statusMessage;
        private bool _exportDone;

        [MenuItem("ProjectC/Localization/Export/Import Tool", priority = 200)]
        public static void Open()
        {
            var win = GetWindow<LocalizationToolWindow>("Loc Tool");
            win.minSize = new Vector2(480, 320);
            win.Show();
        }

        private void OnEnable()
        {
            _exportDir = EditorPrefs.GetString("LocTool.ExportDir", "Assets/_Project/Localization/Export");
        }

        private void OnGUI()
        {
            GUILayout.Label("Инструмент переводчика (CSV Export / Import)", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            // === Export ===
            GUILayout.Label("Экспорт", EditorStyles.boldLabel);
            _exportDir = EditorGUILayout.TextField("Папка экспорта:", _exportDir);

            if (GUILayout.Button("Выгрузить всё (4 таблицы × все локали)", GUILayout.Height(30)))
            {
                ExportAll();
            }

            if (_exportDone && GUILayout.Button("Открыть папку", GUILayout.Height(20)))
            {
                EditorPrefs.SetString("LocTool.ExportDir", _exportDir);
                EnsureDirectory(_exportDir);
                EditorUtility.RevealInFinder(_exportDir);
            }

            EditorGUILayout.Space(16);

            // === Import ===
            GUILayout.Label("Импорт", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            _importPath = EditorGUILayout.TextField("CSV файл:", _importPath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var sel = EditorUtility.OpenFilePanel("Выберите CSV", _exportDir, "csv");
                if (!string.IsNullOrEmpty(sel))
                {
                    // Convert to project-relative
                    var dataPath = Application.dataPath.Replace("/", "\\").TrimEnd('\\');
                    sel = sel.Replace("/", "\\");
                    if (sel.StartsWith(dataPath))
                        _importPath = "Assets" + sel.Substring(dataPath.Length);
                    else
                        _importPath = sel;
                }
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Загрузить CSV → таблицы", GUILayout.Height(30)))
            {
                ImportCsv();
            }

            EditorGUILayout.Space(16);

            // === Validation ===
            GUILayout.Label("Валидация", EditorStyles.boldLabel);
            if (GUILayout.Button("Проверить покрытие (ключи без перевода)", GUILayout.Height(24)))
            {
                CheckCoverage();
            }

            // === Status ===
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox(_statusMessage, _statusMessage.StartsWith("✅") ? MessageType.Info : MessageType.Warning);
            }
        }

        private void ExportAll()
        {
            EnsureDirectory(_exportDir);
            EditorPrefs.SetString("LocTool.ExportDir", _exportDir);

            string[] tables = { "Static_Table", "UI_Table", "Dialogue_Table", "System_Table" };
            int count = 0;
            var sb = new StringBuilder();
            sb.AppendLine($"[LocTool] Export to: {_exportDir}");

            foreach (var tableName in tables)
            {
                var csvPath = $"{_exportDir}/{tableName}.csv";
                try
                {
                    var collection = GetCollection(tableName);
                    if (collection == null)
                    {
                        sb.AppendLine($"  ⚠ {tableName}: collection not found");
                        continue;
                    }

                    ExportTableToCsv(collection, csvPath);
                    sb.AppendLine($"  ✅ {tableName}.csv");
                    count++;
                }
                catch (System.Exception ex)
                {
                    sb.AppendLine($"  ❌ {tableName}: {ex.Message}");
                }
            }

            AssetDatabase.Refresh();
            _exportDone = true;
            _statusMessage = $"✅ Экспортировано {count}/{tables.Length} таблиц в {_exportDir}";
            Debug.Log(sb.ToString());
        }

        private void ExportTableToCsv(StringTableCollection collection, string csvPath)
        {
            var locales = LocalizationEditorSettings.GetLocales();
            var sharedData = collection.SharedData;
            var entries = sharedData.Entries;

            using (var sw = new StreamWriter(csvPath, false, Encoding.UTF8))
            {
                // Header: Key, ru, en, zh, es, de, fr, pt, ja, hi
                var header = new List<string> { "Key" };
                foreach (var loc in locales)
                    header.Add(loc.Identifier.Code);
                sw.WriteLine(string.Join(",", header));

                // Rows
                foreach (var entry in entries)
                {
                    var row = new List<string> { CsvEscape(entry.Key) };
                    foreach (var loc in locales)
                    {
                        var table = collection.GetTable(loc.Identifier) as StringTable;
                        if (table != null)
                        {
                            var tableEntry = table.GetEntry(entry.Id);
                            row.Add(CsvEscape(tableEntry?.GetLocalizedString() ?? ""));
                        }
                        else
                        {
                            row.Add("");
                        }
                    }
                    sw.WriteLine(string.Join(",", row));
                }
            }
        }

        private void ImportCsv()
        {
            if (string.IsNullOrEmpty(_importPath) || !File.Exists(GetAbsolutePath(_importPath)))
            {
                _statusMessage = "❌ Файл не найден: " + _importPath;
                return;
            }

            var stats = new Dictionary<string, int> { ["updated"] = 0, ["new"] = 0, ["skipped"] = 0, ["errors"] = 0 };
            var sb = new StringBuilder();
            sb.AppendLine($"[LocTool] Import from: {_importPath}");

            try
            {
                var lines = File.ReadAllLines(GetAbsolutePath(_importPath), Encoding.UTF8);
                if (lines.Length < 2) { _statusMessage = "❌ CSV пуст или содержит только заголовок."; return; }

                // Parse header
                var header = lines[0].Split(',');
                // Determine which table this CSV is for (from filename or content)
                var tableName = Path.GetFileNameWithoutExtension(_importPath);

                var collection = GetCollection(tableName);
                if (collection == null) { _statusMessage = $"❌ Таблица '{tableName}' не найдена."; return; }

                // Find locale indices in header
                var localeMap = new Dictionary<int, string>(); // colIndex -> localeCode
                for (int c = 1; c < header.Length; c++)
                {
                    var code = header[c].Trim().Trim('"');
                    localeMap[c] = code;
                }

                // Process data rows
                for (int r = 1; r < lines.Length; r++)
                {
                    var cols = ParseCsvLine(lines[r]);
                    if (cols.Length < 2) { stats["skipped"]++; continue; }

                    var key = cols[0].Trim().Trim('"');
                    if (string.IsNullOrEmpty(key)) { stats["skipped"]++; continue; }

                    // Ensure entry exists in SharedData
                    var sharedEntry = collection.SharedData.GetEntry(key);
                    if (sharedEntry == null)
                    {
                        sharedEntry = collection.SharedData.AddKey(key);
                        stats["new"]++;
                    }

                    // Set values for each locale
                    for (int c = 1; c < Mathf.Min(cols.Length, header.Length); c++)
                    {
                        if (!localeMap.TryGetValue(c, out var localeCode)) continue;
                        var value = cols[c].Trim().Trim('"');
                        if (string.IsNullOrEmpty(value)) continue;

                        var locale = LocalizationEditorSettings.GetLocale(localeCode);
                        if (locale == null) continue;

                        var table = collection.GetTable(locale.Identifier) as StringTable;
                        if (table == null) continue;

                        var entry = table.GetEntry(sharedEntry.Id);
                        if (entry == null)
                            table.AddEntry(sharedEntry.Id, value);
                        else
                            entry.Value = value;
                    }
                    stats["updated"]++;
                }

                EditorUtility.SetDirty(collection);
                foreach (var loc in LocalizationEditorSettings.GetLocales())
                {
                    var t = collection.GetTable(loc.Identifier);
                    if (t != null) EditorUtility.SetDirty(t);
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                _statusMessage = $"✅ Импортировано: {stats["updated"]} обновлено, {stats["new"]} новых, {stats["skipped"]} пропущено";
            }
            catch (System.Exception ex)
            {
                _statusMessage = $"❌ Ошибка: {ex.Message}";
                Debug.LogError($"[LocTool] Import error: {ex}");
            }
        }

        private void CheckCoverage()
        {
            string[] tables = { "Static_Table", "UI_Table", "Dialogue_Table", "System_Table" };
            var locales = LocalizationEditorSettings.GetLocales();
            int missing = 0;
            var sb = new StringBuilder();
            sb.AppendLine("[LocTool] Coverage check:");

            foreach (var tableName in tables)
            {
                var collection = GetCollection(tableName);
                if (collection == null) continue;

                var shared = collection.SharedData;
                int tableMissing = 0;
                foreach (var entry in shared.Entries)
                {
                    foreach (var loc in locales)
                    {
                        if (loc.Identifier.Code == "ru") continue; // ru is always filled
                        var table = collection.GetTable(loc.Identifier) as StringTable;
                        if (table == null) continue;
                        var tEntry = table.GetEntry(entry.Id);
                        if (tEntry == null || string.IsNullOrEmpty(tEntry.GetLocalizedString()))
                            tableMissing++;
                    }
                }
                sb.AppendLine($"  {tableName}: {tableMissing} missing translations");
                missing += tableMissing;
            }

            _statusMessage = missing > 0
                ? $"⚠ {missing} ключей без перевода (см. Console)"
                : "✅ Все ключи переведены!";
            Debug.Log(sb.ToString());
        }

        // ===== Helpers =====

        private static StringTableCollection GetCollection(string tableName)
        {
            var path = $"Assets/_Project/Settings/Localization/{tableName}.asset";
            return AssetDatabase.LoadAssetAtPath<StringTableCollection>(path);
        }

        private static string GetAbsolutePath(string assetPath)
        {
            if (assetPath.StartsWith("Assets/") || assetPath.StartsWith("Assets\\"))
                return Path.Combine(Application.dataPath.Replace("/Assets", ""), assetPath);
            return assetPath;
        }

        private static void EnsureDirectory(string dir)
        {
            var abs = GetAbsolutePath(dir);
            if (!Directory.Exists(abs)) Directory.CreateDirectory(abs);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                var parts = dir.Split('/');
                var parent = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    var cur = $"{parent}/{parts[i]}";
                    if (!AssetDatabase.IsValidFolder(cur))
                        AssetDatabase.CreateFolder(parent, parts[i]);
                    parent = cur;
                }
            }
        }

        private static string CsvEscape(string val)
        {
            if (string.IsNullOrEmpty(val)) return "";
            if (val.Contains(",") || val.Contains("\"") || val.Contains("\n"))
                return $"\"{val.Replace("\"", "\"\"")}\"";
            return val;
        }

        private static string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var sb = new StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (inQuotes)
                {
                    if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"')
                    { sb.Append('"'); i++; }
                    else if (ch == '"')
                        inQuotes = false;
                    else
                        sb.Append(ch);
                }
                else
                {
                    if (ch == '"') inQuotes = true;
                    else if (ch == ',') { result.Add(sb.ToString()); sb.Clear(); }
                    else sb.Append(ch);
                }
            }
            result.Add(sb.ToString());
            return result.ToArray();
        }
    }
}
