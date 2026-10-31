// Project C — T-CULL01 / Stage 4b (H5b): edit-mode dry-run для gap close 3 031 → ≤2 500.
//
// Что считает (без Play Mode, без правок сцены/ассетов):
//   1. Фрустум-цензус по позам Stage 4 (C1 площадь / C2 улица / C3 крыша / PILOT чанк) и
//      оценку Hybrid Batch Group: уникальные ключи (mesh, submesh, material) и (mesh, shader).
//   2. Прогноз встроенного GRD small-mesh culling (m_SmallMeshScreenPercentage) на 0.5 / 1 / 2 %.
//      Формула движка (InstanceCuller.CalculateLODVisibility → LODRenderingUtils.CalculateLODDistance):
//        maxDist = max(worldAABB.size) / (percentage * 0.01);  instance culled when cameraDist > maxDist.
//      Плюс полоса плавного затухания 10 % (k_SmallMeshTransitionWidth).
//   3. Прогноз городского shadow budget: сколько кастеров попадает в бакеты дистанции и сколько
//      теневых вызовов уйдёт при пороге 150/200/250/300 м (URP shadowDistance 400 не меняется).
//   4. Material/submesh dry-run: сколько даст переход (mesh,material) → (mesh,shader) (атлас).
//   5. B2 dry-run на пилоте: сравнение текущего слитого состояния A с house/house-large вариантами J.
//
// Чего НЕ делает: не создаёт GameObjects, не вызывает SetActive, не пишет в сцену/префабы/ассеты,
// не запускает Play Mode, не использует Camera.main (её в edit mode всё равно нет).
// Отчёты пишутся только в docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/.
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
    /// <summary>Stage 4b: edit-mode прогноз рычагов gap close (T-CULL01 / H5b).</summary>
    public static class Stage4bDryRun
    {
        private const string RunMenu = "Project C/Culling/Stage 4b Dry Run/Run";
        private const string DataDir = "docs/world/primum_city/10_CULLING_RESEARCH/08_DATA";
        private const string DateTag = "2026-10-31";

        // Параметры, синхронизированные с Stage4GrBench / текущим URP-ассетом.
        private const float BenchFar = 30000f;
        private const float BenchFov = 60f;
        private const float BenchNear = 0.1f;
        private const float BenchAspect = 16f / 9f;
        private const float ShadowDistance = 400f;   // m_ShadowDistance в ProjectC_URP.asset
        private const float DoorCullDistance = 150f; // layerCullDistances[10] = 150
        private const int DoorLayer = 10;

        private const string PilotChunkName = "PRIMUM_HOUSES_S9_TRD-S2_P1_45H";
        private const float PilotBoundsInflate = 4f;
        private const float PilotCamDistance = 58f;
        private const float PilotCamHeight = 24f;

        private static readonly Vector3 AuthoredWorldOffset = new Vector3(39936.0f, 2560.03f, 39936.0f);

        private static readonly float[] SmallMeshPercentages = { 0.5f, 1f, 2f };
        private static readonly float[] ShadowBudgets = { 150f, 200f, 250f, 300f };

        private class DryCase
        {
            public string id;
            public Vector3 local;
            public float yaw;
            public float pitch;
            public bool relativeToPilotChunk;
        }

        private static List<DryCase> BuildCases()
        {
            var p1 = new Vector3(-342.90f, -50.43f, -102.20f);
            return new List<DryCase>
            {
                new DryCase { id = "C1", local = p1, yaw = 337.5f },
                new DryCase { id = "C2", local = new Vector3(-382.90f, -58.23f, -126.20f), yaw = 225f },
                new DryCase { id = "C3", local = new Vector3(-416.00f, -27.94f, -36.00f), yaw = 132f, pitch = -18f },
                new DryCase { id = "PILOT", local = Vector3.zero, yaw = 0f, relativeToPilotChunk = true },
            };
        }

        [MenuItem(RunMenu)]
        public static void Run()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Stage4bDryRun] Идёт Play Mode. Остановить и повторить.");
                return;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dataDir = Path.Combine(projectRoot, DataDir);
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            string rawPath = Path.Combine(dataDir, "P_stage4b_dryrun_" + DateTag + "_raw.txt");
            string csvPath = Path.Combine(dataDir, "P_stage4b_dryrun_" + DateTag + ".csv");
            if (File.Exists(rawPath)) File.Delete(rawPath);
            if (File.Exists(csvPath)) File.Delete(csvPath);

            var raw = new StringBuilder();
            var csv = new StringBuilder();
            csv.AppendLine("case,renderersEnabled,renderersInFrustum,doorsInFrustum,pairsInFrustum,shaderPairsInFrustum," +
                           "atlasGainPairs,submeshSlotsInFrustum," +
                           "sm05_renderers,sm05_pairs,sm05_removedPercent,sm1_renderers,sm1_pairs,sm1_removedPercent," +
                           "sm2_renderers,sm2_pairs,sm2_removedPercent," +
                           "shadowCastersEstimated,shadowKeep150,shadowCut150,shadowKeep200,shadowCut200," +
                           "shadowKeep250,shadowCut250,shadowKeep300,shadowCut300");

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            Transform worldRoot = null;
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == "WorldRoot_0_0") worldRoot = go.transform;

            if (worldRoot == null)
            {
                Debug.LogError("[Stage4bDryRun] WorldRoot_0_0 не найден в активной сцене.");
                return;
            }

            var all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var shipSet = BuildShipRendererSet(scene);

            Transform pilotChunk = FindDeep(worldRoot, PilotChunkName);
            Bounds pilotBounds = pilotChunk != null ? ComputeChunkBounds(pilotChunk) : new Bounds(Vector3.zero, Vector3.one);

            var planes = new Plane[6];
            var seenKeys = new HashSet<long>();
            var seenShaderKeys = new HashSet<long>();

            raw.AppendLine("NOTE;tool=Stage4bDryRun;engine=" + Application.unityVersion +
                           ";date=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            raw.AppendLine("NOTE;mode=edit_mode_prediction;no_playmode;no_scene_change;fov=" + BenchFov +
                           ";aspect=" + BenchAspect.ToString("F4", CultureInfo.InvariantCulture) +
                           ";near=" + BenchNear + ";far=" + BenchFar +
                           ";shadowDistance=" + ShadowDistance + ";doorCullDistance=" + DoorCullDistance);
            raw.AppendLine("NOTE;small_mesh_formula=maxAABBSize/(pct*0.01);fade_band=10%(engine k_SmallMeshTransitionWidth)");
            raw.AppendLine("NOTE;renderers_total=" + all.Length + ";ship_renderers=" + shipSet.Count +
                           ";pilotChunk=" + (pilotChunk != null ? "found" : "NOT_FOUND"));

            foreach (var c in BuildCases())
            {
                Vector3 camPos;
                Quaternion camRot;
                if (c.relativeToPilotChunk)
                {
                    var target = pilotBounds.center;
                    camPos = target + new Vector3(0f, PilotCamHeight, -PilotCamDistance);
                    camRot = Quaternion.LookRotation((target - camPos).normalized, Vector3.up);
                }
                else
                {
                    camPos = c.local + AuthoredWorldOffset + worldRoot.position;
                    camRot = Quaternion.Euler(c.pitch, c.yaw, 0f);
                }

                // ВАЖНО: Matrix4x4.Perspective + GeometryUtility.CalculateFrustumPlanes в Unity 6 даёт
                // вырожденный фрустум (Reversed-Z). Плоскости строятся вручную из позы камеры —
                // проверено: C1 даёт 3818 рендереров в кадре против 3826 в Play Mode Stage 4.
                Vector3 fwd = camRot * Vector3.forward;
                Vector3 up = camRot * Vector3.up;
                Vector3 right = camRot * Vector3.right;
                float halfV = Mathf.Tan(BenchFov * 0.5f * Mathf.Deg2Rad);
                float halfH = halfV * BenchAspect;
                planes[0] = new Plane((fwd + halfH * right).normalized, camPos);
                planes[1] = new Plane((fwd - halfH * right).normalized, camPos);
                planes[2] = new Plane((fwd - halfV * up).normalized, camPos);
                planes[3] = new Plane((fwd + halfV * up).normalized, camPos);
                planes[4] = new Plane(fwd, camPos + fwd * BenchNear);
                planes[5] = new Plane(-fwd, camPos + fwd * BenchFar);

                seenKeys.Clear();
                seenShaderKeys.Clear();

                int enabled = 0, inFrustum = 0, doors = 0, slots = 0;
                int shadowCasters = 0;
                var keepCasters = new Dictionary<float, int>();
                var cutCasters = new Dictionary<float, int>();
                for (int i = 0; i < ShadowBudgets.Length; i++)
                {
                    keepCasters[ShadowBudgets[i]] = 0;
                    cutCasters[ShadowBudgets[i]] = 0;
                }

                // Прогноз small-mesh: считаем оставшиеся рендереры и оставшиеся ключи.
                var smRenderers = new Dictionary<float, int>();
                var smPairs = new Dictionary<float, int>();
                for (int i = 0; i < SmallMeshPercentages.Length; i++)
                {
                    smRenderers[SmallMeshPercentages[i]] = 0;
                    smPairs[SmallMeshPercentages[i]] = 0;
                }
                var smKeys = new Dictionary<float, HashSet<long>>();
                for (int i = 0; i < SmallMeshPercentages.Length; i++)
                    smKeys[SmallMeshPercentages[i]] = new HashSet<long>();

                for (int i = 0; i < all.Length; i++)
                {
                    var r = all[i];
                    if (r == null || !r.enabled) continue;
                    if (shipSet.Contains(r)) continue; // корабли вне scope
                    enabled++;

                    var b = r.bounds;
                    float dist = Vector3.Distance(camPos, b.center);

                    // layerCullDistances[10] = 150 — дверной слой отсекается по дистанции.
                    if (r.gameObject.layer == DoorLayer && dist > DoorCullDistance) continue;

                    if (!GeometryUtility.TestPlanesAABB(planes, b)) continue;
                    inFrustum++;
                    if (r.gameObject.layer == DoorLayer) doors++;

                    var mf = r.GetComponent<MeshFilter>();
                    var mesh = mf != null ? mf.sharedMesh : null;
                    var mats = r.sharedMaterials;

                    // Ключ Hybrid Batch Group: (mesh, submesh index, material).
                    if (mesh != null)
                    {
                        for (int s = 0; s < mesh.subMeshCount; s++)
                        {
                            slots++;
                            var mat = (mats != null && s < mats.Length) ? mats[s] : null;
                            long mk = HashKey(mesh.GetHashCode(), s, mat != null ? mat.GetHashCode() : 0);
                            if (seenKeys.Add(mk))
                            {
                                var shader = mat != null ? mat.shader : null;
                                seenShaderKeys.Add(HashKey(mesh.GetHashCode(), s, shader != null ? shader.GetHashCode() : 0));
                            }
                        }
                    }

                    // Теневой бюджет: кастер живёт только внутри shadowDistance.
                    bool casts = r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (casts && dist <= ShadowDistance)
                    {
                        shadowCasters++;
                        for (int s = 0; s < ShadowBudgets.Length; s++)
                        {
                            float t = ShadowBudgets[s];
                            if (dist <= t) keepCasters[t]++; else cutCasters[t]++;
                        }
                    }

                    // Small-mesh прогноз.
                    float maxSize = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                    for (int s = 0; s < SmallMeshPercentages.Length; s++)
                    {
                        float pct = SmallMeshPercentages[s];
                        float minScreen = pct * 0.01f;
                        float maxDist = maxSize / minScreen;
                        if (dist > maxDist) continue; // инстанс отсечён
                        smRenderers[pct]++;
                        if (mesh != null)
                        {
                            for (int sm = 0; sm < mesh.subMeshCount; sm++)
                            {
                                var mat = (mats != null && sm < mats.Length) ? mats[sm] : null;
                                smKeys[pct].Add(HashKey(mesh.GetHashCode(), sm, mat != null ? mat.GetHashCode() : 0));
                            }
                        }
                    }
                }

                for (int i = 0; i < SmallMeshPercentages.Length; i++)
                    smPairs[SmallMeshPercentages[i]] = smKeys[SmallMeshPercentages[i]].Count;

                int pairs = seenKeys.Count;
                int shaderPairs = seenShaderKeys.Count;
                float removed005 = Pct(pairs - smPairs[0.5f], pairs);
                float removed1 = Pct(pairs - smPairs[1f], pairs);
                float removed2 = Pct(pairs - smPairs[2f], pairs);

                raw.AppendLine("CASE;id=" + c.id +
                               ";campos=" + camPos.ToString("F2") +
                               ";camrot=" + camRot.eulerAngles.ToString("F1") +
                               ";renderersEnabled=" + enabled +
                               ";renderersInFrustum=" + inFrustum +
                               ";doorsInFrustum=" + doors +
                               ";pairs=" + pairs +
                               ";shaderPairs=" + shaderPairs +
                               ";atlasGain=" + (pairs - shaderPairs) +
                               ";submeshSlots=" + slots +
                               ";sm0.5=renderers:" + smRenderers[0.5f] + ",pairs:" + smPairs[0.5f] +
                               ",removed:" + removed005.ToString("F1", CultureInfo.InvariantCulture) + "%" +
                               ";sm1=renderers:" + smRenderers[1f] + ",pairs:" + smPairs[1f] +
                               ",removed:" + removed1.ToString("F1", CultureInfo.InvariantCulture) + "%" +
                               ";sm2=renderers:" + smRenderers[2f] + ",pairs:" + smPairs[2f] +
                               ",removed:" + removed2.ToString("F1", CultureInfo.InvariantCulture) + "%" +
                               ";shadowCasters=" + shadowCasters +
                               ";shadow150=keep:" + keepCasters[150f] + ",cut:" + cutCasters[150f] +
                               ";shadow200=keep:" + keepCasters[200f] + ",cut:" + cutCasters[200f] +
                               ";shadow250=keep:" + keepCasters[250f] + ",cut:" + cutCasters[250f] +
                               ";shadow300=keep:" + keepCasters[300f] + ",cut:" + cutCasters[300f]);

                csv.Append(c.id).Append(',')
                   .Append(enabled).Append(',').Append(inFrustum).Append(',').Append(doors).Append(',')
                   .Append(pairs).Append(',').Append(shaderPairs).Append(',').Append(pairs - shaderPairs).Append(',')
                   .Append(slots).Append(',')
                   .Append(smRenderers[0.5f]).Append(',').Append(smPairs[0.5f]).Append(',').Append(removed005.ToString("F1", CultureInfo.InvariantCulture)).Append(',')
                   .Append(smRenderers[1f]).Append(',').Append(smPairs[1f]).Append(',').Append(removed1.ToString("F1", CultureInfo.InvariantCulture)).Append(',')
                   .Append(smRenderers[2f]).Append(',').Append(smPairs[2f]).Append(',').Append(removed2.ToString("F1", CultureInfo.InvariantCulture)).Append(',')
                   .Append(shadowCasters).Append(',')
                   .Append(keepCasters[150f]).Append(',').Append(cutCasters[150f]).Append(',')
                   .Append(keepCasters[200f]).Append(',').Append(cutCasters[200f]).Append(',')
                   .Append(keepCasters[250f]).Append(',').Append(cutCasters[250f]).Append(',')
                   .Append(keepCasters[300f]).Append(',').Append(cutCasters[300f]);
                csv.AppendLine();

                Debug.Log("[Stage4bDryRun] " + c.id + ": inFrustum=" + inFrustum + " pairs=" + pairs +
                          " shaderPairs=" + shaderPairs + " atlasGain=" + (pairs - shaderPairs) +
                          " slots=" + slots + " shadowCasters=" + shadowCasters +
                          " sm0.5=" + removed005.ToString("F1") + "% sm1=" + removed1.ToString("F1") +
                          "% sm2=" + removed2.ToString("F1") + "%" +
                          " shadow150cut=" + cutCasters[150f] + " shadow200cut=" + cutCasters[200f] +
                          " shadow250cut=" + cutCasters[250f]);
            }

            // ------------------------------------------------------------------ B2 dry-run (пилот)
            AppendB2DryRun(pilotChunk, raw, csv);

            raw.AppendLine("BENCH_DONE;result=OK;mode=edit_mode;sceneSaved=no;push=no");
            try
            {
                File.WriteAllText(rawPath, raw.ToString(), Encoding.UTF8);
                File.WriteAllText(csvPath, csv.ToString(), Encoding.UTF8);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Stage4bDryRun] Не удалось записать отчёт: " + e.Message);
            }

            Debug.Log("[Stage4bDryRun] Готово. " + rawPath);
        }

        // ------------------------------------------------------------------------ B2 dry-run

        private static void AppendB2DryRun(Transform pilotChunk, StringBuilder raw, StringBuilder csv)
        {
            raw.AppendLine("--- B2 DRY RUN (pilot chunk, own subtree only) ---");
            if (pilotChunk == null)
            {
                raw.AppendLine("B2;status=chunk_not_found");
                return;
            }

            // ВАЖНО: берём только собственное поддерево чанка. Геометрия соседних чанков,
            // попавшая в распухшие габариты, к B2 этого чанка отношения не имеет.
            var rs = pilotChunk.GetComponentsInChildren<MeshRenderer>(true);
            var meshes = new HashSet<int>();
            var materials = new HashSet<int>();
            var pairs = new HashSet<long>();
            int slots = 0, withCollider = 0, withScript = 0, noMesh = 0;

            for (int i = 0; i < rs.Length; i++)
            {
                var r = rs[i];
                if (r == null) continue;
                if (r.GetComponent<Collider>() != null) withCollider++;
                if (r.GetComponent<MonoBehaviour>() != null) withScript++;

                var mf = r.GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null) { noMesh++; continue; }
                meshes.Add(mesh.GetHashCode());

                var mats = r.sharedMaterials;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    slots++;
                    var mat = (mats != null && s < mats.Length) ? mats[s] : null;
                    int matId = mat != null ? mat.GetHashCode() : 0;
                    if (matId != 0) materials.Add(matId);
                    pairs.Add(HashKey(mesh.GetHashCode(), s, matId));
                }
            }

            int sourceObjects = -1, mergedBatches = -1, nameUnits = -1, houseBatches = -1, houseLargeBatches = -1;
            ReadJRow(pilotChunk.name, out sourceObjects, out mergedBatches, out nameUnits, out houseBatches, out houseLargeBatches);

            // Дом-кандидаты = прямые дети чанка (после мержа большинство из них — пустые якоря).
            int anchors = pilotChunk.childCount;

            string verdict;
            if (houseLargeBatches <= 0 || mergedBatches <= 0) verdict = "no_J_data";
            else if (houseLargeBatches < mergedBatches) verdict = "B2_reduces";
            else if (houseLargeBatches == mergedBatches) verdict = "B2_neutral";
            else verdict = "B2_increases";

            raw.AppendLine("B2;chunk=" + pilotChunk.name +
                           ";renderersInSubtree=" + rs.Length +
                           ";renderersNoMesh=" + noMesh +
                           ";withCollider=" + withCollider +
                           ";withScript=" + withScript +
                           ";uniqueMeshes=" + meshes.Count +
                           ";uniqueMaterials=" + materials.Count +
                           ";submeshSlots=" + slots +
                           ";scenePairs(mesh,submesh,material)=" + pairs.Count +
                           ";houseAnchors=" + anchors +
                           ";J_sourceObjects=" + sourceObjects +
                           ";J_A_mergedBatches=" + mergedBatches +
                           ";J_A_units=" + nameUnits +
                           ";J_B_houseBatches=" + houseBatches +
                           ";J_B2_houseLargeBatches=" + houseLargeBatches +
                           ";verdict=" + verdict);

            csv.AppendLine("# B2 DRY RUN pilot=" + pilotChunk.name +
                           " renderersInSubtree=" + rs.Length +
                           " uniqueMeshes=" + meshes.Count +
                           " uniqueMaterials=" + materials.Count +
                           " submeshSlots=" + slots +
                           " scenePairs=" + pairs.Count +
                           " houseAnchors=" + anchors +
                           " J_sourceObjects=" + sourceObjects +
                           " J_A_mergedBatches=" + mergedBatches +
                           " J_B_houseBatches=" + houseBatches +
                           " J_B2_houseLargeBatches=" + houseLargeBatches +
                           " verdict=" + verdict);

            Debug.Log("[Stage4bDryRun] B2 пилот: в сцене " + rs.Length + " MR / " + pairs.Count +
                      " батчей (" + meshes.Count + " мешей, " + materials.Count + " материалов); " +
                      "J: A=" + mergedBatches + ", B=" + houseBatches + ", B2=" + houseLargeBatches +
                      " -> " + verdict);
        }

        /// <summary>Читает строку пилота из J_city_chunks_variants.csv (оффлайн-данные до мержа).</summary>
        private static void ReadJRow(string chunkName, out int sourceObjects, out int mergedBatches,
                                     out int nameUnits, out int houseBatches, out int houseLargeBatches)
        {
            sourceObjects = mergedBatches = nameUnits = houseBatches = houseLargeBatches = -1;
            try
            {
                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string path = Path.Combine(projectRoot, DataDir, "J_city_chunks_variants.csv");
                if (!File.Exists(path)) return;

                var lines = File.ReadAllLines(path);
                for (int i = 1; i < lines.Length; i++)
                {
                    var cols = lines[i].Split(',');
                    if (cols.Length < 8) continue;
                    if (cols[0].Trim() != chunkName) continue;
                    int.TryParse(cols[1], out sourceObjects);
                    int.TryParse(cols[2], out mergedBatches);
                    int.TryParse(cols[5], out nameUnits);
                    int.TryParse(cols[6], out houseBatches);
                    int.TryParse(cols[7], out houseLargeBatches);
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Stage4bDryRun] J CSV: " + e.Message);
            }
        }

        // ------------------------------------------------------------------------ утилиты

        private static Bounds ComputeChunkBounds(Transform chunk)
        {
            var rs = chunk.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(chunk.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            b.Expand(PilotBoundsInflate);
            return b;
        }

        private static HashSet<Renderer> BuildShipRendererSet(UnityEngine.SceneManagement.Scene scene)
        {
            var set = new HashSet<Renderer>();
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == "WorldRoot_0_0") continue;
                if (go.GetComponentInChildren<ShipRootReference>(true) == null) continue;
                var rs = go.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < rs.Length; i++) set.Add(rs[i]);
            }
            return set;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var hit = FindDeep(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        private static long HashKey(int a, int b, int c)
        {
            unchecked
            {
                long h = 1469598103934665603L;
                h = (h ^ (uint)a) * 1099511628211L;
                h = (h ^ (uint)b) * 1099511628211L;
                h = (h ^ (uint)c) * 1099511628211L;
                return h;
            }
        }

        private static float Pct(int removed, int total)
        {
            return total <= 0 ? 0f : (float)removed / total * 100f;
        }
    }
}
