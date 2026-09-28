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
        private static readonly List<AirwaySegment> _segs = new List<AirwaySegment>(32);
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
            // Авто-связи по перекрытию объёмов + явные links.
            for (int i = 0; i < _segs.Count; i++)
            {
                if (_segs[i] == null) continue;
                Bounds bi = _segs[i].LiveBounds();
                for (int j = i + 1; j < _segs.Count; j++)
                {
                    if (_segs[j] == null) continue;
                    if (bi.Intersects(_segs[j].LiveBounds()))
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

        private static int Nearest(Vector3 p, float maxDist)
        {
            int best = -1;
            float bestD = maxDist;
            for (int i = 0; i < _segs.Count; i++)
            {
                var s = _segs[i];
                if (s == null) continue;
                // T-NS-AIR03b: дистанция до ОБЪЁМА, не до центра (лог 140210:
                // старт в 640 м от края 3-км бокса давал 2237 м до центра > entry
                // и молчаливый пас всех плеч). Внутри бокса — 0.
                Vector3 q = s.LiveBounds().ClosestPoint(p);
                float d = Vector3.Distance(p, q);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
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
        /// Маршрут по магистрали: точки-центры боксов (без цели).
        /// true = взять магистраль; false = legacy (причина в failReason).
        /// Короткие плечи и чистая прямая — не магистраль (см. дизайн 17 §2).
        /// </summary>
        public static bool TryBuildRoute(Vector3 a, Vector3 b,
            float minUseDist, float entryRadius, float maxDetour, int maxWp,
            List<Vector3> outWaypoints, out string failReason)
        {
            outWaypoints.Clear();
            failReason = "no-airways";
            EnsureBuilt();
            if (_segs.Count == 0) return false;
            float direct = Vector3.Distance(a, b);
            if (direct < 1f) { failReason = "short-hop"; return false; }
            if (direct < minUseDist) { failReason = "short-hop"; return false; }
            int entry = Nearest(a, entryRadius);
            int exit = Nearest(b, entryRadius);
            if (entry < 0 || exit < 0) { failReason = "no-entry"; return false; }
            if (entry == exit) { failReason = "same-box"; return false; }
            if (Linked(entry, exit)) { failReason = "near-box"; return false; }
            // Дейкстра по смежности (вес — дистанция центров, резолв вживую).
            int n = _segs.Count;
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
            if (prev[exit] < 0 && exit != entry) { failReason = "no-path"; return false; }
            var chain = new List<int>(8);
            for (int v = exit; v != entry && v >= 0; v = prev[v]) chain.Add(v);
            chain.Reverse();
            if (chain.Count == 0) { failReason = "no-path"; return false; }
            if (chain.Count > Mathf.Max(1, maxWp)) { failReason = "too-long"; return false; }
            // Стоимость: детур должен окупаться (магистраль в сторону — не тащит).
            // Краевые хопы — до объёма (бокс покрывает старт/финиш), внутри — по центрам.
            float ride = VolDist(a, _segs[entry]) + dist[exit] + VolDist(b, _segs[exit]);
            if (ride > direct * Mathf.Max(1.1f, maxDetour)) { failReason = "detour"; return false; }
            // T-NS-AIR02: точки — XZ центров боксов, Y — высота профиля вызывателя.
            // Магистраль задаёт ЛАТЕРАЛЬНУЮ топологию; вертикаль остаётся профилю
            // (эшелоны/лор): иначе бокс тянул бы всех на свою высоту и ломал эшелоны.
            for (int i = 0; i < chain.Count; i++)
            {
                Vector3 c = _segs[chain[i]].LiveCenter();
                outWaypoints.Add(new Vector3(c.x, a.y, c.z));
            }
            failReason = "ok";
            return true;
        }
    }
}
