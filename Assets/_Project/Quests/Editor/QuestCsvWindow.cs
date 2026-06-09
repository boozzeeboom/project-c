// M19-T4: QuestCsvWindow — EditorWindow для импорта/экспорта CSV
// Tools → ProjectC → Quests → CSV Import/Export

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Quests;

namespace ProjectC.Quests.Editor
{
    public class QuestCsvWindow : EditorWindow
    {
        private TextField _csvPathField;
        private ListView _previewList;
        private Label _statusLabel;
        private List<PreviewRow> _previewData = new List<PreviewRow>();
        private string _currentCsvPath;

        private class PreviewRow
        {
            public int line;
            public string questId;
            public string displayName;
            public string stage;
            public string objType;
            public string errors;
        }

        [MenuItem("Tools/ProjectC/Quests/CSV Import/Export", priority = 103)]
        public static void Open()
        {
            var w = GetWindow<QuestCsvWindow>();
            w.titleContent = new GUIContent("Quest CSV");
            w.minSize = new Vector2(700, 450);
            w.Show();
        }

        private void OnEnable()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.paddingTop = 6;
            root.style.paddingLeft = 6;
            root.style.paddingRight = 6;
            root.style.paddingBottom = 6;

            // ---- Import section ----
            var importHeader = new Label("Import from CSV");
            importHeader.style.fontSize = 14;
            importHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            importHeader.style.marginBottom = 4;
            root.Add(importHeader);

            var fileRow = new VisualElement();
            fileRow.style.flexDirection = FlexDirection.Row;
            fileRow.style.marginBottom = 4;

            _csvPathField = new TextField("CSV File") { name = "csv-path" };
            _csvPathField.style.flexGrow = 1;
            fileRow.Add(_csvPathField);

            var browseBtn = new Button(BrowseFile) { text = "Browse..." };
            browseBtn.style.marginLeft = 4;
            fileRow.Add(browseBtn);

            var previewBtn = new Button(PreviewCsv) { text = "Preview" };
            previewBtn.style.marginLeft = 4;
            fileRow.Add(previewBtn);

            var importBtn = new Button(ImportCsv) { text = "▶ Import" };
            importBtn.style.marginLeft = 4;
            importBtn.style.backgroundColor = new StyleColor(new Color(0.2f, 0.6f, 0.2f, 1f));
            fileRow.Add(importBtn);

            root.Add(fileRow);

            // Preview list
            _previewList = new ListView();
            _previewList.style.flexGrow = 1;
            _previewList.style.marginTop = 4;
            _previewList.style.marginBottom = 6;
            _previewList.makeItem = () =>
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.paddingTop = 2; row.style.paddingBottom = 2;

                var lineLbl = new Label(); lineLbl.name = "line"; lineLbl.style.width = 40; lineLbl.style.fontSize = 10;
                var idLbl = new Label(); idLbl.name = "qid"; idLbl.style.width = 140; idLbl.style.fontSize = 10;
                var nameLbl = new Label(); nameLbl.name = "dname"; nameLbl.style.width = 160; nameLbl.style.fontSize = 10;
                var stageLbl = new Label(); stageLbl.name = "stage"; stageLbl.style.width = 60; stageLbl.style.fontSize = 10;
                var typeLbl = new Label(); typeLbl.name = "otype"; typeLbl.style.width = 100; typeLbl.style.fontSize = 10;
                var errLbl = new Label(); errLbl.name = "err"; errLbl.style.flexGrow = 1; errLbl.style.fontSize = 10; errLbl.style.color = new StyleColor(Color.red);

                row.Add(lineLbl); row.Add(idLbl); row.Add(nameLbl); row.Add(stageLbl); row.Add(typeLbl); row.Add(errLbl);
                return row;
            };
            _previewList.bindItem = (e, i) =>
            {
                if (i >= _previewData.Count) return;
                var d = _previewData[i];
                e.Q<Label>("line").text = d.line.ToString();
                e.Q<Label>("qid").text = d.questId;
                e.Q<Label>("dname").text = d.displayName;
                e.Q<Label>("stage").text = d.stage;
                e.Q<Label>("otype").text = d.objType;
                e.Q<Label>("err").text = d.errors;
            };
            _previewList.itemsSource = _previewData;
            root.Add(_previewList);

