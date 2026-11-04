#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace ProjectC.EditorTools.Ships
{
    /// <summary>
    /// Перенос эталонного визуала класса (Light / Medium / Heavy / HeavyII) в NPC-префабы-заглушки.
    ///
    /// Корень заглушки и все его компоненты остаются нетронутыми: ключ корабля (`_keyItemData`),
    /// расписание NPC, манифест экипажа и NavMesh палубы сохраняются у каждого корабля.
    /// Из эталона переносится дочерняя иерархия (визуал, коллайдеры, двигатели, кресло, слоты, двери)
    /// и недостающие корневые компоненты (mesh/renderer/collider корня, ShipWakeCloudCutter,
    /// ShipShadowBudget, GlobalSceneSourceMarker).
    ///
    /// Узлы, которых в эталоне нет (Exchanger, CrewAnchors, DeckNavSurface), берутся из заглушки,
    /// их локальный трансформ пересчитывается под новый масштаб корня так, чтобы мировая позиция
    /// и размер остались прежними.
    ///
    /// Меню: ProjectC -> Ships -> NPC Graft.
    /// </summary>
    public static class NpcShipVisualGraft
    {
        private const string ShipsDir = "Assets/_Project/Prefabs/Ships";
        private const string BackupDir = "_local_backups/ships_prefab_backup_2026-11-04/Ships";
        private const string ReportPath = "docs/dev/NPC_VISUAL_GRAFT_REPORT.md";

        private static readonly Dictionary<string, string> TemplateByClass = new Dictionary<string, string>
        {
            { "Light",   "Assets/_Project/Prefabs/Ships/Ship_Light_root_fbx_test.prefab" },
            { "Medium",  "Assets/_Project/Prefabs/Ships/Ship_medium_base.prefab" },
            { "Heavy",   "Assets/_Project/Prefabs/Ships/Альбатрос.prefab" },
            { "HeavyII", "Assets/_Project/Prefabs/Ships/Ship_havyII_base.prefab" }
        };

        private sealed class GraftResult
        {
            public string Name;
            public string ClassName;
            public string Template;
            public bool Applied;
            public int RemovedChildren;
            public int MovedChildren;
            public readonly List<string> KeptChildren = new List<string>();
            public readonly List<string> AddedRootComponents = new List<string>();
            public int RemappedRefs;
            public int MappedSlots;
            public string KeyItem;
            public int MeshRenderersBefore;
            public int MeshRenderersAfter;
            public int CollidersAfter;
            public int DoorsAfter;
            public int MissingScripts;
            public Vector3 ScaleBefore;
            public Vector3 ScaleAfter;
            public readonly List<string> Problems = new List<string>();
        }

        // ------------------------------------------------------------------ меню

        [MenuItem("ProjectC/Ships/NPC Graft/1. Dry Run (план, без записи)", priority = 100)]
        public static void DryRun()
        {
            RunAll(false);
        }

        [MenuItem("ProjectC/Ships/NPC Graft/2. Apply", priority = 101)]
        public static void Apply()
        {
            RunAll(true);
        }

        [MenuItem("ProjectC/Ships/NPC Graft/3. Verify", priority = 102)]
        public static void Verify()
        {
            var report = new StringBuilder();
            report.AppendLine("# NPC Graft — проверка");
            report.AppendLine();
            report.AppendLine("| Корабль | Класс | Platform | MR | Colliders | Doors | CrewAnchors | Exchanger | Missing script |");
            report.AppendLine("|---|---|---|---|---|---|---|---|---|");

            int bad = 0;
            foreach (var stubPath in FindStubPaths())
            {
                var root = PrefabUtility.LoadPrefabContents(stubPath);
                try
                {
                    var cls = ReadClassName(root);
                    var platform = root.transform.Find("Platform") != null ? "ЕСТЬ" : "нет";
                    var crew = GetComponentByName(root, "ShipCrewSpawner");
                    var anchors = root.transform.Find("CrewAnchors") != null ? "есть" : "НЕТ";
                    var exchanger = root.transform.Find("Exchanger") != null ? "есть" : "нет";
                    var stats = CollectStats(root);

                    if (root.transform.Find("Platform") != null) bad++;
                    if (anchors == "НЕТ") bad++;
                    if (stats.Missing > 0) bad++;

                    report.AppendLine(string.Format("| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} | {8} |",
                        Path.GetFileNameWithoutExtension(stubPath), cls, platform,
                        stats.MeshRenderers, stats.Colliders, stats.Doors, anchors, exchanger, stats.Missing));
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            report.AppendLine();
            report.AppendLine(bad == 0 ? "Проблем не найдено." : "Проблемных пунктов: " + bad + ".");
            Debug.Log("[NPC Graft] Verify\n" + report);
            WriteReport(AppendReport("verify", report.ToString()));
        }

        [MenuItem("ProjectC/Ships/NPC Graft/9. Rollback (из бэкапа)", priority = 200)]
        public static void Rollback()
        {
            if (!Directory.Exists(BackupDir))
            {
                Debug.LogError("[NPC Graft] Нет бэкапа: " + BackupDir);
                return;
            }

            if (!EditorUtility.DisplayDialog("NPC Graft — Rollback",
                    "Вернуть все 17 NPC-префабов из бэкапа " + BackupDir + "?", "Вернуть", "Отмена"))
            {
                return;
            }

            int restored = 0;
            foreach (var stubPath in FindStubPaths())
            {
                var name = Path.GetFileName(stubPath);
                var backup = Path.Combine(BackupDir, name);
                if (!File.Exists(backup))
                {
                    Debug.LogWarning("[NPC Graft] Нет в бэкапе: " + name);
                    continue;
                }
                File.Copy(backup, stubPath, true);
                AssetDatabase.ImportAsset(stubPath, ImportAssetOptions.ForceUpdate);
                restored++;
            }
            Debug.Log("[NPC Graft] Восстановлено префабов: " + restored);
        }

        // ------------------------------------------------------------------ основной проход

        private static void RunAll(bool apply)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[NPC Graft] Play Mode активен. Остановите игру и повторите.");
                return;
            }

            var report = new StringBuilder();
            report.AppendLine("# NPC Graft — " + (apply ? "применение" : "dry-run"));
            report.AppendLine();
            report.AppendLine("Источник эталонов по классу:");
            foreach (var kv in TemplateByClass)
            {
                report.AppendLine("- **" + kv.Key + "** → `" + kv.Value + "`");
            }
            report.AppendLine();
            report.AppendLine("| Корабль | Класс | Эталон | Удалено узлов | Перенесено | Сохранено из заглушки | +компоненты корня | Ref remap | MR до → после | Colliders | Doors |");
            report.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");

            int processed = 0;
            foreach (var stubPath in FindStubPaths())
            {
                GraftResult result;
                try
                {
                    result = GraftOne(stubPath, apply);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[NPC Graft] " + stubPath + ": " + ex);
                    report.AppendLine("| " + Path.GetFileNameWithoutExtension(stubPath) + " | — | — | **ОШИБКА** | | | | | | | |");
                    continue;
                }

                processed++;
                report.AppendLine(string.Format(
                    "| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} | {8} → {9} | {10} | {11} |",
                    result.Name, result.ClassName, Path.GetFileNameWithoutExtension(result.Template),
                    result.RemovedChildren, result.MovedChildren,
                    result.KeptChildren.Count == 0 ? "—" : string.Join(", ", result.KeptChildren),
                    result.AddedRootComponents.Count == 0 ? "—" : string.Join(", ", result.AddedRootComponents),
                    result.RemappedRefs, result.MeshRenderersBefore, result.MeshRenderersAfter,
                    result.CollidersAfter, result.DoorsAfter));

                foreach (var p in result.Problems)
                {
                    report.AppendLine("| ⚠ " + result.Name + " | | | " + p + " | | | | | | | |");
                }
            }

            report.AppendLine();
            report.AppendLine("Обработано префабов: **" + processed + "**" + (apply ? " (записано)." : " (запись не выполнялась)."));

            Debug.Log("[NPC Graft]\n" + report);
            WriteReport(AppendReport(apply ? "apply" : "dry-run", report.ToString()));

            if (apply)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[NPC Graft] Apply завершён. Дальше: 3. Verify и проверка в Play Mode.");
            }
        }

        private static GraftResult GraftOne(string stubPath, bool apply)
        {
            var result = new GraftResult { Name = Path.GetFileNameWithoutExtension(stubPath), Applied = apply };
            var stubRoot = PrefabUtility.LoadPrefabContents(stubPath);
            try
            {
                result.KeyItem = ReadKeyItem(stubRoot);
                result.ClassName = ReadClassName(stubRoot);
                result.ScaleBefore = stubRoot.transform.localScale;

                string templatePath;
                if (!TemplateByClass.TryGetValue(result.ClassName, out templatePath))
                {
                    result.Problems.Add("нет эталона для класса " + result.ClassName);
                    return result;
                }
                result.Template = templatePath;

                var statsBefore = CollectStats(stubRoot);
                result.MeshRenderersBefore = statsBefore.MeshRenderers;

                var templateRoot = PrefabUtility.LoadPrefabContents(templatePath);
                try
                {
                    Graft(stubRoot, templateRoot, result);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(templateRoot);
                }

                var statsAfter = CollectStats(stubRoot);
                result.MeshRenderersAfter = statsAfter.MeshRenderers;
                result.CollidersAfter = statsAfter.Colliders;
                result.DoorsAfter = statsAfter.Doors;
                result.MissingScripts = statsAfter.Missing;
                result.ScaleAfter = stubRoot.transform.localScale;

                // --- проверки результата
                if (stubRoot.transform.Find("Platform") != null)
                    result.Problems.Add("остался узел Platform");
                if (statsAfter.Doors == 0)
                    result.Problems.Add("нет ни одного DoorController");
                if (statsAfter.Missing > 0)
                    result.Problems.Add("missing script: " + statsAfter.Missing);
                if (result.KeyItem == null || result.KeyItem != ReadKeyItem(stubRoot))
                    result.Problems.Add("изменился ключ корабля");
                if (stubRoot.transform.Find("CrewAnchors") == null)
                    result.Problems.Add("нет CrewAnchors (экипаж не заспавнится)");

                if (apply && result.Problems.Count == 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(stubRoot, stubPath);
                }
                else if (apply)
                {
                    Debug.LogWarning("[NPC Graft] " + result.Name + " не сохранён из-за проблем: " +
                                     string.Join("; ", result.Problems));
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(stubRoot);
            }
            return result;
        }

        /// <summary>Собственно перенос: корень и его компоненты не заменяются, дети — из эталона.</summary>
        private static void Graft(GameObject stubRoot, GameObject templateRoot, GraftResult result)
        {
            var stubChildren = new List<Transform>();
            foreach (Transform t in stubRoot.transform) stubChildren.Add(t);

            // 1. Что из заглушки сохраняем (то, чего в эталоне нет).
            var kept = new List<Transform>();
            var keptPos = new Dictionary<Transform, Vector3>();
            var keptScale = new Dictionary<Transform, Vector3>();
            foreach (var t in stubChildren)
            {
                if (!ShouldKeepFromStub(t.name, templateRoot)) continue;
                kept.Add(t);
                keptPos[t] = t.localPosition;
                keptScale[t] = t.localScale;
            }

            // 2. Удаляем дубли заглушки (Platform и всё, что придёт из эталона).
            foreach (var t in stubChildren)
            {
                if (kept.Contains(t)) continue;
                UnityEngine.Object.DestroyImmediate(t.gameObject);
                result.RemovedChildren++;
            }

            // 3. Клон эталона во временном контейнере внутри preview-сцены заглушки:
            //    так рабочая сцена не затрагивается, а внутренние ссылки ремапятся Unity.
            var temp = new GameObject("__NPC_GRAFT_TEMP__");
            temp.transform.SetParent(stubRoot.transform, false);
            var clone = UnityEngine.Object.Instantiate(templateRoot, temp.transform, false) as GameObject;
            var cloneKids = new List<Transform>();
            foreach (Transform t in clone.transform) cloneKids.Add(t);
            foreach (var t in cloneKids)
            {
                t.SetParent(stubRoot.transform, false); // worldPositionStays=false → сохраняем локальные значения эталона
                result.MovedChildren++;
            }

            // 4. Недостающие корневые компоненты эталона (mesh/renderer/collider корня, ShipWakeCloudCutter,
            //    ShipShadowBudget, GlobalSceneSourceMarker). Совпадающие по типу не трогаем.
            result.AddedRootComponents.AddRange(CopyMissingRootComponents(templateRoot, stubRoot));

            // 5. Ссылки на корень эталона внутри перенесённого поддерева → корень заглушки.
            result.RemappedRefs = RemapRootReferences(stubRoot, clone, stubRoot);

            UnityEngine.Object.DestroyImmediate(temp);

            // 6. Масштаб и позиция корня — как у эталона (позиция нулевая: у эталонов в ней мусор от Floating Origin).
            var oldScale = stubRoot.transform.localScale;
            var newScale = templateRoot.transform.localScale;
            stubRoot.transform.localPosition = Vector3.zero;
            stubRoot.transform.localRotation = Quaternion.identity;
            stubRoot.transform.localScale = newScale;

            // 7. Сохранённые узлы заглушки пересчитываем так, чтобы их мир не изменился.
            foreach (var t in kept)
            {
                if (t == null) continue;
                var p = keptPos[t];
                var s = keptScale[t];
                t.localPosition = new Vector3(
                    p.x * SafeRatio(oldScale.x, newScale.x),
                    p.y * SafeRatio(oldScale.y, newScale.y),
                    p.z * SafeRatio(oldScale.z, newScale.z));
                t.localScale = new Vector3(
                    s.x * SafeRatio(oldScale.x, newScale.x),
                    s.y * SafeRatio(oldScale.y, newScale.y),
                    s.z * SafeRatio(oldScale.z, newScale.z));
                result.KeptChildren.Add(t.name);
            }

            // 8. Служебные ссылки корня.
            WireCrewAnchors(stubRoot);
            result.MappedSlots = CopyModuleSlots(templateRoot, stubRoot);
        }

        // ------------------------------------------------------------------ правила переноса

        /// <summary>
        /// Заглушки: Platform — визуал-заменитель, удаляем. Узлы, которых в эталоне нет и которые
        /// нужны функционально (обменник, точки экипажа, NavMesh-маркер), остаются из заглушки.
        /// Дверь сохраняется из заглушки только если в эталоне нет ни одного DoorController.
        /// Всё остальное (кресло, слоты, двигатели, контейнеры визуала, CargoVisual) берётся из эталона.
        /// </summary>
        private static bool ShouldKeepFromStub(string name, GameObject templateRoot)
        {
            switch (name)
            {
                case "Platform":
                    return false;
                case "Exchanger":
                    return templateRoot.transform.Find("Exchanger") == null;
                case "CrewAnchors":
                    return templateRoot.transform.Find("CrewAnchors") == null;
                case "DeckNavSurface":
                    return templateRoot.transform.Find("DeckNavSurface") == null;
                case "Door":
                    return !HasComponentNamed(templateRoot, "DoorController");
                default:
                    return false;
            }
        }

        private static float SafeRatio(float stubValue, float templateValue)
        {
            if (Mathf.Abs(templateValue) < 0.0001f) return 1f;
            return stubValue / templateValue;
        }

        // ------------------------------------------------------------------ компоненты корня

        private static List<string> CopyMissingRootComponents(GameObject source, GameObject target)
        {
            var added = new List<string>();
            foreach (var c in source.GetComponents<Component>())
            {
                if (c == null || c is Transform) continue;
                var type = c.GetType();
                if (target.GetComponent(type) != null) continue;
                ComponentUtility.CopyComponent(c);
                if (ComponentUtility.PasteComponentAsNew(target))
                {
                    added.Add(type.Name);
                }
            }
            return added;
        }

        // ------------------------------------------------------------------ ссылки

        /// <summary>Все ссылки, указывающие на корень эталона (или его компоненты), перенаправляем на корень заглушки.</summary>
        private static int RemapRootReferences(GameObject root, GameObject oldRoot, GameObject newRoot)
        {
            int remapped = 0;
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                var so = new SerializedObject(component);
                var it = so.GetIterator();
                bool changed = false;
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var value = it.objectReferenceValue;
                    if (value == null) continue;

                    var owner = value as GameObject ?? (value as Component)?.gameObject;
                    if (owner == null || owner != oldRoot) continue;

                    if (value is GameObject)
                    {
                        it.objectReferenceValue = newRoot;
                        changed = true;
                        remapped++;
                    }
                    else if (value is Component valueComponent)
                    {
                        var target = newRoot.GetComponent(valueComponent.GetType());
                        if (target != null)
                        {
                            it.objectReferenceValue = target;
                            changed = true;
                            remapped++;
                        }
                    }
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }
            return remapped;
        }

        private static void WireCrewAnchors(GameObject root)
        {
            var crewSpawner = GetComponentByName(root, "ShipCrewSpawner");
            if (crewSpawner == null) return;
            var anchors = root.transform.Find("CrewAnchors");
            if (anchors == null) return;

            var so = new SerializedObject(crewSpawner);
            var prop = so.FindProperty("crewAnchors");
            if (prop == null) return;
            prop.objectReferenceValue = anchors;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// У заглушек массив слотов модулей пуст, у эталонов Light/Medium он заполнен.
        /// Переносим значения по относительному пути объекта.
        /// </summary>
        private static int CopyModuleSlots(GameObject templateRoot, GameObject stubRoot)
        {
            var source = GetComponentByName(templateRoot, "ShipModuleManager");
            var target = GetComponentByName(stubRoot, "ShipModuleManager");
            if (source == null || target == null) return 0;

            var sourceSo = new SerializedObject(source);
            var sourceProp = sourceSo.FindProperty("slots");
            if (sourceProp == null || sourceProp.arraySize == 0) return 0;

            var targetSo = new SerializedObject(target);
            var targetProp = targetSo.FindProperty("slots");
            if (targetProp == null) return 0;

            targetProp.arraySize = sourceProp.arraySize;
            int mapped = 0;
            for (int i = 0; i < sourceProp.arraySize; i++)
            {
                var element = targetProp.GetArrayElementAtIndex(i);
                var sourceValue = sourceProp.GetArrayElementAtIndex(i).objectReferenceValue as Component;
                if (sourceValue == null)
                {
                    element.objectReferenceValue = null;
                    continue;
                }

                var path = RelativePath(sourceValue.transform, templateRoot.transform);
                var found = string.IsNullOrEmpty(path) ? stubRoot.transform : stubRoot.transform.Find(path);
                if (found == null)
                {
                    element.objectReferenceValue = null;
                    continue;
                }

                var replacement = found.GetComponent(sourceValue.GetType());
                element.objectReferenceValue = replacement;
                if (replacement != null) mapped++;
            }
            targetSo.ApplyModifiedPropertiesWithoutUndo();
            return mapped;
        }

        // ------------------------------------------------------------------ утилиты

        private struct Stats
        {
            public int MeshRenderers;
            public int Colliders;
            public int Doors;
            public int Missing;
        }

        private static Stats CollectStats(GameObject root)
        {
            var stats = new Stats();
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null) { stats.Missing++; continue; }
                var n = c.GetType().Name;
                if (n == "MeshRenderer" || n == "SkinnedMeshRenderer") stats.MeshRenderers++;
                else if (n == "DoorController") stats.Doors++;
            }
            stats.Colliders = root.GetComponentsInChildren<Collider>(true).Length;
            return stats;
        }

        private static bool HasComponentNamed(GameObject root, string typeName)
        {
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (c != null && c.GetType().Name == typeName) return true;
            }
            return false;
        }

        private static Component GetComponentByName(GameObject root, string typeName)
        {
            foreach (var c in root.GetComponents<Component>())
            {
                if (c != null && c.GetType().Name == typeName) return c;
            }
            return null;
        }

        private static string ReadClassName(GameObject root)
        {
            var controller = GetComponentByName(root, "ShipController");
            if (controller == null) return null;
            var so = new SerializedObject(controller);
            var prop = so.FindProperty("shipFlightClass");
            if (prop == null) return null;
            if (prop.propertyType == SerializedPropertyType.Enum &&
                prop.enumNames != null && prop.enumValueIndex >= 0 && prop.enumValueIndex < prop.enumNames.Length)
            {
                return prop.enumNames[prop.enumValueIndex];
            }
            return prop.intValue.ToString();
        }

        private static string ReadKeyItem(GameObject root)
        {
            var controller = GetComponentByName(root, "ShipController");
            if (controller == null) return null;
            var so = new SerializedObject(controller);
            var prop = so.FindProperty("_keyItemData");
            return prop?.objectReferenceValue == null ? null : prop.objectReferenceValue.name;
        }

        private static string RelativePath(Transform target, Transform root)
        {
            if (target == root) return string.Empty;
            var parts = new List<string>();
            var current = target;
            while (current != null && current != root)
            {
                parts.Insert(0, current.name);
                current = current.parent;
            }
            return string.Join("/", parts);
        }

        /// <summary>NPC-заглушки, которые получают визуал эталона (Гигант не входит по решению пользователя).</summary>
        private static readonly string[] StubNames =
        {
            "Берег", "Вавилон", "Ветроворот", "Горгона", "Жук", "Летучий", "Мастодонт", "Олимп",
            "Пещера", "Река", "Сильфида", "Скат", "Странник", "Торренс", "Угольщик", "Цитадель", "Шмель"
        };

        /// <summary>Заглушки — явный реестр из 17 кораблей; эталоны, Гигант и готовые корабли исключены.</summary>
        private static List<string> FindStubPaths()
        {
            var result = new List<string>();
            foreach (var name in StubNames)
            {
                var path = ShipsDir + "/" + name + ".prefab";
                if (!File.Exists(path))
                {
                    Debug.LogWarning("[NPC Graft] Префаб не найден: " + path);
                    continue;
                }
                result.Add(path);
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static string AppendReport(string title, string body)
        {
            if (!File.Exists(ReportPath)) return body;

            var existing = File.ReadAllText(ReportPath);
            var marker = "<!-- " + title + " -->";
            var index = existing.IndexOf(marker, StringComparison.Ordinal);
            if (index >= 0) existing = existing.Substring(0, index);
            return existing + "\n" + marker + "\n" + body;
        }

        private static void WriteReport(string content)
        {
            try
            {
                var directory = Path.GetDirectoryName(ReportPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(ReportPath, content, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NPC Graft] Не удалось записать отчёт: " + ex.Message);
            }
        }
    }
}
#endif
