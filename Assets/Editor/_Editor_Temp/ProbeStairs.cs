using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProjectC.TempProbes
{
    /// <summary>Числовая приёмка секции «Лестницы» на синтетическом марше 8 ступеней.</summary>
    public static class ProbeStairs
    {
        private const float Riser = 0.19f;
        private const float Tread = 0.29f;
        private const float Width = 1.2f;
        private const int Steps = 8;

        private static Type _tool;

        private static Type Tool
        {
            get
            {
                if (_tool != null) return _tool;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("ProjectC.EditorTools.StairCollidersTool", false);
                    if (t != null) { _tool = t; return t; }
                }
                return null;
            }
        }

        private static void Call(string method, params object[] args)
        {
            var m = Tool.GetMethod(method, BindingFlags.Public | BindingFlags.Static);
            if (m == null) throw new Exception("no method " + method);
            m.Invoke(null, args);
        }

        // ---------------------------------------------------------------- меш

        private static void AddQuad(List<int> t, int a, int b, int c, int d)
        {
            t.Add(a); t.Add(b); t.Add(c);
            t.Add(a); t.Add(c); t.Add(d);
        }

        private static void AddBox(List<Vector3> v, List<int> t, Vector3 min, Vector3 max)
        {
            int b = v.Count;
            v.Add(new Vector3(min.x, min.y, min.z));
            v.Add(new Vector3(max.x, min.y, min.z));
            v.Add(new Vector3(max.x, min.y, max.z));
            v.Add(new Vector3(min.x, min.y, max.z));
            v.Add(new Vector3(min.x, max.y, min.z));
            v.Add(new Vector3(max.x, max.y, min.z));
            v.Add(new Vector3(max.x, max.y, max.z));
            v.Add(new Vector3(min.x, max.y, max.z));
            AddQuad(t, b + 4, b + 7, b + 6, b + 5);   // верх
            AddQuad(t, b + 0, b + 1, b + 2, b + 3);   // низ
            AddQuad(t, b + 0, b + 4, b + 5, b + 1);   // -Z
            AddQuad(t, b + 3, b + 2, b + 6, b + 7);   // +Z
            AddQuad(t, b + 0, b + 3, b + 7, b + 4);   // -X
            AddQuad(t, b + 1, b + 5, b + 6, b + 2);   // +X
        }

        private static Mesh BuildMesh(Action<List<Vector3>, List<int>> fill)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            fill(v, t);

            // Обмотка: у замкнутого меша знаковый объём должен быть положительным.
            double vol = 0;
            for (int i = 0; i + 2 < t.Count; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                vol += Vector3.Dot(a, Vector3.Cross(b, c)) / 6.0;
            }
            if (vol < 0)
                for (int i = 0; i + 2 < t.Count; i += 3)
                {
                    int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp;
                }

            var m = new Mesh { name = "probe_stair" };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static GameObject MakeFlight(Transform parent, string name, int from, int to)
        {
            Mesh m = BuildMesh((v, t) =>
            {
                for (int i = from; i <= to; i++)
                    AddBox(v, t,
                        new Vector3(i * Tread, 0f, -Width * 0.5f),
                        new Vector3((i + 1) * Tread, (i + 1) * Riser, Width * 0.5f));
            });

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = null;
            return go;
        }

        // ---------------------------------------------------------------- утилиты

        private static Transform FindHolder(Transform src, string suffix)
        {
            Transform p = src.parent;
            return p == null ? null : p.Find(src.name + suffix);
        }

        private static string RayHitName(Vector3 xz, float fromY)
        {
            var hits = Physics.RaycastAll(new Vector3(xz.x, fromY, xz.z), Vector3.down, 200f);
            foreach (var h in hits)
                if (h.collider != null && h.collider.name.Contains("_STAIR")) return $"{h.point.y:F4}";
            foreach (var h in hits)
                if (h.collider != null) return $"чужой:{h.collider.name}";
            return "нет";
        }

        private static float RayHitY(Vector3 xz, float fromY)
        {
            var hits = Physics.RaycastAll(new Vector3(xz.x, fromY, xz.z), Vector3.down, 200f);
            foreach (var h in hits)
                if (h.collider != null && h.collider.name.Contains("_STAIR")) return h.point.y;
            return float.NaN;
        }

        private static string Diag(Vector3 xz, float fromY)
        {
            var sb = new StringBuilder();
            var hits = Physics.RaycastAll(new Vector3(xz.x, fromY, xz.z), Vector3.down, 200f);
            sb.Append($"лучей={hits.Length}");
            foreach (var h in hits) sb.Append($" [{h.collider?.name} @{h.point.y:F2}]");

            var box = new Collider[8];
            int n = Physics.OverlapBoxNonAlloc(new Vector3(xz.x, fromY - 5f, xz.z), Vector3.one * 2f, box);
            sb.Append($" | overlap={n}");
            for (int i = 0; i < n; i++) sb.Append($" {box[i]?.name}");
            return sb.ToString();
        }

        public static void Execute()
        {
            var sb = new StringBuilder();
            if (Tool == null) { Debug.Log("[Probe10] StairCollidersTool не найден"); return; }

            string suffix = (string)Tool.GetField("HolderSuffix").GetValue(null);
            Tool.GetField("Thickness").SetValue(null, 0.8f);

            // Синтетический марш: real-координаты, поворот 35° вокруг Y.
            var root = new GameObject("__stair_test");
            root.transform.position = new Vector3(39350f, 2520f, 40010f);
            root.transform.rotation = Quaternion.Euler(0f, 35f, 0f);

            var flight = MakeFlight(root.transform, "__stair_test_flight", 0, Steps - 1);

            // ---------------------------------------------------------- режим 1
            Selection.objects = new UnityEngine.Object[] { flight };
            Call("BuildFlightRamp");

            Transform h1 = FindHolder(flight.transform, suffix);
            int boxes1 = h1 == null ? 0 : h1.GetComponentsInChildren<BoxCollider>(true).Length;
            float maxDev1 = 0f;
            var devs = new List<string>();
            if (boxes1 > 0)
            {
                Physics.SyncTransforms();
                for (int i = 0; i < Steps; i++)
                {
                    // три точки на проступи: начало / центр / конец — чтобы видеть ХУДШЕЕ отклонение пандуса
                    float[] xs = { i * Tread + 0.02f, (i + 0.5f) * Tread, (i + 1) * Tread - 0.02f };
                    foreach (float lx in xs)
                    {
                        Vector3 p = flight.transform.TransformPoint(new Vector3(lx, (i + 1) * Riser, 0f));
                        float hit = RayHitY(new Vector3(p.x, 0f, p.z), p.y + 10f);
                        float dev = Mathf.Abs(hit - p.y);
                        maxDev1 = Mathf.Max(maxDev1, dev);
                    }
                    devs.Add(i.ToString());
                }
            }

            Quaternion q1 = boxes1 > 0 ? h1.GetComponentsInChildren<BoxCollider>(true)[0].transform.rotation : Quaternion.identity;
            Vector3 xAxis = q1 * Vector3.right;
            float angle1 = Mathf.Atan2(Mathf.Abs(xAxis.y), new Vector2(xAxis.x, xAxis.z).magnitude) * Mathf.Rad2Deg;
            Vector3 size1 = boxes1 > 0 ? h1.GetComponentsInChildren<BoxCollider>(true)[0].size : Vector3.zero;

            sb.AppendLine($"[Probe10] РЕЖИМ 1: боксов={boxes1} уклон={angle1:F2}° (ждали {Mathf.Atan(Riser / Tread) * Mathf.Rad2Deg:F2}°) " +
                          $"size=({size1.x:F3}; {size1.y:F3}; {size1.z:F3}) " +
                          $"отклонение лучей от проступи: max={maxDev1:F3} м (допустимо <= {Riser * 0.5f + 0.02f:F3})");

            // идемпотентность
            Call("BuildFlightRamp");
            Transform h1b = FindHolder(flight.transform, suffix);
            int boxes1b = h1b == null ? -1 : h1b.parent.GetComponentsInChildren<BoxCollider>(true).Length;
            sb.AppendLine($"[Probe10] повторный режим 1: холдер тот же={(h1b == h1)} боксов у родителя={boxes1b} (ждали {boxes1})");

            if (float.IsNaN(maxDev1))
            {
                Physics.SyncTransforms();
                BoxCollider firstBox = h1b.GetComponentsInChildren<BoxCollider>(true)[0];
                BoxCollider lastBox = h1b.GetComponentsInChildren<BoxCollider>(true)[0];
                sb.AppendLine($"[Probe10] ДИАГ: bounds[{firstBox.name}]={firstBox.bounds} enabled={firstBox.enabled} active={firstBox.gameObject.activeInHierarchy} layer={firstBox.gameObject.layer}");
                sb.AppendLine($"[Probe10] ДИАГ сверху центра: {Diag(new Vector3(firstBox.bounds.center.x, 0f, firstBox.bounds.center.z), firstBox.bounds.max.y + 5f)}");
            }

            Call("RemoveForSelection");

            // ---------------------------------------------------------- режим 3
            Selection.objects = new UnityEngine.Object[] { flight };
            Call("BuildPerStepBlocks");

            Transform h3 = FindHolder(flight.transform, suffix);
            var bc3 = h3 == null ? new BoxCollider[0] : h3.GetComponentsInChildren<BoxCollider>(true);
            float maxDev3 = 0f, minRun = float.MaxValue, maxRun = 0f, minWide = float.MaxValue, maxWide = 0f;
            if (bc3.Length > 0)
            {
                Physics.SyncTransforms();
                foreach (BoxCollider bc in bc3)
                {
                    float top = bc.transform.position.y + bc.transform.up.y * bc.size.y * 0.5f + bc.center.y;
                    maxRun = Mathf.Max(maxRun, bc.size.x);
                    minRun = Mathf.Min(minRun, bc.size.x);
                    maxWide = Mathf.Max(maxWide, bc.size.z);
                    minWide = Mathf.Min(minWide, bc.size.z);

                    float hit = RayHitY(new Vector3(bc.transform.position.x, 0f, bc.transform.position.z),
                                        bc.transform.position.y + 10f);
                    maxDev3 = Mathf.Max(maxDev3, Mathf.Abs(hit - top));
                }
            }
            sb.AppendLine($"[Probe10] РЕЖИМ 3: боксов={bc3.Length} (ждали {Steps}) " +
                          $"глубина {minRun:F3}..{maxRun:F3} м (ждали {Tread:F2}) " +
                          $"ширина {minWide:F3}..{maxWide:F3} м (ждали {Width:F2}) " +
                          $"max отклонение верха от проступи={maxDev3:F4} м");
            Call("RemoveForSelection");

            // ---------------------------------------------------------- режим 2
            var s1 = MakeFlight(root.transform, "__stair_test_step1", 0, 0);
            var s8 = MakeFlight(root.transform, "__stair_test_step8", Steps - 1, Steps - 1);
            Selection.objects = new UnityEngine.Object[] { s8, s1 };   // порядок намеренно обратный
            Call("BuildBetweenTwoSteps");

            Transform h2 = FindHolder(s1.transform, suffix);
            var bc2 = h2 == null ? new BoxCollider[0] : h2.GetComponentsInChildren<BoxCollider>(true);
            string mode2 = "боксов=0";
            if (bc2.Length > 0)
            {
                Physics.SyncTransforms();
                Vector3 xAxis2 = bc2[0].transform.rotation * Vector3.right;
                float angle2 = Mathf.Atan2(Mathf.Abs(xAxis2.y), new Vector2(xAxis2.x, xAxis2.z).magnitude) * Mathf.Rad2Deg;

                float maxDev2 = 0f;
                for (int i = 0; i < Steps; i++)
                {
                    Vector3 p = flight.transform.TransformPoint(
                        new Vector3((i + 0.5f) * Tread, (i + 1) * Riser, 0f));
                    float hit = RayHitY(new Vector3(p.x, 0f, p.z), p.y + 10f);
                    maxDev2 = Mathf.Max(maxDev2, Mathf.Abs(hit - p.y));
                }

                mode2 = $"боксов={bc2.Length} уклон={angle2:F2}° size=({bc2[0].size.x:F3}; {bc2[0].size.y:F3}; {bc2[0].size.z:F3}) " +
                        $"max отклонение от проступей={maxDev2:F3} м";
            }
            sb.AppendLine($"[Probe10] РЕЖИМ 2: {mode2} (холдер='{(h2 != null ? h2.name : "нет")}')");
            Call("RemoveForSelection");

            // ---------------------------------------------------------- реальный меш из сцены
            var prRoot = GameObject.Find("WorldRoot_0_0/PRIMUM/ADMIN_FOR_COLLIDER");
            string real = "кандидатов не найдено";
            if (prRoot != null)
            {
                var candidates = new List<GameObject>();
                foreach (var tr in prRoot.GetComponentsInChildren<Transform>(true))
                {
                    string n = tr.name.ToUpperInvariant();
                    if (tr.GetComponent<MeshFilter>() == null || tr.GetComponent<MeshFilter>().sharedMesh == null) continue;
                    if (n.Contains("RUG") || n.Contains("CARPET")) continue;
                    if (n.Contains("STAIR") || n.Contains("STEP") || n.Contains("LESTN")) candidates.Add(tr.gameObject);
                    if (candidates.Count >= 12) break;
                }

                real = $"кандидатов={candidates.Count}";
                var names = new StringBuilder();
                foreach (GameObject cand in candidates)
                {
                    names.Append("\n    ").Append(cand.name);
                    Selection.objects = new UnityEngine.Object[] { cand };
                    Call("BuildFlightRamp");
                    Transform hr = FindHolder(cand.transform, suffix);
                    var bcr = hr == null ? new BoxCollider[0] : hr.GetComponentsInChildren<BoxCollider>(true);
                    if (bcr.Length > 0)
                        names.Append($" → режим1: {bcr.Length} бокс size=({bcr[0].size.x:F2}; {bcr[0].size.y:F2}; {bcr[0].size.z:F2})")
                             .Append($" уклон={Mathf.Atan2(Mathf.Abs((bcr[0].transform.rotation * Vector3.right).y), new Vector2((bcr[0].transform.rotation * Vector3.right).x, (bcr[0].transform.rotation * Vector3.right).z).magnitude) * Mathf.Rad2Deg:F1}°");
                    else names.Append(" → режим1: не распознан");
                    Call("RemoveForSelection");

                    Selection.objects = new UnityEngine.Object[] { cand };
                    Call("BuildPerStepBlocks");
                    Transform h3r = FindHolder(cand.transform, suffix);
                    var bc3r = h3r == null ? new BoxCollider[0] : h3r.GetComponentsInChildren<BoxCollider>(true);
                    if (bc3r.Length > 0)
                    {
                        float minH = float.MaxValue, maxH = float.MinValue;
                        foreach (var bc in bc3r)
                        {
                            float top = bc.transform.position.y + bc.transform.up.y * bc.size.y * 0.5f;
                            minH = Mathf.Min(minH, top);
                            maxH = Mathf.Max(maxH, top);
                        }
                        names.Append($" | режим3: {bc3r.Length} блоков, подъём {minH:F2}..{maxH:F2} м");
                    }
                    else names.Append(" | режим3: нет");
                    Call("RemoveForSelection");
                }
                real += names.ToString();
            }
            sb.AppendLine($"[Probe10] реальный меш из сцены: {real}");

            // ---------------------------------------------------------- уборка
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = root.transform.GetChild(i);
                Transform holder = FindHolder(child, suffix);
                if (holder != null) UnityEngine.Object.DestroyImmediate(holder.gameObject);
            }
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log(sb.ToString());
        }
    }
}
