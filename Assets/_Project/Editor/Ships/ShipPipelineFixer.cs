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
    /// ShipPipelineFixer — T-SHIPPIPE01.
    ///
    /// Зачем тикет:
    ///   1. <c>Ship_Light_root_fbx_test.prefab</c> и <c>Ship_medium_base.prefab</c> держат
    ///      user-созданные деревья коллайдеров (<c>&lt;NODE&gt;_Colliders/Root/BC_0000_*</c>)
    ///      ВНУТРИ nested FBX-инстанса. Это overrides на узлах модели: замена визуала
    ///      (переимпорт FBX / замена модели на другого корабля) их сносит.
    ///      Для heavy этот перенос уже сделан в H1 (<see cref="ShipPrefabRepair"/>),
    ///      здесь он повторяется параметризованно, без heavy-хардкода.
    ///   2. У light-двери <c>CABIN_ENTRY_DOOR_PIVOT</c> и её створки
    ///      <c>CABIN_ENTRY_DOOR_LEAF</c> ОБА несут <see cref="DoorController"/>.
    ///      При <c>doorModel == null</c> контроллер двигает собственный transform,
    ///      поэтому створка едет дважды (пивот + лист аддитивно).
    ///      Плюс на пивоте висит вырожденный <c>MeshCollider</c> без объёма.
    ///   3. Все три корабля сидят на FBX-embedded материалах URP/Lit.
    ///      Тикет переводит их на существующие процедурные Shader Graph палитры
    ///      (<c>Assets/_Project/Materials/*/M_PC_*</c>). Новых «с нуля» материалов нет:
    ///      только цветовые наследники <c>new Material(parent)</c> там, где палитра не имеет
    ///      семантического слота (Steel dark/light/hull/safety, Bronze dark,
    ///      Lantern red/green, Plastic rubber/blue/red).
    ///
    ///      ОГРАНИЧЕНИЕ: это не «Material Variant» в смысле Unity (у них
    ///      <c>Material.parent == null</c>) — в Unity 6000.5 нет публичного/скриптуемого
    ///      API для связывания варианта, <c>m_Parent</c> сериализуется только у уже
    ///      созданного варианта. Материалы полностью независимы, но несут тот же
    ///      Shader Graph, поэтому правка цвета конкретного слота делается на месте.
    ///
    /// ЧЕГО ТИКЕТ НЕ ДЕЛАЕТ (осознанно):
    ///   - не добавляет <c>GlobalSceneSourceMarker</c> в light-префаб. Сценический инстанс
    ///     уже несёт его как added-component override; бейк компонента в префаб дал бы
    ///     дубликат <c>[DisallowMultipleComponent]</c> на сцене. Это требует синхронной
    ///     правки и сохранения WorldScene_0_0 — отдельное решение;
    ///   - не вводит <c>MainVisual</c>-обёртку: имя нигде не читается рантаймом,
    ///     а перенос FBX-инстанса глубже меняет все пути;
    ///   - не сливает визуал и не строит LODGroup (это H2, другое решение по бюджетам);
    ///   - не меняет корневой scale (6, 0.900, 12) и отключённый корневой
    ///     MeshRenderer/MeshFilter (<c>Cube</c>) — они одинаковы на принятом heavy;
    ///   - не трогает сцену.
    ///
    /// Меню: ProjectC → Ships → Pipeline → …
    /// </summary>
    public static class ShipPipelineFixer
    {
        private const string ReportRelativePath = "docs/dev/T-SHIPPIPE01_SHIP_PREFABS.csv";
        private const string BackupRelativeDir = "_local_backups/T-SHIPPIPE01";
        private const string CollidersRootName = "SHIP_COLLIDERS";
        /// <summary>
        /// Допуск сравнения поз коллайдеров в КООРДИНАТАХ prefab-корня.
        ///
        /// Почему не 1e-4 как в H1: корень light-префаба авторится на мировых
        /// ~4.0e4 (40122, 2502, 39975), а float даёт там шаг ≈ 5e-3. Любой
        /// перенос через мировое пространство даёт ~6e-4 в координатах корня.
        /// Гейт должен ловить реальную поломку (сбитый пивот, потерянный поворот —
        /// это метры), а не шум округления.
        /// </summary>
        private const float PoseTolerance = 5e-3f;

        // =====================================================================
        // Корабли
        // =====================================================================

        private sealed class ShipDef
        {
            public string Key;
            public string PrefabPath;
            /// <summary>Структурный ремонт (коллайдеры/двери) применяется только там, где он нужен.</summary>
            public bool StructureFix;
            /// <summary>Ожидаемое число DoorController после дедупликации; -1 = не проверять.</summary>
            public int ExpectedDoors = -1;
        }

        private static readonly ShipDef[] Ships =
        {
            new ShipDef
            {
                Key = "light",
                PrefabPath = "Assets/_Project/Prefabs/Ships/Ship_Light_root_fbx_test.prefab",
                StructureFix = true,
                ExpectedDoors = 1
            },
            new ShipDef
            {
                Key = "medium",
                PrefabPath = "Assets/_Project/Prefabs/Ships/Ship_medium_base.prefab",
                StructureFix = true,
                ExpectedDoors = 1
            },
            new ShipDef
            {
                Key = "heavy",
                PrefabPath = "Assets/_Project/Prefabs/Ships/Ship_havyII_base.prefab",
                StructureFix = false,
                ExpectedDoors = 9
            },
        };

        // =====================================================================
        // Правила перевода материалов
        // =====================================================================

        private struct PropColor
        {
            public string Prop;
            public Color Value;
            public PropColor(string prop, float r, float g, float b)
            {
                Prop = prop;
                Value = new Color(r, g, b, 1f);
            }
        }

        private sealed class MatRule
        {
            /// <summary>Подстроки имени исходного материала (lowercase). Порядок правил важен: специфичные выше общих.</summary>
            public string[] Tokens;
            /// <summary>Имя целевого материала палитры без .mat.</summary>
            public string Target;
            /// <summary>Родитель варианта; null → материал должен уже существовать в палитре.</summary>
            public string ParentPath;
            public PropColor[] Colors;
        }

        private static readonly MatRule[] MaterialRules =
        {
            // --- специфичные Steel-варианты (проверяются раньше общего _steel) ---
            Rule("safety_yellow", "M_PC_Steel_v2_Safety",
                "Assets/_Project/Materials/Steel/M_PC_Steel_v2.mat",
                P("_Steel_Color", 0.865f, 0.650f, 0.221f),
                P("_Worn_Color", 0.930f, 0.780f, 0.430f)),

            Rule("_steel_light", "M_PC_Steel_v2_Light",
                "Assets/_Project/Materials/Steel/M_PC_Steel_v2.mat",
                P("_Steel_Color", 0.618f, 0.650f, 0.658f),
                P("_Worn_Color", 0.760f, 0.790f, 0.800f)),

            Rule("metal_steel_dark", "M_PC_Steel_v2_Dark",
                "Assets/_Project/Materials/Steel/M_PC_Steel_v2.mat",
                P("_Steel_Color", 0.332f, 0.349f, 0.381f),
                P("_Worn_Color", 0.470f, 0.490f, 0.530f)),

            Rule("_painted_hull_dark", "M_PC_Steel_v2_Hull_Dark",
                "Assets/_Project/Materials/Steel/M_PC_Steel_v2.mat",
                P("_Steel_Color", 0.206f, 0.260f, 0.276f),
                P("_Worn_Color", 0.330f, 0.390f, 0.410f)),

            Rule("_painted_hull", "M_PC_Steel_v2_Hull",
                "Assets/_Project/Materials/Steel/M_PC_Steel_v2.mat",
                P("_Steel_Color", 0.304f, 0.437f, 0.473f),
                P("_Worn_Color", 0.430f, 0.570f, 0.600f)),

            // --- общий металл корпуса ---
            Rule("_steel", "M_PC_Steel_v2", null),
            Rule("lit", "M_PC_Steel_v2", null),

            // --- бронза / латунь ---
            Rule("metal_bronze_dark", "M_PC_Bronze_Dark",
                "Assets/_Project/Materials/Bronze/M_PC_Bronze.mat",
                P("_Bronze_Color", 0.517f, 0.455f, 0.323f),
                P("_Scratch_Color", 0.700f, 0.630f, 0.470f)),

            Rule("metal_brass", "M_PC_Bronze_Brass", null),
            Rule("metal_bronze", "M_PC_Bronze_Brass", null),
            Rule("_bronze", "M_PC_Bronze", null),

            // --- ржавчина / сажа ---
            Rule("metal_rust_worn", "M_PC_IronRust", null),
            Rule("smoke_soot", "M_PC_IronRust", null),

            // --- листовой металл ---
            Rule("deck_plating", "M_PC_Plate", null),
            Rule("metal_reflector", "M_PC_Plate", null),

            // --- стекло ---
            Rule("glass_green", "M_PC_Glass_Green", null),
            Rule("_cabin_glass", "M_PC_Glass_Green", null),

            // --- лампы ---
            Rule("lamp_red", "M_PC_Lantern_Red",
                "Assets/_Project/Materials/Lantern/M_PC_Lantern.mat",
                P("_Glow_Color", 0.955f, 0.248f, 0.248f),
                P("_Core_Color", 1.000f, 0.720f, 0.720f),
                P("_Soot_Color", 0.180f, 0.070f, 0.070f)),

            Rule("lamp_green", "M_PC_Lantern_Green",
                "Assets/_Project/Materials/Lantern/M_PC_Lantern.mat",
                P("_Glow_Color", 0.248f, 0.955f, 0.485f),
                P("_Core_Color", 0.720f, 1.000f, 0.800f),
                P("_Soot_Color", 0.070f, 0.150f, 0.090f)),

            Rule("lamp_white", "M_PC_Lantern_Cool", null),
            Rule("_gravity_core", "M_PC_Lantern_Cool", null),
            Rule("antigrav_emitter", "M_PC_Lantern_Cool", null),
            Rule("_worklight", "M_PC_Lantern", null),
            Rule("glass_lamp_warm", "M_PC_Lantern", null),

            // --- резина / кабели ---
            Rule("rubber", "M_PC_Plastic_Rubber",
                "Assets/_Project/Materials/Plastic/M_PC_Plastic.mat",
                P("_Plastic_Color", 0.172f, 0.190f, 0.190f),
                P("_Chalk_Color", 0.300f, 0.320f, 0.320f),
                P("_Grime_Color", 0.100f, 0.110f, 0.110f),
                P("_Scuff_Color", 0.230f, 0.250f, 0.250f)),

            Rule("_cable_insulation", "M_PC_Plastic_Rubber",
                "Assets/_Project/Materials/Plastic/M_PC_Plastic.mat",
                P("_Plastic_Color", 0.235f, 0.272f, 0.283f),
                P("_Chalk_Color", 0.360f, 0.390f, 0.400f),
                P("_Grime_Color", 0.130f, 0.150f, 0.160f),
                P("_Scuff_Color", 0.280f, 0.310f, 0.320f)),

            Rule("_cable_blue", "M_PC_Plastic_Blue",
                "Assets/_Project/Materials/Plastic/M_PC_Plastic.mat",
                P("_Plastic_Color", 0.152f, 0.381f, 0.527f),
                P("_Chalk_Color", 0.350f, 0.550f, 0.680f),
                P("_Grime_Color", 0.080f, 0.150f, 0.220f),
                P("_Scuff_Color", 0.220f, 0.450f, 0.600f)),

            Rule("_cable_red", "M_PC_Plastic_Red",
                "Assets/_Project/Materials/Plastic/M_PC_Plastic.mat",
                P("_Plastic_Color", 0.566f, 0.172f, 0.143f),
                P("_Chalk_Color", 0.750f, 0.400f, 0.360f),
                P("_Grime_Color", 0.250f, 0.070f, 0.060f),
                P("_Scuff_Color", 0.700f, 0.280f, 0.240f)),

            // --- ткань ---
            Rule("_canvas", "M_PC_Fabric_Canvas", null),
        };

        private static MatRule Rule(string token, string target, string parentPath, params PropColor[] colors)
        {
            return new MatRule
            {
                Tokens = new[] { token },
                Target = target,
                ParentPath = parentPath,
                Colors = colors
            };
        }

        private static PropColor P(string prop, float r, float g, float b)
        {
            return new PropColor(prop, r, g, b);
        }

        /// <summary>
        /// heavy заменяет ВСЮ бронзу на дочку <c>M_PC_Plate</c> (решение пользователя).
        /// Свойства наследуются от родителя без правок цвета — тон бронзы теперь
        /// задаётся палитрой Plate (<c>_Plate_Color</c> = 0.500/0.320/0.160),
        /// а не прежними яркими FBX-цветами. Отдельный материал нужен, чтобы
        /// бронзу можно было тюнить, не трогая <c>Deck_Plating</c> и
        /// <c>Metal_Reflector</c>, которые используют сам <c>M_PC_Plate</c>.
        /// </summary>
        private static readonly MatRule PlateBronze = Rule(
            "plate_bronze", "M_PC_Plate_Bronze",
            "Assets/_Project/Materials/Plate/M_PC_Plate.mat");

        /// <summary>
        /// По-корабельные переопределения правил. Ключ — токен имени исходного материала,
        /// проверяется в том же порядке, что и <see cref="MaterialRules"/>.
        /// </summary>
        private static readonly Dictionary<string, Dictionary<string, MatRule>> ShipMaterialOverrides =
            new Dictionary<string, Dictionary<string, MatRule>>
            {
                {
                    "heavy", new Dictionary<string, MatRule>
                    {
                        { "metal_bronze_dark", PlateBronze },
                        { "metal_bronze", PlateBronze },
                        { "metal_brass", PlateBronze },
                        { "_bronze", PlateBronze },
                    }
                }
            };

        // =====================================================================
        // Меню
        // =====================================================================

        [MenuItem("ProjectC/Ships/Pipeline/Analyze (read-only)", false, 200)]
        public static void MenuAnalyze()
        {
            var rows = new List<Row>();
            try
            {
                foreach (var ship in Ships) AnalyzeShip(ship, rows);
                WriteReport(rows);
                Debug.Log("[T-SHIPPIPE01] Analyze: " + rows.Count + " строк → " + AbsoluteReportPath());
            }
            catch (Exception e)
            {
                Debug.LogError("[T-SHIPPIPE01] Analyze failed: " + e);
            }
        }

        [MenuItem("ProjectC/Ships/Pipeline/Fix Structure (light+medium)", false, 201)]
        public static void MenuFixStructure()
        {
            if (!EditorUtility.DisplayDialog("T-SHIPPIPE01",
                    "Структурный ремонт light/medium префабов:\n" +
                    "· вынести user-деревья *_Colliders из FBX-инстанса в SHIP_COLLIDERS;\n" +
                    "· убрать дублирующий DoorController и вырожденный MeshCollider у двери.\n\n" +
                    "Бэкапы пишутся в " + BackupRelativeDir + ". Продолжить?",
                    "Применить", "Отмена"))
                return;

            ApplyStructure();
        }

        [MenuItem("ProjectC/Ships/Pipeline/Assign Palette Materials (all ships)", false, 202)]
        public static void MenuAssignPalette()
        {
            if (!EditorUtility.DisplayDialog("T-SHIPPIPE01",
                    "Перевести FBX-материалы всех трёх кораблей на процедурную палитру.\n" +
                    "Отсутствующие цветовые слоты будут созданы как Material-варианты " +
                    "(Material(parent)), новых базовых материалов не создаётся.\n\n" +
                    "Бэкапы пишутся в " + BackupRelativeDir + ". Продолжить?",
                    "Применить", "Отмена"))
                return;

            ApplyPalette();
        }

        [MenuItem("ProjectC/Ships/Pipeline/Verify (read-only)", false, 203)]
        public static void MenuVerify()
        {
            var rows = new List<Row>();
            try
            {
                foreach (var ship in Ships) VerifyShip(ship, rows);
                WriteReportKeep(rows);
                int problems = 0;
                foreach (var r in rows) if (r.Phase == "verify_failed") problems++;
                if (problems == 0) Debug.Log("[T-SHIPPIPE01] Verify OK → " + AbsoluteReportPath());
                else Debug.LogError("[T-SHIPPIPE01] Verify: " + problems + " проблем → " + AbsoluteReportPath());
            }
            catch (Exception e)
            {
                Debug.LogError("[T-SHIPPIPE01] Verify failed: " + e);
            }
        }

        [MenuItem("ProjectC/Ships/Pipeline/Rollback from backups", false, 220)]
        public static void MenuRollback()
        {
            if (!EditorUtility.DisplayDialog("T-SHIPPIPE01 rollback",
                    "Восстановить все три префаба кораблей из бэкапов?", "Восстановить", "Отмена"))
                return;

            int restored = 0;
            foreach (var ship in Ships)
            {
                string backup = AbsoluteProjectPath(Path.Combine(BackupRelativeDir, Path.GetFileName(ship.PrefabPath) + ".bak"));
                if (!File.Exists(backup)) { Debug.LogWarning("[T-SHIPPIPE01] Нет бэкапа: " + backup); continue; }
                File.Copy(backup, AbsoluteProjectPath(ship.PrefabPath), true);
                AssetDatabase.ImportAsset(ship.PrefabPath, ImportAssetOptions.ForceUpdate);
                restored++;
            }
            Debug.Log("[T-SHIPPIPE01] Восстановлено из бэкапов: " + restored);
        }

        // =====================================================================
        // Analyze
        // =====================================================================

        private static void AnalyzeShip(ShipDef ship, List<Row> rows)
        {
            var root = PrefabUtility.LoadPrefabContents(ship.PrefabPath);
            try
            {
                string phase = "analyze/" + ship.Key;
                rows.Add(Row.Info(phase, "ship", ship.PrefabPath, "", "root scale=" + V(root.transform.localScale)));

                // --- модель-инстанс (прямой ребёнок prefab-корня) ---
                var model = FindDirectModelInstance(root);
                rows.Add(Row.Info(phase, "model_instance", model != null ? model.name : "<none>", "",
                    model != null ? SourcePathOf(model) : "прямого FBX-инстанса нет"));

                // --- user-деревья коллайдеров ---
                var trees = CollectUserColliderTrees(root);
                rows.Add(Row.Info(phase, "summary", "user_collider_trees", "", trees.Count.ToString(CultureInfo.InvariantCulture)));
                foreach (var t in trees)
                {
                    int colliders = t.GetComponentsInChildren<Collider>(true).Length;
                    rows.Add(Row.Info(phase, "collider_tree", PathOf(t), "",
                        "colliders=" + colliders + " insideModelInstance=" + (IsInsideAnyModelInstance(root, t) ? "yes" : "no")));
                }

                // --- двери ---
                var doors = root.GetComponentsInChildren<DoorController>(true);
                rows.Add(Row.Info(phase, "summary", "door_controllers", "",
                    doors.Length.ToString(CultureInfo.InvariantCulture) +
                    (ship.ExpectedDoors >= 0 ? " (ожидается " + ship.ExpectedDoors + ")" : "")));
                foreach (var dc in doors)
                {
                    var t = dc.transform;
                    var ownerCollider = t.GetComponent<Collider>();
                    rows.Add(Row.Info(phase, "door", PathOf(t), "",
                        "parentHasDoorController=" + (t.parent != null && t.parent.GetComponent<DoorController>() != null ? "YES" : "no") +
                        " colliderHere=" + (ownerCollider != null ? ownerCollider.GetType().Name : "<none>") +
                        " degenerate=" + (IsDegenerate(ownerCollider) ? "YES" : "no")));
                }

                // --- вырожденные коллайдеры вне дверей ---
                foreach (var c in root.GetComponentsInChildren<Collider>(true))
                {
                    if (!IsDegenerate(c)) continue;
                    rows.Add(Row.Info(phase, "degenerate_collider", PathOf(c.transform), "",
                        c.GetType().Name + " bounds=" + V(c.bounds.size)));
                }

                // --- материалы ---
                AnalyzeMaterials(root, ship.Key, phase, rows);

                // --- инварианты ---
                rows.Add(Row.Info(phase, "summary", "mesh_renderers", "",
                    root.GetComponentsInChildren<MeshRenderer>(true).Length.ToString(CultureInfo.InvariantCulture)));
                rows.Add(Row.Info(phase, "summary", "colliders", "",
                    root.GetComponentsInChildren<Collider>(true).Length.ToString(CultureInfo.InvariantCulture)));
                rows.Add(Row.Info(phase, "summary", "ship_colliders_root", "",
                    FindChild(root.transform, CollidersRootName) != null ? "present" : "absent"));
                rows.Add(Row.Info(phase, "summary", "global_scene_source_marker", "",
                    root.GetComponent<ProjectC.World.FloatingOrigin.Network.GlobalSceneSourceMarker>() != null ? "present" : "absent"));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AnalyzeMaterials(GameObject root, string shipKey, string phase, List<Row> rows)
        {
            var seen = new Dictionary<string, int>();
            var resolved = new Dictionary<string, string>();
            var status = new Dictionary<string, string>();
            int slots = 0, nullSlots = 0, unmapped = 0;

            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach (var m in mr.sharedMaterials)
                {
                    slots++;
                    if (m == null) { nullSlots++; continue; }
                    string name = m.name;
                    if (!seen.ContainsKey(name)) seen[name] = 0;
                    seen[name]++;

                    if (resolved.ContainsKey(name)) continue;

                    // 1) переопределение корабля — сильнее любого общего правила
                    var overrideRule = MatchShipOverride(name, shipKey);
                    if (overrideRule != null)
                    {
                        resolved[name] = overrideRule.Target;
                        status[name] = "ПЕРЕОПРЕДЕЛЕНИЕ корабля";
                        continue;
                    }

                    // 2) уже палитровый материал не переводим повторно
                    if (IsPaletteMaterial(m))
                    {
                        resolved[name] = null;
                        status[name] = "уже палитровый — не трогаем";
                        continue;
                    }

                    var rule = MatchRule(name);
                    if (rule == null)
                    {
                        resolved[name] = null;
                        status[name] = "UNMAPPED";
                        unmapped++;
                        continue;
                    }
                    resolved[name] = rule.Target;
                    string existing = FindPaletteMaterialPath(rule.Target);
                    status[name] = existing != null ? "exists → " + existing : "variant создаётся";
                }
            }

            rows.Add(Row.Info(phase, "summary", "material_slots", "",
                slots.ToString(CultureInfo.InvariantCulture) + " (null=" + nullSlots + ", unmapped_unique=" + unmapped + ")"));

            var keys = new List<string>(seen.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var k in keys)
            {
                rows.Add(Row.Info(phase, "material", k, resolved[k] ?? "",
                    "slots=" + seen[k] + " | " + status[k]));
            }
        }

        // =====================================================================
        // Fix Structure
        // =====================================================================

        private static void ApplyStructure()
        {
            var rows = new List<Row>();
            int applied = 0;
            try
            {
                foreach (var ship in Ships)
                {
                    if (!ship.StructureFix)
                    {
                        rows.Add(Row.Info("applied/" + ship.Key, "ship", ship.PrefabPath, "", "структурный ремонт не применяется к этому кораблю"));
                        continue;
                    }
                    if (ApplyStructureToShip(ship, rows)) applied++;
                }

                WriteReportKeep(rows);
                Debug.Log("[T-SHIPPIPE01] Fix Structure: обновлено префабов " + applied + " → " + AbsoluteReportPath());
            }
            catch (Exception e)
            {
                rows.Add(Row.Info("failed", "exception", e.GetType().Name, "", e.Message));
                WriteReportKeep(rows);
                Debug.LogError("[T-SHIPPIPE01] Fix Structure failed: " + e);
                throw;
            }
        }

        private static bool ApplyStructureToShip(ShipDef ship, List<Row> rows)
        {
            string phase = "applied/" + ship.Key;
            Backup(ship.PrefabPath);

            var root = PrefabUtility.LoadPrefabContents(ship.PrefabPath);
            bool saved = false;
            try
            {
                // ---------------- 1. Коллайдеры ----------------
                Transform collidersRoot = FindChild(root.transform, CollidersRootName);
                var trees = CollectUserColliderTrees(root);
                var insideTrees = new List<Transform>();
                foreach (var t in trees)
                    if (IsInsideAnyModelInstance(root, t)) insideTrees.Add(t);

                if (insideTrees.Count > 0)
                {
                    if (collidersRoot == null)
                    {
                        var go = new GameObject(CollidersRootName);
                        go.transform.SetParent(root.transform, false);
                        go.transform.localPosition = Vector3.zero;
                        go.transform.localRotation = Quaternion.identity;
                        go.transform.localScale = Vector3.one;
                        collidersRoot = go.transform;
                        rows.Add(Row.Info(phase, "structure", CollidersRootName, root.name, "создан корень SHIP_COLLIDERS"));
                    }

                    // safety-gate: двигаем только чистые держатели коллайдеров
                    foreach (var t in insideTrees)
                    {
                        string why = ValidateMovable(t.gameObject);
                        if (why != null)
                            throw new InvalidOperationException(
                                "Отказ: '" + PathOf(t) + "' содержит не только коллайдеры (" + why + ").");
                    }

                    var before = SnapshotColliderPoses(root);
                    foreach (var t in insideTrees)
                    {
                        int cc = t.GetComponentsInChildren<Collider>(true).Length;
                        string from = PathOf(t);
                        t.SetParent(collidersRoot, true);
                        rows.Add(Row.Info(phase, "moved", from, PathOf(collidersRoot), "colliders=" + cc));
                    }
                    var after = SnapshotColliderPoses(root);
                    string poseProblem = CompareSnapshots(before, after);
                    if (poseProblem != null)
                        throw new InvalidOperationException("Геометрия коллайдеров изменилась при переносе: " + poseProblem);
                }
                else
                {
                    rows.Add(Row.Info(phase, "colliders", "", "", "user-деревьев внутри FBX нет — шаг пропущен"));
                }

                // ---------------- 2. Дверь ----------------
                FixDuplicateDoors(root, phase, rows);

                // ---------------- 3. Валидация в памяти ----------------
                var problems = new List<string>();
                int leftover = 0;
                foreach (var t in CollectUserColliderTrees(root))
                    if (IsInsideAnyModelInstance(root, t)) leftover++;
                if (leftover != 0) problems.Add("внутри FBX остались user-деревья коллайдеров: " + leftover);

                var doorsNow = root.GetComponentsInChildren<DoorController>(true);
                if (ship.ExpectedDoors >= 0 && doorsNow.Length != ship.ExpectedDoors)
                    problems.Add("DoorController: " + doorsNow.Length + " вместо " + ship.ExpectedDoors);

                foreach (var dc in doorsNow)
                {
                    var t = dc.transform;
                    if (FindAncestorDoor(t) != null)
                        problems.Add("осталась вложенная пара DoorController: " + PathOf(t));
                    if (t.GetComponent<Collider>() == null && FindColliderBelow(t) == null)
                        problems.Add("у двери нет коллайдера ни на узле, ни ниже: " + PathOf(t));
                }

                if (problems.Count > 0)
                {
                    foreach (var p in problems) Debug.LogError("[T-SHIPPIPE01] " + ship.Key + ": " + p);
                    rows.Add(Row.Info("aborted/" + ship.Key, "validation", ship.PrefabPath, "", string.Join(" | ", problems)));
                    return false;
                }

                // ---------------- 4. Сохранение ----------------
                bool changed = false;
                foreach (var r in rows) if (r.Phase == phase && (r.Category == "moved" || r.Category == "door_fix")) changed = true;
                if (!changed)
                {
                    rows.Add(Row.Info(phase, "save", ship.PrefabPath, "", "изменений нет — файл не перезаписан"));
                    return false;
                }

                PrefabUtility.SaveAsPrefabAsset(root, ship.PrefabPath);
                saved = true;
                rows.Add(Row.Info(phase, "save", ship.PrefabPath, "", "SaveAsPrefabAsset OK"));
                return true;
            }
            catch (Exception e)
            {
                rows.Add(Row.Info(saved ? "failed_after_save/" + ship.Key : "failed/" + ship.Key,
                    "exception", e.GetType().Name, "", e.Message));
                Debug.LogError("[T-SHIPPIPE01] " + ship.Key + " structure failed" +
                               (saved ? " (префаб уже сохранён — см. rollback)" : " (не сохранён)") + ": " + e);
                throw;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Убирает двойное движение створки.
        ///
        /// <see cref="DoorController"/> при <c>doorModel == null</c> двигает СВОЙ transform,
        /// то есть весь поддерево. Если контроллер висит и на пивоте, и на створке-ребёнке,
        /// створка едет дважды (движение предка + собственное).
        ///
        /// Остаётся ВЕРХНИЙ контроллер: он везёт и створку, и собственный коллайдер
        /// (на light это MeshCollider проёма у <c>CABIN_ENTRY_DOOR_PIVOT</c>). Если убрать
        /// верхний, проём перестал бы двигаться и перекрытие осталось бы на месте.
        /// Коллайдеры не удаляются: у створки остаётся её BoxCollider для взаимодействия.
        /// </summary>
        private static void FixDuplicateDoors(GameObject root, string phase, List<Row> rows)
        {
            var doomed = new List<DoorController>();

            foreach (var dc in root.GetComponentsInChildren<DoorController>(true))
            {
                if (dc == null) continue;
                var ancestor = FindAncestorDoor(dc.transform);
                if (ancestor == null) continue;
                doomed.Add(dc);
                rows.Add(Row.Info(phase, "door_fix", PathOf(dc.transform), PathOf(ancestor.transform),
                    "снят вложенный DoorController: движение уже выполняется предком"));
            }

            foreach (var dc in doomed)
                UnityEngine.Object.DestroyImmediate(dc);
        }

        private static DoorController FindAncestorDoor(Transform t)
        {
            for (var p = t.parent; p != null; p = p.parent)
            {
                var dc = p.GetComponent<DoorController>();
                if (dc != null) return dc;
            }
            return null;
        }

        // =====================================================================
        // Assign Palette
        // =====================================================================

        private static void ApplyPalette()
        {
            var rows = new List<Row>();
            try
            {
                foreach (var ship in Ships) ApplyPaletteToShip(ship, rows);
                WriteReportKeep(rows);
                Debug.Log("[T-SHIPPIPE01] Assign Palette готово → " + AbsoluteReportPath());
            }
            catch (Exception e)
            {
                rows.Add(Row.Info("failed", "exception", e.GetType().Name, "", e.Message));
                WriteReportKeep(rows);
                Debug.LogError("[T-SHIPPIPE01] Assign Palette failed: " + e);
                throw;
            }
        }

        private static void ApplyPaletteToShip(ShipDef ship, List<Row> rows)
        {
            string phase = "applied/" + ship.Key + "/palette";
            Backup(ship.PrefabPath);

            var root = PrefabUtility.LoadPrefabContents(ship.PrefabPath);
            bool saved = false;
            try
            {
                int renderers = 0, slots = 0, unmapped = 0;
                var unmappedNames = new HashSet<string>();
                var cache = new Dictionary<string, Material>();

                foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mats = mr.sharedMaterials;
                    if (mats == null || mats.Length == 0) continue;

                    bool dirty = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var src = mats[i];
                        if (src == null) continue;                       // отключённый корневой Cube — <null>, не трогаем

                        // 1) Переопределение корабля применяется и к уже палитровым материалам
                        //    (смена решения: heavy переводит всю бронзу на дочку Plate).
                        var rule = MatchShipOverride(src.name, ship.Key);
                        if (rule == null)
                        {
                            // 2) Уже палитровый материал повторно не переводим: имена палитры
                            //    (M_PC_Bronze_Brass, M_PC_Steel_v2_Dark…) содержат те же токены,
                            //    что и FBX-источники, и без этого стража Steel-варианты
                            //    схлопнулись бы в базовый _steel.
                            if (IsPaletteMaterial(src)) continue;

                            rule = MatchRule(src.name);
                            if (rule == null)
                            {
                                unmapped++;
                                unmappedNames.Add(src.name);
                                continue;
                            }
                        }

                        var target = ResolveTarget(rule, cache, phase, rows);
                        if (target == null) continue;
                        if (ReferenceEquals(mats[i], target)) continue;

                        mats[i] = target;
                        dirty = true;
                        slots++;
                    }

                    if (dirty) { mr.sharedMaterials = mats; renderers++; }
                }

                if (unmapped > 0)
                {
                    var list = new List<string>(unmappedNames);
                    list.Sort(StringComparer.Ordinal);
                    rows.Add(Row.Info(phase, "unmapped", string.Join(";", list), "",
                        "слотов без правила: " + unmapped + " — оставлены как есть"));
                }

                rows.Add(Row.Info(phase, "summary", ship.PrefabPath, "",
                    "renderers=" + renderers + ", slots=" + slots + ", unmapped=" + unmapped));

                if (slots == 0)
                {
                    rows.Add(Row.Info(phase, "save", ship.PrefabPath, "", "менять нечего — файл не перезаписан"));
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(root, ship.PrefabPath);
                saved = true;
                rows.Add(Row.Info(phase, "save", ship.PrefabPath, "", "SaveAsPrefabAsset OK"));
            }
            catch (Exception e)
            {
                rows.Add(Row.Info(saved ? "failed_after_save/" + ship.Key : "failed/" + ship.Key,
                    "exception", e.GetType().Name, "", e.Message));
                Debug.LogError("[T-SHIPPIPE01] " + ship.Key + " palette failed" +
                               (saved ? " (префаб уже сохранён — см. rollback)" : " (не сохранён)") + ": " + e);
                throw;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Material ResolveTarget(MatRule rule, Dictionary<string, Material> cache, string phase, List<Row> rows)
        {
            if (cache.TryGetValue(rule.Target, out var cached)) return cached;

            string existing = FindPaletteMaterialPath(rule.Target);
            if (existing != null)
            {
                var m0 = AssetDatabase.LoadAssetAtPath<Material>(existing);
                if (m0 == null) throw new InvalidOperationException("Палитровый материал не загрузился: " + existing);
                cache[rule.Target] = m0;
                return m0;
            }

            if (rule.ParentPath == null)
                throw new InvalidOperationException(
                    "Материал палитры отсутствует и не имеет родителя для варианта: " + rule.Target);

            var parent = AssetDatabase.LoadAssetAtPath<Material>(rule.ParentPath);
            if (parent == null)
                throw new InvalidOperationException("Нет родителя варианта: " + rule.ParentPath);

            var variant = new Material(parent) { name = rule.Target };
            if (rule.Colors != null)
            {
                foreach (var pc in rule.Colors)
                {
                    if (!variant.HasProperty(pc.Prop))
                        throw new InvalidOperationException(
                            "У '" + rule.Target + "' нет свойства " + pc.Prop + " (шейдер " + variant.shader.name + ")");
                    variant.SetColor(pc.Prop, pc.Value);
                }
            }

            string dir = Path.GetDirectoryName(rule.ParentPath).Replace('\\', '/');
            string assetPath = dir + "/" + rule.Target + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(assetPath) != null)
                throw new InvalidOperationException("Путь варианта уже занят: " + assetPath);

            AssetDatabase.CreateAsset(variant, assetPath);
            AssetDatabase.SaveAssets();

            rows.Add(Row.Info(phase, "variant_created", assetPath, rule.ParentPath,
                "цветовой наследник палитры (тот же Shader Graph)"));
            cache[rule.Target] = variant;
            return variant;
        }

        // =====================================================================
        // Verify
        // =====================================================================

        private static void VerifyShip(ShipDef ship, List<Row> rows)
        {
            string phase = "verify/" + ship.Key;
            var root = PrefabUtility.LoadPrefabContents(ship.PrefabPath);
            try
            {
                int leftover = 0;
                foreach (var t in CollectUserColliderTrees(root))
                    if (IsInsideAnyModelInstance(root, t)) leftover++;
                AddCheck(rows, phase, "collider_trees_inside_fbx", leftover == 0, leftover.ToString(CultureInfo.InvariantCulture));

                var doors = root.GetComponentsInChildren<DoorController>(true);
                bool doorCountOk = ship.ExpectedDoors < 0 || doors.Length == ship.ExpectedDoors;
                AddCheck(rows, phase, "door_controllers", doorCountOk, doors.Length.ToString(CultureInfo.InvariantCulture));

                int nestedPairs = 0;
                foreach (var dc in doors)
                    if (FindAncestorDoor(dc.transform) != null) nestedPairs++;
                AddCheck(rows, phase, "nested_door_controllers", nestedPairs == 0, nestedPairs.ToString(CultureInfo.InvariantCulture));

                int degenerate = 0;
                foreach (var c in root.GetComponentsInChildren<Collider>(true))
                    if (IsDegenerate(c)) degenerate++;
                AddCheck(rows, phase, "degenerate_colliders", degenerate == 0, degenerate.ToString(CultureInfo.InvariantCulture));

                int lit = 0;
                var remaining = new HashSet<string>();
                foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
                    foreach (var m in mr.sharedMaterials)
                    {
                        if (m == null) continue;
                        if (m.shader != null && m.shader.name == "Universal Render Pipeline/Lit") { lit++; remaining.Add(m.name); }
                    }
                AddCheck(rows, phase, "urp_lit_slots", lit == 0, lit.ToString(CultureInfo.InvariantCulture));
                if (remaining.Count > 0)
                {
                    var list = new List<string>(remaining);
                    list.Sort(StringComparer.Ordinal);
                    rows.Add(Row.Info(phase, "remaining_lit", string.Join(";", list), "", "материалы, ещё не переведённые на палитру"));
                }

                int missingScripts = 0;
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true)) if (mb == null) missingScripts++;
                AddCheck(rows, phase, "missing_scripts", missingScripts == 0, missingScripts.ToString(CultureInfo.InvariantCulture));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AddCheck(List<Row> rows, string phase, string metric, bool ok, string detail)
        {
            rows.Add(Row.Info(ok ? phase : "verify_failed", "check", metric, ok ? "PASS" : "FAIL", detail));
        }

        // =====================================================================
        // Материалы: подбор правила
        // =====================================================================

        private static MatRule MatchRule(string materialName, string shipKey = null)
        {
            if (string.IsNullOrEmpty(materialName)) return null;
            string n = materialName.ToLowerInvariant();
            foreach (var rule in MaterialRules)
            {
                foreach (var token in rule.Tokens)
                {
                    if (n.IndexOf(token, StringComparison.Ordinal) < 0) continue;

                    if (shipKey != null)
                    {
                        Dictionary<string, MatRule> overrides;
                        MatRule overridden;
                        if (ShipMaterialOverrides.TryGetValue(shipKey, out overrides) &&
                            overrides.TryGetValue(token, out overridden))
                            return overridden;
                    }

                    return rule;
                }
            }
            return null;
        }

        /// <summary>
        /// Переопределение корабля для первого совпавшего токена.
        /// Возвращает null, если первый совпавший токен переопределения не имеет —
        /// тогда работает обычный путь (общее правило + стража палитры).
        /// </summary>
        private static MatRule MatchShipOverride(string materialName, string shipKey)
        {
            if (string.IsNullOrEmpty(materialName) || shipKey == null) return null;

            Dictionary<string, MatRule> overrides;
            if (!ShipMaterialOverrides.TryGetValue(shipKey, out overrides)) return null;

            string n = materialName.ToLowerInvariant();
            foreach (var rule in MaterialRules)
            {
                foreach (var token in rule.Tokens)
                {
                    if (n.IndexOf(token, StringComparison.Ordinal) < 0) continue;

                    MatRule overridden;
                    return overrides.TryGetValue(token, out overridden) ? overridden : null;
                }
            }
            return null;
        }

        /// <summary>
        /// Материал уже лежит в палитре проекта (а не вложен в FBX) → повторно не переводим.
        /// Без этой стражи повторный прогон после первого назначения разрушал бы
        /// результат: токены палитровых имён совпадают с токенами FBX-источников.
        /// </summary>
        private static bool IsPaletteMaterial(Material m)
        {
            string path = AssetDatabase.GetAssetPath(m);
            return !string.IsNullOrEmpty(path) &&
                   path.StartsWith("Assets/_Project/Materials/", StringComparison.Ordinal);
        }

        private static string FindPaletteMaterialPath(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Material", new[] { "Assets/_Project/Materials" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.IndexOf("ARCHIVE_NOTUSED", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                string file = Path.GetFileNameWithoutExtension(p);
                if (file.StartsWith("SMALL_", StringComparison.Ordinal)) continue;
                if (!string.Equals(file, name, StringComparison.Ordinal)) continue;
                return p;
            }
            return null;
        }

        // =====================================================================
        // Структурные утилиты
        // =====================================================================

        private static GameObject FindDirectModelInstance(GameObject root)
        {
            foreach (Transform c in root.transform)
            {
                if (PrefabUtility.GetPrefabInstanceStatus(c.gameObject) != PrefabInstanceStatus.Connected) continue;
                string src = SourcePathOf(c.gameObject);
                if (src != null && src.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) return c.gameObject;
            }
            return null;
        }

        private static string SourcePathOf(GameObject go)
        {
            var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
            return src != null ? AssetDatabase.GetAssetPath(src) : null;
        }

        /// <summary>Все user-созданные держатели коллайдеров (&lt;NODE&gt;_Colliders) в префабе.</summary>
        private static List<Transform> CollectUserColliderTrees(GameObject root)
        {
            var list = new List<Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root.transform) continue;
                if (!t.name.EndsWith("_Colliders", StringComparison.Ordinal)) continue;
                if (!PrefabUtility.IsAddedGameObjectOverride(t.gameObject)) continue;   // уже перенесённые — не трогаем
                var parent = t.parent;
                if (parent != null && parent.name.EndsWith("_Colliders", StringComparison.Ordinal) &&
                    PrefabUtility.IsAddedGameObjectOverride(parent.gameObject))
                    continue;                                                            // берём только верхний
                list.Add(t);
            }
            return list;
        }

        private static bool IsInsideAnyModelInstance(GameObject root, Transform t)
        {
            for (var p = t.parent; p != null && p != root.transform; p = p.parent)
                if (PrefabUtility.GetPrefabInstanceStatus(p.gameObject) == PrefabInstanceStatus.Connected)
                    return true;
            return false;
        }

        /// <summary>Перемещаем только держатели коллайдеров: ни визуала, ни скриптов, ни света.</summary>
        private static string ValidateMovable(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null) return "есть missing component";
                if (c is Transform) continue;
                if (c is Collider) continue;
                if (c is MeshRenderer || c is MeshFilter) return "есть визуал (" + c.GetType().Name + ")";
                return "есть " + c.GetType().Name;
            }
            return null;
        }

        private static bool IsDegenerate(Collider c)
        {
            if (c == null) return false;
            if (!c.enabled) return false;
            var size = c.bounds.size;
            if (size.sqrMagnitude < 1e-8f) return true;

            var mc = c as MeshCollider;
            if (mc != null)
            {
                if (mc.sharedMesh == null) return true;
                if (mc.sharedMesh.bounds.size.sqrMagnitude < 1e-8f) return true;
            }

            var bc = c as BoxCollider;
            if (bc != null && bc.size.sqrMagnitude < 1e-8f) return true;

            return false;
        }

        private static Collider FindColliderBelow(Transform t)
        {
            return t.GetComponentInChildren<Collider>(true);
        }

        private sealed class Pose
        {
            public Vector3 WorldCenter;
            public Vector3 WorldSize;
        }

        /// <summary>
        /// Снимок только BoxCollider — переносятся именно боксовые деревья, а
        /// <c>MeshCollider.bounds</c> в edit mode бывает не обновлён и даёт ложные расхождения.
        /// Центр считается в координатах prefab-корня: он авторится на ~4.0e4 по мировым,
        /// и мировое сравнение теряет точность на порядки.
        /// </summary>
        private static List<Pose> SnapshotColliderPoses(GameObject root)
        {
            var list = new List<Pose>();
            foreach (var bc in root.GetComponentsInChildren<BoxCollider>(true))
            {
                var s = bc.transform.lossyScale;
                var size = new Vector3(Mathf.Abs(bc.size.x * s.x), Mathf.Abs(bc.size.y * s.y), Mathf.Abs(bc.size.z * s.z));
                var world = bc.transform.TransformPoint(bc.center);
                list.Add(new Pose
                {
                    WorldCenter = root.transform.InverseTransformPoint(world),
                    WorldSize = size
                });
            }
            return list;
        }

        private static string CompareSnapshots(List<Pose> before, List<Pose> after)
        {
            var used = new bool[before.Count];
            foreach (var a in after)
            {
                int best = -1;
                float bestDist = float.MaxValue;
                for (int i = 0; i < before.Count; i++)
                {
                    if (used[i]) continue;
                    float d = (before[i].WorldCenter - a.WorldCenter).sqrMagnitude;
                    if (d < bestDist) { bestDist = d; best = i; }
                }
                if (best < 0) return "нет пары для коллайдера в " + V(a.WorldCenter);
                used[best] = true;

                float dc = Vector3.Distance(before[best].WorldCenter, a.WorldCenter);
                float ds = (before[best].WorldSize - a.WorldSize).magnitude;
                if (dc > PoseTolerance)
                    return string.Format(CultureInfo.InvariantCulture, "смещение центра {0:F6} (допуск {1})", dc, PoseTolerance);
                if (ds > PoseTolerance)
                    return string.Format(CultureInfo.InvariantCulture, "изменился размер {0:F6} (допуск {1})", ds, PoseTolerance);
            }
            return null;
        }

        // =====================================================================
        // Бэкапы
        // =====================================================================

        /// <summary>
        /// Бэкап «первая запись побеждает»: повторный apply не должен затирать
        /// исходный файл уже изменённой версией, иначе rollback теряет смысл.
        /// </summary>
        private static void Backup(string prefabPath)
        {
            string target = AbsoluteProjectPath(Path.Combine(BackupRelativeDir, Path.GetFileName(prefabPath) + ".bak"));
            if (File.Exists(target)) return;

            string dir = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.Copy(AbsoluteProjectPath(prefabPath), target, false);
        }

        // =====================================================================
        // Общие утилиты
        // =====================================================================

        private static Transform FindChild(Transform parent, string name)
        {
            foreach (Transform c in parent) if (c.name == name) return c;
            return null;
        }

        private static string PathOf(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        private static string V(Vector3 v)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:F5},{1:F5},{2:F5}", v.x, v.y, v.z);
        }

        private static string AbsoluteProjectPath(string relative)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));
        }

        private static string AbsoluteReportPath()
        {
            return AbsoluteProjectPath(ReportRelativePath);
        }

        // =====================================================================
        // CSV
        // =====================================================================

        private sealed class Row
        {
            public string Phase = "";
            public string Category = "";
            public string Path = "";
            public string Target = "";
            public string Note = "";

            public static Row Info(string phase, string category, string path, string target, string note)
            {
                return new Row { Phase = phase, Category = category, Path = path, Target = target, Note = note };
            }
        }

        private static void WriteReport(List<Row> rows)
        {
            WriteReportInternal(rows, false);
        }

        /// <summary>Apply-фазы дописываются к уже собранному analyze-отчёту, не затирая его.</summary>
        private static void WriteReportKeep(List<Row> rows)
        {
            WriteReportInternal(rows, true);
        }

        private static void WriteReportInternal(List<Row> rows, bool keepExisting)
        {
            string path = AbsoluteReportPath();
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            const string header = "phase,category,path,target,note";

            var phases = new HashSet<string>();
            foreach (var r in rows) phases.Add(r.Phase);

            var kept = new List<string>();
            if (keepExisting && File.Exists(path))
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    if (line.Length == 0) continue;
                    if (line.StartsWith("phase,category,", StringComparison.Ordinal)) continue;
                    bool replaced = false;
                    foreach (var p in phases)
                        if (line.StartsWith(p + ",", StringComparison.Ordinal)) { replaced = true; break; }
                    if (!replaced) kept.Add(line);
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine(header);
            foreach (var l in kept) sb.AppendLine(l);
            foreach (var r in rows)
                sb.AppendLine(Csv(r.Phase) + "," + Csv(r.Category) + "," + Csv(r.Path) + "," + Csv(r.Target) + "," + Csv(r.Note));

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
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
