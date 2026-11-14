using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.World.Generation
{
    /// <summary>
    /// Прутковый забор («как арматура сваренная»).
    ///
    /// Состав на прогон:
    ///   * столбы через postSpacing (и ровно в начале/конце прогона) — сабмеш secondaryMaterial;
    ///   * балки: верхняя railTopY и нижняя railBottomY (+ средняя, если midRail) — идут по кадрам,
    ///     то есть повторяют кривую, а не режутся прямыми хордами;
    ///   * прутки: вертикальные призмы barSize квадратом с шагом barSpacing; внутри столба пруток не ставим.
    ///
    /// Угловой столб ставится в узле Sharp и добавляется в секцию прогона ПОСЛЕ узла,
    /// чтобы не плодить объекты на каждый угол.
    /// </summary>
    public sealed class BarFenceStyle : IFenceStyle
    {
        private const int MaxBarsPerRun = 20000;

        public void Build(FencePath path, FenceTypeDefinition t, FenceBuildOutput output)
        {
            if (path == null || path.runs.Count == 0)
            {
                output.warnings.Add("Bar: нет прогонов — геометрия не построена.");
                return;
            }

            Material primary = t.primaryMaterial;
            if (primary == null)
                output.warnings.Add("Bar: не задан primaryMaterial — секция получит пустой слот материала.");

            Material[] materials = t.secondaryMaterial != null
                ? new[] { primary, t.secondaryMaterial }
                : new[] { primary };

            int postSubmesh = materials.Length > 1 ? 1 : 0;

            for (int r = 0; r < path.runs.Count; r++)
            {
                FenceRun run = path.runs[r];

                if (run.FrameCount < 2)
                {
                    output.warnings.Add($"Bar: прогон {r} короче двух кадров — пропущен.");
                    continue;
                }

                var builder = new FenceMeshBuilder($"Fence_run{r}", materials.Length);
                List<float> posts = PostDistances(run, t);

                AddPosts(builder, run, posts, t, postSubmesh);
                AddCornerPillars(builder, path, run, t, postSubmesh);
                AddRails(builder, run, t, postSubmesh);
                AddBars(builder, run, posts, t, output);

                if (builder.IsEmpty) continue;

                output.sections.Add(new FenceSectionMesh
                {
                    name = $"Fence_run{r}",
                    mesh = builder.Build(),
                    materials = materials
                });

                output.TotalQuads += builder.QuadCount;

                AddPostColliders(output, run, posts, t);
                AddCornerPillarColliders(output, path, run, t);
                AddPanelColliders(output, run, posts, t);
            }
        }

        // ============================================================
        // Столбы
        // ============================================================

        /// <summary>Погонные метры столбов в прогоне: 0, шаг, 2·шаг… и ровно конец прогона.</summary>
        private static List<float> PostDistances(FenceRun run, FenceTypeDefinition t)
        {
            var list = new List<float> { 0f };

            float spacing = Mathf.Max(0.2f, t.postSpacing);
            float length = run.Length;

            for (float d = spacing; d < length - 0.1f; d += spacing)
                list.Add(d);

            if (length - list[list.Count - 1] > 0.05f)
                list.Add(length);
            else
                list[list.Count - 1] = length;   // последний недомерок сдвигаем в торец

            return list;
        }

        private static void AddPosts(FenceMeshBuilder b, FenceRun run, List<float> posts, FenceTypeDefinition t,
                                     int submesh)
        {
            var size = new Vector3(t.postSize, t.postHeight, t.postSize);
            float halfHeight = t.postHeight * 0.5f;

            for (int i = 0; i < posts.Count; i++)
            {
                // у замкнутого прогона начало и конец совпадают: второй столб дал бы z-fighting
                if (i == 0 && run.closedLoop && posts.Count > 1) continue;

                FenceFrame f = run.Sample(posts[i]);
                b.AddBox(submesh, f.position + Vector3.up * halfHeight, f.right, Vector3.up, size);
            }
        }

        /// <summary>Угловые столбы, принадлежащие этому прогону (corner.runAfter == run.index).</summary>
        private static void AddCornerPillars(FenceMeshBuilder b, FencePath path, FenceRun run, FenceTypeDefinition t,
                                            int submesh)
        {
            var size = new Vector3(t.postSize, t.postHeight, t.postSize);
            float halfHeight = t.postHeight * 0.5f;

            for (int c = 0; c < path.corners.Count; c++)
            {
                FenceCorner corner = path.corners[c];
                if (corner.kind != FenceJointKind.Sharp) continue;
                if (corner.runAfter != run.index) continue;

                b.AddBox(submesh, corner.position + Vector3.up * halfHeight, corner.bisector, Vector3.up, size);
            }
        }

        // ============================================================
        // Балки и прутки
        // ============================================================

        private static void AddRails(FenceMeshBuilder b, FenceRun run, FenceTypeDefinition t, int submesh)
        {
            AddRailAt(b, run, t.railTopY, t.railSize, submesh);
            AddRailAt(b, run, t.railBottomY, t.railSize, submesh);

            if (t.midRail)
                AddRailAt(b, run, t.midRailY, t.railSize * 0.85f, submesh);
        }

        /// <summary>Балка идёт по кадрам прогона: на дуге она ломаная с шагом тесселяции, а не хорда.</summary>
        private static void AddRailAt(FenceMeshBuilder b, FenceRun run, float heightY, float railSize, int submesh)
        {
            for (int i = 0; i < run.FrameCount - 1; i++)
            {
                FenceFrame a = run.frames[i];
                FenceFrame c = run.frames[i + 1];

                Vector3 dir = c.position - a.position;
                float length = dir.magnitude;
                if (length < 1e-3f) continue;

                Vector3 center = (a.position + c.position) * 0.5f + Vector3.up * heightY;
                b.AddBox(submesh, center, dir, Vector3.up, new Vector3(length, railSize, railSize));
            }
        }

        private static void AddBars(FenceMeshBuilder b, FenceRun run, List<float> posts, FenceTypeDefinition t,
                                    FenceBuildOutput output)
        {
            float y0 = t.barBottomY;
            float height = Mathf.Max(0.01f, t.barTopY - t.barBottomY);
            float halfHeight = height * 0.5f;

            float spacing = Mathf.Max(0.02f, t.barSpacing);
            float length = run.Length;

            if (length / spacing > MaxBarsPerRun)
            {
                output.warnings.Add($"Bar: прогон {run.index} требует {Mathf.RoundToInt(length / spacing)} прутков " +
                                    $"(> {MaxBarsPerRun}) — прутки пропущены, проверьте barSpacing.");
                return;
            }

            float clearance = t.postSize * 0.5f + t.barSize * 1.5f;
            var size = new Vector3(t.barSize, height, t.barSize);

            for (float d = spacing * 0.5f; d < length - 1e-3f; d += spacing)
            {
                if (IsNearPost(posts, d, clearance)) continue;

                FenceFrame f = run.Sample(d);
                b.AddBox(0, f.position + Vector3.up * (y0 + halfHeight), f.right, Vector3.up, size);
            }
        }

        private static bool IsNearPost(List<float> posts, float distance, float clearance)
        {
            for (int i = 0; i < posts.Count; i++)
                if (Mathf.Abs(posts[i] - distance) < clearance) return true;

            return false;
        }

        // ============================================================
        // Коллайдеры
        // ============================================================

        private static void AddPostColliders(FenceBuildOutput output, FenceRun run, List<float> posts, FenceTypeDefinition t)
        {
            var size = new Vector3(t.postSize, t.postHeight, t.postSize);
            float halfHeight = t.postHeight * 0.5f;

            for (int i = 0; i < posts.Count; i++)
            {
                // у замкнутого прогона начало и конец совпадают: второй коллайдер был бы дублем
                if (i == 0 && run.closedLoop && posts.Count > 1) continue;

                FenceFrame f = run.Sample(posts[i]);
                output.colliders.Add(FenceBoxSpec.Create(
                    $"BC_post_{run.index}_{i:000}",
                    f.position + Vector3.up * halfHeight,
                    f.right, Vector3.up, size));
            }
        }

        private static void AddCornerPillarColliders(FenceBuildOutput output, FencePath path, FenceRun run, FenceTypeDefinition t)
        {
            var size = new Vector3(t.postSize, t.postHeight, t.postSize);
            float halfHeight = t.postHeight * 0.5f;

            for (int c = 0; c < path.corners.Count; c++)
            {
                FenceCorner corner = path.corners[c];
                if (corner.kind != FenceJointKind.Sharp) continue;
                if (corner.runAfter != run.index) continue;

                output.colliders.Add(FenceBoxSpec.Create(
                    $"BC_corner_{corner.knotIndex:000}",
                    corner.position + Vector3.up * halfHeight,
                    corner.bisector, Vector3.up, size));
            }
        }

        /// <summary>Панель между соседними столбами: один бокс на пролёт (§9).</summary>
        private static void AddPanelColliders(FenceBuildOutput output, FenceRun run, List<float> posts, FenceTypeDefinition t)
        {
            float thickness = Mathf.Max(t.thickness, t.barSize * 2f);
            float halfHeight = t.colliderHeight * 0.5f;

            for (int i = 0; i < posts.Count - 1; i++)
            {
                FenceFrame a = run.Sample(posts[i]);
                FenceFrame c = run.Sample(posts[i + 1]);

                Vector3 dir = c.position - a.position;
                dir.y = 0f;

                float length = dir.magnitude;
                if (length < 0.05f) continue;

                Vector3 center = (a.position + c.position) * 0.5f + Vector3.up * halfHeight;

                output.colliders.Add(FenceBoxSpec.Create(
                    $"BC_panel_{run.index}_{i:000}",
                    center, dir.normalized, Vector3.up,
                    new Vector3(length, t.colliderHeight, thickness)));
            }
        }
    }
}
