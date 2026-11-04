// Project C — пайплайн корабля «Альбатрос» (NPC-префаб, метрический класс L: 99.63 м).
//
// Зачем отдельный инструмент: ShipVisualMerger / ShipPrefabRepair / ShipShadowBudgetTool зашиты на
// Ship_havyII_base (PrefabPath, FbxPath, абсолютные инварианты 9 дверей / 250 боксов / 11 слотов /
// 616 MR / LOD 300-900). SHIPS.md §6.5 прямо требует для нового корабля «повторить по образцу
// с собственным отчётом и инвариантами» — здесь это и сделано. Пайплайн:
//   §6.3 шаг 4 — мерж статики по ключу (ячейка, материал, настройки рендерера);
//   §6.3 шаг 5 — LODGroup: LOD0 полный, LOD1 без мелкой greeble, LOD2 = 1 меш × ≤5 материалов;
//   §6.3 шаг 6 — ShipShadowBudget на корень (класс L).
//
// Отличие от HeavyII-пилота: у «Альбатроса» MainVisual/projectC_ship_heavYbase_1_1 — плоская копия
// модели (Object.Instantiate, не nested FBX-инстанс), поэтому мерж не опирается на
// GetCorrespondingObjectFromSource / FindNestedInstanceBySource: источником считается сам узел
// визуала внутри префаба. Имена узлов исходной модели — произвольные (143 префикса), поэтому
// «ячейка» определяется не префиксом, а мировым габаритом узла:
//   Fine   — maxAxis < 0.5 м (болты, поручни, накладки, мелкая арматура) → только LOD0;
//   Core   — остальное → LOD0 и LOD1/LOD2.
// Исключение из правила габарита — навигационные огни (M_PC_Lantern_Red / M_PC_Lantern_Green,
// узлы Lamp_Nav_Prt / Lamp_Nav_Stb, 0.16–0.21 м): они обязаны дожить до LOD2, иначе на 450+ м
// у корабля пропадут красный/зелёный огни (та же логика, что в HeavyII LOD2).
//
// Что НИКОГДА не клеится (§4.2 COMMON): двери и створки (DoorController + Collider + ссылки
// hingePanel/rightHingePanel/doorModel), интерактив вне модели (PilotSeat, Door, Exchanger,
// Slot_Engine/EngineVisuals), коллайдеры, выключенные/неактивные рендереры, зеркальные
// трансформы, рендереры с пустым материалом или числом материалов ≠ числу сабмешей.
//
// Мерж строго edit-time, результат — mesh-ассеты в Assets/_Project/Generated/Ships (git).
// Runtime CombineMeshes не используется. Позиции берутся в локальном пространстве префаба,
// поэтому Floating Origin ничего не ломает (мировых кэшей нет).
//
// Меню: ProjectC → Ships → Albatros (NPC) → …
// Отчёт: docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/W_albatros_merge_2026-11-03.csv
// Бэкап: _local_backups/MAIN_PIPELINE/Albatros/Альбатрос.prefab.bak (вне Assets, не в git).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ProjectC.Ship;

namespace ProjectC.EditorTools
{
    public static class AlbatrosVisualMerger
    {
        // ------------------------------------------------------------------ цель
        private const string PrefabPath = "Assets/_Project/Prefabs/Ships/Альбатрос.prefab";
        private const string MainVisualName = "MainVisual";
        private const string ModelRootName = "projectC_ship_heavYbase_1_1";

        /// <summary>Корень склеенного визуала (полный аналог SHIP_VISUAL_H2 у HeavyII).</summary>
        public const string MergeRootName = "SHIP_VISUAL_ALB";

        private const string Lod0RootName = "LOD0";
        private const string Lod1RootName = "LOD1";
        private const string Lod2Name = "LOD2_All";

        private const string MeshFolder = "Assets/_Project/Generated/Ships";
        private const string MeshPrefix = "Albatros_VIS_";

        private const string ReportRelativePath =
            "docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/W_albatros_merge_2026-11-03.csv";

        private const string BackupRelativePath =
            "_local_backups/MAIN_PIPELINE/Albatros/Альбатрос.prefab.bak";

        // ------------------------------------------------------------ параметры мержа
        /// <summary>Граница ячейки Fine: мировой maxAxis рендерера, м.</summary>
        private const float FineMaxAxis = 0.5f;

        /// <summary>Материалы, которые обязаны дожить до LOD2 (навигационные огни).</summary>
        private static readonly string[] AlwaysCoreMaterials = { "M_PC_Lantern_Red", "M_PC_Lantern_Green" };

        /// <summary>Пороги LOD (м) при FOV 60° и lodBias 1. Флор LOD0 = 0.866 × 99.63 = 86.3 м.</summary>
        private const float Lod1Distance = 150f;
        private const float Lod2Distance = 450f;
        private const float CalibrationFov = 60f;

        /// <summary>
        /// LOD2: 5 семейств. Ключ — имя материала визуала, значение — целевой материал семейства.
        /// Раскладка повторяет логику HeavyII: навигационные огни (красный/зелёный) остаются
        /// самостоятельными сабмешами, стекло и белые линзы уходят в «свечение», палуба и дерево —
        /// в плиту, ржавчина — в тёмную сталь.
        /// </summary>
        private static readonly Dictionary<string, string> Lod2Family = new Dictionary<string, string>
        {
            { "M_PC_Steel_v2", "M_PC_Steel_v2_Dark" },
            { "M_PC_Steel_v2_Dark", "M_PC_Steel_v2_Dark" },
            { "M_PC_IronRust", "M_PC_Steel_v2_Dark" },
            { "M_PC_Bronze", "M_PC_Bronze_Dark" },
            { "M_PC_Bronze_Dark", "M_PC_Bronze_Dark" },
            { "M_PC_Bronze_Brass", "M_PC_Bronze_Dark" },
            { "M_PC_Plate", "M_PC_Plate" },
            { "M_PC_Wood", "M_PC_Plate" },
            { "M_PC_Fabric", "M_PC_Plate" },
            { "M_PC_Plastic_Red", "M_PC_Plate" },
            { "M_PC_Lantern_Red", "M_PC_Lantern_Red" },
            { "M_PC_Glass_Green", "M_PC_Lantern_Green" },
            { "M_PC_Lantern_Cool", "M_PC_Lantern_Green" },
            { "M_PC_Lantern_Green", "M_PC_Lantern_Green" },
        };

