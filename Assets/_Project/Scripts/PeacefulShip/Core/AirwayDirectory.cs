// T-NS-AIR01: реестр глобальных магистралей (дизайн 17_AIRWAYS_DESIGN.md).
// Стратегический слой PlanRoute: дальнее плечо по дизайнерским боксам.
// Server-only, static. FO-safe: храним ТРАНСФОРМЫ (центры — резолвом вживую,
// едут с корнями сцен), смежность — индексная (инвариантна к сдвигу).
// Перестроение не нужно при F8 — только при загрузке/выгрузке сцен
// (лениво: пустой реестр = no-op; явный Rebuild() — из кода сцены при нужде).

using System.Collections.Generic;
using UnityEngine;
using ProjectC.PeacefulShip.Stations;

namespace ProjectC.PeacefulShip.Core
{
    /// <summary>
    /// Граф боксов магистралей. Боксы — сертифицированная дизайнером чистота
    /// (лучами не верифицируем — в этом смысл ручного слоя).
    /// </summary>
    public static class AirwayDirectory
    {
        // T-NS-AIR03d: допуск стыковки боксов (зазор между объёмами, м).
        // 20% перекрытия физически неудобно держать (вопрос пользователя):
        // связываем и через разрыв до LinkGap — разрыв летит прямо под лидаром
        // (lookahead 150–600 м покрывает), тактика Downgrade'ит сюрпризы.
        // Большие разрывы — только явными links.
        private const float LinkGap = 600f;        private static readonly List<AirwaySegment> _segs = new List<AirwaySegment>(32);
        private static readonly List<List<int>> _adj = new List<List<int>>(32);
        private static bool _built;

        public static int Count => _segs.Count;

        private static bool _subscribed;

        /// <summary>
        /// T-NS-AIR02b: залипание пустого справочника — первый PlanRoute мог пройти
        /// до догрузки WorldScene (лог 110704: 0 air-строк при 6 боксах в файле).
        /// Любая загрузка сцены сбрасывает флаг — следующий запрос перестроит.
        /// </summary>
        private static void EnsureSubscribed()
        {
            if (_subscribed) return;
            _subscribed = true;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => _built = false;
        }

        /// <summary>Перестроить из сцены (первый вызов — автоматически).</summary>
        public static void Rebuild()
        {
            _built = true;
            _segs.Clear();
            _adj.Clear();
            var all = Object.FindObjectsByType<AirwaySegment>(FindObjectsInactive.Exclude);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                _segs.Add(all[i]);
                _adj.Add(new List<int>());
            }
            // Авто-связи: перекрытие ИЛИ зазор до LinkGap (расширенные AABB) + явные links.
            // Expand растёт в обе стороны: делим пополам, чтобы допуск был ровно LinkGap.
            for (int i = 0; i < _segs.Count; i++)
            {
                if (_segs[i] == null) continue;
                Bounds bi = _segs[i].LiveBounds();
                bi.Expand(LinkGap * 0.5f);
                for (int j = i + 1; j < _segs.Count; j++)
                {
                    if (_segs[j] == null) continue;
                    Bounds bj = _segs[j].LiveBounds();
                    bj.Expand(LinkGap * 0.5f);
                    if (bi.Intersects(bj))
                    {
                        _adj[i].Add(j);
                        _adj[j].Add(i);
                    }
                }
                var links = _segs[i].links;
                if (links == null) continue;
                for (int k = 0; k < links.Count; k++)
                {
                    int m = _segs.IndexOf(links[k]);
                    if (m >= 0 && m != i && !_adj[i].Contains(m))
                    {
                        _adj[i].Add(m);
                        _adj[m].Add(i);
                    }
                }
            }
            Debug.Log($"[AirwayDirectory] T-NS-AIR01 built: {_segs.Count} segments" +
                (_segs.Count == 0 ? " (сцена с боксами ещё не загружена? см. T-NS-AIR02b)" : ""));
        }

        private static void EnsureBuilt()
        {
            EnsureSubscribed();
            if (!_built) Rebuild();
        }

