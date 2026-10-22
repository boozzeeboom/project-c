using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// PRIMUM BASE: компактные коллайдеры низа города, построенные из РЕАЛЬНОЙ геометрии
    /// поверхности, а не из упрощённых контуров <c>platforms.obj</c>.
    ///
    /// Почему: старые 9 <c>MeshCollider</c>'ов (DECK0/6/12/18 + PLATL00…24) повторяли контур платформ
    /// грубо — например DECK12 перекрывал на 4 760 м² больше, чем реальная поверхность (заваривал
    /// колодцы маршей и края), из-за чего на деках стояла «лишняя» земля.
    ///
    /// Что делает:
    ///  1) <b>плиты</b> — берёт ВЕРХНИЕ горизонтальные грани мешей <c>..._M_M_DECK_STEEL</c> и
    ///     <c>..._M_M_ROCK_DARK</c> (реальная проезжая/террасная поверхность), растеризует их по
    ///     уровням, вырезает пятна маршей и склеивает в максимальные прямоугольники → <c>BoxCollider</c>
    ///     (толщина плиты 1.3 м, как у слэба в модели);
    ///  2) <b>марши</b> — берёт точную геометрию лестниц (<c>stairs.obj</c> → <c>PM_STAIRS_*</c>),
    ///     разбивает на связные острова (1 остров = 1 марш) и подгоняет ОДИН наклонный бокс на марш
    ///     (плоскость по верхним граням ступеней) вместо 6 256 треугольников меша;
    ///  3) <b>поверхность</b> — мощёные тропинки и аллеи (<c>..._M_M_PAVING_BRUSCHATKA</c>) и гранит
    ///     (<c>..._M_M_GRANITE</c>) получают точный <c>MeshCollider</c>: в плиты они не попадают (плиты
    ///     строятся по Deck Steel / Rock Dark), из-за чего все улочки и аллея от арки до админ-здания
    ///     оставались без пола.
    ///
    /// Куда: <c>WorldRoot_0_0/PRIMUM_COLLIDERS/&lt;чанк&gt;/DECK</c> (боксы плит) и
    /// <c>.../STAIR/&lt;марш&gt;</c> (по одному боксу). Трансформ корня коллайдеров = трансформ PRIMUM,
    /// поэтому ребейз мира везёт физику вместе с городом. <c>static</c>-флаги НЕ ставим (Floating Origin).
    /// Идемпотентно: прежние группы чанка сносятся перед сборкой.
    /// Меню: ProjectC → World → PRIMUM → Коллайдеры BASE.
    /// </summary>
    public static class PrimumBaseDeckColliders
    {
        private const string CityRootName = "PRIMUM";
        private const string ChunkName = "PRIMUM_BASE_S0_CITY_P1_00H";
        private const string CollidersRootName = "PRIMUM_COLLIDERS";
        private const string WorldRootFallback = "WorldRoot_0_0";
        private const string StairsMeshPath = "Assets/_Project/Prefabs/City/Colliders/PM_STAIRS_PRIMUM_BASE_S0_CITY_P1_00H.asset";
        private const string SurfaceAssetFolder = "Assets/_Project/Prefabs/City/Colliders";

        private const string DeckMeshSuffix = "_M_M_DECK_STEEL";
        private const string RockMeshSuffix = "_M_M_ROCK_DARK";
        private const string PavingMeshSuffix = "_M_M_PAVING_BRUSCHATKA";
        private const string GraniteMeshSuffix = "_M_M_GRANITE";

        private const string DeckGroupName = "DECK";
        private const string StairGroupName = "STAIR";
        private const string LandingGroupName = "LANDING";
        private const string SurfaceGroupName = "SURFACE";

        private const int CityLayer = 8;            // PRIMUM
        private const float Cell = 1.0f;            // шаг растеризации плит, м
        private const float MinRectArea = 4.0f;     // отсев огрызков, м²
        private const float PlateThickness = 1.3f;  // толщина слэба в модели
        private const float StairThickness = 0.8f;  // толщина бокса-марша
        private const float StairEdgePad = 0.6f;    // запас марша в стороны и к низу (перекрывает щели растера)
        private const float LandingThickness = 1.3f;  // толщина бокса площадки марша
        private const float LandingProbeDepth = 3.2f; // как далеко от торца марша ищем ровный пол площадки
        private const float LandingLevelTol = 0.45f;  // допуск «пол на уровне торца марша»
        private const float StairCarvePad = 0.10f;  // запас выреза под марш
        // Вырез пятен маршей: в геометрии BASE колодцы уже есть, поэтому по умолчанию НЕ режем —
        // иначе плиты дробятся на лишние боксы. Включать только если проверка лучей нашла перекрытие марша.
        private const bool CarveStairSpans = false;
        private const float TopFaceMinNormalY = 0.999f;
        private const float StairPlaneMinNormalY = 0.90f;

        // подписи прежних групп низа — их сносим перед сборкой
        private static readonly string[] LegacyPrefixes = { "DECK", "PLATL", "PLATFORM", "STAIRS", "STAIR", "LANDING", "SURFACE" };

        // ------------------------------------------------------------------ меню

        [MenuItem("ProjectC/World/PRIMUM/Коллайдеры BASE: плиты+марши (пересобрать)", false, 350)]
        public static void MenuBuild()
        {
            Report r = Build(true);
            Debug.Log(Describe(r));
        }

        [MenuItem("ProjectC/World/PRIMUM/Коллайдеры BASE: отчёт (без изменений)", false, 351)]
        public static void MenuReport()
        {
            Debug.Log(Describe(Build(false)));
        }

        [MenuItem("ProjectC/World/PRIMUM/Коллайдеры BASE: проверить покрытие (лучи)", false, 352)]
        public static void MenuVerify()
        {
            Debug.Log(VerifyCoverage());
        }

        // ------------------------------------------------------------------ данные

        public class Report
        {
            public int PlateBoxes;
            public int StairBoxes;
            public int LandingBoxes;
            public int SurfaceColliders;
            public int SurfaceTriangles;
            public int CarvedCells;
            public readonly List<string> Levels = new List<string>();
            public readonly List<string> Warnings = new List<string>();
        }

        private struct RectBox
        {
            public float Top;          // Y верхней грани
            public float X0, X1, Z0, Z1;
            public float Area { get { return (X1 - X0) * (Z1 - Z0); } }
        }

        private struct StairBox
        {
            public Vector3 Center;     // локально в пространстве корня (PRIMUM)
            public Vector3 Size;
            public Quaternion Rotation;
        }

        /// <summary>Треугольник в пространстве корня (PRIMUM), только нужные поля.</summary>
        private struct Tri
        {
            public Vector3 A, B, C;
        }

        // ------------------------------------------------------------------ сборка

        public static Report Build(bool apply)
        {
            var rep = new Report();

            GameObject city = GameObject.Find(CityRootName);
            if (city == null) { rep.Warnings.Add("Не найден корень " + CityRootName); return rep; }

            Transform chunk = city.transform.Find(ChunkName);
            if (chunk == null) { rep.Warnings.Add("Не найден чанк " + ChunkName + " в " + CityRootName); return rep; }

            Matrix4x4 toRoot = city.transform.worldToLocalMatrix;

            // --- источники плит
            List<Tri> plateTris = CollectTopTris(chunk, toRoot, rep);
            if (plateTris.Count == 0) { rep.Warnings.Add("Не найдено верхних граней у " + DeckMeshSuffix + " / " + RockMeshSuffix); return rep; }

            // --- марши (1 остров = 1 марш)
            List<StairBox> stairs = BuildStairs(rep);
            rep.StairBoxes = stairs.Count;

            // --- плиты + вырез пятен маршей
            List<RectBox> plates = RasterizePlates(plateTris, stairs, ref rep);
            rep.PlateBoxes = plates.Count;

            // --- площадки маршей: пол на промежуточных уровнях (мощение, не deck steel) → плит там нет
            List<Tri> surfaceTris = CollectSurfaceTopTris(chunk, toRoot);
            List<RectBox> landings = BuildLandings(stairs, surfaceTris, plates);
            rep.LandingBoxes = landings.Count;
            for (int i = 0; i < landings.Count; i++)
                rep.Levels.Add("площадка " + i.ToString("00") + ": y=" + landings[i].Top.ToString("F2")
                               + "  x[" + landings[i].X0.ToString("F1") + ".." + landings[i].X1.ToString("F1") + "]"
                               + " z[" + landings[i].Z0.ToString("F1") + ".." + landings[i].Z1.ToString("F1") + "]");

            // --- ходовая поверхность: мощение и гранит. Плиты строятся только по Deck Steel / Rock Dark,
            //     поэтому мощёные тропинки и аллеи (в т.ч. лестницы улочек) оставались без пола — длинные
            //     вырезы по всем улочкам. Точный MeshCollider по самой поверхности закрывает их без растра.
            List<Transform> surfaceSources = CollectSurfaceSources(chunk, rep);
            rep.SurfaceColliders = surfaceSources.Count;
            foreach (Transform src in surfaceSources)
            {
                Mesh sm = src.GetComponent<MeshFilter>().sharedMesh;
                if (sm != null) rep.SurfaceTriangles += sm.triangles.Length / 3;
            }

            if (!apply) return rep;

            // --- куда кладём
            Transform worldRoot = city.transform.parent != null ? city.transform.parent : GameObject.Find(WorldRootFallback)?.transform;
            if (worldRoot == null) { rep.Warnings.Add("Не найден world root"); return rep; }

            GameObject rootGo = GameObject.Find(CollidersRootName);
            if (rootGo == null) rootGo = new GameObject(CollidersRootName);
            rootGo.transform.SetParent(worldRoot, false);
            rootGo.transform.position = city.transform.position;
            rootGo.transform.rotation = city.transform.rotation;
            rootGo.transform.localScale = Vector3.one;
            rootGo.layer = CityLayer;
            GameObjectUtility.SetStaticEditorFlags(rootGo, (StaticEditorFlags)0);

            // --- чанк-узел: снести прежние группы (идемпотентно)
            Transform chunkNode = rootGo.transform.Find(ChunkName);
            if (chunkNode != null) chunkNode = EnsureChunkNodeCleared(rootGo.transform, chunkNode);
            else chunkNode = EnsureChunkNodeCleared(rootGo.transform, null);

            // --- DECK: одна группа, N боксов
            GameObject deckGo = new GameObject(DeckGroupName);
            deckGo.transform.SetParent(chunkNode, false);
            deckGo.layer = CityLayer;
            GameObjectUtility.SetStaticEditorFlags(deckGo, (StaticEditorFlags)0);

            foreach (RectBox b in plates)
            {
                var bc = deckGo.AddComponent<BoxCollider>();
                bc.center = new Vector3((b.X0 + b.X1) * 0.5f, b.Top - PlateThickness * 0.5f, (b.Z0 + b.Z1) * 0.5f);
                bc.size = new Vector3(b.X1 - b.X0, PlateThickness, b.Z1 - b.Z0);
            }

            // --- STAIR: по одному наклонному боксу на марш
            GameObject stairGo = new GameObject(StairGroupName);
            stairGo.transform.SetParent(chunkNode, false);
            stairGo.layer = CityLayer;
            GameObjectUtility.SetStaticEditorFlags(stairGo, (StaticEditorFlags)0);

            for (int i = 0; i < stairs.Count; i++)
            {
                StairBox s = stairs[i];
                var go = new GameObject("STAIR_" + i.ToString("00"));
                go.transform.SetParent(stairGo.transform, false);
                go.transform.localPosition = s.Center;
                go.transform.localRotation = s.Rotation;
                go.transform.localScale = Vector3.one;
                go.layer = CityLayer;
                GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)0);
                var bc = go.AddComponent<BoxCollider>();
                bc.center = Vector3.zero;
                bc.size = s.Size;
            }

            // --- LANDING: площадки маршей
            GameObject landingGo = new GameObject(LandingGroupName);
            landingGo.transform.SetParent(chunkNode, false);
            landingGo.layer = CityLayer;
            GameObjectUtility.SetStaticEditorFlags(landingGo, (StaticEditorFlags)0);

            foreach (RectBox b in landings)
            {
                var bc = landingGo.AddComponent<BoxCollider>();
                bc.center = new Vector3((b.X0 + b.X1) * 0.5f, b.Top - LandingThickness * 0.5f, (b.Z0 + b.Z1) * 0.5f);
                bc.size = new Vector3(b.X1 - b.X0, LandingThickness, b.Z1 - b.Z0);
            }

            // --- SURFACE: точные коллайдеры ходовой поверхности (мощение + гранит).
            //     Меши FBX нечитаемы (Read/Write off), поэтому печём копию в root-пространстве
            //     в отдельный ассет — как уже сделано для PM_STAIRS.
            GameObject surfGo = new GameObject(SurfaceGroupName);
            surfGo.transform.SetParent(chunkNode, false);
            surfGo.layer = CityLayer;
            GameObjectUtility.SetStaticEditorFlags(surfGo, (StaticEditorFlags)0);

            foreach (Transform src in surfaceSources)
            {
                Mesh mesh = BakeSurfaceMesh(src, toRoot, rep);
                if (mesh == null) continue;

                var go = new GameObject("SURFACE_" + ShortSurfaceName(src.name));
                go.transform.SetParent(surfGo.transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                go.layer = CityLayer;
                GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)0);
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = false;
            }

            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(city.scene);
            return rep;
        }

        private static Transform EnsureChunkNodeCleared(Transform collidersRoot, Transform existing)
        {
            Transform node = existing;
            if (node == null)
            {
                var go = new GameObject(ChunkName);
                go.transform.SetParent(collidersRoot, false);
                go.layer = CityLayer;
                GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)0);
                return go.transform;
            }

            for (int i = node.childCount - 1; i >= 0; i--)
            {
                Transform c = node.GetChild(i);
                bool legacy = false;
                foreach (string p in LegacyPrefixes)
                    if (c.name.StartsWith(p, StringComparison.OrdinalIgnoreCase)) { legacy = true; break; }
                if (legacy) UnityEngine.Object.DestroyImmediate(c.gameObject);
            }
            return node;
        }

        // ------------------------------------------------------------------ плиты

        /// <summary>Верхние горизонтальные грани мешей поверхности низа (deck steel + rock dark) в пространстве корня.</summary>
        private static List<Tri> CollectTopTris(Transform chunk, Matrix4x4 rootFromWorld, Report rep)
        {
            var result = new List<Tri>();
            int sources = 0;

            foreach (Transform t in chunk.GetComponentsInChildren<Transform>(true))
            {
                bool isDeck = t.name.EndsWith(DeckMeshSuffix, StringComparison.Ordinal);
                bool isRock = t.name.EndsWith(RockMeshSuffix, StringComparison.Ordinal);
                if (!isDeck && !isRock) continue;

                var mf = t.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                sources++;
                Matrix4x4 m = rootFromWorld * t.localToWorldMatrix;
                Vector3[] v = mf.sharedMesh.vertices;
                int[] tr = mf.sharedMesh.triangles;

                for (int i = 0; i + 2 < tr.Length; i += 3)
                {
                    Vector3 a = m.MultiplyPoint3x4(v[tr[i]]);
                    Vector3 b = m.MultiplyPoint3x4(v[tr[i + 1]]);
                    Vector3 c = m.MultiplyPoint3x4(v[tr[i + 2]]);
                    Vector3 cross = Vector3.Cross(b - a, c - a);
                    float len = cross.magnitude;
                    if (len < 1e-6f) continue;
                    if (cross.y / len < TopFaceMinNormalY) continue;   // только верх, низ слэба не нужен
                    result.Add(new Tri { A = a, B = b, C = c });
                }
            }

            if (sources == 0)
                rep.Warnings.Add("Не найдено ни одного меша " + DeckMeshSuffix + " / " + RockMeshSuffix + " в чанке");
            return result;
        }

        /// <summary>Меши реальной ходовой поверхности (мощение + гранит) — по ним ставятся точные MeshCollider'ы.</summary>
        private static List<Transform> CollectSurfaceSources(Transform chunk, Report rep)
        {
            var result = new List<Transform>();
            foreach (Transform t in chunk.GetComponentsInChildren<Transform>(true))
            {
                bool isPaving = t.name.EndsWith(PavingMeshSuffix, StringComparison.Ordinal);
                bool isGranite = t.name.EndsWith(GraniteMeshSuffix, StringComparison.Ordinal);
                if (!isPaving && !isGranite) continue;

                var mf = t.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                result.Add(t);
            }

            if (result.Count == 0)
                rep.Warnings.Add("Не найдено мешей поверхности " + PavingMeshSuffix + " / " + GraniteMeshSuffix);
            return result;
        }

        private static string ShortSurfaceName(string goName)
        {
            string prefix = ChunkName + "_M_M_";
            return goName.StartsWith(prefix, StringComparison.Ordinal) ? goName.Substring(prefix.Length) : goName;
        }

        /// <summary>Копия поверхности в пространстве корня — ассет для MeshCollider (исходные FBX-меши нечитаемы).</summary>
        private static Mesh BakeSurfaceMesh(Transform src, Matrix4x4 toRoot, Report rep)
        {
            var mf = src.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return null;

            Mesh source = mf.sharedMesh;
            Matrix4x4 m = toRoot * src.localToWorldMatrix;

            Vector3[] sv = source.vertices;
            var v = new Vector3[sv.Length];
            for (int i = 0; i < sv.Length; i++) v[i] = m.MultiplyPoint3x4(sv[i]);

            int[] tris = source.triangles;
            if (m.determinant < 0f)
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    int tmp = tris[i + 1];
                    tris[i + 1] = tris[i + 2];
                    tris[i + 2] = tmp;
                }

            if (v.Length == 0 || tris.Length == 0) return null;

            string assetPath = SurfaceAssetFolder + "/PM_SURFACE_" + ShortSurfaceName(src.name) + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            bool created = mesh == null;
            if (created) mesh = new Mesh();
            else mesh.Clear();

            mesh.name = "PM_SURFACE_" + ShortSurfaceName(src.name);
            if (v.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = v;
            mesh.triangles = tris;
            mesh.RecalculateBounds();

            if (created) AssetDatabase.CreateAsset(mesh, assetPath);
            else EditorUtility.SetDirty(mesh);

            return mesh;
        }

        private static List<RectBox> RasterizePlates(List<Tri> tris, List<StairBox> stairs, ref Report rep)
        {
            // уровни: квантуем Y по 0.1 м
            var byLevel = new Dictionary<int, List<Tri>>();
            foreach (Tri t in tris)
            {
                int k = Mathf.RoundToInt((t.A.y + t.B.y + t.C.y) / 3f * 10f);
                List<Tri> list;
                if (!byLevel.TryGetValue(k, out list)) { list = new List<Tri>(); byLevel[k] = list; }
                list.Add(t);
            }

            var result = new List<RectBox>();
            var levelKeys = new List<int>(byLevel.Keys);
            levelKeys.Sort();

            foreach (int key in levelKeys)
            {
                float top = key / 10f;
                List<Tri> list = byLevel[key];

                float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
                foreach (Tri t in list)
                {
                    MinMax(t.A, ref x0, ref x1, ref z0, ref z1);
                    MinMax(t.B, ref x0, ref x1, ref z0, ref z1);
                    MinMax(t.C, ref x0, ref x1, ref z0, ref z1);
                }

                int nx = Mathf.CeilToInt((x1 - x0) / Cell);
                int nz = Mathf.CeilToInt((z1 - z0) / Cell);
                if (nx <= 0 || nz <= 0) continue;

                var occ = new bool[(long)nx * nz];
                foreach (Tri t in list) RasterTriangle(t, x0, z0, nx, nz, occ);

                // вырез только реальных перекрытий: ячейка плиты срезается, если она выше поверхности марша
                // (колодцы уже есть в геометрии, поэтому режется ровно то, что мешало бы пройти)
                if (CarveStairSpans)
                foreach (StairBox s in stairs)
                {
                    float stairLow = StairLowestY(s);
                    if (top <= stairLow + 0.30f) continue;

                    float sx0, sx1, sz0, sz1;
                    StairFootprint(s, out sx0, out sx1, out sz0, out sz1);

                    int ix0 = Mathf.Clamp(Mathf.FloorToInt((sx0 - StairCarvePad - x0) / Cell), 0, nx - 1);
                    int ix1 = Mathf.Clamp(Mathf.CeilToInt((sx1 + StairCarvePad - x0) / Cell), 0, nx);
                    int iz0 = Mathf.Clamp(Mathf.FloorToInt((sz0 - StairCarvePad - z0) / Cell), 0, nz - 1);
                    int iz1 = Mathf.Clamp(Mathf.CeilToInt((sz1 + StairCarvePad - z0) / Cell), 0, nz);
                    for (int iz = iz0; iz < iz1; iz++)
                        for (int ix = ix0; ix < ix1; ix++)
                        {
                            long idx = (long)iz * nx + ix;
                            if (!occ[idx]) continue;
                            float px = x0 + (ix + 0.5f) * Cell;
                            float pz = z0 + (iz + 0.5f) * Cell;
                            if (top < StairHeightAt(s, px, pz) - 0.15f) continue;   // марш выше плиты — не мешает
                            occ[idx] = false;
                            rep.CarvedCells++;
                        }
                }

                List<RectBox> rects = MergeRects(occ, nx, nz, x0, z0, top);
                int kept = 0;
                foreach (RectBox r in rects)
                {
                    if (r.Area < MinRectArea) continue;
                    result.Add(r);
                    kept++;
                }
                rep.Levels.Add(string.Format("y={0,7:F2}: {1,3} боксов", top, kept));
            }
            return result;
        }

        private static void MinMax(Vector3 p, ref float x0, ref float x1, ref float z0, ref float z1)
        {
            if (p.x < x0) x0 = p.x;
            if (p.x > x1) x1 = p.x;
            if (p.z < z0) z0 = p.z;
            if (p.z > z1) z1 = p.z;
        }

        private static void RasterTriangle(Tri t, float x0, float z0, int nx, int nz, bool[] occ)
        {
            Vector3 A = t.A, B = t.B, C = t.C;
            float det = (B.z - C.z) * (A.x - C.x) + (C.x - B.x) * (A.z - C.z);
            if (Mathf.Abs(det) < 1e-9f) return;

            int ix0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(A.x, Mathf.Min(B.x, C.x)) - x0) / Cell), 0, nx - 1);
            int ix1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(A.x, Mathf.Max(B.x, C.x)) - x0) / Cell), 0, nx);
            int iz0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(A.z, Mathf.Min(B.z, C.z)) - z0) / Cell), 0, nz - 1);
            int iz1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(A.z, Mathf.Max(B.z, C.z)) - z0) / Cell), 0, nz);

            for (int iz = iz0; iz < iz1; iz++)
            {
                float pz = z0 + (iz + 0.5f) * Cell;
                for (int ix = ix0; ix < ix1; ix++)
                {
                    float px = x0 + (ix + 0.5f) * Cell;
                    float l1 = ((B.z - C.z) * (px - C.x) + (C.x - B.x) * (pz - C.z)) / det;
                    float l2 = ((C.z - A.z) * (px - C.x) + (A.x - C.x) * (pz - C.z)) / det;
                    float l3 = 1f - l1 - l2;
                    if (l1 < 0f || l2 < 0f || l3 < 0f) continue;
                    occ[(long)iz * nx + ix] = true;
                }
            }
        }

        /// <summary>Жадное слияние занятых ячеек в максимальные прямоугольники (по строкам, затем по одинаковым прогонам).</summary>
        private static List<RectBox> MergeRects(bool[] occ, int nx, int nz, float x0, float z0, float top)
        {
            var open = new Dictionary<string, int[]>();   // ключ = "ix0,ix1" → {ix0,ix1,iz0,iz1}
            var closed = new List<int[]>();

            for (int iz = 0; iz < nz; iz++)
            {
                var active = new HashSet<string>();
                int ix = 0;
                while (ix < nx)
                {
                    if (!occ[(long)iz * nx + ix]) { ix++; continue; }
                    int s = ix;
                    while (ix < nx && occ[(long)iz * nx + ix]) ix++;
                    string key = s + "," + ix;
                    active.Add(key);
                    int[] r;
                    if (open.TryGetValue(key, out r)) r[3] = iz + 1;
                    else open[key] = new int[] { s, ix, iz, iz + 1 };
                }
                var toClose = new List<string>();
                foreach (string k in open.Keys) if (!active.Contains(k)) toClose.Add(k);
                foreach (string k in toClose) { closed.Add(open[k]); open.Remove(k); }
            }
            foreach (int[] r in open.Values) closed.Add(r);

            var rects = new List<RectBox>(closed.Count);
            foreach (int[] r in closed)
                rects.Add(new RectBox
                {
                    Top = top,
                    X0 = x0 + r[0] * Cell,
                    X1 = x0 + r[1] * Cell,
                    Z0 = z0 + r[2] * Cell,
                    Z1 = z0 + r[3] * Cell
                });
            return rects;
        }

        // ------------------------------------------------------------------ марши

        /// <summary>Разбивает меш маршей на связные острова (1 остров = 1 марш) и подгоняет по одному наклонному боксу.</summary>
        private static List<StairBox> BuildStairs(Report rep)
        {
            var result = new List<StairBox>();

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(StairsMeshPath);
            if (mesh == null)
            {
                rep.Warnings.Add("Не найден меш маршей: " + StairsMeshPath + " — марши не собраны");
                return result;
            }

            Vector3[] v = mesh.vertices;
            int[] tr = mesh.triangles;
            int n = v.Length;

            // сварка по позиции + union-find по треугольникам
            var rep2 = new int[n];
            var seen = new Dictionary<long, int>(n);
            for (int i = 0; i < n; i++)
            {
                long k = ((long)Mathf.RoundToInt(v[i].x * 100f) << 40) ^ ((long)Mathf.RoundToInt(v[i].y * 100f) << 20) ^ (long)Mathf.RoundToInt(v[i].z * 100f);
                int idx;
                if (!seen.TryGetValue(k, out idx)) { seen[k] = i; idx = i; }
                rep2[i] = idx;
            }

            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = rep2[i];
            for (int i = 0; i < n; i++) parent[i] = Find(parent, i);

            for (int i = 0; i + 2 < tr.Length; i += 3)
            {
                Union(parent, rep2[tr[i]], rep2[tr[i + 1]]);
                Union(parent, rep2[tr[i + 1]], rep2[tr[i + 2]]);
            }

            var islands = new Dictionary<int, List<int>>();
            for (int i = 0; i + 2 < tr.Length; i += 3)
            {
                int r = Find(parent, rep2[tr[i]]);
                List<int> list;
                if (!islands.TryGetValue(r, out list)) { list = new List<int>(); islands[r] = list; }
                list.Add(i);
            }

            foreach (var kv in islands)
            {
                List<int> faces = kv.Value;

                // 1) ступени: верхние горизонтальные грани с центрами и площадями
                var treads = new List<Vector3>();
                foreach (int i in faces)
                {
                    Vector3 A = v[tr[i]], B = v[tr[i + 1]], C = v[tr[i + 2]];
                    Vector3 cr = Vector3.Cross(B - A, C - A);
                    float ar = cr.magnitude * 0.5f;
                    if (ar < 1e-6f) continue;
                    if (cr.y / cr.magnitude < StairPlaneMinNormalY) continue;
                    treads.Add((A + B + C) / 3f);
                }
                if (treads.Count < 2) continue;

                // 2) ось марша = горизонтальная ось с большим разбросом центров ступеней
                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                foreach (Vector3 c in treads)
                {
                    if (c.x < minX) minX = c.x;
                    if (c.x > maxX) maxX = c.x;
                    if (c.z < minZ) minZ = c.z;
                    if (c.z > maxZ) maxZ = c.z;
                }
                Vector3 uDir = (maxX - minX) >= (maxZ - minZ) ? Vector3.right : Vector3.forward;

                // 3) нижняя и верхняя ступень по этой оси → прямая подъёма (без поперечного наклона)
                Vector3 low = treads[0], high = treads[0];
                float lowU = Vector3.Dot(treads[0], uDir), highU = lowU;
                for (int i = 1; i < treads.Count; i++)
                {
                    float u = Vector3.Dot(treads[i], uDir);
                    if (u < lowU) { lowU = u; low = treads[i]; }
                    if (u > highU) { highU = u; high = treads[i]; }
                }
                float dU = highU - lowU;
                float slope = Mathf.Abs(dU) > 1e-3f ? (high.y - low.y) / dU : 0f;   // подъём на единицу длины

                // 4) базис бокса: localX = ось марша, localY = нормаль пандуса, localZ = в плоскости
                Vector3 up = new Vector3(-slope * uDir.x, 1f, -slope * uDir.z).normalized;
                Vector3 across = Vector3.Cross(uDir, up).normalized;
                if (across.sqrMagnitude < 1e-8f) across = Vector3.forward;
                Vector3 planeDir = Vector3.Cross(up, across).normalized;   // = uDir (горизонталь)

                float planeConst = Vector3.Dot(up, low);                   // плоскость пандуса: up·p = planeConst

                float minU = float.MaxValue, maxU = float.MinValue, minW = float.MaxValue, maxW = float.MinValue;
                foreach (int i in faces)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        Vector3 p = v[tr[i + j]];
                        float pu = Vector3.Dot(p, planeDir);
                        float pw = Vector3.Dot(p, across);
                        if (pu < minU) minU = pu;
                        if (pu > maxU) maxU = pu;
                        if (pw < minW) minW = pw;
                        if (pw > maxW) maxW = pw;
                    }
                }
                if (minU > maxU) continue;

                // запас: в стороны и только к НИЗУ марша — перекрывает суб-клеточные щели у кромки колодца
                float runMin = minU, runMax = maxU;
                if (lowU <= highU) runMin -= StairEdgePad; else runMax += StairEdgePad;
                float acrossMin = minW - StairEdgePad, acrossMax = maxW + StairEdgePad;

                Vector3 center = planeDir * ((runMin + runMax) * 0.5f)
                               + across * ((acrossMin + acrossMax) * 0.5f)
                               + up * (planeConst - StairThickness * 0.5f);

                result.Add(new StairBox
                {
                    Center = center,
                    Rotation = Quaternion.LookRotation(across, up),
                    Size = new Vector3(runMax - runMin, StairThickness, acrossMax - acrossMin)
                });
            }

            return result;
        }

        /// <summary>Самый низкий угол бокса-марша по Y — по нему решается, на каких уровнях вырезать пятно.</summary>
        private static float StairLowestY(StairBox s)
        {
            Vector3 up = s.Rotation * new Vector3(0f, s.Size.y * 0.5f, 0f);
            Vector3 u = s.Rotation * new Vector3(s.Size.x * 0.5f, 0f, 0f);
            Vector3 w = s.Rotation * new Vector3(0f, 0f, s.Size.z * 0.5f);
            float lowest = float.MaxValue;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 p = s.Center + u * sx + up * sy + w * sz;
                        if (p.y < lowest) lowest = p.y;
                    }
            return lowest;
        }

        /// <summary>Высота верхней плоскости бокса-марша в точке (x,z).</summary>
        private static float StairHeightAt(StairBox s, float x, float z)
        {
            Vector3 n = s.Rotation * Vector3.up;                        // нормаль верхней грани
            Vector3 p0 = s.Center + n * (s.Size.y * 0.5f);               // точка на верхней грани
            if (Mathf.Abs(n.y) < 1e-4f) return s.Center.y;
            return p0.y - (n.x * (x - p0.x) + n.z * (z - p0.z)) / n.y;
        }

        private static void StairFootprint(StairBox s, out float x0, out float x1, out float z0, out float z1)
        {
            Vector3 up = s.Rotation * new Vector3(0f, s.Size.y * 0.5f, 0f);
            Vector3 u = s.Rotation * new Vector3(s.Size.x * 0.5f, 0f, 0f);
            Vector3 w = s.Rotation * new Vector3(0f, 0f, s.Size.z * 0.5f);
            x0 = float.MaxValue; x1 = float.MinValue; z0 = float.MaxValue; z1 = float.MinValue;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 p = s.Center + u * sx + up * sy + w * sz;
                        if (p.x < x0) x0 = p.x;
                        if (p.x > x1) x1 = p.x;
                        if (p.z < z0) z0 = p.z;
                        if (p.z > z1) z1 = p.z;
                    }
        }

        // ------------------------------------------------------------------ площадки маршей

        /// <summary>Верхние грани ВСЕХ мешей чанка (мощение в том числе) в пространстве корня — пол площадок.</summary>
        private static List<Tri> CollectSurfaceTopTris(Transform chunk, Matrix4x4 rootFromWorld)
        {
            var result = new List<Tri>();
            foreach (MeshFilter mf in chunk.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Vector3[] v = mf.sharedMesh.vertices;
                int[] tr = mf.sharedMesh.triangles;
                Matrix4x4 m = rootFromWorld * mf.transform.localToWorldMatrix;
                for (int i = 0; i + 2 < tr.Length; i += 3)
                {
                    Vector3 A = m.MultiplyPoint3x4(v[tr[i]]);
                    Vector3 B = m.MultiplyPoint3x4(v[tr[i + 1]]);
                    Vector3 C = m.MultiplyPoint3x4(v[tr[i + 2]]);
                    Vector3 cr = Vector3.Cross(B - A, C - A);
                    float mag = cr.magnitude;
                    if (mag < 1e-9f || cr.y / mag < StairPlaneMinNormalY) continue;
                    result.Add(new Tri { A = A, B = B, C = C });
                }
            }
            return result;
        }

        /// <summary>Самая высокая верхняя грань под потолком cap (навесы над маршем полом не считаем).</summary>
        private static float RayTopUnder(List<Tri> tris, float px, float pz, float cap)
        {
            float best = float.MinValue;
            for (int i = 0; i < tris.Count; i++)
            {
                Vector3 A = tris[i].A, B = tris[i].B, C = tris[i].C;
                float det = (B.z - C.z) * (A.x - C.x) + (C.x - B.x) * (A.z - C.z);
                if (Mathf.Abs(det) < 1e-9f) continue;
                float l1 = ((B.z - C.z) * (px - C.x) + (C.x - B.x) * (pz - C.z)) / det;
                float l2 = ((C.z - A.z) * (px - C.x) + (A.x - C.x) * (pz - C.z)) / det;
                float l3 = 1f - l1 - l2;
                if (l1 < 0f || l2 < 0f || l3 < 0f) continue;
                float y = l1 * A.y + l2 * B.y + l3 * C.y;
                if (y <= cap && y > best) best = y;
            }
            return best;
        }

        /// <summary>
        /// Площадки двухмаршевых лестниц (в BASE их две: y=14.76 и y=8.34). Площадка сделана мощением,
        /// а не deck steel, поэтому в плиты не попадает — под ней зияет дыра 3.2 м. Для каждого торца
        /// марша ищем ровный пол на его уровне; если плит под ним нет — ставим один бокс площадки.
        /// </summary>
        private static List<RectBox> BuildLandings(List<StairBox> stairs, List<Tri> surfaceTris, List<RectBox> plates)
        {
            var result = new List<RectBox>();
            if (surfaceTris.Count == 0) return result;

            foreach (StairBox s in stairs)
            {
                Vector3 run = s.Rotation * Vector3.right;      // в плоскости пандуса
                Vector3 across = s.Rotation * Vector3.forward;
                Vector3 acrossFlat = new Vector3(across.x, 0f, across.z).normalized;
                if (acrossFlat.sqrMagnitude < 1e-6f) acrossFlat = Vector3.forward;
                float halfRun = s.Size.x * 0.5f;
                float halfAcross = s.Size.z * 0.5f;

                for (int e = -1; e <= 1; e += 2)
                {
                    Vector3 end = s.Center + run * (e * halfRun);
                    float endLevel = StairHeightAt(s, end.x, end.z);
                    float cap = endLevel + 0.9f;               // выше — навес/ограждение, не пол
                    Vector3 outFlat = new Vector3(e * run.x, 0f, e * run.z).normalized;

                    float far = 0f;
                    float wMin = float.MaxValue, wMax = float.MinValue;
                    var levels = new List<float>();
                    int goodRows = 0;

                    for (float d = 0.8f; d <= LandingProbeDepth + 1e-3f; d += 0.6f)
                    {
                        int row = 0;
                        for (int k = -1; k <= 1; k++)
                        {
                            float w = k * halfAcross * 0.8f;
                            Vector3 p = end + outFlat * d + acrossFlat * w;
                            float f = RayTopUnder(surfaceTris, p.x, p.z, cap);
                            if (f == float.MinValue) continue;
                            if (Mathf.Abs(f - endLevel) > LandingLevelTol) continue;
                            row++;
                            levels.Add(f);
                            if (d > far) far = d;
                            if (w < wMin) wMin = w;
                            if (w > wMax) wMax = w;
                        }
                        if (row >= 2) goodRows++;
                    }

                    if (goodRows < 2 || levels.Count < 6 || far < 1.9f) continue;

                    levels.Sort();
                    float level = levels[levels.Count / 2];

                    // прямоугольник: от торца бокса наружу, в стороны — по подтверждённым пробам
                    float u1 = far + 0.2f;
                    float w0 = wMin - 0.3f, w1 = wMax + 0.3f;
                    float xa = float.MaxValue, xb = float.MinValue, za = float.MaxValue, zb = float.MinValue;
                    for (int u = 0; u <= 1; u++)
                        for (int w = 0; w <= 1; w++)
                        {
                            Vector3 p = end + outFlat * (u * u1) + acrossFlat * (w == 0 ? w0 : w1);
                            if (p.x < xa) xa = p.x;
                            if (p.x > xb) xb = p.x;
                            if (p.z < za) za = p.z;
                            if (p.z > zb) zb = p.z;
                        }

                    float area = (xb - xa) * (zb - za);
                    if (area < 3f) continue;

                    float cx = (xa + xb) * 0.5f, cz = (za + zb) * 0.5f;
                    if (CoveredAt(plates, cx, cz, level)) continue;          // площадка уже на плите
                    bool dup = false;
                    foreach (RectBox r in result)
                        if (cx > r.X0 && cx < r.X1 && cz > r.Z0 && cz < r.Z1 && Mathf.Abs(r.Top - level) <= LandingLevelTol) { dup = true; break; }
                    if (dup) continue;

                    result.Add(new RectBox { Top = level, X0 = xa, X1 = xb, Z0 = za, Z1 = zb });
                }
            }

            return result;
        }

        private static bool CoveredAt(List<RectBox> plates, float x, float z, float level)
        {
            foreach (RectBox p in plates)
                if (x > p.X0 && x < p.X1 && z > p.Z0 && z < p.Z1 && Mathf.Abs(p.Top - level) <= LandingLevelTol) return true;
            return false;
        }

        private static int Find(int[] parent, int x)
        {
            while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
            return x;
        }

        private static void Union(int[] parent, int a, int b)
        {
            int ra = Find(parent, a), rb = Find(parent, b);
            if (ra != rb) parent[ra] = rb;
        }

        // ------------------------------------------------------------------ проверка

        /// <summary>Лучи вниз по пятну чанка: попадание в новый коллайдер и совпадение с реальной поверхностью.</summary>
        public static string VerifyCoverage()
        {
            GameObject city = GameObject.Find(CityRootName);
            if (city == null) return "[PRIMUM/BASE] Не найден корень " + CityRootName;

            Transform chunk = city.transform.Find(ChunkName);
            if (chunk == null) return "[PRIMUM/BASE] Не найден чанк " + ChunkName;

            Matrix4x4 toRoot = city.transform.worldToLocalMatrix;
            List<Tri> tris = CollectTopTris(chunk, toRoot, new Report());
            if (tris.Count == 0) return "[PRIMUM/BASE] Нет поверхности для сравнения";

            Physics.SyncTransforms();

            const int GridX = 200, GridZ = 200;
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (Tri t in tris)
            {
                MinMax(t.A, ref x0, ref x1, ref z0, ref z1);
                MinMax(t.B, ref x0, ref x1, ref z0, ref z1);
                MinMax(t.C, ref x0, ref x1, ref z0, ref z1);
            }

            int surface = 0, hit = 0, both = 0, missCol = 0, ghost = 0;
            float worst = 0f;
            var ghosts = new List<string>();
            var misses = new List<string>();

            for (int iz = 0; iz < GridZ; iz++)
            {
                for (int ix = 0; ix < GridX; ix++)
                {
                    float px = Mathf.Lerp(x0, x1, (ix + 0.5f) / GridX);
                    float pz = Mathf.Lerp(z0, z1, (iz + 0.5f) / GridZ);

                    float analytic = RayTop(tris, px, pz);
                    bool hasSurface = analytic > float.MinValue;

                    if (!hasSurface) continue;
                    surface++;

                    // px/pz — в пространстве корня (PRIMUM); луч пускаем в мировых координатах
                    Vector3 world = city.transform.TransformPoint(new Vector3(px, analytic + 60f, pz));
                    float hitLocalY;
                    if (!TopBaseHit(city.transform, world, out hitLocalY)) { missCol++; if (misses.Count < 12) misses.Add(string.Format("({0:F0},{1:F0})", px, pz)); continue; }
                    hit++;
                    float diff = hitLocalY - analytic;
                    if (Mathf.Abs(diff) > 1.5f) { ghost++; if (ghosts.Count < 12) ghosts.Add(string.Format("({0:F0},{1:F0}) {2:+0.0;-0.0}", px, pz, diff)); continue; }
                    both++;
                    if (Mathf.Abs(diff) > worst) worst = Mathf.Abs(diff);
                }
            }

            // --- отдельно марши: попадание и совпадение с плоскостью марша
            Report stairRep = new Report();
            List<StairBox> stairs = BuildStairs(stairRep);
            int stairSamples = 0, stairHit = 0, stairUnder = 0, stairOver = 0;
            float stairWorst = 0f;
            foreach (StairBox s in stairs)
            {
                Vector3 run = s.Rotation * Vector3.right;
                Vector3 across = s.Rotation * Vector3.forward;
                for (int i = 0; i <= 8; i++)
                {
                    float t = -0.45f + 0.9f * i / 8f;              // от низа к верху, без самых кромок
                    Vector3 p = s.Center + run * (t * s.Size.x) + across * 0f;
                    float ramp = StairHeightAt(s, p.x, p.z);
                    stairSamples++;
                    Vector3 world = city.transform.TransformPoint(new Vector3(p.x, ramp + 30f, p.z));
                    float hitLocalY;
                    if (!TopBaseHit(city.transform, world, out hitLocalY)) { continue; }
                    stairHit++;
                    float d = hitLocalY - ramp;
                    if (d < -0.35f) stairUnder++;
                    else if (d > 0.35f) stairOver++;
                    if (Mathf.Abs(d) > stairWorst) stairWorst = Mathf.Abs(d);
                }
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[PRIMUM/BASE] Проверка покрытия (только коллайдеры BASE): сетка " + GridX + "×" + GridZ);
            sb.AppendLine("  точек с реальной поверхностью: " + surface);
            sb.AppendLine("  лучей попало в коллайдер BASE:  " + hit);
            sb.AppendLine("  попало в свою поверхность:     " + both + "  (макс. отклонение " + worst.ToString("F2") + " м)");
            sb.AppendLine("  промахов в пустоту:            " + missCol + (misses.Count > 0 ? "  " + string.Join(" ", misses.ToArray()) : ""));
            sb.AppendLine("  отклонение >1.5 м:             " + ghost + (ghosts.Count > 0 ? "  " + string.Join(" ", ghosts.ToArray()) : ""));
            sb.AppendLine("  марши: точек " + stairSamples + ", попало " + stairHit
                          + ", ниже на >0.35 м " + stairUnder
                          + ", выше на >0.35 м " + stairOver
                          + ", макс. отклонение " + stairWorst.ToString("F2") + " м");
            foreach (string w in stairRep.Warnings) sb.AppendLine("  ! " + w);
            return sb.ToString();
        }

        /// <summary>Верхнее попадание луча в коллайдеры текущего чанка BASE (коллайдеры домов не мешают).</summary>
        private static bool TopBaseHit(Transform city, Vector3 worldOrigin, out float localY)
        {
            localY = float.MinValue;
            RaycastHit[] hits = Physics.RaycastAll(worldOrigin, Vector3.down, 500f, ~0, QueryTriggerInteraction.Ignore);
            bool found = false;
            foreach (RaycastHit h in hits)
            {
                if (!IsUnderBaseChunk(h.collider.transform, city)) continue;
                float y = city.InverseTransformPoint(h.point).y;
                if (!found || y > localY) { localY = y; found = true; }
            }
            return found;
        }

        private static bool IsUnderBaseChunk(Transform t, Transform city)
        {
            Transform p = t;
            while (p != null && p != city)
            {
                if (p.name == ChunkName) return true;
                p = p.parent;
            }
            return false;
        }

        private static float RayTop(List<Tri> tris, float px, float pz)
        {
            float best = float.MinValue;
            for (int i = 0; i < tris.Count; i++)
            {
                Vector3 A = tris[i].A, B = tris[i].B, C = tris[i].C;
                float det = (B.z - C.z) * (A.x - C.x) + (C.x - B.x) * (A.z - C.z);
                if (Mathf.Abs(det) < 1e-9f) continue;
                float l1 = ((B.z - C.z) * (px - C.x) + (C.x - B.x) * (pz - C.z)) / det;
                float l2 = ((C.z - A.z) * (px - C.x) + (A.x - C.x) * (pz - C.z)) / det;
                float l3 = 1f - l1 - l2;
                if (l1 < 0f || l2 < 0f || l3 < 0f) continue;
                float y = l1 * A.y + l2 * B.y + l3 * C.y;
                if (y > best) best = y;
            }
            return best;
        }

        // ------------------------------------------------------------------ отчёт

        private static string Describe(Report r)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[PRIMUM/BASE] Коллайдеры низа города (плиты боксами + 1 бокс на марш)");
            sb.AppendLine("  боксов плит:   " + r.PlateBoxes + "   (толщина " + PlateThickness + " м)");
            sb.AppendLine("  боксов маршей: " + r.StairBoxes + "   (по одному наклонному на марш)");
            sb.AppendLine("  боксов площадок: " + r.LandingBoxes + "   (толщина " + LandingThickness + " м)");
            sb.AppendLine("  MeshCollider'ов поверхности (мощение+гранит): " + r.SurfaceColliders + "   (" + r.SurfaceTriangles + " тр.)");
            sb.AppendLine("  вырезано ячеек под пятна маршей: " + r.CarvedCells);
            foreach (string l in r.Levels) sb.AppendLine("   " + l);
            foreach (string w in r.Warnings) sb.AppendLine("  ! " + w);
            return sb.ToString();
        }
    }
}
