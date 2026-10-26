using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// PRIMUM, этап Э3.1, блокер §8.13: городские меши приходят из FBX в «Blender-пространстве»
    /// (поворот X 270°, масштаб ×100, пивот у дома/чанка), а процедурные графы считают рисунок в
    /// object space и ждут Y-up, единицу = метр и пивот внутри объекта.
    ///
    /// Инструмент делает то же, что сделал бы правильный реимпорт (Bake Axis Conversion + единичный
    /// масштаб + пивот в центре меша), но **адресно** — для одного рендерера и **подменой 1:1**:
    ///  * вершины/нормали/касательные переводятся в пространство родителя (L_old · v) и сдвигаются к
    ///    центру габарита; все остальные каналы (UV, цвета, сабмеши, индексы) копируются как есть;
    ///  * трансформ становится (pos = центр, rot = identity, scale = 1) → мировая геометрия та же;
    ///  * новый меш сохраняется ассетом в <c>Assets/_Project/Art/Models/Primum/Rebased/&lt;чанк&gt;/</c>;
    ///  * правка — оверрайд инстанса (MeshFilter.m_Mesh + Transform), Undo, сцена не сохраняется;
    ///  * проверка: максимальное расхождение мировых позиций вершин и мировых нормалей до/после.
    /// FBX, импортёр и другие объекты не трогаются. Коллайдеры лежат отдельно (PRIMUM_COLLIDERS).
    ///
    /// Меню: ProjectC → World → PRIMUM → Материалы: Y-up меш … (386–387).
    /// </summary>
    public static class PrimumMeshRebase
    {
        private const string OutRoot = "Assets/_Project/Art/Models/Primum/Rebased";
        private const string ReportDirRelative = "docs/world/primum_city/09_MATERIALS/reports";

        [MenuItem("ProjectC/World/PRIMUM/Материалы: Y-up меш для выделенного (тест 1:1)", false, 386)]
        public static void MenuRebaseSelected()
        {
            var sb = new StringBuilder();
            foreach (var go in Selection.gameObjects) sb.AppendLine(Rebase(go.transform));
            Debug.Log("[PrimumMeshRebase]\n" + sb);
        }

        [MenuItem("ProjectC/World/PRIMUM/Материалы: вернуть исходный меш выделенного", false, 387)]
        public static void MenuRevertSelected()
        {
            var sb = new StringBuilder();
            foreach (var go in Selection.gameObjects) sb.AppendLine(Revert(go.transform));
            Debug.Log("[PrimumMeshRebase]\n" + sb);
        }

        [MenuItem("ProjectC/World/PRIMUM/Материалы: Y-up меши всего выделенного чанка (1:1)", false, 388)]
        public static void MenuRebaseChunk()
        {
            var t = Selection.activeTransform;
            var city = PrimumMaterialMap.FindCityRoot();
            if (t == null || city == null || t.parent != city)
            {
                EditorUtility.DisplayDialog("PRIMUM — Y-up", "Выделите чанк (прямой ребёнок WorldRoot_0_0/PRIMUM).", "OK");
                return;
            }
            Debug.Log("[PrimumMeshRebase]\n" + RebaseChunk(t));
        }

        /// <summary>
        /// Подмена 1:1 всех мешей чанка в ОДНО object space — пространство корня чанка (Y-up, метры).
        /// Для каждого рендерера: вершины = R·v, где R — цепочка локальных TRS от чанка до объекта
        /// (без мировых координат — точность), нормали — обратно-транспонированной R, тангенсы из нормали;
        /// локальный трансформ = обратный к цепочке родителей (для прямых детей — identity), иерархия не
        /// меняется. Все меши чанка — саб-ассеты одного файла Rebased/&lt;чанк&gt;/&lt;чанк&gt;_YUP_meshes.asset.
        /// Идемпотентно: объект уже в кадре чанка с Rebased-мешем — пропуск. Одна Undo-группа; сцена не сохраняется.
        /// </summary>
        public static string RebaseChunk(Transform chunk)
        {
            string dir = OutRoot + "/" + chunk.name;
            EnsureFolder(dir);
            string path = dir + "/" + chunk.name + "_YUP_meshes.asset";

            var existing = new Dictionary<string, Mesh>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Mesh em) existing[em.name] = em;
            bool containerExists = File.Exists(path);

            var filters = chunk.GetComponentsInChildren<MeshFilter>(true);
            int done = 0, already = 0, skipped = 0, created = 0, updated = 0, colliders = 0;
            long verts = 0; int degBefore = 0, degAfter = 0;
            double maxDelta = 0, maxFrame = 0;
            var skips = new List<string>();
            var used = new HashSet<string>();

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("PRIMUM: Y-up меши чанка 1:1 (" + chunk.name + ")");
            try
            {
                for (int fi = 0; fi < filters.Length; fi++)
                {
                    var mf = filters[fi];
                    if ((fi & 255) == 0) EditorUtility.DisplayProgressBar("PRIMUM — Y-up", chunk.name + ": " + fi + " / " + filters.Length, (float)fi / filters.Length);
                    var t = mf.transform;
                    var src = mf.sharedMesh;
                    if (src == null) { skipped++; continue; }
                    var mc = t.GetComponent<MeshCollider>();
                    if (t.GetComponent<SkinnedMeshRenderer>() != null || t.childCount > 0)
                    { skipped++; skips.Add(t.name + ": skinned/children"); continue; }

                    Matrix4x4 R = ChainToChunk(t, chunk, true);
                    Matrix4x4 P = ChainToChunk(t, chunk, false);
                    if (IsIdentity(R) && AssetDatabase.GetAssetPath(src).StartsWith(OutRoot, System.StringComparison.Ordinal)) { already++; continue; }
                    if (R.determinant <= 0f) { skipped++; skips.Add(t.name + ": det<=0"); continue; }

                    // новый локальный трансформ = P^-1 (равномерный масштаб + поворот)
                    Vector3 lp; Quaternion lr; Vector3 ls;
                    if (!InverseTRS(P, out lp, out lr, out ls)) { skipped++; skips.Add(t.name + ": цепочка родителей не TRS"); continue; }
                    var check = P * Matrix4x4.TRS(lp, lr, ls);
                    maxFrame = System.Math.Max(maxFrame, FrameError(check));

                    var v = new List<Vector3>(); src.GetVertices(v);
                    var n = new List<Vector3>(); src.GetNormals(n);
                    if (v.Count == 0) { skipped++; skips.Add(t.name + ": вершины недоступны"); continue; }
                    degBefore += Degenerate(src);
                    var N = R.inverse.transpose;
                    var nv = new Vector3[v.Count];
                    for (int i = 0; i < v.Count; i++) nv[i] = R.MultiplyPoint3x4(v[i]);
                    Vector3[] nn = null; Vector4[] nt = null;
                    if (n.Count == v.Count)
                    {
                        nn = new Vector3[n.Count]; nt = new Vector4[n.Count];
                        for (int i = 0; i < n.Count; i++) { nn[i] = N.MultiplyVector(n[i]).normalized; nt[i] = TangentFromNormal(nn[i]); }
                    }

                    var dst = Object.Instantiate(src);
                    string key = SafeName(RelPath(t, chunk)) + "_YUP";
                    while (!used.Add(key)) key += "_";
                    dst.name = key;
                    dst.SetVertices(nv);
                    if (nn != null) { dst.SetNormals(nn); dst.SetTangents(nt); }
                    dst.RecalculateBounds();

                    Mesh asset;
                    if (existing.TryGetValue(key, out var ex)) { CopyMeshInto(dst, ex); Object.DestroyImmediate(dst); asset = ex; EditorUtility.SetDirty(ex); updated++; }
                    else
                    {
                        if (!containerExists) { AssetDatabase.CreateAsset(dst, path); containerExists = true; }
                        else AssetDatabase.AddObjectToAsset(dst, path);
                        asset = dst; created++;
                    }

                    // проверка 1:1 в пространстве чанка: старое R·v против нового (P·L_new)·v'
                    var av = new List<Vector3>(); asset.GetVertices(av);
                    for (int i = 0; i < v.Count; i++)
                    {
                        var a = R.MultiplyPoint3x4(v[i]);
                        var b = check.MultiplyPoint3x4(av[i]);
                        maxDelta = System.Math.Max(maxDelta, (a - b).magnitude);
                    }
                    // Коллайдер на том же объекте (в городе это один объект — мостовая
                    // PRIMUM_BASE_..._M_M_PAVING_BRUSCHATKA): его меш-ассет нигде больше не используется,
                    // поэтому пересобираем его в том же кадре чанка тем же R — мировая геометрия 1:1.
                    Mesh mcAsset = null;
                    if (mc != null && mc.sharedMesh != null)
                    {
                        var cv = new List<Vector3>(); mc.sharedMesh.GetVertices(cv);
                        if (cv.Count > 0)
                        {
                            var cdst = Object.Instantiate(mc.sharedMesh);
                            string ckey = key + "_MC_YUP";
                            while (!used.Add(ckey)) ckey += "_";
                            cdst.name = ckey;
                            var cnv = new Vector3[cv.Count];
                            for (int i = 0; i < cv.Count; i++) cnv[i] = R.MultiplyPoint3x4(cv[i]);
                            cdst.SetVertices(cnv);
                            cdst.RecalculateBounds();
                            if (existing.TryGetValue(ckey, out var cex)) { CopyMeshInto(cdst, cex); Object.DestroyImmediate(cdst); mcAsset = cex; EditorUtility.SetDirty(cex); updated++; }
                            else
                            {
                                if (!containerExists) { AssetDatabase.CreateAsset(cdst, path); containerExists = true; }
                                else AssetDatabase.AddObjectToAsset(cdst, path);
                                mcAsset = cdst; created++;
                            }
                            var cav = new List<Vector3>(); mcAsset.GetVertices(cav);
                            for (int i = 0; i < cv.Count; i++)
                            {
                                var a = R.MultiplyPoint3x4(cv[i]);
                                var b = check.MultiplyPoint3x4(cav[i]);
                                maxDelta = System.Math.Max(maxDelta, (a - b).magnitude);
                            }
                        }
                    }

                    degAfter += Degenerate(asset);
                    verts += v.Count;

                    Undo.RecordObject(mf, "Y-up");
                    Undo.RecordObject(t, "Y-up");
                    mf.sharedMesh = asset;
                    t.localPosition = lp; t.localRotation = lr; t.localScale = ls;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(mf);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                    if (mcAsset != null)
                    {
                        Undo.RecordObject(mc, "Y-up collider");
                        mc.sharedMesh = mcAsset;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(mc);
                        colliders++;
                    }
                    done++;
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(chunk.gameObject.scene);

            var rep = new StringBuilder();
            rep.AppendLine("PRIMUM chunk Y-up rebase 1:1 — " + HierarchyPath(chunk) + "  (" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")");
            rep.AppendLine("mesh filters        : " + filters.Length);
            rep.AppendLine("rebased now         : " + done + " (new sub-assets " + created + ", updated " + updated + ")");
            rep.AppendLine("already in frame    : " + already);
            rep.AppendLine("mesh colliders      : " + colliders + " (меш коллайдера пересобран в том же кадре)");
            rep.AppendLine("skipped             : " + skipped);
            foreach (var s in skips) rep.AppendLine("    skip: " + s);
            rep.AppendLine("vertices rebased    : " + verts);
            rep.AppendLine("degenerate tangents : " + degBefore + " -> " + degAfter);
            rep.AppendLine("max frame error |P·L_new − I| : " + maxFrame.ToString("E2"));
            rep.AppendLine("max vertex delta (chunk space) : " + maxDelta.ToString("E2") + " m");
            rep.AppendLine("asset               : " + path);
            rep.AppendLine("VERDICT 1:1         : " + (maxDelta < 1e-3 && maxFrame < 1e-4 && degAfter == 0 ? "PASS" : "CHECK"));
            string text = rep.ToString();
            WriteText(ReportDirRelative + "/mesh_rebase_chunk_" + SafeName(chunk.name) + ".txt", text);
            return text;
        }

        /// <summary>Цепочка локальных TRS от чанка до t (includeSelf) или до родителя t.</summary>
        private static Matrix4x4 ChainToChunk(Transform t, Transform chunk, bool includeSelf)
        {
            var list = new List<Transform>();
            for (var x = includeSelf ? t : t.parent; x != null && x != chunk; x = x.parent) list.Add(x);
            var m = Matrix4x4.identity;
            for (int i = list.Count - 1; i >= 0; i--) m = m * Matrix4x4.TRS(list[i].localPosition, list[i].localRotation, list[i].localScale);
            return m;
        }

        private static bool InverseTRS(Matrix4x4 p, out Vector3 pos, out Quaternion rot, out Vector3 scale)
        {
            pos = Vector3.zero; rot = Quaternion.identity; scale = Vector3.one;
            if (IsIdentity(p)) return true;
            var s = p.lossyScale;
            if (s.x <= 0 || Mathf.Abs(s.x - s.y) > 1e-4f * s.x || Mathf.Abs(s.x - s.z) > 1e-4f * s.x) return false;
            var q = p.rotation;
            var qi = Quaternion.Inverse(q);
            float si = 1f / s.x;
            Vector3 tr = p.GetColumn(3);
            rot = qi; scale = new Vector3(si, si, si); pos = -(qi * tr) * si;
            return true;
        }

        private static bool IsIdentity(Matrix4x4 m)
        {
            return FrameError(m) < 1e-6;
        }

        private static double FrameError(Matrix4x4 m)
        {
            double e = 0;
            for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) e = System.Math.Max(e, System.Math.Abs(m[r, c] - (r == c ? 1f : 0f)));
            return e;
        }

        private static string RelPath(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (var x = t; x != null && x != root; x = x.parent) parts.Add(x.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        /// <summary>
        /// Подмена 1:1 одного объекта. Возвращает строку отчёта (и пишет её в reports/mesh_rebase_&lt;имя&gt;.txt).
        /// <paramref name="sharedFrame"/> = true (по умолчанию): пивот = начало родителя (чанка) — у всех мешей
        /// чанка одно object space, совпадающие грани одного материала дают одинаковый рисунок и z-fight
        /// не виден. false: пивот в центре габарита меша.
        /// </summary>
        public static string Rebase(Transform t, bool sharedFrame = true)
        {
            if (t == null) return "ERROR: null";
            var mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return "SKIP " + t.name + ": нет MeshFilter/меша";
            if (t.GetComponent<SkinnedMeshRenderer>() != null) return "SKIP " + t.name + ": skinned";
            if (t.childCount > 0) return "SKIP " + t.name + ": есть дочерние объекты (сдвинулись бы)";
            if (t.GetComponent<MeshCollider>() != null) return "SKIP " + t.name + ": MeshCollider на объекте";
            if (t.localRotation == Quaternion.identity && t.localScale == Vector3.one)
                return "SKIP " + t.name + ": уже в Y-up/единичном масштабе";

            var src = mf.sharedMesh;
            var L = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale);
            if (L.determinant <= 0f) return "SKIP " + t.name + ": зеркальный трансформ (det <= 0)";
            var N = L.inverse.transpose;

            var v = new List<Vector3>(); src.GetVertices(v);
            var n = new List<Vector3>(); src.GetNormals(n);
            var tg = new List<Vector4>(); src.GetTangents(tg);
            if (v.Count == 0) return "ERROR " + t.name + ": вершины недоступны (isReadable=" + src.isReadable + ")";

            // в пространство родителя
            var pv = new Vector3[v.Count];
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = -min;
            for (int i = 0; i < v.Count; i++)
            {
                pv[i] = L.MultiplyPoint3x4(v[i]);
                min = Vector3.Min(min, pv[i]);
                max = Vector3.Max(max, pv[i]);
            }
            var center = sharedFrame ? Vector3.zero : (min + max) * 0.5f;

            var nv = new Vector3[v.Count];
            for (int i = 0; i < v.Count; i++) nv[i] = pv[i] - center;
            Vector3[] nn = null;
            if (n.Count == v.Count)
            {
                nn = new Vector3[n.Count];
                for (int i = 0; i < n.Count; i++) nn[i] = N.MultiplyVector(n[i]).normalized;
            }
            Vector4[] nt = null;
            if (nn != null)
            {
                // У городских FBX нет UV, поэтому импортёр ставит ВСЕМ вершинам один тангенс
                // (1,0,0,-1). На гранях с нормалью ±X он параллелен нормали → TBN вырожден → NormalTS
                // графа (Normal From Height) даёт чёрные мелькающие грани на одном боку всех домов.
                // Строим ортонормальный тангенс из нормали (рисунок от UV не зависит, важна только
                // невырожденность): стена → горизонталь вдоль стены, пол/потолок → по X.
                nt = new Vector4[nn.Length];
                for (int i = 0; i < nn.Length; i++) nt[i] = TangentFromNormal(nn[i]);
            }
            else if (tg.Count == v.Count)
            {
                nt = new Vector4[tg.Count];
                for (int i = 0; i < tg.Count; i++)
                {
                    var d = L.MultiplyVector(new Vector3(tg[i].x, tg[i].y, tg[i].z)).normalized;
                    nt[i] = new Vector4(d.x, d.y, d.z, tg[i].w);
                }
            }

            var dst = Object.Instantiate(src);
            dst.name = src.name + "_YUP";
            dst.SetVertices(nv);
            if (nn != null) dst.SetNormals(nn);
            if (nt != null) dst.SetTangents(nt);
            dst.RecalculateBounds();

            string chunk = t.parent != null ? t.parent.name : "root";
            string dir = OutRoot + "/" + chunk;
            EnsureFolder(dir);
            string path = dir + "/" + src.name + "_YUP.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // НЕ EditorUtility.CopySerialized: он переписывает сериализованные данные меша, но не
                // перезаливает буферы в GPU — рендер остаётся со старыми вершинами (25.10: стены
                // «пропали» — нарисованы со сдвигом пивота). Запись через Mesh API буферы обновляет.
                CopyMeshInto(dst, existing);
                Object.DestroyImmediate(dst);
                dst = existing;
                EditorUtility.SetDirty(dst);
            }
            else
            {
                AssetDatabase.CreateAsset(dst, path);
            }
            AssetDatabase.SaveAssets();
            dst = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            // мировые данные до подмены — для проверки 1:1
            var worldBefore = t.localToWorldMatrix;
            var worldNBefore = worldBefore.inverse.transpose;

            Undo.IncrementCurrentGroup();
            int g = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("PRIMUM: Y-up меш 1:1 (" + t.name + ")");
            Undo.RecordObject(mf, "Y-up mesh");
            Undo.RecordObject(t, "Y-up mesh");
            mf.sharedMesh = dst;
            t.localPosition = center;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(mf);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            Undo.CollapseUndoOperations(g);
            EditorSceneManager.MarkSceneDirty(t.gameObject.scene);

            // проверка 1:1 в пространстве родителя, в double (мировые координаты ~40 км: шаг float там
            // ~4 мм, сравнение в мире меряло бы точность float, а не подмены)
            var worldAfter = t.localToWorldMatrix;
            var worldNAfter = worldAfter.inverse.transpose;
            var dv = new List<Vector3>(); dst.GetVertices(dv);
            var dn = new List<Vector3>(); dst.GetNormals(dn);
            double maxPos = 0, maxNrm = 0, maxWorld = 0;
            for (int i = 0; i < v.Count; i++)
            {
                double ax = (double)L.m00 * v[i].x + (double)L.m01 * v[i].y + (double)L.m02 * v[i].z + L.m03;
                double ay = (double)L.m10 * v[i].x + (double)L.m11 * v[i].y + (double)L.m12 * v[i].z + L.m13;
                double az = (double)L.m20 * v[i].x + (double)L.m21 * v[i].y + (double)L.m22 * v[i].z + L.m23;
                double bx = (double)t.localPosition.x + dv[i].x;
                double by = (double)t.localPosition.y + dv[i].y;
                double bz = (double)t.localPosition.z + dv[i].z;
                double d = System.Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by) + (az - bz) * (az - bz));
                maxPos = System.Math.Max(maxPos, d);
                maxWorld = System.Math.Max(maxWorld, (worldBefore.MultiplyPoint3x4(v[i]) - worldAfter.MultiplyPoint3x4(dv[i])).magnitude);
                if (n.Count == v.Count && dn.Count == v.Count)
                {
                    var na = worldNBefore.MultiplyVector(n[i]).normalized;
                    var nb = worldNAfter.MultiplyVector(dn[i]).normalized;
                    maxNrm = System.Math.Max(maxNrm, (na - nb).magnitude);
                }
            }

            var rep = new StringBuilder();
            rep.AppendLine("PRIMUM mesh rebase 1:1 — " + HierarchyPath(t));
            rep.AppendLine("source mesh        : " + AssetDatabase.GetAssetPath(src) + " :: " + src.name);
            rep.AppendLine("new mesh           : " + path);
            rep.AppendLine("vertices / submeshes / indices : " + v.Count + " / " + src.subMeshCount + " / " + Indices(src) + "  ->  " + dv.Count + " / " + dst.subMeshCount + " / " + Indices(dst));
            rep.AppendLine("uv channels        : " + UvChannels(src) + " -> " + UvChannels(dst) + "; colors " + src.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color) + " -> " + dst.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color));
            rep.AppendLine("pivot              : " + (sharedFrame ? "начало чанка (общее object space)" : "центр габарита меша"));
            rep.AppendLine("transform before   : pos " + V(L.GetColumn(3)) + " rot " + V(L.rotation.eulerAngles) + " scale " + V(L.lossyScale));
            rep.AppendLine("transform after    : pos " + V(t.localPosition) + " rot " + V(t.localEulerAngles) + " scale " + V(t.localScale));
            rep.AppendLine("object-space bounds: " + src.bounds.size.ToString("F3") + " (ед. FBX) -> " + dst.bounds.size.ToString("F3") + " (м)");
            rep.AppendLine("max vertex delta (parent space, double): " + maxPos.ToString("E2") + " m");
            rep.AppendLine("max vertex delta (world, float, info)  : " + maxWorld.ToString("E2") + " m (шаг float на ~40 км ≈ 3.9E-003)");
            rep.AppendLine("max world normal delta : " + maxNrm.ToString("E2"));
            rep.AppendLine("degenerate tangents (|t·n| > 0.9): " + Degenerate(src) + " -> " + Degenerate(dst));
            rep.AppendLine("VERDICT 1:1        : " + (maxPos < 1e-4 && maxNrm < 1e-4 ? "PASS" : "FAIL"));
            string text = rep.ToString();
            WriteText(ReportDirRelative + "/mesh_rebase_" + SafeName(t.name) + ".txt", text);
            return text;
        }

        public static string Revert(Transform t)
        {
            var mf = t != null ? t.GetComponent<MeshFilter>() : null;
            if (mf == null) return "SKIP: нет MeshFilter";
            if (!PrefabUtility.IsPartOfPrefabInstance(mf)) return "SKIP " + t.name + ": не часть префаб-инстанса";
            PrefabUtility.RevertObjectOverride(mf, InteractionMode.UserAction);
            PrefabUtility.RevertObjectOverride(t, InteractionMode.UserAction);
            return "reverted " + t.name + ": mesh=" + (mf.sharedMesh ? mf.sharedMesh.name : "null") + " rot " + V(t.localEulerAngles) + " scale " + V(t.localScale);
        }

        /// <summary>Ортонормальный тангенс для меша без UV: перпендикулярен нормали, w = +1.</summary>
        public static Vector4 TangentFromNormal(Vector3 n)
        {
            var a = Mathf.Abs(n.y) < 0.99f ? Vector3.up : Vector3.forward;
            var t = Vector3.Cross(a, n).normalized;
            return new Vector4(t.x, t.y, t.z, 1f);
        }

        // ------------------------------------------------------------------ утилиты

        /// <summary>Перенос всех каналов и сабмешей в существующий меш-ассет через Mesh API (с обновлением GPU-буферов).</summary>
        private static void CopyMeshInto(Mesh src, Mesh dst)
        {
            dst.Clear();
            dst.indexFormat = src.indexFormat;
            var v = new List<Vector3>(); src.GetVertices(v); dst.SetVertices(v);
            var n = new List<Vector3>(); src.GetNormals(n); if (n.Count == v.Count) dst.SetNormals(n);
            var t = new List<Vector4>(); src.GetTangents(t); if (t.Count == v.Count) dst.SetTangents(t);
            var c = new List<Color>(); src.GetColors(c); if (c.Count == v.Count) dst.SetColors(c);
            for (int ch = 0; ch < 8; ch++)
            {
                var uv = new List<Vector4>(); src.GetUVs(ch, uv);
                if (uv.Count == v.Count) dst.SetUVs(ch, uv);
            }
            dst.subMeshCount = src.subMeshCount;
            for (int s = 0; s < src.subMeshCount; s++)
                dst.SetIndices(src.GetIndices(s), src.GetTopology(s), s, false, (int)src.GetBaseVertex(s));
            dst.RecalculateBounds();
        }
        private static int Degenerate(Mesh m)
        {
            var n = new List<Vector3>(); m.GetNormals(n);
            var t = new List<Vector4>(); m.GetTangents(t);
            if (n.Count == 0 || t.Count != n.Count) return -1;
            int c = 0;
            for (int i = 0; i < n.Count; i++)
            {
                var tv = new Vector3(t[i].x, t[i].y, t[i].z);
                if (tv.sqrMagnitude < 1e-8f || Mathf.Abs(Vector3.Dot(tv.normalized, n[i].normalized)) > 0.9f) c++;
            }
            return c;
        }

        private static long Indices(Mesh m)
        {
            long c = 0;
            for (int s = 0; s < m.subMeshCount; s++) c += m.GetIndexCount(s);
            return c;
        }

        private static int UvChannels(Mesh m)
        {
            int c = 0;
            for (int i = 0; i < 8; i++)
                if (m.HasVertexAttribute((UnityEngine.Rendering.VertexAttribute)((int)UnityEngine.Rendering.VertexAttribute.TexCoord0 + i))) c++;
            return c;
        }

        private static string V(Vector3 v) => "(" + v.x.ToString("0.###") + ", " + v.y.ToString("0.###") + ", " + v.z.ToString("0.###") + ")";
        private static string V(Vector4 v) => V((Vector3)v);

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
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

        private static void WriteText(string relativePath, string content)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string full = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(full, content, new UTF8Encoding(false));
        }
    }
}
