using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// T-FO09S: 1-клик регистрация нового контента в closed-world каталоге пилота.
    /// Механика — та же, что ручная процедура 09S §3.2 (маркер + observation + entry
    /// + хэши соседей + dependencyHash + digest), но считает и сверяет машина.
    /// Решение остаётся за человеком: reviewNote обязателен для каждой новой записи,
    /// применение — только по кнопке после превью diff. Скан (включая DryRun) read-only.
    /// Алгоритмы SourceId/LayoutHash — из AuditGlobalSceneCatalog, не дублировать.
    /// T-FO06L follow-up: скан также находит записи, чей объект исчез из сохранённой сцены,
    /// и применение может их удалить. Без этого каталог навсегда сохранял фантомные sourceId,
    /// а пилот отказывал в старте с catalog_markers_bound_mismatch.
    /// T-FO06L follow-up 2: структурный дрейф (репарент/корень/сетевость) больше не «ручной разбор»
    /// в красных ошибках: для Unmanaged-записей каталог догоняется автоматически (entry + observation,
    /// пилот затем стартует), для управляемых репарент меняет позу/активацию — запись блокируется
    /// с причиной. Раньше такое расхождение не чинилось ничем, кроме правки ассета руками, и старт
    /// падал с authored_parent_binding_mismatch.
    /// </summary>
    public sealed class BakePilotSceneCatalog : EditorWindow
    {
        private const string CatalogPath = "Assets/_Project/Prefabs/FloatingOrigin/GlobalMotionPilotSceneCatalog.asset";
        private const string ProfilePath = "Assets/_Project/Prefabs/FloatingOrigin/GlobalMotionPilotProfile.asset";
        private const string BootstrapScenePath = "Assets/_Project/Scenes/BootstrapScene.unity";

        private static readonly HashSet<string> KnownDdolComponentNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "NetworkManager", "NetworkManagerController", "ClientSceneLoader",
            "UIManager", "CloudManager", "WindManager", "WorldSceneManager",
            "SkillsClientState", "StatsClientState", "EquipmentClientState",
            "CustomisationClientState", "CraftingClientState", "GatheringClientState",
            "RecipeKnowledgeClientState", "MetaRequirementClientState",
            "CombatClientState", "DockingClientState", "NpcShipClientState",
            "AdminFacade", "AdminRuntimeWindow", "ChartMarkManager", "ChartTrackRecorder",
            "DayNightConstellationController", "HouseDiscovery", "VengeanceMemory",
            "SkillVfxService", "ClientTargetLockService", "ClientTargetHighlightService",
            "ClientDamageNumberService", "CraftingProgressController",
            "GatheringToastController", "MetaRequirementToast", "KnowledgeToast",
            "ShipPositionServer", "PlayerPositionServer", "SceneDebugHUD",
            "HorizonVeilRenderer", "VeilRaymarchMeshController"
        };

        [MenuItem("ProjectC/World/Floating Origin/Bake Pilot Catalog (Preview + Apply)")]
        public static void Open()
        {
            GetWindow<BakePilotSceneCatalog>("Pilot Catalog Bake");
        }

        /// <summary>Headless самопроверка: read-only скан, сцены и ассеты не трогает.</summary>
        public static string DryRun()
        {
            try
            {
                var fresh = new List<NewItem>();
                var missing = new List<MissingItem>();
                var stale = new List<StaleItem>();
                var moved = new List<ReparentItem>();
                var ownershipFixes = new List<OwnershipFixItem>();
                var ddolFlagFixes = new List<DdolFlagFixItem>();
                var errors = new List<string>();
                ScanCatalog(fresh, missing, stale, moved, ownershipFixes, ddolFlagFixes, errors, out var digest);
                var sb = new System.Text.StringBuilder();
                sb.Append($"new={fresh.Count} missing={missing.Count} reparent={moved.Count} ddolOwn={ownershipFixes.Count} ddolFlag={ddolFlagFixes.Count} stale={stale.Count} errors={errors.Count} digest={digest}");
                foreach (var n in fresh) sb.Append($" | NEW:{n.name}[{System.IO.Path.GetFileName(n.scenePath)}]");
                foreach (var r in moved) sb.Append($" | REPARENT:{r.name}{((r.Blocked) ? "(blocked:" + r.blockedReason + ")" : "")}");
                foreach (var m in missing) sb.Append($" | MISSING:{m.sourceId}{(m.Blocked ? "(blocked)" : "")}");
                foreach (var o in ownershipFixes) sb.Append($" | DDOL_FIX:{o.name}({o.oldOwnership}->{o.newOwnership})");
                foreach (var d in ddolFlagFixes) sb.Append($" | DDOL_FLAG:{d.name}({d.componentName}.{d.propertyName}->false)");
                foreach (var s in stale) sb.Append($" | STALE:{s.name}[{s.kind}]");
                foreach (var e in errors) sb.Append($" | ERR:{e}");
                return sb.ToString();
            }
            catch (Exception ex) { return "EXCEPTION: " + ex.GetType().Name + ": " + ex.Message; }
        }

        /// <summary>
        /// 1-клик ребейк (также вызывается из меню/скриптов):
        /// догружает сцены каталога, сохраняет dirty-сцены, сканирует, применяет всё незаблокированное
        /// (новые, пропавшие, репаренты, DDOL ownership, хэши, digest) и делает контрольный скан.
        /// </summary>
        public static string RunOneClickRebake()
        {
            if (EditorApplication.isPlaying) return "Отказ: останови Play Mode перед ребейком.";
            var catalog = AssetDatabase.LoadAssetAtPath<GlobalMotionSceneCatalog>(CatalogPath);
            var profile = AssetDatabase.LoadAssetAtPath<GlobalMotionNetworkProfile>(ProfilePath);
            if (catalog == null || profile == null) return "Отказ: не найден каталог или профиль FO.";

            // 1. Подгрузить недостающие сцены каталога аддитивно и сохранить dirty-сцены.
            foreach (var source in catalog.Data.scenes)
            {
                if (source == null || string.IsNullOrEmpty(source.assetPath)) continue;
                var scene = SceneManager.GetSceneByPath(source.assetPath);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(source.assetPath, OpenSceneMode.Additive);
                    if (!scene.IsValid() || !scene.isLoaded)
                        return "Отказ: не удалось открыть сцену " + source.assetPath;
                }
                if (scene.isDirty)
                {
                    if (!EditorSceneManager.SaveScene(scene))
                        return "Отказ: не удалось сохранить сцену " + source.assetPath;
                }
            }

            // 2. Скан.
            var fresh = new List<NewItem>();
            var missing = new List<MissingItem>();
            var stale = new List<StaleItem>();
            var moved = new List<ReparentItem>();
            var ownershipFixes = new List<OwnershipFixItem>();
            var ddolFlagFixes = new List<DdolFlagFixItem>();
            var errors = new List<string>();
            ScanCatalog(fresh, missing, stale, moved, ownershipFixes, ddolFlagFixes, errors, out var digest);

            if (errors.Count > 0)
                return "Отказ: найдены ошибки перед ребейком: " + string.Join("; ", errors);

            bool digestMismatch = digest != "match";
            bool hasWork = fresh.Count > 0 || missing.Count > 0 || stale.Count > 0
                || moved.Exists(r => !r.Blocked) || ownershipFixes.Count > 0 || ddolFlagFixes.Count > 0 || digestMismatch;
            if (!hasWork)
                return "OK: каталог уже актуален, менять нечего. Можно запускать Play Mode.";

            // 3. Применить всё незаблокированное.
            var result = ApplyBatch(fresh, missing, moved, ownershipFixes, ddolFlagFixes, stale, digestMismatch);
            if (!result.StartsWith("OK", StringComparison.Ordinal))
                return result;

            // 4. Контрольный скан — убедиться, что хвостов не осталось.
            ScanCatalog(fresh, missing, stale, moved, ownershipFixes, ddolFlagFixes, errors, out var digestAfter);
            if (fresh.Count == 0 && missing.Count == 0 && stale.Count == 0 && ownershipFixes.Count == 0 && ddolFlagFixes.Count == 0
                && !moved.Exists(r => !r.Blocked) && errors.Count == 0 && digestAfter == "match")
            {
                return result + " | Проверка: всё чисто, можно запускать Play Mode.";
            }

            return result + $" | Осталось после ребейка: new={fresh.Count}, missing={missing.Count}, reparent={moved.Count}, ddolOwn={ownershipFixes.Count}, ddolFlag={ddolFlagFixes.Count}, stale={stale.Count}, errors={errors.Count}, digest={digestAfter}";
        }

        private sealed class NewItem
        {
            public bool selected = true;
            public GameObject go;
            public string name, scenePath, sceneGuid, sourceId, parentSid;
            public bool isRoot, isNO, hasRB;
            public GlobalSceneTreatment treatment = GlobalSceneTreatment.Unmanaged;
            public GlobalSceneOwnership ownership = GlobalSceneOwnership.SceneOwnedNetworkGameplay;
            public string reviewNote = "";
        }

        private sealed class StaleItem
        {
            public bool selected = true;
            public string name, scenePath, sceneGuid, sourceId, kind; // kind: layoutHash | dependencyHash
            public string oldVal, newVal;
        }

        /// <summary>Запись каталога, чей объект больше не существует в сохранённой сцене.</summary>
        private sealed class MissingItem
        {
            public bool selected = true;
            public string sourceId, scenePath, sceneGuid, parentSourceId, reviewNote;
            public string blockedReason;
            public bool Blocked => !string.IsNullOrEmpty(blockedReason);
        }

        /// <summary>
        /// Структурный дрейф: живая сцена и наблюдение расходятся по ближайшему предку-маркеру, корневости
        /// или сетевости. Для Unmanaged-записей это только бухгалтерия идентичности (исполнитель объект
        /// не трогает) — каталог догоняется автоматически. Для управляемых репарент меняет позу и
        /// активацию, поэтому запись блокируется до ручного разбора.
        /// </summary>
        private sealed class ReparentItem
        {
            public bool selected;
            public GameObject go;
            public string name, scenePath, sceneGuid, sourceId, reviewNote, newParentName;
            public string oldParent, newParent;
            public bool oldIsRoot, newIsRoot, oldIsNO, newIsNO;
            public string blockedReason;
            public bool Blocked => !string.IsNullOrEmpty(blockedReason);
        }

        /// <summary>
        /// Объект в сцене уводит себя в DontDestroyOnLoad, но в каталоге записан не как PersistentBootstrapService.
        /// Без этого пилот отклоняет старт с cataloged_source_in_ddol.
        /// </summary>
        private sealed class OwnershipFixItem
        {
            public bool selected = true;
            public string name, scenePath, sceneGuid, sourceId;
            public GlobalSceneOwnership oldOwnership, newOwnership;
            public string reason;
        }

        /// <summary>
        /// Объект в мировой сцене (не BootstrapScene) имеет включённый флаг dontDestroyOnLoad.
        /// В мировой сцене PersistentBootstrapService запрещён, поэтому флаг нужно выключить.
        /// </summary>
        private sealed class DdolFlagFixItem
        {
            public bool selected = true;
            public MonoBehaviour component;
            public string name, scenePath, componentName, propertyName;
        }

        private Vector2 _scroll;
        private readonly List<NewItem> _newItems = new List<NewItem>();
        private readonly List<MissingItem> _missingItems = new List<MissingItem>();
        private readonly List<ReparentItem> _reparentItems = new List<ReparentItem>();
        private readonly List<OwnershipFixItem> _ownershipFixes = new List<OwnershipFixItem>();
        private readonly List<DdolFlagFixItem> _ddolFlagFixes = new List<DdolFlagFixItem>();
        private readonly List<StaleItem> _staleItems = new List<StaleItem>();
        private readonly List<string> _errors = new List<string>();
        private string _status = "Готово к работе. Нажми «Ребейк (сделать всё)» после правок на сцене.";
        private MessageType _statusType = MessageType.Info;
        private bool _scanned;
        private bool _digestMismatch;
        private bool _showAdvanced;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Floating Origin — Ребейк каталога (T-FO09S)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Удалил или добавил объекты на сцене? Просто нажми «Ребейк (сделать всё)».\n" +
                "Тулза сама сохранит сцены, добавит новые объекты, удалит пропавшие, починит DDOL/репаренты и обновит хэши. Откат — Ctrl+Z.",
                MessageType.Info);

            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.35f, 0.85f, 0.45f);
            if (GUILayout.Button("Ребейк (сделать всё автоматически)", GUILayout.Height(38)))
            {
                try
                {
                    _status = RunOneClickRebake();
                    _statusType = _status.StartsWith("OK", StringComparison.Ordinal) ? MessageType.Info : MessageType.Error;
                    if (!_status.StartsWith("OK", StringComparison.Ordinal))
                    {
                        RunScanIntoUi();
                        _showAdvanced = true;
                    }
                    else
                    {
                        _newItems.Clear(); _missingItems.Clear(); _reparentItems.Clear(); _ownershipFixes.Clear(); _ddolFlagFixes.Clear(); _staleItems.Clear(); _errors.Clear();
                        _scanned = false;
                        _digestMismatch = false;
                    }
                }
                catch (Exception ex)
                {
                    _status = "EXCEPTION: " + ex.Message;
                    _statusType = MessageType.Error;
                }
            }
            GUI.backgroundColor = prevBg;

            EditorGUILayout.HelpBox(_status, _statusType);

            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Подробности / Ручной режим (Preview + Apply)", true);
            if (!_showAdvanced) return;

            if (GUILayout.Button("Сканировать (read-only)", GUILayout.Height(26)))
                RunScanIntoUi();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField($"Новое без записи ({_newItems.Count})", EditorStyles.boldLabel);
            foreach (var item in _newItems)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                item.selected = EditorGUILayout.Toggle(item.selected, GUILayout.Width(18));
                EditorGUILayout.LabelField($"{item.name}  [{System.IO.Path.GetFileName(item.scenePath)}]" +
                    (item.isRoot ? " root" : "") + (item.isNO ? " NO" : "") + (item.hasRB ? " RB" : ""), EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("sourceId: " + item.sourceId, EditorStyles.miniLabel);
                EditorGUILayout.LabelField("parent: " + (item.parentSid.Length == 0 ? "<root>" : item.parentSid), EditorStyles.miniLabel);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("treatment", GUILayout.Width(90));
                item.treatment = (GlobalSceneTreatment)EditorGUILayout.EnumPopup(item.treatment);
                EditorGUILayout.LabelField("ownership", GUILayout.Width(90));
                item.ownership = (GlobalSceneOwnership)EditorGUILayout.EnumPopup(item.ownership);
                EditorGUILayout.EndHorizontal();
                if (item.treatment != GlobalSceneTreatment.Unmanaged)
                    EditorGUILayout.HelpBox("Не-Unmanaged требует ручной доводки (inactive/фрейм/поза, см. 09S §3.2): тулза печёт только маркер+запись+хэши.", MessageType.Warning);
                EditorGUILayout.LabelField("reviewNote (обязательно, тикет!):");
                item.reviewNote = EditorGUILayout.TextField(item.reviewNote);
                if (string.IsNullOrWhiteSpace(item.reviewNote) && item.selected)
                    EditorGUILayout.HelpBox("Без reviewNote применение заблокировано: это аттестация человека.", MessageType.Error);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.LabelField($"Пропали из сцены — удалить записи ({_missingItems.Count})", EditorStyles.boldLabel);
            foreach (var miss in _missingItems)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                miss.selected = EditorGUILayout.Toggle(miss.selected, GUILayout.Width(18));
                EditorGUILayout.LabelField(miss.sourceId + "  [" + System.IO.Path.GetFileName(miss.scenePath) + "]", EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("parent: " + (string.IsNullOrEmpty(miss.parentSourceId) ? "<root>" : miss.parentSourceId), EditorStyles.miniLabel);
                if (!string.IsNullOrWhiteSpace(miss.reviewNote))
                    EditorGUILayout.LabelField("было: " + miss.reviewNote, EditorStyles.wordWrappedMiniLabel);
                if (miss.Blocked)
                    EditorGUILayout.HelpBox("Удаление заблокировано: " + miss.blockedReason, MessageType.Error);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.LabelField($"Структурный дрейф — репарент/корень/сетевость ({_reparentItems.Count})", EditorStyles.boldLabel);
            foreach (var rep in _reparentItems)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                rep.selected = EditorGUILayout.Toggle(rep.selected, GUILayout.Width(18));
                EditorGUILayout.LabelField($"{rep.name}  [{System.IO.Path.GetFileName(rep.scenePath)}]", EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("parent: " + ShowParent(rep.oldParent) + " → " + ShowParent(rep.newParent) +
                    (rep.newParent.Length == 0 ? "" : " (" + rep.newParentName + ")"), EditorStyles.miniLabel);
                if (rep.oldIsRoot != rep.newIsRoot) EditorGUILayout.LabelField($"isRoot: {rep.oldIsRoot} → {rep.newIsRoot}", EditorStyles.miniLabel);
                if (rep.oldIsNO != rep.newIsNO) EditorGUILayout.LabelField($"isNetworkObject: {rep.oldIsNO} → {rep.newIsNO}", EditorStyles.miniLabel);
                if (!string.IsNullOrWhiteSpace(rep.reviewNote))
                    EditorGUILayout.LabelField("reviewNote (не меняется): " + rep.reviewNote, EditorStyles.wordWrappedMiniLabel);
                if (rep.Blocked) EditorGUILayout.HelpBox("Автопочинка запрещена: " + rep.blockedReason, MessageType.Error);
                EditorGUILayout.EndVertical();
            }

            if (_ownershipFixes.Count > 0)
            {
                EditorGUILayout.LabelField($"DDOL ownership (исправить) ({_ownershipFixes.Count})", EditorStyles.boldLabel);
                foreach (var fix in _ownershipFixes)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    fix.selected = EditorGUILayout.Toggle(fix.selected, GUILayout.Width(18));
                    EditorGUILayout.LabelField($"{fix.name}: {fix.oldOwnership} → {fix.newOwnership} ({fix.reason})", EditorStyles.wordWrappedLabel);
                    EditorGUILayout.EndHorizontal();
                }
            }

            if (_ddolFlagFixes.Count > 0)
            {
                EditorGUILayout.LabelField($"DDOL-флаг в мировой сцене (выключить) ({_ddolFlagFixes.Count})", EditorStyles.boldLabel);
                foreach (var fix in _ddolFlagFixes)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    fix.selected = EditorGUILayout.Toggle(fix.selected, GUILayout.Width(18));
                    EditorGUILayout.LabelField($"{fix.name}: {fix.componentName}.{fix.propertyName} → false", EditorStyles.wordWrappedLabel);
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.LabelField($"Stale (обновить хэши) ({_staleItems.Count})", EditorStyles.boldLabel);
            foreach (var stale in _staleItems)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                stale.selected = EditorGUILayout.Toggle(stale.selected, GUILayout.Width(18));
                EditorGUILayout.LabelField($"{stale.name} [{stale.kind}] {Short(stale.oldVal)} → {Short(stale.newVal)}", EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndHorizontal();
            }

            if (_errors.Count > 0)
            {
                EditorGUILayout.LabelField($"Ошибки ({_errors.Count})", EditorStyles.boldLabel);
                foreach (var error in _errors) EditorGUILayout.HelpBox(error, MessageType.Error);
            }
            EditorGUILayout.EndScrollView();

            GUI.enabled = _scanned;
            if (GUILayout.Button("Применить выбранное", GUILayout.Height(32)))
            {
                try
                {
                    _status = ApplySelected();
                    _statusType = _status.StartsWith("OK", StringComparison.Ordinal) ? MessageType.Info : MessageType.Error;
                }
                catch (Exception ex)
                {
                    _status = "EXCEPTION: " + ex.Message;
                    _statusType = MessageType.Error;
                }
                if (_status.StartsWith("OK", StringComparison.Ordinal))
                {
                    _newItems.Clear(); _missingItems.Clear(); _staleItems.Clear(); _reparentItems.Clear(); _ownershipFixes.Clear(); _ddolFlagFixes.Clear();
                    _digestMismatch = false;
                }
                _scanned = false; // заставить пересканировать после мутации
            }
            GUI.enabled = true;
        }

        private void RunScanIntoUi()
        {
            _newItems.Clear(); _missingItems.Clear(); _staleItems.Clear(); _reparentItems.Clear(); _ownershipFixes.Clear(); _ddolFlagFixes.Clear(); _errors.Clear();
            try
            {
                ScanCatalog(_newItems, _missingItems, _staleItems, _reparentItems, _ownershipFixes, _ddolFlagFixes, _errors, out var digest);
                _digestMismatch = digest != "match";
                _status = $"Скан: новых={_newItems.Count}, пропавших={_missingItems.Count}, репарент={_reparentItems.Count}, DDOL={_ownershipFixes.Count + _ddolFlagFixes.Count}, stale={_staleItems.Count}, ошибок={_errors.Count}, digest={digest}";
                _statusType = _errors.Count > 0 ? MessageType.Error : MessageType.Info;
                _scanned = true;
            }
            catch (Exception ex)
            {
                _status = "EXCEPTION: " + ex.Message;
                _statusType = MessageType.Error;
            }
        }

        private static string Short(string hex) => hex.Length <= 12 ? hex : hex.Substring(0, 12);

        private static string ShowParent(string parentSourceId) => string.IsNullOrEmpty(parentSourceId) ? "<root>" : parentSourceId;

        /// <summary>
        /// Определяет, уводит ли корневой объект себя в DontDestroyOnLoad в рантайме.
        /// Проверяет bool-поля dontDestroyOnLoad/_dontDestroyOnLoad == true и известные DDOL-компоненты.
        /// </summary>
        private static bool IsLikelyDontDestroyOnLoadRoot(GameObject go, out string reason)
        {
            return InspectDdolRoot(go, out reason, out _, out _);
        }

        private static bool InspectDdolRoot(GameObject go, out string reason, out MonoBehaviour flagComponent, out string flagProperty)
        {
            reason = null;
            flagComponent = null;
            flagProperty = null;
            if (go == null || go.transform.parent != null) return false;
            var behaviours = go.GetComponents<MonoBehaviour>();
            foreach (var mb in behaviours)
            {
                if (mb == null) continue;
                var so = new SerializedObject(mb);
                var prop = so.FindProperty("dontDestroyOnLoad") ?? so.FindProperty("_dontDestroyOnLoad");
                if (prop != null && prop.propertyType == SerializedPropertyType.Boolean)
                {
                    if (prop.boolValue)
                    {
                        reason = mb.GetType().Name + "." + prop.name + "=true";
                        flagComponent = mb;
                        flagProperty = prop.name;
                        return true;
                    }
                    continue;
                }
                var typeName = mb.GetType().Name;
                if (KnownDdolComponentNames.Contains(typeName))
                {
                    reason = typeName + " (DDOL)";
                    return true;
                }
            }
            return false;
        }

        /// <summary>Имя живого объекта по sourceId — только для читаемого превью репарента.</summary>
        private static string NameBySourceId(Dictionary<GameObject, string> ids, string sourceId)
        {
            foreach (var pair in ids)
                if (pair.Value == sourceId) return pair.Key == null ? sourceId : pair.Key.name;
            return sourceId;
        }

        /// <summary>Не даёт сделать запись потомком самой себя через цепочку parentSourceId.</summary>
        private static bool IsSelfOrAncestor(string candidateParent, string sourceId, Dictionary<string, GlobalSceneEntry> entries)
        {
            var cursor = candidateParent;
            for (int guard = 0; guard < 4096 && !string.IsNullOrEmpty(cursor); guard++)
            {
                if (cursor == sourceId) return true;
                if (!entries.TryGetValue(cursor, out var entry)) return false;
                cursor = entry.parentSourceId;
            }
            return false;
        }

        private static void ScanCatalog(List<NewItem> fresh, List<MissingItem> missingItems, List<StaleItem> stale,
            List<ReparentItem> reparent, List<OwnershipFixItem> ownershipFixes, List<DdolFlagFixItem> ddolFlagFixes,
            List<string> errors, out string digest)
        {
            digest = "?";
            fresh.Clear(); missingItems.Clear(); stale.Clear(); reparent.Clear(); ownershipFixes.Clear(); ddolFlagFixes.Clear(); errors.Clear();
            var catalog = AssetDatabase.LoadAssetAtPath<GlobalMotionSceneCatalog>(CatalogPath);
            var profile = AssetDatabase.LoadAssetAtPath<GlobalMotionNetworkProfile>(ProfilePath);
            if (catalog == null) throw new InvalidOperationException("Каталог не найден: " + CatalogPath);
            if (profile == null) throw new InvalidOperationException("Профиль не найден: " + ProfilePath);
            if (!GlobalSceneCatalogCompiler.TryCompile(catalog.Data, out var plan, out _)
                || !GlobalSceneCatalogCompiler.TryMatchDigest(plan, profile.SceneLayoutDigest, out _))
                digest = "MISMATCH (починить до bake!)";
            else
                digest = "match";

            // Пары entry/observation живут в одном каталоге: репарент обязан править обе половины.
            var entryByIdScan = new Dictionary<string, GlobalSceneEntry>(StringComparer.Ordinal);
            foreach (var storedEntry in catalog.Data.entries) if (storedEntry != null) entryByIdScan[storedEntry.sourceId] = storedEntry;

            foreach (var source in catalog.Data.scenes)
            {
                if (source == null) continue;
                var scene = SceneManager.GetSceneByPath(source.assetPath);
                if (!scene.IsValid() || !scene.isLoaded) { errors.Add("Сцена не загружена, пропущена: " + source.assetPath); continue; }
                var candidates = new HashSet<GameObject>();
                foreach (var root in scene.GetRootGameObjects())
                {
                    candidates.Add(root);
                    foreach (var no in root.GetComponentsInChildren<NetworkObject>(true)) candidates.Add(no.gameObject);
                }
                var obsById = new Dictionary<string, GlobalSceneObservation>(StringComparer.Ordinal);
                if (source.observations != null)
                    foreach (var o in source.observations)
                        if (o != null) obsById[o.sourceId] = o;

                var ids = new Dictionary<GameObject, string>();
                foreach (var candidate in candidates)
                {
                    string sid = AuditGlobalSceneCatalog.SourceId(candidate);
                    if (!GlobalSceneCatalogCompiler.IsSourceId(sid, source.sceneGuid))
                    {
                        errors.Add("Transient identity (несохранённый объект): " + source.assetPath + " / " + candidate.name);
                        continue;
                    }
                    ids.Add(candidate, sid);
                }
                foreach (var pair in ids)
                {
                    var go = pair.Key;
                    string sid = pair.Value;
                    // Ближайший предок-кандидат (как в аудите).
                    var parent = go.transform.parent;
                    while (parent != null && !candidates.Contains(parent.gameObject)) parent = parent.parent;
                    string parentSid = "";
                    if (parent != null && !ids.TryGetValue(parent.gameObject, out parentSid))
                    {
                        errors.Add("Missing observed ancestor: " + sid);
                        continue;
                    }
                    bool isRoot = go.transform.parent == null;
                    bool isNO = go.GetComponent<NetworkObject>() != null;
                    bool isBootstrapScene = string.Equals(source.assetPath, BootstrapScenePath, StringComparison.Ordinal);
                    string ddolReason = null;
                    MonoBehaviour ddolFlagComp = null;
                    string ddolFlagProp = null;
                    bool isDdol = isRoot && InspectDdolRoot(go, out ddolReason, out ddolFlagComp, out ddolFlagProp);
                    if (isDdol && !isBootstrapScene)
                    {
                        if (ddolFlagComp != null)
                        {
                            ddolFlagFixes.Add(new DdolFlagFixItem
                            {
                                component = ddolFlagComp, name = go.name, scenePath = source.assetPath,
                                componentName = ddolFlagComp.GetType().Name, propertyName = ddolFlagProp,
                            });
                        }
                        else
                        {
                            errors.Add($"Объект '{go.name}' в {System.IO.Path.GetFileName(source.assetPath)} уходит в DontDestroyOnLoad ({ddolReason}) — перенеси в BootstrapScene или удали.");
                        }
                    }
                    if (!obsById.TryGetValue(sid, out var stored))
                    {
                        bool hasRB = go.GetComponentsInChildren<Rigidbody>(true).Length != 0;
                        fresh.Add(new NewItem
                        {
                            go = go, name = go.name, scenePath = source.assetPath, sceneGuid = source.sceneGuid,
                            sourceId = sid, parentSid = parentSid, isRoot = isRoot, isNO = isNO, hasRB = hasRB,
                            treatment = GlobalSceneTreatment.Unmanaged,
                            ownership = (isDdol && isBootstrapScene) ? GlobalSceneOwnership.PersistentBootstrapService
                                : !isNO ? GlobalSceneOwnership.AuthoredSceneContent
                                : hasRB ? GlobalSceneOwnership.ShipOrRigidbodyRoot
                                : GlobalSceneOwnership.SceneOwnedNetworkGameplay,
                            reviewNote = "Auto-bake (" + DateTime.Now.ToString("yyyy-MM-dd") + ")",
                        });
                        continue;
                    }
                    if (isDdol && isBootstrapScene && entryByIdScan.TryGetValue(sid, out var existingEntry)
                        && existingEntry.ownership != GlobalSceneOwnership.PersistentBootstrapService)
                    {
                        ownershipFixes.Add(new OwnershipFixItem
                        {
                            name = go.name, scenePath = source.assetPath, sceneGuid = source.sceneGuid,
                            sourceId = sid, oldOwnership = existingEntry.ownership,
                            newOwnership = GlobalSceneOwnership.PersistentBootstrapService,
                            reason = ddolReason,
                        });
                    }
                    else if (!isBootstrapScene && entryByIdScan.TryGetValue(sid, out var worldEntry)
                        && (worldEntry.ownership == GlobalSceneOwnership.PersistentBootstrapService
                            || worldEntry.ownership == GlobalSceneOwnership.BootstrapService))
                    {
                        ownershipFixes.Add(new OwnershipFixItem
                        {
                            name = go.name, scenePath = source.assetPath, sceneGuid = source.sceneGuid,
                            sourceId = sid, oldOwnership = worldEntry.ownership,
                            newOwnership = !isNO ? GlobalSceneOwnership.AuthoredSceneContent : GlobalSceneOwnership.SceneOwnedNetworkGameplay,
                            reason = "Bootstrap ownership запрещён в мировой сцене",
                        });
                    }
                    string liveHash = AuditGlobalSceneCatalog.LayoutHash(go, out bool missing);
                    if (missing) errors.Add("Missing script в сабтри: " + sid + " (" + go.name + ")");
                    // T-FO06L follow-up 2: структурный дрейф — запись на автопочинку, а не красная ошибка.
                    // Unmanaged: исполнитель объект не трогает, поэтому расхождение — только идентичность.
                    // Управляемые: репарент меняет позу/активацию, автопочинка запрещена.
                    if (stored.parentSourceId != parentSid || stored.isRoot != isRoot || stored.isNetworkObject != isNO)
                    {
                        var moved = new ReparentItem
                        {
                            go = go, name = go.name, scenePath = source.assetPath, sceneGuid = source.sceneGuid, sourceId = sid,
                            oldParent = stored.parentSourceId, newParent = parentSid,
                            newParentName = parentSid.Length == 0 ? "<root>" : NameBySourceId(ids, parentSid),
                            oldIsRoot = stored.isRoot, newIsRoot = isRoot, oldIsNO = stored.isNetworkObject, newIsNO = isNO,
                        };
                        GlobalSceneEntry selfEntry;
                        if (!entryByIdScan.TryGetValue(sid, out selfEntry)) moved.blockedReason = "нет записи каталога для " + sid;
                        else
                        {
                            moved.reviewNote = selfEntry.reviewNote;
                            if (selfEntry.treatment != GlobalSceneTreatment.Unmanaged)
                                moved.blockedReason = "управляемая запись (" + selfEntry.treatment + "): репарент меняет позу/активацию — разбор вручную";
                            else if (selfEntry.poseKind == GlobalScenePoseKind.ParentLocal && parentSid.Length == 0)
                                moved.blockedReason = "poseKind=ParentLocal без родителя — компилятор это отвергнет";
                        }
                        if (!moved.Blocked && parentSid.Length != 0)
                        {
                            GlobalSceneEntry parentEntry;
                            if (!entryByIdScan.TryGetValue(parentSid, out parentEntry))
                                moved.blockedReason = "у нового родителя " + parentSid + " нет записи в каталоге — сначала запеки родителя";
                            else if ((parentEntry.treatment == GlobalSceneTreatment.Exclude || parentEntry.treatment == GlobalSceneTreatment.ReplaceWithNetworkPrefab) &&
                                     (selfEntry == null || selfEntry.treatment != GlobalSceneTreatment.Exclude))
                                moved.blockedReason = "новый родитель " + parentEntry.treatment + " — компилятор это отвергнет";
                            else if (IsSelfOrAncestor(parentSid, sid, entryByIdScan))
                                moved.blockedReason = "новый родитель вкладывается в сам объект (цикл)";
                        }
                        moved.selected = !moved.Blocked;
                        reparent.Add(moved);
                    }
                    if (stored.layoutHash != liveHash)
                        stale.Add(new StaleItem
                        {
                            name = go.name, scenePath = source.assetPath, sceneGuid = source.sceneGuid,
                            sourceId = sid, kind = "layoutHash",
                            oldVal = stored.layoutHash, newVal = liveHash,
                        });
                }
                // Фантомные записи: объект исчез из сохранённой сцены. Собираем ровно тот же набор
                // маркеров, что видит нативный исполнитель (все маркеры в корнях сцены), иначе
                // живая запись на не-корневом объекте без NetworkObject была бы объявлена пропавшей.
                var liveMarkerIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var marker in root.GetComponentsInChildren<GlobalSceneSourceMarker>(true))
                        if (marker != null && !string.IsNullOrEmpty(marker.SourceId)) liveMarkerIds.Add(marker.SourceId);
                foreach (var entry in catalog.Data.entries)
                {
                    if (entry == null || entry.sceneGuid != source.sceneGuid || string.IsNullOrEmpty(entry.sourceId)) continue;
                    if (liveMarkerIds.Contains(entry.sourceId)) continue;
                    var item = new MissingItem
                    {
                        sourceId = entry.sourceId, sceneGuid = entry.sceneGuid, scenePath = source.assetPath,
                        parentSourceId = entry.parentSourceId, reviewNote = entry.reviewNote,
                    };
                    if (scene.isDirty) item.blockedReason = "сцена не сохранена (dirty) — сначала сохрани её";
                    missingItems.Add(item);
                }

                string depNow = AssetDatabase.GetAssetDependencyHash(source.assetPath).ToString();
                if (depNow != source.dependencyHash)
                    stale.Add(new StaleItem
                    {
                        name = System.IO.Path.GetFileName(source.assetPath), scenePath = source.assetPath,
                        sceneGuid = source.sceneGuid, sourceId = "", kind = "dependencyHash",
                        oldVal = source.dependencyHash, newVal = depNow,
                    });
            }
            // Удалять запись-родителя, на которую ссылается живая запись, нельзя: сначала разбирай ребёнка.
            foreach (var item in missingItems)
            {
                if (item.Blocked) continue;
                foreach (var entry in catalog.Data.entries)
                {
                    if (entry == null || entry.sourceId == item.sourceId) continue;
                    if (entry.parentSourceId != item.sourceId) continue;
                    item.blockedReason = "на эту запись ссылается живая запись " + entry.sourceId;
                    break;
                }
            }
        }

        private string ApplySelected()
        {
            var selNew = _newItems.FindAll(i => i.selected);
            var selStale = _staleItems.FindAll(i => i.selected);
            var selMissing = _missingItems.FindAll(i => i.selected);
            var selReparent = _reparentItems.FindAll(i => i.selected);
            var selOwnership = _ownershipFixes.FindAll(i => i.selected);
            var selDdolFlags = _ddolFlagFixes.FindAll(i => i.selected);
            return ApplyBatch(selNew, selMissing, selReparent, selOwnership, selDdolFlags, selStale, _digestMismatch);
        }

        private static string ApplyBatch(List<NewItem> selNew, List<MissingItem> selMissing,
            List<ReparentItem> selReparent, List<OwnershipFixItem> selOwnership,
            List<DdolFlagFixItem> selDdolFlags, List<StaleItem> selStale, bool forceDigestUpdate)
        {
            if (EditorApplication.isPlaying) return "Отказ: только Edit Mode.";
            var catalog = AssetDatabase.LoadAssetAtPath<GlobalMotionSceneCatalog>(CatalogPath);
            var profile = AssetDatabase.LoadAssetAtPath<GlobalMotionNetworkProfile>(ProfilePath);
            if (catalog == null || profile == null) return "Отказ: нет каталога/профиля.";

            if (selNew.Count == 0 && selStale.Count == 0 && selMissing.Count == 0
                && selReparent.Count == 0 && selOwnership.Count == 0 && selDdolFlags.Count == 0 && !forceDigestUpdate)
                return "Нечего применять: ничего не отмечено.";
            foreach (var item in selReparent)
                if (item.Blocked) return "Отказ: " + item.name + " — " + item.blockedReason;
            foreach (var item in selNew)
            {
                if (string.IsNullOrWhiteSpace(item.reviewNote)) return $"Отказ: пустой reviewNote у {item.name}.";
                if (item.reviewNote.Length > 2048) return $"Отказ: reviewNote > 2048 у {item.name}.";
            }

            var data = catalog.Data;
            var entriesById = new Dictionary<string, GlobalSceneEntry>(StringComparer.Ordinal);
            foreach (var e in data.entries) if (e != null) entriesById[e.sourceId] = e;

            // Родители — топологически: сначала те, чей parent уже в записях.
            selNew.Sort((a, b) => Depth(a.go.transform).CompareTo(Depth(b.go.transform)));
            var touchedScenes = new HashSet<string>(StringComparer.Ordinal);

            Undo.RecordObject(catalog, "Bake pilot catalog");
            Undo.RecordObject(profile, "Bake pilot digest");

            // 0. Удаление записей об исчезнувших объектах: до добавления, чтобы валидация
            // родителей видела уже очищенный каталог. Ничего не сохранено до успеха.
            if (selMissing.Count > 0)
            {
                var purgeError = PurgeMissing(data, selMissing, entriesById, touchedScenes);
                if (purgeError != null) return purgeError;
            }

            // 0b. Структурный дрейф: каталог догоняет живое дерево. Entry и observation правятся парой —
            // компилятор требует их равенства, а их расхождение и есть authored_parent_binding_mismatch.
            foreach (var item in selReparent)
            {
                var movedSource = FindSource(data, item.sceneGuid);
                if (movedSource == null) return "Отказ: нет блока сцены " + item.scenePath;
                GlobalSceneObservation observation = null;
                foreach (var candidate in movedSource.observations)
                    if (candidate != null && candidate.sourceId == item.sourceId) { observation = candidate; break; }
                if (observation == null) return "Отказ: observation пропал: " + item.name;
                if (!entriesById.TryGetValue(item.sourceId, out var movedEntry)) return "Отказ: запись пропала: " + item.name;
                if (movedEntry.poseKind == GlobalScenePoseKind.ParentLocal && item.newParent.Length == 0)
                    return "Отказ: " + item.name + " — poseKind=ParentLocal без родителя.";
                observation.parentSourceId = item.newParent;
                observation.isRoot = item.newIsRoot;
                observation.isNetworkObject = item.newIsNO;
                movedEntry.parentSourceId = item.newParent;
                touchedScenes.Add(item.scenePath);
            }

            // 0c. Исправление ownership для DDOL-корней (защита от cataloged_source_in_ddol).
            foreach (var fix in selOwnership)
            {
                if (!entriesById.TryGetValue(fix.sourceId, out var fixEntry))
                    return "Отказ: запись пропала: " + fix.name;
                fixEntry.ownership = fix.newOwnership;
                touchedScenes.Add(fix.scenePath);
            }

            // 0d. Выключение dontDestroyOnLoad у объектов в мировых сценах.
            foreach (var fix in selDdolFlags)
            {
                if (fix.component == null) continue;
                Undo.RecordObject(fix.component, "Disable DDOL in world scene");
                var so = new SerializedObject(fix.component);
                var prop = so.FindProperty(fix.propertyName);
                if (prop != null && prop.propertyType == SerializedPropertyType.Boolean)
                {
                    prop.boolValue = false;
                    so.ApplyModifiedProperties();
                    touchedScenes.Add(fix.scenePath);
                }
            }

            // 1. Маркеры.
            foreach (var item in selNew)
            {
                var marker = item.go.GetComponent<GlobalSceneSourceMarker>();
                if (marker == null) marker = Undo.AddComponent<GlobalSceneSourceMarker>(item.go);
                else Undo.RecordObject(marker, "Bake marker");
                var so = new SerializedObject(marker);
                so.FindProperty("_sourceId").stringValue = item.sourceId;
                so.FindProperty("_frameId").intValue = 0;
                so.FindProperty("_activateWhenReady").boolValue = false;
                so.ApplyModifiedProperties();
                touchedScenes.Add(item.scenePath);
            }
            // 2. Сейв сцен (хэши и depHash считаются по сохранённому).
            foreach (var path in touchedScenes)
            {
                var scene = SceneManager.GetSceneByPath(path);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) return "Отказ: не сохранилась сцена " + path;
            }
            // 3. Мутация каталога.
            foreach (var item in selNew)
            {
                // Родитель обязан иметь запись (старую или из этого же батча).
                if (item.parentSid.Length != 0 && !entriesById.ContainsKey(item.parentSid))
                    return $"Отказ: у {item.name} родитель {item.parentSid} без записи — отметь и его.";
                var wsrc = FindSource(data, item.sceneGuid);
                if (wsrc == null) return "Отказ: нет блока сцены " + item.scenePath;
                string liveHash = AuditGlobalSceneCatalog.LayoutHash(item.go, out bool missing);
                if (missing) return $"Отказ: missing script в сабтри {item.name}.";
                var obs = new GlobalSceneObservation
                {
                    sourceId = item.sourceId, parentSourceId = item.parentSid,
                    isRoot = item.isRoot, isNetworkObject = item.isNO, layoutHash = liveHash,
                };
                var olist = new List<GlobalSceneObservation>(wsrc.observations) { obs };
                wsrc.observations = olist.ToArray();
                var entry = new GlobalSceneEntry
                {
                    sceneGuid = item.sceneGuid, sourceId = item.sourceId, parentSourceId = item.parentSid,
                    reviewNote = item.reviewNote.Trim(),
                    treatment = item.treatment, ownership = item.ownership,
                    spatial = false, replacementPrefabHash = 0,
                    poseKind = GlobalScenePoseKind.None,
                    worldPosition = ProjectC.World.FloatingOrigin.GlobalPosition.Zero,
                    parentLocalPosition = Vector3.zero,
                    rotation = Quaternion.identity, scale = Vector3.one,
                };
                var elist = new List<GlobalSceneEntry>(data.entries) { entry };
                data.entries = elist.ToArray();
                entriesById[entry.sourceId] = entry;
                wsrc.dependencyHash = AssetDatabase.GetAssetDependencyHash(item.scenePath).ToString();
                touchedScenes.Add(item.scenePath);
            }
            foreach (var stale in selStale)
            {
                var wsrc = FindSource(data, stale.sceneGuid);
                if (wsrc == null) return "Отказ: нет блока сцены " + stale.scenePath;
                if (stale.kind == "dependencyHash")
                {
                    wsrc.dependencyHash = AssetDatabase.GetAssetDependencyHash(stale.scenePath).ToString();
                }
                else
                {
                    // Только layoutHash: parent/isRoot/isNO дрейф отбракован ещё на скане (errors),
                    // поэтому парные поля entry не трогаем — компилятор это гарантирует.
                    bool found = false;
                    foreach (var o in wsrc.observations)
                    {
                        if (o == null || o.sourceId != stale.sourceId) continue;
                        var scene = SceneManager.GetSceneByPath(stale.scenePath);
                        var go = FindBySourceId(scene, stale.sourceId);
                        if (go == null) return $"Отказ: объект пропал: {stale.name}.";
                        o.layoutHash = AuditGlobalSceneCatalog.LayoutHash(go, out bool missing);
                        if (missing) return $"Отказ: missing script в сабтри {stale.name}.";
                        found = true;
                    }
                    if (!found) return $"Отказ: observation пропал: {stale.name}.";
                }
                touchedScenes.Add(stale.scenePath);
            }
            // saved/inspectedComplete — только по тронутым и только если сцена не dirty.
            foreach (var path in touchedScenes)
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (scene.isDirty) return "Отказ: сцена грязная после сейва: " + path;
            }
            // Всегда освежить dependencyHash для всех затронутых сцен после сохранения.
            foreach (var source in data.scenes)
            {
                if (source == null || string.IsNullOrEmpty(source.assetPath)) continue;
                if (touchedScenes.Contains(source.assetPath))
                    source.dependencyHash = AssetDatabase.GetAssetDependencyHash(source.assetPath).ToString();
            }
            // 4. Digest (компилируем ДО сохранения ассетов, чтобы не записать битый каталог).
            if (!GlobalSceneCatalogCompiler.TryCompile(data, out var plan, out var cerr))
                return "Откат (Ctrl+Z): компиляция каталога: " + cerr;
            string hex = GlobalMotionNetworkContract.DigestHex(plan.CopyDigest());
            var pso = new SerializedObject(profile);
            pso.FindProperty("_sceneLayoutDigest").stringValue = hex;
            pso.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            // 5. Ре-верификация с диска.
            if (!GlobalSceneCatalogCompiler.TryCompile(catalog.Data, out var plan2, out _)
                || !GlobalSceneCatalogCompiler.TryMatchDigest(plan2, profile.SceneLayoutDigest, out _))
                return "Откат (Ctrl+Z): digest не сошёлся после применения.";
            return $"OK: +{selNew.Count} новых, -{selMissing.Count} пропавших, репарент={selReparent.Count}, DDOL={selOwnership.Count + selDdolFlags.Count}, stale={selStale.Count}, entries={plan2.SpawnOrder.Count}, digest={hex.Substring(0, 16)}...";
        }

        /// <summary>
        /// Удаляет записи и observation объектов, которых нет в сохранённой сцене, и освежает
        /// dependencyHash затронутых сцен. Возвращает текст отказа (тогда каталог восстановлен) или null.
        /// </summary>
        private static string PurgeMissing(GlobalSceneCatalogData data, List<MissingItem> items,
            Dictionary<string, GlobalSceneEntry> entriesById, HashSet<string> touchedScenes)
        {
            var removeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (item.Blocked) return "Отказ: " + item.blockedReason + " (" + item.sourceId + ")";
                var scene = SceneManager.GetSceneByPath(item.scenePath);
                if (!scene.IsValid() || !scene.isLoaded) return "Отказ: сцена не загружена: " + item.scenePath;
                if (scene.isDirty) return "Отказ: сцена не сохранена: " + item.scenePath;
                if (FindBySourceId(scene, item.sourceId) != null) return "Отказ: объект на месте, удаление отменено: " + item.sourceId;
                removeIds.Add(item.sourceId);
            }
            // Живая запись не должна потерять родителя.
            foreach (var entry in data.entries)
                if (entry != null && !removeIds.Contains(entry.sourceId) && removeIds.Contains(entry.parentSourceId))
                    return "Отказ: " + entry.sourceId + " ссылается на удаляемого родителя " + entry.parentSourceId;

            var savedEntries = data.entries;
            var savedObservations = new Dictionary<GlobalSceneSource, GlobalSceneObservation[]>();
            foreach (var source in data.scenes) if (source != null) savedObservations[source] = source.observations;

            var keptEntries = new List<GlobalSceneEntry>(data.entries.Length);
            foreach (var entry in data.entries)
            {
                if (entry == null) continue;
                if (removeIds.Contains(entry.sourceId)) continue;
                keptEntries.Add(entry);
            }
            data.entries = keptEntries.ToArray();

            foreach (var source in data.scenes)
            {
                if (source == null) continue;
                bool touched = false;
                var keptObs = new List<GlobalSceneObservation>(source.observations.Length);
                foreach (var observation in source.observations)
                {
                    if (observation == null) continue;
                    if (removeIds.Contains(observation.sourceId)) { touched = true; continue; }
                    keptObs.Add(observation);
                }
                if (!touched) continue;
                source.observations = keptObs.ToArray();
                source.dependencyHash = AssetDatabase.GetAssetDependencyHash(source.assetPath).ToString();
                touchedScenes.Add(source.assetPath);
            }

            if (!GlobalSceneCatalogCompiler.TryCompile(data, out _, out var error))
            {
                data.entries = savedEntries;
                foreach (var pair in savedObservations) pair.Key.observations = pair.Value;
                return "Отказ: каталог перестал компилироваться: " + error;
            }
            foreach (var id in removeIds) entriesById.Remove(id);
            return null;
        }

        private static int Depth(Transform t)
        {
            int d = 0;
            while (t.parent != null) { d++; t = t.parent; }
            return d;
        }

        private static GlobalSceneSource FindSource(GlobalSceneCatalogData data, string sceneGuid)
        {
            foreach (var s in data.scenes)
                if (s != null && s.sceneGuid == sceneGuid) return s;
            return null;
        }

        private static GameObject FindBySourceId(Scene scene, string sourceId)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (AuditGlobalSceneCatalog.SourceId(root) == sourceId) return root;
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                    if (AuditGlobalSceneCatalog.SourceId(tr.gameObject) == sourceId) return tr.gameObject;
            }
            return null;
        }
    }
}
