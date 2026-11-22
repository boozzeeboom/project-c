#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using ProjectC.World;
using ProjectC.World.NpcPrimum;

namespace ProjectC.EditorTools.Npc
{
    /// <summary>
    /// Проверка готовности сцены к толпе PRIMUM — без запуска Play Mode.
    /// Меряет то, что реально спрашивает директор и PrimumCrowdNpc при спавне:
    ///   • HouseMarker (активные) и уникальные houseId → ёмкость толпы;
    ///   • попадают ли центры домов на навмеш в радиусах houseSampleRadius / houseSampleFallbackRadius;
    ///   • валидность humanoid-аватаров и рендереров у всех моделей каталога;
    ///   • насколько пивот моделей ниже ступней (нужно для выравнивания по земле);
    ///   • где стоит PRIMUM_NPC_CROWD и назначен ли конфиг.
    /// </summary>
    public static class PrimumCrowdAudit
    {
        private const string ConfigPath = "Assets/_Project/Data/NPC/PrimumCrowdConfig.asset";
        private const string WorldScenePath = "Assets/_Project/Scenes/World/WorldScene_0_0.unity";
        private const string CrowdRootName = "PRIMUM_NPC_CROWD";

        [MenuItem("Tools/Project C/NPC/Primum Crowd/0) Audit readiness (no Play Mode)", priority = 299)]
        public static void Audit()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══ PRIMUM CROWD AUDIT ═══");

            var config = AssetDatabase.LoadAssetAtPath<PrimumCrowdConfig>(ConfigPath);
            if (config == null)
            {
                Debug.LogError("[PrimumCrowdAudit] Нет конфига " + ConfigPath + " — сначала Build T1.");
                return;
            }

            AuditConfig(sb, config);
            AuditModels(sb, config);

            var active = EditorSceneManager.GetActiveScene();
            if (active.path != WorldScenePath)
            {
                sb.AppendLine($"[scene] открыта '{active.path}', а не WorldScene_0_0 — аудит домов пропущен.");
                Debug.Log(sb.ToString());
                return;
            }

            AuditScene(sb, config);
            Debug.Log(sb.ToString());
        }

        private static void AuditConfig(StringBuilder sb, PrimumCrowdConfig config)
        {
            int nullDefs = 0;
            for (int i = 0; i < config.residentDefinitions.Length; i++)
                if (config.residentDefinitions[i] == null) nullDefs++;

            sb.AppendLine($"[config] targetCount={config.targetCount} maxPerHouse={config.maxResidentsPerHouse} " +
                          $"моделей={config.modelCatalog.Length} определений={config.residentDefinitions.Length} " +
                          $"пустых={nullDefs}");
            sb.AppendLine($"[config] template={(config.npcTemplate != null ? config.npcTemplate.name : "NULL")} " +
                          $"female={config.animatorControllerFemale?.name ?? "NULL"} male={config.animatorControllerMale?.name ?? "NULL"}");

            int crowdInDb = 0;
            var guids = AssetDatabase.FindAssets("t:QuestDatabase");
            for (int g = 0; g < guids.Length; g++)
            {
                var db = AssetDatabase.LoadAssetAtPath<ProjectC.Quests.QuestDatabase>(AssetDatabase.GUIDToAssetPath(guids[g]));
                if (db == null || db.npcs == null) continue;
                for (int i = 0; i < db.npcs.Length; i++)
                    if (db.npcs[i] != null && db.npcs[i].npcId != null &&
                        db.npcs[i].npcId.StartsWith("primum_crowd_")) crowdInDb++;
            }
            sb.AppendLine($"[config] NPC толпы в QuestDatabase = {crowdInDb} " +
                          (crowdInDb >= config.residentDefinitions.Length
                              ? "(диалоги зарегистрированы)"
                              : "(ВНИМАНИЕ: не все — запусти Re-scan Quest Database)"));
        }

        private static void AuditModels(StringBuilder sb, PrimumCrowdConfig config)
        {
            int noAnimator = 0, badAvatar = 0, noRenderer = 0;
            float minBottom = float.MaxValue, maxBottom = float.MinValue, maxTop = float.MinValue;
            int measured = 0;

            foreach (var model in config.modelCatalog)
            {
                if (model == null) continue;
                var animator = model.GetComponentInChildren<Animator>(true);
                if (animator == null) { noAnimator++; continue; }
                if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman) badAvatar++;

                var renderers = model.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) { noRenderer++; continue; }

