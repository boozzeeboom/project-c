// Project C — пайплайн порта PRIMUM_PORT (порт «города»: 4 745 рендереров, 5 443 трансформа).
//
// Соответствует docs/world/optimization/MAIN_PIPELINE_MESH_SHIP_LIGHT:
//   COMMON.md §3 шаг 7   — материалы: палитра M_PC_* (процедурные Shader Graph), 0 слотов URP/Lit;
//   COMMON.md §5         — цветовые вариации берутся ГОТОВЫМИ дочерними Material Variant (m_Parent),
//                          новые материалы не создаются: 163 исходных → 23 материала палитры;
//   COMMON.md §4.2       — матрица мерджа (двери / коллайдеры / свет / выключенные / зеркальные — никогда);
//   SETTLEMENTS.md §7.5  — коллайдеры живут на отдельных узлах BC_*, не на узлах FBX;
//   LIGHTS.md §8.2       — свет только префабами PM_LIGHT_*, материал лампы PM_LAMP_GLOW_WARM
//                          только на реальных приборах; стекло — M_PC_Glass_Green, не лампа.
//
// Бэкап (вне Assets, не в git): _local_backups/MAIN_PIPELINE/PRIMUM_PORT/PRIMUM_PORT.prefab.pre-opt.bak
// Отчёт: docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/W_port_pipeline_2026-11-04.csv
//
// Меню: ProjectC → World → PRIMUM PORT → …
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
    public static class PrimumPortPipeline
    {
        // ------------------------------------------------------------------ цель

        public const string PrefabPath = "Assets/_Project/Prefabs/City/Port/PRIMUM_PORT.prefab";
        public const string ModelRootName = "gorod_port_model";

        private const string BackupRelativePath =
            "_local_backups/MAIN_PIPELINE/PRIMUM_PORT/PRIMUM_PORT.prefab.pre-opt.bak";

        private const string ReportRelativePath =
            "docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/W_port_pipeline_2026-11-04.csv";

        // ------------------------------------------------------------- материалы

        /// <summary>
        /// 163 исходных материала FBX → 23 материала палитры M_PC_*.
        /// Стекло (включая прозрачные MAT_ADMIN_*_GLASS и «стеклянные» LP_Hangar*_Glass) идёт
        /// в M_PC_Glass_Green — обычное зелёное стекло, НЕ материал лампы.
        /// PM_LAMP_GLOW_WARM ставится только на реальные приборы (WarmLamp / WorkLight /
        /// Dock_Light / Service_Lamp / Interior_Lamp / AntiGravGlow); подсветка вывесок
        /// (Nav_Text / Sign_Text / Interior_Text) остаётся на металле палитры.
        /// </summary>
        public static readonly Dictionary<string, string> MaterialMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // --- Cargo
            { "Cargo_Copper",                    "M_PC_Bronze" },
            { "Cargo_Painted_Blue",              "M_PC_Plastic_Blue" },
            { "Cargo_Painted_Mustard",           "M_PC_Steel_v2_Safety" },
            { "Cargo_Rusted_Metal",              "M_PC_IronRust" },
            { "Cargo_Steel_Dark",                "M_PC_Steel_v2_Dark" },
            { "Cargo_Steel_Galvanized",          "M_PC_Steel_v2" },
            { "Cargo_Tarpaulin",                 "M_PC_Fabric_Canvas" },
            { "Cargo_Timber",                    "M_PC_Wood" },
            { "Cargo_Timber_Light",              "M_PC_Wood_Pine" },
            { "Cargo_Warning_Yellow",            "M_PC_Steel_v2_Safety" },
            // --- Dock
            { "Dock_Blockout_Pocket",            "M_PC_Steel_v2_Dark" },
            { "Dock_Blockout_Safety",            "M_PC_Steel_v2_Safety" },
            { "Dock_Blockout_Steel",             "M_PC_Steel_v2" },
            { "Dock_Blockout_Walkway",           "M_PC_Steel_v2_Light" },
            { "Dock_Infra2_Light",               "M_PC_Steel_v2_Safety" },
            { "Dock_Infra2_Railing",             "M_PC_Steel_v2_Light" },
            { "Dock_Infra2_Safety",              "M_PC_Steel_v2_Safety" },
            { "Dock_Infra2_Sign",                "M_PC_Plastic_Blue" },
            { "Dock_Infra2_Walkway",             "M_PC_Steel_v2_Light" },
            // --- HUB2
            { "HUB2_MAT_Exterior_Copper",        "M_PC_Bronze" },
            { "HUB2_MAT_Exterior_Frame",         "M_PC_Steel_v2_Dark" },
            { "HUB2_MAT_Exterior_Wall",          "M_PC_Concrete" },
            { "HUB2_MAT_Frame_Dark_Steel",       "M_PC_Steel_v2_Dark" },
            { "HUB2_MAT_Frame_Oxidized",         "M_PC_IronRust" },
            { "HUB2_MAT_Interior_Floor",         "M_PC_Plate" },
            { "HUB2_MAT_Interior_Walkway",       "M_PC_Steel_v2_Light" },
            { "HUB2_MAT_Nav_Sign",               "M_PC_Steel_v2_Dark" },
            { "HUB2_MAT_Nav_Text",               "M_PC_Steel_v2_Light" },
            { "HUB2_MAT_Rolling_Gate",           "M_PC_Steel_v2" },
            { "HUB2_MAT_Roof_Dark",              "M_PC_Steel_v2_Dark" },
            { "HUB2_MAT_Safety_Band",            "M_PC_Steel_v2_Safety" },
            { "HUB2_MAT_Sign_Panel",             "M_PC_Steel_v2_Dark" },
            { "HUB2_MAT_Sign_Text",              "M_PC_Steel_v2_Light" },
            { "HUB2_MAT_Threshold_Rust",         "M_PC_IronRust" },
            { "HUB2_MAT_Transfer_Court",         "M_PC_Cobble" },
            // --- HUB3
            { "HUB3_MAT_Cargo_Rust",             "M_PC_IronRust" },
            { "HUB3_MAT_Cargo_Wood",             "M_PC_Wood" },
            { "HUB3_MAT_Cargo_Wood_Light",       "M_PC_Wood_Pine" },
            { "HUB3_MAT_Interior_Lamp",          "PM_LAMP_GLOW_WARM" },
            { "HUB3_MAT_Interior_Sign",          "M_PC_Steel_v2_Dark" },
            { "HUB3_MAT_Interior_Text",          "M_PC_Steel_v2_Light" },
            { "HUB3_MAT_Rack_Copper",            "M_PC_Bronze" },
            { "HUB3_MAT_Rack_Dark",              "M_PC_Steel_v2_Dark" },
            { "HUB3_MAT_Rack_Shelf",             "M_PC_Plate" },
            { "HUB3_MAT_Stair_Railing",          "M_PC_Steel_v2_Light" },
            { "HUB3_MAT_Stair_Steel",            "M_PC_Steel_v2" },
            // --- прочее
            { "Lit",                             "M_PC_Concrete" },
            // --- LP_Hangar001
            { "LP_Hangar001_Glass",              "M_PC_Glass_Green" },
            { "LP_Hangar001_Machinery",          "M_PC_Steel_v2_Dark" },
            { "LP_Hangar001_PanelLight",         "M_PC_Steel_v2_Light" },
            { "LP_Hangar001_Safety",             "M_PC_Steel_v2_Safety" },
            { "LP_Hangar001_Structure",          "M_PC_Steel_v2_Dark" },
            { "LP_Hangar001_WallSteel",          "M_PC_Steel_v2" },
            { "LP_Hangar001_WarmLamp",           "PM_LAMP_GLOW_WARM" },
            { "LP_Hangar001_WornMetal",          "M_PC_IronRust" },
            // --- LP_Hangar002
            { "LP_Hangar002_Glass",              "M_PC_Glass_Green" },
            { "LP_Hangar002_Lumber",             "M_PC_Wood" },
            { "LP_Hangar002_Machinery",          "M_PC_Steel_v2_Dark" },
            { "LP_Hangar002_PanelLight",         "M_PC_Steel_v2_Light" },
            { "LP_Hangar002_Safety",             "M_PC_Steel_v2_Safety" },
            { "LP_Hangar002_Structure",          "M_PC_Steel_v2_Dark" },
            { "LP_Hangar002_WallSteel",          "M_PC_Steel_v2" },
            { "LP_Hangar002_WarmLamp",           "PM_LAMP_GLOW_WARM" },
            // --- LP_HangarFamily
            { "LP_HangarFamily_Cargo",           "M_PC_Wood" },
            { "LP_HangarFamily_Glass",           "M_PC_Glass_Green" },
            { "LP_HangarFamily_Machinery",       "M_PC_Steel_v2_Dark" },
            { "LP_HangarFamily_PanelLight",      "M_PC_Steel_v2_Light" },
            { "LP_HangarFamily_Safety",          "M_PC_Steel_v2_Safety" },
            { "LP_HangarFamily_Structure",       "M_PC_Steel_v2_Dark" },
            { "LP_HangarFamily_WallSteel",       "M_PC_Steel_v2" },
            { "LP_HangarFamily_WarmLamp",        "PM_LAMP_GLOW_WARM" },
            // --- LP2
            { "LP2_Cyan_Dock_Light",             "PM_LAMP_GLOW_WARM" },
            { "LP2_Rust_Brace",                  "M_PC_IronRust" },
            { "LP2_Sheet_Dark",                  "M_PC_Steel_v2_Dark" },
            { "LP2_Sheet_Light",                 "M_PC_Steel_v2_Light" },
            { "LP2_Structural_Steel",            "M_PC_Steel_v2" },
            { "LP2_Warm_Service_Light",          "PM_LAMP_GLOW_WARM" },
            // --- LP3
            { "LP3_Machine_Detail",              "M_PC_Steel_v2_Dark" },
            { "LP3_Panel_Seam",                  "M_PC_Steel_v2_Dark" },
            { "LP3_Rivet_Steel",                 "M_PC_Steel_v2_Light" },
            { "LP3_Safety_Paint",                "M_PC_Steel_v2_Safety" },
            { "LP3_Worn_Rust",                   "M_PC_IronRust" },
            // --- ADMIN
            { "MAT_ADMIN_FLOOR_CONCRETE",              "M_PC_Concrete" },
            { "MAT_ADMIN_FRAME_DARK_METAL",            "M_PC_Steel_v2_Dark" },
            { "MAT_ADMIN_GLASS_BLUE",                  "M_PC_Glass_Green" },
            { "MAT_ADMIN_INTERIOR_PARTITION_TRIM",     "M_PC_Bronze" },
            { "MAT_ADMIN_INTERIOR_PARTITION_WARM_GRAY","M_PC_Concrete" },
            { "MAT_ADMIN_INTERIOR_PATH_BRASS",         "M_PC_Bronze_Brass" },
            { "MAT_ADMIN_INTERIOR_ZONE_MARKER",        "M_PC_Plastic_Blue" },
            { "MAT_ADMIN_ROOM_FURNITURE",              "M_PC_Wood_Dark" },
            { "MAT_ADMIN_ROOM_PARTITION",              "M_PC_Concrete" },
            { "MAT_ADMIN_ROOM_TRIM",                   "M_PC_Bronze" },
            { "MAT_ADMIN_ROOM_WOOD",                   "M_PC_Wood" },
            { "MAT_ADMIN_SANITARY",                    "M_PC_Marble" },
            { "MAT_ADMIN_SERVICE_BODY",                "M_PC_Concrete" },
            { "MAT_ADMIN_SERVICE_GLASS",               "M_PC_Glass_Green" },
            { "MAT_ADMIN_SERVICE_SIGN",                "M_PC_Plastic_Blue" },
            { "MAT_ADMIN_SERVICE_WOOD_BRASS",          "M_PC_Bronze" },
            { "MAT_ADMIN_TRIM_COPPER",                 "M_PC_Bronze" },
            { "MAT_Bronze_Dome",                       "M_PC_Bronze" },
            { "MAT_Granite_Grey",                      "M_PC_Cobble_Granite" },
            // --- MD2
            { "MD2_MAT_Cargo_Blue_Tarp",         "M_PC_Fabric_Canvas" },
            { "MD2_MAT_Cargo_Dark_Metal",        "M_PC_Steel_v2_Dark" },
            { "MD2_MAT_Cargo_Patina",            "M_PC_Bronze" },
            { "MD2_MAT_Cargo_Rust",              "M_PC_IronRust" },
            { "MD2_MAT_Cargo_Rust_Stage02",      "M_PC_IronRust" },
            { "MD2_MAT_Cargo_Soot",              "M_PC_IronRust_Lead" },
            { "MD2_MAT_Cargo_Warning_Label",     "M_PC_Steel_v2_Safety" },
            { "MD2_MAT_Cargo_Wood",              "M_PC_Wood" },
            { "MD2_MAT_Cargo_Wood_Light",        "M_PC_Wood_Pine" },
            { "MD2_MAT_Control_Booth_Stage03",   "M_PC_Steel_v2" },
            { "MD2_MAT_Convoy_Lane_Stage03",     "M_PC_Concrete" },
            { "MD2_MAT_Dark_Steel",              "M_PC_Steel_v2_Dark" },
            { "MD2_MAT_Detail_Faded_White",      "M_PC_Concrete_Limewash" },
            { "MD2_MAT_Detail_Fasteners",        "M_PC_Steel_v2_Light" },
            { "MD2_MAT_Detail_Repair_Patch",     "M_PC_Steel_v2" },
            { "MD2_MAT_Detail_Rust",             "M_PC_IronRust" },
            { "MD2_MAT_Detail_Safety_Yellow",    "M_PC_Steel_v2_Safety" },
            { "MD2_MAT_Detail_Seams",            "M_PC_Steel_v2_Dark" },
            { "MD2_MAT_Detail_Soot",             "M_PC_IronRust_Lead" },
            { "MD2_MAT_Pedestrian_Blue",         "M_PC_Plastic_Blue" },
            { "MD2_MAT_Pedestrian_Route_Stage03","M_PC_Plastic_Blue" },
            { "MD2_MAT_Safety_Barrier_Stage03",  "M_PC_Steel_v2" },
            { "MD2_MAT_Safety_Curb",             "M_PC_Steel_v2_Light" },
            { "MD2_MAT_Safety_Marking_Stage03",  "M_PC_Steel_v2_Safety" },
            { "MD2_MAT_Service_Cable",           "M_PC_Plastic_Rubber" },
            { "MD2_MAT_Service_Copper",          "M_PC_Bronze" },
            { "MD2_MAT_Service_Dark_Metal",      "M_PC_Steel_v2_Dark" },
            { "MD2_MAT_Service_Lamp",            "PM_LAMP_GLOW_WARM" },
            { "MD2_MAT_Service_Oxidized_Metal",  "M_PC_IronRust" },
            { "MD2_MAT_Service_Patina",          "M_PC_Bronze" },
            { "MD2_MAT_Service_Rust",            "M_PC_IronRust" },
            { "MD2_MAT_Service_Warning",         "M_PC_Steel_v2_Safety" },
            { "MD2_MAT_Sign_Panel_Stage03",      "M_PC_Steel_v2_Dark" },
            { "MD2_MAT_Sign_Text_Stage03",       "M_PC_Steel_v2_Light" },
            // --- PC_Canopy
            { "PC_Canopy_AntiGravGlow",          "PM_LAMP_GLOW_WARM" },
            { "PC_Canopy_Concrete",              "M_PC_Concrete" },
            { "PC_Canopy_Copper",                "M_PC_Bronze" },
            { "PC_Canopy_DarkMetal",             "M_PC_Steel_v2_Dark" },
            { "PC_Canopy_Sign",                  "M_PC_Steel_v2_Safety" },
            { "PC_Canopy_Steel",                 "M_PC_Steel_v2" },
            { "PC_Canopy_Wood",                  "M_PC_Wood" },
            // --- Transition
            { "Transition_AntiGravBlue",         "PM_LAMP_GLOW_WARM" },
            { "Transition_Concrete",             "M_PC_Concrete" },
            { "Transition_Copper",               "M_PC_Bronze" },
            { "Transition_DarkSteel",            "M_PC_Steel_v2_Dark" },
            { "Transition_IndustrialBlue",       "M_PC_Plastic_Blue" },
            { "Transition_MesiumGreen",          "PM_LAMP_GLOW_WARM" },
            { "Transition_Rust",                 "M_PC_IronRust" },
            { "Transition_SafetyYellow",         "M_PC_Steel_v2_Safety" },
            { "Transition_Steel",                "M_PC_Steel_v2" },
            { "Transition_Worklight",            "PM_LAMP_GLOW_WARM" },
            // --- Workshop2
            { "Workshop2_AeroBlue",              "M_PC_Plastic_Blue" },
            { "Workshop2_Concrete",              "M_PC_Concrete" },
            { "Workshop2_Copper",                "M_PC_Bronze" },
            { "Workshop2_DarkSteel",             "M_PC_Steel_v2_Dark" },
            { "Workshop2_OxidizedGreen",         "M_PC_IronRust" },
            { "Workshop2_Rust",                  "M_PC_IronRust" },
            { "Workshop2_SafetyYellow",          "M_PC_Steel_v2_Safety" },
            { "Workshop2_Steel",                 "M_PC_Steel_v2" },
            { "Workshop2_Wood",                  "M_PC_Wood" },
            { "Workshop2_WoodLight",             "M_PC_Wood_Pine" },
            { "Workshop2_WorkLight",             "PM_LAMP_GLOW_WARM" },
        };

        /// <summary>Допустимый набор палитры на ассет этого класса (COMMON.md §5.2: ≤24).</summary>
        private const int MaxPaletteMaterials = 24;

        // =====================================================================
        // Меню
        // =====================================================================

        [MenuItem("ProjectC/World/PRIMUM PORT/1. Materials (dry-run)", false, 800)]
        public static void MaterialsDryRun() { RunMaterials(false); }

        [MenuItem("ProjectC/World/PRIMUM PORT/2. Materials (apply)", false, 801)]
        public static void MaterialsApply() { RunMaterials(true); }

        [MenuItem("ProjectC/World/PRIMUM PORT/3. Verify", false, 808)]
        public static void Verify() { RunVerify(); }

        [MenuItem("ProjectC/World/PRIMUM PORT/9. Rollback (from backup)", false, 819)]
        public static void Rollback()
        {
            string backup = AbsoluteProjectPath(BackupRelativePath);
            if (!File.Exists(backup)) { Debug.LogError("[PORT] Rollback: нет бэкапа " + backup); return; }
            File.Copy(backup, AbsoluteProjectPath(PrefabPath), true);
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("[PORT] Rollback: префаб восстановлен из " + backup);
        }

        // =====================================================================
        // Шаг 1–2: материалы палитры
        // =====================================================================

        private static void RunMaterials(bool apply)
        {
            var report = new Report();
            var problems = new List<string>();
            string phase = apply ? "apply" : "dryrun";
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var modelRoot = FindModelRoot(root.transform);
                if (modelRoot == null)
                {
                    Debug.LogError("[PORT] Модель не найдена: " + ModelRootName);
                    return;
                }

                var palette = LoadPalette();
                foreach (var kv in MaterialMap)
                    if (!palette.ContainsKey(kv.Value))
                        problems.Add("нет материала палитры: " + kv.Value);

                var plan = new List<(MeshRenderer r, Material from, Material to)>();
                var unmapped = new SortedDictionary<string, int>(StringComparer.Ordinal);
                var perTarget = new SortedDictionary<string, int>(StringComparer.Ordinal);
                var renderers = modelRoot.GetComponentsInChildren<MeshRenderer>(true);

                foreach (var r in renderers)
                {
                    var mats = r.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var src = mats[i];
                        if (src == null) { problems.Add("пустой слот материала: " + PathOf(r.transform)); continue; }
                        if (!MaterialMap.TryGetValue(src.name, out string target))
                        {
                            unmapped.TryGetValue(src.name, out int v);
                            unmapped[src.name] = v + 1;
                            continue;
                        }
                        palette.TryGetValue(target, out Material to);
                        if (to == null) continue;
                        plan.Add((r, src, to));
                        perTarget.TryGetValue(target, out int n);
                        perTarget[target] = n + 1;
                        changed = true;
                    }
                    if (!changed) continue;
                }

                int litSlotsBefore = 0;
                foreach (var r in renderers)
                    foreach (var m in r.sharedMaterials)
                        if (m != null && m.shader != null && m.shader.name.StartsWith("Universal Render Pipeline/Lit"))
                            litSlotsBefore++;

                report.Add(phase, "materials", "renderers", renderers.Length.ToString(), "");
                report.Add(phase, "materials", "slots_urp_lit_before", litSlotsBefore.ToString(), "");
                report.Add(phase, "materials", "assignments_planned", plan.Count.ToString(), "");
                report.Add(phase, "materials", "distinct_targets", perTarget.Count.ToString(), "гейт ≤" + MaxPaletteMaterials);
                foreach (var kv in perTarget)
                    report.Add(phase, "materials", "target=" + kv.Key, kv.Value.ToString(), "");
                foreach (var kv in unmapped)
                    problems.Add("материал вне карты: " + kv.Key + " (слотов " + kv.Value + ")");

                if (apply)
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

                    int litAfter = 0;
                    var targets = new HashSet<string>();
                    foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                        foreach (var m in r.sharedMaterials)
                        {
                            if (m == null) continue;
                            targets.Add(m.name);
                            if (m.shader != null && m.shader.name.StartsWith("Universal Render Pipeline/Lit")) litAfter++;
                        }
                    report.Add("apply", "materials", "slots_urp_lit_after", litAfter.ToString(), "");
                    report.Add("apply", "materials", "distinct_materials_after", targets.Count.ToString(), "");
                    if (litAfter != 0) problems.Add("остались слоты URP/Lit: " + litAfter);
                    if (targets.Count > MaxPaletteMaterials)
                        problems.Add("материалов на ассете " + targets.Count + " > " + MaxPaletteMaterials);
                }

                report.Write();
                if (problems.Count == 0)
                    Debug.Log("[PORT] Materials " + phase + ": OK, назначений " + plan.Count +
                              ", целей " + perTarget.Count + ", URP/Lit до " + litSlotsBefore +
                              " → отчёт " + AbsoluteProjectPath(ReportRelativePath));
                else
                    Debug.LogError("[PORT] Materials " + phase + ": проблем " + problems.Count + ": " + string.Join(" | ", problems));
            }
            catch (Exception e)
            {
                report.Add("failed", "exception", e.GetType().Name, "", e.Message);
                report.Write();
                Debug.LogError("[PORT] Materials failed: " + e);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void RunVerify()
        {
            var report = new Report();
            var problems = new List<string>();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var modelRoot = FindModelRoot(root.transform);
                if (modelRoot == null) problems.Add("модель " + ModelRootName + " не найдена");

                var mats = new HashSet<string>();
                int litSlots = 0, nullSlots = 0;
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) { nullSlots++; continue; }
                        mats.Add(m.name);
                        if (m.shader != null && m.shader.name.StartsWith("Universal Render Pipeline/Lit")) litSlots++;
                    }

                int glassGlass = 0, glassLamp = 0, lampSlots = 0;
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    bool glassNamed = r.name.IndexOf("GLASS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      r.name.IndexOf("WINDOW", StringComparison.OrdinalIgnoreCase) >= 0;
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        if (m.name == "PM_LAMP_GLOW_WARM") lampSlots++;
                        if (!glassNamed) continue;
                        if (m.name == "M_PC_Glass_Green") glassGlass++;
                        if (m.name == "PM_LAMP_GLOW_WARM") glassLamp++;
                    }
                }

                int box = 0, meshc = 0, bcNodes = 0, doors = 0, lights = 0;
                foreach (var c in root.GetComponentsInChildren<Component>(true))
                {
                    if (c == null) { problems.Add("missing script"); continue; }
                    if (c is BoxCollider) box++;
                    if (c is MeshCollider) meshc++;
                    if (c.GetType().Name == "DoorController") doors++;
                    if (c is Light) lights++;
                }
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("BC_")) bcNodes++;

                report.Add("verify", "state", "distinct_materials", mats.Count.ToString(), "");
                report.Add("verify", "state", "slots_urp_lit", litSlots.ToString(), "должно быть 0");
                report.Add("verify", "state", "null_slots", nullSlots.ToString(), "");
                report.Add("verify", "state", "glass_slots_with_glass_green", glassGlass.ToString(), "");
                report.Add("verify", "state", "glass_slots_with_lamp_material", glassLamp.ToString(), "должно быть 0");
                report.Add("verify", "state", "lamp_material_slots", lampSlots.ToString(), "");
                report.Add("verify", "state", "box_colliders", box.ToString(), "");
                report.Add("verify", "state", "mesh_colliders", meshc.ToString(), "");
                report.Add("verify", "state", "bc_nodes", bcNodes.ToString(), "");
                report.Add("verify", "state", "door_controllers", doors.ToString(), "");
                report.Add("verify", "state", "light_components", lights.ToString(), "");
                report.Add("verify", "state", "root_scale", root.transform.localScale.ToString("F3"), "");

                if (litSlots != 0) problems.Add("слоты URP/Lit: " + litSlots);
                if (mats.Count > MaxPaletteMaterials) problems.Add("материалов " + mats.Count + " > " + MaxPaletteMaterials);
                if (glassLamp != 0) problems.Add("материал лампы на стекле: " + glassLamp);
                if (nullSlots != 0) problems.Add("пустых слотов: " + nullSlots);

                report.Write();
                if (problems.Count == 0)
                    Debug.Log("[PORT] Verify: OK, материалов " + mats.Count + ", стекло зелёное " + glassGlass +
                              ", коллайдеров " + (box + meshc) + ", дверей " + doors + ", свет " + lights);
                else
                    Debug.LogError("[PORT] Verify: проблем " + problems.Count + ": " + string.Join(" | ", problems));
            }
            catch (Exception e)
            {
                report.Add("failed", "exception", e.GetType().Name, "", e.Message);
                report.Write();
                Debug.LogError("[PORT] Verify failed: " + e);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // =====================================================================
        // Утилиты
        // =====================================================================

        public static Dictionary<string, Material> LoadPalette()
        {
            var result = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project/Materials" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/ARCHIVE_NOTUSED/") || path.Contains("/archive/")) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) continue;
                if (!result.ContainsKey(m.name)) result[m.name] = m;
            }
            return result;
        }

        public static Transform FindModelRoot(Transform root)
        {
            var direct = root.Find(ModelRootName);
            if (direct != null) return direct;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == ModelRootName) return t;
            return null;
        }

        private static void EnsureBackup()
        {
            string backup = AbsoluteProjectPath(BackupRelativePath);
            if (File.Exists(backup)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            File.Copy(AbsoluteProjectPath(PrefabPath), backup, true);
            Debug.Log("[PORT] Бэкап префаба: " + backup);
        }

        public static string PathOf(Transform t)
        {
            var sb = new StringBuilder(t.name);
            var p = t.parent;
            while (p != null) { sb.Insert(0, p.name + "/"); p = p.parent; }
            return sb.ToString();
        }

        public static string AbsoluteProjectPath(string relative)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));
        }

        public static long CountTris(Mesh m)
        {
            long n = 0;
            for (int i = 0; i < m.subMeshCount; i++) n += (long)m.GetIndexCount(i) / 3L;
            return n;
        }

        public static string F(Vector3 v)
        {
            return v.x.ToString("F2", CultureInfo.InvariantCulture) + "x" +
                   v.y.ToString("F2", CultureInfo.InvariantCulture) + "x" +
                   v.z.ToString("F2", CultureInfo.InvariantCulture);
        }

        // =====================================================================
        // Отчёт
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
                string path = AbsoluteProjectPath(ReportRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (!File.Exists(path)) File.WriteAllText(path, "phase,category,key,value,note\n");
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
