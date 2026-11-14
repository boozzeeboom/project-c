using System.Text;
using ProjectC.World.Generation;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// Self-check дорожки: miter на углах, ширина ленты, замкнутый шов, бордюры, коллайдеры.
    /// Строит сплайны и меши в памяти. Play Mode НЕ нужен и НЕ запускается, сцена не трогается.
    ///
    /// Меню: Tools → ProjectC → World → Spline Road → 0. Self-check RoadPath (лог)
    /// </summary>
    public static class SplineRoadProbe
    {
        [MenuItem("Tools/ProjectC/World/Spline Road/0. Self-check RoadPath (лог)", false, 859)]
        public static void RunMenu() => Debug.Log(RunAll());

        private static RoadPathSettings S(float halfWidth = 2f) => new()
        {
            maxDeviation = 0.01f,
            maxStep = 2f,
            minStep = 0.05f,
            halfWidth = halfWidth,
            miterLimit = 3f,
            sharpAngleDeg = 20f,
            maxFrames = 20000,
            up = Vector3.up
        };

        private static RoadGeometrySettings G(RoadSurface surface = RoadSurface.Plane, float width = 4f)
        {
            RoadGeometrySettings g = RoadGeometrySettings.Default;
            g.surface = surface;
            g.width = width;
            g.roadMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Materials/Cobble/M_PC_Cobble.mat");
            g.curbMaterial = null;
            g.generateColliders = true;
            return g;
        }

        public static string RunAll()
        {
            var sb = new StringBuilder();
            int pass = 0, fail = 0;

            void Check(string name, bool ok, string detail)
            {
                if (ok) pass++;
                else fail++;
                sb.AppendLine($"{(ok ? "PASS" : "FAIL")}  {name}  —  {detail}");
            }

            // ---------- T1: прямая 20 м — одна лента ровно 4 м ----------
            {
                var spline = SplineFactory.CreateLinear(new[]
                {
                    new float3(0, 0, 0), new float3(20, 0, 0)
                });

                var path = RoadPath.Build(spline, S());
                var output = RoadMeshBuilder.Build(path, G());

                float worstWidth = 0f;
                for (int i = 0; i < path.frames.Count; i++)
                    worstWidth = Mathf.Max(worstWidth, Mathf.Abs(
                        Vector3.Distance(path.frames[i].leftEdge, path.frames[i].rightEdge) - 4f));

                Bounds b = output.mesh != null ? output.mesh.bounds : default;

                bool normalsUp = output.mesh != null;
                if (normalsUp)
                {
                    Vector3[] normals = output.mesh.normals;
                    for (int i = 0; i < normals.Length; i++)
                        if (normals[i].y < 0.999f) { normalsUp = false; break; }
                }

                bool ok = path.IsValid
                          && path.frames.Count == 11
                          && path.SegmentCount == 10
                          && worstWidth < 1e-3f
                          && Mathf.Abs(path.Length - 20f) < 0.05f
                          && output.mesh != null && output.mesh.subMeshCount == 1
                          && Mathf.Abs(b.min.x) < 0.01f && Mathf.Abs(b.max.x - 20f) < 0.01f
                          && Mathf.Abs(b.size.z - 4f) < 0.01f
                          && Mathf.Abs(b.size.y) < 1e-4f
                          && normalsUp
                          && output.warnings.Count == 0;

                Check("T1 прямая 20 м", ok,
                    $"кадров {path.frames.Count}, длина {path.Length:F3} м, худшая ширина Δ {worstWidth:F5} м, " +
                    $"submesh {output.mesh?.subMeshCount ?? 0}, нормали вверх={normalsUp}, " +
                    $"bounds min={b.min} max={b.max}");

                output.Release();
            }

            // ---------- T2: 90° угловой — miter, подрезка кадров, выпуклый квад ----------
            {
                var spline = SplineFactory.CreateLinear(new[]
                {
                    new float3(0, 0, 0), new float3(10, 0, 0), new float3(10, 0, 10)
                });

                var path = RoadPath.Build(spline, S());
                var output = RoadMeshBuilder.Build(path, G());

                int corner = IndexOfSharpest(path);
                float cornerDistance = path.frames[corner].distance;
                float k = path.frames[corner].miterScale;
                float turn = path.frames[corner].turnDeg;

                bool neighborsOk = true;
                float minNeighbor = float.MaxValue;
                for (int i = 0; i < path.frames.Count; i++)
                {
                    if (i == corner) continue;
                    float d = Mathf.Abs(path.frames[i].distance - cornerDistance);
                    if (d < 1f) continue;              // кадров ближе метра быть не должно вовсе
                    minNeighbor = Mathf.Min(minNeighbor, d);
                }

                // подрезать должно не меньше hw·k = 2·1.414 = 2.83 м
                neighborsOk = minNeighbor >= 2f * k - 1e-3f;

                bool convex = corner > 0 && corner < path.frames.Count - 1
                              && ConvexQuad(path.frames[corner - 1], path.frames[corner])
                              && ConvexQuad(path.frames[corner], path.frames[corner + 1]);

                float edgeWidth = Vector3.Distance(path.frames[corner].leftEdge, path.frames[corner].rightEdge);

                bool ok = path.frames.Count > 3
                          && Mathf.Abs(turn - 90f) < 1f
                          && Mathf.Abs(k - 1.4142f) < 0.01f
                          && neighborsOk
                          && convex
                          && Mathf.Abs(edgeWidth - 4f * k) < 0.01f
                          && output.colliders.Exists(c => c.name.StartsWith("BC_corner"))
                          && output.warnings.Count == 0;

                Check("T2 90°: miter + подрезка кадров + выпуклый квад", ok,
                    $"угол {turn:F2}°, k={k:F4}, кадров {path.frames.Count}, ближайший сосед {minNeighbor:F3} м " +
                    $"(нужно ≥ {2f * k:F3}), диагональ на угле {edgeWidth:F3} м (ожидали {4f * k:F3}), " +
                    $"квад выпуклый={convex}, угловых плит {(output.colliders.Exists(c => c.name.StartsWith("BC_corner")) ? 1 : 0)}");

                output.Release();
            }

            // ---------- T3: замкнутая окружность R=10 ----------
            {
                var spline = SplineFactory.CreateCircle(10f);
                var path = RoadPath.Build(spline, S());
                var output = RoadMeshBuilder.Build(path, G());

                // обход круга может быть в любую сторону, поэтому inner/outer берём по факту, а не по имени края
                float minRadius = float.MaxValue, maxRadius = 0f;
                bool finite = true;

                for (int i = 0; i < path.frames.Count; i++)
                {
                    RoadFrame f = path.frames[i];

                    float rl = new Vector2(f.leftEdge.x, f.leftEdge.z).magnitude;
                    float rr = new Vector2(f.rightEdge.x, f.rightEdge.z).magnitude;

                    minRadius = Mathf.Min(minRadius, Mathf.Min(rl, rr));
                    maxRadius = Mathf.Max(maxRadius, Mathf.Max(rl, rr));

                    if (!IsFinite(f.leftEdge) || !IsFinite(f.rightEdge)) finite = false;
                }

                float worstInner = Mathf.Abs(minRadius - 8f);
                float worstOuter = Mathf.Abs(maxRadius - 12f);

                Bounds b = output.mesh != null ? output.mesh.bounds : default;

                bool segmentOk = path.SegmentCount == path.frames.Count;
                RoadFrame seam = path.Sample(10f * 2f * Mathf.PI - 1e-4f);
                bool seamOk = IsFinite(seam.position) && IsFinite(seam.leftEdge) && IsFinite(seam.rightEdge);

                bool ok = path.closed && segmentOk && finite && seamOk
                          && Mathf.Abs(worstInner) < 0.05f && Mathf.Abs(worstOuter) < 0.05f
                          && Mathf.Abs(b.size.x - 24f) < 0.3f && Mathf.Abs(b.size.z - 24f) < 0.3f
                          && output.mesh.subMeshCount == 1
                          && output.warnings.Count == 0;

                Check("T3 замкнутый круг R=10: края 8/12 м, шов без NaN", ok,
                    $"closed={path.closed}, кадров {path.frames.Count}, сегментов {path.SegmentCount}, " +
                    $"радиусы краёв {minRadius:F4}…{maxRadius:F4} м (ожидали 8…12), " +
                    $"шов конечен={seamOk}, bounds size={b.size}");

                output.Release();
            }

            // ---------- T4: бордюры — второй сабмеш, верх на curbHeight, ширина не растёт ----------
            {
                var spline = SplineFactory.CreateLinear(new[]
                {
                    new float3(0, 0, 0), new float3(20, 0, 0)
                });

                var path = RoadPath.Build(spline, S());

                var plain = G(RoadSurface.Plane);
                var plainOut = RoadMeshBuilder.Build(path, plain);
                Bounds plainBounds = plainOut.mesh != null ? plainOut.mesh.bounds : default;
                int plainSubmesh = plainOut.mesh != null ? plainOut.mesh.subMeshCount : 0;

                var curbed = G(RoadSurface.PlaneWithCurbs);
                curbed.curbMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Materials/Concrete/M_PC_Concrete.mat");

                var curbedOut = RoadMeshBuilder.Build(path, curbed);
                Bounds curbedBounds = curbedOut.mesh != null ? curbedOut.mesh.bounds : default;
                int curbedSubmesh = curbedOut.mesh != null ? curbedOut.mesh.subMeshCount : 0;

                bool ok = plainSubmesh == 1 && curbedSubmesh == 2
                          && Mathf.Abs(plainBounds.size.y) < 1e-4f
                          && Mathf.Abs(curbedBounds.max.y - 0.15f) < 1e-3f
                          && Mathf.Abs(curbedBounds.size.z - plainBounds.size.z) < 1e-4f
                          && curbedOut.triangles > plainOut.triangles;

                Check("T4 бордюры: 2 сабмеша, верх 0.15 м, ширина та же", ok,
                    $"сабмешей plane {plainSubmesh} / curbs {curbedSubmesh}, " +
                    $"верх plane {plainBounds.max.y:F3} / curbs {curbedBounds.max.y:F3} (ожидали 0.15), " +
                    $"ширина Z {plainBounds.size.z:F3} / {curbedBounds.size.z:F3}, " +
                    $"треугольников {plainOut.triangles} → {curbedOut.triangles}");

                plainOut.Release();
                curbedOut.Release();
            }

            // ---------- T5: коллайдеры — плиты, верх на покрытии, галочка выключает ----------
            {
                var spline = SplineFactory.CreateLinear(new[]
                {
                    new float3(0, 0, 0), new float3(20, 0, 0)
                });

                var path = RoadPath.Build(spline, S());

                var on = G();
                on.generateColliders = true;
                on.colliderThickness = 0.4f;
                on.colliderStep = 2f;
                var onOut = RoadMeshBuilder.Build(path, on);

                float worstTop = 0f, worstWidth = 0f;
                for (int i = 0; i < onOut.colliders.Count; i++)
                {
                    FenceBoxSpec spec = onOut.colliders[i];
                    float top = spec.center.y + spec.size.y * 0.5f;
                    worstTop = Mathf.Max(worstTop, Mathf.Abs(top));
                    worstWidth = Mathf.Max(worstWidth, Mathf.Abs(spec.size.z - 4f));
                }

                var off = G();
                off.generateColliders = false;
                var offOut = RoadMeshBuilder.Build(path, off);

                bool ok = onOut.colliders.Count == 10
                          && worstTop < 1e-4f
                          && worstWidth < 1e-4f
                          && offOut.colliders.Count == 0
                          && offOut.mesh != null;

                Check("T5 коллайдеры: 10 плит на 20 м, верх ровно на покрытии, галочка работает", ok,
                    $"плит включено {onOut.colliders.Count} (ожидали 10), верх худший Δ {worstTop:F5} м, " +
                    $"ширина худшая Δ {worstWidth:F5} м, выключено → {offOut.colliders.Count}");

                onOut.Release();
                offOut.Release();
            }

            // ---------- T6: широкая дорожка 8 м на том же 90° — подрезка сильнее, квад держится ----------
            {
                var spline = SplineFactory.CreateLinear(new[]
                {
                    new float3(0, 0, 0), new float3(10, 0, 0), new float3(10, 0, 10)
                });

                var narrow = RoadPath.Build(spline, S(2f));
                var wide = RoadPath.Build(spline, S(4f));

                int wi = IndexOfSharpest(wide);
                bool convex = wi > 0 && wi < wide.frames.Count - 1
                              && ConvexQuad(wide.frames[wi - 1], wide.frames[wi])
                              && ConvexQuad(wide.frames[wi], wide.frames[wi + 1]);

                bool ok = wide.frames.Count < narrow.frames.Count && convex && wide.IsValid;

                Check("T6 ширина 8 м: кадры под углом режутся сильнее, квад остаётся выпуклым", ok,
                    $"кадров при 4 м: {narrow.frames.Count}, при 8 м: {wide.frames.Count}, " +
                    $"k={wide.frames[wi].miterScale:F3}, выпуклый={convex}");
            }

            // ---------- T7: тесная дуга — warning «радиус < полуширины» ----------
            {
                var spline = SplineFactory.CreateCircle(3f);
                var path = RoadPath.Build(spline, S(4f));

                bool warned = false;
                for (int i = 0; i < path.warnings.Count; i++)
                    if (path.warnings[i].Contains("Радиус")) warned = true;

                Check("T7 R=3 при полуширине 4: предупреждение о вывернутом крае", warned,
                    warned ? path.warnings[0] : "предупреждения нет");
            }

            // ---------- T8: вырожденный сплайн ----------
            {
                var spline = SplineFactory.CreateLinear(new[]
                {
                    new float3(0, 0, 0), new float3(0, 0, 0)
                });

                var path = RoadPath.Build(spline, S());
                var output = RoadMeshBuilder.Build(path, G());

                Check("T8 вырожденный сплайн: warning, меша нет",
                    !path.IsValid && path.warnings.Count > 0 && output.mesh == null,
                    $"IsValid={path.IsValid}, warnings: {(path.warnings.Count > 0 ? path.warnings[0] : "—")}");

                output.Release();
            }

            sb.AppendLine();
            sb.AppendLine($"ИТОГО: PASS {pass}, FAIL {fail}");
            return sb.ToString();
        }

        // ============================================================
        // Помощники
        // ============================================================

        private static int IndexOfSharpest(RoadPath path)
        {
            int best = 0;
            float bestTurn = -1f;

            for (int i = 0; i < path.frames.Count; i++)
            {
                if (path.frames[i].turnDeg <= bestTurn) continue;
                bestTurn = path.frames[i].turnDeg;
                best = i;
            }

            return best;
        }

        /// <summary>Квад ленты (a.left, b.left, b.right, a.right) должен быть выпуклым — иначе он «бабочкой».</summary>
        private static bool ConvexQuad(RoadFrame a, RoadFrame b)
        {
            Vector3 p0 = a.leftEdge;
            Vector3 p1 = b.leftEdge;
            Vector3 p2 = b.rightEdge;
            Vector3 p3 = a.rightEdge;

            float s0 = Orientation(p0, p1, p2);
            float s1 = Orientation(p1, p2, p3);
            float s2 = Orientation(p2, p3, p0);
            float s3 = Orientation(p3, p0, p1);

            bool positive = s0 > 1e-6f && s1 > 1e-6f && s2 > 1e-6f && s3 > 1e-6f;
            bool negative = s0 < -1e-6f && s1 < -1e-6f && s2 < -1e-6f && s3 < -1e-6f;

            return positive || negative;
        }

        private static float Orientation(Vector3 a, Vector3 b, Vector3 c)
        {
            return Vector3.Cross(b - a, c - b).y;
        }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
                   !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }
    }
}
