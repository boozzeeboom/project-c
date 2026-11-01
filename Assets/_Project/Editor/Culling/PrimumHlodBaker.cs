// Project C — T-CULL01 / Stage 7 (H7): bake HLOD/L1 блоков PRIMUM и L2-импостора города.
//
// Только редактор. Рантайм ничего не генерирует: VisibilityDirector читает манифест
// (Assets/_Project/Resources/HLOD/PRIMUM_HLOD.asset) и ставит запечённые меши под WorldRoot_0_0.
//
// L1 (на блок = ребёнок PRIMUM = чанк директора):
//   1. Рендереры по правилам директора (MeshRenderer, активен, не слой 10, не под DoorController).
//   2. Каждый сабмеш копируется (CombineMeshes без матриц → читаемая копия; исходники не читаемы),
//      вершины переводятся в пространство WorldRoot_0_0; цвет материала (главное свойство-цвет
//      процедурного графа) → цвет вершин, класс эмиссии (окно / лампа) → альфа вершин.
//   3. Мелочь: связные компоненты (сварка позиций 1 мм); компонента с габаритом < 0.25 м удаляется
//      (окна и лампы не удаляются — это ночные огни).
//   4. Интерьеры и скрытое: внешняя видимость лучами из 41 направления (сетка 0.75 м, PhysX
//      MeshCollider в preview-сцене, без сварки/чистки, поэтому triangleIndex = исходный).
//      Треугольник, в который не попал ни один луч снаружи, удаляется.
//   5. Один меш на блок, один сабмеш, UInt32 при > 65 535 вершин; pivot = центр AABB.
// L2 (на весь город): 8 азимутов × 3 высоты (0/10/25°) ортокамерой по L1-мешам → атлас
//   альбедо+покрытие и атлас ночных огней (+ нормаль.y), 2× суперсэмплинг, мипы с сохранением
//   покрытия, DXT5. Квад разворачивается к камере в шейдере.
//
// Детерминизм: один и тот же порядок обхода, фиксированная сетка лучей, без случайности (окна — хэш
// координаты). Меню «Rebake Determinism Check» пересчитывает всё в памяти и сравнивает хэши.
//
// Что НЕ делает: не меняет сцену, префабы, FBX, исходные меши/материалы, ProjectSettings, URP.
// Меню: Project C → Culling → Stage 7 HLOD → Bake Pilot Block / Bake All Blocks + L2 /
//        Rebake Determinism Check.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ProjectC.Ship;
using ProjectC.World;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectC.EditorTools
{
    public static class PrimumHlodBaker
    {
        private const string Menu = "Project C/Culling/Stage 7 HLOD/";

        public const string OutDir = "Assets/_Project/Generated/HLOD";
        public const string ManifestAssetPath = "Assets/_Project/Resources/HLOD/PRIMUM_HLOD.asset";
        private const string L1MaterialPath = OutDir + "/PRIMUM_HLOD_L1.mat";
        private const string L2MaterialPath = OutDir + "/PRIMUM_HLOD_L2.mat";
        private const string L2MeshPath = OutDir + "/PRIMUM_L2_Impostor.asset";
        private const string L2AlbedoPath = OutDir + "/PRIMUM_L2_Albedo.asset";
        private const string L2NightPath = OutDir + "/PRIMUM_L2_Night.asset";
        private const string DataDir = "docs/world/primum_city/10_CULLING_RESEARCH/08_DATA";
        private const string DateTag = "2026-11-01";

        public const string PilotBlock = "PRIMUM_HOUSES_S9_TRD-S2_P1_45H";
        private const string WorldRootName = "WorldRoot_0_0";
        private const string CityRootName = "PRIMUM";
        private const string L1ShaderName = "ProjectC/PrimumHlod";
        private const string L2ShaderName = "ProjectC/PrimumImpostor";
        private const int DoorLayer = 10;

        // ---- параметры bake (входят в bakeSettings и в отчёт) ----
        private const float SmallDetail = 0.25f;
        private const float WeldQuantum = 0.001f;
        private const float RaySpacing = 0.75f;
        private const int MaxRaysPerDirection = 250000;
        private const int VisibilityMinTriangles = 20000;
        private const int WindowLitPercent = 55;
        private static readonly float[] RayElevations = { -15f, 5f, 25f, 50f, 75f };
        private const int RayAzimuths = 8;

        private const int AzimuthCount = 8;
        // 4 ряда × 8 азимутов по 512×256 = атлас 4096×1024 (POT — иначе DXT5 с мипами недоступен).
        private static readonly float[] RowElevations = { 0f, 8f, 20f, 45f };
        private const int FrameW = 512;
        private const int FrameH = 256;
        private const int Supersample = 2;
        private const float NightScale = 4f;
        private const int ImpostorMips = 6;
        private const float AlphaCutoff = 0.5f;

        private const byte ClassOpaque = 0;
        private const byte ClassWindowDark = 85;
        private const byte ClassWindowLit = 170;
        private const byte ClassLamp = 255;

        private static readonly string[] PrimaryColorProps =
        {
            "_BaseColor", "_Wood_Color", "_Bronze_Color", "_Stone_Color", "_Concrete_Color", "_Tile_Color",
            "_Brick_Color", "_Iron_Color", "_Plate_Color", "_Fabric_Color", "_Marble_Color", "_Leaf_Color",
            "_Steel_Color", "_Plastic_Color", "_Rock_Color", "_Bark_Color", "_Glass_Color", "_Color"
        };

        private static string Settings
        {
            get
            {
                return "small=" + F(SmallDetail) + ";weld=" + F(WeldQuantum) + ";raySpacing=" + F(RaySpacing) +
                       ";maxRaysPerDir=" + MaxRaysPerDirection + ";visMinTris=" + VisibilityMinTriangles +
                       ";rayDirs=" + (RayElevations.Length * RayAzimuths + 1) + ";lampPass=glass_non_occluding" +
                       ";windowsLit=" + WindowLitPercent +
                       "%;atlas=" + AzimuthCount + "x" + RowElevations.Length + "@" + FrameW + "x" + FrameH +
                       "ss" + Supersample + ";rows=" + string.Join("/", Array.ConvertAll(RowElevations, F)) +
                       ";mips=" + ImpostorMips + ";nightScale=" + F(NightScale) + ";format=DXT5";
            }
        }

        // =================================================================== данные

        private sealed class BlockData
        {
            public string name;
            public int layer;
            public int sourceRenderers;
            public int sourceTriangles;
            public int smallRemoved;
            public int hiddenRemoved;
            public int mirrored;
            public int rays;
            public int sanityChecked;
            public int sanityBad;
            public bool visibilitySkipped;
            public Vector3 pivot;
            public Vector3 size;
            public Vector3[] positions; // относительно pivot
            public Vector3[] normals;
            public Color32[] colors;
            public Color32[] surface; // r = metallic, g = smoothness (исходного материала)
            public int[] triangles;
            public string hash;
            public double ms;
            public Mesh mesh; // временный (для bake L2) или ассет после записи
        }

        private sealed class ImpostorData
        {
            public Vector3 center;
            public Vector3 size;
            public float halfW;
            public float[] halfH;
            public float metallic;
            public float smoothness;
            public Color32[][] albedoMips;
            public Color32[][] nightMips;
            public int width;
            public int height;
            public string hash;
            public double ms;
        }

        private enum Mode { Pilot, All, Verify }

        // =================================================================== меню

        [MenuItem(Menu + "Bake Pilot Block")]
        public static void BakePilot() { Run(Mode.Pilot); }

        [MenuItem(Menu + "Bake All Blocks + L2")]
        public static void BakeAll() { Run(Mode.All); }

        [MenuItem(Menu + "Rebake Determinism Check")]
        public static void VerifyDeterminism() { Run(Mode.Verify); }

        /// <summary>Публичный статус последнего запуска (для автоматизации/отчёта).</summary>
        public static string LastResult { get; private set; }

        private static void Run(Mode mode)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[PrimumHlodBaker] Bake только в редакторе, не в Play Mode.");
                return;
            }

            var worldRoot = FindWorldRoot();
            var city = worldRoot != null ? worldRoot.Find(CityRootName) : null;
            if (city == null)
            {
                Debug.LogError("[PrimumHlodBaker] Не найден " + WorldRootName + "/" + CityRootName + ".");
                return;
            }

            var shader = Shader.Find(L1ShaderName);
            var impShader = Shader.Find(L2ShaderName);
            if (shader == null || impShader == null)
            {
                Debug.LogError("[PrimumHlodBaker] Шейдеры " + L1ShaderName + " / " + L2ShaderName + " не найдены.");
                return;
            }

            bool sceneDirtyBefore = SceneManager.GetActiveScene().isDirty;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var blocks = new List<BlockData>();
            var csv = new StringBuilder();
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string csvPath = Path.Combine(projectRoot, DataDir, "U_stage7_bake_" + DateTag + ".csv");
            bool header = !File.Exists(csvPath);
            if (header)
                csv.AppendLine("run,mode,block,sourceRenderers,sourceTris,smallRemoved,hiddenRemoved,finalTris,vertices,mirrored,rays,visSkipped,sanityChecked,sanityBad,sizeX,sizeY,sizeZ,ms,hash,expectedHash,match");

            string run = DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture);
            PrimumHlodManifest manifest = mode == Mode.Verify ? AssetDatabase.LoadAssetAtPath<PrimumHlodManifest>(ManifestAssetPath) : null;
            if (mode == Mode.Verify && manifest == null)
            {
                Debug.LogError("[PrimumHlodBaker] Нет манифеста для проверки: " + ManifestAssetPath);
                return;
            }

            var mat = mode == Mode.Verify ? manifest.l1Material : EnsureMaterial(L1MaterialPath, shader);
            int mismatches = 0;

            try
            {
                for (int c = 0; c < city.childCount; c++)
                {
                    var child = city.GetChild(c);
                    if (mode == Mode.Pilot && child.name != PilotBlock) continue;

                    EditorUtility.DisplayProgressBar("PRIMUM HLOD", child.name, (float)c / Mathf.Max(1, city.childCount));
                    var data = BakeBlock(child, worldRoot);
                    if (data == null) continue;
                    blocks.Add(data);

                    string expected = "";
                    bool match = true;
                    if (mode == Mode.Verify)
                    {
                        int bi = manifest.FindBlock(data.name);
                        expected = bi >= 0 ? manifest.blocks[bi].hash : "missing";
                        match = expected == data.hash;
                        if (!match) mismatches++;
                    }

                    csv.Append(run).Append(',').Append(mode).Append(',').Append(data.name).Append(',')
                       .Append(data.sourceRenderers).Append(',').Append(data.sourceTriangles).Append(',')
                       .Append(data.smallRemoved).Append(',').Append(data.hiddenRemoved).Append(',')
                       .Append(data.triangles.Length / 3).Append(',').Append(data.positions.Length).Append(',')
                       .Append(data.mirrored).Append(',').Append(data.rays).Append(',').Append(data.visibilitySkipped ? 1 : 0).Append(',')
                       .Append(data.sanityChecked).Append(',').Append(data.sanityBad).Append(',')
                       .Append(F(data.size.x)).Append(',').Append(F(data.size.y)).Append(',').Append(F(data.size.z)).Append(',')
                       .Append(data.ms.ToString("F0", CultureInfo.InvariantCulture)).Append(',')
                       .Append(data.hash).Append(',').Append(expected).Append(',').Append(match ? 1 : 0).AppendLine();

                    Debug.Log("[PrimumHlodBaker] " + data.name + ": рендереров " + data.sourceRenderers + ", тр. " +
                              data.sourceTriangles + " → " + data.triangles.Length / 3 + " (мелочь −" + data.smallRemoved +
                              ", скрытые −" + data.hiddenRemoved + (data.visibilitySkipped ? ", видимость пропущена" : "") +
                              "), вершин " + data.positions.Length + ", лучей " + data.rays + ", sanity " + data.sanityBad + "/" +
                              data.sanityChecked + ", " + data.ms.ToString("F0", CultureInfo.InvariantCulture) + " мс" +
                              (mode == Mode.Verify ? (match ? ", хэш совпал" : ", ХЭШ НЕ СОВПАЛ") : ""));
                }

                foreach (var b in blocks) b.mesh = BuildMesh(b, b.name + "_L1");

                ImpostorData imp = null;
                if (mode == Mode.All || (mode == Mode.Verify && manifest.impostorValid))
                {
                    EditorUtility.DisplayProgressBar("PRIMUM HLOD", "L2 impostor", 0.95f);
                    imp = BakeImpostor(blocks, mat);
                    string expected = mode == Mode.Verify ? manifest.impostorHash : "";
                    bool match = mode != Mode.Verify || expected == imp.hash;
                    if (!match) mismatches++;
                    csv.Append(run).Append(',').Append(mode).Append(",L2_IMPOSTOR,").Append(blocks.Count).Append(",0,0,0,2,4,0,0,0,0,0,")
                       .Append(F(imp.size.x)).Append(',').Append(F(imp.size.y)).Append(',').Append(F(imp.size.z)).Append(',')
                       .Append(imp.ms.ToString("F0", CultureInfo.InvariantCulture)).Append(',').Append(imp.hash).Append(',')
                       .Append(expected).Append(',').Append(match ? 1 : 0).AppendLine();
                    WritePreview(imp, Path.Combine(projectRoot, DataDir, "U_stage7_L2_atlas_preview.png"));
                }

                // Полный блок vs L1 — для КАЖДОГО блока (PNG — только пилот и ADMIN с куполом).
                if (mode != Mode.Verify)
                {
                    foreach (var b in blocks)
                    {
                        string png = null;
                        if (b.name == PilotBlock) png = Path.Combine(projectRoot, DataDir, "U_stage7_L1_pilot_preview.png");
                        else if (b.name.Contains("_ADMIN_")) png = Path.Combine(projectRoot, DataDir, "U_stage7_L1_admin_preview.png");
                        WriteBlockComparison(city.Find(b.name), worldRoot, b, mat, png, csv, run);
                    }
                }

                if (mode != Mode.Verify)
                    WriteAssets(mode, blocks, imp, mat, impShader);

                string bakeHash = CombinedHash(blocks, imp);
                if (mode == Mode.Verify)
                {
                    bool overall = mismatches == 0 && bakeHash == manifest.bakeHash;
                    LastResult = (overall ? "DETERMINISTIC" : "MISMATCH") + ";mismatches=" + mismatches + ";bakeHash=" + bakeHash +
                                 ";manifestHash=" + manifest.bakeHash;
                }
                else
                {
                    LastResult = "BAKED;mode=" + mode + ";blocks=" + blocks.Count + ";impostor=" + (imp != null) + ";bakeHash=" + bakeHash;
                }

                foreach (var b in blocks)
                    if (b.mesh != null && !AssetDatabase.Contains(b.mesh)) Object.DestroyImmediate(b.mesh);
            }
            catch (Exception e)
            {
                LastResult = "EXCEPTION;" + e.GetType().Name + ":" + e.Message;
                Debug.LogError("[PrimumHlodBaker] Исключение: " + e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            bool sceneDirtyAfter = SceneManager.GetActiveScene().isDirty;
            csv.Append("#").Append(run).Append(",result=").Append(LastResult).Append(",totalMs=")
               .Append(sw.ElapsedMilliseconds).Append(",sceneDirtyBefore=").Append(sceneDirtyBefore)
               .Append(",sceneDirtyAfter=").Append(sceneDirtyAfter).Append(",settings=").Append(Settings.Replace(',', ' ')).AppendLine();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(csvPath));
                File.AppendAllText(csvPath, csv.ToString(), Encoding.UTF8);
            }
            catch (Exception e) { Debug.LogWarning("[PrimumHlodBaker] CSV: " + e.Message); }

            Debug.Log("[PrimumHlodBaker] " + LastResult + ", " + sw.ElapsedMilliseconds + " мс, сцена dirty " +
                      sceneDirtyBefore + "→" + sceneDirtyAfter + ". CSV: " + csvPath);
        }

        // ============================================================= L1: блок

        private static BlockData BakeBlock(Transform block, Transform worldRoot)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var renderers = CollectRenderers(block);
            if (renderers.Count == 0) return null;

            var data = new BlockData { name = block.name, sourceRenderers = renderers.Count };
            var layerVotes = new Dictionary<int, int>();

            var P = new List<Vector3>(1 << 16);
            var N = new List<Vector3>(1 << 16);
            var C = new List<Color32>(1 << 16);
            var S = new List<Color32>(1 << 16);
            var T = new List<int>(1 << 18);

            Matrix4x4 toRoot = worldRoot.worldToLocalMatrix;
            var tmp = new Mesh { indexFormat = IndexFormat.UInt32 };
            var single = new CombineInstance[1];

            try
            {
                for (int ri = 0; ri < renderers.Count; ri++)
                {
                    var r = renderers[ri];
                    var src = r.GetComponent<MeshFilter>().sharedMesh;
                    var mats = r.sharedMaterials;
                    int subCount = Mathf.Min(src.subMeshCount, mats.Length);
                    int layer = r.gameObject.layer;
                    int votes;
                    layerVotes.TryGetValue(layer, out votes);
                    layerVotes[layer] = votes + 1;

                    Matrix4x4 m = toRoot * r.localToWorldMatrix;
                    Matrix4x4 nm = m.inverse.transpose;
                    bool mirrored = m.determinant < 0f;
                    if (mirrored) data.mirrored++;

                    for (int s = 0; s < subCount; s++)
                    {
                        var material = mats[s];
                        if (material == null) continue;
                        Color32 color;
                        byte cls;
                        Color32 surf;
                        Classify(material, out color, out cls, out surf);

                        tmp.Clear();
                        single[0] = new CombineInstance { mesh = src, subMeshIndex = s, transform = Matrix4x4.identity };
                        tmp.CombineMeshes(single, true, false, false);
                        var v = tmp.vertices;
                        var n = tmp.normals;
                        if (n == null || n.Length != v.Length)
                        {
                            tmp.RecalculateNormals();
                            n = tmp.normals;
                        }
                        var idx = tmp.GetTriangles(0);
                        data.sourceTriangles += idx.Length / 3;

                        var remap = new int[v.Length];
                        for (int i = 0; i < remap.Length; i++) remap[i] = -1;

                        for (int t = 0; t + 2 < idx.Length; t += 3)
                        {
                            int a = idx[t], b = idx[t + 1], c = idx[t + 2];
                            if (mirrored) { int sw2 = b; b = c; c = sw2; }
                            T.Add(Remap(a, remap, v, n, m, nm, color, cls, surf, P, N, C, S));
                            T.Add(Remap(b, remap, v, n, m, nm, color, cls, surf, P, N, C, S));
                            T.Add(Remap(c, remap, v, n, m, nm, color, cls, surf, P, N, C, S));
                        }
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(tmp);
            }

            int bestLayer = 8, bestVotes = -1;
            foreach (var kv in layerVotes)
                if (kv.Value > bestVotes || (kv.Value == bestVotes && kv.Key < bestLayer)) { bestLayer = kv.Key; bestVotes = kv.Value; }
            data.layer = bestLayer;

            var pos = P.ToArray();
            var tris = T.ToArray();
            var cols = C.ToArray();
            int triCount = tris.Length / 3;

            // 3. мелочь + решение «окно горит» по компоненте
            var keep = new bool[triCount];
            ComponentPass(pos, tris, cols, keep, out data.smallRemoved);

            // 4. внешняя видимость
            int kept = 0;
            for (int t = 0; t < triCount; t++) if (keep[t]) kept++;
            if (kept >= VisibilityMinTriangles)
            {
                var subTris = new int[kept * 3];
                var map = new int[kept];
                int k = 0;
                for (int t = 0; t < triCount; t++)
                {
                    if (!keep[t]) continue;
                    subTris[k * 3] = tris[t * 3];
                    subTris[k * 3 + 1] = tris[t * 3 + 1];
                    subTris[k * 3 + 2] = tris[t * 3 + 2];
                    map[k] = t;
                    k++;
                }
                var vis = ComputeVisibility(pos, subTris, null, out data.rays, out data.sanityChecked, out data.sanityBad);

                // Лампы в L0 видны сквозь прозрачное стекло (ламповые кожухи, окна): для них — второй проход,
                // в котором стекло не заслоняет (иначе 99 % ламп считаются скрытыми).
                bool hasLamps = false;
                var occluder = new bool[kept];
                for (int i = 0; i < kept; i++)
                {
                    byte cls = cols[subTris[i * 3]].a;
                    occluder[i] = cls != ClassWindowDark && cls != ClassWindowLit;
                    if (cls == ClassLamp) hasLamps = true;
                }
                bool[] visGlassFree = null;
                if (hasLamps)
                {
                    int raysA, sc, sb;
                    visGlassFree = ComputeVisibility(pos, subTris, occluder, out raysA, out sc, out sb);
                    data.rays += raysA;
                    data.sanityChecked += sc;
                    data.sanityBad += sb;
                }

                for (int i = 0; i < kept; i++)
                {
                    bool v = vis[i];
                    if (!v && visGlassFree != null && cols[subTris[i * 3]].a == ClassLamp) v = visGlassFree[i];
                    if (v) continue;
                    keep[map[i]] = false;
                    data.hiddenRemoved++;
                }
            }
            else
            {
                data.visibilitySkipped = true;
            }

            // 5. компактный результат, pivot = центр AABB
            Compact(pos, N.ToArray(), cols, S.ToArray(), tris, keep, data);
            data.hash = HashBlock(data);
            data.ms = sw.Elapsed.TotalMilliseconds;
            return data;
        }

        private static int Remap(int i, int[] remap, Vector3[] v, Vector3[] n, Matrix4x4 m, Matrix4x4 nm,
                                 Color32 color, byte cls, Color32 surf, List<Vector3> P, List<Vector3> N, List<Color32> C,
                                 List<Color32> S)
        {
            int r = remap[i];
            if (r >= 0) return r;
            r = P.Count;
            remap[i] = r;
            P.Add(m.MultiplyPoint3x4(v[i]));
            Vector3 nn = nm.MultiplyVector(n[i]);
            float len = nn.magnitude;
            N.Add(len > 1e-8f ? nn / len : Vector3.up);
            C.Add(new Color32(color.r, color.g, color.b, cls));
            S.Add(surf);
            return r;
        }

        private static List<MeshRenderer> CollectRenderers(Transform block)
        {
            var list = new List<MeshRenderer>();
            var all = block.GetComponentsInChildren<MeshRenderer>(false);
            for (int i = 0; i < all.Length; i++)
            {
                var r = all[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r.gameObject.layer == DoorLayer) continue;
                if (r.forceRenderingOff) continue;
                if (r.GetComponentInParent<DoorController>(true) != null) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                list.Add(r);
            }
            return list;
        }

        /// <summary>
        /// Цвет, класс эмиссии и PBR-параметры материала (детерминированно, только свойства материала).
        /// surf.r = _Metallic, surf.g = _Smoothness: исходные графы — URP Lit, L1 должен освещаться тем же
        /// PBR, иначе на переходе 3 км металл (бронза 0.9) и глянец «гаснут».
        /// </summary>
        private static void Classify(Material m, out Color32 color, out byte cls, out Color32 surf)
        {
            float metallic = TryFloat(m, "_Metallic", 0f);
            float smoothness = TryFloat(m, "_Smoothness", 0.3f);
            surf = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(metallic) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(smoothness) * 255f), 0, 0);
            Classify(m, out color, out cls);
            if (cls == ClassWindowDark) surf = new Color32(0, 217, 0, 0); // стекло: гладкое диэлектрическое
        }

        private static float TryFloat(Material m, string prop, float fallback)
        {
            var sh = m.shader;
            int idx = sh != null ? sh.FindPropertyIndex(prop) : -1;
            if (idx < 0) return fallback;
            var t = sh.GetPropertyType(idx);
            if (t != ShaderPropertyType.Float && t != ShaderPropertyType.Range) return fallback;
            return m.GetFloat(prop);
        }

        /// <summary>Цвет и класс эмиссии материала (детерминированно, только свойства материала).</summary>
        private static void Classify(Material m, out Color32 color, out byte cls)
        {
            string shaderName = m.shader != null ? m.shader.name : "";
            string matName = m.name ?? "";
            Color c;

            if (matName.IndexOf("LAMP_GLOW", StringComparison.OrdinalIgnoreCase) >= 0 ||
                shaderName.IndexOf("Lantern", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                c = TryColor(m, "_Glow_Color", out c) ? c : (TryColor(m, "_BaseColor", out c) ? c : new Color(1f, 0.8f, 0.5f));
                float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                if (max > 1f) c = new Color(c.r / max, c.g / max, c.b / max, 1f);
                color = (Color32)c;
                cls = ClassLamp;
                return;
            }

            if (shaderName.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (!TryColor(m, "_Glass_Color", out c)) c = new Color(0.3f, 0.35f, 0.38f);
                color = (Color32)c;
                cls = ClassWindowDark; // решение «горит/не горит» — по компоненте в ComponentPass
                return;
            }

            for (int i = 0; i < PrimaryColorProps.Length; i++)
            {
                if (TryColor(m, PrimaryColorProps[i], out c))
                {
                    color = (Color32)Clamp01(c);
                    cls = ClassOpaque;
                    return;
                }
            }

            var sh = m.shader;
            if (sh != null)
            {
                int count = sh.GetPropertyCount();
                for (int i = 0; i < count; i++)
                {
                    if (sh.GetPropertyType(i) != ShaderPropertyType.Color) continue;
                    color = (Color32)Clamp01(m.GetColor(sh.GetPropertyName(i)));
                    cls = ClassOpaque;
                    return;
                }
            }

            color = new Color32(128, 128, 128, 0);
            cls = ClassOpaque;
        }

        private static bool TryColor(Material m, string prop, out Color c)
        {
            c = Color.gray;
            if (!m.HasProperty(prop)) return false;
            var sh = m.shader;
            int idx = sh != null ? sh.FindPropertyIndex(prop) : -1;
            if (idx < 0 || sh.GetPropertyType(idx) != ShaderPropertyType.Color) return false;
            c = m.GetColor(prop);
            return true;
        }

        private static Color Clamp01(Color c)
        {
            return new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);
        }

        // --------------------------------------------------------- компоненты

        private static int Find(int[] parent, int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }

        private static void Union(int[] parent, int a, int b)
        {
            a = Find(parent, a);
            b = Find(parent, b);
            if (a == b) return;
            if (a < b) parent[b] = a; else parent[a] = b; // детерминированно: корень — меньший индекс
        }

        /// <summary>
        /// Связные компоненты по сварке позиций: компонента меньше SmallDetail удаляется (кроме окон и
        /// ламп), окно «горит» по хэшу центра компоненты (WindowLitPercent %).
        /// </summary>
        private static void ComponentPass(Vector3[] pos, int[] tris, Color32[] cols, bool[] keep, out int smallRemoved)
        {
            int vc = pos.Length;
            var parent = new int[vc];
            for (int i = 0; i < vc; i++) parent[i] = i;

            var weld = new Dictionary<Vector3Int, int>(vc);
            float inv = 1f / WeldQuantum;
            for (int i = 0; i < vc; i++)
            {
                var p = pos[i];
                var key = new Vector3Int(Mathf.RoundToInt(p.x * inv), Mathf.RoundToInt(p.y * inv), Mathf.RoundToInt(p.z * inv));
                int other;
                if (weld.TryGetValue(key, out other)) Union(parent, i, other);
                else weld.Add(key, i);
            }
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                Union(parent, tris[t], tris[t + 1]);
                Union(parent, tris[t], tris[t + 2]);
            }

            var min = new Vector3[vc];
            var max = new Vector3[vc];
            var has = new bool[vc];
            for (int i = 0; i < vc; i++)
            {
                int root = Find(parent, i);
                var p = pos[i];
                if (!has[root]) { min[root] = p; max[root] = p; has[root] = true; }
                else { min[root] = Vector3.Min(min[root], p); max[root] = Vector3.Max(max[root], p); }
            }

            // окна: «горит» — по центру компоненты, одинаково для всех её вершин
            for (int i = 0; i < vc; i++)
            {
                if (cols[i].a != ClassWindowDark) continue;
                int root = Find(parent, i);
                Vector3 center = (min[root] + max[root]) * 0.5f;
                int hx = Mathf.FloorToInt(center.x * 2f), hy = Mathf.FloorToInt(center.y * 2f), hz = Mathf.FloorToInt(center.z * 2f);
                uint h = (uint)(hx * 73856093) ^ (uint)(hy * 19349663) ^ (uint)(hz * 83492791);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                var c = cols[i];
                c.a = (h % 100u) < (uint)WindowLitPercent ? ClassWindowLit : ClassWindowDark;
                cols[i] = c;
            }

            smallRemoved = 0;
            int triCount = tris.Length / 3;
            for (int t = 0; t < triCount; t++)
            {
                int a = tris[t * 3];
                byte cls = cols[a].a;
                if (cls != ClassOpaque) { keep[t] = true; continue; }
                int root = Find(parent, a);
                Vector3 ext = max[root] - min[root];
                float size = Mathf.Max(ext.x, Mathf.Max(ext.y, ext.z));
                keep[t] = size >= SmallDetail;
                if (!keep[t]) smallRemoved++;
            }
        }

        // --------------------------------------------------------- видимость

        private static Vector3 ViewDir(float azDeg, float elDeg)
        {
            float az = azDeg * Mathf.Deg2Rad, el = elDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(el) * Mathf.Sin(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(az));
        }

        private static List<Vector3> RayDirections()
        {
            var dirs = new List<Vector3>();
            for (int e = 0; e < RayElevations.Length; e++)
            {
                float offset = (e % 2) * (180f / RayAzimuths);
                for (int a = 0; a < RayAzimuths; a++)
                    dirs.Add(ViewDir(offset + a * 360f / RayAzimuths, RayElevations[e]));
            }
            dirs.Add(Vector3.up);
            return dirs;
        }

        /// <summary>
        /// Видимость треугольников снаружи: сетка лучей из каждого направления, ближайшее попадание.
        /// occluder != null — в коллайдер идут только occluder[i] (стекло «прозрачное»), индексы попаданий
        /// переводятся обратно. Preview-сцена с собственной физикой — активная сцена не трогается.
        /// </summary>
        private static bool[] ComputeVisibility(Vector3[] pos, int[] trisAll, bool[] occluder, out int rays, out int sanityChecked, out int sanityBad)
        {
            int triCountAll = trisAll.Length / 3;
            var visibleAll = new bool[triCountAll];
            int[] backMap = null;
            int[] tris = trisAll;
            if (occluder != null)
            {
                int n = 0;
                for (int i = 0; i < triCountAll; i++) if (occluder[i]) n++;
                tris = new int[n * 3];
                backMap = new int[n];
                int k2 = 0;
                for (int i = 0; i < triCountAll; i++)
                {
                    if (!occluder[i]) continue;
                    tris[k2 * 3] = trisAll[i * 3]; tris[k2 * 3 + 1] = trisAll[i * 3 + 1]; tris[k2 * 3 + 2] = trisAll[i * 3 + 2];
                    backMap[k2] = i;
                    k2++;
                }
            }
            int triCount = tris.Length / 3;
            var visible = new bool[triCount];
            rays = 0;
            sanityChecked = 0;
            sanityBad = 0;
            if (triCount == 0) return visibleAll;

            var bounds = new Bounds(pos[tris[0]], Vector3.zero);
            for (int i = 0; i < tris.Length; i++) bounds.Encapsulate(pos[tris[i]]);
            Vector3 center = bounds.center;
            float radius = bounds.extents.magnitude + 1f;

            var scene = EditorSceneManager.NewPreviewScene();
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            try
            {
                mesh.SetVertices(pos);
                mesh.SetTriangles(tris, 0, true);
                var go = new GameObject("HLOD_Visibility");
                SceneManager.MoveGameObjectToScene(go, scene);
                var mc = go.AddComponent<MeshCollider>();
                mc.cookingOptions = MeshColliderCookingOptions.UseFastMidphase; // без чистки/сварки: индексы = исходные
                mc.sharedMesh = mesh;
                Physics.SyncTransforms();
                var ps = scene.GetPhysicsScene();
                var qp = new QueryParameters(~0, false, QueryTriggerInteraction.Ignore, false);

                var corners = new Vector3[8];
                for (int i = 0; i < 8; i++)
                    corners[i] = center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));

                foreach (var view in RayDirections())
                {
                    Vector3 dir = -view;
                    Vector3 u = Vector3.Cross(Mathf.Abs(dir.y) > 0.99f ? Vector3.forward : Vector3.up, dir).normalized;
                    Vector3 w = Vector3.Cross(dir, u);
                    float umin = float.MaxValue, umax = float.MinValue, wmin = float.MaxValue, wmax = float.MinValue;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 d = corners[i] - center;
                        float pu = Vector3.Dot(d, u), pw = Vector3.Dot(d, w);
                        umin = Mathf.Min(umin, pu); umax = Mathf.Max(umax, pu);
                        wmin = Mathf.Min(wmin, pw); wmax = Mathf.Max(wmax, pw);
                    }
                    float area = (umax - umin) * (wmax - wmin);
                    float spacing = Mathf.Max(RaySpacing, Mathf.Sqrt(area / MaxRaysPerDirection));
                    int nu = Mathf.Max(1, Mathf.CeilToInt((umax - umin) / spacing));
                    int nw = Mathf.Max(1, Mathf.CeilToInt((wmax - wmin) / spacing));
                    int count = nu * nw;
                    rays += count;

                    var cmds = new NativeArray<RaycastCommand>(count, Allocator.TempJob);
                    var hits = new NativeArray<RaycastHit>(count, Allocator.TempJob);
                    try
                    {
                        Vector3 back = center - dir * (radius + 50f);
                        float dist = radius * 2f + 100f;
                        int k = 0;
                        for (int j = 0; j < nw; j++)
                        {
                            float pw = wmin + (j + 0.5f) * spacing;
                            for (int i = 0; i < nu; i++)
                            {
                                float pu = umin + (i + 0.5f) * spacing;
                                cmds[k++] = new RaycastCommand(ps, back + u * pu + w * pw, dir, qp, dist);
                            }
                        }
                        RaycastCommand.ScheduleBatch(cmds, hits, 256, 1, default(Unity.Jobs.JobHandle)).Complete();

                        for (int h = 0; h < count; h++)
                        {
                            var hit = hits[h];
                            if (hit.distance <= 0f) continue;
                            int t = hit.triangleIndex;
                            if (t < 0 || t >= triCount) continue;
                            visible[t] = true;

                            if (h % 997 == 0)
                            {
                                sanityChecked++;
                                Vector3 a = pos[tris[t * 3]], b = pos[tris[t * 3 + 1]], c = pos[tris[t * 3 + 2]];
                                Vector3 nrm = Vector3.Cross(b - a, c - a);
                                float len = nrm.magnitude;
                                if (len > 1e-10f && Mathf.Abs(Vector3.Dot(hit.point - a, nrm / len)) > 0.05f) sanityBad++;
                            }
                        }
                    }
                    finally
                    {
                        cmds.Dispose();
                        hits.Dispose();
                    }
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                Object.DestroyImmediate(mesh);
            }
            if (backMap == null) return visible;
            for (int i = 0; i < triCount; i++) if (visible[i]) visibleAll[backMap[i]] = true;
            return visibleAll;
        }

        // ------------------------------------------------------------ сборка

        private static void Compact(Vector3[] pos, Vector3[] nrm, Color32[] cols, Color32[] surf, int[] tris, bool[] keep, BlockData data)
        {
            var remap = new int[pos.Length];
            for (int i = 0; i < remap.Length; i++) remap[i] = -1;
            var P = new List<Vector3>();
            var N = new List<Vector3>();
            var C = new List<Color32>();
            var S = new List<Color32>();
            var T = new List<int>();
            int triCount = tris.Length / 3;
            for (int t = 0; t < triCount; t++)
            {
                if (!keep[t]) continue;
                for (int k = 0; k < 3; k++)
                {
                    int v = tris[t * 3 + k];
                    int r = remap[v];
                    if (r < 0)
                    {
                        r = P.Count;
                        remap[v] = r;
                        P.Add(pos[v]);
                        N.Add(nrm[v]);
                        C.Add(cols[v]);
                        S.Add(surf[v]);
                    }
                    T.Add(r);
                }
            }

            var b = P.Count > 0 ? new Bounds(P[0], Vector3.zero) : new Bounds();
            for (int i = 1; i < P.Count; i++) b.Encapsulate(P[i]);
            data.pivot = b.center;
            data.size = b.size;
            for (int i = 0; i < P.Count; i++) P[i] -= data.pivot;
            data.positions = P.ToArray();
            data.normals = N.ToArray();
            data.colors = C.ToArray();
            data.surface = S.ToArray();
            data.triangles = T.ToArray();
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct HlodVertex
        {
            public Vector3 position;
            public sbyte nx, ny, nz, nw;
            public Color32 color;
            public Color32 surface;
        }

        private static readonly VertexAttributeDescriptor[] HlodLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.SNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.UNorm8, 4),
        };

        private static sbyte Snorm8(float v)
        {
            return (sbyte)Mathf.Clamp(Mathf.RoundToInt(v * 127f), -127, 127);
        }

        /// <summary>Компактный формат вершины L1: позиция f32×3, нормаль SNorm8×4, цвет UNorm8×4, PBR (UV0) UNorm8×4 = 24 байта.</summary>
        private static Mesh BuildMesh(BlockData b, string meshName)
        {
            var mesh = new Mesh { name = meshName };
            int n = b.positions.Length;
            mesh.indexFormat = n > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertexBufferParams(n, HlodLayout);
            var verts = new HlodVertex[n];
            for (int i = 0; i < n; i++)
            {
                var nn = b.normals[i];
                verts[i] = new HlodVertex
                {
                    position = b.positions[i],
                    nx = Snorm8(nn.x), ny = Snorm8(nn.y), nz = Snorm8(nn.z), nw = 0,
                    color = b.colors[i],
                    surface = b.surface != null && i < b.surface.Length ? b.surface[i] : new Color32(0, 77, 0, 0)
                };
            }
            mesh.SetVertexBufferData(verts, 0, 0, n, 0, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            mesh.SetIndexBufferParams(b.triangles.Length, mesh.indexFormat);
            if (mesh.indexFormat == IndexFormat.UInt16)
            {
                var idx16 = new ushort[b.triangles.Length];
                for (int i = 0; i < idx16.Length; i++) idx16[i] = (ushort)b.triangles[i];
                mesh.SetIndexBufferData(idx16, 0, 0, idx16.Length, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            }
            else
            {
                mesh.SetIndexBufferData(b.triangles, 0, 0, b.triangles.Length, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            }
            mesh.subMeshCount = 1;
            var bounds = new Bounds(Vector3.zero, b.size);
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, b.triangles.Length, MeshTopology.Triangles) { bounds = bounds, vertexCount = n },
                            MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            mesh.bounds = bounds;
            return mesh;
        }

        private static string HashBlock(BlockData b)
        {
            using (var sha = SHA1.Create())
            {
                AppendFloats(sha, b.pivot);
                var buf = new byte[b.positions.Length * 12];
                Buffer.BlockCopy(ToFloats(b.positions), 0, buf, 0, buf.Length);
                sha.TransformBlock(buf, 0, buf.Length, null, 0);
                Buffer.BlockCopy(ToFloats(b.normals), 0, buf, 0, buf.Length);
                sha.TransformBlock(buf, 0, buf.Length, null, 0);
                var cb = new byte[b.colors.Length * 4];
                for (int i = 0; i < b.colors.Length; i++)
                {
                    cb[i * 4] = b.colors[i].r; cb[i * 4 + 1] = b.colors[i].g; cb[i * 4 + 2] = b.colors[i].b; cb[i * 4 + 3] = b.colors[i].a;
                }
                sha.TransformBlock(cb, 0, cb.Length, null, 0);
                if (b.surface != null)
                {
                    var sbuf = new byte[b.surface.Length * 2];
                    for (int i = 0; i < b.surface.Length; i++) { sbuf[i * 2] = b.surface[i].r; sbuf[i * 2 + 1] = b.surface[i].g; }
                    sha.TransformBlock(sbuf, 0, sbuf.Length, null, 0);
                }
                var ib = new byte[b.triangles.Length * 4];
                Buffer.BlockCopy(b.triangles, 0, ib, 0, ib.Length);
                sha.TransformFinalBlock(ib, 0, ib.Length);
                return ToHex(sha.Hash);
            }
        }

        private static float[] ToFloats(Vector3[] v)
        {
            var f = new float[v.Length * 3];
            for (int i = 0; i < v.Length; i++) { f[i * 3] = v[i].x; f[i * 3 + 1] = v[i].y; f[i * 3 + 2] = v[i].z; }
            return f;
        }

        private static void AppendFloats(HashAlgorithm sha, Vector3 v)
        {
            var b = new byte[12];
            Buffer.BlockCopy(new[] { v.x, v.y, v.z }, 0, b, 0, 12);
            sha.TransformBlock(b, 0, 12, null, 0);
        }

        private static string ToHex(byte[] h)
        {
            var sb = new StringBuilder(h.Length * 2);
            for (int i = 0; i < h.Length; i++) sb.Append(h[i].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        private static string CombinedHash(List<BlockData> blocks, ImpostorData imp)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < blocks.Count; i++) sb.Append(blocks[i].name).Append('=').Append(blocks[i].hash).Append(';');
            if (imp != null) sb.Append("L2=").Append(imp.hash);
            using (var sha = SHA1.Create()) return ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString())));
        }

        // ============================================================= L2: импостор

        private static ImpostorData BakeImpostor(List<BlockData> blocks, Material l1)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var imp = new ImpostorData();

            bool first = true;
            var cityBounds = new Bounds();
            foreach (var b in blocks)
            {
                var bb = new Bounds(b.pivot, b.size);
                if (first) { cityBounds = bb; first = false; }
                else cityBounds.Encapsulate(bb);
            }
            imp.center = cityBounds.center;
            imp.size = cityBounds.size;

            // экстенты кадров по реальным вершинам (не по AABB): ширина общая, высота — на ряд
            int rows = RowElevations.Length;
            imp.halfH = new float[rows];
            float halfW = 0f;
            for (int r = 0; r < rows; r++)
            {
                for (int a = 0; a < AzimuthCount; a++)
                {
                    Vector3 v = ViewDir(a * 360f / AzimuthCount, RowElevations[r]);
                    var rot = Quaternion.LookRotation(-v, Vector3.up);
                    Vector3 right = rot * Vector3.right, up = rot * Vector3.up;
                    foreach (var b in blocks)
                    {
                        Vector3 off = b.pivot - imp.center;
                        var ps = b.positions;
                        for (int i = 0; i < ps.Length; i++)
                        {
                            Vector3 p = ps[i] + off;
                            float x = Mathf.Abs(Vector3.Dot(p, right));
                            float y = Mathf.Abs(Vector3.Dot(p, up));
                            if (x > halfW) halfW = x;
                            if (y > imp.halfH[r]) imp.halfH[r] = y;
                        }
                    }
                }
            }
            imp.halfW = halfW * 1.03f + 2f;
            for (int r = 0; r < rows; r++) imp.halfH[r] = imp.halfH[r] * 1.06f + 2f;

            // PBR импостора — среднее по площади L1-треугольников (город с 12+ км — единая поверхность).
            double areaSum = 0, metSum = 0, smoSum = 0;
            foreach (var b in blocks)
            {
                if (b.surface == null) continue;
                for (int t = 0; t + 2 < b.triangles.Length; t += 3)
                {
                    int i0 = b.triangles[t], i1 = b.triangles[t + 1], i2 = b.triangles[t + 2];
                    double area = Vector3.Cross(b.positions[i1] - b.positions[i0], b.positions[i2] - b.positions[i0]).magnitude * 0.5;
                    areaSum += area;
                    metSum += area * b.surface[i0].r / 255.0;
                    smoSum += area * b.surface[i0].g / 255.0;
                }
            }
            imp.metallic = areaSum > 0 ? (float)Math.Round(metSum / areaSum, 3) : 0f;
            imp.smoothness = areaSum > 0 ? (float)Math.Round(smoSum / areaSum, 3) : 0.3f;

            int ssW = FrameW * Supersample, ssH = FrameH * Supersample;
            imp.width = FrameW * AzimuthCount;
            imp.height = FrameH * rows;
            var albedo = new Color32[imp.width * imp.height];
            var night = new Color32[imp.width * imp.height];

            int passAlbedo = l1.FindPass("HlodBakeAlbedo");
            int passNight = l1.FindPass("HlodBakeNight");
            if (passAlbedo < 0 || passNight < 0) throw new InvalidOperationException("В шейдере L1 нет bake-проходов.");

            var rtA = new RenderTexture(new RenderTextureDescriptor(ssW, ssH, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            var rtN = new RenderTexture(new RenderTextureDescriptor(ssW, ssH, RenderTextureFormat.ARGB32, 24) { sRGB = false, msaaSamples = 1 });
            var readA = new Texture2D(ssW, ssH, TextureFormat.RGBA32, false, false);
            var readN = new Texture2D(ssW, ssH, TextureFormat.RGBA32, false, true);
            var prevActive = RenderTexture.active;
            float camDist = imp.size.magnitude + 200f;

            try
            {
                rtA.Create();
                rtN.Create();
                for (int r = 0; r < rows; r++)
                {
                    for (int a = 0; a < AzimuthCount; a++)
                    {
                        Vector3 v = ViewDir(a * 360f / AzimuthCount, RowElevations[r]);
                        var rot = Quaternion.LookRotation(-v, Vector3.up);
                        Vector3 camPos = v * camDist;
                        Matrix4x4 view = Matrix4x4.TRS(camPos, rot, new Vector3(1f, 1f, -1f)).inverse;
                        Matrix4x4 proj = Matrix4x4.Ortho(-imp.halfW, imp.halfW, -imp.halfH[r], imp.halfH[r], 1f, camDist * 2f);

                        RenderFrame(rtA, view, proj, blocks, imp.center, l1, passAlbedo);
                        RenderTexture.active = rtA;
                        readA.ReadPixels(new Rect(0, 0, ssW, ssH), 0, 0, false);
                        RenderFrame(rtN, view, proj, blocks, imp.center, l1, passNight);
                        RenderTexture.active = rtN;
                        readN.ReadPixels(new Rect(0, 0, ssW, ssH), 0, 0, false);

                        ComposeFrame(readA.GetPixels32(), readN.GetPixels32(), ssW, ssH, albedo, night, imp.width, a * FrameW, r * FrameH);
                    }
                }
            }
            finally
            {
                RenderTexture.active = prevActive;
                rtA.Release(); rtN.Release();
                Object.DestroyImmediate(rtA); Object.DestroyImmediate(rtN);
                Object.DestroyImmediate(readA); Object.DestroyImmediate(readN);
            }

            DilateColor(albedo, imp.width, imp.height, 4);
            imp.albedoMips = BuildMips(albedo, imp.width, imp.height, false);
            imp.nightMips = BuildMips(night, imp.width, imp.height, true);
            PreserveCoverage(imp.albedoMips, imp.width, imp.height);

            using (var sha = SHA1.Create())
            {
                var p = new byte[12 + 4 + rows * 4 + 8];
                Buffer.BlockCopy(new[] { imp.center.x, imp.center.y, imp.center.z, imp.halfW }, 0, p, 0, 16);
                Buffer.BlockCopy(imp.halfH, 0, p, 16, rows * 4);
                Buffer.BlockCopy(new[] { imp.metallic, imp.smoothness }, 0, p, 16 + rows * 4, 8);
                sha.TransformBlock(p, 0, p.Length, null, 0);
                foreach (var mip in imp.albedoMips) HashColors(sha, mip);
                foreach (var mip in imp.nightMips) HashColors(sha, mip);
                sha.TransformFinalBlock(new byte[0], 0, 0);
                imp.hash = ToHex(sha.Hash);
            }
            imp.ms = sw.Elapsed.TotalMilliseconds;
            Debug.Log("[PrimumHlodBaker] L2: центр " + imp.center.ToString("F1") + ", габарит " + imp.size.ToString("F0") +
                      ", halfW " + F(imp.halfW) + ", halfH " + string.Join("/", Array.ConvertAll(imp.halfH, F)) +
                      ", атлас " + imp.width + "x" + imp.height + ", " + imp.ms.ToString("F0", CultureInfo.InvariantCulture) + " мс");
            return imp;
        }

        private static void HashColors(HashAlgorithm sha, Color32[] c)
        {
            var b = new byte[c.Length * 4];
            for (int i = 0; i < c.Length; i++) { b[i * 4] = c[i].r; b[i * 4 + 1] = c[i].g; b[i * 4 + 2] = c[i].b; b[i * 4 + 3] = c[i].a; }
            sha.TransformBlock(b, 0, b.Length, null, 0);
        }

        private static void RenderFrame(RenderTexture rt, Matrix4x4 view, Matrix4x4 proj, List<BlockData> blocks, Vector3 center,
                                        Material mat, int pass)
        {
            var cmd = new CommandBuffer { name = "PrimumHlodBake" };
            try
            {
                cmd.SetRenderTarget(rt);
                cmd.ClearRenderTarget(true, true, new Color(0f, 0f, 0f, 0f));
                cmd.SetViewProjectionMatrices(view, proj);
                cmd.SetGlobalFloat("_HlodBakeNightScale", NightScale);
                foreach (var b in blocks)
                    cmd.DrawMesh(b.mesh, Matrix4x4.Translate(b.pivot - center), mat, 0, pass);
                Graphics.ExecuteCommandBuffer(cmd);
            }
            finally
            {
                cmd.Release();
            }
        }

        /// <summary>2×2 суперсэмпл → кадр атласа: альбедо взвешено покрытием, огни — максимум.</summary>
        private static void ComposeFrame(Color32[] a, Color32[] n, int ssW, int ssH, Color32[] albedo, Color32[] night,
                                         int atlasW, int x0, int y0)
        {
            int s = Supersample;
            for (int y = 0; y < FrameH; y++)
            {
                for (int x = 0; x < FrameW; x++)
                {
                    float sr = 0, sg = 0, sb = 0, sa = 0;
                    int nr = 0, ng = 0, nb = 0;
                    float ny = 0, nw = 0;
                    for (int dy = 0; dy < s; dy++)
                    {
                        for (int dx = 0; dx < s; dx++)
                        {
                            int i = (y * s + dy) * ssW + (x * s + dx);
                            var ca = a[i];
                            float w = ca.a / 255f;
                            sr += ca.r * w; sg += ca.g * w; sb += ca.b * w; sa += w;
                            var cn = n[i];
                            if (cn.r > nr) nr = cn.r;
                            if (cn.g > ng) ng = cn.g;
                            if (cn.b > nb) nb = cn.b;
                            ny += cn.a * w; nw += w;
                        }
                    }
                    int o = (y0 + y) * atlasW + (x0 + x);
                    float cov = sa / (s * s);
                    albedo[o] = sa > 0f
                        ? new Color32((byte)Mathf.RoundToInt(sr / sa), (byte)Mathf.RoundToInt(sg / sa), (byte)Mathf.RoundToInt(sb / sa),
                                      (byte)Mathf.RoundToInt(cov * 255f))
                        : new Color32(0, 0, 0, 0);
                    night[o] = new Color32((byte)nr, (byte)ng, (byte)nb, nw > 0f ? (byte)Mathf.RoundToInt(ny / nw) : (byte)128);
                }
            }
        }

        /// <summary>Цвет «растекается» в прозрачные пиксели (без тёмной каймы при фильтрации); альфа не меняется.</summary>
        private static void DilateColor(Color32[] px, int w, int h, int iterations)
        {
            var src = (Color32[])px.Clone();
            for (int it = 0; it < iterations; it++)
            {
                var dst = (Color32[])src.Clone();
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (src[i].a > 0 || (src[i].r | src[i].g | src[i].b) != 0) continue;
                        int r = 0, g = 0, b = 0, c = 0;
                        for (int k = 0; k < 4; k++)
                        {
                            int nx = x + (k == 0 ? -1 : k == 1 ? 1 : 0);
                            int ny = y + (k == 2 ? -1 : k == 3 ? 1 : 0);
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            var q = src[ny * w + nx];
                            if (q.a == 0 && (q.r | q.g | q.b) == 0) continue;
                            r += q.r; g += q.g; b += q.b; c++;
                        }
                        if (c > 0) dst[i] = new Color32((byte)(r / c), (byte)(g / c), (byte)(b / c), 0);
                    }
                }
                src = dst;
            }
            Array.Copy(src, px, px.Length);
        }

        private static Color32[][] BuildMips(Color32[] mip0, int w, int h, bool lights)
        {
            var mips = new List<Color32[]> { mip0 };
            int cw = w, ch = h;
            var cur = mip0;
            for (int m = 1; m < ImpostorMips && cw > 1 && ch > 1; m++)
            {
                int nw = Mathf.Max(1, cw / 2), nh = Mathf.Max(1, ch / 2);
                var next = new Color32[nw * nh];
                for (int y = 0; y < nh; y++)
                {
                    for (int x = 0; x < nw; x++)
                    {
                        float r = 0, g = 0, b = 0, a = 0, wsum = 0;
                        int mr = 0, mg = 0, mb = 0;
                        for (int k = 0; k < 4; k++)
                        {
                            var q = cur[(y * 2 + (k >> 1)) * cw + (x * 2 + (k & 1))];
                            float wq = lights ? 1f : (q.a / 255f) + 1e-4f;
                            r += q.r * wq; g += q.g * wq; b += q.b * wq; wsum += wq;
                            a += q.a;
                            if (q.r > mr) mr = q.r;
                            if (q.g > mg) mg = q.g;
                            if (q.b > mb) mb = q.b;
                        }
                        next[y * nw + x] = lights
                            ? new Color32((byte)mr, (byte)mg, (byte)mb, (byte)Mathf.RoundToInt(a / 4f))
                            : new Color32((byte)Mathf.RoundToInt(r / wsum), (byte)Mathf.RoundToInt(g / wsum), (byte)Mathf.RoundToInt(b / wsum),
                                          (byte)Mathf.RoundToInt(a / 4f));
                    }
                }
                mips.Add(next);
                cur = next;
                cw = nw; ch = nh;
            }
            return mips.ToArray();
        }

        /// <summary>Мипы альфа-теста сохраняют долю покрытия mip0 (иначе силуэт «тает» вдали).</summary>
        private static void PreserveCoverage(Color32[][] mips, int w, int h)
        {
            float target = Coverage(mips[0], 1f);
            for (int m = 1; m < mips.Length; m++)
            {
                float lo = 0.25f, hi = 8f;
                for (int it = 0; it < 20; it++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (Coverage(mips[m], mid) < target) lo = mid; else hi = mid;
                }
                float scale = (lo + hi) * 0.5f;
                var px = mips[m];
                for (int i = 0; i < px.Length; i++)
                {
                    var c = px[i];
                    c.a = (byte)Mathf.Clamp(Mathf.RoundToInt(c.a * scale), 0, 255);
                    px[i] = c;
                }
            }
        }

        private static float Coverage(Color32[] px, float scale)
        {
            int cut = Mathf.RoundToInt(AlphaCutoff * 255f), n = 0;
            for (int i = 0; i < px.Length; i++) if (px[i].a * scale >= cut) n++;
            return (float)n / px.Length;
        }

        // ============================================================ запись

        private static void WriteAssets(Mode mode, List<BlockData> blocks, ImpostorData imp, Material l1, Shader impShader)
        {
            EnsureFolder(OutDir);
            EnsureFolder(Path.GetDirectoryName(ManifestAssetPath).Replace('\\', '/'));

            var manifest = AssetDatabase.LoadAssetAtPath<PrimumHlodManifest>(ManifestAssetPath);
            if (manifest == null)
            {
                manifest = ScriptableObject.CreateInstance<PrimumHlodManifest>();
                AssetDatabase.CreateAsset(manifest, ManifestAssetPath);
            }

            if (mode == Mode.All) manifest.blocks.Clear();

            foreach (var b in blocks)
            {
                string path = OutDir + "/" + b.name + "_L1.asset";
                var asset = SaveOrReplace(b.mesh, path);
                b.mesh = asset;

                int idx = manifest.FindBlock(b.name);
                var entry = idx >= 0 ? manifest.blocks[idx] : new PrimumHlodManifest.Block();
                entry.name = b.name;
                entry.mesh = asset;
                entry.pivot = b.pivot;
                entry.size = b.size;
                entry.layer = b.layer;
                entry.sourceRenderers = b.sourceRenderers;
                entry.sourceTriangles = b.sourceTriangles;
                entry.smallRemovedTriangles = b.smallRemoved;
                entry.hiddenRemovedTriangles = b.hiddenRemoved;
                entry.triangles = b.triangles.Length / 3;
                entry.vertices = b.positions.Length;
                entry.hash = b.hash;
                if (idx < 0) manifest.blocks.Add(entry);
            }

            manifest.format = PrimumHlodManifest.CurrentFormat;
            manifest.l1Material = l1;
            manifest.bakeSettings = Settings;

            if (imp != null)
            {
                var albedoTex = MakeTexture(imp.albedoMips, imp.width, imp.height, false, "PRIMUM_L2_Albedo");
                var nightTex = MakeTexture(imp.nightMips, imp.width, imp.height, true, "PRIMUM_L2_Night");
                albedoTex = SaveOrReplace(albedoTex, L2AlbedoPath);
                nightTex = SaveOrReplace(nightTex, L2NightPath);

                var quad = new Mesh { name = "PRIMUM_L2_Impostor" };
                quad.SetVertices(new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0) });
                quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0, false);
                float maxH = 0f;
                for (int r = 0; r < imp.halfH.Length; r++) maxH = Mathf.Max(maxH, imp.halfH[r]);
                float radius = Mathf.Sqrt(imp.halfW * imp.halfW + maxH * maxH);
                quad.bounds = new Bounds(Vector3.zero, Vector3.one * radius * 2f);
                quad = SaveOrReplace(quad, L2MeshPath);

                var impMat = EnsureMaterial(L2MaterialPath, impShader);
                impMat.SetTexture("_AlbedoTex", albedoTex);
                impMat.SetTexture("_NightTex", nightTex);
                impMat.SetVector("_Grid", new Vector4(AzimuthCount, RowElevations.Length, 0, 0));
                impMat.SetVector("_RowElev", new Vector4(RowElevations[0], RowElevations.Length > 1 ? RowElevations[1] : 0,
                                                         RowElevations.Length > 2 ? RowElevations[2] : 0,
                                                         RowElevations.Length > 3 ? RowElevations[3] : 0));
                impMat.SetVector("_RowHalfH", new Vector4(imp.halfH[0], imp.halfH.Length > 1 ? imp.halfH[1] : 0,
                                                          imp.halfH.Length > 2 ? imp.halfH[2] : 0,
                                                          imp.halfH.Length > 3 ? imp.halfH[3] : 0));
                impMat.SetFloat("_HalfW", imp.halfW);
                impMat.SetFloat("_Metallic", imp.metallic);
                impMat.SetFloat("_Smoothness", imp.smoothness);
                impMat.SetFloat("_NightScale", NightScale);
                impMat.SetFloat("_Cutoff", AlphaCutoff);
                EditorUtility.SetDirty(impMat);

                manifest.impostorValid = true;
                manifest.impostorMesh = quad;
                manifest.impostorMaterial = impMat;
                manifest.cityCenter = imp.center;
                manifest.citySize = imp.size;
                manifest.impostorHash = imp.hash;
            }
            else if (mode == Mode.All)
            {
                manifest.impostorValid = false;
            }

            manifest.bakeHash = CombinedHash(blocks, imp);
            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssets();
        }

        private static Texture2D MakeTexture(Color32[][] mips, int w, int h, bool linear, string texName)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mips.Length, linear) { name = texName };
            for (int m = 0; m < mips.Length; m++) tex.SetPixels32(mips[m], m);
            tex.Apply(false, false);
            EditorUtility.CompressTexture(tex, TextureFormat.DXT5, TextureCompressionQuality.Normal);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 1;
            return tex;
        }

        /// <summary>Пишет ассет, сохраняя GUID существующего (повторный bake не меняет ссылки).</summary>
        private static T SaveOrReplace<T>(T obj, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(obj, path);
                return obj;
            }
            EditorUtility.CopySerialized(obj, existing);
            existing.name = Path.GetFileNameWithoutExtension(path);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(obj);
            return existing;
        }

        private static Material EnsureMaterial(string path, Shader shader)
        {
            EnsureFolder(OutDir);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path), enableInstancing = false };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // =========================================================== превью

        /// <summary>Атлас L2 поверх серого фона — PNG для визуальной проверки силуэта (в git не идёт).</summary>
        private static void WritePreview(ImpostorData imp, string path)
        {
            int w = imp.width, h = imp.height;
            var tex = new Texture2D(w, h * 2, TextureFormat.RGBA32, false, false);
            var px = new Color32[w * h * 2];
            var a = imp.albedoMips[0];
            var n = imp.nightMips[0];
            for (int i = 0; i < w * h; i++)
            {
                float k = a[i].a >= AlphaCutoff * 255f ? 1f : 0f;
                px[i + w * h] = new Color32((byte)(a[i].r * k + 90 * (1 - k)), (byte)(a[i].g * k + 110 * (1 - k)), (byte)(a[i].b * k + 140 * (1 - k)), 255);
                byte lr = (byte)Mathf.Min(255, n[i].r * 3), lg = (byte)Mathf.Min(255, n[i].g * 3), lb = (byte)Mathf.Min(255, n[i].b * 3);
                px[i] = k > 0f ? new Color32((byte)Mathf.Max(lr, (byte)(a[i].r / 6)), (byte)Mathf.Max(lg, (byte)(a[i].g / 6)), (byte)Mathf.Max(lb, (byte)(a[i].b / 6)), 255)
                               : new Color32(8, 10, 20, 255);
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            try { File.WriteAllBytes(path, tex.EncodeToPNG()); }
            catch (Exception e) { Debug.LogWarning("[PrimumHlodBaker] Превью: " + e.Message); }
            Object.DestroyImmediate(tex);
        }

        /// <summary>
        /// Тот же блок БЕЗ фильтров (все треугольники, те же цвета) против L1 с фильтрами —
        /// 8 азимутов × 2 высоты, ортокамера (м/px — в CSV; жёстче, чем 3 км в игре ≈ 2.9 м/px).
        /// Разница покрытия и цвета = видимая потеря от удаления интерьеров и мелочи.
        /// </summary>
        private static void WriteBlockComparison(Transform block, Transform worldRoot, BlockData filtered, Material mat,
                                                 string pngPath, StringBuilder csv, string run)
        {
            if (block == null) return;
            var full = BakeBlockUnfiltered(block, worldRoot, filtered.pivot);
            var fullMesh = BuildMesh(full, "pilot_full");
            var list = new List<BlockData> { full };
            var listF = new List<BlockData> { filtered };
            int pass = mat.FindPass("HlodBakeAlbedo");
            const int res = 384;
            float[] els = { 10f, 35f };
            var rt = new RenderTexture(new RenderTextureDescriptor(res, res, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            var read = new Texture2D(res, res, TextureFormat.RGBA32, false, false);
            var sheet = new Color32[res * 8 * res * 4];
            int coverDiff = 0, colorDiff = 0, covered = 0;
            var prev = RenderTexture.active;
            float halfExt = filtered.size.magnitude * 0.5f + 2f;
            try
            {
                rt.Create();
                full.mesh = fullMesh;
                for (int e = 0; e < els.Length; e++)
                {
                    for (int a = 0; a < 8; a++)
                    {
                        Vector3 v = ViewDir(a * 45f + 22.5f, els[e]);
                        var rot = Quaternion.LookRotation(-v, Vector3.up);
                        Matrix4x4 view = Matrix4x4.TRS(v * (halfExt * 3f), rot, new Vector3(1, 1, -1)).inverse;
                        Matrix4x4 proj = Matrix4x4.Ortho(-halfExt, halfExt, -halfExt, halfExt, 1f, halfExt * 8f);

                        RenderFrame(rt, view, proj, list, filtered.pivot, mat, pass);
                        RenderTexture.active = rt;
                        read.ReadPixels(new Rect(0, 0, res, res), 0, 0, false);
                        var pf = read.GetPixels32();
                        RenderFrame(rt, view, proj, listF, filtered.pivot, mat, pass);
                        RenderTexture.active = rt;
                        read.ReadPixels(new Rect(0, 0, res, res), 0, 0, false);
                        var pl = read.GetPixels32();

                        for (int i = 0; i < pf.Length; i++)
                        {
                            bool cf = pf[i].a > 127, cl = pl[i].a > 127;
                            if (cf) covered++;
                            if (cf != cl) coverDiff++;
                            else if (cf && (Mathf.Abs(pf[i].r - pl[i].r) + Mathf.Abs(pf[i].g - pl[i].g) + Mathf.Abs(pf[i].b - pl[i].b)) > 60) colorDiff++;
                            int x = i % res, y = i / res;
                            int row = e * 2;
                            sheet[(row * res + y) * res * 8 + a * res + x] = cf ? pf[i] : new Color32(90, 110, 140, 255);
                            sheet[((row + 1) * res + y) * res * 8 + a * res + x] = cl ? pl[i] : new Color32(90, 110, 140, 255);
                        }
                    }
                }
            }
            finally
            {
                RenderTexture.active = prev;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(read);
                Object.DestroyImmediate(fullMesh);
            }

            if (!string.IsNullOrEmpty(pngPath))
            {
                var tex = new Texture2D(res * 8, res * 4, TextureFormat.RGBA32, false, false);
                tex.SetPixels32(sheet);
                tex.Apply(false, false);
                try { File.WriteAllBytes(pngPath, tex.EncodeToPNG()); }
                catch (Exception ex) { Debug.LogWarning("[PrimumHlodBaker] Превью блока: " + ex.Message); }
                Object.DestroyImmediate(tex);
            }

            float cov = covered > 0 ? 100f * coverDiff / covered : 0f;
            float col = covered > 0 ? 100f * colorDiff / covered : 0f;
            csv.Append("#").Append(run).Append(",compare=").Append(filtered.name).Append(",fullTris=").Append(full.triangles.Length / 3)
               .Append(",l1Tris=").Append(filtered.triangles.Length / 3).Append(",coveredPx=").Append(covered)
               .Append(",coverageDiffPct=").Append(F(cov)).Append(",colorDiffPct=").Append(F(col)).Append(",views=16,res=").Append(res)
               .Append(",mPerPx=").Append(F(halfExt * 2f / res)).AppendLine();
            Debug.Log("[PrimumHlodBaker] Сравнение " + filtered.name + ": полный " + full.triangles.Length / 3 + " тр. vs L1 " +
                      filtered.triangles.Length / 3 + " тр.; расхождение покрытия " + F(cov) + " %, цвета " + F(col) +
                      " % (16 видов, " + F(halfExt * 2f / res) + " м/px)" + (pngPath != null ? ". PNG: " + pngPath : ""));
        }

        private static BlockData BakeBlockUnfiltered(Transform block, Transform worldRoot, Vector3 pivot)
        {
            var renderers = CollectRenderers(block);
            var P = new List<Vector3>(); var N = new List<Vector3>(); var C = new List<Color32>(); var T = new List<int>();
            var S = new List<Color32>();
            Matrix4x4 toRoot = worldRoot.worldToLocalMatrix;
            var tmp = new Mesh { indexFormat = IndexFormat.UInt32 };
            var single = new CombineInstance[1];
            try
            {
                foreach (var r in renderers)
                {
                    var src = r.GetComponent<MeshFilter>().sharedMesh;
                    var mats = r.sharedMaterials;
                    Matrix4x4 m = toRoot * r.localToWorldMatrix;
                    Matrix4x4 nm = m.inverse.transpose;
                    bool mirrored = m.determinant < 0f;
                    for (int s = 0; s < Mathf.Min(src.subMeshCount, mats.Length); s++)
                    {
                        if (mats[s] == null) continue;
                        Color32 color; byte cls; Color32 surf;
                        Classify(mats[s], out color, out cls, out surf);
                        tmp.Clear();
                        single[0] = new CombineInstance { mesh = src, subMeshIndex = s, transform = Matrix4x4.identity };
                        tmp.CombineMeshes(single, true, false, false);
                        var v = tmp.vertices; var n = tmp.normals;
                        if (n == null || n.Length != v.Length) { tmp.RecalculateNormals(); n = tmp.normals; }
                        var idx = tmp.GetTriangles(0);
                        var remap = new int[v.Length];
                        for (int i = 0; i < remap.Length; i++) remap[i] = -1;
                        for (int t = 0; t + 2 < idx.Length; t += 3)
                        {
                            int a = idx[t], b = idx[t + 1], c = idx[t + 2];
                            if (mirrored) { int x = b; b = c; c = x; }
                            T.Add(Remap(a, remap, v, n, m, nm, color, cls, surf, P, N, C, S));
                            T.Add(Remap(b, remap, v, n, m, nm, color, cls, surf, P, N, C, S));
                            T.Add(Remap(c, remap, v, n, m, nm, color, cls, surf, P, N, C, S));
                        }
                    }
                }
            }
            finally { Object.DestroyImmediate(tmp); }

            var data = new BlockData { name = block.name + "_full", pivot = pivot };
            var pos = P.ToArray();
            for (int i = 0; i < pos.Length; i++) pos[i] -= pivot;
            data.positions = pos;
            data.normals = N.ToArray();
            data.colors = C.ToArray();
            data.surface = S.ToArray();
            data.triangles = T.ToArray();
            var b2 = new Bounds(pos.Length > 0 ? pos[0] : Vector3.zero, Vector3.zero);
            for (int i = 1; i < pos.Length; i++) b2.Encapsulate(pos[i]);
            data.size = b2.size * 2f + Vector3.one;
            return data;
        }

        // =========================================================== утилиты

        private static Transform FindWorldRoot()
        {
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var go in scene.GetRootGameObjects())
                    if (go != null && go.name == WorldRootName) return go.transform;
            }
            return null;
        }

        private static string F(float v)
        {
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Страховка от «залипших» runtime-объектов HLOD: при возврате в Edit Mode удаляет корень
    /// PRIMUM_HLOD под WorldRoot_0_0, если директор не успел (объекты и так HideFlags.DontSave).
    /// </summary>
    [InitializeOnLoad]
    internal static class PrimumHlodEditModeCleanup
    {
        static PrimumHlodEditModeCleanup()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (go == null || go.name != "WorldRoot_0_0") continue;
                    var t = go.transform.Find(VisibilityDirector.HlodRootName);
                    if (t == null) continue;
                    Debug.Log("[PrimumHlodBaker] Удалён оставшийся runtime-корень " + VisibilityDirector.HlodRootName + " (Edit Mode).");
                    Object.DestroyImmediate(t.gameObject);
                }
            }
        }
    }
}
