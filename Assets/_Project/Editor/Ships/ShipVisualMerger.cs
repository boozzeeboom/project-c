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
    /// <summary>
    /// ShipVisualMerger — T-CULL01 / Stage 3a (H2), пилот только для <c>Ship_havyII_base</c>.
    ///
    /// P1 — greeble-мерж: узлы модели <c>Bolt_*</c>, <c>Mess_*</c>, <c>UnderHull_*</c> (ячейка GrbFine)
    ///      и <c>Interior_*</c>, <c>Contour_*</c>, <c>Det_*</c> (ячейка GrbDetail) склеиваются по
    ///      (ячейка, ассет материала, настройки рендерера) в отдельные mesh-ассеты.
    ///      С исходных узлов FBX снимаются только MeshRenderer + MeshFilter (removed-component
    ///      overrides внутри prefab-ассета); сами узлы, их Transform и всё остальное остаются.
    /// P2 — LODGroup на корне <c>SHIP_VISUAL_H2</c>:
    ///      LOD0 — всё статичное после P1 (полный вид);
    ///      LOD1 — статика без Bolt/Mess/UnderHull, по одному мешу на материал;
    ///      LOD2 — один меш, 5 материалов (сабмеши), без Bolt/Mess/UnderHull; без cull
    ///      (решение пользователя: «2500 = LOD2 до конца»).
    ///      Пороги считаются от реального габарита корабля для 300 / 900 м при FOV 60 и lodBias 1.
    ///
    /// Что никогда не клеится и не входит в LODGroup (динамика/интерактив):
    ///   рендереры с DoorController и все створки, на которые ссылаются DoorController
    ///   (9 дверей, Ramp_Cargo, Door_AftGate_Stb), всё вне FBX-инстанса (PilotSeat, Slot_Engine ×2
    ///   с EngineThrusterVisual/ShipPartShake, CargoVisual, ContrailVFX, слоты), выключенные рендереры,
    ///   зеркальные (det &lt; 0) трансформы. Коллайдеры не трогаются.
    ///
    /// ShipContrailVfx при старте берёт размер самого большого MeshRenderer. Склеенные меши крупнее,
    /// поэтому значение, которое компонент вычислял до мержа, закрепляется в префабе
    /// (UseShipBounds=false, ManualBoundsSize=прежнее значение) — поведение следов не меняется.
    ///
    /// Floating origin: меши хранятся в локальных координатах корабля, мировых кэшей нет.
    /// Runtime CombineMeshes не используется — всё делается в редакторе и сохраняется в .asset.
    ///
    /// Меню: ProjectC → Ships → Heavy Visual Merge …
    /// Отчёт: docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/M_stage3_heavy_2026-10-31.csv
    /// Бэкап: _local_backups/T-CULL01/Stage3/Ship_havyII_base.prefab.bak (вне Assets).
    /// </summary>
    public static class ShipVisualMerger
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Ships/Ship_havyII_base.prefab";

        private const string FbxPath =
            "Assets/_Project/Art/Models/Ships/HeavyII_base/" +
            "project-c_ship_heavyII_1_cline_deepseek_1_glm-53_1_aura_1_game-ready_1.fbx";

        private const string MeshFolder = "Assets/_Project/Generated/Ships";
        private const string MeshPrefix = "HeavyII_VIS_";

        private const string ReportRelativePath =
            "docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/M_stage3_heavy_2026-10-31.csv";

        private const string BackupRelativePath =
            "_local_backups/T-CULL01/Stage3/Ship_havyII_base.prefab.bak";

        /// <summary>Корень визуала H2 под prefab-корнем (используется и в ShipPrefabRepair).</summary>
        public const string VisualRootName = "SHIP_VISUAL_H2";

        private const string Lod0RootName = "LOD0_Greeble";
        private const string Lod1RootName = "LOD1";
        private const string Lod2Name = "LOD2_All";

        private const string CellFine = "GrbFine";
        private const string CellDetail = "GrbDetail";

        private static readonly string[] FinePrefixes = { "Bolt_", "Mess_", "UnderHull_" };
        private static readonly string[] DetailPrefixes = { "Interior_", "Contour_", "Det_" };

        /// <summary>Пороги P2 (м) при FOV 60° и lodBias 1.</summary>
        private const float Lod1Distance = 300f;
        private const float Lod2Distance = 900f;
        private const float CalibrationFov = 60f;

        /// <summary>
        /// LOD2: 5 материалов. Ключ — имя материала FBX-корпуса, значение — имя целевого материала.
        /// Красный/зелёный навигационные огни сохраняются; белые линзы (в основном интерьер) уходят в стекло.
        /// </summary>
        private static readonly Dictionary<string, string> Lod2Family = new Dictionary<string, string>
        {
            { "Metal_Steel_Dark", "Metal_Steel_Dark" },
            { "Metal_Rust_Worn", "Metal_Steel_Dark" },
            { "Deck_Plating", "Metal_Steel_Dark" },
            { "Metal_Bronze_Dark", "Metal_Bronze_Dark" },
            { "Metal_Bronze", "Metal_Bronze_Dark" },
            { "Metal_Brass", "Metal_Bronze_Dark" },
            { "Glass_Green_Matte", "Glass_Green_Matte" },
            { "Lamp_White", "Glass_Green_Matte" },
            { "Lamp_Red", "Lamp_Red" },
            { "Lamp_Green", "Lamp_Green" },
        };

        private static readonly string[] Lod2Order =
            { "Metal_Steel_Dark", "Metal_Bronze_Dark", "Glass_Green_Matte", "Lamp_Red", "Lamp_Green" };

        // =====================================================================
        // Меню
        // =====================================================================

        [MenuItem("ProjectC/Ships/Heavy Visual Merge (dry-run)", false, 130)]
        public static void DryRun()
        {
            var report = new Report();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (FindChild(root.transform, VisualRootName) != null)
                {
                    report.Add("dryrun", "state", "already_applied", "yes", "H2 уже применён; см. Verify/Rollback");
                    ValidateMergedState(root, report.Problems);
                }
                else
                {
                    var plan = BuildPlan(root, report, "dryrun");
                    BuildMeshes(root, plan, report, "dryrun", inMemoryOnly: true);
                    plan.DisposeMeshes();
                }
                report.Write();
                Debug.Log("[H2] Dry-run OK → " + AbsoluteProjectPath(ReportRelativePath));
            }
            catch (Exception e)
            {
                Debug.LogError("[H2] Dry-run failed: " + e);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("ProjectC/Ships/Heavy Visual Merge (apply P1+P2)", false, 131)]
        public static void Apply()
        {
            var report = new Report();
            var createdAssets = new List<string>();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            bool saved = false;
            Plan plan = null;
            try
            {
                if (FindChild(root.transform, VisualRootName) != null)
                    throw new InvalidOperationException("H2 уже применён (есть " + VisualRootName + "). Сначала Rollback.");

                plan = BuildPlan(root, report, "before");
                BuildMeshes(root, plan, report, "applied", inMemoryOnly: false);

                // --- изменения иерархии в памяти ---
                var visRoot = CreateChild(root.transform, VisualRootName);
                var lod0Root = CreateChild(visRoot, Lod0RootName);
                var lod1Root = CreateChild(visRoot, Lod1RootName);

                var lod0 = new List<Renderer>(plan.StaticKeep);
                foreach (var g in plan.P1Groups) lod0.Add(SpawnRenderer(lod0Root, g.ObjectName, g.Mesh, new[] { g.Material }, g.Settings));
                var lod1 = new List<Renderer>();
                foreach (var g in plan.Lod1Groups) lod1.Add(SpawnRenderer(lod1Root, g.ObjectName, g.Mesh, new[] { g.Material }, g.Settings));
                var lod2 = new List<Renderer>
                {
                    SpawnRenderer(visRoot, Lod2Name, plan.Lod2Mesh, plan.Lod2Materials, plan.Lod2Settings)
                };

                // Снять визуал с greeble-узлов модели (узлы и трансформы остаются).
                foreach (var r in plan.Greeble)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    UnityEngine.Object.DestroyImmediate(r);
                    if (mf != null) UnityEngine.Object.DestroyImmediate(mf);
                }

                ConfigureLodGroup(visRoot.gameObject, lod0, lod1, lod2, report);
                PinContrail(root, plan, report);

                // --- валидация в памяти ---
                var problems = new List<string>();
                ValidateMergedState(root, problems);
                ValidateInteractive(root, problems);
                if (problems.Count > 0)
                    throw new InvalidOperationException("Валидация не прошла, префаб НЕ сохранён: " + string.Join(" | ", problems));

                // --- бэкап, ассеты, префаб ---
                string backup = AbsoluteProjectPath(BackupRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                File.Copy(AbsoluteProjectPath(PrefabPath), backup, true);
                report.Add("applied", "backup", BackupRelativePath, "ok", "");

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
                Debug.Log("[H2] Apply OK. Mesh-ассетов: " + createdAssets.Count + ". Отчёт: " +
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
                Debug.LogError("[H2] Apply failed" + (saved ? " (префаб сохранён — см. Rollback)" : " (префаб не сохранён)") + ": " + e);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                if (saved) AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            }
        }

        [MenuItem("ProjectC/Ships/Verify Heavy Visual Merge (FBX reimport)", false, 132)]
        public static void VerifyAfterReimport()
        {
            var report = new Report();
            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var problems = report.Problems;
                ValidateMergedState(root, problems);
                ValidateInteractive(root, problems);
                AddSlotModel(root, report, "after_reimport");
                report.Add("after_reimport", "validation", "", problems.Count == 0 ? "OK" : "FAIL", string.Join(" | ", problems));
                report.Write();
                if (problems.Count == 0) Debug.Log("[H2] Reimport OK. Отчёт: " + AbsoluteProjectPath(ReportRelativePath));
                else foreach (var p in problems) Debug.LogError("[H2] Reimport: " + p);
            }
            catch (Exception e)
            {
                Debug.LogError("[H2] Verify failed: " + e);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("ProjectC/Ships/Rollback Heavy Visual Merge (from backup)", false, 150)]
        public static void RollbackFromBackup()
        {
            string backup = AbsoluteProjectPath(BackupRelativePath);
            if (!File.Exists(backup)) { Debug.LogError("[H2] Бэкап не найден: " + backup); return; }
            if (!EditorUtility.DisplayDialog("H2 rollback",
                    "Восстановить Ship_havyII_base.prefab из бэкапа и удалить меши " + MeshPrefix + "*?\n" + backup,
                    "Восстановить", "Отмена"))
                return;

            File.Copy(backup, AbsoluteProjectPath(PrefabPath), true);
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            foreach (var guid in AssetDatabase.FindAssets(MeshPrefix + " t:Mesh", new[] { MeshFolder }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(p).StartsWith(MeshPrefix, StringComparison.Ordinal)) AssetDatabase.DeleteAsset(p);
            }
            Debug.Log("[H2] Префаб восстановлен из бэкапа, меши H2 удалены.");
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
            public Transform FbxRoot;
            public readonly List<MeshRenderer> Greeble = new List<MeshRenderer>();
            public readonly List<MeshRenderer> StaticKeep = new List<MeshRenderer>();     // LOD0 без мержа
            public readonly List<MeshRenderer> Protected = new List<MeshRenderer>();      // вне LODGroup
            public readonly List<Group> P1Groups = new List<Group>();
            public readonly List<Group> Lod1Groups = new List<Group>();
            public Mesh Lod2Mesh;
            public Material[] Lod2Materials;
            public RendererSettings Lod2Settings;
            public long Lod2SourceTris;
            public Vector3 ContrailSizeBefore;
            public bool ContrailFound;

            public IEnumerable<Mesh> AllMeshes()
            {
                foreach (var g in P1Groups) if (g.Mesh != null) yield return g.Mesh;
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
            var plan = new Plan { FbxRoot = FindNestedInstanceBySource(root.transform, FbxPath) };
            if (plan.FbxRoot == null) throw new InvalidOperationException("Не найден nested FBX-инстанс: " + FbxPath);

            AddSlotModel(root, report, phase);
            plan.ContrailFound = TryComputeContrailSize(root, out plan.ContrailSizeBefore);

            var protectedTransforms = CollectProtectedTransforms(root, plan.FbxRoot);

            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!IsSelfOrDescendant(r.transform, plan.FbxRoot)) { plan.Protected.Add(r); continue; }
                string why = ProtectReason(r, protectedTransforms);
                if (why != null)
                {
                    plan.Protected.Add(r);
                    report.Add(phase, "protected", PathOf(r.transform), why, "");
                    continue;
                }
                if (CellOf(r.name) != null) plan.Greeble.Add(r);
                else plan.StaticKeep.Add(r);
            }

            // P1: (ячейка, материал, настройки).
            var p1 = new Dictionary<string, Group>();
            foreach (var r in plan.Greeble) AddToGroups(p1, CellOf(r.name), r);
            plan.P1Groups.AddRange(SortedGroups(p1));

            // LOD1: статика + GrbDetail (без GrbFine), по материалу.
            var l1 = new Dictionary<string, Group>();
            foreach (var r in plan.StaticKeep) AddToGroups(l1, "LOD1", r);
            foreach (var r in plan.Greeble) if (CellOf(r.name) == CellDetail) AddToGroups(l1, "LOD1", r);
            plan.Lod1Groups.AddRange(SortedGroups(l1));

            int greebleFine = 0;
            foreach (var r in plan.Greeble) if (CellOf(r.name) == CellFine) greebleFine++;
            report.Add(phase, "plan", "fbx_renderers", (plan.Greeble.Count + plan.StaticKeep.Count +
                CountProtectedInside(plan)).ToString(CultureInfo.InvariantCulture), "");
            report.Add(phase, "plan", "greeble_sources", plan.Greeble.Count.ToString(CultureInfo.InvariantCulture),
                "GrbFine=" + greebleFine + " GrbDetail=" + (plan.Greeble.Count - greebleFine));
            report.Add(phase, "plan", "static_unmerged", plan.StaticKeep.Count.ToString(CultureInfo.InvariantCulture), "");
            report.Add(phase, "plan", "protected_total", plan.Protected.Count.ToString(CultureInfo.InvariantCulture),
                "двери/створки, PilotSeat, Slot_Engine, корневой dummy");
            report.Add(phase, "plan", "p1_groups", plan.P1Groups.Count.ToString(CultureInfo.InvariantCulture), "");
            report.Add(phase, "plan", "lod1_groups", plan.Lod1Groups.Count.ToString(CultureInfo.InvariantCulture), "");
            report.Add(phase, "plan", "contrail_size_before",
                plan.ContrailFound ? V(plan.ContrailSizeBefore) : "n/a", "значение, которое ShipContrailVfx вычислял до мержа");
            return plan;
        }

        private static int CountProtectedInside(Plan plan)
        {
            int n = 0;
            foreach (var r in plan.Protected) if (IsSelfOrDescendant(r.transform, plan.FbxRoot)) n++;
            return n;
        }

        private static void AddToGroups(Dictionary<string, Group> groups, string cell, MeshRenderer r)
        {
            var mesh = r.GetComponent<MeshFilter>().sharedMesh;
            var mats = r.sharedMaterials;
            var settings = RendererSettings.Of(r);
            var touched = new HashSet<Group>();
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                // Группировка по ассету материала (путь + имя саб-ассета), не по одному имени.
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

        private static List<Group> SortedGroups(Dictionary<string, Group> groups)
        {
            var list = new List<Group>(groups.Values);
            list.Sort((a, b) =>
            {
                int c = string.CompareOrdinal(a.Cell, b.Cell);
                return c != 0 ? c : string.CompareOrdinal(a.Material.name, b.Material.name);
            });

            // Уникальные имена: дубли имён материалов/настроек получают суффикс.
            var used = new HashSet<string>();
            foreach (var g in list)
            {
                string baseName = MeshPrefix + g.Cell + "_" + Sanitize(g.Material.name);
                string name = baseName;
                for (int i = 2; used.Contains(name); i++) name = baseName + "_" + i;
                used.Add(name);
                g.ObjectName = name;
            }
            return list;
        }

        // =====================================================================
        // Сборка мешей
        // =====================================================================

        private static void BuildMeshes(GameObject root, Plan plan, Report report, string phase, bool inMemoryOnly)
        {
            // Меши живут в локальном пространстве SHIP_VISUAL_H2 = локальное пространство prefab-корня.
            Matrix4x4 toLocal = root.transform.worldToLocalMatrix;

            foreach (var g in plan.P1Groups) g.Mesh = CombineGroup(g, toLocal, report, phase);
            foreach (var g in plan.Lod1Groups) g.Mesh = CombineGroup(g, toLocal, report, phase);

            // LOD2: сначала семьи материалов (mergeSubMeshes=true), затем семьи → сабмеши (mergeSubMeshes=false).
            var familyMeshes = new List<CombineInstance>();
            var familyMats = new List<Material>();
            var temps = new List<Mesh>();
            long lod2Tris = 0;
            foreach (string family in Lod2Order)
            {
                var parts = new List<CombineInstance>();
                Material target = null;
                foreach (var g in plan.Lod1Groups)
                {
                    if (!Lod2Family.TryGetValue(g.Material.name, out var fam))
                        throw new InvalidOperationException("Материал без LOD2-семьи: " + g.Material.name);
                    if (fam != family) continue;
                    parts.Add(new CombineInstance { mesh = g.Mesh, subMeshIndex = 0, transform = Matrix4x4.identity });
                    lod2Tris += g.SourceTris;
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
            var lod2 = NewMesh(MeshPrefix + "LOD2_All", CountVerts(familyMeshes));
            lod2.CombineMeshes(familyMeshes.ToArray(), false, false, false);
            lod2.RecalculateBounds();
            foreach (var t in temps) UnityEngine.Object.DestroyImmediate(t);
            plan.Lod2Mesh = lod2;
            plan.Lod2Materials = familyMats.ToArray();
            plan.Lod2Settings = plan.Lod1Groups[0].Settings;
            plan.Lod2SourceTris = lod2Tris;

            long lod2Out = CountTris(lod2);
            CheckTris("LOD2_All", lod2Tris, lod2Out);
            report.Add(phase, "mesh", lod2.name, lod2Out.ToString(CultureInfo.InvariantCulture),
                "src_tris=" + lod2Tris + " verts=" + lod2.vertexCount + " submeshes=" + lod2.subMeshCount +
                " idx=" + lod2.indexFormat + " mats=" + string.Join("/", Names(familyMats)));

            // Σtris по P1.
            long p1Src = 0, p1Out = 0;
            foreach (var g in plan.P1Groups) { p1Src += g.SourceTris; p1Out += CountTris(g.Mesh); }
            CheckTris("P1 total", p1Src, p1Out);
            report.Add(phase, "tris", "p1_greeble", p1Out.ToString(CultureInfo.InvariantCulture), "src=" + p1Src);

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
                "cell=" + g.Cell + " mat=" + g.Material.name + " (" + AssetDatabase.GetAssetPath(g.Material) + ")" +
                " sources=" + g.Sources + " src_tris=" + g.SourceTris + " verts=" + mesh.vertexCount +
                " idx=" + mesh.indexFormat);
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
            foreach (var r in plan.Greeble) { if (!has) { src = r.bounds; has = true; } else src.Encapsulate(r.bounds); }
            Bounds dst = default; bool hasDst = false;
            var l2w = root.transform.localToWorldMatrix;
            foreach (var g in plan.P1Groups)
            {
                var b = TransformBounds(l2w, g.Mesh.bounds);
                if (!hasDst) { dst = b; hasDst = true; } else dst.Encapsulate(b);
            }
            float d = Mathf.Max((src.min - dst.min).magnitude, (src.max - dst.max).magnitude);
            if (d > 0.05f)
                throw new InvalidOperationException("Мировой AABB greeble-мержа расходится на " + d.ToString("F3", CultureInfo.InvariantCulture) + " м");
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
        // Префаб: объекты, LODGroup, Contrail
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

        private static void ConfigureLodGroup(GameObject host, List<Renderer> lod0, List<Renderer> lod1, List<Renderer> lod2, Report report)
        {
            var group = host.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.None;
            group.animateCrossFading = false;

            Bounds wb = default; bool has = false;
            foreach (var r in lod0) { if (!has) { wb = r.bounds; has = true; } else wb.Encapsulate(r.bounds); }

            // LODGroup.size — в локальных единицах; мировой размер = size × max|lossyScale|.
            // ВАЖНО: SetLODs пересчитывает bounds (для неравномерного масштаба корня 6×0.9×12
            // это завышает размер в 1.5 раза), поэтому size/точка отсчёта задаются ПОСЛЕ SetLODs.
            float worldSize = Mathf.Max(wb.size.x, Mathf.Max(wb.size.y, wb.size.z));
            var ls = host.transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(ls.x), Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z)));

            float h0 = RelativeHeight(worldSize, Lod1Distance);
            float h1 = RelativeHeight(worldSize, Lod2Distance);
            group.SetLODs(new[]
            {
                new LOD(h0, lod0.ToArray()),
                new LOD(h1, lod1.ToArray()),
                new LOD(0f, lod2.ToArray()), // без cull: LOD2 до far clip
            });
            group.localReferencePoint = host.transform.InverseTransformPoint(wb.center);
            group.size = worldSize / maxScale;

            float d0 = EditorLodDistance(group, h0), d1 = EditorLodDistance(group, h1);
            if (!float.IsNaN(d0) && (Mathf.Abs(d0 - Lod1Distance) > 3f || Mathf.Abs(d1 - Lod2Distance) > 9f))
                throw new InvalidOperationException("Пороги LOD не совпали: " + F(d0) + " / " + F(d1) + " м");

            report.Add("applied", "lod", "world_size_m", F(worldSize), "size(local)=" + F(group.size));
            report.Add("applied", "lod", "lod0_to_lod1", F(h0), Lod1Distance + " м @FOV60 lodBias1 (LODUtility: " + F(d0) + " м); renderers=" + lod0.Count);
            report.Add("applied", "lod", "lod1_to_lod2", F(h1), Lod2Distance + " м @FOV60 lodBias1 (LODUtility: " + F(d1) + " м); renderers=" + lod1.Count);
            report.Add("applied", "lod", "lod2_cull", "0", "без cull (решение пользователя)");
            string[] q = QualitySettings.names;
            var sb = new StringBuilder();
            int cur = QualitySettings.GetQualityLevel();
            sb.Append("active=" + q[cur] + " lodBias=" + F(QualitySettings.lodBias) + "; ");
            sb.Append("эффективные пороги = 300/900 м × lodBias");
            report.Add("applied", "lod", "lod_bias_note", F(QualitySettings.lodBias), sb.ToString());
        }

        private static float RelativeHeight(float worldSize, float distance)
        {
            float tanHalf = Mathf.Tan(CalibrationFov * 0.5f * Mathf.Deg2Rad);
            return Mathf.Clamp(worldSize / (2f * distance * tanHalf), 0.0001f, 0.999f);
        }

        /// <summary>
        /// Дистанция перехода по данным самого Unity (internal UnityEditor.LODUtility.CalculateDistance)
        /// для временной камеры FOV 60. NaN — если API недоступен.
        /// </summary>
        private static float EditorLodDistance(LODGroup group, float relativeHeight)
        {
            var lu = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LODUtility");
            var m = lu?.GetMethod("CalculateDistance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (m == null) return float.NaN;
            var camGo = new GameObject("H2_LodProbeCamera") { hideFlags = HideFlags.HideAndDontSave };
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

        private static void PinContrail(GameObject root, Plan plan, Report report)
        {
            var c = root.GetComponentInChildren<ShipContrailVfx>(true);
            if (c == null) { report.Add("applied", "contrail", "", "n/a", "ShipContrailVfx не найден"); return; }
            if (!c.UseShipBounds)
            {
                report.Add("applied", "contrail", PathOf(c.transform), "kept", "UseShipBounds уже false — не меняется");
                return;
            }
            if (!plan.ContrailFound) throw new InvalidOperationException("Не удалось вычислить прежний размер Contrail");
            c.UseShipBounds = false;
            c.ManualBoundsSize = plan.ContrailSizeBefore;
            report.Add("applied", "contrail", PathOf(c.transform), V(plan.ContrailSizeBefore),
                "UseShipBounds=false, ManualBoundsSize = значение до мержа (поведение не меняется)");
        }

        /// <summary>Повтор ShipContrailVfx.GetShipVisualSize (самый большой enabled MeshRenderer).</summary>
        private static bool TryComputeContrailSize(GameObject root, out Vector3 size)
        {
            size = Vector3.zero;
            float best = 0f;
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!mr.enabled) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var sz = mf.sharedMesh.bounds.size;
                var ls = mr.transform.lossyScale;
                sz = new Vector3(sz.x * ls.x, sz.y * ls.y, sz.z * ls.z);
                float vol = sz.x * sz.y * sz.z;
                if (vol > best) { best = vol; size = sz; }
            }
            return best > 0f;
        }

        // =====================================================================
        // Защита интерактива
        // =====================================================================

        private static HashSet<Transform> CollectProtectedTransforms(GameObject root, Transform fbxRoot)
        {
            var set = new HashSet<Transform>();
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                if (mb.transform != root.transform && mb.transform != fbxRoot) set.Add(mb.transform);

                var so = new SerializedObject(mb);
                var it = so.GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var o = it.objectReferenceValue;
                    Transform t = o is Component c ? c.transform : (o is GameObject g ? g.transform : null);
                    if (t == null || t == root.transform || t == fbxRoot) continue;
                    if (!t.IsChildOf(root.transform)) continue;
                    set.Add(t);
                }
            }
            return set;
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

        private static string CellOf(string name)
        {
            foreach (var p in FinePrefixes) if (name.StartsWith(p, StringComparison.Ordinal)) return CellFine;
            foreach (var p in DetailPrefixes) if (name.StartsWith(p, StringComparison.Ordinal)) return CellDetail;
            return null;
        }

        // =====================================================================
        // Валидация (используется и ShipPrefabRepair после реимпорта)
        // =====================================================================

        /// <summary>Инварианты H2. Пусто → состояние корректно.</summary>
        public static void ValidateMergedState(GameObject root, List<string> problems)
        {
            var fbxRoot = FindNestedInstanceBySource(root.transform, FbxPath);
            var vis = FindChild(root.transform, VisualRootName);
            if (vis == null) { problems.Add("нет " + VisualRootName); return; }
            if (fbxRoot == null) { problems.Add("нет FBX-инстанса"); return; }

            var group = vis.GetComponent<LODGroup>();
            if (group == null) { problems.Add("нет LODGroup на " + VisualRootName); return; }
            var lods = group.GetLODs();
            if (lods.Length != 3) problems.Add("LOD уровней: " + lods.Length + " вместо 3");
            else
            {
                float d0 = EditorLodDistance(group, lods[0].screenRelativeTransitionHeight);
                float d1 = EditorLodDistance(group, lods[1].screenRelativeTransitionHeight);
                if (!float.IsNaN(d0) && (Mathf.Abs(d0 - Lod1Distance) > 3f || Mathf.Abs(d1 - Lod2Distance) > 9f))
                    problems.Add("пороги LOD уехали (пересчитаны bounds?): " + F(d0) + " / " + F(d1) + " м");
                if (lods[2].screenRelativeTransitionHeight != 0f) problems.Add("LOD2 с cull — ожидался 0");
            }

            var inLod0 = new HashSet<Renderer>();
            for (int i = 0; i < lods.Length; i++)
            {
                foreach (var r in lods[i].renderers)
                {
                    if (r == null) { problems.Add("LOD" + i + ": пустой renderer (битая ссылка после реимпорта?)"); continue; }
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) problems.Add("LOD" + i + ": нет меша у " + r.name);
                    if (i == 0) inLod0.Add(r);
                }
            }

            var protectedTransforms = CollectProtectedTransforms(root, fbxRoot);
            long srcTris = 0;
            foreach (var t in fbxRoot.GetComponentsInChildren<Transform>(true))
            {
                if (CellOf(t.name) == null) continue;
                if (t.GetComponent<MeshRenderer>() != null)
                    problems.Add("greeble-узел снова с MeshRenderer (новый узел FBX?): " + t.name);
                var src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                var srcMf = src != null ? src.GetComponent<MeshFilter>() : null;
                if (srcMf != null && srcMf.sharedMesh != null) srcTris += CountTris(srcMf.sharedMesh);
            }

            foreach (var r in fbxRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (ProtectReason(r, protectedTransforms) != null) continue;
                if (!inLod0.Contains(r)) problems.Add("статичный renderer вне LOD0: " + r.name);
            }

            var lod0Root = FindChild(vis, Lod0RootName);
            long mergedTris = 0;
            if (lod0Root != null)
                foreach (var mf in lod0Root.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null) mergedTris += CountTris(mf.sharedMesh);
            if (srcTris != mergedTris)
                problems.Add("Σtris greeble: FBX " + srcTris + " ≠ склейка " + mergedTris + " (перезапустить мерж)");

            var contrail = root.GetComponentInChildren<ShipContrailVfx>(true);
            if (contrail != null && contrail.UseShipBounds)
                problems.Add("ShipContrailVfx.UseShipBounds=true — размер следов изменится из-за склеенных мешей");
        }

        private static void ValidateInteractive(GameObject root, List<string> problems)
        {
            Expect(problems, "DoorController", root.GetComponentsInChildren<DoorController>(true).Length, 9);
            Expect(problems, "BoxCollider", root.GetComponentsInChildren<BoxCollider>(true).Length, 250);
            Expect(problems, "ModuleSlot", root.GetComponentsInChildren<ModuleSlot>(true).Length, 11);
            Expect(problems, "PilotSeatController", root.GetComponentsInChildren<PilotSeatController>(true).Length, 1);
            Expect(problems, "ShipCargoVisual", root.GetComponentsInChildren<ProjectC.Ship.Cargo.ShipCargoVisual>(true).Length, 1);
            Expect(problems, "EngineThrusterVisual", root.GetComponentsInChildren<ProjectC.Ship.Engine.EngineThrusterVisual>(true).Length, 2);
            Expect(problems, "ShipPartShake", root.GetComponentsInChildren<ShipPartShake>(true).Length, 2);
            Expect(problems, "ShipContrailVfx", root.GetComponentsInChildren<ShipContrailVfx>(true).Length, 1);
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb == null) problems.Add("missing script");
            foreach (var dc in root.GetComponentsInChildren<DoorController>(true))
            {
                if (dc.GetComponent<Collider>() == null) problems.Add("у двери нет коллайдера: " + dc.name);
                if (dc.GetComponent<MeshRenderer>() == null) problems.Add("у двери снят визуал: " + dc.name);
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
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!r.enabled || inLod.Contains(r)) continue;
                int s = r.sharedMaterials.Length;
                always += s;
                if (r.shadowCastingMode != ShadowCastingMode.Off) alwaysShadow += s;
            }
            report.Add(phase, "model", "mesh_renderers", mr.ToString(CultureInfo.InvariantCulture), "");
            if (lods.Length == 0)
            {
                report.Add(phase, "model", "slots", always.ToString(CultureInfo.InvariantCulture),
                    "shadow_slots=" + alwaysShadow + "; dc_upper(1+2 каскада)=" + (always + 2 * alwaysShadow));
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
                    "; dc_upper(1+2 каскада)=" + (slots + 2 * shadow));
            }
        }

        // =====================================================================
        // Утилиты
        // =====================================================================

        private static Transform FindNestedInstanceBySource(Transform root, string sourceAssetPath)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(t.gameObject)) continue;
                var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
                if (instanceRoot == null || instanceRoot == root.gameObject || instanceRoot.transform != t) continue;
                var source = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
                if (source != null && AssetDatabase.GetAssetPath(source) == sourceAssetPath) return t;
            }
            return null;
        }

        private static Transform FindChild(Transform parent, string name)
        {
            foreach (Transform c in parent) if (c.name == name) return c;
            return null;
        }

        private static bool IsSelfOrDescendant(Transform t, Transform ancestor)
        {
            for (var p = t; p != null; p = p.parent) if (p == ancestor) return true;
            return false;
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

        private static string V(Vector3 v)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:F3};{1:F3};{2:F3}", v.x, v.y, v.z);
        }

        private static string F(float f)
        {
            return f.ToString("F4", CultureInfo.InvariantCulture);
        }

        private static string AbsoluteProjectPath(string relative)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));
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