        private static readonly string[] Lod2Order =
        {
            "M_PC_Steel_v2_Dark", "M_PC_Bronze_Dark", "M_PC_Plate", "M_PC_Lantern_Red", "M_PC_Lantern_Green"
        };

        // ------------------------------------------------------- инварианты «Альбатроса»
        // Зафиксированы на 2026-11-03 после ручной правки пользователя (коллайдеры + двери).
        // При следующей ручной правке дверей/коллайдеров — обновить эти числа вместе с отчётом.
        private const int ExpectDoors = 8;
        private const int ExpectBoxColliders = 125;
        private const int ExpectColliders = 127;
        private const int ExpectModuleSlots = 10;

        /// <summary>Рендереры визуала, которые обязаны остаться неслитыми (двери/створки/интерактив).</summary>
        private const int ExpectProtectedRenderers = 25;

        /// <summary>Все материалы визуала обязаны быть из палитры M_PC_* (14 штук ≤ 24 из §5.2).</summary>
        private const int MaxPaletteMaterials = 24;

        // =====================================================================
        // Меню
        // =====================================================================

        [MenuItem("ProjectC/Ships/Albatros (NPC)/1. Merge Visual (dry-run)", false, 170)]
        public static void DryRun()
        {
            var report = new Report();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (FindMergeRoot(root.transform) != null)
                {
                    report.Add("dryrun", "state", "already_applied", "yes", "SHIP_VISUAL_ALB уже есть; см. Verify / Rollback");
                    ValidateMergedState(root, report.Problems);
                }
                else
                {
                    var plan = BuildPlan(root, report, "dryrun");
                    BuildMeshes(root, plan, report, "dryrun");
                    plan.DisposeMeshes();
                }
                report.Write();
                Debug.Log("[ALB] Dry-run OK → " + AbsoluteProjectPath(ReportRelativePath));
            }
            catch (Exception e)
            {
                report.Add("failed", "exception", e.GetType().Name, "", e.Message);
                report.Write();
                Debug.LogError("[ALB] Dry-run failed: " + e);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("ProjectC/Ships/Albatros (NPC)/2. Merge Visual (apply)", false, 171)]
        public static void Apply()
        {
            var report = new Report();
            var createdAssets = new List<string>();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            bool saved = false;
            Plan plan = null;
            try
            {
                if (FindMergeRoot(root.transform) != null)
                    throw new InvalidOperationException("Мерж уже применён (есть " + MergeRootName + "). Сначала Rollback.");

                plan = BuildPlan(root, report, "before");
                BuildMeshes(root, plan, report, "applied");

                // ---- иерархия в памяти ----
                var mainVisual = FindModelRoot(root.transform).parent;
                var mergeRoot = CreateChild(mainVisual, MergeRootName);
                var lod0Root = CreateChild(mergeRoot, Lod0RootName);
                var lod1Root = CreateChild(mergeRoot, Lod1RootName);

                var lod0 = new List<Renderer>();
                foreach (var g in plan.Lod0Groups)
                    lod0.Add(SpawnRenderer(lod0Root, g.ObjectName, g.Mesh, new[] { g.Material }, g.Settings));
                var lod1 = new List<Renderer>();
                foreach (var g in plan.Lod1Groups)
                    lod1.Add(SpawnRenderer(lod1Root, g.ObjectName, g.Mesh, new[] { g.Material }, g.Settings));
                var lod2 = new List<Renderer>
                {
                    SpawnRenderer(mergeRoot, Lod2Name, plan.Lod2Mesh, plan.Lod2Materials, plan.Lod2Settings)
                };

                // ---- снять визуал со слитых узлов (узлы и Transform остаются, как в HeavyII) ----
                foreach (var r in plan.Merged)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    UnityEngine.Object.DestroyImmediate(r);
                    if (mf != null) UnityEngine.Object.DestroyImmediate(mf);
                }

                ConfigureLodGroup(mergeRoot.gameObject, lod0, lod1, lod2, plan.WorldSize, report);

                // ---- валидация в памяти ----
                var problems = new List<string>();
                ValidateMergedState(root, problems);
                ValidateInteractive(root, problems);
                if (problems.Count > 0)
                    throw new InvalidOperationException("Валидация не прошла, префаб НЕ сохранён: " +
                                                        string.Join(" | ", problems));

                // ---- бэкап, ассеты, сохранение ----
                string backup = AbsoluteProjectPath(BackupRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                File.Copy(AbsoluteProjectPath(PrefabPath), backup, true);
                report.Add("applied", "backup", BackupRelativePath, "ok", "полный .prefab до мержа");

                EnsureFolder(MeshFolder);
                foreach (var mesh in plan.AllMeshes())
                {
                    string path = MeshFolder + "/" + mesh.name + ".asset";
                    if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
                    AssetDatabase.CreateAsset(mesh, path);
                    createdAssets.Add(path);
                }
                AssetDatabase.SaveAssets();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                if (!ok) throw new InvalidOperationException("SaveAsPrefabAsset вернул false");
                saved = true;

                AddSlotModel(root, report, "after");
                report.Add("applied", "save", PrefabPath, "ok", "mesh-ассетов: " + createdAssets.Count);
                report.Write();
                Debug.Log("[ALB] Apply OK. Mesh-ассетов: " + createdAssets.Count + ". Отчёт: " +
                          AbsoluteProjectPath(ReportRelativePath));
            }
            catch (Exception e)
            {
                if (!saved)
                {
                    foreach (var p in createdAssets) AssetDatabase.DeleteAsset(p);
                    if (plan != null) plan.DisposeMeshes();
                }
                report.Add(saved ? "failed_after_save" : "failed", "exception", e.GetType().Name, "", e.Message);
                report.Write();
                Debug.LogError("[ALB] Apply failed" + (saved ? " (префаб сохранён — см. Rollback)" : " (префаб не сохранён)") + ": " + e);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                if (saved) AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            }
        }

