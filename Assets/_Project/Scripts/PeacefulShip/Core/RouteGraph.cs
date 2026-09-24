// T-NS-GRAPH01: граф трасс вокруг пиков — структурное курсирование (server-only).
// См. docs/NPC_others_peacfull/npc_ship/14_ROUTE_GRAPH_DESIGN.md.
//
// Зачем: PlanRoute умел «прямая + до 2 дисков + до 2 bypass» — хребет из 3+ пиков
// и воронки планом не разрешались, каждый leg заново открывал географию.
// Граф: кольцо из гейтов вокруг каждого блокирующего диска + Дейкстра
// «старт → гейты → цель» по чистым прямым (тот же фильтр стен, что лидар).
// Локальный steering (WallFollow/scatter/divert) остаётся страховкой.
//
// F8-безопасно по построению: гейты считаются вживую каждый PlanRoute,
// мировых координат не кэшируем (паттерн GateApproach). Вертикаль — всегда
// ProfileY (лор: облетаем, не перелетаем). Диски ниже профиля игнорируются
// в PeakRegistry (пролёт выше пика — не препятствие).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.PeacefulShip.Core
{
    /// <summary>
    /// Статический построитель маршрута по кольцам гейтов вокруг дисков пиков.
    /// Без состояния: вход — отрезок + диски, выход — точки обхода (без цели).
    /// </summary>
    public static class RouteGraph
    {
        private const int GatesPerDisc = 8; // кольцо гейтов вокруг диска
        private const int MaxDiscs = 8;     // больше — идём legacy-петлёй

        /// <summary>
        /// Построить точки обхода. true = граф дал путь (может быть пустым:
        /// прямая чиста — летим прямо). false = граф неприменим, идти legacy.
        /// legClear — та же проверка прямой, что LegClear контроллера.
        /// legClearGoal — lenient-проверка финального прыжка (геометрия станции
        /// у цели — не стена); null = как legClear.
        /// gateValid — гейт стоит в открытом месте (не зарыт в склон); null = все ок.
        /// hotspot — штраф HotspotPenalty (память заторов) или null.
        /// paritySide: +1 (чётный id) / −1 (нечётный) — развод встречных.
        /// failReason: ok-direct / ok / no-discs / too-many-discs /
        /// layer-empty / no-path / too-long (для Nav-лога).
        /// </summary>
        public static bool TryBuild(Vector3 a, Vector3 b, float y,
            float gateMargin, int maxWp,
            Func<Vector3, Vector3, bool> legClear, Func<Vector3, Vector3, bool> legClearGoal,
            Func<Vector3, bool> gateValid, Func<Vector3, float> hotspot,
            float paritySide, List<Vector3> outWaypoints, out int discCount, out string failReason)
        {
            outWaypoints.Clear();
            discCount = 0;
            failReason = "no-discs";
            if (legClear == null) return false;
            if ((b - a).sqrMagnitude < 1f) { failReason = "ok-direct"; return true; }
            if (legClear(a, b)) { failReason = "ok-direct"; return true; }

            // Диски, пересекающие отрезок (по порядку от старта).
            var discs = new List<(Vector3 c, float r, float t)>(MaxDiscs + 1);
            PeakRegistry.CollectBlockingDiscs(a, b, y, gateMargin, discs, MaxDiscs + 1);
            if (discs.Count == 0) return false;  // мешает меш, не диск — legacy-зонд
            discCount = discs.Count;
            if (discs.Count > MaxDiscs) { failReason = "too-many-discs"; return false; }
            discs.Sort((x, y2) => x.t.CompareTo(y2.t));
            var HopClear = legClearGoal ?? legClear;

            // Слои гейтов: слой i — кольцо вокруг discs[i].
            // T-NS-LOG02: зарытые гейты (внутри склона/меша) отбрасываем сразу —
            // иначе Дейкстра честно не находит пути, а legacy коммитит вслепую.
            Vector2 perp = PerpDir(a, b);
            var layers = new List<Vector3>(discs.Count * GatesPerDisc);
            var layerOf = new List<int>(discs.Count * GatesPerDisc);
            for (int i = 0; i < discs.Count; i++)
            {
                float rr = discs[i].r + gateMargin;
                int added = 0;
                for (int g = 0; g < GatesPerDisc; g++)
                {
                    float ang = g * Mathf.PI * 2f / GatesPerDisc;
                    Vector3 gate = new Vector3(
                        discs[i].c.x + Mathf.Cos(ang) * rr, y,
                        discs[i].c.z + Mathf.Sin(ang) * rr);
                    if (gateValid != null && !gateValid(gate)) continue;
                    layers.Add(gate);
                    layerOf.Add(i);
                    added++;
                }
                if (added == 0) { failReason = "layer-empty"; return false; }
            }

            int n = layers.Count;
            // Дейкстра: узлы 0 = старт, 1..n = гейты, n+1 = цель.
            // Рёбра: старт → слой 0 (+ пропуск слоя), слой i → i+1 (+ через один),
            // последний слой → цель. Полный меш не строим (экономия лучей).
            int start = 0, goal = n + 1;
            float[] dist = new float[n + 2];
            int[] prev = new int[n + 2];
            for (int i = 0; i < dist.Length; i++) { dist[i] = float.MaxValue; prev[i] = -1; }
            dist[start] = 0f;

            var open = new List<int> { start };
            var closed = new bool[n + 2];
            while (open.Count > 0)
            {
                int u = -1;
                float best = float.MaxValue;
                for (int i = 0; i < open.Count; i++)
                    if (dist[open[i]] < best) { best = dist[open[i]]; u = open[i]; }
                open.Remove(u);
                if (u == goal || u < 0) break;
                if (closed[u]) continue;
                closed[u] = true;

                Vector3 up = u == start ? a : layers[u - 1];
                int ul = u == start ? -1 : layerOf[u - 1];
                // Кандидаты: следующий слой, слой через один, цель (с последнего/предпоследнего).
                for (int v = 1; v <= n; v++)
                {
                    int vl = layerOf[v - 1];
                    if (vl <= ul) continue;
                    if (vl > ul + 2) continue;
                    if (closed[v]) continue;
                    if (!legClear(up, layers[v - 1])) continue;
                    float w = Vector3.Distance(up, layers[v - 1]) + NodeCost(layers[v - 1], discs[vl], perp, paritySide, hotspot);
                    if (dist[u] + w < dist[v])
                    {
                        dist[v] = dist[u] + w;
                        prev[v] = u;
                        if (!open.Contains(v)) open.Add(v);
                    }
                }
                // Цель достижима с последних двух слоёв (и со старта — проверено выше чистотой прямой).
                // T-NS-LOG02: lenient-финал — хит у самой цели (геометрия станции,
                // как в HasLineOfSight) — не стена: граф дотягивается до порога горы-дома.
                if (ul >= discs.Count - 2)
                {
                    if (!closed[goal] && HopClear(up, b))
                    {
                        float w = Vector3.Distance(up, b);
                        if (dist[u] + w < dist[goal]) { dist[goal] = dist[u] + w; prev[goal] = u; }
                        if (!open.Contains(goal)) open.Add(goal);
                    }
                }
            }

            if (prev[goal] < 0) { failReason = "no-path"; return false; } // пути нет — legacy
            // Восстановить цепочку гейтов (без старта/цели), в порядке полёта.
            var chain = new List<int>(8);
            for (int v = prev[goal]; v > start; v = prev[v]) chain.Add(v);
            chain.Reverse();
            if (chain.Count > Math.Max(1, maxWp)) { failReason = "too-long"; return false; }
            for (int i = 0; i < chain.Count; i++) outWaypoints.Add(layers[chain[i] - 1]);
            failReason = "ok";
            return true;
        }

        private static float NodeCost(Vector3 gate, (Vector3 c, float r, float t) disc,
            Vector2 perp, float paritySide, Func<Vector3, float> hotspot)
        {
            float cost = 0f;
            // Развод встречных: чётные тянет в одну сторону кольца, нечётных — в другую.
            Vector2 off = new Vector2(gate.x - disc.c.x, gate.z - disc.c.z);
            if (off.sqrMagnitude > 0.001f)
                cost += -paritySide * Vector2.Dot(off.normalized, perp) * 50f;
            if (hotspot != null) cost += hotspot(gate);
            return cost;
        }

        private static Vector2 PerpDir(Vector3 a, Vector3 b)
        {
            Vector2 d = new Vector2(b.x - a.x, b.z - a.z);
            if (d.sqrMagnitude < 1f) return Vector2.right;
            d.Normalize();
            return new Vector2(-d.y, d.x);
        }
    }
}
