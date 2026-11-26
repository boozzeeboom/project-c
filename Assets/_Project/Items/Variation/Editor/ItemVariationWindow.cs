// =====================================================================================
// ItemVariationWindow.cs — генератор вариаций pickable-предметов (T-VAR01)
// =====================================================================================
// Документация: docs/pickble_items/variation_generator/03_EDITOR_TOOL.md
//
// Окно: Tools → Project C → Items → Variation Generator.
//   • создаёт профиль из выбранного ItemData;
//   • редактирует тиры/аффиксы/базовые статы;
//   • детерминированный preview по seed (тот же seed = тот же предмет);
//   • пакетная генерация и экспорт JSON для сервера;
//   • self-test без Play Mode.
// =====================================================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using ProjectC.Items.Variation.Dto;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Items.Variation.EditorTools
{
    public class ItemVariationWindow : EditorWindow
    {
        private const string VariationFolder = "Assets/_Project/Items/Data/Variation";
        private const string RegistryResourcesPath = "Items/Data/ItemRegistry";
        private const int MaxRowsShown = 200;

        private static readonly ItemStatId[] AllStats = (ItemStatId[])Enum.GetValues(typeof(ItemStatId));

        private ItemVariationProfileAsset _asset;
        private SerializedObject _serialized;

        private long _seed = 12345L;
        private int _luckBps;
        private ItemVariationResult _preview;
        private string _previewError;

        private long _batchStartSeed = 1L;
        private int _batchCount = 20;
        private readonly List<ItemInstanceDto> _batch = new List<ItemInstanceDto>();

        private Vector2 _leftScroll;
        private Vector2 _rightScroll;
        private List<ItemVariationIssue> _issues = new List<ItemVariationIssue>();
        private bool _issuesFoldout = true;
        private string _status = "Выберите ItemData в Project и нажмите 'Create from Selected ItemData'.";

        [MenuItem("Tools/Project C/Items/Variation Generator", priority = 210)]
        public static void Open()
        {
            var window = GetWindow<ItemVariationWindow>();
            window.titleContent = new GUIContent("Item Variations");
            window.minSize = new Vector2(900f, 560f);
            window.Show();
        }

        public static void OpenWith(ItemVariationProfileAsset asset)
        {
            var window = GetWindow<ItemVariationWindow>();
            window.titleContent = new GUIContent("Item Variations");
            window.minSize = new Vector2(900f, 560f);
            window.SetAsset(asset);
            window.Show();
        }

        private void OnEnable()
        {
            if (_asset != null) SetAsset(_asset);
        }

        private void OnGUI()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorGUILayout.HelpBox("Unity компилируется/обновляет ассеты. Дождитесь окончания.", MessageType.Info);
                return;
            }

            DrawToolbar();
            if (_asset == null)
            {
                EditorGUILayout.HelpBox(
                    "Нет выбранного профиля.\n\n" +
                    "1. Выберите ItemData (или WeaponItemData/ClothingItemData/ModuleItemData) в Project.\n" +
                    "2. Нажмите 'Create from Selected ItemData'.\n\n" +
                    "Профиль сохранится в " + VariationFolder + ".",
                    MessageType.Info);
                return;
            }

            if (_serialized == null || _serialized.targetObject == null)
            {
                SetAsset(_asset);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(Mathf.Max(360f, position.width * 0.52f)));
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
            DrawProfileEditor();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            GUILayout.Space(6f);

            EditorGUILayout.BeginVertical();
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
            DrawPreviewSection();
            EditorGUILayout.Space(10f);
            DrawBatchSection();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Create from Selected ItemData", EditorStyles.toolbarButton, GUILayout.Width(210f)))
                CreateProfileFromSelection();

            var newAsset = (ItemVariationProfileAsset)EditorGUILayout.ObjectField(
                _asset, typeof(ItemVariationProfileAsset), false, GUILayout.Width(260f));
            if (newAsset != _asset) SetAsset(newAsset);

            using (new EditorGUI.DisabledScope(_asset == null))
            {
                if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(70f))) ValidateProfile();
                if (GUILayout.Button("Self-test", EditorStyles.toolbarButton, GUILayout.Width(70f))) RunSelfTest();
                if (GUILayout.Button("Server JSON…", EditorStyles.toolbarButton, GUILayout.Width(105f))) ExportServerJson();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(_status, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawProfileEditor()
        {
            _serialized.Update();

            EditorGUILayout.LabelField("Профиль вариации", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_serialized.FindProperty("baseItem"), new GUIContent("Base ItemData"));
            EditorGUILayout.PropertyField(_serialized.FindProperty("notes"), new GUIContent("Notes"));

            SerializedProperty profileProp = _serialized.FindProperty("profile");
            EditorGUILayout.PropertyField(profileProp, new GUIContent("Rules"), true);
            _serialized.ApplyModifiedProperties();

            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Capture base stats from item"))
            {
                Undo.RecordObject(_asset, "Capture variation base stats");
                _asset.CaptureFromItem(GetItemId(_asset.baseItem));
                EditorUtility.SetDirty(_asset);
                _serialized.Update();
                ValidateProfile();
                _preview = null;
            }
            if (GUILayout.Button("Sync name/id"))
            {
                Undo.RecordObject(_asset, "Sync variation identity");
                SyncIdentity();
                EditorUtility.SetDirty(_asset);
                _serialized.Update();
                ValidateProfile();
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Rebuild default tiers + affixes (replaces lists)"))
            {
                if (EditorUtility.DisplayDialog(
                    "Rebuild variation rules",
                    "Тиры и аффиксы будут заменены дефолтными для baseItem.\nБазовые статы и seedSalt сохранятся.",
                    "Rebuild", "Cancel"))
                {
                    Undo.RecordObject(_asset, "Rebuild variation rules");
                    _asset.profile.tiers = ItemVariationProfileFactory.BuildDefaultTiers();
                    _asset.profile.modifiers = ItemVariationProfileFactory.BuildDefaultModifiers(_asset.baseItem);
                    EditorUtility.SetDirty(_asset);
                    _serialized.Update();
                    ValidateProfile();
                    _preview = null;
                }
            }

            DrawIssues();
        }

        private void DrawIssues()
        {
            int errors = 0;
            int warnings = 0;
            for (int i = 0; i < _issues.Count; i++)
            {
                if (_issues[i].severity == ItemVariationSeverity.Error) errors++;
                else if (_issues[i].severity == ItemVariationSeverity.Warning) warnings++;
            }

            if (errors == 0 && warnings == 0 && _issues.Count == 0)
            {
                EditorGUILayout.HelpBox("Валидация: ок.", MessageType.Info);
                return;
            }

            _issuesFoldout = EditorGUILayout.Foldout(
                _issuesFoldout,
                $"Валидация: ошибок {errors}, предупреждений {warnings}",
                true);
            if (!_issuesFoldout) return;

            int shown = 0;
            for (int i = 0; i < _issues.Count && shown < 30; i++)
            {
                MessageType type = _issues[i].severity == ItemVariationSeverity.Error
                    ? MessageType.Error
                    : (_issues[i].severity == ItemVariationSeverity.Warning ? MessageType.Warning : MessageType.Info);
                EditorGUILayout.HelpBox(_issues[i].message, type);
                shown++;
            }
            if (_issues.Count > shown)
                EditorGUILayout.LabelField($"… ещё {_issues.Count - shown} сообщений (см. Console).", EditorStyles.miniLabel);
        }

        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("Preview (deterministic by seed)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            _seed = EditorGUILayout.LongField("Seed", _seed);
            if (GUILayout.Button("Random", GUILayout.Width(70f)))
            {
                _seed = VariationSeed.NewRandomSeed();
                RollPreview();
            }
            if (GUILayout.Button("Roll", GUILayout.Width(60f))) RollPreview();
            EditorGUILayout.EndHorizontal();

            _luckBps = EditorGUILayout.IntSlider("Luck (bps)", _luckBps, -5000, 5000);
            EditorGUILayout.LabelField(
                "Удача сдвигает веса высоких тиров: +1000 bps = +10% к весу каждого следующего тира.",
                EditorStyles.miniLabel);

            if (!string.IsNullOrEmpty(_previewError))
            {
                EditorGUILayout.HelpBox(_previewError, MessageType.Error);
                return;
            }

            if (_preview == null)
            {
                EditorGUILayout.HelpBox("Нажмите 'Roll', чтобы увидеть предмет.", MessageType.None);
                return;
            }

            DrawPreviewCard(_preview);
        }

        private void DrawPreviewCard(ItemVariationResult result)
        {
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, wordWrap = true };
            EditorGUILayout.LabelField(result.displayName, titleStyle);

            Color tierColor = ParseColor(result.tierColorHex, Color.white);
            Color previousColor = GUI.color;
            GUI.color = tierColor;
            EditorGUILayout.LabelField($"{result.tierName} · качество {result.quality}/100 · {result.finalPrice} CR",
                EditorStyles.boldLabel);
            GUI.color = previousColor;

            EditorGUILayout.LabelField(
                $"profile: {result.profileId}   ·   seed: {result.seed}   ·   luck: {result.luckBps} bps   ·   rolls: {result.RollCount}",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Статы", EditorStyles.boldLabel);
            for (int i = 0; i < AllStats.Length; i++)
            {
                ItemStatId stat = AllStats[i];
                int baseValue = result.baseStats.Get(stat);
                int finalValue = result.finalStats.Get(stat);
                if (baseValue == 0 && finalValue == 0) continue;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(ItemStatCatalog.Get(stat).DisplayName, GUILayout.Width(120f));
                GUILayout.Label(ItemStatCatalog.Format(stat, baseValue), GUILayout.Width(100f));
                GUILayout.Label("→", GUILayout.Width(18f));
                GUILayout.Label(ItemStatCatalog.Format(stat, finalValue), GUILayout.Width(100f));
                int delta = finalValue - baseValue;
                if (delta != 0)
                {
                    Color old = GUI.color;
                    GUI.color = delta > 0 ? new Color(0.4f, 0.95f, 0.45f) : new Color(1f, 0.55f, 0.5f);
                    GUILayout.Label(ItemStatCatalog.FormatSigned(stat, delta));
                    GUI.color = old;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Аффиксы", EditorStyles.boldLabel);
            if (result.rolls == null || result.rolls.Count == 0)
            {
                EditorGUILayout.LabelField("(нет аффиксов)", EditorStyles.miniLabel);
            }
            else
            {
                for (int i = 0; i < result.rolls.Count; i++)
                {
                    ItemStatRoll roll = result.rolls[i];
                    string affix = ResolveAffixName(roll.modifierIndex);
                    string value = FormatRoll(roll);
                    string multiplier = roll.tierMultiplierBps != 10000
                        ? $"  (тир ×{roll.tierMultiplierBps / 10000f:0.##})"
                        : string.Empty;
                    EditorGUILayout.LabelField(
                        $"{affix}: {ItemStatCatalog.Get(roll.stat).DisplayName} {value}{multiplier}");
                }
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy result JSON")) EditorGUIUtility.systemCopyBuffer = ItemVariationJson.ResultToJson(result);
            if (GUILayout.Button("Use seed for batch")) _batchStartSeed = result.seed;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBatchSection()
        {
            EditorGUILayout.LabelField("Batch", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _batchCount = Mathf.Clamp(EditorGUILayout.IntField("Count", _batchCount), 1, 500);
            _batchStartSeed = EditorGUILayout.LongField("Start Seed", _batchStartSeed);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate Batch")) GenerateBatch();
            using (new EditorGUI.DisabledScope(_batch.Count == 0))
            {
                if (GUILayout.Button("Export Batch JSON…")) ExportBatchJson();
                if (GUILayout.Button("Clear")) _batch.Clear();
            }
            EditorGUILayout.EndHorizontal();

            if (_batch.Count == 0)
            {
                EditorGUILayout.LabelField("Пакет пуст. Seeds идут подряд: start, start+1, …", EditorStyles.miniLabel);
                return;
            }

            int rows = Mathf.Min(_batch.Count, MaxRowsShown);
            for (int i = 0; i < rows; i++)
            {
                ItemInstanceDto dto = _batch[i];
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("→", GUILayout.Width(24f)))
                {
                    _seed = dto.seed;
                    _luckBps = dto.luckBps;
                    RollPreview();
                }
                GUILayout.Label(dto.seed.ToString(), GUILayout.Width(95f));
                GUILayout.Label(dto.displayName, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{dto.tierName} · q{dto.quality} · {dto.finalPrice} CR", GUILayout.Width(180f));
                EditorGUILayout.EndHorizontal();
            }
            if (_batch.Count > rows)
                EditorGUILayout.LabelField($"… показаны первые {rows} из {_batch.Count}.", EditorStyles.miniLabel);
        }

        private void SetAsset(ItemVariationProfileAsset asset)
        {
            _asset = asset;
            _serialized = asset != null ? new SerializedObject(asset) : null;
            _preview = null;
            _previewError = null;
            _batch.Clear();
            _issues = new List<ItemVariationIssue>();
            if (asset != null)
            {
                titleContent = new GUIContent("Item Variations — " + asset.name);
                ValidateProfile();
            }
            Repaint();
        }

        private void CreateProfileFromSelection()
        {
            var item = Selection.activeObject as ItemData;
            if (item == null)
            {
                EditorUtility.DisplayDialog(
                    "Variation Generator",
                    "Выберите ItemData (или его наследника) в окне Project.",
                    "OK");
                return;
            }

            EnsureFolder(VariationFolder);
            string fileName = ItemVariationProfileFactory.MakeProfileId(item);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{VariationFolder}/{fileName}.asset");

            var asset = CreateInstance<ItemVariationProfileAsset>();
            asset.baseItem = item;
            asset.profile = ItemVariationProfileFactory.CreateDefault(item, GetItemId(item));
            asset.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(asset);
            SetAsset(asset);
            _status = "Создан профиль: " + path;
        }

        private void RollPreview()
        {
            _previewError = null;
            if (_asset == null || _asset.profile == null)
            {
                _preview = null;
                _previewError = "Профиль не выбран.";
                return;
            }

            bool ok = ItemVariationGenerator.TryRoll(
                _asset.profile, _seed, out ItemVariationResult result, out string error, _luckBps);
            _preview = ok ? result : null;
            if (!ok) _previewError = error;
            Repaint();
        }

        private void GenerateBatch()
        {
            _batch.Clear();
            _previewError = null;
            if (_asset == null || _asset.profile == null) return;

            for (int i = 0; i < _batchCount; i++)
            {
                long seed = unchecked(_batchStartSeed + i);
                if (ItemVariationGenerator.TryRoll(
                    _asset.profile, seed, out ItemVariationResult result, out string error, _luckBps))
                {
                    _batch.Add(ItemInstanceDto.FromResult(result, i + 1));
                }
                else
                {
                    _previewError = error;
                    break;
                }
            }
            _status = $"Сгенерировано: {_batch.Count}";
            Repaint();
        }

        private void ValidateProfile()
        {
            _issues = _asset != null && _asset.profile != null
                ? ItemVariationProfileFactory.Validate(_asset.profile)
                : new List<ItemVariationIssue>();

            for (int i = 0; i < _issues.Count; i++)
            {
                if (_issues[i].severity == ItemVariationSeverity.Error)
                    _status = "Ошибка: " + _issues[i].message;
            }
        }

        private void SendSelfTestReport(string report)
        {
            Debug.Log(report);
            EditorUtility.DisplayDialog("Variation Self-Test", report, "OK");
        }

        private void RunSelfTest()
        {
            if (_asset == null || _asset.profile == null) return;
            SendSelfTestReport(ItemVariationSelfTest.Run(_asset.profile));
        }

        private void ExportServerJson()
        {
            if (_asset == null || _asset.profile == null) return;
            EnsureFolder(VariationFolder);

            string defaultName = SanitizeFileName(_asset.profile.profileId) + "_server.json";
            string path = EditorUtility.SaveFilePanelInProject(
                "Export variation profile for server", defaultName, "json",
                "JSON с профилем и снапшотом базовых статов", VariationFolder);
            if (string.IsNullOrEmpty(path)) return;

            File.WriteAllText(path, _asset.ToServerJson());
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<TextAsset>(path));
            _status = "Экспортирован: " + path;
        }

        private void ExportBatchJson()
        {
            if (_batch.Count == 0) return;
            EnsureFolder(VariationFolder);

            var snapshot = new ItemInstanceRegistrySnapshot
            {
                schemaVersion = ItemVariationJson.CurrentSchemaVersion,
                nextId = _batch.Count + 1,
                instances = _batch.ToArray(),
            };

            string defaultName = SanitizeFileName(_asset.profile.profileId) + "_batch.json";
            string path = EditorUtility.SaveFilePanelInProject(
                "Export variation batch", defaultName, "json",
                "JSON с пакетом сгенерированных инстансов", VariationFolder);
            if (string.IsNullOrEmpty(path)) return;

            File.WriteAllText(path, ItemVariationJson.ToJson(snapshot));
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<TextAsset>(path));
            _status = "Экспортирован пакет: " + path;
        }

        private void SyncIdentity()
        {
            if (_asset == null || _asset.profile == null || _asset.baseItem == null) return;
            _asset.profile.baseItemName = _asset.baseItem.itemName;
            _asset.profile.baseItemId = GetItemId(_asset.baseItem);
            if (string.IsNullOrEmpty(_asset.profile.profileId))
                _asset.profile.profileId = ItemVariationProfileFactory.MakeProfileId(_asset.baseItem);
            if (string.IsNullOrEmpty(_asset.profile.displayName))
                _asset.profile.displayName = _asset.baseItem.itemName;
        }

        private string ResolveAffixName(int modifierIndex)
        {
            if (_asset == null || _asset.profile == null || _asset.profile.modifiers == null)
                return "affix[" + modifierIndex + "]";
            if (modifierIndex < 0 || modifierIndex >= _asset.profile.modifiers.Count)
                return "affix[" + modifierIndex + "]";

            ItemVariationModifier modifier = _asset.profile.modifiers[modifierIndex];
            if (modifier == null) return "affix[" + modifierIndex + "]";
            return string.IsNullOrEmpty(modifier.affixName) ? modifier.affixId : modifier.affixName;
        }

        private static string FormatRoll(ItemStatRoll roll)
        {
            string flat = roll.flat != 0 ? ItemStatCatalog.FormatSigned(roll.stat, roll.flat) : null;
            string percent = roll.percentBps != 0
                ? (roll.percentBps >= 0 ? "+" : "") + (roll.percentBps / 100f).ToString("0.##") + "%"
                : null;

            if (flat != null && percent != null) return flat + " / " + percent;
            return flat ?? percent ?? "0";
        }

        private static int GetItemId(ItemData item)
        {
            if (item == null) return 0;
            var registry = Resources.Load<ItemRegistry>(RegistryResourcesPath);
            if (registry != null && registry.TryGetId(item, out int id)) return id;
            return 0;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path);
            string leaf = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;
            parent = parent.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "variation";
            var sb = new System.Text.StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            }
            return sb.ToString();
        }

        private static Color ParseColor(string hex, Color fallback)
        {
            return !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out Color color)
                ? color
                : fallback;
        }
    }
}
#endif
