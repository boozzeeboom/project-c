// Project C — пайплайн PRIMUM PORT v3 (1 FBX → 1 префаб).
//
// v3 = порт без выключенных в Blender коллекций (они удалены как контент):
//   UPPER_ADMIN_BUILDING_001, PASSENGER_CANOPY_WAITING_AREA, 40_HUB_WAREHOUSE,
//   MD2 stage 02–05 + reference/markers, 00_TERRAIN, 80_BUILDINGS, 99_REFERENCE.
//
// Соответствует docs/world/optimization/MAIN_PIPELINE_MESH_SHIP_LIGHT:
//   COMMON.md §3 шаг 3   — join блок × материал, флэт иерархии, ≤4 сабмеша;
//   COMMON.md §2 п.7     — двери/створки/интерактив/VFX не мержатся;
//   COMMON.md §5 шаг 7   — материалы только палитры M_PC_*/PM_*, 0 слотов URP/Lit;
//   COMMON.md §5.2       — ≤24 материала на статик-визуал;
//   COMMON.md §5 шаг 8   — root scale, визуал ребёнком;
//   COMMON.md §5 шаг 9   — слой 8 PRIMUM, static = 0;
//   COMMON.md §1.1       — канон масштаба k = 1.06.
//
// v3-дополнение: MD2_Deck_Blockout, LP2_Exact_Form_Sheets и UPPER_DOCK_Blockout_Deck
// остаются отдельными объектами в общем FBX (не входят в мерж).
//
// Меню: ProjectC → World → PRIMUM PORT V3 → …
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectC.EditorTools
{
    public static class PrimumPortV3Pipeline
    {
        public const string Folder = "Assets/_Project/Prefabs/City/Port";
        private const string ModelFolder = "Assets/_Project/Art/Models/Primum/PortV3";
        private const float RootScale = 1.06f;
        private const int PrimumLayer = 8;
        private const int MaxPaletteMaterials = 24;

        private const string ReportRelativePath =
            "docs/world/primum_city/09_MATERIALS/port_v3_import_report.csv";

        public class Target
        {
            public string Fbx;
            public string Prefab;
            public string Root;
        }

        public static readonly Target[] Targets =
        {
            new Target { Fbx = ModelFolder + "/PRIMUM_PORT_V3.fbx",
                         Prefab = Folder + "/primum_port_v3.prefab",
                         Root = "primum_port_v3" },
        };

        /// <summary>Объекты, которые по условию v3 остаются отдельными в общем FBX.</summary>
        public static readonly string[] StandaloneObjects =
        {
            "MD2_Deck_Blockout",
            "LP2_Exact_Form_Sheets",
            "UPPER_DOCK_Blockout_Deck",
        };

        // =====================================================================
        // Меню
        // =====================================================================

        [MenuItem("ProjectC/World/PRIMUM PORT V3/1. Probe (dry-run)", false, 830)]
        public static void ProbeMenu() { Probe(); }

        [MenuItem("ProjectC/World/PRIMUM PORT V3/2. Remap materials → palette", false, 831)]
        public static void RemapMenu() { RemapMaterials(); }

        [MenuItem("ProjectC/World/PRIMUM PORT V3/3. Build prefab", false, 832)]
        public static void BuildMenu() { BuildPrefabs(); }

        [MenuItem("ProjectC/World/PRIMUM PORT V3/4. Verify", false, 833)]
        public static void VerifyMenu() { Verify(); }

        // =====================================================================
        // 1. Проба
        // =====================================================================

        public static void Probe()
        {
            var sb = new StringBuilder();
            var palette = PrimumPortPipeline.LoadPalette();
            sb.AppendLine("[PORTV3] palette assets: " + palette.Count);

            foreach (var t in Targets)
            {
                sb.AppendLine("--- " + t.Fbx);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(t.Fbx);
                if (model == null) { sb.AppendLine("  FBX не загружается"); continue; }

                var slotCount = new Dictionary<string, int>(StringComparer.Ordinal);
                var shaders = new Dictionary<string, string>(StringComparer.Ordinal);
                var embeddedNames = new HashSet<string>(StringComparer.Ordinal);
                var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
                int nullSlots = 0, subMeshes = 0;
                foreach (var r in renderers)
                {
                    var mats = r.sharedMaterials;
                    subMeshes += mats.Length;
                    foreach (var m in mats)
                    {
                        if (m == null) { nullSlots++; continue; }
                        slotCount.TryGetValue(m.name, out int n); slotCount[m.name] = n + 1;
                        shaders[m.name] = m.shader != null ? m.shader.name : "<null>";
                        if (AssetDatabase.GetAssetPath(m) == t.Fbx) embeddedNames.Add(m.name);
                    }
                }

                int merged = 0, standalone = 0, other = 0;
                var found = new HashSet<string>(StringComparer.Ordinal);
                foreach (var tr in model.GetComponentsInChildren<Transform>(true))
                {
                    if (tr == model.transform) continue;
                    found.Add(tr.name);
                    if (tr.name.StartsWith("PM_PORT_", StringComparison.Ordinal)) merged++;
                    else if (Array.IndexOf(StandaloneObjects, tr.name) >= 0) standalone++;
                    else other++;
                }

                sb.AppendLine("  objects=" + found.Count + " (PM_PORT merged=" + merged +
                              ", standalone=" + standalone + ", doors/lamps=" + other + ")");
                sb.AppendLine("  renderers=" + renderers.Length + " slots=" + subMeshes + " nullSlots=" + nullSlots +
                              " distinct=" + slotCount.Count + " embedded=" + embeddedNames.Count);
                foreach (var name in StandaloneObjects)
                    sb.AppendLine("    standalone " + name + " -> " + (found.Contains(name) ? "OK" : "ОТСУТСТВУЕТ"));
                foreach (var kv in slotCount)
                {
                    palette.TryGetValue(kv.Key, out Material pal);
                    sb.AppendLine("    " + kv.Key + "  slots=" + kv.Value +
                                  "  shader=" + shaders[kv.Key] +
                                  "  palette=" + (pal != null ? AssetDatabase.GetAssetPath(pal) : "—"));
                }

                var bounds = new Bounds();
                bool first = true;
                foreach (var r in renderers)
                {
                    if (first) { bounds = r.bounds; first = false; }
                    else bounds.Encapsulate(r.bounds);
                }
                sb.AppendLine("  model bounds size = " + PrimumPortPipeline.F(bounds.size) +
                              " center=" + PrimumPortPipeline.F(bounds.center) +
                              " min=" + PrimumPortPipeline.F(bounds.min));
            }

            Debug.Log(sb.ToString());
        }

        // =====================================================================
        // 2. Материалы: внешняя карта объектов ModelImporter (embedded → палитра)
        // =====================================================================

        public static void RemapMaterials()
        {
            var palette = PrimumPortPipeline.LoadPalette();
            var problems = new List<string>();
            var report = new Report();

            foreach (var t in Targets)
            {
                var importer = AssetImporter.GetAtPath(t.Fbx) as ModelImporter;
                if (importer == null) { problems.Add("нет ModelImporter: " + t.Fbx); continue; }

                var embedded = new List<Material>();
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(t.Fbx))
                    if (obj is Material m) embedded.Add(m);

                var map = importer.GetExternalObjectMap();
                int added = 0, already = 0;
                foreach (var m in embedded)
                {
                    string name = m.name;
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
                    if (map.ContainsKey(id)) { already++; continue; }
                    if (!palette.TryGetValue(name, out Material pal))
                    { problems.Add("нет в палитре: " + name + " (" + t.Fbx + ")"); continue; }
                    importer.AddRemap(id, pal);
                    added++;
                }

                report.Add("remap", "material", Path.GetFileNameWithoutExtension(t.Fbx) + ".embedded",
                           embedded.Count.ToString(), "added=" + added + "; already=" + already);

                if (added > 0) importer.SaveAndReimport();

                int stillEmbedded = 0, litSlots = 0, nullSlots = 0;
                var distinct = new HashSet<string>();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(t.Fbx);
                if (model != null)
                {
                    foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
                        foreach (var m in r.sharedMaterials)
                        {
                            if (m == null) { nullSlots++; continue; }
                            distinct.Add(m.name);
                            if (AssetDatabase.GetAssetPath(m) == t.Fbx) stillEmbedded++;
                            if (m.shader != null && m.shader.name.StartsWith("Universal Render Pipeline/Lit")) litSlots++;
                        }
                }

                report.Add("remap", "material", Path.GetFileNameWithoutExtension(t.Fbx) + ".after",
                           distinct.Count.ToString(),
                           "embedded=" + stillEmbedded + "; urpLit=" + litSlots + "; null=" + nullSlots);

                if (stillEmbedded != 0) problems.Add("остались embedded-материалы: " + stillEmbedded + " (" + t.Fbx + ")");
                if (nullSlots != 0) problems.Add("пустые слоты: " + nullSlots + " (" + t.Fbx + ")");
                if (distinct.Count > MaxPaletteMaterials)
                    problems.Add("материалов " + distinct.Count + " > " + MaxPaletteMaterials + " (" + t.Fbx + ")");
                if (litSlots != 0) problems.Add("слоты URP/Lit: " + litSlots + " (" + t.Fbx + ")");
            }

            report.Write();
            if (problems.Count == 0) Debug.Log("[PORTV3] Remap: OK");
            else Debug.LogError("[PORTV3] Remap: проблем " + problems.Count + ": " + string.Join(" | ", problems));
        }

        // =====================================================================
        // 3. Префаб: root scale 1.06, FBX-инстанс ребёнком, слой 8, static = 0
        // =====================================================================

        public static void BuildPrefabs()
        {
            var problems = new List<string>();
            Directory.CreateDirectory(PrimumPortPipeline.AbsoluteProjectPath(Folder));

            foreach (var t in Targets)
            {
                var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(t.Fbx);
                if (modelAsset == null) { problems.Add("нет модели: " + t.Fbx); continue; }

                if (File.Exists(PrimumPortPipeline.AbsoluteProjectPath(t.Prefab)))
                { problems.Add("префаб уже существует (не перезаписываю): " + t.Prefab); continue; }

                Scene preview = EditorSceneManager.NewPreviewScene();
                try
                {
                    var root = new GameObject(t.Root);
                    SceneManager.MoveGameObjectToScene(root, preview);

                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, preview);
                    inst.name = t.Root + "_model";
                    inst.transform.SetParent(root.transform, false);
                    inst.transform.localPosition = Vector3.zero;
                    inst.transform.localRotation = Quaternion.identity;
                    inst.transform.localScale = Vector3.one;

                    root.transform.localScale = Vector3.one * RootScale;
                    SetLayerRecursive(root.transform, PrimumLayer);

                    PrefabUtility.SaveAsPrefabAsset(root, t.Prefab);
                    Debug.Log("[PORTV3] prebuilt " + t.Prefab);
                }
                catch (Exception e)
                {
                    problems.Add("ошибка сборки " + t.Prefab + ": " + e.Message);
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(preview);
                }
            }

            AssetDatabase.SaveAssets();
            if (problems.Count == 0) Debug.Log("[PORTV3] Build prefab: OK");
            else Debug.LogError("[PORTV3] Build prefab: проблем " + problems.Count + ": " + string.Join(" | ", problems));
        }

        // =====================================================================
        // 4. Проверка
        // =====================================================================

        public static void Verify()
        {
            var problems = new List<string>();
            var report = new Report();

            foreach (var t in Targets)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(t.Prefab) == null)
                { problems.Add("нет префаба: " + t.Prefab); continue; }

                var root = PrefabUtility.LoadPrefabContents(t.Prefab);
                try
                {
                    int renderers = 0, slots = 0, nullSlots = 0, litSlots = 0, submeshOver4 = 0, depth = 0;
                    var distinct = new HashSet<string>();
                    foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        renderers++;
                        var mats = r.sharedMaterials;
                        slots += mats.Length;
                        if (mats.Length > 4) submeshOver4++;
                        foreach (var m in mats)
                        {
                            if (m == null) { nullSlots++; continue; }
                            distinct.Add(m.name);
                            if (m.shader != null && m.shader.name.StartsWith("Universal Render Pipeline/Lit")) litSlots++;
                        }
                    }
                    foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                        depth = Mathf.Max(depth, Depth(tr, root.transform));

                    int wrongLayer = 0;
                    foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                        if (tr.gameObject.layer != PrimumLayer) wrongLayer++;

                    int merged = 0, standalone = 0, other = 0;
                    var found = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (tr == root.transform) continue;
                        found.Add(tr.name);
                        if (tr.name.StartsWith("PM_PORT_", StringComparison.Ordinal)) merged++;
                        else if (Array.IndexOf(StandaloneObjects, tr.name) >= 0) standalone++;
                        else other++;
                    }
                    var missingStandalone = new List<string>();
                    foreach (var n in StandaloneObjects)
                        if (!found.Contains(n)) missingStandalone.Add(n);

                    bool isStatic = root.isStatic;
                    string rootScale = root.transform.localScale.x.ToString("F3");
                    var b = new Bounds();
                    bool first = true;
                    foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                    }
                    string size = first ? "" : PrimumPortPipeline.F(b.size);

                    string key = Path.GetFileNameWithoutExtension(t.Prefab);
                    report.Add("verify", "prefab", key,
                               "MR=" + renderers + "; slots=" + slots + "; mats=" + distinct.Count +
                               "; urpLit=" + litSlots + "; null=" + nullSlots + "; submesh>4=" + submeshOver4 +
                               "; rootScale=" + rootScale + "; layer8=" + (wrongLayer == 0) + "; static=" + isStatic +
                               "; depth=" + depth + "; objects=" + found.Count + "; merged=" + merged +
                               "; standalone=" + standalone + "; other=" + other + "; size=" + size, "");

                    foreach (var n in missingStandalone)
                        report.Add("verify", "standalone", n, "MISSING", "");
                    foreach (var n in StandaloneObjects)
                        if (found.Contains(n)) report.Add("verify", "standalone", n, "OK", "");

                    if (litSlots != 0) problems.Add(key + ": слоты URP/Lit " + litSlots);
                    if (nullSlots != 0) problems.Add(key + ": пустые слоты " + nullSlots);
                    if (distinct.Count > MaxPaletteMaterials) problems.Add(key + ": материалов " + distinct.Count);
                    if (submeshOver4 != 0) problems.Add(key + ": мешей с >4 сабмешей " + submeshOver4);
                    if (wrongLayer != 0) problems.Add(key + ": объектов вне слоя 8: " + wrongLayer);
                    if (isStatic) problems.Add(key + ": root static = true");
                    if (Mathf.Abs(root.transform.localScale.x - RootScale) > 0.001f)
                        problems.Add(key + ": root scale " + rootScale + " ≠ " + RootScale);
                    if (missingStandalone.Count != 0)
                        problems.Add(key + ": нет отдельных объектов " + string.Join(", ", missingStandalone));

                    Debug.Log("[PORTV3] " + key + ": MR=" + renderers + " slots=" + slots + " mats=" + distinct.Count +
                              " urpLit=" + litSlots + " null=" + nullSlots + " depth=" + depth +
                              " objects=" + found.Count + " merged=" + merged + " standalone=" + standalone +
                              " other=" + other + " size=" + size);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            report.Write();
            if (problems.Count == 0) Debug.Log("[PORTV3] Verify: OK");
            else Debug.LogError("[PORTV3] Verify: проблем " + problems.Count + ": " + string.Join(" | ", problems));
        }

        // =====================================================================
        // Утилиты
        // =====================================================================

        private static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform c in t) SetLayerRecursive(c, layer);
        }

        private static int Depth(Transform t, Transform root)
        {
            int d = 0;
            while (t != null && t != root) { d++; t = t.parent; }
            return d;
        }

        // =====================================================================
        // Отчёт (свой файл, не v1/v2)
        // =====================================================================

        public class Report
        {
            private readonly List<string> _rows = new List<string>();

            public void Add(string phase, string category, string key, string value, string note)
            {
                _rows.Add(string.Join(",", new[] { phase, category, Csv(key), Csv(value), Csv(note) }));
            }

            public void Write()
            {
                if (_rows.Count == 0) return;
                string path = PrimumPortPipeline.AbsoluteProjectPath(ReportRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (!File.Exists(path)) File.WriteAllText(path, "phase,category,key,value,note\n", new UTF8Encoding(false));
                using (var w = new StreamWriter(path, true, new UTF8Encoding(false)))
                    w.Write(string.Join("\n", _rows) + "\n");
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
