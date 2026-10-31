using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using ProjectC.Ship;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// ShipPrefabRepair — T-CULL01 / Stage 1 (H1).
    ///
    /// Зачем: коллайдеры корабля жили внутри nested FBX-инстанса префаба
    /// <c>Ship_havyII_base.prefab</c> как user-overrides на узлах модели. Переимпорт модели
    /// (переименование/удаление узла) сносит такие overrides. Задача — вынести
    /// user-созданные деревья коллайдеров в обычные дети prefab-корня
    /// (<c>SHIP_COLLIDERS</c>), чтобы они стали частью самого prefab-ассета
    /// и не зависели от FBX.
    ///
    /// ВАЖНО — что НЕ переносится и почему (решение Lead, подтверждено замером):
    ///   9 <c>DoorController</c> и 10 leaf-<c>BoxCollider</c> створок (9 дверей +
    ///   <c>Door_AftGate_Stb</c>) остаются на узлах FBX. Створка — это узел модели, и коллайдер
    ///   обязан ехать вместе с ней (сейчас он в точности повторяет bounds листа). Перенести
    ///   <c>DoorController</c> на отдельный объект нельзя: у него
    ///   <c>[RequireComponent(typeof(Collider))]</c>, а <c>Collider</c> абстрактный —
    ///   <c>AddComponent&lt;DoorController&gt;()</c> без коллайдера возвращает null
    ///   (проверено). Любой дублирующий коллайдер у проёма означал бы 259 боксов
    ///   и/или статичную преграду в открытой двери.
    ///
    /// Визуал не клеится, source FBX не меняется, сцена не трогается.
    /// Floating origin: коллайдеры хранят только локальные center/size — мировых кэшей нет.
    ///
    /// Меню (Tools): ProjectC → Ships → …
    ///   Repair Heavy Prefab (dry-run)   — только отчёт, без изменений;
    ///   Repair Heavy Prefab (apply)     — перенос + валидация + сохранение префаба;
    ///   Verify Heavy Prefab (FBX reimport) — ForceUpdate реимпорт FBX и повторная проверка;
    ///   Rollback Heavy Prefab (from backup) — восстановление префаба из локального бэкапа.
    ///
    /// Отчёт: docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/H1_prefab_repair.csv
    /// Бэкап: _local_backups/T-CULL01/H1/Ship_havyII_base.prefab.bak (вне Assets, не в git).
    /// </summary>
    public static class ShipPrefabRepair
    {
        private const string PrefabPath =
            "Assets/_Project/Prefabs/Ships/Ship_havyII_base.prefab";

        private const string FbxPath =
            "Assets/_Project/Art/Models/Ships/HeavyII_base/" +
            "project-c_ship_heavyII_1_cline_deepseek_1_glm-53_1_aura_1_game-ready_1.fbx";

        private const string ReportRelativePath =
            "docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/H1_prefab_repair.csv";

        private const string BackupRelativePath =
            "_local_backups/T-CULL01/H1/Ship_havyII_base.prefab.bak";

        private const string CollidersRootName = "SHIP_COLLIDERS";

        /// <summary>Ожидаемые baseline-значения H1 (замер Stage 0).</summary>
        private const int ExpectedBoxColliders = 250;
        private const int ExpectedDoorControllers = 9;
        private const int ExpectedMeshRenderers = 973;
        private const int ExpectedMeshFilters = 973;
        private const int ExpectedTransforms = 1452;
        /// <summary>Leaf-коллайдеры створок, которые остаются внутри FBX по решению Lead.</summary>
        private const int ExpectedBoxesInsideFbx = 10;

        private const float PoseTolerance = 1e-4f;

        // =====================================================================

        [MenuItem("ProjectC/Ships/Repair Heavy Prefab (dry-run)", false, 100)]
        public static void DryRun()
        {
            var rows = new List<Row>();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var ctx = new Context(root);
                CollectState(ctx, "before", rows);
                AddSummary(rows, "before", ctx);
                WriteReport(rows);
                Debug.Log("[H1] Dry-run: " + rows.Count + " строк → " + AbsoluteReportPath());
            }
            catch (Exception e)
            {
                Debug.LogError("[H1] Dry-run failed: " + e);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("ProjectC/Ships/Repair Heavy Prefab (apply)", false, 101)]
        public static void Apply()
        {
            var rows = new List<Row>();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            bool saved = false;
            bool needsImport = false;
            try
            {
                var ctx = new Context(root);
                CollectState(ctx, "before", rows);
                AddSummary(rows, "before", ctx);

                var before = SnapshotColliderPoses(root);

                // ---------------------------------------------------------
                // 1. Дерево коллайдеров prefab-корня.
                // ---------------------------------------------------------
                Transform collidersRoot = FindChild(root.transform, CollidersRootName);
                if (collidersRoot == null)
                {
                    var go = new GameObject(CollidersRootName);
                    go.transform.SetParent(root.transform, false);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.localScale = Vector3.one;
                    collidersRoot = go.transform;
                    rows.Add(Row.Info("applied", "structure", CollidersRootName, root.name,
                        "создан корень SHIP_COLLIDERS под prefab-корнем"));
                }

                if (ctx.FbxRoot == null)
                    throw new InvalidOperationException("Не найден nested FBX-инстанс в префабе: " + FbxPath);

                // ---------------------------------------------------------
                // 2. Собрать переносимые user-деревья коллайдеров.
                // ---------------------------------------------------------
                var movable = new List<GameObject>();
                foreach (var bc in root.GetComponentsInChildren<BoxCollider>(true))
                {
                    if (PrefabUtility.IsAddedComponentOverride(bc)) continue;      // leaf створок — остаются
                    if (!IsSelfOrDescendant(bc.transform, ctx.FbxRoot.transform)) continue;

                    var addedRoot = FindTopmostAddedRoot(bc.transform, ctx.FbxRoot.transform);
                    if (addedRoot == null)
                        throw new InvalidOperationException(
                            "Коллайдер на узле модели без user-корня — перенос без unpack невозможен: " +
                            PathOf(bc.transform));
                    if (addedRoot == collidersRoot.gameObject) continue;
                    if (!movable.Contains(addedRoot)) movable.Add(addedRoot);
                }

                // Пустые user-деревья коллайдеров (например <X>_Colliders без боксов) тоже выносим:
                // они часть того же ручного слоя и не должны зависеть от FBX.
                foreach (var t in ctx.FbxRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (t == ctx.FbxRoot) continue;
                    if (!t.name.EndsWith(BuildSettlementColliders.GeneratedSuffix, StringComparison.Ordinal)) continue;
                    if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject)) continue;

                    var addedRoot = FindTopmostAddedRoot(t, ctx.FbxRoot);
                    if (addedRoot == null) continue;
                    if (addedRoot == collidersRoot.gameObject) continue;
                    if (!movable.Contains(addedRoot)) movable.Add(addedRoot);
                }

                // ---------------------------------------------------------
                // 3. Safety-gate: переносим только чистую коллайдерную геометрию.
                // ---------------------------------------------------------
                foreach (var go in movable)
                {
                    string why = ValidateMovable(go);
                    if (why != null)
                        throw new InvalidOperationException(
                            "Отказ: '" + PathOf(go.transform) + "' содержит не только коллайдеры (" + why + ").");
                }

                // ---------------------------------------------------------
                // 4. Перенос (world pose сохраняется).
                // ---------------------------------------------------------
                int movedColliders = 0;
                foreach (var go in movable)
                {
                    movedColliders += go.GetComponentsInChildren<BoxCollider>(true).Length;
                    go.transform.SetParent(collidersRoot, true);
                    rows.Add(Row.Info("applied", "moved", PathOf(go.transform), PathOf(collidersRoot),
                        "перенесён user-корень коллайдеров"));
                }

                // ---------------------------------------------------------
                // 5. Валидация в памяти — сохраняем префаб только при полном совпадении.
                // ---------------------------------------------------------
                var after = SnapshotColliderPoses(root);
                var problems = ValidateAfter(root, ctx, before, after);
                if (problems.Count > 0)
                {
                    foreach (var p in problems) Debug.LogError("[H1] " + p);
                    rows.Add(Row.Info("aborted", "validation", "", "", string.Join(" | ", problems)));
                    WriteReport(rows);
                    throw new InvalidOperationException("Валидация не прошла, префаб НЕ сохранён.");
                }

                // ---------------------------------------------------------
                // 6. Сохранение prefab-ассета (сцену не трогаем).
                // ---------------------------------------------------------
                if (movable.Count == 0)
                {
                    // Идемпотентность: префаб уже отремонтирован — файл не переписываем.
                    rows.Add(Row.Info("applied", "save", PrefabPath, "",
                        "переносить нечего — префаб уже в целевом состоянии, файл не переписан"));
                }
                else
                {
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                    saved = true;
                    needsImport = true;
                    rows.Add(Row.Info("applied", "save", PrefabPath, "", "PrefabUtility.SaveAsPrefabAsset OK"));
                }

                CollectState(ctx, "after", rows);
                AddSummary(rows, "after", ctx);
                AddCheckedRow(rows, "after", "moved_user_roots", movable.Count.ToString(CultureInfo.InvariantCulture));
                AddCheckedRow(rows, "after", "moved_box_colliders", movedColliders.ToString(CultureInfo.InvariantCulture));
                WriteReport(rows);

                Debug.Log("[H1] Apply OK. Перенесено user-корней: " + movable.Count +
                          ", боксов вне FBX: " + CountOutside(ctx) +
                          ", отчёт: " + AbsoluteReportPath());
            }
            catch (Exception e)
            {
                Debug.LogError("[H1] Apply failed" + (saved ? " (префаб уже сохранён — см. rollback)" : " (префаб не сохранён)") + ": " + e);
                if (rows.Count > 0)
                {
                    rows.Add(Row.Info(saved ? "failed_after_save" : "failed", "exception", e.GetType().Name, "", e.Message));
                    WriteReport(rows);
                }
                throw;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                if (needsImport)
                    AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            }
        }

        [MenuItem("ProjectC/Ships/Verify Heavy Prefab (FBX reimport)", false, 102)]
        public static void VerifyAfterReimport()
        {
            var rows = new List<Row>();
            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var ctx = new Context(root);
                CollectState(ctx, "after_reimport", rows);
                AddSummary(rows, "after_reimport", ctx);

                var problems = ValidateCounts(root, ctx);
                rows.Add(Row.Info(problems.Count == 0 ? "after_reimport" : "after_reimport_failed",
                    "validation", "", "", problems.Count == 0 ? "OK" : string.Join(" | ", problems)));
                WriteReport(rows);

                if (problems.Count == 0)
                    Debug.Log("[H1] Reimport OK: 250 боксов / 9 дверей / 973 MR на месте. Отчёт: " + AbsoluteReportPath());
                else
                    foreach (var p in problems) Debug.LogError("[H1] Reimport: " + p);
            }
            catch (Exception e)
            {
                Debug.LogError("[H1] Reimport verify failed: " + e);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("ProjectC/Ships/Rollback Heavy Prefab (from backup)", false, 120)]
        public static void RollbackFromBackup()
        {
            string backup = AbsoluteProjectPath(BackupRelativePath);
            if (!File.Exists(backup))
            {
                Debug.LogError("[H1] Бэкап не найден: " + backup);
                return;
            }

            if (!EditorUtility.DisplayDialog("H1 rollback",
                    "Восстановить Ship_havyII_base.prefab из бэкапа?\n" + backup, "Восстановить", "Отмена"))
                return;

            File.Copy(backup, AbsoluteProjectPath(PrefabPath), true);
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("[H1] Префаб восстановлен из бэкапа: " + backup);
        }

        // =====================================================================
        // Контекст / сбор состояния
        // =====================================================================

        private sealed class Context
        {
            public readonly GameObject Root;
            public readonly Transform FbxRoot;

            public Context(GameObject root)
            {
                Root = root;
                FbxRoot = FindNestedInstanceBySource(root.transform, FbxPath);
            }
        }

        private static Transform FindNestedInstanceBySource(Transform root, string sourceAssetPath)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(t.gameObject)) continue;
                var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
                if (instanceRoot == null || instanceRoot == root.gameObject) continue;
                if (instanceRoot.transform != t) continue;

                var source = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
                if (source != null && AssetDatabase.GetAssetPath(source) == sourceAssetPath)
                    return instanceRoot.transform;
            }
            return null;
        }

        private static void CollectState(Context ctx, string phase, List<Row> rows)
        {
            var root = ctx.Root;

            foreach (var bc in root.GetComponentsInChildren<BoxCollider>(true))
            {
                bool addedComponent = PrefabUtility.IsAddedComponentOverride(bc);
                bool insideFbx = ctx.FbxRoot != null && IsSelfOrDescendant(bc.transform, ctx.FbxRoot);
                var t = bc.transform;

                rows.Add(new Row
                {
                    Phase = phase,
                    Category = "collider",
                    Path = PathOf(t),
                    Parent = t.parent != null ? PathOf(t.parent) : "",
                    InsideFbx = insideFbx ? "yes" : "no",
                    AddedGameObject = PrefabUtility.IsAddedGameObjectOverride(bc.gameObject) ? "yes" : "no",
                    AddedComponent = addedComponent ? "yes" : "no",
                    Type = "BoxCollider",
                    LocalPos = V(t.localPosition),
                    LocalRot = V(t.localEulerAngles),
                    LocalScale = V(t.localScale),
                    WorldPos = V(t.position),
                    Center = V(bc.center),
                    Size = V(bc.size),
                    IsTrigger = bc.isTrigger ? "yes" : "no",
                    Layer = t.gameObject.layer.ToString(CultureInfo.InvariantCulture),
                    Note = addedComponent ? "leaf-створка: остаётся в FBX (требование дверей)" :
                           (insideFbx ? "user-дерево внутри FBX" : "уже вне FBX")
                });
            }

            foreach (var dc in root.GetComponentsInChildren<DoorController>(true))
            {
                var t = dc.transform;
                bool insideFbx = ctx.FbxRoot != null && IsSelfOrDescendant(t, ctx.FbxRoot);
                var so = new SerializedObject(dc);
                var typeProp = so.FindProperty("doorType");
                string type = typeProp != null
                    ? typeProp.enumNames[typeProp.enumValueIndex]
                    : "?";

                rows.Add(new Row
                {
                    Phase = phase,
                    Category = "door",
                    Path = PathOf(t),
                    Parent = t.parent != null ? PathOf(t.parent) : "",
                    InsideFbx = insideFbx ? "yes" : "no",
                    AddedGameObject = PrefabUtility.IsAddedGameObjectOverride(dc.gameObject) ? "yes" : "no",
                    AddedComponent = PrefabUtility.IsAddedComponentOverride(dc) ? "yes" : "no",
                    Type = "DoorController/" + type,
                    LocalPos = V(t.localPosition),
                    LocalRot = V(t.localEulerAngles),
                    LocalScale = V(t.localScale),
                    WorldPos = V(t.position),
                    Center = "",
                    Size = "",
                    IsTrigger = "",
                    Layer = t.gameObject.layer.ToString(CultureInfo.InvariantCulture),
                    Note = insideFbx
                        ? "остаётся в FBX: collider створки обязан ехать с узлом модели"
                        : "уже вне FBX"
                });
            }

            foreach (var sr in root.GetComponentsInChildren<ShipRootReference>(true))
            {
                rows.Add(Row.Info(phase, "ship_root_reference", PathOf(sr.transform),
                    UnityEditor.PrefabUtility.IsAddedComponentOverride(sr) ? "added-component" : "prefab-own",
                    "контроль целостности"));
            }
        }

        private static void AddSummary(List<Row> rows, string phase, Context ctx)
        {
            var root = ctx.Root;
            int boxTotal = root.GetComponentsInChildren<BoxCollider>(true).Length;
            int boxInside = ctx.FbxRoot != null
                ? ctx.FbxRoot.GetComponentsInChildren<BoxCollider>(true).Length : 0;
            int doors = root.GetComponentsInChildren<DoorController>(true).Length;
            int doorsInside = ctx.FbxRoot != null
                ? ctx.FbxRoot.GetComponentsInChildren<DoorController>(true).Length : 0;
            int mr = root.GetComponentsInChildren<MeshRenderer>(true).Length;
            int mf = root.GetComponentsInChildren<MeshFilter>(true).Length;
            int tf = root.GetComponentsInChildren<Transform>(true).Length;
            int missing = CountMissingScripts(root);
            int leftoverTrees = CountColliderTreesInsideFbx(ctx);

            AddCheckedRow(rows, phase, "box_colliders_total", boxTotal.ToString(CultureInfo.InvariantCulture));
            AddCheckedRow(rows, phase, "box_colliders_inside_fbx", boxInside.ToString(CultureInfo.InvariantCulture));
            AddCheckedRow(rows, phase, "box_colliders_outside_fbx",
                (boxTotal - boxInside).ToString(CultureInfo.InvariantCulture));
            AddCheckedRow(rows, phase, "door_controllers_total", doors.ToString(CultureInfo.InvariantCulture));
            AddCheckedRow(rows, phase, "door_controllers_inside_fbx", doorsInside.ToString(CultureInfo.InvariantCulture));
            AddCheckedRow(rows, phase, "mesh_renderers", mr.ToString(CultureInfo.InvariantCulture));
            AddCheckedRow(rows, phase, "mesh_filters", mf.ToString(CultureInfo.InvariantCulture));
            AddCheckedRow(rows, phase, "transforms", tf.ToString(CultureInfo.InvariantCulture));
            AddCheckedRow(rows, phase, "missing_script_refs", missing.ToString(CultureInfo.InvariantCulture));
            AddCheckedRow(rows, phase, "collider_trees_left_inside_fbx", leftoverTrees.ToString(CultureInfo.InvariantCulture));
            AddCheckedRow(rows, phase, "ship_colliders_root_exists",
                FindChild(root.transform, CollidersRootName) != null ? "yes" : "no");
        }

        // =====================================================================
        // Снимок поз коллайдеров (до/после)
        // =====================================================================

        private sealed class PoseSnapshot
        {
            public string Path;
            public Vector3 WorldCenter;
            public Vector3 WorldSize;
        }

        private static List<PoseSnapshot> SnapshotColliderPoses(GameObject root)
        {
            var list = new List<PoseSnapshot>();
            foreach (var bc in root.GetComponentsInChildren<BoxCollider>(true))
            {
                var t = bc.transform;
                var s = t.lossyScale;
                list.Add(new PoseSnapshot
                {
                    Path = PathOf(t),
                    WorldCenter = t.TransformPoint(bc.center),
                    WorldSize = new Vector3(Mathf.Abs(bc.size.x * s.x), Mathf.Abs(bc.size.y * s.y), Mathf.Abs(bc.size.z * s.z))
                });
            }
            return list;
        }

        /// <summary>Сопоставление снимков по ПОЛНОМУ пути не работает (путь меняется при переносе),
        /// поэтому сравниваем наборы мировых поз: каждая «после» должна найтись в «до».</summary>
        private static string CompareSnapshots(List<PoseSnapshot> before, List<PoseSnapshot> after, out int matched)
        {
            matched = 0;
            float worstCenter = 0f, worstSize = 0f;
            string worstPath = "";

            var used = new bool[before.Count];
            foreach (var a in after)
            {
                int best = -1;
                float bestDist = float.MaxValue;
                for (int i = 0; i < before.Count; i++)
                {
                    float d = (before[i].WorldCenter - a.WorldCenter).sqrMagnitude;
                    if (d < bestDist) { bestDist = d; best = i; }
                }
                if (best < 0) return "нет пары для " + a.Path;

                var b = before[best];
                float dc = Vector3.Distance(b.WorldCenter, a.WorldCenter);
                float ds = (b.WorldSize - a.WorldSize).magnitude;
                if (dc > worstCenter) { worstCenter = dc; worstPath = a.Path; }
                if (ds > worstSize) worstSize = ds;
                if (!used[best]) { used[best] = true; matched++; }

                if (dc > PoseTolerance)
                    return string.Format(CultureInfo.InvariantCulture,
                        "смещение мирового центра {0:F6} у '{1}' (допуск {2})", dc, a.Path, PoseTolerance);
                if (ds > PoseTolerance)
                    return string.Format(CultureInfo.InvariantCulture,
                        "изменился мировой размер {0:F6} у '{1}' (допуск {2})", ds, a.Path, PoseTolerance);
            }

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[H1] Позы коллайдеров: сопоставлено {0}, max Δcenter={1:E3} ('{2}'), max Δsize={3:E3}",
                matched, worstCenter, worstPath, worstSize));
            return null;
        }

        // =====================================================================
        // Валидация
        // =====================================================================

        private static List<string> ValidateAfter(GameObject root, Context ctx,
            List<PoseSnapshot> before, List<PoseSnapshot> after)
        {
            var problems = ValidateCounts(root, ctx);

            string poseProblem = CompareSnapshots(before, after, out _);
            if (poseProblem != null) problems.Add("геометрия коллайдеров изменилась: " + poseProblem);

            int leftover = CountColliderTreesInsideFbx(ctx);
            if (leftover != 0)
                problems.Add("внутри FBX остались user-деревья коллайдеров: " + leftover);

            return problems;
        }

        private static List<string> ValidateCounts(GameObject root, Context ctx)
        {
            var problems = new List<string>();

            int boxTotal = root.GetComponentsInChildren<BoxCollider>(true).Length;
            int boxInside = ctx.FbxRoot != null
                ? ctx.FbxRoot.GetComponentsInChildren<BoxCollider>(true).Length : -1;
            int doors = root.GetComponentsInChildren<DoorController>(true).Length;
            int mr = root.GetComponentsInChildren<MeshRenderer>(true).Length;
            int mf = root.GetComponentsInChildren<MeshFilter>(true).Length;
            int missing = CountMissingScripts(root);

            if (boxTotal != ExpectedBoxColliders)
                problems.Add("BoxCollider: " + boxTotal + " вместо " + ExpectedBoxColliders);
            if (boxInside != ExpectedBoxesInsideFbx)
                problems.Add("BoxCollider внутри FBX: " + boxInside + " вместо " + ExpectedBoxesInsideFbx +
                             " (только leaf-створки)");
            if (doors != ExpectedDoorControllers)
                problems.Add("DoorController: " + doors + " вместо " + ExpectedDoorControllers);
            if (mr != ExpectedMeshRenderers)
                problems.Add("MeshRenderer: " + mr + " вместо " + ExpectedMeshRenderers);
            if (mf != ExpectedMeshFilters)
                problems.Add("MeshFilter: " + mf + " вместо " + ExpectedMeshFilters);
            if (missing != 0)
                problems.Add("missing script references: " + missing);

            foreach (var dc in root.GetComponentsInChildren<DoorController>(true))
            {
                var so = new SerializedObject(dc);
                foreach (var field in new[] { "hingePivot", "hingePanel", "leftPivot", "rightPivot", "leftHingePanel", "rightHingePanel", "liftPanel", "doorModel" })
                {
                    var p = so.FindProperty(field);
                    if (p != null && p.propertyType == SerializedPropertyType.ObjectReference &&
                        p.objectReferenceValue != null)
                    {
                        var comp = p.objectReferenceValue as Component;
                        if (comp == null || comp.gameObject == null)
                            problems.Add("битая ссылка DoorController." + field + " у " + dc.name);
                    }
                }
            }

            return problems;
        }

        private static int CountOutside(Context ctx)
        {
            int total = ctx.Root.GetComponentsInChildren<BoxCollider>(true).Length;
            int inside = ctx.FbxRoot != null ? ctx.FbxRoot.GetComponentsInChildren<BoxCollider>(true).Length : 0;
            return total - inside;
        }

        private static int CountMissingScripts(GameObject root)
        {
            int n = 0;
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb == null) n++;
            return n;
        }

        private static int CountColliderTreesInsideFbx(Context ctx)
        {
            if (ctx.FbxRoot == null) return 0;
            int n = 0;
            foreach (var t in ctx.FbxRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t == ctx.FbxRoot) continue;
                if (!t.name.EndsWith(BuildSettlementColliders.GeneratedSuffix, StringComparison.Ordinal)) continue;
                if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject)) continue; // узлы модели, не user-деревья
                n++;
            }
            return n;
        }

        /// <summary>Переносим только чистые держатели коллайдеров: ничего, что нельзя терять.</summary>
        private static string ValidateMovable(GameObject go)
        {
            if (!PrefabUtility.IsAddedGameObjectOverride(go))
                return "не является user (added) объектом";

            foreach (var c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null) return "есть missing component";
                if (c is Transform) continue;
                if (c is BoxCollider) continue;
                if (c is MeshRenderer || c is MeshFilter) return "есть визуал (" + c.GetType().Name + ")";
                return "есть " + c.GetType().Name;
            }
            return null;
        }

        // =====================================================================
        // Утилиты
        // =====================================================================

        private static GameObject FindTopmostAddedRoot(Transform from, Transform fbxRoot)
        {
            GameObject best = null;
            for (Transform t = from; t != null; t = t.parent)
            {
                if (PrefabUtility.IsAddedGameObjectOverride(t.gameObject)) best = t.gameObject;
                if (t == fbxRoot) break;
            }
            return best;
        }

        private static bool IsSelfOrDescendant(Transform t, Transform ancestor)
        {
            if (t == null || ancestor == null) return false;
            for (Transform p = t; p != null; p = p.parent)
                if (p == ancestor) return true;
            return false;
        }

        private static Transform FindChild(Transform parent, string name)
        {
            foreach (Transform c in parent)
                if (c.name == name) return c;
            return null;
        }

        private static string PathOf(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent)
                sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        private static string V(Vector3 v)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:F5},{1:F5},{2:F5}", v.x, v.y, v.z);
        }

        // =====================================================================
        // CSV
        // =====================================================================

        private sealed class Row
        {
            public string Phase = "";
            public string Category = "";
            public string Path = "";
            public string Parent = "";
            public string InsideFbx = "";
            public string AddedGameObject = "";
            public string AddedComponent = "";
            public string Type = "";
            public string LocalPos = "";
            public string LocalRot = "";
            public string LocalScale = "";
            public string WorldPos = "";
            public string Center = "";
            public string Size = "";
            public string IsTrigger = "";
            public string Layer = "";
            public string Note = "";

            public static Row Info(string phase, string category, string path, string parent, string note)
            {
                return new Row
                {
                    Phase = phase,
                    Category = category,
                    Path = path,
                    Parent = parent,
                    Note = note
                };
            }
        }

        private static void AddCheckedRow(List<Row> rows, string phase, string metric, string value)
        {
            rows.Add(Row.Info(phase, "summary", metric, "", value));
        }

        private static string AbsoluteProjectPath(string relative)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));
        }

        private static string AbsoluteReportPath()
        {
            return AbsoluteProjectPath(ReportRelativePath);
        }

        /// <summary>
        /// Отчёт — один CSV на тикет, но фазы пишутся разными запусками меню
        /// (dry-run / apply / reimport). Поэтому строки уже присутствующих фаз заменяются,
        /// а не затираются: в файле всегда живут before + after + after_reimport.
        /// </summary>
        private static void WriteReport(List<Row> rows)
        {
            string path = AbsoluteReportPath();
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            const string header = "phase,category,path,parent,inside_fbx,added_gameobject,added_component,type," +
                                  "local_pos,local_rot,local_scale,world_pos,center,size,is_trigger,layer,note";

            var phases = new HashSet<string>();
            foreach (var r in rows) phases.Add(r.Phase);

            // Сохранить строки фаз, которые текущий запуск не пересобирает.
            var lines = new List<string>();
            if (File.Exists(path))
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    if (line.Length == 0) continue;
                    if (line.StartsWith("phase,category,", StringComparison.Ordinal)) continue;

                    bool replaced = false;
                    foreach (var p in phases)
                        if (line.StartsWith(p + ",", StringComparison.Ordinal)) { replaced = true; break; }
                    if (!replaced) lines.Add(line);
                }
            }

            lines.Sort((a, b) => PhaseRank(a).CompareTo(PhaseRank(b)));
            var fresh = new List<string>();
            foreach (var r in rows) fresh.Add(ToLine(r));

            var sb = new StringBuilder();
            sb.AppendLine(header);
            foreach (var l in lines) sb.AppendLine(l);
            foreach (var l in fresh) sb.AppendLine(l);

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static int PhaseRank(string line)
        {
            if (line.StartsWith("before,", StringComparison.Ordinal)) return 0;
            if (line.StartsWith("applied,", StringComparison.Ordinal)) return 1;
            if (line.StartsWith("after,", StringComparison.Ordinal)) return 2;
            if (line.StartsWith("after_reimport", StringComparison.Ordinal)) return 3;
            return 4;
        }

        private static string ToLine(Row r)
        {
            return Csv(r.Phase) + "," + Csv(r.Category) + "," + Csv(r.Path) + "," + Csv(r.Parent) + "," +
                   Csv(r.InsideFbx) + "," + Csv(r.AddedGameObject) + "," + Csv(r.AddedComponent) + "," +
                   Csv(r.Type) + "," + Csv(r.LocalPos) + "," + Csv(r.LocalRot) + "," + Csv(r.LocalScale) + "," +
                   Csv(r.WorldPos) + "," + Csv(r.Center) + "," + Csv(r.Size) + "," + Csv(r.IsTrigger) + "," +
                   Csv(r.Layer) + "," + Csv(r.Note);
        }

        private static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            bool needQuotes = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
            if (!needQuotes) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
