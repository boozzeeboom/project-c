using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// PRIMUM, ДОМА: ТОЛЬКО БОКСОВЫЕ коллайдеры. Мебель, окна и декор в физику не попадают.
    ///
    /// Схема (одноэтажный дом с одной дверью): 3 стены по одному боксу + крыша одним боксом,
    /// фасад с дверью — 3 бокса (лево / право / над дверью), внутренняя стена — 1 бокс без двери
    /// и 3 с дверью. Двухэтажный дом — то же плюс перекрытие одним боксом на уровень и лестница
    /// одним наклонным боксом от первой ступени до последней.
    ///
    /// Чем это отличается от прежней сборки. Раньше стены собирались компланарными кусками по
    /// касанию габаритов, крыши и марши оставались точными мешами, а мебель и оконная столярка
    /// попадали в физику через геометрическое «угадывание» — дом превращался в сотни боксов и
    /// игрок не мог пройти. Теперь класс объекта определяется ТОЛЬКО по имени (Blender-скрипт
    /// <c>tools/houses_boxes.py</c>), а боксов на дом — единицы.
    ///
    /// Куда: <c>WorldRoot_0_0/PRIMUM_COLLIDERS/&lt;чанк&gt;/HOUSE_WALL_BOX | HOUSE_FLOOR_BOX |
    /// HOUSE_ROOF_BOX | HOUSE_STAIR_BOX</c>. Осевые боксы лежат на одном объекте группы (много
    /// BoxCollider'ов на одном трансформе), повёрнутые и наклонные — дочерними объектами.
    /// Трансформ объектов — identity: координаты уже в пространстве корня коллайдеров
    /// (он копирует трансформ PRIMUM и едет вместе с ребейзом мира). static-флаги НЕ ставим.
    /// Идемпотентно: группы <c>HOUSE_*</c> чанка сносятся перед сборкой.
    ///
    /// Меню: ProjectC → World → PRIMUM → Коллайдеры домов.
    /// </summary>
    public static class PrimumHouseColliders
    {
        private const string BoxesJsonRelative = "docs/world/primum_city/04_COLLIDERS/houses_boxes.json";
        private const string AdminJsonRelative = "docs/world/primum_city/04_COLLIDERS/houses_boxes_ADMIN.json";
        private const string CollidersRootName = "PRIMUM_COLLIDERS";
        private const string CityRootName = "PRIMUM";
        private const string WorldRootFallback = "WorldRoot_0_0";
        private const int CityLayer = 8;   // PRIMUM

        private const string WallBoxGroup = "HOUSE_WALL_BOX";
        private const string FloorBoxGroup = "HOUSE_FLOOR_BOX";
        private const string RoofBoxGroup = "HOUSE_ROOF_BOX";
        private const string StairBoxGroup = "HOUSE_STAIR_BOX";

        /// <summary>Меш-коллайдер маршей прошлой схемы (PrimumCollidersBuilder) — один
        /// невыпуклый MeshCollider на весь чанк. На ADMIN это 2640 треугольников в габарите
        /// y 18.4 м по всей шахте: он перекрывает новые наклонные боксы, и игрок упирается
        /// в меш прямо на ступенях. Сносится, когда для чанка есть боксовые марши.</summary>
        private const string LegacyStairMeshGroup = "STAIRS";
        private const float DoorProbeRadius = 0.30f;

        /// <summary>Группы прошлых схем, которых в сцене быть не должно.</summary>
        private static readonly string[] ObsoleteGroups =
        {
            "HOUSE_WALL", "HOUSE_FLOOR", "HOUSE_ROOF", "HOUSE_STAIR", "HOUSE_STEP_BOX"
        };

        /// <summary>Старые боксовые группы домов (PrimumCollidersBuilder) — заменены боксами из JSON.</summary>
        private static readonly string[] LegacyBoxGroups = { "WALL", "FLOOR", "ROOF" };

        // ------------------------------------------------------------------ меню

        [MenuItem("ProjectC/World/PRIMUM/Коллайдеры домов: боксы из RAW (пересобрать)", false, 360)]
        public static void MenuBuild()
        {
            Debug.Log(Describe(Build(true)));
        }

        [MenuItem("ProjectC/World/PRIMUM/Коллайдеры домов: отчёт (без изменений)", false, 361)]
        public static void MenuReport()
        {
            Debug.Log(Describe(Build(false)));
        }

        [MenuItem("ProjectC/World/PRIMUM/Коллайдеры домов: снести боксы WALL/FLOOR/ROOF", false, 362)]
        public static void MenuRemoveLegacyBoxes()
        {
            int n = RemoveLegacyBoxes();
            Debug.Log("[PRIMUM/Houses] снесено боксовых групп домов: " + n +
                      " (WALL/FLOOR/ROOF старой схемы)");
        }

        [MenuItem("ProjectC/World/PRIMUM/Коллайдеры домов: проверить проёмы и плиты", false, 363)]
        public static void MenuVerify()
        {
            Debug.Log(VerifyBoxes());
        }

        // ------------------------------------------------------------------ отчёт

        public class Report
        {
            public int Chunks;
            public int WallBoxes;
            public int FloorBoxes;
            public int RoofBoxes;
            public int StairBoxes;
            public int RotatedBoxes;
            public int BoxObjects;
            public int Doors;
            public int LegacyStairMeshesDropped;
            public readonly Dictionary<string, string> ChunkSummary = new Dictionary<string, string>();
            public readonly List<string> Warnings = new List<string>();

            public int Total { get { return WallBoxes + FloorBoxes + RoofBoxes + StairBoxes; } }
        }

        // ------------------------------------------------------------------ DTO для JSON

        [Serializable] private class BoxDto { public float[] c; public float[] s; public float ry; public float[] q; }
        [Serializable] private class DoorDto { public float[] c; public float[] t; public float w; public float h; public string n; }
        [Serializable]
        private class ChunkDto
        {
            public string chunk;
            public BoxDto[] walls;
            public BoxDto[] floors;
            public BoxDto[] roofs;
            public BoxDto[] stairs;
            public DoorDto[] doors;
        }
        [Serializable] private class RootDto { public ChunkDto[] chunks; }

        // ------------------------------------------------------------------ сборка

        public static Report Build(bool apply, string relativeJson = BoxesJsonRelative)
        {
            var rep = new Report();

            GameObject city = GameObject.Find(CityRootName);
            if (city == null) { rep.Warnings.Add("Не найден корень " + CityRootName); return rep; }

            Transform worldRoot = city.transform.parent != null ? city.transform.parent
                                : GameObject.Find(WorldRootFallback)?.transform;
            if (worldRoot == null) { rep.Warnings.Add("Не найден world root"); return rep; }

            Transform root = null;
            if (apply)
            {
                GameObject rootGo = GameObject.Find(CollidersRootName);
                if (rootGo == null) rootGo = new GameObject(CollidersRootName);
                rootGo.transform.SetParent(worldRoot, false);
                rootGo.transform.position = city.transform.position;
                rootGo.transform.rotation = city.transform.rotation;
                rootGo.transform.localScale = Vector3.one;
                rootGo.layer = CityLayer;
                GameObjectUtility.SetStaticEditorFlags(rootGo, (StaticEditorFlags)0);
                root = rootGo.transform;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string jsonPath = Path.Combine(projectRoot, relativeJson.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(jsonPath))
            {
                rep.Warnings.Add("Нет " + jsonPath + " — сначала прогоните tools/houses_boxes.py");
                return rep;
            }

            var dto = JsonUtility.FromJson<RootDto>(File.ReadAllText(jsonPath, Encoding.UTF8));
            if (dto == null || dto.chunks == null || dto.chunks.Length == 0)
            {
                rep.Warnings.Add("JSON не разобран: " + jsonPath);
                return rep;
            }

            foreach (ChunkDto cd in dto.chunks)
            {
                if (cd == null || string.IsNullOrEmpty(cd.chunk)) continue;

                int w = Count(cd.walls), f = Count(cd.floors), r = Count(cd.roofs), st = Count(cd.stairs);
                rep.Chunks++;
                rep.WallBoxes += w; rep.FloorBoxes += f; rep.RoofBoxes += r; rep.StairBoxes += st;
                rep.Doors += cd.doors != null ? cd.doors.Length : 0;
                rep.ChunkSummary[cd.chunk] = "стен=" + w + " плит=" + f + " крыш=" + r + " марш=" + st;
                if (!apply) continue;

                Transform chunkNode = EnsureChunkNode(root, cd.chunk);
                foreach (string obsolete in ObsoleteGroups) DropGroup(chunkNode, obsolete);
                DropGroup(chunkNode, WallBoxGroup);
                DropGroup(chunkNode, FloorBoxGroup);
                DropGroup(chunkNode, RoofBoxGroup);
                DropGroup(chunkNode, StairBoxGroup);
                // Меш старых маршей сносим только если в JSON есть чем его заменить.
                if (cd.stairs != null && cd.stairs.Length > 0)
                {
                    DropGroup(chunkNode, LegacyStairMeshGroup);
                    rep.LegacyStairMeshesDropped++;
                }

                int rot = 0;
                rot += AddBoxGroup(chunkNode, WallBoxGroup, cd.walls);
                rot += AddBoxGroup(chunkNode, FloorBoxGroup, cd.floors);
                rot += AddBoxGroup(chunkNode, RoofBoxGroup, cd.roofs);
                rot += AddBoxGroup(chunkNode, StairBoxGroup, cd.stairs);
                rep.RotatedBoxes += rot;
                rep.BoxObjects += (w > 0 ? 1 : 0) + (f > 0 ? 1 : 0) + (r > 0 ? 1 : 0) + (st > 0 ? 1 : 0);
            }

            if (apply)
            {
                Physics.SyncTransforms();
                EditorSceneManager.MarkSceneDirty(city.scene);
            }
            return rep;
        }

        private static int Count(BoxDto[] a)
        {
            return a == null ? 0 : a.Length;
        }

        private static void DropGroup(Transform chunkNode, string name)
        {
            Transform prev = chunkNode.Find(name);
            if (prev != null) UnityEngine.Object.DestroyImmediate(prev.gameObject);
        }

        /// <summary>
        /// Осевые боксы — на одном объекте группы (по BoxCollider на бокс), повёрнутые и наклонные —
        /// дочерними объектами. Возвращает число дочерних (повёрнутых) боксов.
        /// </summary>
        private static int AddBoxGroup(Transform chunkNode, string name, BoxDto[] boxes)
        {
            if (boxes == null || boxes.Length == 0) return 0;

            var go = new GameObject(name);
            go.transform.SetParent(chunkNode, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.layer = CityLayer;
            GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)0);

            Transform rotRoot = null;
            int flat = 0, rot = 0;
            foreach (BoxDto b in boxes)
            {
                if (b == null || b.c == null || b.c.Length < 3 || b.s == null || b.s.Length < 3) continue;
                Vector3 c = new Vector3(b.c[0], b.c[1], b.c[2]);
                Vector3 s = new Vector3(Mathf.Max(b.s[0], 0.05f), Mathf.Max(b.s[1], 0.05f), Mathf.Max(b.s[2], 0.05f));

                bool hasQ = b.q != null && b.q.Length >= 4;
                Quaternion q = hasQ ? new Quaternion(b.q[0], b.q[1], b.q[2], b.q[3]) : Quaternion.Euler(0f, b.ry, 0f);
                bool turned = hasQ
                    ? Quaternion.Angle(q, Quaternion.identity) > 0.05f
                    : Mathf.Abs(Mathf.DeltaAngle(0f, b.ry)) > 0.05f;

                if (!turned)
                {
                    var bc = go.AddComponent<BoxCollider>();
                    bc.center = c;
                    bc.size = s;
                    flat++;
                    continue;
                }

                if (rotRoot == null)
                {
                    var rg = new GameObject("R");
                    rg.transform.SetParent(go.transform, false);
                    rg.layer = CityLayer;
                    GameObjectUtility.SetStaticEditorFlags(rg, (StaticEditorFlags)0);
                    rotRoot = rg.transform;
                }
                var child = new GameObject("r" + rot.ToString(CultureInfo.InvariantCulture));
                child.transform.SetParent(rotRoot, false);
                child.transform.localPosition = c;
                child.transform.localRotation = q;
                child.layer = CityLayer;
                GameObjectUtility.SetStaticEditorFlags(child, (StaticEditorFlags)0);
                var cbc = child.AddComponent<BoxCollider>();
                cbc.center = Vector3.zero;
                cbc.size = s;
                rot++;
            }

            if (flat == 0 && rot == 0)
            {
                UnityEngine.Object.DestroyImmediate(go);
                return 0;
            }
            return rot;
        }

        private static Transform EnsureChunkNode(Transform root, string chunk)
        {
            Transform t = root.Find(chunk);
            if (t != null) return t;
            var go = new GameObject(chunk);
            go.transform.SetParent(root, false);
            go.layer = CityLayer;
            GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)0);
            return go.transform;
        }

        // ------------------------------------------------------------------ снос старых боксов

        /// <summary>Удаляет боксовые группы домов прошлой схемы (WALL/FLOOR/ROOF) во всех чанках.</summary>
        public static int RemoveLegacyBoxes()
        {
            GameObject rootGo = GameObject.Find(CollidersRootName);
            if (rootGo == null) return 0;

            int removed = 0;
            var toKill = new List<GameObject>();
            foreach (Transform chunk in rootGo.transform)
            {
                for (int i = chunk.childCount - 1; i >= 0; i--)
                {
                    Transform c = chunk.GetChild(i);
                    foreach (string legacy in LegacyBoxGroups)
                    {
                        if (!string.Equals(c.name, legacy, StringComparison.OrdinalIgnoreCase)) continue;
                        removed += c.GetComponentsInChildren<Collider>(true).Length;
                        toKill.Add(c.gameObject);
                        break;
                    }
                }
            }
            foreach (GameObject go in toKill) UnityEngine.Object.DestroyImmediate(go);

            if (removed > 0)
            {
                Physics.SyncTransforms();
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }
            return removed;
        }

        // ------------------------------------------------------------------ проверка

        /// <summary>
        /// Проверка боксов: (1) каждый дверной проём из JSON должен быть свободен шаром
        /// радиуса 0.30 м и иметь опору под ногами; (2) каждая плита должна ловиться лучом
        /// сверху в своей же точке. Проверка НЕ входит в Play Mode.
        /// </summary>
        public static string VerifyBoxes(string relativeJson = BoxesJsonRelative)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[PRIMUM/Houses] Проверка боксов (проёмы + плиты)");

            GameObject rootGo = GameObject.Find(CollidersRootName);
            if (rootGo == null) return sb.AppendLine("  ! нет узла " + CollidersRootName).ToString();
            Transform root = rootGo.transform;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string jsonPath = Path.Combine(projectRoot, relativeJson.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(jsonPath)) return sb.AppendLine("  ! нет " + jsonPath).ToString();

            var dto = JsonUtility.FromJson<RootDto>(File.ReadAllText(jsonPath, Encoding.UTF8));
            if (dto == null || dto.chunks == null) return sb.AppendLine("  ! JSON не разобран").ToString();

            int doors = 0, blocked = 0, noFloor = 0, plates = 0, plateMiss = 0;
            var badDoors = new List<string>();
            var badPlates = new List<string>();

            foreach (ChunkDto cd in dto.chunks)
            {
                if (cd == null || string.IsNullOrEmpty(cd.chunk)) continue;
                Transform chunkNode = root.Find(cd.chunk);

                if (cd.doors != null)
                {
                    foreach (DoorDto d in cd.doors)
                    {
                        if (d == null || d.c == null || d.c.Length < 3) continue;
                        doors++;
                        Vector3 w = root.TransformPoint(new Vector3(d.c[0], d.c[1], d.c[2]));
                        bool bad = false;
                        foreach (Collider h in Physics.OverlapSphere(w, DoorProbeRadius, ~0, QueryTriggerInteraction.Ignore))
                        {
                            if (IsUnderColliders(h.transform)) { bad = true; break; }
                        }
                        if (bad)
                        {
                            blocked++;
                            if (badDoors.Count < 10)
                                badDoors.Add(cd.chunk.Replace("PRIMUM_HOUSES_", "") +
                                             " (" + d.c[0].ToString("F1") + "," + d.c[1].ToString("F1") + "," +
                                             d.c[2].ToString("F1") + ")");
                            continue;
                        }
                        // Пол ищем под ПОРОГОМ проёма, а не в его середине: середина — это
                        // уже внутри объёма проёма, и луч оттуда может начаться внутри плиты.
                        Vector3 thr = new Vector3(w.x, w.y - d.h * 0.5f + 0.42f, w.z);
                        bool floor = false;
                        foreach (RaycastHit hf in Physics.RaycastAll(thr, Vector3.down, 3.0f,
                                                                     ~0, QueryTriggerInteraction.Ignore))
                        {
                            if (IsUnderColliders(hf.collider.transform)) { floor = true; break; }
                        }
                        if (!floor) noFloor++;
                    }
                }

                if (chunkNode == null) continue;

                Transform fg = chunkNode.Find(FloorBoxGroup);
                if (fg != null)
                {
                    foreach (BoxCollider bc in fg.GetComponentsInChildren<BoxCollider>(true))
                    {
                        plates++;
                        Vector3 top = bc.transform.TransformPoint(bc.center + Vector3.up * (bc.size.y * 0.5f + 0.05f));
                        if (!Physics.Raycast(top, Vector3.down, 2.0f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            plateMiss++;
                            if (badPlates.Count < 10)
                                badPlates.Add(cd.chunk.Replace("PRIMUM_HOUSES_", "") +
                                              " (" + top.x.ToString("F1") + "," + top.y.ToString("F1") + "," +
                                              top.z.ToString("F1") + ")");
                        }
                    }
                }
            }

            sb.AppendLine("  проёмов: " + doors + ", перекрыто (шар 0.30 м): " + blocked +
                          ", без пола под ногами: " + noFloor);
            sb.AppendLine("  плит: " + plates + ", промах луча вниз: " + plateMiss);
            foreach (string s in badDoors) sb.AppendLine("   ! перекрыт проём: " + s);
            foreach (string s in badPlates) sb.AppendLine("   ! плита без попадания: " + s);
            return sb.ToString();
        }

        /// <summary>Диагностика: какие именно проёмы остались без пола (имя + координаты + высота).</summary>
        public static string DebugDoorFloors(string relativeJson)
        {
            var sb = new StringBuilder();
            GameObject rootGo = GameObject.Find(CollidersRootName);
            if (rootGo == null) return "нет " + CollidersRootName;
            Transform root = rootGo.transform;
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string jsonPath = Path.Combine(projectRoot, relativeJson.Replace('/', Path.DirectorySeparatorChar));
            var dto = JsonUtility.FromJson<RootDto>(File.ReadAllText(jsonPath, Encoding.UTF8));
            int n = 0;
            foreach (ChunkDto cd in dto.chunks)
            {
                if (cd == null || cd.doors == null) continue;
                foreach (DoorDto d in cd.doors)
                {
                    Vector3 w = root.TransformPoint(new Vector3(d.c[0], d.c[1], d.c[2]));
                    Vector3 thr = new Vector3(w.x, w.y - d.h * 0.5f + 0.42f, w.z);
                    bool floor = false;
                    foreach (RaycastHit hf in Physics.RaycastAll(thr, Vector3.down, 3.0f, ~0, QueryTriggerInteraction.Ignore))
                        if (IsUnderColliders(hf.collider.transform)) { floor = true; break; }
                    if (floor) continue;
                    n++;
                    if (n <= 25)
                        sb.AppendLine("  " + (string.IsNullOrEmpty(d.n) ? "?" : d.n) +
                                      " c=(" + d.c[0].ToString("F2") + "," + d.c[1].ToString("F2") + "," + d.c[2].ToString("F2") +
                                      ") h=" + d.h.ToString("F2") + " thr.y=" + thr.y.ToString("F2"));
                }
            }
            sb.Insert(0, "без пола: " + n + "\n");
            return sb.ToString();
        }

        private static bool IsUnderColliders(Transform t)
        {
            Transform p = t;
            while (p != null)
            {
                if (p.name == CollidersRootName) return true;
                p = p.parent;
            }
            return false;
        }

        private static bool IsUnderNamed(Transform t, string name)
        {
            Transform p = t;
            while (p != null)
            {
                if (p.name == name) return true;
                p = p.parent;
            }
            return false;
        }

        /// <summary>Группа бокса, которая первой закрыла проход: WALL / FLOOR / ROOF / STAIR / OTHER.</summary>
        private static string GroupOf(Transform t)
        {
            Transform p = t;
            while (p != null)
            {
                if (p.name == WallBoxGroup) return "WALL";
                if (p.name == FloorBoxGroup) return "FLOOR";
                if (p.name == RoofBoxGroup) return "ROOF";
                if (p.name == StairBoxGroup) return "STAIR";
                p = p.parent;
            }
            return "OTHER";
        }

        /// <summary>
        /// Проверка ПРОХОДИМОСТИ проёма капсулой. Порог берётся из самой записи проёма
        /// (<c>c.y - h/2</c>), поэтому проверка не зависит от того, попал ли луч в перекрытие или
        /// в уличную плиту. Дверные полотна (<c>PRIMUM_DOORS</c>) игнорируются: закрытая створка —
        /// отдельная задача (см. 05_HOUSES_BOXES.md, раздел про двери), здесь проверяется стена.
        /// </summary>
        public static string VerifyPassages(string relativeJson = BoxesJsonRelative)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[PRIMUM/Houses] Проходимость проёмов капсулой 0.30 м");

            GameObject rootGo = GameObject.Find(CollidersRootName);
            if (rootGo == null) return sb.AppendLine("  ! нет узла " + CollidersRootName).ToString();
            Transform root = rootGo.transform;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string jsonPath = Path.Combine(projectRoot, relativeJson.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(jsonPath)) return sb.AppendLine("  ! нет " + jsonPath).ToString();

            var dto = JsonUtility.FromJson<RootDto>(File.ReadAllText(jsonPath, Encoding.UTF8));
            if (dto == null || dto.chunks == null) return sb.AppendLine("  ! JSON не разобран").ToString();

            int tested = 0, pass = 0, fail = 0, wall = 0;
            var byGroup = new Dictionary<string, int>();
            var bad = new List<string>();

            foreach (ChunkDto cd in dto.chunks)
            {
                if (cd == null || cd.doors == null) continue;
                foreach (DoorDto d in cd.doors)
                {
                    if (d == null || d.c == null || d.c.Length < 3) continue;
                    tested++;
                    Vector3 c = root.TransformPoint(new Vector3(d.c[0], d.c[1], d.c[2]));
                    float thr = c.y - d.h * 0.5f;
                    Vector3 p0 = new Vector3(c.x, thr + 0.32f, c.z);
                    Vector3 p1 = new Vector3(c.x, thr + 1.52f, c.z);

                    bool blocked = false;
                    string group = null;
                    foreach (Collider h in Physics.OverlapCapsule(p0, p1, DoorProbeRadius, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (IsUnderNamed(h.transform, "PRIMUM_DOORS")) continue;
                        blocked = true;
                        group = GroupOf(h.transform);
                        break;
                    }

                    if (!blocked) { pass++; continue; }
                    fail++;
                    if (group != "OTHER") wall++;
                    int seen;
                    byGroup.TryGetValue(group, out seen);
                    byGroup[group] = seen + 1;
                    if (bad.Count < 10)
                        bad.Add(cd.chunk.Replace("PRIMUM_HOUSES_", "") + " " +
                                (d.t == null || d.t.Length < 2 ? "?" : "") + "(" +
                                d.c[0].ToString("F1") + "," + d.c[1].ToString("F1") + "," +
                                d.c[2].ToString("F1") + ") w=" + d.w.ToString("F2"));
                }
            }

            sb.AppendLine("  проходов проверено: " + tested + ", прошло: " + pass + ", занято: " + fail +
                          " (боксами домов: " + wall + ")");
            sb.Append("  чем занято: ");
            foreach (var kv in byGroup) sb.Append(kv.Key + "=" + kv.Value + " ");
            sb.AppendLine();
            foreach (string s in bad) sb.AppendLine("   ! занят: " + s);
            return sb.ToString();
        }

        // ------------------------------------------------------------------ утилиты

        [MenuItem("ProjectC/World/PRIMUM/Коллайдеры домов: проходимость проёмов капсулой", false, 364)]
        public static void MenuVerifyPassages()
        {
            Debug.Log(VerifyPassages());
        }

        [MenuItem("ProjectC/World/PRIMUM/Коллайдеры ADMIN: боксы из RAW (пересобрать)", false, 365)]
        public static void MenuBuildAdmin()
        {
            Debug.Log(Describe(Build(true, AdminJsonRelative)));
        }

        [MenuItem("ProjectC/World/PRIMUM/Коллайдеры ADMIN: отчёт (без изменений)", false, 366)]
        public static void MenuReportAdmin()
        {
            Debug.Log(Describe(Build(false, AdminJsonRelative)));
        }

        [MenuItem("ProjectC/World/PRIMUM/Коллайдеры ADMIN: проверить проёмы и плиты", false, 367)]
        public static void MenuVerifyAdmin()
        {
            Debug.Log(VerifyBoxes(AdminJsonRelative));
        }

        [MenuItem("ProjectC/World/PRIMUM/Коллайдеры ADMIN: проходимость проёмов капсулой", false, 368)]
        public static void MenuVerifyPassagesAdmin()
        {
            Debug.Log(VerifyPassages(AdminJsonRelative));
        }

        private static string Describe(Report r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[PRIMUM/Houses] Боксы домов из RAW (стены / перекрытия / крыши / марши)");
            sb.AppendLine("  чанков: " + r.Chunks + ", боксов всего: " + r.Total);
            sb.AppendLine("    стены: " + r.WallBoxes + ", плиты: " + r.FloorBoxes +
                          ", крыши: " + r.RoofBoxes + ", марши: " + r.StairBoxes);
            sb.AppendLine("  проёмов дверей в JSON: " + r.Doors +
                          ", повёрнутых/наклонных боксов: " + r.RotatedBoxes);
            foreach (var kv in r.ChunkSummary) sb.AppendLine("   " + kv.Key.Replace("PRIMUM_", "") + ": " + kv.Value);
            foreach (string w in r.Warnings) sb.AppendLine("  ! " + w);
            return sb.ToString();
        }
    }
}