        [MenuItem("ProjectC/Ships/Albatros (NPC)/3. Verify (merge + LOD + budget)", false, 172)]
        public static void Verify()
        {
            var report = new Report();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var problems = report.Problems;
                ValidateMergedState(root, problems);
                ValidateInteractive(root, problems);
                AddSlotModel(root, report, "verify");
                ReportBudget(root, report, problems);
                report.Add("verify", "validation", "", problems.Count == 0 ? "OK" : "FAIL",
                    string.Join(" | ", problems));
                report.Write();

                if (problems.Count == 0) Debug.Log("[ALB] Verify OK. Отчёт: " + AbsoluteProjectPath(ReportRelativePath));
                else foreach (var p in problems) Debug.LogError("[ALB] Verify: " + p);
            }
            catch (Exception e)
            {
                report.Add("failed", "exception", e.GetType().Name, "", e.Message);
                report.Write();
                Debug.LogError("[ALB] Verify failed: " + e);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("ProjectC/Ships/Albatros (NPC)/4. Log Merge (no changes)", false, 173)]
        public static void LogCurrentState()
        {
            var report = new Report();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                AddSlotModel(root, report, "current");
                report.Add("current", "instance", "renderers",
                    root.GetComponentsInChildren<MeshRenderer>(true).Length.ToString(CultureInfo.InvariantCulture),
                    "LODGroup=" + root.GetComponentsInChildren<LODGroup>(true).Length);
                report.Write();
                Debug.Log("[ALB] Состояние записано в " + AbsoluteProjectPath(ReportRelativePath));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // =====================================================================
        // Shadow budget (класс L) — отдельные пункты меню, как в HeavyII-пайплайне
        // =====================================================================

        [MenuItem("ProjectC/Ships/Albatros (NPC)/5. Shadow Budget (apply)", false, 174)]
        public static void ApplyShadowBudget()
        {
            var report = new Report();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            bool saved = false;
            try
            {
                if (root.GetComponent<ShipShadowBudget>() != null)
                    throw new InvalidOperationException("ShipShadowBudget уже на префабе — повторно не добавляется.");

                var problems = new List<string>();
                ValidateMergedState(root, problems);
                ValidateInteractive(root, problems);
                if (problems.Count > 0)
                    throw new InvalidOperationException("Валидация ДО установки бюджета не прошла: " +
                                                        string.Join(" | ", problems));

                string backup = AbsoluteProjectPath(BackupRelativePath + ".budget");
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                File.Copy(AbsoluteProjectPath(PrefabPath), backup, true);
                report.Add("budget_applied", "backup", BackupRelativePath + ".budget", "ok", "состояние до бюджета");

                var budget = root.AddComponent<ShipShadowBudget>();
                report.Add("budget_applied", "component", "ShipShadowBudget", "added",
                    "on_m=" + F(budget.ShadowOnDistance) + "; off_m=" + F(budget.ShadowOffDistance) +
                    "; coverWholeLodGroup=" + budget.CoverWholeLodGroup +
                    "; maxAxis_m=" + F(budget.SmallPartMaxAxis));

                var after = new List<string>();
                ValidateMergedState(root, after);
                ValidateInteractive(root, after);
                if (after.Count > 0)
                    throw new InvalidOperationException("Валидация ПОСЛЕ установки бюджета не прошла: " +
                                                        string.Join(" | ", after));

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                if (!ok) throw new InvalidOperationException("SaveAsPrefabAsset вернул false");
                saved = true;
                report.Add("budget_applied", "save", PrefabPath, "ok", "компонент на корне");
                report.Write();
                Debug.Log("[ALB] ShipShadowBudget установлен (on 140 / off 150 = порог LOD1). Бэкап: " +
                          BackupRelativePath + ".budget");
            }
            catch (Exception e)
            {
                report.Add(saved ? "failed_after_save" : "failed", "exception", e.GetType().Name, "", e.Message);
                report.Write();
                Debug.LogError("[ALB] Shadow Budget apply failed: " + e);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                if (saved) AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            }
        }

        [MenuItem("ProjectC/Ships/Albatros (NPC)/6. Shadow Budget (rollback)", false, 175)]
        public static void RollbackShadowBudget()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            bool saved = false;
            try
            {
                var budget = root.GetComponent<ShipShadowBudget>();
                if (budget == null) { Debug.Log("[ALB] ShipShadowBudget нет — снимать нечего."); return; }

                string backup = AbsoluteProjectPath(BackupRelativePath + ".budget.rollback");
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                File.Copy(AbsoluteProjectPath(PrefabPath), backup, true);
                UnityEngine.Object.DestroyImmediate(budget, true);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                if (!ok) throw new InvalidOperationException("SaveAsPrefabAsset вернул false");
                saved = true;
                Debug.Log("[ALB] ShipShadowBudget снят, префаб сохранён. Бэкап: " + backup);
            }
            catch (Exception e)
            {
                Debug.LogError("[ALB] Shadow Budget rollback failed: " + e);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                if (saved) AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            }
        }

        // =====================================================================
        // Rollback мержа
        // =====================================================================

        [MenuItem("ProjectC/Ships/Albatros (NPC)/9. Rollback (from backup)", false, 179)]
        public static void Rollback()
        {
            string backup = AbsoluteProjectPath(BackupRelativePath);
            if (!File.Exists(backup)) { Debug.LogError("[ALB] Бэкап не найден: " + backup); return; }
            if (!EditorUtility.DisplayDialog("Albatros merge rollback",
                    "Восстановить Альбатрос.prefab из бэкапа и удалить меши " + MeshPrefix + "*?\n" + backup,
                    "Восстановить", "Отмена"))
                return;

            File.Copy(backup, AbsoluteProjectPath(PrefabPath), true);
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            DeleteMeshAssets();
            Debug.Log("[ALB] Префаб восстановлен из бэкапа, меши " + MeshPrefix + "* удалены.");
        }

        private static void DeleteMeshAssets()
        {
            if (!AssetDatabase.IsValidFolder(MeshFolder)) return;
            foreach (var guid in AssetDatabase.FindAssets(MeshPrefix + " t:Mesh", new[] { MeshFolder }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(p).StartsWith(MeshPrefix, StringComparison.Ordinal)) AssetDatabase.DeleteAsset(p);
            }
        }

        // =====================================================================
        // План
        // =====================================================================

        private struct RendererSettings
        {
            public int Layer;
            public ShadowCastingMode Shadows;
            public bool ReceiveShadows;
            public LightProbeUsage LightProbes;
            public ReflectionProbeUsage ReflectionProbes;
            public MotionVectorGenerationMode MotionVectors;
            public uint RenderingLayerMask;

            public static RendererSettings Of(MeshRenderer r)
            {
                return new RendererSettings
                {
                    Layer = r.gameObject.layer,
                    Shadows = r.shadowCastingMode,
                    ReceiveShadows = r.receiveShadows,
                    LightProbes = r.lightProbeUsage,
                    ReflectionProbes = r.reflectionProbeUsage,
                    MotionVectors = r.motionVectorGenerationMode,
                    RenderingLayerMask = r.renderingLayerMask
                };
            }