        private static bool Linked(int a, int b)
        {
            if (a < 0 || b < 0 || a >= _adj.Count || b >= _adj.Count) return false;
            return _adj[a].Contains(b);
        }

        /// <summary>Дистанция от точки до объёма бокса (внутри — 0).</summary>
        private static float VolDist(Vector3 p, AirwaySegment s)
        {
            if (s == null) return float.MaxValue;
            return Vector3.Distance(p, s.LiveBounds().ClosestPoint(p));
        }

        /// <summary>
        /// T-NS-AIR02b: живые LocationId станций бокса (для AIR03-роутинга и лора).
        /// Читается из DockStationController вживую — переименование/правка
        /// definition привязку не ломают. Пустые/битые ссылки пропускаются.
        /// </summary>
        public static List<string> ServedLocationIds(AirwaySegment seg)
        {
            var ids = new List<string>(2);
            if (seg == null || seg.stations == null) return ids;
            for (int i = 0; i < seg.stations.Count; i++)
            {
                var st = seg.stations[i];
                if (st == null) continue;
                string loc = st.LocationId;
                if (!string.IsNullOrEmpty(loc) && !ids.Contains(loc)) ids.Add(loc);
            }
            return ids;
        }

        /// <summary>
        /// T-NS-AIR03: маршрут по магистрали (дизайн 17 + модель пользователя).
        /// Влететь можно с любой стороны (вливание), выйти — съездом напротив цели;
        /// пересадки между несвязанными линиями — через links (развязки).
        /// true = взять магистраль (точки без цели); false = legacy (failReason).
        /// goalLocationId: hints выходов (stationIds крайних боксов); null/пусто —
        /// чистые дистанции (как раньше).
        /// </summary>
        public static bool TryBuildRoute(Vector3 a, Vector3 b, string goalLocationId,
            float minUseDist, float entryRadius, float maxDetour, int maxWp,
            List<Vector3> outWaypoints, out string failReason)
        {
            outWaypoints.Clear();
            failReason = "no-airways";
            EnsureBuilt();
            if (_segs.Count == 0) return false;
            float direct = Vector3.Distance(a, b);
            if (direct < 1f || direct < minUseDist) { failReason = "short-hop"; return false; }
            // Кандидаты: до 3 ближайших входов; выходы — сначала обслуживающие цель.
            var entries = NearestK(a, entryRadius, 3);
            var exits = ExitsFor(b, entryRadius, goalLocationId, 3);
            if (entries.Count == 0 || exits.Count == 0) { failReason = "no-entry"; return false; }
            float bestRide = float.MaxValue;
            List<int> bestChain = null;
            int bestEntry = -1;
            bool anyEligible = false;
            foreach (int en in entries)
            {
                foreach (int ex in exits)
                {
                    if (en == ex || Linked(en, ex)) continue; // свой/соседний — не магистраль
                    anyEligible = true;
                    if (!Dijkstra(en, ex, out List<int> chain, out float plen)) continue;
                    float ride = VolDist(a, _segs[en]) + plen + VolDist(b, _segs[ex]);
                    if (ride < bestRide) { bestRide = ride; bestChain = chain; bestEntry = en; }
                }
            }
            if (!anyEligible) { failReason = "near-box"; return false; }
            if (bestChain == null) { failReason = "no-path"; return false; }
            if (bestChain.Count > Mathf.Max(1, maxWp)) { failReason = "too-long"; return false; }
            if (bestRide > direct * Mathf.Max(1.1f, maxDetour)) { failReason = "detour"; return false; }
            // Точки: вливание (ближайшая точка входного бокса), середина — центры,
            // съезд (ближайшая точка выходного к цели). Y — профиль вызывателя.
            Vector3 merge = _segs[bestEntry].LiveBounds().ClosestPoint(a);
            outWaypoints.Add(new Vector3(merge.x, a.y, merge.z));
            for (int i = 0; i < bestChain.Count; i++)
            {
                int idx = bestChain[i];
                if (idx == bestEntry) continue;
                // Последний узел — съезд к цели, а не центр.
                Vector3 wp = (i == bestChain.Count - 1)
                    ? _segs[idx].LiveBounds().ClosestPoint(b)
                    : _segs[idx].LiveCenter();
                outWaypoints.Add(new Vector3(wp.x, a.y, wp.z));
            }
            failReason = "ok";
            return true;
        }

