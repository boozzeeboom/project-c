using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.World.Generation
{
    /// <summary>
    /// Геометрические параметры дорожки. Отделены от компонента, чтобы сборку можно было
    /// прогнать без сцены (self-check) — так же, как FenceTypeDefinition → FencePathSettings.
    /// </summary>
    public struct RoadGeometrySettings
    {
        public RoadSurface surface;

        /// <summary>Полная ширина покрытия, м.</summary>
        public float width;

        /// <summary>Ширина бордюра, м (лежит внутри покрытия).</summary>
        public float curbWidth;

        /// <summary>Высота бордюра над покрытием, м.</summary>
        public float curbHeight;

        public Material roadMaterial;
        public Material curbMaterial;

        public bool generateColliders;
        public float colliderThickness;
        public float colliderStep;

        /// <summary>Излом выше этого угла получает дополнительную плиту-коллайдер (miter-выступ).</summary>
        public float cornerColliderAngleDeg;

        public static RoadGeometrySettings Default => new()
        {
            surface = RoadSurface.PlaneWithCurbs,
            width = 4f,
            curbWidth = 0.25f,
            curbHeight = 0.15f,
            generateColliders = true,
            colliderThickness = 0.4f,
            colliderStep = 2f,
            cornerColliderAngleDeg = 15f
        };
    }

    /// <summary>Результат сборки дорожки: один меш, материалы по сабмешам, бокс-коллайдеры.</summary>
    public sealed class RoadBuildOutput
    {
        public Mesh mesh;
        public Material[] materials = Array.Empty<Material>();

        public readonly List<FenceBoxSpec> colliders = new();
        public readonly List<string> warnings = new();

        public int quads;
        public int triangles;

        public void Release()
        {
            if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
            mesh = null;
            colliders.Clear();
        }
    }

    /// <summary>
    /// Сборка геометрии дорожки по готовому RoadPath.
    ///
    /// Покрытие — одна непрерывная лента: квад на сегмент между кадрами, края берутся из
    /// <see cref="RoadFrame.leftEdge"/>/<see cref="RoadFrame.rightEdge"/> (там уже посчитан miter),
    /// поэтому 90° не рвёт дорожку и не даёт щелей.
    ///
    /// UV0 в метрах (U вдоль дорожки, V поперёк) — как у FenceMeshBuilder: текстура не растягивается
    /// ни на длинной прямой, ни на коротком повороте.
    ///
    /// Коллайдеры — плиты высотой colliderThickness, верх ровно на поверхности покрытия, плюс
    /// квадратные плиты на углах (miter-выступ шире одного пролёта).
    /// </summary>
    public static class RoadMeshBuilder
    {
        private const int MaxColliderSlabs = 4000;
        private const int MaxCornerSlabs = 500;

        public static RoadBuildOutput Build(RoadPath path, RoadGeometrySettings s)
        {
            var output = new RoadBuildOutput();

            if (path == null || !path.IsValid)
            {
                output.warnings.Add("Путь не построен — геометрия не построена.");
                return output;
            }

            if (s.width < 0.05f)
            {
                output.warnings.Add($"Ширина {s.width:F2} м — слишком мала, геометрия не построена.");
                return output;
            }

            bool curbs = s.surface == RoadSurface.PlaneWithCurbs;
            bool twoMaterials = curbs && s.curbMaterial != null;

            int submeshCount = twoMaterials ? 2 : 1;
            int curbSubmesh = twoMaterials ? 1 : 0;

            Material[] materials = twoMaterials
                ? new[] { s.roadMaterial, s.curbMaterial }
                : new[] { s.roadMaterial };

            if (s.roadMaterial == null)
                output.warnings.Add("Не задан roadMaterial — покрытие получит пустой слот материала.");

            var builder = new FenceMeshBuilder("Road", submeshCount);

            Vector3 up = path.up;
            int n = path.frames.Count;
            int segments = path.SegmentCount;

            for (int i = 0; i < segments; i++)
            {
                RoadFrame a = path.frames[i];
                RoadFrame b = path.frames[(i + 1) % n];

                // порядок обхода даёт нормаль вверх: cross(b.left-a.left, b.right-a.left) ≈ +up
                builder.AddQuad(0, a.leftEdge, b.leftEdge, b.rightEdge, a.rightEdge);

                if (!curbs) continue;

                AddCurb(builder, curbSubmesh, a.leftEdge, b.leftEdge, up, s, 1f);    // левый борт внутрь
                AddCurb(builder, curbSubmesh, a.rightEdge, b.rightEdge, up, s, -1f); // правый борт внутрь
            }

            if (builder.IsEmpty)
            {
                output.warnings.Add("Геометрия пустая: все сегменты вырождены.");
                output.warnings.AddRange(path.warnings);
                return output;
            }

            if (s.generateColliders && s.colliderThickness > 0.001f)
                AddColliders(path, s, up, output);

            output.mesh = builder.Build();
            output.mesh.name = "Road";
            output.materials = materials;
            output.quads = builder.QuadCount;

            int tris = 0;
            for (int m = 0; m < output.mesh.subMeshCount; m++)
                tris += (int)(output.mesh.GetIndexCount(m) / 3);

            output.triangles = tris;
            output.warnings.AddRange(path.warnings);

            return output;
        }

        /// <summary>Бордюр = коробка на ребре, сдвинутая внутрь покрытия на свою полуширину.</summary>
        private static void AddCurb(FenceMeshBuilder b, int submesh, Vector3 a, Vector3 c, Vector3 up,
                                    RoadGeometrySettings s, float inwardSign)
        {
            if (s.curbWidth <= 0.001f || s.curbHeight <= 0.001f) return;

            Vector3 dir = c - a;
            float length = dir.magnitude;
            if (length < 1e-4f) return;

            Vector3 flat = dir - up * Vector3.Dot(dir, up);
            if (flat.sqrMagnitude < 1e-8f) return;

            Vector3 perpendicular = Vector3.Cross(up, flat.normalized);
            if (perpendicular.sqrMagnitude < 1e-8f) return;
            perpendicular.Normalize();

            Vector3 center = (a + c) * 0.5f
                             + perpendicular * (inwardSign * s.curbWidth * 0.5f)
                             + up * (s.curbHeight * 0.5f);

            b.AddBox(submesh, center, dir, up, new Vector3(length, s.curbHeight, s.curbWidth));
        }

        private static void AddColliders(RoadPath path, RoadGeometrySettings s, Vector3 up, RoadBuildOutput output)
        {
            float thickness = Mathf.Max(0.02f, s.colliderThickness);
            float step = Mathf.Max(0.5f, s.colliderStep);

            float total = path.closed ? path.splineLength : path.Length;
            if (total < 1e-3f) return;

            int count = Mathf.Clamp(Mathf.CeilToInt(total / step), 1, MaxColliderSlabs);

            for (int i = 0; i < count; i++)
            {
                RoadFrame a = path.Sample(total * i / count);
                RoadFrame b = path.Sample(total * (i + 1) / count);

                Vector3 dir = b.position - a.position;
                float length = dir.magnitude;
                if (length < 1e-3f) continue;

                Vector3 center = (a.position + b.position) * 0.5f - up * (thickness * 0.5f);

                output.colliders.Add(FenceBoxSpec.Create(
                    $"BC_road_{i:0000}",
                    center, dir, up,
                    new Vector3(length, thickness, s.width)));
            }

            float angle = Mathf.Clamp(s.cornerColliderAngleDeg, 1f, 179f);
            int corners = 0;

            for (int i = 0; i < path.frames.Count; i++)
            {
                if (corners >= MaxCornerSlabs) break;

                RoadFrame f = path.frames[i];
                if (f.turnDeg <= angle) continue;

                float halfSide = s.width * 0.5f * Mathf.Max(1f, f.miterScale);

                output.colliders.Add(FenceBoxSpec.Create(
                    $"BC_corner_{i:0000}",
                    f.position - up * (thickness * 0.5f),
                    f.forward, up,
                    new Vector3(halfSide * 2f, thickness, halfSide * 2f)));

                corners++;
            }
        }
    }
}