            public string Key()
            {
                return Layer + "|" + Shadows + "|" + ReceiveShadows + "|" + LightProbes + "|" +
                       ReflectionProbes + "|" + MotionVectors + "|" + RenderingLayerMask;
            }
        }

        private sealed class Group
        {
            public string Cell;
            public Material Material;
            public RendererSettings Settings;
            public readonly List<CombineInstance> Parts = new List<CombineInstance>();
            public int Sources;
            public long SourceTris;
            public Mesh Mesh;
            public string ObjectName;
        }

        private sealed class Plan
        {
            public Transform ModelRoot;
            public float WorldSize;
            public readonly List<MeshRenderer> Merged = new List<MeshRenderer>();
            public readonly List<MeshRenderer> Protected = new List<MeshRenderer>();
            public readonly List<Group> Lod0Groups = new List<Group>();
            public readonly List<Group> Lod1Groups = new List<Group>();
            public Mesh Lod2Mesh;
            public Material[] Lod2Materials;
            public RendererSettings Lod2Settings;
            public long Lod2SourceTris;
            public long SourceTris;

            public IEnumerable<Mesh> AllMeshes()
            {
                foreach (var g in Lod0Groups) if (g.Mesh != null) yield return g.Mesh;
                foreach (var g in Lod1Groups) if (g.Mesh != null) yield return g.Mesh;
                if (Lod2Mesh != null) yield return Lod2Mesh;
            }

            public void DisposeMeshes()
            {
                foreach (var m in new List<Mesh>(AllMeshes()))
                    if (m != null && !AssetDatabase.Contains(m)) UnityEngine.Object.DestroyImmediate(m);
            }
        }

        private static Plan BuildPlan(GameObject root, Report report, string phase)
        {
            var modelRoot = FindModelRoot(root.transform);
            if (modelRoot == null)
                throw new InvalidOperationException("Не найден узел визуала " + MainVisualName + "/" + ModelRootName);

            var plan = new Plan { ModelRoot = modelRoot };
            var protectedTransforms = CollectProtectedTransforms(root, modelRoot);

            long srcTris = 0, srcVerts = 0;
            foreach (var r in modelRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                string why = ProtectReason(r, protectedTransforms);
                if (why != null)
                {
                    plan.Protected.Add(r);
                    report.Add(phase, "protected", PathOf(r.transform), why, "");
                    continue;
                }
                plan.Merged.Add(r);
                var mf = r.GetComponent<MeshFilter>();
                for (int sub = 0; sub < mf.sharedMesh.subMeshCount; sub++)
                    srcTris += mf.sharedMesh.GetIndexCount(sub) / 3;
                srcVerts += mf.sharedMesh.vertexCount;
            }
            plan.SourceTris = srcTris;

            // --- LOD0: (ячейка, материал, настройки) ---
            var lod0 = new Dictionary<string, Group>();
            foreach (var r in plan.Merged) AddToGroups(lod0, CellOf(r), r);
            plan.Lod0Groups.AddRange(SortedGroups(lod0, "LOD0"));

            // --- LOD1: только Core, по материалу ---
            var lod1 = new Dictionary<string, Group>();
            foreach (var r in plan.Merged)
                if (CellOf(r) == CellCore) AddToGroups(lod1, "LOD1", r);
            plan.Lod1Groups.AddRange(SortedGroups(lod1, "LOD1"));

            // --- мир: габарит для порогов LOD ---
            Bounds wb = default; bool has = false;
            foreach (var r in modelRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!has) { wb = r.bounds; has = true; } else wb.Encapsulate(r.bounds);
            }
            plan.WorldSize = has ? Mathf.Max(wb.size.x, Mathf.Max(wb.size.y, wb.size.z)) : 0f;
            if (plan.WorldSize <= 0f) throw new InvalidOperationException("Не удалось измерить габарит визуала");

            float floor = 0.866f * plan.WorldSize;
            report.Add(phase, "plan", "world_size_m", F(plan.WorldSize), "порог LOD0 не может быть ближе 0.866×size = " + F(floor) + " м");
            if (Lod1Distance < floor)
                throw new InvalidOperationException("LOD0→LOD1 " + Lod1Distance + " м ближе физического флора " + F(floor) + " м");

            int fineCount = 0;
            foreach (var r in plan.Merged) if (CellOf(r) == CellFine) fineCount++;

            report.Add(phase, "plan", "visual_renderers", (plan.Merged.Count + plan.Protected.Count).ToString(CultureInfo.InvariantCulture),
                "сливается=" + plan.Merged.Count + " (Fine=" + fineCount + ", Core=" + (plan.Merged.Count - fineCount) + "), защищено=" + plan.Protected.Count);
            report.Add(phase, "plan", "merged_tris", srcTris.ToString(CultureInfo.InvariantCulture),
                "verts=" + srcVerts + " (Σ по всем сливаемым рендерерам — инвариант склейки)");
            report.Add(phase, "plan", "lod0_groups", plan.Lod0Groups.Count.ToString(CultureInfo.InvariantCulture), "");
            report.Add(phase, "plan", "lod1_groups", plan.Lod1Groups.Count.ToString(CultureInfo.InvariantCulture), "");
            return plan;
        }

        /// <summary>Ячейка мержа: габаритная мелочь → Fine, навигационные огни всегда Core.</summary>
        private static string CellOf(MeshRenderer r)
        {
            var mat = r.sharedMaterials.Length > 0 ? r.sharedMaterials[0] : null;
            if (mat != null)
                foreach (var n in AlwaysCoreMaterials)
                    if (mat.name == n) return CellCore;

            var b = r.bounds;
            float axis = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            return axis < FineMaxAxis ? CellFine : CellCore;
        }

        private const string CellFine = "Fine";
        private const string CellCore = "Core";