            // ---- Import summary ----
            _statusLabel = new Label("(no file loaded)");
            _statusLabel.style.fontSize = 10;
            _statusLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f, 1f));
            root.Add(_statusLabel);

            // ---- Export section ----
            var exportHeader = new Label("Export to CSV");
            exportHeader.style.fontSize = 14;
            exportHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            exportHeader.style.marginTop = 8;
            exportHeader.style.marginBottom = 4;
            root.Add(exportHeader);

            var exportRow = new VisualElement();
            exportRow.style.flexDirection = FlexDirection.Row;

            var exportBtn = new Button(ExportAllQuests) { text = "📤 Export All Quests" };
            exportBtn.style.marginRight = 4;
            exportRow.Add(exportBtn);

            var exportSelectedBtn = new Button(ExportSelectedQuest) { text = "📤 Export Selected" };
            exportRow.Add(exportSelectedBtn);

            root.Add(exportRow);
        }

        private void BrowseFile()
        {
            var path = EditorUtility.OpenFilePanel("Select Quest CSV", "Assets/_Project/Quests/Import", "csv");
            if (!string.IsNullOrEmpty(path))
            {
                _csvPathField.value = path;
                PreviewCsv();
            }
        }

        private void PreviewCsv()
        {
            var path = _csvPathField.value;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                _statusLabel.text = "File not found: " + path;
                return;
            }

            _currentCsvPath = path;
            var (rows, header, errors) = QuestCsvParser.ParseFile(path);
            _previewData.Clear();

            foreach (var r in rows)
            {
                _previewData.Add(new PreviewRow
                {
                    line = r.lineNumber,
                    questId = r.Get("questId"),
                    displayName = r.Get("displayName"),
                    stage = r.Get("stageNum"),
                    objType = r.Get("objectiveType"),
                    errors = r.errors.Count > 0 ? string.Join("; ", r.errors) : ""
                });
            }

            // Add global errors as virtual rows
            foreach (var e in errors)
            {
                _previewData.Add(new PreviewRow
                {
                    line = 0, questId = "", displayName = "", stage = "", objType = "",
                    errors = "⚠ " + e
                });
            }

            _previewList.Rebuild();
            _statusLabel.text = $"Parsed {rows.Count} row(s), {errors.Count} global error(s)";
        }

        private void ImportCsv()
        {
            if (string.IsNullOrEmpty(_currentCsvPath))
            {
                EditorUtility.DisplayDialog("Import CSV", "Please select and preview a CSV file first.", "OK");
                return;
            }

            var result = QuestCsvImporter.Import(_currentCsvPath);
            var msg = $"Import complete:\n\nCreated: {result.created}\nUpdated: {result.updated}\nSkipped: {result.skipped}\n";
            if (result.errors.Count > 0)
                msg += $"\nErrors ({result.errors.Count}):\n" + string.Join("\n", result.errors.Take(5));
            if (result.warnings.Count > 0)
                msg += $"\nWarnings ({result.warnings.Count}):\n" + string.Join("\n", result.warnings.Take(5));

            EditorUtility.DisplayDialog("Import CSV", msg, "OK");
            _statusLabel.text = $"Import: {result.created} created, {result.updated} updated";
        }

        private void ExportAllQuests()
        {
            var db = AssetDatabase.LoadAssetAtPath<QuestDatabase>("Assets/_Project/Quests/Data/QuestDatabase.asset");
            if (db?.quests == null || db.quests.Length == 0)
            {
                EditorUtility.DisplayDialog("Export CSV", "No quests found in database.", "OK");
                return;
            }

            var path = EditorUtility.SaveFilePanel("Export Quests to CSV", "Assets/_Project/Quests/Import", "all_quests.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;

            int rows = QuestCsvExporter.Export(path, db.quests);
            EditorUtility.DisplayDialog("Export CSV", $"Exported {db.quests.Length} quest(s) as {rows} row(s) to:\n{path}", "OK");
            _csvPathField.value = path;
            PreviewCsv();
        }

        private void ExportSelectedQuest()
        {
            var db = AssetDatabase.LoadAssetAtPath<QuestDatabase>("Assets/_Project/Quests/Data/QuestDatabase.asset");
            if (db?.quests == null || db.quests.Length == 0) return;

            // Show a selection dialog for simplicity - export the first quest as example
            var path = EditorUtility.SaveFilePanel("Export Quest to CSV", "Assets/_Project/Quests/Import", "single_quest.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;

            int rows = QuestCsvExporter.Export(path, new[] { db.quests[0] });
            EditorUtility.DisplayDialog("Export CSV", $"Exported '{db.quests[0].questId}' as {rows} row(s).", "OK");
            _csvPathField.value = path;
            PreviewCsv();
        }
    }
}
#endif
