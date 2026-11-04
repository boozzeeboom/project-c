// Project C — пайплайн корабля «Лорейн» (класс Light, метрический класс S: 5.16 м).
//
// Отличие от «Альбатроса» (класс L): SHIPS.md §6.1 для полосы S требует «H1-аналог → мерж по материалу
// (1–3 меша) → 1 уровень», LODGroup НЕ нужен и ShipShadowBudget НЕ нужен. Поэтому здесь только:
//   §3 шаг 7  — материалы: палитра M_PC_* (процедурные Shader Graph), 0 слотов URP/Lit;
//   §4.1–4.2  — мерж статики по ключу (ячейка, материал, renderer-settings);
//   §5.2      — ≤24 материала на статик-визуал корабля, 1 сабмеш на меш.
//
// Матрица §4.2, что НИКОГДА не клеится: двери/створки, интерактив, коллайдеры, VFX, выключенные
// рендереры, зеркальные трансформы, рендереры без меша / с числом материалов != числу сабмешей.
// Дверь (`Door_Gangway`, `DoorController` + `BoxCollider`) мержится в ОТДЕЛЬНУЮ ячейку: сам узел створки
// остаётся корнем (hingePanel), его внутренняя геометрия склеивается по материалам — створка как целое
// по-прежнему едет одним трансформом, коллайдер и `DoorController` не трогаются.
//
// Инварианты зафиксированы 2026-11-04 после ручной правки пользователя (23 BoxCollider + 2 прочих,
// 1 DoorController, 19 узлов BC_*, 8 групп *_Colliders).
//
// Меню: ProjectC → Ships → Loreyn (Light) → …
// Бэкап: _local_backups/MAIN_PIPELINE/Loreyn/Лорейн.prefab.pre-merge.bak (вне Assets, не в git).
// Отчёт: docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/W_loreyn_merge_2026-11-04.csv
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectC.EditorTools
{
    public static class LoreynVisualMerger
    {
        // ------------------------------------------------------------------ цель
        private const string PrefabPath = "Assets/_Project/Prefabs/Ships/Лорейн.prefab";
        private const string MainVisualName = "MainVisual";
        private const string ModelRootName = "project-c_ship_light_prem_1_detail_1_ship";
        private const string DoorRootName = "Door_Gangway";

        /// <summary>Корень склеенного визуала (аналог SHIP_VISUAL_ALB / SHIP_VISUAL_H2).</summary>
        public const string MergeRootName = "SHIP_VISUAL_LOR";

        private const string MeshFolder = "Assets/_Project/Generated/Ships";
        private const string MeshPrefix = "Loreyn_VIS_";

        private const string BackupRelativePath =
            "_local_backups/MAIN_PIPELINE/Loreyn/Лорейн.prefab.pre-merge.bak";

        private const string ReportRelativePath =
            "docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/W_loreyn_merge_2026-11-04.csv";

        private const string CellHull = "Hull";
        private const string CellDoor = "Door";

        // ------------------------------------------------------------- материалы
        /// <summary>
        /// Исходный материал FBX → материал палитры (§5.1). Стекло получает ТОЛЬКО Glass,
        /// Lantern — только реальные лампы/навигационные огни (два стёкол-материала не смешиваются).
        /// </summary>
        private static readonly Dictionary<string, string> MaterialMap = new Dictionary<string, string>
        {
            { "Metal_Bronze_Polished", "M_PC_Bronze" },
            { "Metal_Brass",           "M_PC_Bronze_Brass" },
            { "Wood_Light_Lacquer",    "M_PC_Wood" },
            { "Leather_Light",         "M_PC_Fabric_Cream" },
            { "Carpet_Dark",           "M_PC_Fabric" },
            { "Lamp_Amber",            "M_PC_Lantern" },
            { "Lamp_White",            "M_PC_Lantern_Cool" },
            { "Glass_Green_Matte",     "M_PC_Glass_Green" },
            { "Nav_Red",               "M_PC_Lantern_Red" },
            { "Nav_Green",             "M_PC_Lantern_Green" },
        };

        /// <summary>Материалы палитры, допустимые на визуале этого корабля (§5.2 ≤24).</summary>
        private static readonly string[] PaletteWhitelist =
        {
            "M_PC_Bronze", "M_PC_Bronze_Brass", "M_PC_Wood", "M_PC_Fabric_Cream",
            "M_PC_Fabric", "M_PC_Lantern", "M_PC_Lantern_Cool", "M_PC_Glass_Green",
            "M_PC_Lantern_Red", "M_PC_Lantern_Green"
        };

        // ------------------------------------------------------------ инварианты
        private const int ExpectDoors = 1;
        private const int ExpectBoxColliders = 23;
        private const int ExpectColliders = 25;
        private const int ExpectColliderBoxNodes = 19;
        private const int ExpectColliderGroups = 8;
        private const int ExpectSlotNodes = 10;
        private const int MaxPaletteMaterials = 24;

        /// <summary>Ожидаемое число склеенных рендереров (10 корпус + 3 створка двери).</summary>
        private const int ExpectMergedRenderers = 13;

        // =====================================================================
        // Меню
        // =====================================================================

        [MenuItem("ProjectC/Ships/Loreyn (Light)/1. Materials (dry-run)", false, 190)]
        public static void MaterialsDryRun()
        {
            RunMaterials(false);
        }

        [MenuItem("ProjectC/Ships/Loreyn (Light)/2. Materials (apply)", false, 191)]
        public static void MaterialsApply()
        {
            RunMaterials(true);
        }

        [MenuItem("ProjectC/Ships/Loreyn (Light)/3. Merge Visual (dry-run)", false, 192)]
        public static void DryRun()
        {
            RunMerge(false);
        }

        [MenuItem("ProjectC/Ships/Loreyn (Light)/4. Merge Visual (apply)", false, 193)]
        public static void Apply()
        {
            RunMerge(true);
        }

        [MenuItem("ProjectC/Ships/Loreyn (Light)/5. Verify", false, 194)]
        public static void Verify()
        {
            var report = new Report();
            var problems = new List<string>();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (FindMergeRoot(root.transform) == null)
                {
                    problems.Add("SHIP_VISUAL_LOR отсутствует — мерж не выполнен");
                }
                else
                {
                    ValidateMergedState(root, problems);
                }
                WriteStateRow(root, report);
                report.Write();
            }
            catch (Exception e)
            {
                report.Add("failed", "exception", e.GetType().Name, "", e.Message);
                report.Write();
                Debug.LogError("[LOR] Verify failed: " + e);
                return;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            if (problems.Count == 0) Debug.Log("[LOR] Verify: OK, проблем нет → " + AbsoluteProjectPath(ReportRelativePath));
            else Debug.LogError("[LOR] Verify: НАЙДЕНО " + problems.Count + " проблем: " + string.Join(" | ", problems));
        }

        [MenuItem("ProjectC/Ships/Loreyn (Light)/9. Rollback (from backup)", false, 199)]
        public static void Rollback()
        {
            string backup = AbsoluteProjectPath(BackupRelativePath);
            if (!File.Exists(backup))
            {
                Debug.LogError("[LOR] Rollback: нет бэкапа " + backup);
                return;
            }
            File.Copy(backup, AbsoluteProjectPath(PrefabPath), true);
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            DeleteMeshAssets();
            Debug.Log("[LOR] Rollback: префаб восстановлен, Loreyn_VIS_* удалены");
        }

        private static void DeleteMeshAssets()
        {
            if (!AssetDatabase.IsValidFolder(MeshFolder)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { MeshFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path).StartsWith(MeshPrefix)) AssetDatabase.DeleteAsset(path);
            }
        }

        // =====================================================================
        // Шаг 1–2: материалы палитры
        // =====================================================================

        private static void RunMaterials(bool apply)
        {
            var report = new Report();
            var problems = new List<string>();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var modelRoot = FindModelRoot(root.transform);
                if (modelRoot == null)
                {
                    report.Add(apply ? "apply" : "dryrun", "materials", "model_root", "missing", ModelRootName);
                    report.Write();
                    Debug.LogError("[LOR] Модель не найдена: " + MainVisualName + "/" + ModelRootName);
                    return;
                }

                var palette = LoadPalette();
                var renderers = modelRoot.GetComponentsInChildren<MeshRenderer>(true);
                var perTarget = new Dictionary<string, int>();
                var unmapped = new HashSet<string>();

                var plan = new List<(MeshRenderer r, Material from, Material to)>();
                foreach (var r in renderers)
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var src = mats[i];
                        if (src == null) { problems.Add("пустой материал: " + PathOf(r.transform)); continue; }
                        if (!MaterialMap.TryGetValue(src.name, out string target))
                        {
                            if (!IsPalette(src)) unmapped.Add(src.name);
                            continue; // уже палитра или неизвестный — не трогаем
                        }
                        if (!palette.TryGetValue(target, out Material to))
                        {
                            problems.Add("нет материала палитры: " + target);
                            continue;
                        }
                        plan.Add((r, src, to));
                        perTarget.TryGetValue(target, out int n);
                        perTarget[target] = n + 1;
                    }
                }

                foreach (var kv in perTarget)
                    report.Add(apply ? "apply" : "dryrun", "materials", "target=" + kv.Key, kv.Value.ToString(), "");

                if (unmapped.Count > 0)
                    foreach (var u in unmapped) problems.Add("материал вне карты: " + u);

                int litSlots = 0;
                foreach (var r in renderers)
                    foreach (var m in r.sharedMaterials)
                        if (m != null && m.shader != null && m.shader.name.StartsWith("Universal Render Pipeline/Lit")) litSlots++;

                report.Add(apply ? "apply" : "dryrun", "materials", "urp_lit_slots_before", litSlots.ToString(), "");
                report.Add(apply ? "apply" : "dryrun", "materials", "assignments", plan.Count.ToString(), "");

                if (apply && plan.Count > 0)
                {
                    EnsureBackup();
                    foreach (var step in plan)
                    {
                        var mats = step.r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++)
                            if (mats[i] == step.from) mats[i] = step.to;
                        step.r.sharedMaterials = mats;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }

                report.Write();

                if (problems.Count == 0)
                    Debug.Log("[LOR] Materials " + (apply ? "apply" : "dry-run") + ": OK, назначений " + plan.Count +
                              ", URP/Lit слотов до = " + litSlots);
                else
                    Debug.LogError("[LOR] Materials: проблем " + problems.Count + ": " + string.Join(" | ", problems));
            }
            catch (Exception e)
            {
                report.Add("failed", "exception", e.GetType().Name, "", e.Message);
                report.Write();
                Debug.LogError("[LOR] Materials failed: " + e);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool IsPalette(Material m)
        {
            foreach (var p in PaletteWhitelist) if (m.name == p) return true;
            return false;
        }

        private static Dictionary<string, Material> LoadPalette()
        {
            var result = new Dictionary<string, Material>();
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project/Materials" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/ARCHIVE_NOTUSED/")) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) continue;
                if (!result.ContainsKey(m.name)) result[m.name] = m;
            }
            return result;
        }

        // =====================================================================
        // Шаги 3–4: мерж статики
        // =====================================================================

        private static void RunMerge(bool apply)
        {
            var report = new Report();
            var problems = new List<string>();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            var tempMeshes = new List<Mesh>();
            try
            {
                var existing = FindMergeRoot(root.transform);
                if (existing != null)
                {
                    report.Add(apply ? "apply" : "dryrun", "state", "already_merged", "yes", "SHIP_VISUAL_LOR уже есть");
                    ValidateMergedState(root, problems);
                    report.Write();
                    Debug.LogWarning("[LOR] Мерж уже выполнен; проблем: " + problems.Count);
                    return;
                }

                var modelRoot = FindModelRoot(root.transform);
                if (modelRoot == null)
                {
                    report.Add(apply ? "apply" : "dryrun", "merge", "model_root", "missing", ModelRootName);
                    report.Write();
                    Debug.LogError("[LOR] Модель не найдена");
                    return;
                }

                var doorPanel = FindDoorPanel(modelRoot);
                var mergeParent = modelRoot.parent != null ? modelRoot.parent : root.transform;
                var mergeRoot = CreateChild(mergeParent, MergeRootName);
                var plan = BuildPlan(modelRoot, doorPanel, mergeRoot.transform, report,
                    apply ? "apply" : "dryrun", problems);

                long srcTris = 0;
                Bounds srcBounds = default; bool hasBounds = false;
                foreach (var g in plan.Values)
                    foreach (var p in g.Parts)
                        srcTris += p.Mesh.triangles.Length / 3L;
                foreach (var r in modelRoot.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!hasBounds) { srcBounds = r.bounds; hasBounds = true; }
                    else srcBounds.Encapsulate(r.bounds);
                }

                long dstTris = 0;
                Bounds dstBounds = default; bool hasDst = false;
                foreach (var g in plan.Values)
                {
                    var mesh = CombineGroup(g, report, apply ? "apply" : "dryrun", problems);
                    if (mesh == null) continue;
                    dstTris += CountTris(mesh);
                    var meshWorldBounds = TransformBounds(g.Parent.localToWorldMatrix, mesh.bounds);
                    if (!hasDst) { dstBounds = meshWorldBounds; hasDst = true; }
                    else dstBounds.Encapsulate(meshWorldBounds);

                    if (apply)
                    {
                        string assetName = MeshPrefix + Sanitize(g.Cell) + "_" + Sanitize(g.Material.name);
                        string assetPath = MeshFolder + "/" + assetName + ".asset";
                        EnsureFolder(MeshFolder);
                        if (AssetDatabase.LoadAssetAtPath<Mesh>(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
                        AssetDatabase.CreateAsset(mesh, assetPath);
                        SpawnRenderer(g.Parent, assetName, mesh, g.Material, g.Settings);
                        report.Add("apply", "mesh", assetName, CountTris(mesh).ToString(), assetPath);
                    }
                    else
                    {
                        tempMeshes.Add(mesh);
                    }
                }

                report.Add(apply ? "apply" : "dryrun", "tris", "source", srcTris.ToString(), "");
                report.Add(apply ? "apply" : "dryrun", "tris", "merged", dstTris.ToString(), "");
                if (srcTris != dstTris)
                    problems.Add("Σtris источник " + srcTris + " != склейка " + dstTris);
                else
                    report.Add(apply ? "apply" : "dryrun", "tris", "delta", "0", "OK");

                if (hasBounds && hasDst)
                {
                    // Сравнение честное только "в одну сторону": `Renderer.bounds` — это AABB меша,
                    // развёрнутого трансформом (при поворотах он раздувается), а склейка даёт точный
                    // AABB по вершинам. Поэтому: склейка обязана лежать внутри исходного AABB,
                    // а сжатие допускается ≤2% габарита.
                    float tol = 0.05f;
                    bool inside =
                        dstBounds.min.x >= srcBounds.min.x - tol && dstBounds.min.y >= srcBounds.min.y - tol &&
                        dstBounds.min.z >= srcBounds.min.z - tol && dstBounds.max.x <= srcBounds.max.x + tol &&
                        dstBounds.max.y <= srcBounds.max.y + tol && dstBounds.max.z <= srcBounds.max.z + tol;

                    float sizeDev = Mathf.Max(
                        Mathf.Abs(srcBounds.size.x - dstBounds.size.x),
                        Mathf.Abs(srcBounds.size.y - dstBounds.size.y),
                        Mathf.Abs(srcBounds.size.z - dstBounds.size.z));
                    float worldSize = Mathf.Max(srcBounds.size.x, Mathf.Max(srcBounds.size.y, srcBounds.size.z));
                    float shrinkPct = worldSize > 0.001f ? sizeDev / worldSize * 100f : 0f;

                    report.Add(apply ? "apply" : "dryrun", "bounds", "source_size", F(srcBounds.size), "");
                    report.Add(apply ? "apply" : "dryrun", "bounds", "merged_size", F(dstBounds.size), "");
                    report.Add(apply ? "apply" : "dryrun", "bounds", "shrink_pct", F3(shrinkPct), "% от габарита");
                    report.Add(apply ? "apply" : "dryrun", "bounds", "merged_inside_source", inside ? "yes" : "no", "");

                    if (!inside)
                        problems.Add("склейка вышла за исходный AABB");
                    if (shrinkPct > 2f)
                        problems.Add("AABB сжатие " + F3(shrinkPct) + "% > 2%");
                }

                if (apply)
                {
                    EnsureBackup();
                    var merged = new HashSet<MeshRenderer>();
                    foreach (var g in plan.Values) foreach (var p in g.Parts) merged.Add(p.Renderer);
                    foreach (var r in merged)
                    {
                        var mf = r.GetComponent<MeshFilter>();
                        if (mf != null) UnityEngine.Object.DestroyImmediate(mf);
                        UnityEngine.Object.DestroyImmediate(r);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                    ValidateMergedState(root, problems);
                }
                else
                {
                    // dry-run ничего не оставляет в префабе
                    UnityEngine.Object.DestroyImmediate(mergeRoot.gameObject);
                }

                report.Write();

                if (problems.Count == 0)
                    Debug.Log("[LOR] Merge " + (apply ? "apply" : "dry-run") + ": OK, групп " + plan.Count +
                              ", Σtris " + srcTris + " = " + dstTris + " → " + AbsoluteProjectPath(ReportRelativePath));
                else
                    Debug.LogError("[LOR] Merge: проблем " + problems.Count + ": " + string.Join(" | ", problems));
            }
            catch (Exception e)
            {
                report.Add("failed", "exception", e.GetType().Name, "", e.Message);
                report.Write();
                Debug.LogError("[LOR] Merge failed: " + e);
            }
            finally
            {
                foreach (var m in tempMeshes)
                    if (m != null) UnityEngine.Object.DestroyImmediate(m);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private class Part
        {
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public Matrix4x4 ToMergeRoot;
        }

        private class Group
        {
            public string Cell;
            public Material Material;
            public RendererSettings Settings;
            /// <summary>Куда ставится склеенный меш: корпус — SHIP_VISUAL_LOR, створка — её подвижный узел.</summary>
            public Transform Parent;
            public readonly List<Part> Parts = new List<Part>();
        }

        private struct RendererSettings : IEquatable<RendererSettings>
        {
            public int Layer;
            public ShadowCastingMode Shadows;
            public bool ReceiveShadows;
            public LightProbeUsage LightProbes;
            public ReflectionProbeUsage ReflectionProbes;
            public MotionVectorGenerationMode MotionVectors;
            public uint RenderingLayerMask;

            public static RendererSettings From(MeshRenderer r)
            {
                return new RendererSettings
                {
                    Layer = r.gameObject.layer,
                    Shadows = r.shadowCastingMode,
                    ReceiveShadows = r.receiveShadows,
                    LightProbes = r.lightProbeUsage,
                    ReflectionProbes = r.reflectionProbeUsage,
                    MotionVectors = r.motionVectorGenerationMode,
                    RenderingLayerMask = r.renderingLayerMask,
                };
            }

            public string Key
            {
                get
                {
                    return Layer + "|" + Shadows + "|" + ReceiveShadows + "|" + LightProbes + "|" +
                           ReflectionProbes + "|" + MotionVectors + "|" + RenderingLayerMask;
                }
            }

            public bool Equals(RendererSettings o)
            {
                return Layer == o.Layer && Shadows == o.Shadows && ReceiveShadows == o.ReceiveShadows &&
                       LightProbes == o.LightProbes && ReflectionProbes == o.ReflectionProbes &&
                       MotionVectors == o.MotionVectors && RenderingLayerMask == o.RenderingLayerMask;
            }

            public override bool Equals(object obj) { return obj is RendererSettings o && Equals(o); }
            public override int GetHashCode() { return Key.GetHashCode(); }
        }

        private static Dictionary<string, Group> BuildPlan(Transform modelRoot, Transform doorPanel,
            Transform mergeRoot, Report report, string phase, List<string> problems)
        {
            var plan = new Dictionary<string, Group>();
            int total = 0, merged = 0, skipped = 0;

            foreach (var r in modelRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                total++;
                var mf = r.GetComponent<MeshFilter>();
                string reason = ProtectReason(r, mf, doorPanel);
                if (reason != null)
                {
                    skipped++;
                    report.Add(phase, "protected", PathOf(r.transform), reason, "");
                    continue;
                }

                bool inDoor = IsUnder(r.transform, doorPanel);
                string cell = inDoor ? CellDoor : CellHull;
                var mat = r.sharedMaterials[0];
                var settings = RendererSettings.From(r);
                string key = cell + "|" + AssetDatabase.GetAssetPath(mat) + "#" + mat.name + "|" + settings.Key;

                if (!plan.TryGetValue(key, out Group g))
                {
                    g = new Group
                    {
                        Cell = cell,
                        Material = mat,
                        Settings = settings,
                        Parent = inDoor ? doorPanel : mergeRoot,
                    };
                    plan[key] = g;
                }
                g.Parts.Add(new Part
                {
                    Renderer = r,
                    Mesh = mf.sharedMesh,
                    ToMergeRoot = modelRoot.worldToLocalMatrix * r.transform.localToWorldMatrix,
                });
                merged++;
            }

            report.Add(phase, "plan", "renderers_total", total.ToString(), "");
            report.Add(phase, "plan", "to_merge", merged.ToString(), "");
            report.Add(phase, "plan", "protected", skipped.ToString(), "");
            report.Add(phase, "plan", "groups", plan.Count.ToString(), "");

            if (plan.Count > MaxPaletteMaterials)
                problems.Add("групп мержа " + plan.Count + " > " + MaxPaletteMaterials);

            return plan;
        }

        /// <summary>Причина, по которой рендерер нельзя клеить (§4.2), либо null.</summary>
        private static string ProtectReason(MeshRenderer r, MeshFilter mf, Transform doorPanel)
        {
            if (r == null) return "renderer null";
            if (!r.enabled) return "renderer выключен";
            if (mf == null || mf.sharedMesh == null) return "нет меша";
            if (mf.sharedMesh.subMeshCount != 1) return "сабмешей " + mf.sharedMesh.subMeshCount;
            if (r.sharedMaterials.Length != 1 || r.sharedMaterials[0] == null) return "материалов " + r.sharedMaterials.Length;

            var s = r.transform.lossyScale;
            if (s.x * s.y * s.z < 0f) return "зеркальный трансформ";

            // Сам узел створки (hingePanel) клеится: его коллайдер, DoorController и ShipRootReference
            // остаются на нём, а визуал переезжает в склеенные дети — створка по-прежнему едет одним трансформом.
            if (r.transform == doorPanel) return null;

            // Коллайдер на самом меш-узле — физика, не клеим.
            if (r.GetComponent<Collider>() != null && !IsUnder(r.transform, doorPanel))
                return "коллайдер на узле";

            foreach (var c in r.GetComponents<Component>())
            {
                if (c == null) return "missing script";
                if (c is Transform || c is MeshFilter || c is MeshRenderer) continue;
                if (c is Collider && IsUnder(r.transform, doorPanel)) continue;
                return "компонент " + c.GetType().Name;
            }
            return null;
        }

        private static Mesh CombineGroup(Group g, Report report, string phase, List<string> problems)
        {
            var instances = new List<CombineInstance>(g.Parts.Count);
            long verts = 0;
            foreach (var p in g.Parts)
            {
                instances.Add(new CombineInstance
                {
                    mesh = p.Mesh,
                    subMeshIndex = 0,
                    transform = g.Parent.worldToLocalMatrix * p.Renderer.transform.localToWorldMatrix,
                });
                verts += p.Mesh.vertexCount;
            }

            var mesh = new Mesh { name = MeshPrefix + Sanitize(g.Cell) + "_" + Sanitize(g.Material.name) };
            if (verts > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.CombineMeshes(instances.ToArray(), true, true);
            mesh.RecalculateBounds();

            var normals = mesh.normals;
            if (normals.Length != mesh.vertexCount)
                problems.Add("нормали: " + normals.Length + " != " + mesh.vertexCount);

            report.Add(phase, "group", g.Cell + "/" + g.Material.name,
                "parts=" + g.Parts.Count + "; tris=" + CountTris(mesh) + "; verts=" + mesh.vertexCount, "");
            return mesh;
        }

        private static MeshRenderer SpawnRenderer(Transform parent, string name, Mesh mesh, Material mat, RendererSettings s)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.layer = s.Layer;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { mat };
            mr.shadowCastingMode = s.Shadows;
            mr.receiveShadows = s.ReceiveShadows;
            mr.lightProbeUsage = s.LightProbes;
            mr.reflectionProbeUsage = s.ReflectionProbes;
            mr.motionVectorGenerationMode = s.MotionVectors;
            mr.renderingLayerMask = s.RenderingLayerMask;
            return mr;
        }

        // =====================================================================
        // Проверка инвариантов
        // =====================================================================

        private static void ValidateMergedState(GameObject root, List<string> problems)
        {
            var modelRoot = FindModelRoot(root.transform);
            var mergeRoot = FindMergeRoot(root.transform);

            var mergedRenderers = new List<MeshRenderer>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                if (r.name.StartsWith(MeshPrefix)) mergedRenderers.Add(r);

            if (mergeRoot != null)
            {
                Expect(problems, "склеенных рендереров", mergedRenderers.Count, ExpectMergedRenderers);

                var mats = new HashSet<string>();
                foreach (var r in mergedRenderers)
                {
                    if (r.sharedMaterials.Length != 1 || r.sharedMaterials[0] == null)
                    {
                        problems.Add("склеенный рендерер без одного материала: " + r.name);
                        continue;
                    }
                    var m = r.sharedMaterials[0];
                    mats.Add(m.name);
                    if (!IsPalette(m)) problems.Add("материал вне палитры: " + m.name);
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) problems.Add("склеенный рендерер без меша: " + r.name);
                    else if (mf.sharedMesh.subMeshCount != 1)
                        problems.Add("сабмешей " + mf.sharedMesh.subMeshCount + " у " + r.name);
                }
                if (mats.Count > MaxPaletteMaterials)
                    problems.Add("материалов в палитре " + mats.Count + " > " + MaxPaletteMaterials);

                // Створка двери: склеенные меши обязаны быть её детьми, иначе не поедут с hingePanel
                var doorPanel = FindDoorPanel(modelRoot);
                if (doorPanel == null) problems.Add("узел створки (DoorController) не найден");
                else
                {
                    int doorMerged = 0;
                    foreach (var r in mergedRenderers) if (r.transform.IsChildOf(doorPanel)) doorMerged++;
                    if (doorMerged == 0) problems.Add("визуал створки не привязан к hingePanel");
                }
            }

            if (modelRoot != null)
            {
                int leftover = 0;
                foreach (var r in modelRoot.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.name.StartsWith(MeshPrefix)) continue;
                    if (ProtectReason(r, r.GetComponent<MeshFilter>(), FindDoorPanel(modelRoot)) == null)
                        leftover++;
                }
                Expect(problems, "несклеенных статик-рендереров", leftover, 0);
            }

            // Двери и коллайдеры — физика/геймплей, обязаны совпасть с авторскими
            int doors = 0, box = 0, colliders = 0, bcNodes = 0, groups = 0, slots = 0;
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null) { problems.Add("missing script найден"); continue; }
                var n = c.GetType().Name;
                if (n == "DoorController") doors++;
                if (c is BoxCollider) box++;
                if (c is Collider) colliders++;
            }
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("BC_")) bcNodes++;
                if (t.name.EndsWith("_Colliders")) groups++;
                if (t.name.StartsWith("Slot_")) slots++;
            }
            Expect(problems, "DoorController", doors, ExpectDoors);
            Expect(problems, "BoxCollider", box, ExpectBoxColliders);
            Expect(problems, "Collider всего", colliders, ExpectColliders);
            Expect(problems, "узлов BC_*", bcNodes, ExpectColliderBoxNodes);
            Expect(problems, "групп *_Colliders", groups, ExpectColliderGroups);
            Expect(problems, "узлов Slot_*", slots, ExpectSlotNodes);

            // Живые ссылки двери
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c.GetType().Name != "DoorController") continue;
                var t = c.GetType();
                foreach (var field in new[] { "hingePanel", "hingePivot" })
                {
                    var f = t.GetField(field, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f == null) continue;
                    var val = f.GetValue(c) as UnityEngine.Object;
                    if (val == null) problems.Add("DoorController." + field + " = null");
                }
            }

            // 0 слотов URP/Lit в склеенном визуале (интерактив вне модели — не в счёте, §5.1 разрешает URP/Lit одиночкам)
            int lit = 0;
            foreach (var r in mergedRenderers)
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.shader != null && m.shader.name.StartsWith("Universal Render Pipeline/Lit")) lit++;
            Expect(problems, "слотов URP/Lit в визуале", lit, 0);
        }

        private static void WriteStateRow(GameObject root, Report report)
        {
            var modelRoot = FindModelRoot(root.transform);
            int mrTotal = root.GetComponentsInChildren<MeshRenderer>(true).Length;
            int mrMerged = 0;
            long tris = 0;
            Bounds b = default; bool has = false;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.name.StartsWith(MeshPrefix)) continue;
                mrMerged++;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) tris += CountTris(mf.sharedMesh);
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            report.Add("state", "prefab", "mr_total", mrTotal.ToString(), "");
            report.Add("state", "prefab", "mr_merged", mrMerged.ToString(), "");
            report.Add("state", "prefab", "tris_merged", tris.ToString(), "");
            report.Add("state", "prefab", "bounds_merged", has ? F(b.size) : "n/a", "");
            report.Add("state", "prefab", "model_root", modelRoot != null ? modelRoot.name : "missing", "");
            report.Add("state", "prefab", "root_scale", root.transform.localScale.ToString("F3"), "");
            report.Add("state", "prefab", "file_size",
                new FileInfo(AbsoluteProjectPath(PrefabPath)).Length.ToString(), "");
        }

        private static void Expect(List<string> problems, string what, int actual, int expected)
        {
            if (actual != expected) problems.Add(what + ": " + actual + " вместо " + expected);
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

        private static Transform FindByName(Transform scope, string name)
        {
            foreach (var t in scope.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        /// <summary>Узел створки = hingePanel: тот, на котором висит DoorController.</summary>
        private static Transform FindDoorPanel(Transform modelRoot)
        {
            if (modelRoot == null) return null;
            foreach (var t in modelRoot.GetComponentsInChildren<Transform>(true))
                foreach (var c in t.GetComponents<Component>())
                    if (c != null && c.GetType().Name == "DoorController") return t;
            return FindByName(modelRoot, DoorRootName);
        }

        private static Bounds TransformBounds(Matrix4x4 m, Bounds b)
        {
            var result = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
            var ext = b.extents;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    ((i & 1) == 0 ? -ext.x : ext.x),
                    ((i & 2) == 0 ? -ext.y : ext.y),
                    ((i & 4) == 0 ? -ext.z : ext.z));
                result.Encapsulate(m.MultiplyPoint3x4(b.center + corner));
            }
            return result;
        }

        private static bool IsUnder(Transform t, Transform ancestor)
        {
            if (t == null || ancestor == null) return false;
            return t == ancestor || t.IsChildOf(ancestor);
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        private static long CountTris(Mesh m)
        {
            long n = 0;
            for (int i = 0; i < m.subMeshCount; i++) n += (long)m.GetIndexCount(i) / 3L;
            return n;
        }

        private static void EnsureBackup()
        {
            string backup = AbsoluteProjectPath(BackupRelativePath);
            if (File.Exists(backup)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            File.Copy(AbsoluteProjectPath(PrefabPath), backup, true);
        }

        private static string PathOf(Transform t)
        {
            var sb = new StringBuilder(t.name);
            var p = t.parent;
            while (p != null) { sb.Insert(0, p.name + "/"); p = p.parent; }
            return sb.ToString();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static string Sanitize(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.ToString();
        }

        private static string F(Vector3 v)
        {
            return v.x.ToString("F2", CultureInfo.InvariantCulture) + "x" +
                   v.y.ToString("F2", CultureInfo.InvariantCulture) + "x" +
                   v.z.ToString("F2", CultureInfo.InvariantCulture);
        }

        private static string F3(float f)
        {
            return f.ToString("F3", CultureInfo.InvariantCulture);
        }

        private static string AbsoluteProjectPath(string relative)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));
        }

        // =====================================================================
        // Отчёт
        // =====================================================================

        private class Report
        {
            private readonly List<string> _rows = new List<string>();

            public void Add(string phase, string category, string key, string value, string note)
            {
                _rows.Add(string.Join(",", new[]
                {
                    phase, category, Csv(key), Csv(value), Csv(note)
                }));
            }

            public void Write()
            {
                string path = AbsoluteProjectPath(ReportRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string header = "phase,category,key,value,note";
                if (!File.Exists(path)) File.WriteAllText(path, header + "\n");
                using (var w = new StreamWriter(path, true, new UTF8Encoding(false)))
                {
                    w.Write(string.Join("\n", _rows) + "\n");
                }
                AssetDatabase.Refresh();
            }

            private static string Csv(string s)
            {
                if (string.IsNullOrEmpty(s)) return "";
                if (s.IndexOfAny(new[] { ',', '"', '\n' }) < 0) return s;
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            }
        }
    }
}
