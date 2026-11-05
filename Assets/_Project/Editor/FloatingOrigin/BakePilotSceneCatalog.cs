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
    /// </summary>
    public sealed class BakePilotSceneCatalog : EditorWindow
    {
        private const string CatalogPath = "Assets/_Project/Prefabs/FloatingOrigin/GlobalMotionPilotSceneCatalog.asset";
        private const string ProfilePath = "Assets/_Project/Prefabs/FloatingOrigin/GlobalMotionPilotProfile.asset";

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
                var errors = new List<string>();
                ScanCatalog(fresh, missing, stale, errors, out var digest);
                var sb = new System.Text.StringBuilder();
                sb.Append($"new={fresh.Count} missing={missing.Count} stale={stale.Count} errors={errors.Count} digest={digest}");
                foreach (var n in fresh) sb.Append($" | NEW:{n.name}[{System.IO.Path.GetFileName(n.scenePath)}]");
                foreach (var m in missing) sb.Append($" | MISSING:{m.sourceId}{(m.Blocked ? "(blocked)" : "")}");
                foreach (var s in stale) sb.Append($" | STALE:{s.name}[{s.kind}]");
                foreach (var e in errors) sb.Append($" | ERR:{e}");
                return sb.ToString();
            }
            catch (Exception ex) { return "EXCEPTION: " + ex.GetType().Name + ": " + ex.Message; }
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

        private Vector2 _scroll;
        private readonly List<NewItem> _newItems = new List<NewItem>();
        private readonly List<MissingItem> _missingItems = new List<MissingItem>();
        private readonly List<StaleItem> _staleItems = new List<StaleItem>();
        private readonly List<string> _errors = new List<string>();
        private string _status = "Не сканировано. Нажми «Сканировать».";
        private bool _scanned;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Bake Pilot Scene Catalog (T-FO09S)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1) Сканируй → 2) отметь нужное, впиши reviewNote каждой новой записи → 3) проверь превью → 4) Применить. " +
                "Сцены из списка каталога должны быть загружены и сохранены. Откат — Ctrl+Z (Undo).",
                MessageType.Info);

            if (GUILayout.Button("Сканировать (read-only)", GUILayout.Height(28)))
            {
                _newItems.Clear(); _missingItems.Clear(); _staleItems.Clear(); _errors.Clear();
                try
                {
                    ScanCatalog(_newItems, _missingItems, _staleItems, _errors, out var digest);
                    _status = $"Скан: новых={_newItems.Count}, пропавших={_missingItems.Count}, stale={_staleItems.Count}, ошибок={_errors.Count}, digest={digest}";
                    _scanned = true;
                }
                catch (Exception ex) { _status = "EXCEPTION: " + ex.Message; }
            }

            EditorGUILayout.LabelField("Статус: " + _status, EditorStyles.wordWrappedLabel);
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
                try { _status = ApplySelected(); }
                catch (Exception ex) { _status = "EXCEPTION: " + ex.Message; }
                if (_status.StartsWith("OK", StringComparison.Ordinal)) { _newItems.Clear(); _missingItems.Clear(); _staleItems.Clear(); }
                _scanned = false; // заставить пересканировать после мутации
            }
            GUI.enabled = true;
        }

        private static string Short(string hex) => hex.Length <= 12 ? hex : hex.Substring(0, 12);

        private static void ScanCatalog(List<NewItem> fresh, List<MissingItem> missingItems, List<StaleItem> stale, List<string> errors, out string digest)
        {
            digest = "?";
            fresh.Clear(); missingItems.Clear(); stale.Clear(); errors.Clear();
            var catalog = AssetDatabase.LoadAssetAtPath<GlobalMotionSceneCatalog>(CatalogPath);
            var profile = AssetDatabase.LoadAssetAtPath<GlobalMotionNetworkProfile>(ProfilePath);
            if (catalog == null) throw new InvalidOperationException("Каталог не найден: " + CatalogPath);
            if (profile == null) throw new InvalidOperationException("Профиль не найден: " + ProfilePath);
            if (!GlobalSceneCatalogCompiler.TryCompile(catalog.Data, out var plan, out _)
                || !GlobalSceneCatalogCompiler.TryMatchDigest(plan, profile.SceneLayoutDigest, out _))
                digest = "MISMATCH (починить до bake!)";
            else
                digest = "match";

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
                    if (!obsById.TryGetValue(sid, out var stored))
                    {
                        bool hasRB = go.GetComponentsInChildren<Rigidbody>(true).Length != 0;
                        fresh.Add(new NewItem
                        {
                            go = go, name = go.name, scenePath = source.assetPath, sceneGuid = source.sceneGuid,
                            sourceId = sid, parentSid = parentSid, isRoot = isRoot, isNO = isNO, hasRB = hasRB,
                            treatment = GlobalSceneTreatment.Unmanaged,
                            ownership = !isNO ? GlobalSceneOwnership.AuthoredSceneContent
                                : hasRB ? GlobalSceneOwnership.ShipOrRigidbodyRoot
                                : GlobalSceneOwnership.SceneOwnedNetworkGameplay,
                        });
                        continue;
                    }
                    string liveHash = AuditGlobalSceneCatalog.LayoutHash(go, out bool missing);
                    if (missing) errors.Add("Missing script в сабтри: " + sid + " (" + go.name + ")");
                    // Структурный дрейф (родитель/корень/сетевость) — не чиним автоматически:
                    // у entry есть парные поля, тут нужен разбор вручную.
                    if (stored.parentSourceId != parentSid || stored.isRoot != isRoot || stored.isNetworkObject != isNO)
                    {
                        errors.Add($"Structural drift, нужен ручной разбор: {go.name} ({sid}): " +
                            $"parent [{stored.parentSourceId}]→[{parentSid}], root {stored.isRoot}→{isRoot}, no {stored.isNetworkObject}→{isNO}");
                        continue;
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
            if (EditorApplication.isPlaying) return "Отказ: только Edit Mode.";
            var catalog = AssetDatabase.LoadAssetAtPath<GlobalMotionSceneCatalog>(CatalogPath);
            var profile = AssetDatabase.LoadAssetAtPath<GlobalMotionNetworkProfile>(ProfilePath);
            if (catalog == null || profile == null) return "Отказ: нет каталога/профиля.";

            var selNew = _newItems.FindAll(i => i.selected);
            var selStale = _staleItems.FindAll(i => i.selected);
            var selMissing = _missingItems.FindAll(i => i.selected);
            if (selNew.Count == 0 && selStale.Count == 0 && selMissing.Count == 0) return "Нечего применять: ничего не отмечено.";
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
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            // 4. Digest.
            if (!GlobalSceneCatalogCompiler.TryCompile(data, out var plan, out var cerr))
                return "Откат (Ctrl+Z): компиляция каталога: " + cerr;
            string hex = GlobalMotionNetworkContract.DigestHex(plan.CopyDigest());
            var pso = new SerializedObject(profile);
            pso.FindProperty("_sceneLayoutDigest").stringValue = hex;
            pso.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            // 5. Ре-верификация с диска.
            if (!GlobalSceneCatalogCompiler.TryCompile(catalog.Data, out var plan2, out _)
                || !GlobalSceneCatalogCompiler.TryMatchDigest(plan2, profile.SceneLayoutDigest, out _))
                return "Откат (Ctrl+Z): digest не сошёлся после применения.";
            return $"OK: +{selNew.Count} записей, stale закрыто: {selStale.Count}, entries={plan2.SpawnOrder.Count}, digest={hex.Substring(0, 16)}...";
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