                bool has = false;
                Bounds b = default;
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    if (!has) { b = renderers[i].bounds; has = true; } else b.Encapsulate(renderers[i].bounds);
                }
                if (!has) continue;

                measured++;
                minBottom = Mathf.Min(minBottom, b.min.y);
                maxBottom = Mathf.Max(maxBottom, b.min.y);
                maxTop = Mathf.Max(maxTop, b.max.y);
            }

            sb.AppendLine($"[models] проверено={measured} безAnimator={noAnimator} плохойАватар={badAvatar} безRenderer={noRenderer}");
            if (measured > 0)
            {
                sb.AppendLine($"[models] низ меша относительно пивота: {minBottom:F3} .. {maxBottom:F3} м " +
                              "(отрицательное = модель садится ниже точки спавна)");
                sb.AppendLine($"[models] самая высокая точка = {maxTop:F3} м → нейм-плейт ставится выше автоматически");
            }
        }

        private static void AuditScene(StringBuilder sb, PrimumCrowdConfig config)
        {
            var crowd = GameObject.Find("WorldRoot_0_0/" + CrowdRootName);
            if (crowd == null)
                sb.AppendLine($"[scene] {CrowdRootName} НЕ найден (меню 3) Place root in WorldScene_0_0).");
            else
            {
                var director = crowd.GetComponent<PrimumNpcCrowdDirector>();
                sb.AppendLine($"[scene] {CrowdRootName}: parent={crowd.transform.parent?.name} " +
                              $"director={(director != null ? "OK" : "НЕТ")} " +
                              $"config={(director != null && director.Config != null ? director.Config.name : "NULL")}");
                if (crowd.transform.parent == null || crowd.transform.parent.name != "WorldRoot_0_0")
                    sb.AppendLine("[scene] ВНИМАНИЕ: коллекция не под WorldRoot_0_0 — Floating Origin её не потащит.");
            }

            var active = Object.FindObjectsByType<HouseMarker>(FindObjectsInactive.Exclude);
            var all = Object.FindObjectsByType<HouseMarker>(FindObjectsInactive.Include);

            var sum = new Dictionary<string, Vector3>();
            var count = new Dictionary<string, int>();
            for (int i = 0; i < active.Length; i++)
            {
                var m = active[i];
                if (m == null || string.IsNullOrEmpty(m.houseId)) continue;
                if (sum.TryGetValue(m.houseId, out var c))
                {
                    sum[m.houseId] = c + m.MapPosition;
                    count[m.houseId] = count[m.houseId] + 1;
                }
                else
                {
                    sum[m.houseId] = m.MapPosition;
                    count[m.houseId] = 1;
                }
            }

            int onNear = 0, onFallback = 0, off = 0;
            foreach (var kv in sum)
            {
                Vector3 center = kv.Value / count[kv.Key];
                if (NavMesh.SamplePosition(center, out _, config.houseSampleRadius, NavMesh.AllAreas)) onNear++;
                else if (NavMesh.SamplePosition(center, out _, config.houseSampleFallbackRadius, NavMesh.AllAreas)) onFallback++;
                else off++;
            }

            int capacity = sum.Count * config.maxResidentsPerHouse;
            sb.AppendLine($"[scene] HouseMarker активных={active.Length} всего={all.Length} уникальныхДомов={sum.Count}");
            sb.AppendLine($"[scene] центры домов на навмеше: r<={config.houseSampleRadius:F0}→{onNear} " +
                          $"r<={config.houseSampleFallbackRadius:F0}→{onFallback} ВНЕ={off}");
            sb.AppendLine($"[scene] ёмкость по домам = {sum.Count} × {config.maxResidentsPerHouse} = {capacity} NPC; " +
                          $"target={config.targetCount}" +
                          (config.targetCount > capacity
                              ? $" (сверх лимита пойдут уличными, без дома: {config.targetCount - capacity})"
                              : " (все с домом)"));

            var surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include);
            int withData = 0;
            for (int i = 0; i < surfaces.Length; i++)
                if (surfaces[i].navMeshData != null) withData++;
            sb.AppendLine($"[scene] NavMeshSurface={surfaces.Length} с данными={withData} " +
                          $"трис={NavMesh.CalculateTriangulation().indices.Length / 3}");
        }
    }
}
#endif
