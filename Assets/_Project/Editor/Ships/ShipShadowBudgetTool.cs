// Project C — T-CULL01 / Stage 3c (P2): установка и проверка бюджета теней Ship_havyII_base.
//
// Зачем отдельный тул: ShipShadowBudget — runtime-компонент, он ничего не делает в edit mode
// (это сделано намеренно, см. шапку компонента). Значит проверять надо prefab-side состояние:
// что компонент вообще есть, с какими параметрами, какое множество мелких частей он возьмёт
// и не разъехались ли инварианты H2 после установки.
//
// Что делает:
//   Apply    — бэкап .prefab вне Assets, AddComponent<ShipShadowBudget> на корень, валидация
//              и сохранение. Повторная установка запрещена (нужен Rollback).
//   Verify   — только чтение: параметры компонента, инвентарь кандидатов по габариту,
//              аудит shadowCastingMode (префаб vs источник FBX), инварианты H2 и LOD,
//              проекция ΔDC на позу 200 м. Ничего не сохраняет.
//   Rollback — снять компонент с префаба (тоже с бэкапом).
//
// Инварианты, которые нельзя сломать этим тулом: пороги LOD остаются raw 300/900 м
// (ближе ~281.6 м порог LOD0 задать НЕЛЬЗЯ — screenRelativeTransitionHeight ≤ 1.0 при
// worldSize 324.85 м), LODGroup.size/localReferencePoint не пересчитываются, 9 дверей /
// 250 BoxCollider / 11 ModuleSlot / 616 MeshRenderer остаются на месте, авторские
// shadowCastingMode в префабе не переписываются (в сцене Stage 2 уже оставил 5 346
// override'ов m_CastShadows: 0 — такие значения здесь считаются провалом проверки).
//
// Гейт P2 (≤1300 DC на 200 м, корабль целиком в кадре) этот тул НЕ подтверждает:
// он даёт только prefab-side проверку и проекцию. Подтверждение — Play Mode прогон
// Project C → Culling → Stage 3 Ship Bench → Run (с 9.3 замер сам останавливает игру).
// Гейт теперь закрывается по двум позам: d200 (1434 → перебор +134) и d300 (1611 — худшая поза
// при lodBias 1.3, когда LOD0 держится до 390 м).
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
    public static class ShipShadowBudgetTool
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Ships/Ship_havyII_base.prefab";

        private const string ReportRelativePath =
            "docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/N_stage3c_heavy_2026-10-31.csv";

        private const string BackupRelativePath =
            "_local_backups/T-CULL01/Stage3c/Ship_havyII_base.prefab.bak";

        private const string ApplyMenu = "ProjectC/Ships/Shadow Budget/Apply to Ship_havyII_base (Stage 3c)";
        private const string VerifyMenu = "ProjectC/Ships/Shadow Budget/Verify (Stage 3c)";
        private const string RollbackMenu = "ProjectC/Ships/Shadow Budget/Rollback (Stage 3c)";

        // ---------------------------------------------------------------- замеры-опоры
        // Источник: N_stage3c_heavy_runtime_2026-10-31.txt, прогон 2026-10-31 21:24 (shadowDistance 400,
        // 4 каскада, soft), строка CASE id=d200: dc_all=3926, dc_withoutShip=2492, ship_dc=1434,
        // slots=553, ship_shadowCasters=489 → 881 теневых draw → 1.80 draw на кастер.
        // Прошлая опора 1538/555/593 снята при shadowDistance=1000 и несравнима — оставлена в отчёте
        // как историческая (отчёт Stage 3c §9).
        private const int MeasuredShipDc200 = 1434;
        private const int MeasuredSlots200 = 553;
        private const int MeasuredShadowCasters200 = 489;

        /// <summary>Гейт P2: ship_dc на 200 м при полном корабле в кадре (и на 300 м — худшая поза).</summary>
        private const int GateDc = 1300;

        // ---------------------------------------------------------------- инварианты H2
        private const int ExpectDoors = 9;
        private const int ExpectBoxColliders = 250;
        private const int ExpectModuleSlots = 11;
        private const int ExpectMeshRenderers = 616;
        private const float ExpectLod1Distance = 300f;
        private const float ExpectLod2Distance = 900f;
        private const float CalibrationFov = 60f;

        // =====================================================================
        // Apply
        // =====================================================================

        [MenuItem(ApplyMenu, false, 160)]
        public static void Apply()
        {
            var rows = new List<string[]>();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            bool saved = false;
            try
            {
                if (root.GetComponent<ShipShadowBudget>() != null)
                    throw new InvalidOperationException(
                        "ShipShadowBudget уже висит на префабе — повторно не добавляется (сначала Rollback).");

                var problems = new List<string>();
                ShipVisualMerger.ValidateMergedState(root, problems);
                CheckInvariants(root, problems, rows);
                if (problems.Count > 0)
                    throw new InvalidOperationException("Валидация ДО установки не прошла, префаб не сохранён: " +
                                                        string.Join(" | ", problems));

                AddRow(rows, "applied", "component", "before", "нет", "компонента на корне не было");

                string backup = AbsoluteProjectPath(BackupRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                File.Copy(AbsoluteProjectPath(PrefabPath), backup, true);
                AddRow(rows, "applied", "backup", BackupRelativePath, "ok", "полный .prefab до установки");

                var budget = root.AddComponent<ShipShadowBudget>();
                var group = root.GetComponentInChildren<LODGroup>(true);
                if (group == null)
                    throw new InvalidOperationException("На префабе нет LODGroup — бюджету не с чем работать.");

                AddRow(rows, "applied", "component", "ShipShadowBudget", "added",
                    "on_m=" + F(budget.ShadowOnDistance) + "; off_m=" + F(budget.ShadowOffDistance) +
                    "; maxAxis_m=" + F(budget.SmallPartMaxAxis) + "; enabled=" + budget.BudgetEnabled);

                var after = new List<string>();
                ShipVisualMerger.ValidateMergedState(root, after);
                CheckInvariants(root, after, rows);
                if (after.Count > 0)
                    throw new InvalidOperationException("Валидация ПОСЛЕ установки не прошла, префаб не сохранён: " +
                                                        string.Join(" | ", after));

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                if (!ok) throw new InvalidOperationException("SaveAsPrefabAsset вернул false");
                saved = true;

                AddRow(rows, "applied", "save", PrefabPath, "ok", "компонент установлен на корень");
                WriteReport(rows);
                Debug.Log("[SB3c] Компонент ShipShadowBudget установлен на " + PrefabPath +
                          " (бэкап: " + BackupRelativePath + "). Отчёт: " + AbsoluteProjectPath(ReportRelativePath));
            }
            catch (Exception e)
            {
                AddRow(rows, saved ? "failed_after_save" : "failed", "exception", e.GetType().Name, "", e.Message);
                WriteReport(rows);
                Debug.LogError("[SB3c] Apply failed" + (saved ? " (префаб сохранён)" : " (префаб не сохранён)") +
                               ": " + e);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                if (saved) AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            }
        }

        // =====================================================================
        // Verify
        // =====================================================================

        [MenuItem(VerifyMenu, false, 161)]
        public static void Verify()
        {
            var rows = new List<string[]>();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var problems = new List<string>();
                ShipVisualMerger.ValidateMergedState(root, problems);
                CheckInvariants(root, problems, rows);

                var group = root.GetComponentInChildren<LODGroup>(true);
                var budget = root.GetComponent<ShipShadowBudget>();
                if (budget == null)
                {
                    AddRow(rows, "verify", "component", "ShipShadowBudget", "MISSING", "");
                    problems.Add("нет ShipShadowBudget на корне префаба");
                }

                AuditShadowModes(root, rows, problems);
                ReportInteractiveOutsideLod(root, group, rows);

                if (budget != null && group != null)
                {
                    AddRow(rows, "verify", "params", "present", "yes",
                        "enabled=" + budget.BudgetEnabled +
                        "; on_m=" + F(budget.ShadowOnDistance) +
                        "; off_m=" + F(budget.ShadowOffDistance) +
                        "; maxAxis_m=" + F(budget.SmallPartMaxAxis));

                    var inv = Describe(root, group, budget.SmallPartMaxAxis);
                    ReportInventory(inv, budget, rows);
                    Project(inv, group, rows, problems);
                }

                AddRow(rows, "verify", "validation", "", problems.Count == 0 ? "OK" : "FAIL",
                    string.Join(" | ", problems));
                WriteReport(rows);

                if (problems.Count == 0)
                    Debug.Log("[SB3c] Verify OK. Отчёт: " + AbsoluteProjectPath(ReportRelativePath));
                else
                    foreach (var p in problems) Debug.LogError("[SB3c] Verify: " + p);
            }
            catch (Exception e)
            {
                AddRow(rows, "failed", "exception", e.GetType().Name, "", e.Message);
                WriteReport(rows);
                Debug.LogError("[SB3c] Verify failed: " + e);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // =====================================================================
        // Rollback
        // =====================================================================

        [MenuItem(RollbackMenu, false, 162)]
        public static void Rollback()
        {
            var rows = new List<string[]>();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            bool saved = false;
            try
            {
                var budget = root.GetComponent<ShipShadowBudget>();
                if (budget == null)
                {
                    Debug.Log("[SB3c] На префабе нет ShipShadowBudget — снимать нечего.");
                    return;
                }

                if (!EditorUtility.DisplayDialog("Stage 3c rollback",
                        "Снять ShipShadowBudget с Ship_havyII_base и сохранить префаб?\n" +
                        "Тени вернутся к авторским режимам, LOD и мерж не меняются.",
                        "Снять", "Отмена"))
                    return;

                string backup = AbsoluteProjectPath(BackupRelativePath + ".rollback");
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                File.Copy(AbsoluteProjectPath(PrefabPath), backup, true);
                AddRow(rows, "rollback", "backup", BackupRelativePath + ".rollback", "ok", "состояние с компонентом");

                UnityEngine.Object.DestroyImmediate(budget, true);

                var problems = new List<string>();
                ShipVisualMerger.ValidateMergedState(root, problems);
                CheckInvariants(root, problems, rows);
                if (problems.Count > 0)
                    throw new InvalidOperationException("Валидация после снятия не прошла, префаб не сохранён: " +
                                                        string.Join(" | ", problems));

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                if (!ok) throw new InvalidOperationException("SaveAsPrefabAsset вернул false");
                saved = true;

                AddRow(rows, "rollback", "component", "ShipShadowBudget", "removed", "префаб сохранён");
                WriteReport(rows);
                Debug.Log("[SB3c] ShipShadowBudget снят с " + PrefabPath + ", префаб сохранён.");
            }
            catch (Exception e)
            {
                AddRow(rows, saved ? "failed_after_save" : "failed", "exception", e.GetType().Name, "", e.Message);
                WriteReport(rows);
                Debug.LogError("[SB3c] Rollback failed" + (saved ? " (префаб сохранён)" : " (префаб не сохранён)") +
                               ": " + e);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                if (saved) AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            }
        }

        // =====================================================================
        // Инвентарь кандидатов
        // =====================================================================

        private sealed class Inventory
        {
            public int Renderers;
            public int Slots;
            public int Casters;
            public int CasterSlots;
            public int OffInPrefab;
            public int LevelMask;                    // какие LOD-уровни дали кандидатов (бит на уровень)
            public readonly int[] BucketRenderers = new int[9];
            public readonly int[] BucketSlots = new int[9];
        }

        /// <summary>
        /// Ровно то множество, которое возьмёт рантайм: ShipShadowBudget.CollectCandidates
        /// (члены LODGroup, чей габарит по максимальной оси не больше порога).
        /// </summary>
        private static Inventory Describe(GameObject root, LODGroup group, float maxAxis)
        {
            var inv = new Inventory();
            var list = new List<MeshRenderer>(512);
            ShipShadowBudget.CollectCandidates(root, group, maxAxis, list);

            var inLevel = new Dictionary<Renderer, int>();
            var lods = group.GetLODs();
            for (int i = 0; i < lods.Length; i++)
                foreach (var r in lods[i].renderers)
                    if (r != null && !inLevel.ContainsKey(r)) inLevel[r] = i;

            inv.Renderers = list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                if (r == null) continue;
                int slots = r.sharedMaterials.Length;
                inv.Slots += slots;
                if (r.shadowCastingMode != ShadowCastingMode.Off)
                {
                    inv.Casters++;
                    inv.CasterSlots += slots;
                }
                else inv.OffInPrefab++;

                int level;
                if (inLevel.TryGetValue(r, out level)) inv.LevelMask |= 1 << level;

                int b = Mathf.Clamp(Mathf.CeilToInt(MaxAxis(r.bounds.size)), 0, 8);
                inv.BucketRenderers[b]++;
                inv.BucketSlots[b] += slots;
            }
            return inv;
        }

        private static void ReportInventory(Inventory inv, ShipShadowBudget budget, List<string[]> rows)
        {
            AddRow(rows, "verify", "inventory", "candidates",
                inv.Renderers.ToString(CultureInfo.InvariantCulture),
                "slots=" + inv.Slots + "; casters=" + inv.Casters + "; casterSlots=" + inv.CasterSlots +
                "; off в префабе=" + inv.OffInPrefab + "; LOD-уровни(биты)=" + inv.LevelMask);

            AddRow(rows, "verify", "inventory", "by_max_axis",
                Join(inv.BucketRenderers),
                "рендереров по ceil(maxAxis): " + BucketLabels());

            AddRow(rows, "verify", "inventory", "slots_by_max_axis",
                Join(inv.BucketSlots),
                "слотов по ceil(maxAxis): " + BucketLabels() +
                "; порог компонента = " + F(budget.SmallPartMaxAxis) + " м");
        }

        private static string BucketLabels()
        {
            return "0|1|2|3|4|5|6|7|8 (м, потолок)";
        }

        /// <summary>
        /// Проекция ΔDC на позу 200 м. Это НЕ замер: ship_dc = slots_в_кадре + теневые draw.
        /// С 9.3 бюджет решает по дистанции до КОРАБЛЯ, поэтому на 200 м под отсечение попадает
        /// ВЕСЬ набор кандидатов, а не доля: модель «219 из 314» опровергнута замером 21:24
        /// (факт 1434 вместо 1175) и больше не используется.
        /// </summary>
        private static void Project(Inventory inv, LODGroup group, List<string[]> rows, List<string> problems)
        {
            int shadowDraws = MeasuredShipDc200 - MeasuredSlots200;                 // 881
            float perCaster = shadowDraws / (float)MeasuredShadowCasters200;        // 1.80

            AddRow(rows, "projection", "dc", "anchor_200m", MeasuredShipDc200.ToString(CultureInfo.InvariantCulture),
                "замер 2026-10-31 21:24: slots=" + MeasuredSlots200 + "; shadowCasters=" + MeasuredShadowCasters200 +
                "; shadowDraws=" + shadowDraws + "; drawsPerCaster=" + F(perCaster));

            int requiredCulled = Mathf.CeilToInt((MeasuredShipDc200 - GateDc) / perCaster);
            int culledSlots = inv.Slots;
            int predicted = MeasuredShipDc200 - Mathf.RoundToInt(culledSlots * perCaster);
            int bandLow = MeasuredShipDc200 - Mathf.RoundToInt(culledSlots * 2.4f);
            int bandHigh = MeasuredShipDc200 - Mathf.RoundToInt(culledSlots * 1.3f);

            AddRow(rows, "projection", "dc", "gate", GateDc.ToString(CultureInfo.InvariantCulture),
                "требуется снять слотов теней: " + requiredCulled +
                " (при " + F(perCaster) + " draw на кастер)");

            AddRow(rows, "projection", "dc", "modelled", predicted.ToString(CultureInfo.InvariantCulture),
                "правило по дистанции до корабля: снимается весь набор " + culledSlots + " слотов, а не доля; " +
                "полоса при 1.3…2.4 draw/кастер = " + bandHigh + "…" + bandLow);

            bool plausible = predicted <= GateDc && bandHigh <= GateDc;
            AddRow(rows, "projection", "dc", "verdict", plausible ? "PROJECTED_ONLY" : "INSUFFICIENT",
                "ГЕЙТ НЕ ПОДТВЕРЖДЁН: нужен прогон Stage 3 Ship Bench; закрывать по d200 И d300 " +
                "(d300 = 1611 DC — худшая поза при lodBias 1.3)");

            if (inv.Slots <= requiredCulled)
                problems.Add("всех кандидатов (" + inv.Slots + " слотов) не хватает, чтобы снять нужные " +
                             requiredCulled + " слотов теней — гейт этим порогом не закрывается");

            var lods = group.GetLODs();
            if (lods.Length > 0)
            {
                float d0 = LodDistance(group, lods[0].screenRelativeTransitionHeight);
                if (d0 < 280f)
                    problems.Add("порог LOD0 ниже физического минимума 281.6 м: " + F(d0) + " м");
            }
        }

        // =====================================================================
        // Аудит shadowCastingMode и интерактива
        // =====================================================================

        /// <summary>
        /// В префабе авторские режимы теней не должны отличаться от источника FBX:
        /// Stage 2 оставил после edit-mode-пробы 5 346 override'ов m_CastShadows: 0.
        /// </summary>
        private static void AuditShadowModes(GameObject root, List<string[]> rows, List<string> problems)
        {
            int total = 0, off = 0, mismatch = 0;
            var firstMismatch = new List<string>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r == null) continue;
                total++;
                if (r.shadowCastingMode == ShadowCastingMode.Off) off++;

                var src = PrefabUtility.GetCorrespondingObjectFromSource(r);
                if (src == null) continue;                       // объект создан мержем/тулом
                if (src.shadowCastingMode == r.shadowCastingMode) continue;
                mismatch++;
                if (firstMismatch.Count < 6)
                    firstMismatch.Add(r.name + ": " + src.shadowCastingMode + "→" + r.shadowCastingMode);
            }

            AddRow(rows, "verify", "shadow_modes", "renderers", total.ToString(CultureInfo.InvariantCulture),
                "off=" + off + "; расхождений с источником=" + mismatch +
                (firstMismatch.Count > 0 ? "; " + string.Join(" ‖ ", firstMismatch.ToArray()) : ""));

            if (mismatch > 0)
                problems.Add("shadowCastingMode в префабе расходится с источником у " + mismatch +
                             " рендереров (переписать в edit mode?)");
        }

        private static void ReportInteractiveOutsideLod(GameObject root, LODGroup group, List<string[]> rows)
        {
            if (group == null) return;
            var inLod = new HashSet<Renderer>();
            foreach (var l in group.GetLODs())
                foreach (var r in l.renderers) if (r != null) inLod.Add(r);

            int count = 0, slots = 0, shadowSlots = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r == null || !r.enabled || inLod.Contains(r)) continue;
                int s = r.sharedMaterials.Length;
                count++;
                slots += s;
                if (r.shadowCastingMode != ShadowCastingMode.Off) shadowSlots += s;
            }

            AddRow(rows, "verify", "interactive", "outside_lodgroup", count.ToString(CultureInfo.InvariantCulture),
                "slots=" + slots + "; shadowSlots=" + shadowSlots +
                "; бюджет их не трогает (двери, створки, слоты, PilotSeat, CargoVisual, двигатели, VFX)");
        }

        // =====================================================================
        // Инварианты H2 / LOD
        // =====================================================================

        private static void CheckInvariants(GameObject root, List<string> problems, List<string[]> rows)
        {
            Expect(problems, "DoorController", root.GetComponentsInChildren<DoorController>(true).Length, ExpectDoors);
            Expect(problems, "BoxCollider", root.GetComponentsInChildren<BoxCollider>(true).Length, ExpectBoxColliders);
            Expect(problems, "ModuleSlot", root.GetComponentsInChildren<ModuleSlot>(true).Length, ExpectModuleSlots);
            Expect(problems, "MeshRenderer", root.GetComponentsInChildren<MeshRenderer>(true).Length, ExpectMeshRenderers);

            var group = root.GetComponentInChildren<LODGroup>(true);
            if (group == null) { problems.Add("нет LODGroup"); return; }

            var lods = group.GetLODs();
            if (lods.Length != 3) { problems.Add("LOD уровней: " + lods.Length + " вместо 3"); return; }

            float d0 = LodDistance(group, lods[0].screenRelativeTransitionHeight);
            float d1 = LodDistance(group, lods[1].screenRelativeTransitionHeight);
            if (Mathf.Abs(d0 - ExpectLod1Distance) > 3f || Mathf.Abs(d1 - ExpectLod2Distance) > 9f)
                problems.Add("пороги LOD уехали: " + F(d0) + " / " + F(d1) + " м (ожидалось 300 / 900)");
            if (lods[2].screenRelativeTransitionHeight != 0f)
                problems.Add("LOD2 с cull — ожидался 0");

            var ls = group.transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(ls.x), Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
            AddRow(rows, "verify", "lod", "thresholds",
                F(d0) + "/" + F(d1),
                "size=" + F(group.size) + "; maxLossyScale=" + F(maxScale) +
                "; worldSize=" + F(group.size * maxScale) +
                "; floor для LOD0 при FOV60 = " + F(group.size * maxScale / (2f * Mathf.Tan(CalibrationFov * 0.5f * Mathf.Deg2Rad))) +
                " м; уровней=" + lods.Length + "; LOD0 renderers=" + lods[0].renderers.Length +
                "; LOD1=" + lods[1].renderers.Length + "; LOD2=" + lods[2].renderers.Length);
        }

        private static void Expect(List<string> problems, string what, int actual, int expected)
        {
            if (actual != expected) problems.Add(what + ": " + actual + " вместо " + expected);
        }

        /// <summary>Дистанция перехода при FOV 60 и lodBias 1: worldSize / (2·tan(fov/2)·h).</summary>
        private static float LodDistance(LODGroup group, float relativeHeight)
        {
            if (relativeHeight <= 0f) return float.PositiveInfinity;
            var ls = group.transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(ls.x), Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
            float worldSize = group.size * maxScale;
            float tanHalf = Mathf.Tan(CalibrationFov * 0.5f * Mathf.Deg2Rad);
            return worldSize / (2f * tanHalf * relativeHeight);
        }

        // =====================================================================
        // Утилиты
        // =====================================================================

        private static float MaxAxis(Vector3 size)
        {
            return Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        }

        private static string Join(int[] values)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) sb.Append('|');
                sb.Append(values[i].ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        private static string F(float f)
        {
            return f.ToString("F3", CultureInfo.InvariantCulture);
        }

        private static string AbsoluteProjectPath(string relative)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));
        }

        private static void AddRow(List<string[]> rows, string phase, string category, string item,
            string value, string note)
        {
            rows.Add(new[] { phase, category, item, value, note });
        }

        /// <summary>Фазы, которыми владеет инструмент: при перезаписи они заменяются, остальные строки сохраняются.</summary>
        private static readonly string[] OwnedPhases =
            { "applied", "verify", "rollback", "projection", "projection_superseded", "failed", "failed_after_save" };

        /// <summary>
        /// Перезапись блока инструмента с сохранением чужих строк: в этом же файле лежат замеры
        /// Play Mode (phase=measured/next/...), которые нельзя терять при повторном Verify/Apply/Rollback.
        /// </summary>
        private static void WriteReport(List<string[]> rows)
        {
            string path = AbsoluteProjectPath(ReportRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var foreign = new List<string>();
            if (File.Exists(path))
            {
                foreach (string line in File.ReadAllLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (line.StartsWith("phase,", StringComparison.Ordinal)) continue;
                    string phase = line.Split(',')[0];
                    if (Array.IndexOf(OwnedPhases, phase) >= 0) continue;
                    foreign.Add(line);
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("phase,category,item,value,note");
            foreach (var r in rows)
                sb.AppendLine(Csv(r[0]) + "," + Csv(r[1]) + "," + Csv(r[2]) + "," + Csv(r[3]) + "," + Csv(r[4]));
            foreach (string line in foreign)
                sb.AppendLine(line);
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