        private static void AddToGroups(Dictionary<string, Group> groups, string cell, MeshRenderer r)
        {
            var mesh = r.GetComponent<MeshFilter>().sharedMesh;
            var mats = r.sharedMaterials;
            var settings = RendererSettings.Of(r);
            var touched = new HashSet<Group>();
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var mat = mats[sub];
                string key = cell + "|" + AssetDatabase.GetAssetPath(mat) + "#" + mat.name + "|" + settings.Key();
                if (!groups.TryGetValue(key, out var g))
                {
                    g = new Group { Cell = cell, Material = mat, Settings = settings };
                    groups.Add(key, g);
                }
                g.Parts.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = r.localToWorldMatrix });
                g.SourceTris += mesh.GetIndexCount(sub) / 3;
                touched.Add(g);
            }
            foreach (var g in touched) g.Sources++;
        }

        private static List<Group> SortedGroups(Dictionary<string, Group> groups, string lodTag)
        {
            var list = new List<Group>(groups.Values);
            list.Sort((a, b) =>
            {
                int c = string.CompareOrdinal(a.Cell, b.Cell);
                return c != 0 ? c : string.CompareOrdinal(a.Material.name, b.Material.name);
            });

            var used = new HashSet<string>();
            foreach (var g in list)
            {
                string baseName = MeshPrefix + lud(lodTag) + "_" + g.Cell + "_" + Sanitize(g.Material.name);
                string name = baseName;
                for (int i = 2; used.Contains(name); i++) name = baseName + "_" + i;
                used.Add(name);
                g.ObjectName = name;
            }
            return list;
        }

        private static string lud(string s)
        {
            return s.ToLowerInvariant();
        }

        // =====================================================================
        // Сборка мешей
        // =====================================================================

        private static void BuildMeshes(GameObject root, Plan plan, Report report, string phase)
        {
            // Меши живут в локальном пространстве склеенного корня = локальное пространство префаба
            // (MainVisual и корень имеют identity-трансформ, масштаб 1.06 модели запекается в вершины).
            Matrix4x4 toLocal = root.transform.worldToLocalMatrix;

            foreach (var g in plan.Lod0Groups) g.Mesh = CombineGroup(g, toLocal, report, phase);
            foreach (var g in plan.Lod1Groups) g.Mesh = CombineGroup(g, toLocal, report, phase);

            long lod0Out = 0;
            foreach (var g in plan.Lod0Groups) lod0Out += CountTris(g.Mesh);
            CheckTris("LOD0 total", plan.SourceTris, lod0Out);

            report.Add(phase, "tris", "lod0_total", lod0Out.ToString(CultureInfo.InvariantCulture),
                "src=" + plan.SourceTris + "; Δ=0 обязателен");

            // LOD2: семейства материалов → один меш с ≤5 сабмешами.
            var familyMeshes = new List<CombineInstance>();
            var familyMats = new List<Material>();
            var temps = new List<Mesh>();
            foreach (string family in Lod2Order)
            {
                var parts = new List<CombineInstance>();
                Material target = null;
                foreach (var g in plan.Lod1Groups)
                {
                    if (!Lod2Family.TryGetValue(g.Material.name, out var fam))
                        throw new InvalidOperationException("Материал без LOD2-семейства: " + g.Material.name);
                    if (fam != family) continue;
                    parts.Add(new CombineInstance { mesh = g.Mesh, subMeshIndex = 0, transform = Matrix4x4.identity });
                    if (g.Material.name == family) target = g.Material;
                }
                if (parts.Count == 0) continue;
                if (target == null) throw new InvalidOperationException("Нет ассета целевого материала LOD2: " + family);
                var fm = NewMesh(MeshPrefix + "tmp_" + family, CountVerts(parts));
                fm.CombineMeshes(parts.ToArray(), true, false, false);
                temps.Add(fm);
                familyMeshes.Add(new CombineInstance { mesh = fm, subMeshIndex = 0, transform = Matrix4x4.identity });
                familyMats.Add(target);
            }

            if (familyMats.Count > 5)
                throw new InvalidOperationException("LOD2: сабмешей " + familyMats.Count + " > 5 (бюджет L)");

            var lod2 = NewMesh(MeshPrefix + "LOD2_All", CountVerts(familyMeshes));
            lod2.CombineMeshes(familyMeshes.ToArray(), false, false, false);
            lod2.RecalculateBounds();
            foreach (var t in temps) UnityEngine.Object.DestroyImmediate(t);
            plan.Lod2Mesh = lod2;
            plan.Lod2Materials = familyMats.ToArray();
            plan.Lod2Settings = plan.Lod1Groups[0].Settings;

            long lod1Out = 0;
            foreach (var g in plan.Lod1Groups) lod1Out += CountTris(g.Mesh);
            plan.Lod2SourceTris = lod1Out;

            report.Add(phase, "mesh", lod2.name, CountTris(lod2).ToString(CultureInfo.InvariantCulture),
                "src_tris=" + lod1Out + " verts=" + lod2.vertexCount + " submeshes=" + lod2.subMeshCount +
                " idx=" + lod2.indexFormat + " mats=" + string.Join("/", Names(familyMats)));

            // Мировой AABB склейки обязан совпасть с объединением исходных bounds (проверка матриц).
            CheckWorldBounds(root, plan);
        }

        private static Mesh CombineGroup(Group g, Matrix4x4 toLocal, Report report, string phase)
        {
            var parts = new CombineInstance[g.Parts.Count];
            for (int i = 0; i < parts.Length; i++)
            {
                var p = g.Parts[i];
                p.transform = toLocal * p.transform;
                parts[i] = p;
            }
            var mesh = NewMesh(g.ObjectName, CountVerts(g.Parts));
            mesh.CombineMeshes(parts, true, true, false);
            mesh.RecalculateBounds();

            long outTris = CountTris(mesh);
            CheckTris(g.ObjectName, g.SourceTris, outTris);
            report.Add(phase, "mesh", g.ObjectName, outTris.ToString(CultureInfo.InvariantCulture),
                "cell=" + g.Cell + " mat=" + g.Material.name + " sources=" + g.Sources +
                " src_tris=" + g.SourceTris + " verts=" + mesh.vertexCount + " idx=" + mesh.indexFormat);
            return mesh;
        }

        private static Mesh NewMesh(string name, long vertexCount)
        {
            var mesh = new Mesh { name = name };
            if (vertexCount > 65535) mesh.indexFormat = IndexFormat.UInt32;
            return mesh;
        }

        private static long CountVerts(List<CombineInstance> parts)
        {
            long n = 0;
            foreach (var p in parts) n += p.mesh.vertexCount;
            return n;
        }

        private static long CountTris(Mesh m)
        {
            long n = 0;
            for (int i = 0; i < m.subMeshCount; i++) n += m.GetIndexCount(i) / 3;
            return n;
        }

        private static void CheckTris(string what, long src, long dst)
        {
            if (src != dst)
                throw new InvalidOperationException("Σtris расходится у " + what + ": " + src + " → " + dst);
        }

        private static void CheckWorldBounds(GameObject root, Plan plan)
        {
            Bounds src = default; bool has = false;
            var l2w = root.transform.localToWorldMatrix;
            foreach (var g in plan.Lod0Groups)
            {
                var b = TransformBounds(l2w, g.Mesh.bounds);
                if (!has) { src = b; has = true; } else src.Encapsulate(b);
            }
            Bounds dst = default; bool hasDst = false;
            foreach (var r in plan.Merged)
            {
                if (!hasDst) { dst = r.bounds; hasDst = true; } else dst.Encapsulate(r.bounds);
            }
            if (!has || !hasDst) return;
            float d = Mathf.Max((src.min - dst.min).magnitude, (src.max - dst.max).magnitude);
            if (d > 0.05f)
                throw new InvalidOperationException("Мировой AABB склейки расходится на " +
                                                    d.ToString("F3", CultureInfo.InvariantCulture) + " м");
        }

        private static Bounds TransformBounds(Matrix4x4 m, Bounds b)
        {
            var result = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
            var e = b.extents;
            for (int i = 0; i < 8; i++)
            {
                var c = b.center + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                result.Encapsulate(m.MultiplyPoint3x4(c));
            }
            return result;
        }

        // =====================================================================
        // Префаб: объекты, LODGroup
        // =====================================================================

        private static Transform CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        private static MeshRenderer SpawnRenderer(Transform parent, string name, Mesh mesh, Material[] mats, RendererSettings s)
        {
            var t = CreateChild(parent, name);
            t.gameObject.layer = s.Layer;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = t.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = s.Shadows;
            r.receiveShadows = s.ReceiveShadows;
            r.lightProbeUsage = s.LightProbes;
            r.reflectionProbeUsage = s.ReflectionProbes;
            r.motionVectorGenerationMode = s.MotionVectors;
            r.renderingLayerMask = s.RenderingLayerMask;
            return r;
        }

        private static void ConfigureLodGroup(GameObject host, List<Renderer> lod0, List<Renderer> lod1,
            List<Renderer> lod2, float worldSize, Report report)
        {
            var group = host.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.None;
            group.animateCrossFading = false;

            var ls = host.transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(ls.x), Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
            if (maxScale <= 0f) maxScale = 1f;

            float h0 = RelativeHeight(worldSize, Lod1Distance);
            float h1 = RelativeHeight(worldSize, Lod2Distance);

            group.SetLODs(new[]
            {
                new LOD(h0, lod0.ToArray()),
                new LOD(h1, lod1.ToArray()),
                new LOD(0f, lod2.ToArray()), // без cull: LOD2 до far clip
            });

            // localReferencePoint/size — ПОСЛЕ SetLODs (SetLODs пересчитывает bounds).
            Bounds wb = default; bool has = false;
            foreach (var r in lod0) { if (!has) { wb = r.bounds; has = true; } else wb.Encapsulate(r.bounds); }
            if (has) group.localReferencePoint = host.transform.InverseTransformPoint(wb.center);
            group.size = worldSize / maxScale;

            float d0 = EditorLodDistance(group, h0), d1 = EditorLodDistance(group, h1);
            if (!float.IsNaN(d0) && (Mathf.Abs(d0 - Lod1Distance) > 3f || Mathf.Abs(d1 - Lod2Distance) > 9f))
                throw new InvalidOperationException("Пороги LOD не совпали: " + F(d0) + " / " + F(d1) + " м");

            report.Add("applied", "lod", "world_size_m", F(worldSize), "size(local)=" + F(group.size) + "; maxLossyScale=" + F(maxScale));
            report.Add("applied", "lod", "lod0_to_lod1", F(h0),
                Lod1Distance + " м @FOV60 lodBias1 (LODUtility: " + F(d0) + " м); renderers=" + lod0.Count);
            report.Add("applied", "lod", "lod1_to_lod2", F(h1),
                Lod2Distance + " м @FOV60 lodBias1 (LODUtility: " + F(d1) + " м); renderers=" + lod1.Count);
            report.Add("applied", "lod", "lod2_cull", "0",
                "без cull; сабмешей=" + (lod2.Count > 0 ? lod2[0].sharedMaterials.Length : 0));
        }

        private static float RelativeHeight(float worldSize, float distance)
        {
            float tanHalf = Mathf.Tan(CalibrationFov * 0.5f * Mathf.Deg2Rad);
            return Mathf.Clamp(worldSize / (2f * distance * tanHalf), 0.0001f, 0.999f);
        }

        private static float EditorLodDistance(LODGroup group, float relativeHeight)
        {
            var lu = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LODUtility");
            var m = lu?.GetMethod("CalculateDistance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (m == null) return float.NaN;
            var camGo = new GameObject("ALB_LodProbeCamera") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.enabled = false;
                cam.fieldOfView = CalibrationFov;
                return (float)m.Invoke(null, new object[] { cam, relativeHeight, group });
            }
            catch (Exception)
            {
                return float.NaN;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        // =====================================================================
        // Защита интерактива
        // =====================================================================

        /// <summary>
        /// Трансформы, которые нельзя терять: компоненты на узлах визуала (DoorController) и всё,
        /// на что ссылаются сериализованные поля любых скриптов префаба (hingePanel/rightHingePanel/
        /// doorModel/Pivot и т.п.). Предки узла визуала (корень, MainVisual) в набор НЕ попадают —
        /// иначе защищённым оказался бы весь визуал целиком.
        /// </summary>
        private static HashSet<Transform> CollectProtectedTransforms(GameObject root, Transform modelRoot)
        {
            var set = new HashSet<Transform>();
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                if (IsEligible(mb.transform, root.transform, modelRoot)) set.Add(mb.transform);

                var so = new SerializedObject(mb);
                var it = so.GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var o = it.objectReferenceValue;
                    Transform t = o is Component c ? c.transform : (o is GameObject g ? g.transform : null);
                    if (t != null && IsEligible(t, root.transform, modelRoot)) set.Add(t);
                }
            }
            return set;
        }

        private static bool IsEligible(Transform t, Transform root, Transform modelRoot)
        {
            if (t == null || t == root || t == modelRoot) return false;
            if (modelRoot.IsChildOf(t)) return false; // t — предок визуала (MainVisual/корень)
            return t.IsChildOf(root);
        }

        private static string ProtectReason(MeshRenderer r, HashSet<Transform> protectedTransforms)
        {
            if (!r.enabled) return "renderer выключен";
            if (!r.gameObject.activeInHierarchy) return "объект неактивен";
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return "нет меша";
            var mats = r.sharedMaterials;
            if (mats.Length != mf.sharedMesh.subMeshCount) return "число материалов ≠ сабмешей";
            foreach (var m in mats) if (m == null) return "пустой материал";
            if (r.transform.localToWorldMatrix.determinant < 0f) return "зеркальный трансформ";
            if (r.GetComponent<Collider>() != null) return "на узле есть коллайдер (створка)";
            for (var t = r.transform; t != null; t = t.parent)
                if (protectedTransforms.Contains(t)) return "интерактив/ссылка скрипта: " + t.name;
            return null;
        }

        // =====================================================================
        // Валидация
        // =====================================================================

        /// <summary>Инварианты мержа «Альбатроса». Пусто → состояние корректно.</summary>
        public static void ValidateMergedState(GameObject root, List<string> problems)
        {
            var modelRoot = FindModelRoot(root.transform);
            if (modelRoot == null) { problems.Add("нет узла визуала " + ModelRootName); return; }

            var mergeRoot = FindMergeRoot(root.transform);
            if (mergeRoot == null) { problems.Add("нет " + MergeRootName); return; }

            var group = mergeRoot.GetComponent<LODGroup>();
            if (group == null) { problems.Add("нет LODGroup на " + MergeRootName); return; }

            var lods = group.GetLODs();
            if (lods.Length != 3) problems.Add("LOD уровней: " + lods.Length + " вместо 3");
            else
            {
                float d0 = EditorLodDistance(group, lods[0].screenRelativeTransitionHeight);
                float d1 = EditorLodDistance(group, lods[1].screenRelativeTransitionHeight);
                if (!float.IsNaN(d0) && (Mathf.Abs(d0 - Lod1Distance) > 3f || Mathf.Abs(d1 - Lod2Distance) > 9f))
                    problems.Add("пороги LOD уехали: " + F(d0) + " / " + F(d1) + " м");
                if (lods[2].screenRelativeTransitionHeight != 0f) problems.Add("LOD2 с cull — ожидался 0");

                var lod2Renderers = lods[2].renderers;
                if (lod2Renderers.Length != 1) problems.Add("LOD2 renderers: " + lod2Renderers.Length + " вместо 1");
                else if (lod2Renderers[0] != null && lod2Renderers[0].sharedMaterials.Length > 5)
                    problems.Add("LOD2 сабмешей: " + lod2Renderers[0].sharedMaterials.Length + " > 5");

                foreach (var l in lods)
                    foreach (var r in l.renderers)
                    {
                        if (r == null) { problems.Add("пустая ссылка renderer в LODGroup (битый меш-ассет?)"); continue; }
                        var mf = r.GetComponent<MeshFilter>();
                        if (mf == null || mf.sharedMesh == null) problems.Add("нет меша у " + r.name);
                        foreach (var m in r.sharedMaterials)
                        {
                            if (m == null) problems.Add("пустой материал у " + r.name);
                            else if (m.shader != null && (m.shader.name == "Universal Render Pipeline/Lit" || m.shader.name == "Standard"))
                                problems.Add("слот URP/Lit или Standard у " + r.name + " (" + m.name + ")");
                        }
                    }
            }

            // Ни один слитый узел модели не должен снова получить MeshRenderer.
            var protectedTransforms = CollectProtectedTransforms(root, modelRoot);
            int survivor = 0, unexpected = 0;
            foreach (var r in modelRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (ProtectReason(r, protectedTransforms) != null) { survivor++; continue; }
                unexpected++;
                problems.Add("неслитый статичный renderer в модели: " + r.name);
            }
            if (survivor != ExpectProtectedRenderers)
                problems.Add("защищённых рендереров в модели: " + survivor + " вместо " + ExpectProtectedRenderers +
                             " (ручная правка дверей? обновить инвариант)");
            if (unexpected > 0) problems.Add("статики в модели осталось: " + unexpected);

            // Σtris склейки vs запись в отчёте (источник — плоская копия, FBX-сверки нет).
            long lod0Tris = SumTris(lods, 0);
            long lod1Tris = SumTris(lods, 1);
            long recorded = TryReadRecordedSourceTris();
            if (recorded > 0 && recorded != lod0Tris)
                problems.Add("Σtris LOD0 " + lod0Tris + " ≠ записи отчёта " + recorded + " (перезапустить мерж)");
            if (lod1Tris > lod0Tris)
                problems.Add("LOD1 тяжелее LOD0 по треугольникам: " + lod1Tris + " > " + lod0Tris);
        }

        private static long SumTris(LOD[] lods, int level)
        {
            if (lods.Length <= level) return 0;
            long n = 0;
            foreach (var r in lods[level].renderers)
            {
                if (r == null) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                n += CountTris(mf.sharedMesh);
            }
            return n;
        }

        private static void ValidateInteractive(GameObject root, List<string> problems)
        {
            Expect(problems, "DoorController", root.GetComponentsInChildren<DoorController>(true).Length, ExpectDoors);
            Expect(problems, "BoxCollider", root.GetComponentsInChildren<BoxCollider>(true).Length, ExpectBoxColliders);
            Expect(problems, "Collider", root.GetComponentsInChildren<Collider>(true).Length, ExpectColliders);
            Expect(problems, "ModuleSlot", root.GetComponentsInChildren<ModuleSlot>(true).Length, ExpectModuleSlots);

            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb == null) problems.Add("missing script");
            foreach (var dc in root.GetComponentsInChildren<DoorController>(true))
            {
                if (dc.GetComponent<Collider>() == null) problems.Add("у двери нет коллайдера: " + dc.name);
                if (dc.GetComponentInChildren<MeshRenderer>(true) == null) problems.Add("у двери снят визуал: " + dc.name);
            }
        }

        private static void Expect(List<string> problems, string what, int actual, int expected)
        {
            if (actual != expected) problems.Add(what + ": " + actual + " вместо " + expected);
        }

        // =====================================================================
        // Модель слотов / DC
        // =====================================================================

        private static void AddSlotModel(GameObject root, Report report, string phase)
        {
            int mr = root.GetComponentsInChildren<MeshRenderer>(true).Length;
            var group = root.GetComponentInChildren<LODGroup>(true);
            var inLod = new HashSet<Renderer>();
            LOD[] lods = group != null ? group.GetLODs() : new LOD[0];
            foreach (var l in lods) foreach (var r in l.renderers) if (r != null) inLod.Add(r);

            int always = 0, alwaysShadow = 0;
            var palette = new HashSet<string>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                    if (m != null) palette.Add(m.name);
                if (!r.enabled || inLod.Contains(r)) continue;
                int s = r.sharedMaterials.Length;
                always += s;
                if (r.shadowCastingMode != ShadowCastingMode.Off) alwaysShadow += s;
            }

            report.Add(phase, "model", "mesh_renderers", mr.ToString(CultureInfo.InvariantCulture),
                "вне LODGroup=" + (always > 0 ? "да" : "нет") + "; материалов в палитре=" + palette.Count);
            if (palette.Count > MaxPaletteMaterials)
                report.Add(phase, "model", "palette_over_budget", palette.Count.ToString(CultureInfo.InvariantCulture),
                    "лимит " + MaxPaletteMaterials + " (§5.2)");

            if (lods.Length == 0)
            {
                report.Add(phase, "model", "slots_always", always.ToString(CultureInfo.InvariantCulture),
                    "LODGroup нет; shadow_slots=" + alwaysShadow);
                return;
            }

            for (int i = 0; i < lods.Length; i++)
            {
                int s = 0, sh = 0;
                foreach (var r in lods[i].renderers)
                {
                    if (r == null) continue;
                    s += r.sharedMaterials.Length;
                    if (r.shadowCastingMode != ShadowCastingMode.Off) sh += r.sharedMaterials.Length;
                }
                int slots = always + s, shadow = alwaysShadow + sh;
                report.Add(phase, "model", "slots_LOD" + i, slots.ToString(CultureInfo.InvariantCulture),
                    "вне LODGroup=" + always + " + LOD" + i + "=" + s + "; shadow_slots=" + shadow +
                    "; dc_model(2×P+C)=" + (2 * slots + shadow));
            }
        }

        private static void ReportBudget(GameObject root, Report report, List<string> problems)
        {
            var budget = root.GetComponent<ShipShadowBudget>();
            if (budget == null)
            {
                report.Add("verify", "budget", "ShipShadowBudget", "MISSING", "класс L требует бюджет теней (§6.3 шаг 6)");
                problems.Add("нет ShipShadowBudget на корне префаба");
                return;
            }

            report.Add("verify", "budget", "ShipShadowBudget", "present",
                "enabled=" + budget.BudgetEnabled + "; on_m=" + F(budget.ShadowOnDistance) +
                "; off_m=" + F(budget.ShadowOffDistance) + "; coverWholeLodGroup=" + budget.CoverWholeLodGroup +
                "; maxAxis_m=" + F(budget.SmallPartMaxAxis));

            if (Mathf.Abs(budget.ShadowOffDistance - Lod1Distance) > 0.5f)
                problems.Add("shadowOffDistance " + F(budget.ShadowOffDistance) + " ≠ порогу LOD1 " + F(Lod1Distance) +
                             " м (тени должны гаснуть вместе с LOD0)");

            var group = root.GetComponentInChildren<LODGroup>(true);
            if (group == null) return;
            var list = new List<MeshRenderer>(256);
            ShipShadowBudget.CollectLodGroupRenderers(group, list);
            int slots = 0, casters = 0, off = 0;
            foreach (var r in list)
            {
                if (r == null) continue;
                slots += r.sharedMaterials.Length;
                if (r.shadowCastingMode != ShadowCastingMode.Off) casters += r.sharedMaterials.Length;
                else off++;
            }
            report.Add("verify", "budget", "covered", list.Count.ToString(CultureInfo.InvariantCulture),
                "slots=" + slots + "; caster_slots=" + casters + "; off_в_префабе=" + off +
                "; покрытие — только LODGroup (двери/слоты/PilotSeat вне его by design)");
        }

        // =====================================================================
        // Утилиты
        // =====================================================================

        private static Transform FindModelRoot(Transform root)
        {
            var direct = root.Find(MainVisualName + "/" + ModelRootName);
            if (direct != null) return direct;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == ModelRootName) return t;
            return null;
        }

        private static Transform FindMergeRoot(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == MergeRootName) return t;
            return null;
        }

        private static string PathOf(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static string Sanitize(string s)
        {
            var sb = new StringBuilder();
            foreach (char ch in s) sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
            return sb.ToString();
        }

        private static IEnumerable<string> Names(List<Material> mats)
        {
            foreach (var m in mats) yield return m.name;
        }

        private static string F(float f)
        {
            return f.ToString("F3", CultureInfo.InvariantCulture);
        }

        private static string AbsoluteProjectPath(string relative)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));
        }

        private static long TryReadRecordedSourceTris()
        {
            string path = AbsoluteProjectPath(ReportRelativePath);
            if (!File.Exists(path)) return -1;
            foreach (var line in File.ReadAllLines(path))
            {
                // phase,category,item,value,note
                if (!line.StartsWith("before,plan,merged_tris,", StringComparison.Ordinal)) continue;
                string rest = line.Substring("before,plan,merged_tris,".Length);
                int comma = rest.IndexOf(',');
                string value = comma > 0 ? rest.Substring(0, comma) : rest;
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v)) return v;
            }
            return -1;
        }

        // =====================================================================
        // CSV (строки фаз текущего запуска заменяются, остальные сохраняются)
        // =====================================================================

        private sealed class Report
        {
            private readonly List<string[]> _rows = new List<string[]>();
            public readonly List<string> Problems = new List<string>();

            public void Add(string phase, string category, string item, string value, string note)
            {
                _rows.Add(new[] { phase, category, item, value, note });
            }

            public void Write()
            {
                string path = AbsoluteProjectPath(ReportRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                const string header = "phase,category,item,value,note";

                var phases = new HashSet<string>();
                foreach (var r in _rows) phases.Add(r[0]);
                phases.Add("problem");

                var keep = new List<string>();
                if (File.Exists(path))
                {
                    foreach (var line in File.ReadAllLines(path))
                    {
                        if (line.Length == 0 || line.StartsWith(header, StringComparison.Ordinal)) continue;
                        int comma = line.IndexOf(',');
                        string ph = comma > 0 ? line.Substring(0, comma) : line;
                        if (!phases.Contains(ph)) keep.Add(line);
                    }
                }

                var sb = new StringBuilder();
                sb.AppendLine(header);
                foreach (var l in keep) sb.AppendLine(l);
                foreach (var r in _rows)
                    sb.AppendLine(Csv(r[0]) + "," + Csv(r[1]) + "," + Csv(r[2]) + "," + Csv(r[3]) + "," + Csv(r[4]));
                foreach (var p in Problems)
                    sb.AppendLine("problem,validation,," + Csv("FAIL") + "," + Csv(p));
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            }

            private static string Csv(string v)
            {
                if (string.IsNullOrEmpty(v)) return "";
                if (v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return v;
                return "\"" + v.Replace("\"", "\"\"") + "\"";
            }
        }
    }
}
