// =====================================================================================
// ResourcesCsvWindow.cs — EditorWindow для импорта/экспорта Resources CSV
// =====================================================================================
// Документация:
//   • docs/Markets/Resources_import_export/02_DESIGN.md §5
//   • docs/Markets/Resources_import_export/03_TICKETS.md T-IE06
//
// Назначение: UI Toolkit окно для импорта/экспорта Resources CSV. Прецедент — QuestCsvWindow
// (Assets/_Project/Quests/Editor/QuestCsvWindow.cs), адаптированный под 4 секции + phase2 recipes.
//
// Pitfall #16b: НЕ ставить `display: none !important` на .dialog-root в USS — иначе
// inline style.display = Flex в Show() не сработает. Initial = inline `display: None`.
//
// T-IE06 (2026-06-12): MVP. ~280 LOC.
// =====================================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.Items.Editor
{
    public class ResourcesCsvWindow : EditorWindow
    {
        // State
        private string _csvPath = "";
        private Dictionary<string, List<ResourcesCsvRow>> _blocks;
        private List<string> _globalErrors = new List<string>();
        private ResourcesCsvImporter.ImportResult _lastResult;
        private string _lastStatus = "Idle. Choose a CSV file to begin.";

        // UI refs
        private TextField _csvPathField;
        private TextField _statusField;
        private ListView _previewList;
        private Button _importBtn;
        private Button _exportBtn;
        private VisualElement _resultsRoot;
        private Label _importedLabel;

        // T-IE08: Prune UI (visible only after Preview, when prune block is present).
        private VisualElement _pruneRoot;
        private Label _pruneModeLabel;
        private Label _pruneSummaryLabel;

        // Flat row для ListView (одна строка CSV = один FlatRow, не важно из какой секции).
        private class FlatRow
        {
            public string block;
            public int lineNumber;
            public string key;       // itemName / tradeItemId / "rate@..."
            public string name;      // display name / inventoryItemName
            public bool hasError;
            public string errors;
        }
        private List<FlatRow> _flatRows = new List<FlatRow>();

        [MenuItem("Tools/ProjectC/Resources/CSV Import/Export", priority = 200)]
        public static void Open()
        {
            var w = GetWindow<ResourcesCsvWindow>();
            w.titleContent = new GUIContent("Resources CSV");
            w.minSize = new Vector2(800, 500);
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

            // ---- File path section ----
            var fileRow = new VisualElement();
            fileRow.style.flexDirection = FlexDirection.Row;
            fileRow.style.marginBottom = 4;

            _csvPathField = new TextField("CSV File") { name = "csv-path", value = _csvPath };
            _csvPathField.style.flexGrow = 1;
            fileRow.Add(_csvPathField);

            var browseBtn = new Button(BrowseFile) { text = "Browse..." };
            browseBtn.style.marginLeft = 4;
            fileRow.Add(browseBtn);

            var previewBtn = new Button(PreviewCsv) { text = "Preview" };
            previewBtn.style.marginLeft = 4;
            fileRow.Add(previewBtn);

            _importBtn = new Button(ImportCsv) { text = "▶ Import" };
            _importBtn.style.marginLeft = 4;
            _importBtn.style.backgroundColor = new StyleColor(new Color(0.2f, 0.6f, 0.2f, 1f));
            fileRow.Add(_importBtn);

            _exportBtn = new Button(ExportCsv) { text = "Export" };
            _exportBtn.style.marginLeft = 4;
            _exportBtn.style.backgroundColor = new StyleColor(new Color(0.4f, 0.4f, 0.6f, 1f));
            fileRow.Add(_exportBtn);

            root.Add(fileRow);

            // ---- Parse summary ----
            _importedLabel = new Label("No CSV parsed yet.");
            _importedLabel.style.fontSize = 11;
            _importedLabel.style.marginBottom = 4;
            _importedLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_importedLabel);

            // ---- T-IE08: Prune section (visible only if CSV has # block=prune) ----
            _pruneRoot = new VisualElement();
            _pruneRoot.style.marginTop = 4;
            _pruneRoot.style.paddingTop = 4;
            _pruneRoot.style.paddingBottom = 4;
            _pruneRoot.style.borderTopWidth = 1;
            _pruneRoot.style.borderTopColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
            _pruneRoot.style.display = DisplayStyle.None;  // hidden by default

            var pruneHeader = new Label("── Prune mode (T-IE08) ──");
            pruneHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            _pruneRoot.Add(pruneHeader);

            _pruneModeLabel = new Label("Mode: (no prune block in CSV)");
            _pruneModeLabel.style.fontSize = 11;
            _pruneModeLabel.style.marginTop = 2;
            _pruneRoot.Add(_pruneModeLabel);

            _pruneSummaryLabel = new Label("");
            _pruneSummaryLabel.style.fontSize = 10;
            _pruneSummaryLabel.style.whiteSpace = WhiteSpace.Normal;
            _pruneSummaryLabel.style.marginTop = 2;
            _pruneRoot.Add(_pruneSummaryLabel);

            root.Add(_pruneRoot);

            // ---- Preview ListView ----
            _previewList = new ListView
            {
                fixedItemHeight = 22,
                selectionType = SelectionType.None,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
            };
            _previewList.style.flexGrow = 1;
            _previewList.style.marginTop = 4;
            _previewList.style.marginBottom = 4;
            _previewList.makeItem = MakePreviewRow;
            _previewList.bindItem = BindPreviewRow;
            _previewList.itemsSource = _flatRows;
            root.Add(_previewList);

            // ---- Results section (hidden until Import runs) ----
            _resultsRoot = new VisualElement();
            _resultsRoot.style.marginTop = 4;
            _resultsRoot.style.paddingTop = 4;
            _resultsRoot.style.borderTopWidth = 1;
            _resultsRoot.style.borderTopColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
            root.Add(_resultsRoot);

            // ---- Status bar ----
            _statusField = new TextField("Status") { value = _lastStatus };
            _statusField.isReadOnly = true;
            _statusField.style.marginTop = 4;
            root.Add(_statusField);

            RefreshButtons();
        }

        // ============================================================
        // ListView row maker/binder
        // ============================================================

        private VisualElement MakePreviewRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.paddingTop = 1;
            row.style.paddingBottom = 1;
            row.style.alignItems = Align.Center;

            var blockLbl = new Label { name = "block" };   blockLbl.style.width = 90;  blockLbl.style.fontSize = 10;
            var lineLbl = new Label { name = "line" };     lineLbl.style.width = 40;   lineLbl.style.fontSize = 10;
            var keyLbl = new Label { name = "key" };       keyLbl.style.width = 180;  keyLbl.style.fontSize = 10;
            var nameLbl = new Label { name = "name" };     nameLbl.style.width = 200;  nameLbl.style.fontSize = 10;
            var issuesLbl = new Label { name = "issues" }; issuesLbl.style.flexGrow = 1; issuesLbl.style.fontSize = 10;

            row.Add(blockLbl);
            row.Add(lineLbl);
            row.Add(keyLbl);
            row.Add(nameLbl);
            row.Add(issuesLbl);
            return row;
        }

        private void BindPreviewRow(VisualElement elem, int i)
        {
            if (i < 0 || i >= _flatRows.Count) return;
            var d = _flatRows[i];
            elem.Q<Label>("block").text = d.block;
            elem.Q<Label>("line").text = d.lineNumber.ToString();
            elem.Q<Label>("key").text = d.key ?? "";
            elem.Q<Label>("name").text = d.name ?? "";
            var issues = elem.Q<Label>("issues");
            issues.text = d.hasError ? d.errors : "";
            issues.style.color = d.hasError
                ? new StyleColor(new Color(0.9f, 0.4f, 0.4f, 1f))
                : new StyleColor(new Color(0.6f, 0.8f, 0.6f, 1f));
        }

        // ============================================================
        // Buttons
        // ============================================================

        private void BrowseFile()
        {
            var path = EditorUtility.OpenFilePanel("Select Resources CSV", "", "csv");
            if (string.IsNullOrEmpty(path)) return;
            _csvPathField.value = path;
            _csvPath = path;
            _lastStatus = $"File selected: {Path.GetFileName(path)}";
            _statusField.value = _lastStatus;
            RefreshButtons();
        }

        private void PreviewCsv()
        {
            var path = _csvPathField.value;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                _lastStatus = "File not found: " + path;
                _statusField.value = _lastStatus;
                return;
            }

            _csvPath = path;
            var tuple = ResourcesCsvParser.ParseFile(path);
            _blocks = tuple.blocks;
            _globalErrors = tuple.globalErrors;
            ResourcesCsvCrossValidator.Validate(_blocks, _globalErrors);

            // Flatten rows for ListView (limit to first 200 for performance).
            _flatRows.Clear();
            int total = 0;
            foreach (var kv in _blocks)
            {
                foreach (var r in kv.Value)
                {
                    total++;
                    if (_flatRows.Count >= 200) continue;
                    _flatRows.Add(new FlatRow
                    {
                        block = r.block,
                        lineNumber = r.lineNumber,
                        key = GetRowKey(r),
                        name = GetRowName(r),
                        hasError = r.HasError,
                        errors = r.HasError ? string.Join("; ", r.errors) : "OK",
                    });
                }
            }

            _previewList.RefreshItems();

            // Update summary.
            int errCount = 0;
            foreach (var kv in _blocks)
                foreach (var r in kv.Value) if (r.HasError) errCount++;
            string summary = $"Parsed {total} rows";
            if (_flatRows.Count < total) summary += $" (showing first {_flatRows.Count})";
            summary += ". " + ResourcesCsvCrossValidator.Summary(_blocks);
            if (_globalErrors.Count > 0) summary += $". {_globalErrors.Count} global errors.";
            if (errCount > 0) summary += $" {errCount} row errors.";
            _importedLabel.text = summary;

            // T-IE08: Update Prune summary (if prune block present).
            UpdatePruneSummary();

            _lastStatus = "Preview ready. Click Import to apply.";
            _statusField.value = _lastStatus;
            RefreshButtons();
        }

        /// <summary>T-IE08: обновляет UI prune-секции (mode + applyTo) на основе распарсенного CSV.</summary>
        private void UpdatePruneSummary()
        {
            if (_pruneRoot == null) return;
            if (_blocks == null || !_blocks.TryGetValue("prune", out var pruneRows) || pruneRows.Count == 0)
            {
                _pruneRoot.style.display = DisplayStyle.None;
                return;
            }
            var row = pruneRows[0];
            var mode = (row.Get("mode") ?? "none").Trim();
            var applyTo = (row.Get("applyTo") ?? "all").Trim();
            _pruneModeLabel.text = $"Mode: {mode}    ApplyTo: {applyTo}";
            _pruneSummaryLabel.text =
                "Prune will: delete items NOT in CSV (inventory/tradeItems) " +
                "and remove from registry/db/arrays. mode='none' is a no-op. " +
                "mode='orphan' blocks delete if there are references. mode='replace' ignores references. " +
                "Saved runtime state (InventoryData/Warehouse/CargoData/ContractData) is NOT scanned — clear saves before testing.";
            _pruneRoot.style.display = DisplayStyle.Flex;
        }

        private void ImportCsv()
        {
            if (_blocks == null)
            {
                _lastStatus = "Preview first, then import.";
                _statusField.value = _lastStatus;
                return;
            }

            // T-IE08: confirm dialog for prune modes.
            if (_blocks.TryGetValue("prune", out var pruneRows) && pruneRows.Count > 0)
            {
                var mode = (pruneRows[0].Get("mode") ?? "none").Trim().ToLowerInvariant();
                if (mode == "replace")
                {
                    bool ok1 = EditorUtility.DisplayDialog("Prune: REPLACE",
                        "This will DELETE ALL existing data in the selected sections (not in CSV).\n\n" +
                        "If you have NOT backed up via git, do Ctrl+Z in Unity or close without saving.\n\n" +
                        "Continue?",
                        "Yes", "Cancel");
                    if (!ok1) { _lastStatus = "Import cancelled (replace mode)."; _statusField.value = _lastStatus; return; }
                    bool ok2 = EditorUtility.DisplayDialog("Prune: REPLACE — final confirm",
                        "FINAL CONFIRMATION.\n\n" +
                        "Pressing Yes will permanently delete all out-of-CSV items, registry entries, " +
                        "market items, and exchange rates in the selected sections.\n\n" +
                        "Are you absolutely sure?",
                        "Yes, delete", "Cancel");
                    if (!ok2) { _lastStatus = "Import cancelled (replace mode)."; _statusField.value = _lastStatus; return; }
                }
                else if (mode == "orphan")
                {
                    bool ok = EditorUtility.DisplayDialog("Prune: ORPHAN",
                        "This will delete items NOT in CSV (only those without references in scenes/SOs).\n\n" +
                        "Items with references (PickupItem/LootTable/RecipeData/etc) will be BLOCKED (kept).\n\n" +
                        "Saved runtime state (InventoryData/Warehouse/CargoData/ContractData) cannot be scanned " +
                        "and may break if you delete an ItemData.\n\n" +
                        "Continue?",
                        "Yes", "Cancel");
                    if (!ok) { _lastStatus = "Import cancelled (orphan mode)."; _statusField.value = _lastStatus; return; }
                }
            }

            _lastResult = ResourcesCsvImporter.Apply(_blocks);
            _lastStatus = $"Import: C={_lastResult.created}, U={_lastResult.updated}, S={_lastResult.skipped}, E={_lastResult.errors.Count}, W={_lastResult.warnings.Count}";
            _statusField.value = _lastStatus;
            RenderResults(_lastResult);
            EditorUtility.DisplayDialog("Import complete",
                $"Created: {_lastResult.created}\nUpdated: {_lastResult.updated}\nSkipped: {_lastResult.skipped}\nErrors: {_lastResult.errors.Count}\nWarnings: {_lastResult.warnings.Count}",
                "OK");
            Debug.Log($"[ResourcesCsvWindow] Import done: {_lastStatus}");
        }

        private void ExportCsv()
        {
            var path = EditorUtility.SaveFilePanel("Export Resources CSV",
                Application.dataPath, "Resources_Import.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            int rows = ResourcesCsvExporter.Export(path);
            _lastStatus = $"Exported {rows} rows to {path}";
            _statusField.value = _lastStatus;
            EditorUtility.DisplayDialog("Export complete", $"Exported {rows} rows to:\n{path}", "OK");
            Debug.Log($"[ResourcesCsvWindow] Exported {rows} rows to {path}");
        }

        // ============================================================
        // Helpers
        // ============================================================

        private void RefreshButtons()
        {
            bool canImport = _blocks != null && _globalErrors.Count == 0;
            if (_importBtn != null) _importBtn.SetEnabled(canImport);
        }

        private void RenderResults(ResourcesCsvImporter.ImportResult result)
        {
            _resultsRoot.Clear();
            if (result.errors.Count > 0)
            {
                var errLbl = new Label($"Errors ({result.errors.Count}):");
                errLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                _resultsRoot.Add(errLbl);
                foreach (var e in result.errors)
                {
                    var l = new Label("  " + e);
                    l.style.color = new StyleColor(new Color(0.9f, 0.4f, 0.4f, 1f));
                    _resultsRoot.Add(l);
                }
            }
            if (result.warnings.Count > 0)
            {
                var wLbl = new Label($"Warnings ({result.warnings.Count}):");
                wLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                _resultsRoot.Add(wLbl);
                foreach (var w in result.warnings)
                {
                    var l = new Label("  " + w);
                    l.style.color = new StyleColor(new Color(0.9f, 0.8f, 0.4f, 1f));
                    _resultsRoot.Add(l);
                }
            }
        }

        private string GetRowKey(ResourcesCsvRow r)
        {
            return r.Get("itemName") ?? r.Get("tradeItemId") ?? $"<{r.block}>";
        }

        private string GetRowName(ResourcesCsvRow r)
        {
            return r.Get("displayName") ?? r.Get("inventoryItemName") ?? "";
        }
    }
}
#endif
