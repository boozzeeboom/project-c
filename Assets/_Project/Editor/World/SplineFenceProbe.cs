using System.Text;
using ProjectC.World.Generation;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// Self-check FencePath: прогоны, метрика излома, miter-подрезка, тесселяция по прогибу.
    /// Строит сплайны в памяти и проверяет числа. Play Mode НЕ нужен и НЕ запускается.
    /// Шаг 1 реализации по docs/world/spline_assets/00_DESIGN_SplineFence.md §16.
    ///
    /// Меню: Tools → ProjectC → World → Spline Fence → 0. Self-check FencePath (лог)
    /// </summary>
    public static class SplineFenceProbe
    {
        private const float PostSize = 0.06f;      // h = 0.03 — как у Bar-столба
        private const float PostSizeBig = 0.38f;   // для таблицы §5.3

        [MenuItem("Tools/ProjectC/World/Spline Fence/0. Self-check FencePath (лог)", false, 859)]
        public static void RunMenu() => Debug.Log(RunAll());

        /// <summary>Прогон всех проверок. Возвращает отчёт (PASS/FAIL по каждому пункту).</summary>
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

            FencePathSettings S() => new()
            {
                maxDeviation = 0.01f,
                sharpAngleDeg = 25f,
                smoothAngleDeg = 2f,
                breakAngleDeg = 150f,
                bendProbeMeters = 0.05f,
                maxStep = 2.5f,
                minStep = 0.05f,
                postHalfSize = PostSize * 0.5f,
                miterGap = 0.002f,
                minRadius = 0f
            };

            // ---------- T1: прямая ----------
            {
                var spline = SplineFactory.CreateLinear(new[]
                {
                    new float3(0, 0, 0), new float3(10, 0, 0)
                });
                var path = FencePath.Build(spline, S());

                float maxStepSeen = 0f, minStepSeen = float.MaxValue;
                var straightRun = path.runs[0];
                for (int i = 1; i < straightRun.FrameCount; i++)
                {
                    float step = straightRun.frames[i].distance - straightRun.frames[i - 1].distance;
                    maxStepSeen = Mathf.Max(maxStepSeen, step);
                    minStepSeen = Mathf.Min(minStepSeen, step);
                }

                // на прямой шаг упирается в maxStep (= шаг столбов), а не в прогиб
                bool ok = path.runs.Count == 1
                          && Mathf.Abs(straightRun.Length - 10f) < 0.05f
                          && straightRun.FrameCount == 5
                          && maxStepSeen <= 2.5f + 1e-3f
                          && minStepSeen >= 2.5f - 1e-3f
                          && straightRun.frames.TrueForAll(f => f.forward.x > 0.999f)
                          && straightRun.frames.TrueForAll(f => f.up == Vector3.up);

                Check("T1 прямая 10 м", ok,
                    $"прогонов {path.runs.Count}, длина {straightRun.Length:F3} м, кадров {straightRun.FrameCount}, " +
                    $"шаг {minStepSeen:F2}…{maxStepSeen:F2} м (ожидали 2.5 = maxStep)");

                var mid = path.runs[0].Sample(5f);
                Check("T1 Sample(5)", Mathf.Abs(mid.position.x - 5f) < 1e-3f && Mathf.Abs(mid.distance - 5f) < 1e-4f,
                    $"position={mid.position}, forward={mid.forward}");
            }

            // ---------- T2: 90° ----------
            {
                var spline = SplineFactory.CreateLinear(new[]
                {
                    new float3(0, 0, 0), new float3(10, 0, 0), new float3(10, 0, 10)
                });
                var path = FencePath.Build(spline, S());

                FenceCorner corner = path.corners.Count > 0 ? path.corners[0] : null;
                float expectedTrim = FencePath.MiterTrim(PostSize, 90f) + 0.002f;

                bool ok = path.corners.Count == 1
                          && corner != null
                          && corner.kind == FenceJointKind.Sharp
                          && Mathf.Abs(corner.bendAngleDeg - 90f) < 2f
                          && Mathf.Abs(corner.cornerAngleDeg - 90f) < 2f
                          && Mathf.Abs(corner.trim - expectedTrim) < 1e-3f
                          && path.runs.Count == 2;

                Check("T2 90°: классификация", ok,
                    corner == null
                        ? "угол не найден"
                        : $"bend={corner.bendAngleDeg:F2}°, corner={corner.cornerAngleDeg:F2}°, kind={corner.kind}, " +
                          $"trim={corner.trim:F4} м (ожидали {expectedTrim:F4})");

                if (path.runs.Count == 2)
                {
                    bool runsOk = Mathf.Abs(path.runs[0].Length - (10f - expectedTrim)) < 0.05f
                                  && Mathf.Abs(path.runs[1].Length - (10f - expectedTrim)) < 0.05f
                                  && path.runs[0].End.forward.x > 0.99f
                                  && path.runs[1].Start.forward.z > 0.99f;

                    Check("T2 90°: прогоны и направления", runsOk,
                        $"run0={path.runs[0].Length:F3} м (end.fwd={path.runs[0].End.forward}), " +
                        $"run1={path.runs[1].Length:F3} м (start.fwd={path.runs[1].Start.forward})");
                }
                else
                {
                    Check("T2 90°: прогоны и направления", false, $"прогонов {path.runs.Count}, ожидали 2");
                }
            }

            // ---------- T3: окружность R = 10 (замкнутый, без углов) ----------
            {
                var spline = SplineFactory.CreateCircle(10f);
                var path = FencePath.Build(spline, S());

                // шаг при R=10 и прогибе 1 см: sqrt(8*0.01*10) = 0.894 м
                float maxStepSeen = 0f, minStepSeen = float.MaxValue;
                var run = path.runs.Count > 0 ? path.runs[0] : null;
                if (run != null)
                {
                    for (int i = 1; i < run.FrameCount - 1; i++)
                    {
                        float step = run.frames[i].distance - run.frames[i - 1].distance;
                        maxStepSeen = Mathf.Max(maxStepSeen, step);
                        minStepSeen = Mathf.Min(minStepSeen, step);
                    }
                }

                bool noSharp = true;
                for (int i = 0; i < path.corners.Count; i++)
                    if (path.corners[i].BreaksPath) noSharp = false;

                float expected = 2f * Mathf.PI * 10f;

                bool ok = spline.Closed
                          && path.runs.Count == 1
                          && run != null && run.closedLoop
                          && noSharp
                          && Mathf.Abs(run.Length - expected) < 0.3f
                          && maxStepSeen < 0.95f && minStepSeen > 0.7f
                          && run.frames.TrueForAll(f => Mathf.Abs(f.forward.y) < 1e-4f);

                Check("T3 окружность R=10, замкнутая", ok,
                    $"прогонов {path.runs.Count}, длина {(run != null ? run.Length : 0f):F2} м (ожидали {expected:F2}), " +
                    $"шаг {minStepSeen:F3}…{maxStepSeen:F3} м (ожидали ~0.894), углов-разрывов {(noSharp ? "нет" : "есть")}");
            }

            // ---------- T4: тесная окружность R = 2 ----------
            {
                var spline = SplineFactory.CreateCircle(2f);
                var path = FencePath.Build(spline, S());

                var run = path.runs.Count > 0 ? path.runs[0] : null;
                float maxStepSeen = 0f, minStepSeen = float.MaxValue;
                if (run != null)
                {
                    for (int i = 1; i < run.FrameCount - 1; i++)
                    {
                        float step = run.frames[i].distance - run.frames[i - 1].distance;
                        maxStepSeen = Mathf.Max(maxStepSeen, step);
                        minStepSeen = Mathf.Min(minStepSeen, step);
                    }
                }

                // sqrt(8*0.01*2) = 0.4 м
                bool ok = run != null && maxStepSeen < 0.45f && minStepSeen > 0.3f;
                Check("T4 окружность R=2: шаг адаптируется", ok,
                    $"шаг {minStepSeen:F3}…{maxStepSeen:F3} м (ожидали ~0.400), кадров {(run != null ? run.FrameCount : 0)}");
            }

            // ---------- T5: шпилька ----------
            {
                var spline = SplineFactory.CreateLinear(new[]
                {
                    new float3(0, 0, 0), new float3(5, 0, 0), new float3(0.1f, 0, 0)
                });
                var path = FencePath.Build(spline, S());

                FenceCorner corner = path.corners.Count > 0 ? path.corners[0] : null;
                bool ok = corner != null
                          && corner.kind == FenceJointKind.Reversed
                          && corner.bendAngleDeg > 150f
                          && corner.trim == 0f
                          && path.runs.Count == 2
                          && path.warnings.Count > 0;

                Check("T5 шпилька: разрыв без столба", ok,
                    corner == null
                        ? "угол не найден"
                        : $"bend={corner.bendAngleDeg:F1}°, kind={corner.kind}, trim={corner.trim:F3}, " +
                          $"прогонов {path.runs.Count}, предупреждений {path.warnings.Count}");
            }

            // ---------- T6: плавный 90° (AutoSmooth) не должен рвать путь ----------
            {
                var spline = new Spline(new[]
                {
                    new float3(0, 0, 0), new float3(10, 0, 0), new float3(10, 0, 10)
                }, TangentMode.AutoSmooth);

                var path = FencePath.Build(spline, S());

                FenceCorner corner = path.corners.Count > 0 ? path.corners[0] : null;
                bool ok = path.runs.Count == 1
                          && corner != null
                          && !corner.BreaksPath;

                Check("T6 плавный 90° (AutoSmooth) — путь не рвётся", ok,
                    corner == null
                        ? "угол не найден"
                        : $"bend={corner.bendAngleDeg:F2}°, kind={corner.kind}, прогонов {path.runs.Count}");
            }

            // ---------- T7: таблица miter (§5.3) ----------
            {
                float t180 = FencePath.MiterTrim(PostSizeBig, 180f);
                float t90 = FencePath.MiterTrim(PostSizeBig, 90f);
                float t60 = FencePath.MiterTrim(PostSizeBig, 60f);
                float t30 = FencePath.MiterTrim(PostSizeBig, 30f);

                bool ok = Mathf.Abs(t180 - 0.190f) < 5e-4f
                          && Mathf.Abs(t90 - 0.269f) < 5e-4f
                          && Mathf.Abs(t60 - 0.260f) < 5e-4f
                          && Mathf.Abs(t30 - 0.233f) < 5e-4f;

                Check("T7 miter-подрезка, P=0.38", ok,
                    $"180°→{t180:F4}, 90°→{t90:F4}, 60°→{t60:F4}, 30°→{t30:F4} м");
            }

            // ---------- T8: наклонный сплайн — кадры остаются вертикальными ----------
            {
                var spline = SplineFactory.CreateLinear(new[]
                {
                    new float3(0, 0, 0), new float3(10, 2, 0), new float3(20, 2, 10)
                });
                var path = FencePath.Build(spline, S());

                bool vertical = true, followsHeight = false;
                for (int r = 0; r < path.runs.Count; r++)
                    for (int i = 0; i < path.runs[r].FrameCount; i++)
                    {
                        var f = path.runs[r].frames[i];
                        if (Mathf.Abs(f.forward.y) > 1e-4f || f.up != Vector3.up) vertical = false;
                        if (f.position.y > 1.5f) followsHeight = true;
                    }

                Check("T8 наклонный сплайн: кадры вертикальны, высота следует", vertical && followsHeight,
                    $"вертикальность={vertical}, высота взята со сплайна={followsHeight}, прогонов {path.runs.Count}");
            }

            // ---------- T9: вырожденный сплайн ----------
            {
                var spline = SplineFactory.CreateLinear(new[]
                {
                    new float3(0, 0, 0), new float3(0, 0, 0)
                });
                var path = FencePath.Build(spline, S());

                Check("T9 вырожденный сплайн даёт warning", path.runs.Count == 0 && path.warnings.Count > 0,
                    $"прогонов {path.runs.Count}, warnings: {(path.warnings.Count > 0 ? path.warnings[0] : "—")}");
            }

            // ---------- T10: проверка минимального радиуса ----------
            {
                var settings = S();
                settings.minRadius = 5f;   // окружность R=2 должна ругнуться

                var spline = SplineFactory.CreateCircle(2f);
                var path = FencePath.Build(spline, settings);

                bool warned = false;
                for (int i = 0; i < path.warnings.Count; i++)
                    if (path.warnings[i].Contains("радиус")) warned = true;

                Check("T10 minRadius: предупреждение о тесном радиусе", warned,
                    warned ? path.warnings[0] : "предупреждения нет");
            }

            // ---------- T11: Bar, прямая 10 м — меш, габарит, коллайдеры ----------
            {
                var type = ScriptableObject.CreateInstance<FenceTypeDefinition>();
                var spline = SplineFactory.CreateLinear(new[]
                {
                    new float3(0, 0, 0), new float3(10, 0, 0)
                });

                var path = FencePath.Build(spline, type.ToPathSettings());
                var output = FenceStyleBuilder.Build(path, type);

                var mesh = output.sections.Count > 0 ? output.sections[0].mesh : null;
                Bounds b = mesh != null ? mesh.bounds : default;
                int tris = output.TotalTriangles;
                int verts = mesh != null ? mesh.vertexCount : 0;

                bool ok = output.sections.Count == 1
                          && mesh != null && mesh.subMeshCount == 1
                          && tris > 800 && tris < 1600
                          && verts > 0 && verts % 4 == 0
                          && Mathf.Abs(b.min.x + 0.03f) < 0.02f
                          && Mathf.Abs(b.max.x - 10.03f) < 0.02f
                          && Mathf.Abs(b.min.y) < 0.02f
                          && Mathf.Abs(b.max.y - type.postHeight) < 0.02f
                          && Mathf.Abs(b.size.z - type.thickness) < 0.03f
                          && output.colliders.Count == 9;

                Check("T11 Bar прямая 10 м", ok,
                    $"секций {output.sections.Count}, сабмешей {(mesh != null ? mesh.subMeshCount : 0)}, " +
                    $"треугольников {tris}, вершин {verts}, bounds min={b.min} max={b.max}, " +
                    $"коллайдеров {output.colliders.Count} (ожидали 9 = 5 столбов + 4 панели)");

                output.ReleaseMeshes();
                Object.DestroyImmediate(type);
            }

            // ---------- T12: Bar, 90° — разрыв, угловой столб, miter-торец ----------
            {
                var type = ScriptableObject.CreateInstance<FenceTypeDefinition>();
                var spline = SplineFactory.CreateLinear(new[]
                {
                    new float3(0, 0, 0), new float3(10, 0, 0), new float3(10, 0, 10)
                });

                var path = FencePath.Build(spline, type.ToPathSettings());
                var output = FenceStyleBuilder.Build(path, type);

                Vector3 cornerPos = path.corners.Count > 0 ? path.corners[0].position : Vector3.zero;

                bool cornerCollider = false;
                for (int i = 0; i < output.colliders.Count; i++)
                {
                    Vector3 c = output.colliders[i].center;
                    if (Mathf.Abs(c.x - cornerPos.x) < 0.03f && Mathf.Abs(c.z - cornerPos.z) < 0.03f)
                        cornerCollider = true;
                }

                // прогон вдоль +X не должен вылезать за узел: торец подрезан miter'ом
                bool noOverlap = output.sections.Count > 0 && output.sections[0].mesh.bounds.max.x < 10f - 1e-3f;

                bool ok = output.sections.Count == 2 && output.colliders.Count == 19 && cornerCollider && noOverlap;

                Check("T12 Bar 90°: разрыв + угловой столб", ok,
                    $"секций {output.sections.Count}, коллайдеров {output.colliders.Count} (ожидали 19), " +
                    $"столб в узле={cornerCollider}, торец подрезан={noOverlap}");

                output.ReleaseMeshes();
                Object.DestroyImmediate(type);
            }

            // ---------- T13: жёсткие нормали, UV в метрах, без NaN ----------
            {
                var type = ScriptableObject.CreateInstance<FenceTypeDefinition>();
                var spline = SplineFactory.CreateLinear(new[]
                {
                    new float3(0, 0, 0), new float3(10, 0, 0)
                });

                var path = FencePath.Build(spline, type.ToPathSettings());
                var output = FenceStyleBuilder.Build(path, type);

                bool quadOk = false, uvOk = false, normOk = false, finiteOk = false;
                float maxU = 0f, maxV = 0f;

                if (output.sections.Count > 0)
                {
                    var mesh = output.sections[0].mesh;
                    var uv = mesh.uv;
                    var normals = mesh.normals;
                    var positions = mesh.vertices;

                    int tris = 0;
                    for (int s = 0; s < mesh.subMeshCount; s++) tris += (int)(mesh.GetIndexCount(s) / 3);

                    // жёсткие кромки: 4 вершины на квад, никакой склейки
                    quadOk = positions.Length % 4 == 0 && tris * 2 == positions.Length;
                    normOk = normals != null && normals.Length == positions.Length;
                    uvOk = uv != null && uv.Length == positions.Length;

                    finiteOk = true;
                    for (int i = 0; i < positions.Length; i++)
                        if (float.IsNaN(positions[i].x) || float.IsNaN(positions[i].y) || float.IsNaN(positions[i].z))
                            finiteOk = false;

                    for (int i = 0; i < uv.Length; i++)
                    {
                        maxU = Mathf.Max(maxU, uv[i].x);
                        maxV = Mathf.Max(maxV, uv[i].y);
                    }
                    output.ReleaseMeshes();
                }

                // UV в МЕТРАХ, а не 0..1: балка прямой = 2.5 м по U, боковая грань столба = 1.95 м по V.
                // У горизонтальных граней (верх/низ) V приходится на длину — это нормально: кладка
                // ориентируется по боковым граням (±Z), где U = длина, V = высота.
                bool uInMeters = maxU > 1.5f && maxU < 12f;
                bool vInMeters = maxV > 1.5f && maxV < 12f;

                Check("T13 жёсткие нормали + UV в метрах",
                    quadOk && uvOk && normOk && finiteOk && uInMeters && vInMeters,
                    $"4 верт./квад={quadOk}, normals={normOk}, uv={uvOk}, без NaN={finiteOk}, " +
                    $"maxU={maxU:F2} м, maxV={maxV:F2} м (метричные, не 0..1)");

                Object.DestroyImmediate(type);
            }

            // ---------- T14: замкнутый круг — столб в шве не дублируется ----------
            {
                var type = ScriptableObject.CreateInstance<FenceTypeDefinition>();
                var spline = SplineFactory.CreateCircle(10f);

                var path = FencePath.Build(spline, type.ToPathSettings());
                var output = FenceStyleBuilder.Build(path, type);

                Vector3 seam = path.runs.Count > 0 ? path.runs[0].Start.position : Vector3.zero;

                int atSeam = 0;
                for (int i = 0; i < output.colliders.Count; i++)
                {
                    Vector3 c = output.colliders[i].center;
                    float dx = c.x - seam.x, dz = c.z - seam.z;
                    if (Mathf.Sqrt(dx * dx + dz * dz) < 0.02f) atSeam++;
                }

                Check("T14 Bar замкнутый круг R=10: один столб в шве",
                    output.sections.Count == 1 && atSeam == 1,
                    $"секций {output.sections.Count}, столбов в шве {atSeam} (ожидали 1), " +
                    $"всего коллайдеров {output.colliders.Count}");

                output.ReleaseMeshes();
                Object.DestroyImmediate(type);
            }

            // ---------- T15: коллайдер панели — высота и толщина ----------
            {
                var type = ScriptableObject.CreateInstance<FenceTypeDefinition>();
                var spline = SplineFactory.CreateLinear(new[]
                {
                    new float3(0, 0, 0), new float3(10, 0, 0)
                });

                var path = FencePath.Build(spline, type.ToPathSettings());
                var output = FenceStyleBuilder.Build(path, type);

                int panels = 0;
                bool sizeOk = true;
                float minThickness = float.MaxValue;

                for (int i = 0; i < output.colliders.Count; i++)
                {
                    FenceBoxSpec spec = output.colliders[i];
                    if (spec.name == null || !spec.name.StartsWith("BC_panel")) continue;

                    panels++;
                    minThickness = Mathf.Min(minThickness, spec.size.z);

                    if (Mathf.Abs(spec.size.y - type.colliderHeight) > 1e-3f) sizeOk = false;
                    if (spec.size.z < type.barSize * 2f - 1e-4f) sizeOk = false;
                }

                Check("T15 коллайдер панели: высота и толщина",
                    panels == 4 && sizeOk,
                    $"панелей {panels}, толщина {minThickness:F3} м ≥ 2·barSize ({type.barSize * 2f:F3}), " +
                    $"высота {type.colliderHeight:F2} м");

                output.ReleaseMeshes();
                Object.DestroyImmediate(type);
            }

            // ---------- T16: Masonry, прямая 10 м — панели, шапка, привязка к модулю ----------
            {
                var type = MakeMasonry();

                var spline = SplineFactory.CreateLinear(new[]
                {
                    new float3(0, 0, 0), new float3(10, 0, 0)
                });

                var path = FencePath.Build(spline, type.ToPathSettings());
                var panels = MasonryFenceStyle.Layout(path.runs[0], type, null);
                var output = FenceStyleBuilder.Build(path, type);

                float cell = MasonryFenceStyle.BrickCell(type);
                bool snapped = true;
                float worstRemainder = 0f;

                for (int i = 0; i < panels.Count; i++)
                {
                    float cells = panels[i].wallLength / cell;
                    if (Mathf.Abs(cells - Mathf.Round(cells)) > 1e-3f) snapped = false;
                    worstRemainder = Mathf.Max(worstRemainder, panels[i].span - panels[i].wallLength);
                }

                var mesh = output.sections.Count > 0 ? output.sections[0].mesh : null;
                Bounds b = mesh != null ? mesh.bounds : default;

                bool ok = panels.Count == 4
                          && output.sections.Count == 1
                          && mesh != null && mesh.subMeshCount == 2
                          && output.colliders.Count == 9
                          && snapped
                          && worstRemainder < type.postSize * 0.5f
                          && Mathf.Abs(b.min.y) < 0.02f
                          && Mathf.Abs(b.max.y - (type.postHeight + type.capHeight)) < 0.02f
                          && Mathf.Abs(b.size.z - type.capSize) < 0.02f
                          && Mathf.Abs(b.min.x + type.capSize * 0.5f) < 0.02f
                          && Mathf.Abs(b.max.x - (10f + type.capSize * 0.5f)) < 0.02f;

                Check("T16 Masonry прямая 10 м: панели, шапка, модуль кладки", ok,
                    $"панелей {panels.Count}, секций {output.sections.Count}, " +
                    $"подмешей {(mesh != null ? mesh.subMeshCount : 0)}, коллайдеров {output.colliders.Count} " +
                    $"(ожидали 9 = 5 столбов + 4 стены), модуль {cell:F4} м, кратно модулю={snapped}, " +
                    $"макс. остаток {worstRemainder:F3} м, bounds min={b.min} max={b.max}");

                output.ReleaseMeshes();
                Object.DestroyImmediate(type);
            }

            // ---------- T17: Masonry, дуга R=10 — панель всегда хорда, оси секций по стене ----------
            {
                var type = MakeMasonry();
                var spline = SplineFactory.CreateCircle(10f);

                var path = FencePath.Build(spline, type.ToPathSettings());
                var run = path.runs[0];
                var pillars = MasonryFenceStyle.PillarDistances(run, type, null);
                var output = FenceStyleBuilder.Build(path, type);

                float worst = 0f;
                for (int i = 0; i + 1 < pillars.Count; i++)
                    worst = Mathf.Max(worst, Sagitta(run, pillars[i], pillars[i + 1]));

                bool axesOk = true;
                for (int i = 0; i < output.sections.Count; i++)
                {
                    Vector3 x = output.sections[i].localRotation * Vector3.right;
                    Vector3 z = output.sections[i].localRotation * Vector3.forward;
                    if (Mathf.Abs(x.y) > 1e-4f || Mathf.Abs(z.y) > 1e-4f) axesOk = false;
                }

                bool ok = worst <= type.panelChordError + 1e-3f && output.sections.Count > 1 && axesOk;

                Check("T17 Masonry дуга R=10: панель = хорда, оси секций по стене", ok,
                    $"панелей {pillars.Count - 1}, секций {output.sections.Count}, " +
                    $"худшая стрелка {worst:F4} м (допуск {type.panelChordError:F3}), " +
                    $"оси секций горизонтальны={axesOk}");

                output.ReleaseMeshes();
                Object.DestroyImmediate(type);
            }

            // ---------- T18: Masonry, 90° — угловой столб, без дубля на торце ----------
            {
                var type = MakeMasonry();
                var spline = SplineFactory.CreateLinear(new[]
                {
                    new float3(0, 0, 0), new float3(10, 0, 0), new float3(10, 0, 10)
                });

                var path = FencePath.Build(spline, type.ToPathSettings());
                var output = FenceStyleBuilder.Build(path, type);

                Vector3 cornerPos = path.corners.Count > 0 ? path.corners[0].position : Vector3.zero;

                var cornerPillars = 0;
                for (int i = 0; i < output.colliders.Count; i++)
                {
                    Vector3 c = output.colliders[i].center;
                    float dx = c.x - cornerPos.x, dz = c.z - cornerPos.z;
                    if (Mathf.Sqrt(dx * dx + dz * dz) < 0.03f) cornerPillars++;
                }

                // прогон вдоль +X не должен вылезать за узел: торец подрезан miter'ом и своего столба не ставит
                bool trimmed = output.sections.Count > 0 && output.sections[0].mesh.bounds.max.x < 10f - 1e-3f;

                bool ok = output.sections.Count == 2 && output.colliders.Count == 17 &&
                          cornerPillars == 1 && trimmed;

                Check("T18 Masonry 90°: один столб в узле, торец подрезан", ok,
                    $"секций {output.sections.Count}, коллайдеров {output.colliders.Count} (ожидали 17 = " +
                    $"2×(4 столба + 4 стены) + угловой), столбов в узле {cornerPillars}, торец подрезан={trimmed}");

                output.ReleaseMeshes();
                Object.DestroyImmediate(type);
            }

            // ---------- T19: Masonry, замкнутый круг — столб в шве не дублируется ----------
            {
                var type = MakeMasonry();
                var spline = SplineFactory.CreateCircle(10f);

                var path = FencePath.Build(spline, type.ToPathSettings());
                var output = FenceStyleBuilder.Build(path, type);

                Vector3 seam = path.runs.Count > 0 ? path.runs[0].Start.position : Vector3.zero;

                int atSeam = 0;
                for (int i = 0; i < output.colliders.Count; i++)
                {
                    Vector3 c = output.colliders[i].center;
                    float dx = c.x - seam.x, dz = c.z - seam.z;
                    if (Mathf.Sqrt(dx * dx + dz * dz) < 0.02f) atSeam++;
                }

                Check("T19 Masonry замкнутый круг R=10: один столб в шве",
                    atSeam == 1 && output.sections.Count > 1,
                    $"секций {output.sections.Count}, столбов в шве {atSeam} (ожидали 1), " +
                    $"всего коллайдеров {output.colliders.Count}");

                output.ReleaseMeshes();
                Object.DestroyImmediate(type);
            }

            // ---------- T20: Masonry — шаг столбов адаптируется к допуску хорды ----------
            {
                var loose = MakeMasonry();
                loose.panelChordError = 0.02f;

                var strict = MakeMasonry();
                strict.panelChordError = 0.005f;

                var spline = SplineFactory.CreateCircle(6f);

                var looseRun = FencePath.Build(spline, loose.ToPathSettings()).runs[0];
                var strictRun = FencePath.Build(spline, strict.ToPathSettings()).runs[0];

                int loosePillars = MasonryFenceStyle.PillarDistances(looseRun, loose, null).Count;
                int strictPillars = MasonryFenceStyle.PillarDistances(strictRun, strict, null).Count;

                Check("T20 Masonry круг R=6: строже допуск — больше столбов",
                    strictPillars > loosePillars && loosePillars > 1,
                    $"столбов при 2 см: {loosePillars}, при 0.5 см: {strictPillars} (шаг диктует геометрия дуги)");

                Object.DestroyImmediate(loose);
                Object.DestroyImmediate(strict);
            }

            sb.AppendLine();
            sb.AppendLine($"ИТОГО: PASS {pass}, FAIL {fail}");
            return sb.ToString();
        }

        /// <summary>Тип кирпичного забора с параметрами пилота (§7) — без ассета, для чисел.</summary>
        private static FenceTypeDefinition MakeMasonry()
        {
            var t = ScriptableObject.CreateInstance<FenceTypeDefinition>();

            t.style = FenceStyle.Masonry;
            t.height = 1.35f;
            t.thickness = 0.25f;
            t.colliderHeight = 1.35f;
            t.postSize = 0.38f;
            t.postHeight = 1.55f;
            t.postSpacing = 2.6f;

            // материалы как в FT_Fence_Brick_Block: полотно — кладка, столбы/шапка — бетон
            t.primaryMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Materials/Brick/M_PC_Brick.mat");
            t.secondaryMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Materials/Concrete/M_PC_Concrete.mat");

            return t;
        }

        /// <summary>Стрелка прогиба хорды панели между двумя погонными метрами прогона.</summary>
        private static float Sagitta(FenceRun run, float from, float to)
        {
            Vector3 a = run.Sample(from).position;
            Vector3 b = run.Sample(to).position;
            a.y = 0f;
            b.y = 0f;

            Vector3 dir = b - a;
            float length = dir.magnitude;
            if (length < 1e-4f) return 0f;
            dir /= length;

            float worst = 0f;
            for (int i = 1; i < 32; i++)
            {
                Vector3 p = run.Sample(from + (to - from) * i / 32f).position;
                p.y = 0f;

                Vector3 rel = p - a;
                worst = Mathf.Max(worst, (rel - dir * Vector3.Dot(rel, dir)).magnitude);
            }

            return worst;
        }
    }
}