        /// <summary>До K ближайших сегментов (по объёму) в радиусе.</summary>
        private static List<int> NearestK(Vector3 p, float maxDist, int k)
        {
            var scored = new List<(int idx, float d)>(8);
            for (int i = 0; i < _segs.Count; i++)
            {
                if (_segs[i] == null) continue;
                float d = VolDist(p, _segs[i]);
                if (d <= maxDist) scored.Add((i, d));
            }
            scored.Sort((x, y) => x.d.CompareTo(y.d));
            var res = new List<int>(k);
            for (int i = 0; i < scored.Count && res.Count < k; i++) res.Add(scored[i].idx);
            return res;
        }

        /// <summary>
        /// Кандидаты выходов: сначала боксы, обслуживающие цель (stationIds),
        /// потом ближайшие. Съезд — напротив фермы, а не мимо неё.
        /// </summary>
        private static List<int> ExitsFor(Vector3 b, float maxDist, string goalLocationId, int k)
        {
            var serving = new List<(int idx, float d)>(8);
            var other = new List<(int idx, float d)>(8);
            bool wantHint = !string.IsNullOrEmpty(goalLocationId);
            for (int i = 0; i < _segs.Count; i++)
            {
                var s = _segs[i];
                if (s == null) continue;
                float d = VolDist(b, s);
                if (d > maxDist) continue;
                bool serves = false;
                if (wantHint)
                {
                    foreach (var loc in ServedLocationIds(s))
                        if (string.Equals(loc, goalLocationId, System.StringComparison.OrdinalIgnoreCase))
                        {
                            serves = true;
                            break;
                        }
                }
                (serves ? serving : other).Add((i, d));
            }
            serving.Sort((x, y) => x.d.CompareTo(y.d));
            other.Sort((x, y) => x.d.CompareTo(y.d));
            var res = new List<int>(k);
            foreach (var e in serving) { if (res.Count >= k) break; res.Add(e.idx); }
            foreach (var e in other) { if (res.Count >= k) break; res.Add(e.idx); }
            return res;
        }

        /// <summary>Дейкстра entry→exit по смежности (вес — дистанция центров вживую).</summary>
        private static bool Dijkstra(int entry, int exit, out List<int> chain, out float pathLen)
        {
            chain = null;
            pathLen = float.MaxValue;
            int n = _segs.Count;
            if (entry < 0 || exit < 0 || entry >= n || exit >= n) return false;
            float[] dist = new float[n];
            int[] prev = new int[n];
            bool[] closed = new bool[n];
            for (int i = 0; i < n; i++) { dist[i] = float.MaxValue; prev[i] = -1; }
            dist[entry] = 0f;
            var open = new List<int> { entry };
            while (open.Count > 0)
            {
                int u = -1;
                float best = float.MaxValue;
                for (int i = 0; i < open.Count; i++)
                    if (dist[open[i]] < best) { best = dist[open[i]]; u = open[i]; }
                open.Remove(u);
                if (u < 0 || u == exit) break;
                if (closed[u]) continue;
                closed[u] = true;
                var su = _segs[u];
                if (su == null) continue;
                Vector3 cu = su.LiveCenter();
                for (int k = 0; k < _adj[u].Count; k++)
                {
                    int v = _adj[u][k];
                    if (v < 0 || v >= n || closed[v]) continue;
                    var sv = _segs[v];
                    if (sv == null) continue;
                    float w = Vector3.Distance(cu, sv.LiveCenter());
                    if (dist[u] + w < dist[v])
                    {
                        dist[v] = dist[u] + w;
                        prev[v] = u;
                        if (!open.Contains(v)) open.Add(v);
                    }
                }
            }
            if (prev[exit] < 0) return false;
            chain = new List<int>(8);
            for (int v = exit; v != entry && v >= 0; v = prev[v]) chain.Add(v);
            chain.Reverse();
            if (chain.Count == 0) return false;
            pathLen = dist[exit];
            return true;
        }
    }
}
