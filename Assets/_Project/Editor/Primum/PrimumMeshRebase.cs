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

        /// <summary>Подмена 1:1 одного объекта. Возвращает строку отчёта (и пишет её в reports/mesh_rebase_&lt;имя&gt;.txt).</summary>
        public static string Rebase(Transform t)
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
            var center = (min + max) * 0.5f;

            var nv = new Vector3[v.Count];
            for (int i = 0; i < v.Count; i++) nv[i] = pv[i] - center;
            Vector3[] nn = null;
            if (n.Count == v.Count)
            {
                nn = new Vector3[n.Count];
                for (int i = 0; i < n.Count; i++) nn[i] = N.MultiplyVector(n[i]).normalized;
            }
            Vector4[] nt = null;
            if (tg.Count == v.Count)
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
                EditorUtility.CopySerialized(dst, existing);
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
            rep.AppendLine("transform before   : pos " + V(L.GetColumn(3)) + " rot " + V(L.rotation.eulerAngles) + " scale " + V(L.lossyScale));
            rep.AppendLine("transform after    : pos " + V(t.localPosition) + " rot " + V(t.localEulerAngles) + " scale " + V(t.localScale));
            rep.AppendLine("object-space bounds: " + src.bounds.size.ToString("F3") + " (ед. FBX) -> " + dst.bounds.size.ToString("F3") + " (м)");
            rep.AppendLine("max vertex delta (parent space, double): " + maxPos.ToString("E2") + " m");
            rep.AppendLine("max vertex delta (world, float, info)  : " + maxWorld.ToString("E2") + " m (шаг float на ~40 км ≈ 3.9E-003)");
            rep.AppendLine("max world normal delta : " + maxNrm.ToString("E2"));
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

        // ------------------------------------------------------------------ утилиты

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
