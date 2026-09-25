// T-NS-NAV16: лёгкое глобальное знание пиков — диски из heightmap (server-only).
// Зачем: лидар видит только вперёд, навигатор планировал зондом с места —
// про архитектуру хребта не знал и мог вести назад в ту же гору (U-turn).
// Диски дают GRADE: где пики и какой радиус — обход строится по касательным,
// точно, а не ±250 наугад.
//
// Источник: Unity Terrain heightmap (Terrain_0_0 и др.), сэмпл один раз.
// Хранение ЛОКАЛЬНОЕ (доли + метры от origin террейна) — F8-хук не нужен:
// мир едет, доли те же, world резолвится вживую через трансформ террейна.
// Меши-скалы дисками не покрыты — их добирает лидар как раньше (fallback).
// Лор: диск учитывается, только если пик реально выше профиля (иначе прямо;
// пролёт ВЫШЕ пика — не «перелёт через», препятствия там нет).

using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.PeacefulShip.Core
{
    /// <summary>Один пик: локальные координаты + радиус. Мир — резолвом вживую.</summary>
    public struct PeakDisc
    {
        public int terrainIdx; // индекс террейна в _terrains (каждый пик — свой)
        public float nx;      // 0..1 по X террейна
        public float nz;      // 0..1 по Z террейна
        public float localY;  // метры над origin террейна (вершина)
        public float radius;  // метры (аппроксимация склона)
    }

    public static class PeakRegistry
    {
        // Настройки извлечения (централизовано, не magic: см. Build).
        private const int SampleGrid = 200;          // сетка сэмпла heightmap на террейн
        private const float MinProminence = 150f;    // мин. превышение над окружением (м)
        private const float MinSeparation = 1500f;   // мин. дистанция между пиками (м)
        private const float RadiusMin = 300f;        // мин. радиус диска (м)
        private const float RadiusMax = 2000f;       // макс. радиус диска (м)
        // T-NS-MESH01: меши-скалы — только крупные (мелочь добирает лидар).
        private const float MeshMinSpan = 200f;      // мин. XZ-диагональ меша (м)
        private const float MeshRadiusPad = 1.2f;    // запас радиуса от габарита

        private static readonly List<PeakDisc> _peaks = new List<PeakDisc>(64);
        private static readonly List<Terrain> _terrains = new List<Terrain>(4);
        // T-NS-MESH01: меш-диски — Transform + локальные данные (FO-safe: мир едет,
        // трансформы едут вместе с корнями сцен; мир резолвится вживую).
        private struct MeshDisc
        {
            public Transform t;          // null = меш удалён (чистим при резолве)
            public Vector3 localCenter;  // центр bounds в локале трансформа (на момент бейка)
            public float radius;         // метры (предположение: скейл ~1, как у скал сцены)
            public float heightAboveCenter; // верх bounds над центром, мировые метры (бейк)
        }
        private static readonly List<MeshDisc> _meshes = new List<MeshDisc>(64);
        private static bool _meshCacheValid;
        private static bool _built;

        public static int Count => _peaks.Count;

        /// <summary>Построить один раз (лениво, при первом планировании). Server-only.</summary>
        public static void Build()
        {
            if (_built) return;
            _built = true;
            _terrains.Clear();
            _terrains.AddRange(Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude));
            for (int i = 0; i < _terrains.Count; i++)
            {
                var t = _terrains[i];
                if (t == null || t.terrainData == null) continue;
                ExtractPeaks(i, t);
            }
            BuildMeshes(); // T-NS-MESH01: меши-скалы тем же заходом
            Debug.Log($"[PeakRegistry] T-NS-NAV16/MESH01 built: {_peaks.Count} peaks + {_meshes.Count} meshes over {_terrains.Count} terrain(s)");
        }

        /// <summary>
        /// T-NS-MESH01: скан мешей-препятствий в диски. Триггеры (пады!) и мелочь —
        /// мимо; корабли — не стены (как в лидаре). Server-only, вызывается из Build
        /// и повторно после F8 (см. InvalidateMeshCache).
        /// </summary>
        public static void BuildMeshes()
        {
            _meshes.Clear();
            var colliders = Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Exclude);
            for (int i = 0; i < colliders.Length; i++)
            {
                var c = colliders[i];
                if (c == null || !c.enabled || c.isTrigger) continue;
                if (c.GetComponentInParent<Player.ShipController>() != null) continue;
                if (c.GetComponentInParent<Stations.NpcShipController>() != null) continue;
                var b = c.bounds; // мировые (на момент бейка)
                float span = Mathf.Sqrt(b.size.x * b.size.x + b.size.z * b.size.z);
                if (span < MeshMinSpan) continue;
                var tr = c.transform;
                _meshes.Add(new MeshDisc
                {
                    t = tr,
                    localCenter = tr.InverseTransformPoint(b.center),
                    radius = Mathf.Min(span * 0.5f * MeshRadiusPad, RadiusMax),
                    heightAboveCenter = b.max.y - b.center.y
                });
            }
            _meshCacheValid = true;
        }

        /// <summary>
        /// T-NS-MESH01: инвалидация меш-кэша после сдвига мира. Вызывается один раз
        /// за сдвиг из NpcShipTrafficManager.ApplyRebaseTranslation (центрально,
        /// не per-ship). Следующий запрос перестроит кэш резолвом живых трансформов.
        /// </summary>
        public static void InvalidateMeshCache()
        {
            _meshCacheValid = false;
        }

        /// <summary>Резолв меш-диска в мир (false = меш удалён, дропнуть).</summary>
        private static bool ResolveMeshDisc(MeshDisc m, out Vector3 center, out float radius, out float topY)
        {
            center = Vector3.zero;
            radius = 0f;
            topY = 0f;
            if (m.t == null) return false;
            center = m.t.TransformPoint(m.localCenter);
            radius = m.radius;
            topY = center.y + m.heightAboveCenter;
            return true;
        }

        private static void EnsureMeshCache()
        {
            if (_meshCacheValid) return;
            BuildMeshes();
        }

        /// <summary>
        /// Найти диск, пересекающий отрезок (с полем margin). Возврат — world-центр/радиус.
        /// Пик ниже профиля (profileY - clearance) — не препятствие, пропускаем.
        /// </summary>
        public static bool FindBlockingDisc(Vector3 a, Vector3 b, float profileY, float margin,
            out Vector3 center, out float radius)
        {
            Build();
            EnsureMeshCache(); // T-NS-MESH01: кэш мог протухнуть при F8
            center = Vector3.zero;
            radius = 0f;
            float bestT = float.MaxValue;
            bool found = false;
            for (int j = 0; j < _peaks.Count; j++)
            {
                var p = _peaks[j];
                if (p.terrainIdx < 0 || p.terrainIdx >= _terrains.Count) continue;
                var t = _terrains[p.terrainIdx];
                if (t == null || t.terrainData == null) continue;
                Vector3 org = t.transform.position;
                Vector3 size = t.terrainData.size;
                Vector3 c = new Vector3(org.x + p.nx * size.x, org.y + p.localY, org.z + p.nz * size.z);
                if (c.y < profileY - margin) continue; // пик ниже профиля — летим прямо
                float r = p.radius + margin;
                if (SegmentDist(a, b, c) < r)
                {
                    // Ближайшее пересечение к началу (параметр t) — обходим по порядку.
                    float tt = SegmentParam(a, b, c);
                    if (tt < bestT) { bestT = tt; center = c; radius = r; found = true; }
                }
            }
            // T-NS-MESH01: меши тем же предикатом (верх вместо вершины).
            for (int j = _meshes.Count - 1; j >= 0; j--)
            {
                if (!ResolveMeshDisc(_meshes[j], out Vector3 c, out float mr, out float topY))
                {
                    _meshes.RemoveAt(j);
                    continue;
                }
                if (topY < profileY - margin) continue; // козырёк ниже профиля — летим прямо
                float r = mr + margin;
                if (SegmentDist(a, b, c) < r)
                {
                    float tt = SegmentParam(a, b, c);
                    if (tt < bestT) { bestT = tt; center = c; radius = r; found = true; }
                }
            }
            return found;
        }

        /// <summary>
        /// T-NS-DOCK01: цель внутри диска (станция в горе) — пики там не обходим,
        /// заход ведёт Berthing напрямую. Возврат — world-центр/радиус диска-дома.
        /// </summary>
        public static bool IsInsideDisc(Vector3 pos, float margin,
            out Vector3 center, out float radius)
        {
            Build();
            EnsureMeshCache(); // T-NS-MESH01
            center = Vector3.zero;
            radius = 0f;
            for (int j = 0; j < _peaks.Count; j++)
            {
                var p = _peaks[j];
                if (p.terrainIdx < 0 || p.terrainIdx >= _terrains.Count) continue;
                var t = _terrains[p.terrainIdx];
                if (t == null || t.terrainData == null) continue;
                Vector3 org = t.transform.position;
                Vector3 size = t.terrainData.size;
                Vector3 c = new Vector3(org.x + p.nx * size.x, org.y + p.localY, org.z + p.nz * size.z);
                float r = p.radius + margin;
                Vector2 d = new Vector2(pos.x - c.x, pos.z - c.z);
                if (d.magnitude < r) { center = c; radius = r; return true; }
            }
            // T-NS-MESH01: станция внутри городской геометрии — тоже «дом».
            for (int j = _meshes.Count - 1; j >= 0; j--)
            {
                if (!ResolveMeshDisc(_meshes[j], out Vector3 c, out float mr, out _))
                {
                    _meshes.RemoveAt(j);
                    continue;
                }
                float r = mr + margin;
                Vector2 d = new Vector2(pos.x - c.x, pos.z - c.z);
                if (d.magnitude < r) { center = c; radius = r; return true; }
            }
            return false;
        }

        /// <summary>
        /// T-NS-GRAPH01: все диски, пересекающие отрезок (для графа трасс).
        /// Порядок не гарантирован (граф сортирует по t). Пик ниже профиля —
        /// не препятствие (как в FindBlockingDisc). Мир — резолвом вживую.
        /// </summary>
        public static void CollectBlockingDiscs(Vector3 a, Vector3 b, float profileY, float margin,
            List<(Vector3 c, float r, float t)> results, int maxCount)
        {
            Build();
            EnsureMeshCache(); // T-NS-MESH01
            results.Clear();
            for (int j = 0; j < _peaks.Count && results.Count < maxCount; j++)
            {
                var p = _peaks[j];
                if (p.terrainIdx < 0 || p.terrainIdx >= _terrains.Count) continue;
                var t = _terrains[p.terrainIdx];
                if (t == null || t.terrainData == null) continue;
                Vector3 org = t.transform.position;
                Vector3 size = t.terrainData.size;
                Vector3 c = new Vector3(org.x + p.nx * size.x, org.y + p.localY, org.z + p.nz * size.z);
                if (c.y < profileY - margin) continue; // пик ниже профиля — летим прямо
                float r = p.radius + margin;
                if (SegmentDist(a, b, c) < r)
                    results.Add((c, r, SegmentParam(a, b, c)));
            }
            // T-NS-MESH01: меши тем же предикатом (верх вместо вершины).
            for (int j = _meshes.Count - 1; j >= 0 && results.Count < maxCount; j--)
            {
                if (!ResolveMeshDisc(_meshes[j], out Vector3 c, out float mr, out float topY))
                {
                    _meshes.RemoveAt(j);
                    continue;
                }
                if (topY < profileY - margin) continue;
                float r = mr + margin;
                if (SegmentDist(a, b, c) < r)
                    results.Add((c, r, SegmentParam(a, b, c)));
            }
        }

        // === Извлечение ===

        private static void ExtractPeaks(int terrainIdx, Terrain t)
        {
            var data = t.terrainData;
            int n = SampleGrid;
            float[,] h = new float[n, n];
            for (int ix = 0; ix < n; ix++)
                for (int iz = 0; iz < n; iz++)
                    h[ix, iz] = data.GetInterpolatedHeight((float)ix / (n - 1), (float)iz / (n - 1));

            int win = 2;      // окно локального максимума (3×3 → дальше подавление)
            int promWin = 12; // окно проминенции
            var cands = new List<(int x, int z, float h)>();
            for (int ix = win; ix < n - win; ix++)
                for (int iz = win; iz < n - win; iz++)
                {
                    float v = h[ix, iz];
                    bool isMax = true;
                    for (int dx = -win; dx <= win && isMax; dx++)
                        for (int dz = -win; dz <= win; dz++)
                        {
                            if (dx == 0 && dz == 0) continue;
                            if (h[ix + dx, iz + dz] >= v) { isMax = false; break; }
                        }
                    if (!isMax) continue;
                    float low = v;
                    for (int dx = -promWin; dx <= promWin; dx += 3)
                        for (int dz = -promWin; dz <= promWin; dz += 3)
                        {
                            int ax = Mathf.Clamp(ix + dx, 0, n - 1);
                            int az = Mathf.Clamp(iz + dz, 0, n - 1);
                            if (h[ax, az] < low) low = h[ax, az];
                        }
                    if (v - low >= MinProminence) cands.Add((ix, iz, v));
                }

            // Подавление близких: сильнее выживает.
            cands.Sort((x, y) => y.h.CompareTo(x.h));
            var kept = new List<(int x, int z, float h)>();
            float cellX = data.size.x / (n - 1);
            float cellZ = data.size.z / (n - 1);
            foreach (var c in cands)
            {
                bool dup = false;
                foreach (var k in kept)
                {
                    float dxm = (c.x - k.x) * cellX;
                    float dzm = (c.z - k.z) * cellZ;
                    if (dxm * dxm + dzm * dzm < MinSeparation * MinSeparation) { dup = true; break; }
                }
                if (!dup) kept.Add(c);
            }

            // Радиус: где склон падает ниже половины проминенции (8 направлений).
            foreach (var k in kept)
            {
                float half = k.h - MinProminence * 0.5f;
                float r = 0f;
                for (int d = 0; d < 8; d++)
                {
                    float ang = d * Mathf.PI / 4f;
                    float sx = Mathf.Sin(ang), sz = Mathf.Cos(ang);
                    for (float s = cellX; s < RadiusMax; s += cellX * 2f)
                    {
                        int ax = Mathf.Clamp(k.x + Mathf.RoundToInt(sx * s / cellX), 0, n - 1);
                        int az = Mathf.Clamp(k.z + Mathf.RoundToInt(sz * s / cellZ), 0, n - 1);
                        if (h[ax, az] < half) { r = Mathf.Max(r, s); break; }
                        r = Mathf.Max(r, s);
                    }
                }
                r = Mathf.Clamp(r, RadiusMin, RadiusMax);
                _peaks.Add(new PeakDisc
                {
                    terrainIdx = terrainIdx,
                    nx = (float)k.x / (n - 1),
                    nz = (float)k.z / (n - 1),
                    localY = k.h,
                    radius = r
                });
            }
        }

        // === Геометрия отрезков (XZ) ===

        private static float SegmentDist(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector2 ab = new Vector2(b.x - a.x, b.z - a.z);
            Vector2 ap = new Vector2(p.x - a.x, p.z - a.z);
            float len2 = ab.sqrMagnitude;
            if (len2 < 0.001f) return ap.magnitude;
            float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / len2);
            return (ap - ab * t).magnitude;
        }

        private static float SegmentParam(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector2 ab = new Vector2(b.x - a.x, b.z - a.z);
            Vector2 ap = new Vector2(p.x - a.x, p.z - a.z);
            float len2 = ab.sqrMagnitude;
            if (len2 < 0.001f) return 0f;
            return Mathf.Clamp01(Vector2.Dot(ap, ab) / len2);
        }
    }
}
