using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.World.Generation
{
    /// <summary>
    /// Кирпичный забор: стена-призма + столбы-простенки + шапка + оголовки.
    ///
    /// Кирпич — это МАТЕРИАЛ, а не геометрия (§7.1): кладку рисует M_PC_Brick в object space,
    /// поэтому настоящие блоки не строим (иначе ~75 тыс. трис на 100 м забора).
    /// Отсюда три обязательных следствия, которые и реализованы здесь:
    ///
    ///   1. ПОЛОТНО ВСЕГДА ПРЯМАЯ ХОРДА между столбами (§5.4). Если стрелка хорды больше
    ///      panelChordError — панель укорачивается и ставится лишний столб. Это адаптивность
    ///      к дуге без всякого флага: длину панели диктует геометрия пути.
    ///   2. ДЛИНА ПОЛОТНА КРАТНА brickCell = 1/brickScale (§7.3): остаток (< половины столба)
    ///      прячется внутри столба, поэтому швы кладки не съезжают относительно столбов.
    ///   3. СЕКЦИЯ = ЦЕПОЧКА ПОЧТИ СОНАПРАВЛЕННЫХ ПАНЕЛЕЙ (§7.2). Локальная X секции идёт вдоль
    ///      стены, значит ряды кладки параллельны стене. Панели с расхождением больше
    ///      sectionAlignDeg уходят в следующую секцию.
    ///
    /// Меш секции строится в ЛОКАЛЬНЫХ координатах СЕКЦИИ, а секция ставится в корень забора
    /// через FenceSectionMesh.localPosition/localRotation. Поэтому кладка выровнена по стене,
    /// даже если сам корень повёрнут: object space для шейдера — это секция, а не корень.
    ///
    /// Углы: прогон режется miter'ом (§5.3) штатно в FencePath, а столб в узле ставится один —
    /// угловой (по биссектрисе). Торец прогона, примыкающий к такому углу, своего столба не получает
    /// (его роль играет угловой), иначе на 90° в узле стояли бы две коробки 0.38 почти друг в друге.
    /// </summary>
    public sealed class MasonryFenceStyle : IFenceStyle
    {
        private const int MaxPanelsPerRun = 4000;
        private const float MinPanelLength = 0.5f;

        /// <summary>Одна панель: столб слева + прямая стена между столбами. Выдаётся Layout().</summary>
        public struct MasonryPanel
        {
            /// <summary>Центр левого столба (локальные координаты корня забора).</summary>
            public Vector3 start;

            /// <summary>Центр правого столба.</summary>
            public Vector3 end;

            /// <summary>Горизонтальное единичное направление хорды.</summary>
            public Vector3 dir;

            /// <summary>Расстояние между центрами столбов, м.</summary>
            public float span;

            /// <summary>Длина полотна после привязки к brickCell, м (≤ span).</summary>
            public float wallLength;

            /// <summary>На старте панели стоит столб. false — там угловой столб угла.</summary>
            public bool startHasPillar;

            /// <summary>На конце панели стоит столб. false — там угловой столб угла.</summary>
            public bool endHasPillar;
        }

        // ============================================================
        // IFenceStyle
        // ============================================================

        public void Build(FencePath path, FenceTypeDefinition t, FenceBuildOutput output)
        {
            if (path == null || path.runs.Count == 0)
            {
                output.warnings.Add("Masonry: нет прогонов — геометрия не построена.");
                return;
            }

            Material primary = t.primaryMaterial;
            if (primary == null)
                output.warnings.Add("Masonry: не задан primaryMaterial — полотно получит пустой слот материала.");

            Material[] materials = t.secondaryMaterial != null
                ? new[] { primary, t.secondaryMaterial }
                : new[] { primary };

            // 0 — полотно (кладка), 1 — столбы/оголовки/шапка (бетон)
            int trimSubmesh = materials.Length > 1 ? 1 : 0;

            for (int r = 0; r < path.runs.Count; r++)
            {
                FenceRun run = path.runs[r];

                if (run.FrameCount < 2)
                {
                    output.warnings.Add($"Masonry: прогон {r} короче двух кадров — пропущен.");
                    continue;
                }

                List<MasonryPanel> panels = Layout(run, t, output.warnings);

                if (panels.Count == 0)
                {
                    output.warnings.Add($"Masonry: прогон {r} не дал ни одной панели — пропущен.");
                    continue;
                }

                BuildSections(path, run, panels, t, materials, trimSubmesh, output);
                AddColliders(path, run, panels, t, output);
            }
        }

        // ============================================================
        // Раскладка: столбы и панели
        // ============================================================

        /// <summary>Модуль кладки, м. Берётся из масштаба материала: _Brick_Scale = 5.8 → 0.172 м.</summary>
        public static float BrickCell(FenceTypeDefinition t)
        {
            float scale = t != null ? t.brickScale : 5.8f;
            return 1f / Mathf.Clamp(scale, 0.1f, 100f);
        }

        /// <summary>Панели прогона: где столбы, какая длина полотна, у каких торцов столба нет.</summary>
        public static List<MasonryPanel> Layout(FenceRun run, FenceTypeDefinition t, List<string> warnings)
        {
            var panels = new List<MasonryPanel>();

            float cell = BrickCell(t);
            List<float> pillars = PillarDistances(run, t, warnings);
            if (pillars.Count < 2) return panels;

            // Шов замкнутого прогона: столб стоит один раз — на конце прогона, начало под ним проходит.
            // Торец у резкого угла тоже без своего столба — там угловой.
            bool startSuppressed = run.startTrim > 1e-4f || run.closedLoop;
            bool endSuppressed = run.endTrim > 1e-4f;

            for (int i = 0; i < pillars.Count - 1; i++)
            {
                FenceFrame a = run.Sample(pillars[i]);
                FenceFrame b = run.Sample(pillars[i + 1]);

                Vector3 dir = b.position - a.position;
                dir.y = 0f;

                float span = dir.magnitude;
                if (span < 1e-3f) continue;
                dir /= span;

                bool startHasPillar = !(i == 0 && startSuppressed);
                bool endHasPillar = !(i == pillars.Count - 2 && endSuppressed);

                float wallLength = span;

                // Привязка к модулю кладки — только если остаток спрячется в столбе.
                if (endHasPillar)
                {
                    float snapped = Mathf.Floor(span / cell) * cell;
                    if (snapped >= cell && span - snapped < t.postSize * 0.5f)
                        wallLength = snapped;
                }

                panels.Add(new MasonryPanel
                {
                    start = a.position,
                    end = b.position,
                    dir = dir,
                    span = span,
                    wallLength = Mathf.Max(cell * 0.5f, wallLength),
                    startHasPillar = startHasPillar,
                    endHasPillar = endHasPillar
                });
            }

            return panels;
        }

        /// <summary>
        /// Погонные метры столбов: шаг ≤ postSpacing, но панель укорачивается, пока стрелка её хорды
        /// не влезет в panelChordError. Огрызок панели в конце прогона не оставляем.
        /// </summary>
        public static List<float> PillarDistances(FenceRun run, FenceTypeDefinition t, List<string> warnings)
        {
            var list = new List<float> { 0f };

            float length = run.Length;
            if (length < 1e-3f) return list;

            float spacing = Mathf.Clamp(t.postSpacing, 0.6f, 8f);
            float cell = BrickCell(t);
            float minPanel = Mathf.Max(MinPanelLength, cell * 3f);
            float error = Mathf.Max(0.002f, t.panelChordError);

            float d = 0f;
            int guard = 0;

            while (length - d > 1e-3f)
            {
                float remaining = length - d;
                float step = Mathf.Min(spacing, remaining);

                // последняя панель добирает остаток, чтобы не было огрызка
                if (remaining - step > 1e-3f && remaining - step < minPanel)
                    step = remaining;

                step = FitToChord(run, d, step, error, minPanel);
                if (step <= 1e-3f) break;

                d = Mathf.Min(length, d + step);
                list.Add(d);

                if (++guard > MaxPanelsPerRun)
                {
                    warnings?.Add($"Masonry: прогон {run.index} требует больше {MaxPanelsPerRun} панелей — " +
                                  "раскладка прервана, проверьте postSpacing/panelChordError.");
                    break;
                }
            }

            if (list[list.Count - 1] < length - 1e-3f) list.Add(length);

            return list;
        }

        private static float FitToChord(FenceRun run, float from, float step, float error, float minPanel)
        {
            // стрелка хорды растёт как L²/R, поэтому шаг на 0.7 уменьшает её вдвое — сходится быстро
            while (step > minPanel + 1e-4f)
            {
                if (ChordError(run, from, step) <= error) return step;
                step = Mathf.Max(minPanel, step * 0.7f);
            }

            return step;
        }

        /// <summary>Стрелка прогиба хорды панели: максимальное горизонтальное отклонение пути от прямой.</summary>
        private static float ChordError(FenceRun run, float from, float step)
        {
            Vector3 a = run.Sample(from).position;
            Vector3 b = run.Sample(from + step).position;
            a.y = 0f;
            b.y = 0f;

            Vector3 dir = b - a;
            float length = dir.magnitude;
            if (length < 1e-4f) return 0f;
            dir /= length;

            int samples = Mathf.Clamp(Mathf.CeilToInt(length / 0.05f), 4, 64);
            float worst = 0f;

            for (int i = 1; i < samples; i++)
            {
                Vector3 p = run.Sample(from + step * i / samples).position;
                p.y = 0f;

                Vector3 rel = p - a;
                Vector3 perp = rel - dir * Vector3.Dot(rel, dir);

                float distance = perp.magnitude;
                if (distance > worst) worst = distance;
            }

            return worst;
        }

        // ============================================================
        // Геометрия по секциям
        // ============================================================

        /// <summary>Поворот секции: локальная X = направление стены, Y = вверх, Z = поперёк стены.</summary>
        private static Quaternion SectionRotation(Vector3 dir)
        {
            Vector3 forward = Vector3.Cross(dir, Vector3.up);
            if (forward.sqrMagnitude < 1e-8f) forward = Vector3.forward;

            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private static void BuildSections(FencePath path, FenceRun run, List<MasonryPanel> panels,
                                          FenceTypeDefinition t, Material[] materials, int trimSubmesh,
                                          FenceBuildOutput output)
        {
            float alignDeg = Mathf.Clamp(t.sectionAlignDeg, 0.05f, 45f);

            int sectionIndex = 0;
            int cursor = 0;

            while (cursor < panels.Count)
            {
                Vector3 sectionDir = panels[cursor].dir;
                int end = cursor + 1;

                while (end < panels.Count && Vector3.Angle(sectionDir, panels[end].dir) <= alignDeg)
                    end++;

                Vector3 origin = panels[cursor].start;
                Quaternion rotation = SectionRotation(sectionDir);
                Quaternion inverse = Quaternion.Inverse(rotation);

                string name = $"Fence_run{run.index}_s{sectionIndex}";
                var builder = new FenceMeshBuilder(name, materials.Length);

                for (int i = cursor; i < end; i++)
                    AddPanel(builder, panels[i], t, trimSubmesh, origin, inverse);

                // угловой столб стоит в узле, то есть в начале прогона — только в его первой секции
                if (sectionIndex == 0)
                    AddCornerPillars(builder, path, run, t, trimSubmesh, origin, inverse);

                // замыкающий столб последней панели: стартовые уже нарисованы в своих панелях
                if (end >= panels.Count)
                {
                    MasonryPanel last = panels[panels.Count - 1];
                    if (last.endHasPillar)
                        AddPillar(builder, inverse * (last.end - origin), inverse * last.dir, t, trimSubmesh);
                }

                if (!builder.IsEmpty)
                {
                    output.sections.Add(new FenceSectionMesh
                    {
                        name = name,
                        mesh = builder.Build(),
                        materials = materials,
                        localPosition = origin,
                        localRotation = rotation
                    });

                    output.TotalQuads += builder.QuadCount;
                }

                sectionIndex++;
                cursor = end;
            }
        }

        private static void AddPanel(FenceMeshBuilder b, MasonryPanel p, FenceTypeDefinition t, int trimSubmesh,
                                     Vector3 origin, Quaternion inverse)
        {
            Vector3 a = inverse * (p.start - origin);
            Vector3 e = inverse * (p.end - origin);
            Vector3 dir = inverse * p.dir;

            if (p.startHasPillar)
                AddPillar(b, a, dir, t, trimSubmesh);

            // Ступень по столбу (§5.5): панель горизонтальна, подъём берётся по нижнему торцу,
            // чтобы на уклоне стена не висела в воздухе.
            float baseY = Mathf.Min(a.y, e.y);
            Vector3 baseCenter = new Vector3(a.x, baseY, a.z);
            Vector3 mid = baseCenter + dir * (p.wallLength * 0.5f);

            // полотно — экструдированная стена; кладку рисует материал
            b.AddBox(0,
                mid + Vector3.up * (t.height * 0.5f),
                dir, Vector3.up,
                new Vector3(p.wallLength, t.height, t.thickness));

            // шапка поверх полотна, свес — по толщине стены
            if (t.copingHeight > 0.0005f)
            {
                float copingThickness = t.thickness + Mathf.Max(0f, t.copingOverhang) * 2f;

                b.AddBox(trimSubmesh,
                    mid + Vector3.up * (t.height + t.copingHeight * 0.5f),
                    dir, Vector3.up,
                    new Vector3(p.wallLength, t.copingHeight, copingThickness));
            }
        }

        private static void AddPillar(FenceMeshBuilder b, Vector3 baseCenter, Vector3 axisX, FenceTypeDefinition t,
                                      int submesh)
        {
            b.AddBox(submesh,
                baseCenter + Vector3.up * (t.postHeight * 0.5f),
                axisX, Vector3.up,
                new Vector3(t.postSize, t.postHeight, t.postSize));

            if (t.capHeight > 0.0005f && t.capSize > 0.0005f)
                b.AddBox(submesh,
                    baseCenter + Vector3.up * (t.postHeight + t.capHeight * 0.5f),
                    axisX, Vector3.up,
                    new Vector3(t.capSize, t.capHeight, t.capSize));
        }

        private static void AddCornerPillars(FenceMeshBuilder b, FencePath path, FenceRun run, FenceTypeDefinition t,
                                             int submesh, Vector3 origin, Quaternion inverse)
        {
            for (int c = 0; c < path.corners.Count; c++)
            {
                FenceCorner corner = path.corners[c];
                if (corner.kind != FenceJointKind.Sharp) continue;
                if (corner.runAfter != run.index) continue;

                AddPillar(b, inverse * (corner.position - origin), inverse * corner.bisector, t, submesh);
            }
        }

        // ============================================================
        // Коллайдеры: бокс на столб (с оголовком) и бокс на панель
        // ============================================================

        private static void AddColliders(FencePath path, FenceRun run, List<MasonryPanel> panels,
                                         FenceTypeDefinition t, FenceBuildOutput output)
        {
            float wallHeight = Mathf.Max(t.height, t.colliderHeight);
            float pillarHeight = t.postHeight + Mathf.Max(0f, t.capHeight);
            var pillarSize = new Vector3(t.postSize, pillarHeight, t.postSize);

            for (int i = 0; i < panels.Count; i++)
            {
                MasonryPanel p = panels[i];

                if (p.startHasPillar)
                    output.colliders.Add(FenceBoxSpec.Create(
                        $"BC_pillar_{run.index}_{i:000}",
                        p.start + Vector3.up * (pillarHeight * 0.5f),
                        p.dir, Vector3.up, pillarSize));

                // панель перекрывается сплошняком до центра следующего столба — без зазора на
                // «съеденный» остаток привязки к brickCell
                float baseY = Mathf.Min(p.start.y, p.end.y);

                var center = new Vector3(
                    (p.start.x + p.end.x) * 0.5f,
                    baseY + wallHeight * 0.5f,
                    (p.start.z + p.end.z) * 0.5f);

                output.colliders.Add(FenceBoxSpec.Create(
                    $"BC_wall_{run.index}_{i:000}",
                    center, p.dir, Vector3.up,
                    new Vector3(p.span, wallHeight, t.thickness)));
            }

            // замыкающий столб прогона
            MasonryPanel last = panels[panels.Count - 1];
            if (last.endHasPillar)
                output.colliders.Add(FenceBoxSpec.Create(
                    $"BC_pillar_{run.index}_{panels.Count:000}",
                    last.end + Vector3.up * (pillarHeight * 0.5f),
                    last.dir, Vector3.up, pillarSize));

            // угловые столбы, принадлежащие этому прогону (corner.runAfter == run.index)
            for (int c = 0; c < path.corners.Count; c++)
            {
                FenceCorner corner = path.corners[c];
                if (corner.kind != FenceJointKind.Sharp) continue;
                if (corner.runAfter != run.index) continue;

                output.colliders.Add(FenceBoxSpec.Create(
                    $"BC_corner_{corner.knotIndex:000}",
                    corner.position + Vector3.up * (pillarHeight * 0.5f),
                    corner.bisector, Vector3.up, pillarSize));
            }
        }
    }
}
