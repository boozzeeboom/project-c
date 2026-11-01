// Project C: манифест HLOD города PRIMUM (T-CULL01 / Stage 7, H7).
//
// Что это: данные, запечённые в РЕДАКТОРЕ инструментом PrimumHlodBaker
// (Project C → Culling → Stage 7 HLOD). В рантайме ничего не генерируется: VisibilityDirector только
// читает манифест (Resources/HLOD/PRIMUM_HLOD), ставит запечённые меши под WorldRoot_0_0 и
// переключает L0 / L1 / L2 по дистанции.
//
//   L1 (3–12 км): один меш на блок (= ребёнок PRIMUM = чанк директора), без интерьеров и мелочи
//                 < 0.25 м, цвет материала — в вершинах, окна — эмиссией ночью; 1 материал на весь город.
//   L2 (12+ км):  один camera-facing квад на город; атлас 8 азимутов × 3 высоты (силуэт + ночные огни).
//
// Координаты — в пространстве WorldRoot_0_0 (FO-инвариантно): ребаз переносит WorldRoot, а вместе с
// ним и всё, что под ним. Хранятся только меши/материалы/скаляры, никаких ссылок на объекты сцены.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.World
{
    public class PrimumHlodManifest : ScriptableObject
    {
        /// <summary>Путь для Resources.Load (Assets/_Project/Resources/HLOD/PRIMUM_HLOD.asset).</summary>
        public const string ResourcePath = "HLOD/PRIMUM_HLOD";

        public const int CurrentFormat = 1;

        [Serializable]
        public class Block
        {
            [Tooltip("Имя ребёнка PRIMUM — по нему директор находит чанк.")]
            public string name;

            [Tooltip("L1-меш блока; вершины относительно pivot.")]
            public Mesh mesh;

            [Tooltip("Pivot (центр AABB меша) в пространстве WorldRoot_0_0.")]
            public Vector3 pivot;

            [Tooltip("Габарит AABB меша, м.")]
            public Vector3 size;

            [Tooltip("Слой исходных рендереров блока (PRIMUM = 8).")]
            public int layer;

            public int sourceRenderers;
            public int sourceTriangles;
            public int smallRemovedTriangles;
            public int hiddenRemovedTriangles;
            public int triangles;
            public int vertices;

            [Tooltip("Хэш содержимого меша (SHA1) — для проверки детерминизма повторного bake.")]
            public string hash;
        }

        public int format = CurrentFormat;

        [Tooltip("Общий хэш bake (все блоки + импостор).")]
        public string bakeHash;

        [Tooltip("Параметры bake одной строкой (для отчёта).")]
        public string bakeSettings;

        [Header("L1")]
        public Material l1Material;
        public List<Block> blocks = new List<Block>();

        [Header("L2")]
        public bool impostorValid;
        public Mesh impostorMesh;
        public Material impostorMaterial;

        [Tooltip("Центр AABB города (все блоки) в пространстве WorldRoot_0_0.")]
        public Vector3 cityCenter;

        public Vector3 citySize;
        public string impostorHash;

        [Header("Дистанции (м до AABB, ближайшая камера union)")]
        public float l1Distance = 3000f;
        public float l2Distance = 12000f;

        [Tooltip("Гистерезис обратного перехода (доля).")]
        public float hysteresis = 0.1f;

        public int FindBlock(string blockName)
        {
            for (int i = 0; i < blocks.Count; i++)
                if (blocks[i] != null && blocks[i].name == blockName) return i;
            return -1;
        }
    }
}
