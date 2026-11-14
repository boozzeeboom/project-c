using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.World.Generation
{
    /// <summary>Одна меш-секция забора: объект-ребёнок корня + меш + материалы по сабмешам.</summary>
    public sealed class FenceSectionMesh
    {
        public string name;
        public Mesh mesh;
        public Material[] materials;

        /// <summary>Смещение секции в локальных координатах корня забора (сейчас всегда 0).</summary>
        public Vector3 localPosition = Vector3.zero;

        public Quaternion localRotation = Quaternion.identity;
    }

    /// <summary>
    /// Бокс-коллайдер в локальных координатах корня забора.
    /// Базис тот же, что у FenceMeshBuilder.AddBox: axisZ = cross(axisX, axisY) — поэтому
    /// коллайдер совпадает с видимой геометрией, а не «примерно».
    /// </summary>
    public struct FenceBoxSpec
    {
        public string name;
        public Vector3 center;
        public Vector3 axisX;
        public Vector3 axisY;

        /// <summary>(вдоль axisX, вдоль axisY, вдоль axisZ), м.</summary>
        public Vector3 size;

        public static FenceBoxSpec Create(string name, Vector3 center, Vector3 axisX, Vector3 axisY, Vector3 size)
        {
            return new FenceBoxSpec
            {
                name = name,
                center = center,
                axisX = axisX,
                axisY = axisY,
                size = size
            };
        }
    }

    /// <summary>Результат работы стиля: секции меша + бокс-коллайдеры + предупреждения.</summary>
    public sealed class FenceBuildOutput
    {
        public readonly List<FenceSectionMesh> sections = new();
        public readonly List<FenceBoxSpec> colliders = new();
        public readonly List<string> warnings = new();

        public int TotalQuads { get; set; }

        public int TotalTriangles
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < sections.Count; i++)
                {
                    Mesh m = sections[i].mesh;
                    if (m == null) continue;
                    for (int s = 0; s < m.subMeshCount; s++) sum += (int)(m.GetIndexCount(s) / 3);
                }
                return sum;
            }
        }

        public void ReleaseMeshes()
        {
            for (int i = 0; i < sections.Count; i++)
            {
                if (sections[i].mesh != null) Object.DestroyImmediate(sections[i].mesh);
                sections[i].mesh = null;
            }

            sections.Clear();
            colliders.Clear();
        }
    }

    /// <summary>Стиль забора: путь + тип → геометрия. Стиль ничего не знает про сцену и Undo.</summary>
    public interface IFenceStyle
    {
        void Build(FencePath path, FenceTypeDefinition type, FenceBuildOutput output);
    }

    /// <summary>Единая точка входа для инструмента: выбор стиля по FenceTypeDefinition.style.</summary>
    public static class FenceStyleBuilder
    {
        private static readonly BarFenceStyle Bar = new();
        private static readonly MasonryFenceStyle Masonry = new();

        public static FenceBuildOutput Build(FencePath path, FenceTypeDefinition type)
        {
            var output = new FenceBuildOutput();

            if (type == null)
            {
                output.warnings.Add("Не задан FenceTypeDefinition — геометрия не построена.");
                return output;
            }

            if (path == null)
            {
                output.warnings.Add("Путь не построен — геометрия не построена.");
                return output;
            }

            switch (type.style)
            {
                case FenceStyle.Bar:
                    Bar.Build(path, type, output);
                    break;

                case FenceStyle.Masonry:
                    Masonry.Build(path, type, output);
                    break;

                default:
                    output.warnings.Add($"Стиль {type.style} ещё не реализован (пилот: {FenceStyle.Bar} / {FenceStyle.Masonry}).");
                    break;
            }

            output.warnings.AddRange(path.warnings);
            return output;
        }
    }
}
