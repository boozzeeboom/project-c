using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// PRIMUM, этап Э3.1 «Материалы»: перевод слотов города с FBX-плейсхолдеров <c>URP/Lit</c> на
    /// процедурные материалы проекта по таблице <c>docs/world/primum_city/09_MATERIALS/material_map.csv</c>.
    ///
    /// Контракт (01_MATERIAL_UNIFICATION.md §2, §7.1):
    ///  * ключ — ИМЯ текущего материала слота (имена из Blender/FBX не переименовываются);
    ///  * только <c>sharedMaterials</c>, подстановка в тот же индекс слота, число слотов не меняется;
    ///  * Undo + <c>PrefabUtility.RecordPrefabInstancePropertyModifications</c>, на диск ничего не пишется
    ///    (сцена остаётся грязной — сохраняет пользователь);
    ///  * идемпотентно: слот, уже стоящий на цели таблицы, считается <c>already</c>;
    ///  * двери (<c>PM_DOOR_*</c> и рендереры внутри префабов <c>Prefabs/City/Doors/</c>) по умолчанию
    ///    пропускаются — это шаг 7.2.5, правка префабов дверей;
    ///  * виды <c>DONE</c> / <c>HOLD</c> / <c>GAP</c> не трогаются.
    ///
    /// Цели ищутся по имени в <c>Assets/_Project/Materials/**</c>, папки <c>archive*</c> исключены.
    /// Отчёты пишутся в <c>docs/world/primum_city/09_MATERIALS/reports/</c>.
    ///
    /// Меню: ProjectC → World → PRIMUM → Материалы: … (приоритеты 380–389).
    /// </summary>
    public static class PrimumMaterialMap
    {
        // ------------------------------------------------------------------ константы

        public const string CityRootName = "PRIMUM";
        public const string WorldRootName = "WorldRoot_0_0";

        private const string MapRelativePath = "docs/world/primum_city/09_MATERIALS/material_map.csv";
        private const string ReportDirRelative = "docs/world/primum_city/09_MATERIALS/reports";
        private const string MaterialsRoot = "Assets/_Project/Materials";
        private const string DoorPrefabFolder = "/Prefabs/City/Doors/";
        private const string DoorMaterialPrefix = "PM_DOOR_";
        private const string GlowMatName = "PM_LAMP_GLOW_WARM";
        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";

        private const string BronzeBasePath = "Assets/_Project/Materials/Bronze/M_PC_Bronze.mat";
        private const string BronzeArchivePath = "Assets/_Project/Materials/Bronze/ARCHIVE_NOTUSED/M_PC_Bronze.mat";

        private static readonly HashSet<string> ApplyKinds = new HashSet<string>
        {
            "BASE", "VARIANT", "VARIANT_BASE", "PROJECT_LIT"
        };

        private static readonly HashSet<string> ModelExtensions = new HashSet<string>
        {
            ".fbx", ".obj", ".blend", ".dae", ".3ds", ".max", ".ma", ".mb"
        };

        // ------------------------------------------------------------------ данные

        private sealed class MapRow
        {
            public string Source;
            public int Slots;
            public string Hex;
            public string Family;
            public string Target;
            public string Kind;
            public string Note;
        }

        public sealed class RunStats
        {
            public string Scope;
            public bool Apply;
            public int Renderers;
            public int RenderersChanged;
            public int TotalSlots;
            public int Changed;
            public int Already;
            public int Unmapped;
            public int SkipDoors;
            public int SkipKind;
            public int Null;
            public int TargetMissing;
            public string ReportPath;
            public readonly List<string> Errors = new List<string>();

            public override string ToString()
            {
                return string.Format(
                    "[{0}] {1}: changed {2} / already {3} / unmapped {4} / total {5}  " +
                    "(skip_doors {6}, skip_DONE/HOLD/GAP {7}, null {8}, target_missing {9}; renderers {10}, изменено рендереров {11})",
                    Scope, Apply ? "APPLY" : "DRY-RUN", Changed, Already, Unmapped, TotalSlots,
                    SkipDoors, SkipKind, Null, TargetMissing, Renderers, RenderersChanged);
            }
        }

        private sealed class Bucket
        {
            public string Source;
            public string Kind;
            public string Target;
            public string Action;
            public int Slots;
            public readonly HashSet<Renderer> Renderers = new HashSet<Renderer>();
        }

        // ------------------------------------------------------------------ меню

        [MenuItem("ProjectC/World/PRIMUM/Материалы: отчёт (без изменений)", false, 380)]
        public static void MenuReport()
        {
            var root = FindCityRoot();
            if (root == null) return;
            Debug.Log("[PrimumMaterialMap] " + Run(root, false, false, "report"));
        }

        [MenuItem("ProjectC/World/PRIMUM/Материалы: применить", false, 381)]
        public static void MenuApply()
        {
            var root = FindCityRoot();
            if (root == null) return;
            if (!EditorUtility.DisplayDialog("PRIMUM — материалы",
                    "Подставить материалы по material_map.csv во ВСЁМ PRIMUM?\n" +
                    "Двери не трогаются. Сцена останется несохранённой (Undo доступен).",
                    "Применить", "Отмена"))
                return;
            Debug.Log("[PrimumMaterialMap] " + Run(root, true, false, "apply"));
        }

        [MenuItem("ProjectC/World/PRIMUM/Материалы: проверить (verify)", false, 382)]
        public static void MenuVerify()
        {
            var root = FindCityRoot();
            if (root == null) return;
            Debug.Log("[PrimumMaterialMap] verify\n" + Verify(root, "verify"));
        }

        [MenuItem("ProjectC/World/PRIMUM/Материалы: собрать цветовые варианты", false, 383)]
        public static void MenuBuildVariants()
        {
            Debug.Log("[PrimumMaterialMap] variants\n" + BuildVariants());
        }

        [MenuItem("ProjectC/World/PRIMUM/Материалы: применить к выделенному чанку", false, 384)]
        public static void MenuApplySelected()
        {
            var t = Selection.activeTransform;
            var root = FindCityRoot();
            if (t == null || root == null || !t.IsChildOf(root) || t == root)
            {
                EditorUtility.DisplayDialog("PRIMUM — материалы",
                    "Выделите чанк (или любой узел) внутри WorldRoot_0_0/PRIMUM.", "OK");
                return;
            }
            Debug.Log("[PrimumMaterialMap] " + Run(t, true, false, "apply"));
        }

        [MenuItem("ProjectC/World/PRIMUM/Материалы: проверить выделенный чанк", false, 385)]
        public static void MenuVerifySelected()
        {
            var t = Selection.activeTransform;
            var root = FindCityRoot();
            if (t == null || root == null || !t.IsChildOf(root))
            {
                EditorUtility.DisplayDialog("PRIMUM — материалы",
                    "Выделите чанк (или любой узел) внутри WorldRoot_0_0/PRIMUM.", "OK");
                return;
            }
            Debug.Log("[PrimumMaterialMap] verify\n" + Verify(t, "verify"));
        }

        // ------------------------------------------------------------------ поиск корня

        public static Transform FindCityRoot()
        {
            var world = GameObject.Find(WorldRootName);
            var root = world != null ? world.transform.Find(CityRootName) : null;
            if (root == null)
                Debug.LogError("[PrimumMaterialMap] Не найден " + WorldRootName + "/" + CityRootName +
                               " — откройте Assets/_Project/Scenes/World/WorldScene_0_0.unity.");
            return root;
        }

        public static Transform FindChunk(string chunkName)
        {
            var root = FindCityRoot();
            return root != null ? root.Find(chunkName) : null;
        }

        // ------------------------------------------------------------------ таблица и цели

        private static Dictionary<string, MapRow> LoadMap(List<string> errors)
        {
            var map = new Dictionary<string, MapRow>(StringComparer.Ordinal);
            string full = Path.Combine(ProjectRoot(), MapRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
            {
                errors.Add("нет таблицы " + MapRelativePath);
                return map;
            }

            string[] lines = File.ReadAllLines(full, Encoding.UTF8);
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] f = line.Split(';');
                if (f.Length < 7)
                {
                    errors.Add("строка " + (i + 1) + ": меньше 7 колонок");
                    continue;
                }
                var row = new MapRow
                {
                    Source = f[0],
                    Slots = int.TryParse(f[1], out int s) ? s : 0,
                    Hex = f[3],
                    Family = f[4],
                    Target = f[5].Trim(),
                    Kind = f[6].Trim(),
                    Note = f.Length > 7 ? f[7] : ""
                };
                if (map.ContainsKey(row.Source))
                {
                    errors.Add("дубль source в таблице: " + row.Source);
                    continue;
                }
                if (ApplyKinds.Contains(row.Kind) && string.IsNullOrEmpty(row.Target))
                    errors.Add("у " + row.Source + " вид " + row.Kind + ", но пустой target");
                map.Add(row.Source, row);
            }
            return map;
        }

        /// <summary>Имя → материал в Assets/_Project/Materials/** (без папок archive*). Дубли имён — в errors.</summary>
        private static Dictionary<string, Material> IndexProjectMaterials(out Dictionary<string, List<string>> duplicates)
        {
            var index = new Dictionary<string, Material>(StringComparer.Ordinal);
            duplicates = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var pathsByName = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialsRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) continue;
                if (path.ToLowerInvariant().Contains("/archive")) continue;
                string name = Path.GetFileNameWithoutExtension(path);
                if (!pathsByName.TryGetValue(name, out var list)) pathsByName[name] = list = new List<string>();
                list.Add(path);
            }

            foreach (var kv in pathsByName)
            {
                if (kv.Value.Count > 1) duplicates[kv.Key] = kv.Value;
                var m = AssetDatabase.LoadAssetAtPath<Material>(kv.Value[0]);
                if (m != null) index[kv.Key] = m;
            }
            return index;
        }

        private static Dictionary<string, Material> ResolveTargets(Dictionary<string, MapRow> map, List<string> errors)
        {
            var index = IndexProjectMaterials(out var dups);
            var targets = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (var row in map.Values)
            {
                if (string.IsNullOrEmpty(row.Target) || targets.ContainsKey(row.Target)) continue;
                if (dups.TryGetValue(row.Target, out var dupPaths))
                {
                    errors.Add("цель " + row.Target + " неоднозначна: " + string.Join(", ", dupPaths));
                    continue;
                }
                if (index.TryGetValue(row.Target, out var m)) targets[row.Target] = m;
            }
            return targets;
        }

        // ------------------------------------------------------------------ правила по имени объекта

        /// <summary>
        /// Решения пользователя на пилотном чанке (25.10, журнал шаг 2m) для слитых мешей чанка: источник один
        /// и тот же (например <c>M_STONE_BEIGE</c>), но снаружи и внутри нужен разный материал — по имени материала
        /// это не различить. Правило: суффикс имени объекта → цель; только рендереры с одним слотом, не двери.
        /// </summary>
        private static readonly string[,] ObjectRules =
        {
            { "_INT_M_STONE_BEIGE", "M_PC_Concrete" },
            { "_M_M_WINDOW_GLASS", "M_PC_Glass_Green" },
            { "_M_M_TQ_STONE", "M_PC_Wood_Dark" },
            { "_M_M_TQ_BRICK_RED", "SMALL_M_PC_Brick" },
        };

        private static Dictionary<string, Material> ResolveObjectRules(List<string> errors)
        {
            var index = IndexProjectMaterials(out _);
            var res = new Dictionary<string, Material>(StringComparer.Ordinal);
            for (int i = 0; i < ObjectRules.GetLength(0); i++)
            {
                if (index.TryGetValue(ObjectRules[i, 1], out var m)) res[ObjectRules[i, 0]] = m;
                else errors.Add("правило " + ObjectRules[i, 0] + ": нет материала " + ObjectRules[i, 1]);
            }
            return res;
        }

        private static bool TryObjectRule(string objectName, Dictionary<string, Material> ruleMats, out Material mat, out string suffix)
        {
            foreach (var kv in ruleMats)
                if (objectName.EndsWith(kv.Key, StringComparison.Ordinal)) { mat = kv.Value; suffix = kv.Key; return true; }
            mat = null; suffix = null;
            return false;
        }

        // ------------------------------------------------------------------ отчёт / применение

        /// <summary>
        /// Прогон по всем рендерерам <paramref name="scope"/>. <paramref name="apply"/> = false — dry-run.
        /// <paramref name="tag"/> — суффикс файла отчёта: reports/material_report_&lt;scope&gt;_&lt;tag&gt;.csv.
        /// </summary>
        public static RunStats Run(Transform scope, bool apply, bool includeDoors, string tag)
        {
            var stats = new RunStats { Scope = scope != null ? scope.name : "<null>", Apply = apply };
            if (scope == null)
            {
                stats.Errors.Add("scope == null");
                return stats;
            }

            var map = LoadMap(stats.Errors);
            var targets = ResolveTargets(map, stats.Errors);
            var targetSet = new HashSet<Material>(targets.Values);
            var glow = FindProjectMaterial(GlowMatName);
            if (glow != null) targetSet.Add(glow);

            var buckets = new Dictionary<string, Bucket>(StringComparer.Ordinal);
            var doorCache = new Dictionary<GameObject, bool>();
            var ruleMats = ResolveObjectRules(stats.Errors);
            foreach (var rm in ruleMats.Values) targetSet.Add(rm);
            var renderers = scope.GetComponentsInChildren<Renderer>(true);
            stats.Renderers = renderers.Length;

            int undoGroup = -1;
            if (apply)
            {
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("PRIMUM: материалы — применить (" + scope.name + ")");
            }

            try
            {
                for (int ri = 0; ri < renderers.Length; ri++)
                {
                    var r = renderers[ri];
                    if ((ri & 511) == 0)
                        EditorUtility.DisplayProgressBar("PRIMUM — материалы",
                            scope.name + ": " + ri + " / " + renderers.Length, (float)ri / renderers.Length);

                    var mats = r.sharedMaterials;
                    bool isDoor = IsDoorRenderer(r, doorCache);
                    bool changedHere = false;

                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i];
                        stats.TotalSlots++;
                        string action;
                        string kind = "";
                        string targetName = "";
                        string key = m != null ? m.name : "<null>";

                        if (m == null)
                        {
                            action = "null";
                            stats.Null++;
                        }
                        else if (mats.Length == 1 && !isDoor && TryObjectRule(r.name, ruleMats, out var ruleMat, out var ruleSuffix))
                        {
                            // правило по имени объекта (решение пользователя на пилоте, шаг 2m): сильнее таблицы по имени материала
                            kind = "OBJECT_RULE" + ruleSuffix;
                            targetName = ruleMat.name;
                            if (m == ruleMat) { action = "already"; stats.Already++; }
                            else
                            {
                                action = apply ? "changed" : "would_change";
                                stats.Changed++;
                                if (apply) { mats[i] = ruleMat; changedHere = true; }
                            }
                        }
                        else if (targetSet.Contains(m))
                        {
                            action = "already";
                            kind = "TARGET";
                            targetName = m.name;
                            stats.Already++;
                        }
                        else if (map.TryGetValue(key, out var row))
                        {
                            kind = row.Kind;
                            targetName = row.Target;
                            if (!ApplyKinds.Contains(row.Kind))
                            {
                                action = "skip_" + row.Kind;
                                stats.SkipKind++;
                            }
                            else if (!includeDoors && (isDoor || key.StartsWith(DoorMaterialPrefix, StringComparison.Ordinal)))
                            {
                                action = "skip_doors";
                                stats.SkipDoors++;
                            }
                            else if (!targets.TryGetValue(row.Target, out var t) || t == null)
                            {
                                action = "target_missing";
                                stats.TargetMissing++;
                            }
                            else
                            {
                                action = apply ? "changed" : "would_change";
                                stats.Changed++;
                                if (apply)
                                {
                                    mats[i] = t;
                                    changedHere = true;
                                }
                            }
                        }
                        else
                        {
                            action = "unmapped";
                            stats.Unmapped++;
                        }

                        string bk = key + "|" + action;
                        if (!buckets.TryGetValue(bk, out var b))
                            buckets[bk] = b = new Bucket { Source = key, Kind = kind, Target = targetName, Action = action };
                        b.Slots++;
                        b.Renderers.Add(r);
                    }

                    if (changedHere)
                    {
                        Undo.RecordObject(r, "PRIMUM: материалы");
                        r.sharedMaterials = mats;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                        stats.RenderersChanged++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (apply)
            {
                Undo.CollapseUndoOperations(undoGroup);
                if (stats.RenderersChanged > 0) EditorSceneManager.MarkSceneDirty(scope.gameObject.scene);
            }

            // --- CSV
            var sb = new StringBuilder();
            sb.AppendLine("source;kind;target;action;slots;renderers");
            foreach (var b in buckets.Values.OrderBy(x => ActionOrder(x.Action)).ThenByDescending(x => x.Slots).ThenBy(x => x.Source, StringComparer.Ordinal))
                sb.Append(b.Source).Append(';').Append(b.Kind).Append(';').Append(b.Target).Append(';')
                  .Append(b.Action).Append(';').Append(b.Slots).Append(';').Append(b.Renderers.Count).AppendLine();
            sb.AppendLine("# " + stats);
            if (stats.Errors.Count > 0) sb.AppendLine("# errors: " + string.Join(" | ", stats.Errors));

            string rel = ReportDirRelative + "/material_report_" + SafeName(scope.name) + "_" + tag + ".csv";
            WriteText(rel, sb.ToString());
            stats.ReportPath = rel;

            foreach (var e in stats.Errors) Debug.LogError("[PrimumMaterialMap] " + e);
            return stats;
        }

        private static int ActionOrder(string a)
        {
            switch (a)
            {
                case "changed":
                case "would_change": return 0;
                case "target_missing": return 1;
                case "unmapped": return 2;
                case "already": return 3;
                case "skip_doors": return 4;
                case "null": return 6;
                default: return 5;
            }
        }

        private static bool IsDoorRenderer(Renderer r, Dictionary<GameObject, bool> cache)
        {
            var root = PrefabUtility.GetNearestPrefabInstanceRoot(r.gameObject);
            if (root == null) return false;
            if (cache.TryGetValue(root, out bool v)) return v;
            string p = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) ?? "";
            v = p.Replace('\\', '/').Contains(DoorPrefabFolder);
            cache[root] = v;
            return v;
        }

        // ------------------------------------------------------------------ verify

        /// <summary>Численная проверка §9 по <paramref name="scope"/>. Пишет reports/material_verify_&lt;scope&gt;_&lt;tag&gt;.txt.</summary>
        public static string Verify(Transform scope, string tag)
        {
            var errors = new List<string>();
            var map = LoadMap(errors);
            var targets = ResolveTargets(map, errors);
            var targetSet = new HashSet<Material>(targets.Values);
            foreach (var rm in ResolveObjectRules(errors).Values) targetSet.Add(rm);

            int slots = 0, nullSlots = 0, fbxSlots = 0, instanceSlots = 0, outsideSlots = 0, urpLitSlots = 0, nonGraphSlots = 0;
            var distinct = new HashSet<Material>();
            var unmappedNames = new Dictionary<string, int>(StringComparer.Ordinal);
            var leftByName = new Dictionary<string, int>(StringComparer.Ordinal);
            var urpLitNames = new SortedSet<string>(StringComparer.Ordinal);
            var renderers = scope.GetComponentsInChildren<Renderer>(true);

            foreach (var r in renderers)
            {
                foreach (var m in r.sharedMaterials)
                {
                    slots++;
                    if (m == null) { nullSlots++; continue; }
                    distinct.Add(m);
                    string path = AssetDatabase.GetAssetPath(m);
                    bool isTarget = targetSet.Contains(m) || m.name == GlowMatName;

                    if (string.IsNullOrEmpty(path)) instanceSlots++;
                    else if (IsModelAsset(path)) fbxSlots++;
                    if (string.IsNullOrEmpty(path) || !path.StartsWith(MaterialsRoot + "/", StringComparison.Ordinal)) outsideSlots++;

                    if (m.shader != null && m.shader.name == UrpLitShaderName) { urpLitSlots++; urpLitNames.Add(m.name); }
                    string shPath = m.shader != null ? AssetDatabase.GetAssetPath(m.shader) : "";
                    if (!shPath.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase) && m.name != GlowMatName) nonGraphSlots++;

                    if (!isTarget)
                    {
                        leftByName[m.name] = leftByName.TryGetValue(m.name, out int c) ? c + 1 : 1;
                        if (!map.ContainsKey(m.name))
                            unmappedNames[m.name] = unmappedNames.TryGetValue(m.name, out int u) ? u + 1 : 1;
                    }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("PRIMUM materials verify — scope: " + HierarchyPath(scope) + "  (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")");
            sb.AppendLine("renderers                          : " + renderers.Length);
            sb.AppendLine("slots                              : " + slots);
            sb.AppendLine("null slots                         : " + nullSlots);
            sb.AppendLine("slots from model sub-materials     : " + fbxSlots + "   (DoD: 0)");
            sb.AppendLine("slots on runtime instances (.material): " + instanceSlots + "   (норма: 0)");
            sb.AppendLine("slots outside Assets/_Project/Materials: " + outsideSlots + "   (DoD: 0)");
            sb.AppendLine("slots on URP/Lit                   : " + urpLitSlots + "   (DoD: 0)");
            sb.AppendLine("slots on non-ShaderGraph (кроме " + GlowMatName + "): " + nonGraphSlots);
            sb.AppendLine("distinct Material objects          : " + distinct.Count + "   (DoD: <= 60)");
            sb.AppendLine("names without row in table         : " + unmappedNames.Count + "   (DoD: 0)");
            foreach (var kv in unmappedNames.OrderByDescending(x => x.Value))
                sb.AppendLine("    unmapped: " + kv.Key + " = " + kv.Value);
            sb.AppendLine("remaining non-target names         : " + leftByName.Count + " / slots " + leftByName.Values.Sum());
            foreach (var kv in leftByName.OrderByDescending(x => x.Value))
            {
                string kind = map.TryGetValue(kv.Key, out var row) ? row.Kind + " -> " + (string.IsNullOrEmpty(row.Target) ? "-" : row.Target) : "NO ROW";
                sb.AppendLine("    left: " + kv.Key + " = " + kv.Value + "   [" + kind + "]");
            }
            if (urpLitNames.Count > 0) sb.AppendLine("URP/Lit names: " + string.Join(", ", urpLitNames));
            if (errors.Count > 0) sb.AppendLine("ERRORS: " + string.Join(" | ", errors));

            string text = sb.ToString();
            WriteText(ReportDirRelative + "/material_verify_" + SafeName(scope.name) + "_" + tag + ".txt", text);
            return text;
        }

        private static bool IsModelAsset(string path)
        {
            if (ModelExtensions.Contains(Path.GetExtension(path).ToLowerInvariant())) return true;
            return AssetImporter.GetAtPath(path) is ModelImporter;
        }

        // ------------------------------------------------------------------ цветовые варианты (§5.4)

        private enum Tint { Tone, Hue }

        private sealed class VariantSpec
        {
            public string BasePath;
            public string Name;
            public string Hex;           // ориентир из material_map.csv (доминирующий источник, без плейсхолдеров #CCCCCC)
            public string Main;          // свойство, которому соответствует hex
            public string[] Tone;        // тон + насыщенность + светлота (относительно Main)
            public string[] Hue;         // только оттенок/насыщенность, светлота сохраняется (свечение)
            public float Contrast = 1f;  // <1 — сжать разброс светлоты (крашеные/светлые поверхности)
            public Dictionary<string, float> Floats;
        }

        private const string WoodBase = "Assets/_Project/Materials/Wood/M_PC_Wood.mat";
        private const string BrickBase = "Assets/_Project/Materials/Brick/M_PC_Brick.mat";
        private const string CobbleBase = "Assets/_Project/Materials/Cobble/M_PC_Cobble.mat";
        private const string ConcreteBase = "Assets/_Project/Materials/Concrete/M_PC_Concrete.mat";
        private const string TilesBase = "Assets/_Project/Materials/Tiles/M_PC_Tiles.mat";
        private const string GlassBase = "Assets/_Project/Materials/Glass/EMPTY_COLOR/M_PC_Glass.mat";
        private const string FabricBase = "Assets/_Project/Materials/Fabric/M_PC_Fabric.mat";
        private const string IronBase = "Assets/_Project/Materials/Iron/M_PC_IronRust.mat";
        private const string LanternBase = "Assets/_Project/Materials/Lantern/M_PC_Lantern.mat";

        private static readonly string[] WoodTone = { "_Wood_Color", "_LateWood_Color", "_Grain_Color", "_Pore_Color", "_Wear_Color" };
        private static readonly string[] BrickTone = { "_Brick_Color", "_Brick_Light_Color" };
        private static readonly string[] CobbleTone = { "_Stone_Color", "_Stone_Light_Color" };
        private static readonly string[] ConcreteTone = { "_Concrete_Color", "_Mottle_Color", "_Aggregate_Color", "_Aggregate_Light_Color", "_Wear_Color" };
        private static readonly string[] TilesTone = { "_Tile_Color", "_Tile_Light_Color" };
        private static readonly string[] GlassTone = { "_Glass_Color", "_Murk_Color", "_Murk_Deep_Color" };
        private static readonly string[] FabricTone = { "_Fabric_Color", "_Gap_Color", "_Heather_Color", "_Pilling_Color", "_Wear_Color" };
        private static readonly string[] BronzeTone = { "_Bronze_Color", "_Scratch_Color" };
        private static readonly string[] IronTone = { "_Iron_Color", "_Scratch_Color" };
        private static readonly string[] LanternTone = { "_Glass_Color" };
        private static readonly string[] LanternHue = { "_Glow_Color", "_Core_Color" };

        private static VariantSpec V(string basePath, string name, string hex, string[] tone, float contrast = 1f,
            string[] hue = null, Dictionary<string, float> floats = null)
        {
            return new VariantSpec
            {
                BasePath = basePath, Name = name, Hex = hex, Main = tone[0], Tone = tone,
                Hue = hue ?? new string[0], Contrast = contrast, Floats = floats
            };
        }

        /// <summary>31 вариант из §5.4. Hex — доминирующий по слотам источник таблицы (см. журнал этапа).</summary>
        private static List<VariantSpec> VariantSpecs()
        {
            const float Paint = 0.6f;
            return new List<VariantSpec>
            {
                // Wood — 7
                V(WoodBase, "M_PC_Wood_Dark",  "#3D2616", WoodTone),          // M_WOOD_DARK 4103
                V(WoodBase, "M_PC_Wood_Pine",  "#C1A370", WoodTone),          // M_TQ_PINE (M_TIMBER_FRAME = плейсхолдер #CCCCCC)
                V(WoodBase, "M_PC_Wood_Red",   "#843026", WoodTone, Paint),   // M_TQ_TRIM_R 343
                V(WoodBase, "M_PC_Wood_Blue",  "#324C6D", WoodTone, Paint),   // M_TQ_TRIM_B 519
                V(WoodBase, "M_PC_Wood_Green", "#385642", WoodTone, Paint),   // M_TQ_TRIM_G 497
                V(WoodBase, "M_PC_Wood_White", "#EDEAE2", WoodTone, Paint),   // M_TQ_TRIM_W 1195
                V(WoodBase, "M_PC_Wood_Ochre", "#C6A847", WoodTone, Paint),   // M_TQ_TRIM_Y 405
                // Brick — 2
                V(BrickBase, "M_PC_Brick_Yellow", "#9E653D", BrickTone),      // M_BRICK_YELLOW 37
                V(BrickBase, "M_PC_Brick_Red",    "#8E4C3D", BrickTone),      // M_TQ_BRICK_RED 2
                // Cobble — 3
                V(CobbleBase, "M_PC_Cobble_Granite", "#6B6B72", CobbleTone),  // M_GRANITE 237
                V(CobbleBase, "M_PC_Cobble_Beige",   "#9E8C70", CobbleTone),  // M_STONE_BEIGE 231
                V(CobbleBase, "M_PC_Cobble_Rock",    "#47423F", CobbleTone),  // M_ROCK_DARK 26
                // Concrete — 2
                V(ConcreteBase, "M_PC_Concrete_Limewash", "#CCC1A8", ConcreteTone, Paint), // M_LIME_WASH 60
                V(ConcreteBase, "M_PC_Concrete_Rose",     "#B28C7A", ConcreteTone),        // M_PLASTER_ROSE 29
                // Tiles — 6
                V(TilesBase, "M_PC_Tiles_Bordeaux", "#59211E", TilesTone),    // M_ROOF_BORDEAUX 67
                V(TilesBase, "M_PC_Tiles_Copper",   "#729989", TilesTone),    // M_TQ_R_COPPER
                V(TilesBase, "M_PC_Tiles_Green",    "#425B4C", TilesTone),    // M_TQ_R_GREEN
                V(TilesBase, "M_PC_Tiles_Shingle",  "#563F30", TilesTone),    // M_TQ_R_SHINGLE
                V(TilesBase, "M_PC_Tiles_Slate",    "#495463", TilesTone),    // M_TQ_R_SLATE
                V(TilesBase, "M_PC_Tiles_Steel",    "#656870", TilesTone),    // M_TQ_R_STEEL
                // Glass — 4
                V(GlassBase, "M_PC_Glass_Green", "#265947", GlassTone),       // M_GLASS_GREEN 1099
                V(GlassBase, "M_PC_Glass_Dark",  "#212832", GlassTone),       // M_WINDOW_GLASS 348
                V(GlassBase, "M_PC_Glass_Amber", "#B75B19", GlassTone),       // M_GLASS_STAIN_A 139
                V(GlassBase, "M_PC_Glass_Blue",  "#284C8C", GlassTone),       // M_GLASS_STAIN_B 3
                // Fabric — 4
                V(FabricBase, "M_PC_Fabric_Cream",    "#F3EEE1", FabricTone, Paint), // M_FABRIC_CREAM 43
                V(FabricBase, "M_PC_Fabric_Green",    "#0C301C", FabricTone),        // M_FABRIC_GREEN 9
                V(FabricBase, "M_PC_Fabric_Bordeaux", "#5B231E", FabricTone),        // M_AWNING 17
                V(FabricBase, "M_PC_Fabric_Canvas",   "#AD9975", FabricTone),        // M_CANVAS
                // Bronze — 1
                V(BronzeBasePath, "M_PC_Bronze_Brass", "#B28E47", BronzeTone),       // M_TQ_BRASS 1496
                // IronRust — 1 (свинец: без ржавчины)
                V(IronBase, "M_PC_IronRust_Lead", "#1C1E23", IronTone, 1f, null,
                    new Dictionary<string, float> { { "_Rust_Amount", 0f } }),       // M_LEAD 468
                // Lantern — 1
                V(LanternBase, "M_PC_Lantern_Cool", "#A5CCE5", LanternTone, 1f, LanternHue), // G_COOL 2
            };
        }

        /// <summary>
        /// Идемпотентно: база Bronze (копия архивного пресета, архив не трогается) + отсутствующие варианты.
        /// Существующие варианты НЕ перезаписываются (их могли подстроить руками) — только проверяются.
        /// Пишет reports/variants_build.csv.
        /// </summary>
        public static string BuildVariants()
        {
            var log = new StringBuilder();
            var csv = new StringBuilder();
            csv.AppendLine("name;base;path;status;hex;overrides;shader_error");

            // 1. база Bronze
            if (AssetDatabase.LoadAssetAtPath<Material>(BronzeBasePath) == null)
            {
                if (AssetDatabase.LoadAssetAtPath<Material>(BronzeArchivePath) == null)
                    return "ERROR: нет ни " + BronzeBasePath + ", ни архивного " + BronzeArchivePath;
                bool ok = AssetDatabase.CopyAsset(BronzeArchivePath, BronzeBasePath);
                log.AppendLine((ok ? "created " : "ERROR copy ") + BronzeBasePath + " <- " + BronzeArchivePath);
                csv.AppendLine("M_PC_Bronze;-;" + BronzeBasePath + ";" + (ok ? "created_base" : "error") + ";;;");
                if (!ok) return log.ToString();
            }
            else
            {
                csv.AppendLine("M_PC_Bronze;-;" + BronzeBasePath + ";exists_base;;;");
            }

            int created = 0, exists = 0, failed = 0;
            foreach (var spec in VariantSpecs())
            {
                var baseMat = AssetDatabase.LoadAssetAtPath<Material>(spec.BasePath);
                if (baseMat == null)
                {
                    failed++;
                    log.AppendLine("ERROR нет базы " + spec.BasePath + " для " + spec.Name);
                    continue;
                }

                string dir = Path.GetDirectoryName(spec.BasePath).Replace('\\', '/');
                string path = dir + "/" + spec.Name + ".mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                string status;
                Material v;
                if (existing != null)
                {
                    v = existing;
                    status = existing.parent == baseMat ? "exists" : "exists_PARENT_MISMATCH";
                    exists++;
                }
                else
                {
                    if (!ColorUtility.TryParseHtmlString(spec.Hex, out var target))
                    {
                        failed++;
                        log.AppendLine("ERROR hex " + spec.Hex + " у " + spec.Name);
                        continue;
                    }

                    v = new Material(baseMat) { name = spec.Name };
                    v.parent = baseMat;
                    v.RevertAllPropertyOverrides();
                    ApplyTint(v, baseMat, spec, target);
                    if (spec.Floats != null)
                        foreach (var kv in spec.Floats)
                            if (baseMat.HasFloat(kv.Key)) v.SetFloat(kv.Key, kv.Value);
                    AssetDatabase.CreateAsset(v, path);
                    v = AssetDatabase.LoadAssetAtPath<Material>(path);
                    status = v != null && v.parent == baseMat ? "created" : "created_PARENT_MISMATCH";
                    created++;
                }

                var overridden = new List<string>();
                if (v != null)
                {
                    var sh = v.shader;
                    for (int i = 0; i < sh.GetPropertyCount(); i++)
                    {
                        string pn = sh.GetPropertyName(i);
                        if (v.isVariant && v.IsPropertyOverriden(pn)) overridden.Add(pn);
                    }
                }
                bool shErr = v != null && v.shader != null && ShaderUtil.ShaderHasError(v.shader);
                csv.Append(spec.Name).Append(';').Append(baseMat.name).Append(';').Append(path).Append(';')
                   .Append(status).Append(';').Append(spec.Hex).Append(';').Append(string.Join(",", overridden)).Append(';')
                   .Append(shErr ? "YES" : "no").AppendLine();
                if (status.Contains("MISMATCH") || shErr) failed++;
            }

            AssetDatabase.SaveAssets();
            WriteText(ReportDirRelative + "/variants_build.csv", csv.ToString());
            log.Insert(0, "variants: created " + created + " / exists " + exists + " / problems " + failed + " (всего " + VariantSpecs().Count + ")\n");
            return log.ToString();
        }

        /// <summary>
        /// Перекраска в гамма-пространстве HSV: оттенок — из hex; насыщенность — hex × (S свойства / S главного);
        /// светлота — V_hex × (V свойства / V главного)^Contrast, светлые детали сжимаются под 0.97.
        /// Так сохраняется рисунок пресета (тёмные волокна/швы темнее, светлые блики светлее), меняется тон.
        /// </summary>
        private static void ApplyTint(Material v, Material baseMat, VariantSpec spec, Color target)
        {
            Color.RGBToHSV(baseMat.GetColor(spec.Main), out _, out float ms, out float mv);
            Color.RGBToHSV(target, out float th, out float ts, out float tv);
            mv = Mathf.Max(mv, 1e-3f);

            float hi = 0f;
            foreach (var p in spec.Tone)
            {
                if (!baseMat.HasColor(p)) continue;
                Color.RGBToHSV(baseMat.GetColor(p), out _, out _, out float pv);
                hi = Mathf.Max(hi, pv);
            }

            foreach (var p in spec.Tone)
            {
                if (!baseMat.HasColor(p)) continue;
                var c = baseMat.GetColor(p);
                Color.RGBToHSV(c, out _, out float ps, out float pv);
                float s = ms > 0.02f ? Mathf.Clamp01(ts * ps / ms) : ts;
                float val = tv * Mathf.Pow(Mathf.Max(pv, 1e-4f) / mv, spec.Contrast);
                if (pv > mv && hi > mv + 1e-4f)
                {
                    float ceiling = Mathf.Max(0.97f, tv);
                    float comp = tv + (pv - mv) / (hi - mv) * (ceiling - tv);
                    val = Mathf.Min(val, comp);
                }
                var n = Color.HSVToRGB(th, s, Mathf.Clamp01(val));
                n.a = c.a;
                v.SetColor(p, n);
            }

            foreach (var p in spec.Hue)
            {
                if (!baseMat.HasColor(p)) continue;
                var c = baseMat.GetColor(p);
                Color.RGBToHSV(c, out _, out float ps, out float pv);
                float s = ms > 0.02f ? Mathf.Clamp01(ts * ps / ms) : ts;
                var n = Color.HSVToRGB(th, s, pv, true);
                n.a = c.a;
                v.SetColor(p, n);
            }
        }

        // ------------------------------------------------------------------ утилиты

        private static Material FindProjectMaterial(string name)
        {
            var index = IndexProjectMaterials(out _);
            return index.TryGetValue(name, out var m) ? m : null;
        }

        private static string HierarchyPath(Transform t)
        {
            var parts = new List<string>();
            for (var x = t; x != null; x = x.parent) parts.Add(x.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static string SafeName(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s) sb.Append(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? ch : '_');
            return sb.ToString();
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static void WriteText(string relativePath, string content)
        {
            string full = Path.Combine(ProjectRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
            string dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(full, content, new UTF8Encoding(false));
        }
    }
}
