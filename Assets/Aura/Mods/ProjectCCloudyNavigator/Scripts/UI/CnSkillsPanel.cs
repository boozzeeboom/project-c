// Cloudy Navigator — полноэкранный граф навыков (мод v4-2/4.3: боевое и социальное дерево).
//
// Две вкладки-бирки: БОЕВЫЕ / СОЦИАЛЬНЫЕ. Узлы — реальный каталог Resources/Skills с авторскими treeX/treeY:
// положение считается по шкале легаси SkillTreeWindow (treeX * 2.5 + 10), но от минимума каталога, иначе узлы
// с отрицательным treeY (combat_basic_spike, combat_basic_spinstrike) уезжают за верхний край содержимого.
// Рёбра — Painter2D 2 px от низа предпосылки к верху зависимого узла; зелёное ребро — предпосылка выучена.
//
// Видимость узлов ровно как у легаси (SocialSkillTreeWindow.IsSkillVisible): AlwaysVisible ‖ выучен ‖ известен.
// Окно не раскрывает скрытые знания и ничего не изучает: запроса изучения на клиенте нет, он идёт серверным RPC
// SkillsServer.RequestLearnSkillRpc из легаси-окна персонажа.
//
// Социальный граф дополнительно включает предпосылки, помеченные другим разделом (в ассетах social_basic_talk
// помечен боевым, хотя от него зависят «Торговля» и «Убеждение»): такой узел помечается «ДРУГОЙ РАЗДЕЛ»,
// иначе ребро некуда вести.
using System;
using System.Collections.Generic;
using System.Text;
using ProjectCCloudyNavigator.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Граф навыков: узлы каталога, связи предпосылок и разбор выбранного узла.</summary>
    public sealed class CnSkillsPanel
    {
        private const int ModeCombat = 0;
        private const int ModeSocial = 1;

        /// <summary>SkillCategory.Social = 0, Combat = 1 (ProjectC.Skills.SkillCategory).</summary>
        private const byte SocialCategory = 0;

        // Шкала легаси SkillTreeWindow: node.left = treeX * 2.5 + 10, node.top = treeY * 2.5 + 10.
        private const float Scale = 2.5f;
        private const float PadX = 10f;
        private const float PadY = 10f;
        private const float NodeW = 148f;
        private const float NodeH = 30f;

        /// <summary>Минимальный зазор между узлами одного ряда после разведения (D11).</summary>
        private const float RowGapX = 10f;

        private readonly VisualElement _root;
        private readonly VisualElement _tabs;
        private readonly ScrollView _scroll;
        private readonly VisualElement _graph;
        private readonly ScrollView _det;

        private readonly List<Edge> _edges = new List<Edge>(64);

        private int _mode = ModeCombat;
        private string _selected = string.Empty;
        private string _signature = string.Empty;

        private struct Edge
        {
            public Vector2 a;
            public Vector2 b;
            public bool on;
        }

        public CnSkillsPanel()
        {
            _root = CnStyle.Box("cn-skg");

            _tabs = CnStyle.Row("cn-skg__tabs");
            _root.Add(_tabs);

            var cols = CnStyle.Row("cn-skg__cols");
            cols.style.alignItems = Align.Stretch;

            _scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            _scroll.AddToClassList("cn-skg__scroll");
            _graph = CnStyle.Box("cn-skg__graph");
            _graph.generateVisualContent += OnGenerate;
            _scroll.Add(_graph);
            cols.Add(_scroll);

            _det = new ScrollView(ScrollViewMode.Vertical);
            _det.AddToClassList("cn-skg__det");
            _det.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            cols.Add(_det);

            _root.Add(cols);
        }

        public VisualElement Build() => _root;

        /// <summary>Открыть на нужном графе: true — социальные.</summary>
        public void Open(bool social)
        {
            int mode = social ? ModeSocial : ModeCombat;
            if (_mode != mode)
            {
                _mode = mode;
                _selected = string.Empty;
            }
            _signature = string.Empty;
        }

        public void Refresh(CnDataFeed feed)
        {
            if (feed == null) return;

            string signature = Signature(feed);
            if (signature == _signature) return;
            _signature = signature;

            Rebuild(feed);
        }

        // ------------------------------------------------------------------ данные

        /// <summary>Ровно гейт легаси: AlwaysVisible ‖ выучен ‖ известен.</summary>
        private static bool Visible(CnSkillNodeRow n) => n.alwaysVisible || n.learned || n.known;

        private string Signature(CnDataFeed feed)
        {
            var sb = new StringBuilder(320);
            sb.Append(_mode).Append('#').Append(_selected).Append('#');

            var all = feed.skillNodes;
            if (all == null) return sb.ToString();

            for (int i = 0; i < all.Length; i++)
            {
                var n = all[i];
                sb.Append(n.skillId).Append(Visible(n) ? 'v' : '-').Append(n.learned ? 'l' : '.')
                  .Append(n.category).Append(n.treeX).Append(',').Append(n.treeY).Append(';');
            }

            return sb.ToString();
        }

        private void Rebuild(CnDataFeed feed)
        {
            var all = feed.skillNodes ?? new CnSkillNodeRow[0];
            var byId = new Dictionary<string, CnSkillNodeRow>(all.Length);
            for (int i = 0; i < all.Length; i++) byId[all[i].skillId] = all[i];

            var picked = new List<CnSkillNodeRow>(all.Length);
            var foreign = new HashSet<string>();

            for (int i = 0; i < all.Length; i++)
            {
                var n = all[i];
                if (!Visible(n)) continue;
                if (_mode == ModeSocial && n.category != SocialCategory) continue;
                picked.Add(n);
            }

            if (_mode == ModeSocial)
            {
                int origin = picked.Count;
                for (int i = 0; i < origin; i++)
                {
                    var ids = picked[i].prerequisites;
                    if (ids == null) continue;

                    for (int k = 0; k < ids.Length; k++)
                    {
                        if (!byId.TryGetValue(ids[k], out var pre)) continue;
                        if (!Visible(pre) || pre.category == SocialCategory) continue;
                        if (foreign.Add(ids[k])) picked.Add(pre);
                    }
                }
            }

            BuildTabs(all, all.Length);
            BuildGraph(picked, byId, foreign);
            BuildDetail(picked, byId, foreign);
        }

        // ------------------------------------------------------------------ вкладки

        private void BuildTabs(CnSkillNodeRow[] all, int total)
        {
            _tabs.Clear();

            int combat = 0, social = 0;
            for (int i = 0; i < all.Length; i++)
            {
                var n = all[i];
                if (!Visible(n)) continue;
                if (n.category == SocialCategory) social++;
                else combat++;
            }

            _tabs.Add(Tab("БОЕВЫЕ " + combat, _mode == ModeCombat, true, () => SetMode(ModeCombat)));
            _tabs.Add(Tab("СОЦИАЛЬНЫЕ " + social, _mode == ModeSocial, false, () => SetMode(ModeSocial)));
            _tabs.Add(CnStyle.Box("cn-grow"));
            _tabs.Add(CnStyle.Label(total > 0
                ? "видно " + (combat + social) + " из " + total + " · остальные не открыты"
                : "каталог навыков не найден", "cn-skg__hint"));
        }

        private VisualElement Tab(string label, bool active, bool first, Action onSelect)
        {
            var tag = CnStyle.Box("cn-skg__tab");
            if (first) tag.AddToClassList("is-first");
            if (active) tag.AddToClassList("is-on");
            tag.Add(CnStyle.Label(label, "cn-skg__tab-t"));
            tag.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                onSelect();
            });
            return tag;
        }

        private void SetMode(int mode)
        {
            if (_mode == mode) return;
            _mode = mode;
            _selected = string.Empty;
            _signature = string.Empty;
        }

        private void Select(string skillId)
        {
            if (_selected == skillId) return;
            _selected = skillId;
            _signature = string.Empty;
        }

        // ------------------------------------------------------------------ граф

        private void BuildGraph(List<CnSkillNodeRow> picked, Dictionary<string, CnSkillNodeRow> byId,
            HashSet<string> foreign)
        {
            _edges.Clear();
            _graph.Clear();

            if (picked.Count == 0)
            {
                _graph.style.width = Length.Percent(100f);
                _graph.style.height = Length.Percent(100f);
                _graph.MarkDirtyRepaint();
                return;
            }

            int minX = int.MaxValue, minY = int.MaxValue;
            for (int i = 0; i < picked.Count; i++)
            {
                minX = Mathf.Min(minX, picked[i].treeX);
                minY = Mathf.Min(minY, picked[i].treeY);
            }

            var position = new Dictionary<string, Vector2>(picked.Count);
            for (int i = 0; i < picked.Count; i++)
            {
                var n = picked[i];
                position[n.skillId] = new Vector2((n.treeX - minX) * Scale + PadX, (n.treeY - minY) * Scale + PadY);
            }

            SpreadRows(picked, position);

            // Полотно считается по разрешённым позициям: разведённые вправо узлы не должны вылезти за край.
            float right = 0f, bottom = 0f;
            foreach (var p in position.Values)
            {
                right = Mathf.Max(right, p.x + NodeW);
                bottom = Mathf.Max(bottom, p.y + NodeH);
            }
            _graph.style.width = Mathf.Max(right + PadX, 240f);
            _graph.style.height = Mathf.Max(bottom + PadY, 160f);

            // Рёбра: от низа предпосылки к верху зависимого — как в легаси.
            for (int i = 0; i < picked.Count; i++)
            {
                var n = picked[i];
                if (n.prerequisites == null || n.prerequisites.Length == 0) continue;
                if (!position.TryGetValue(n.skillId, out var to)) continue;

                for (int k = 0; k < n.prerequisites.Length; k++)
                {
                    if (!position.TryGetValue(n.prerequisites[k], out var from)) continue;
                    if (!byId.TryGetValue(n.prerequisites[k], out var pre)) continue;

                    _edges.Add(new Edge
                    {
                        a = new Vector2(from.x + NodeW * 0.5f, from.y + NodeH),
                        b = new Vector2(to.x + NodeW * 0.5f, to.y),
                        on = pre.learned
                    });
                }
            }

            for (int i = 0; i < picked.Count; i++)
            {
                var n = picked[i];
                _graph.Add(MakeNode(n, position[n.skillId], foreign.Contains(n.skillId)));
            }

            _graph.MarkDirtyRepaint();
        }

        private VisualElement MakeNode(CnSkillNodeRow n, Vector2 at, bool foreign)
        {
            var node = CnStyle.Row("cn-skg__node");
            node.style.left = at.x;
            node.style.top = at.y;
            node.style.width = NodeW;
            node.style.height = NodeH;

            node.AddToClassList(n.learned ? "is-learned" : "is-known");
            if (foreign) node.AddToClassList("is-foreign");
            if (_selected == n.skillId) node.AddToClassList("is-sel");
            CnStyle.Fill(node, CnStyle.VeilSoft, n.learned ? 0.86f : 0.58f);

            node.Add(CnStyle.Label(n.learned ? "✓" : "○", "cn-skg__badge"));
            node.Add(CnStyle.Label(n.displayName, "cn-skg__title"));
            node.Add(CnStyle.Box("cn-grow"));
            node.Add(CnStyle.Label(n.active ? "A" : "P", n.active ? "cn-skg__type is-on" : "cn-skg__type"));

            node.tooltip = n.displayName + (n.learned ? " · выучен" : " · известен, не выучен");
            node.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                Select(n.skillId);
            });

            return node;
        }

        /// <summary>
        /// D11: в каталоге ranged_crossbow (50, 250) и ranged_reload (100, 250) стоят через 50 единиц — это 125 px
        /// при ширине узла 148 px, то есть нахлёст. Раздвигаем только реально наехавшие пары внутри одного ряда
        /// (treeY), порядок — порядок каталога, поэтому результат детерминирован.
        /// </summary>
        private static void SpreadRows(List<CnSkillNodeRow> picked, Dictionary<string, Vector2> position)
        {
            var rows = new Dictionary<int, List<string>>(picked.Count);
            for (int i = 0; i < picked.Count; i++)
            {
                var n = picked[i];
                if (!rows.TryGetValue(n.treeY, out var row)) rows[n.treeY] = row = new List<string>(4);
                row.Add(n.skillId);
            }

            foreach (var row in rows.Values)
            {
                row.Sort((a, b) => position[a].x.CompareTo(position[b].x));
                for (int i = 1; i < row.Count; i++)
                {
                    var prev = position[row[i - 1]];
                    var cur = position[row[i]];
                    float min = prev.x + NodeW + RowGapX;
                    if (cur.x < min) position[row[i]] = new Vector2(min, cur.y);
                }
            }
        }

        private void OnGenerate(MeshGenerationContext context)
        {
            if (_edges.Count == 0) return;

            var painter = context.painter2D;
            painter.lineWidth = 2f;
            painter.lineJoin = LineJoin.Miter;
            painter.lineCap = LineCap.Butt;

            for (int i = 0; i < _edges.Count; i++)
            {
                var edge = _edges[i];
                painter.strokeColor = edge.on
                    ? CnStyle.A(CnStyle.Accent, 0.90f)
                    : CnStyle.A(CnStyle.VeilEdge, 0.55f);
                painter.BeginPath();
                painter.MoveTo(edge.a);
                painter.LineTo(edge.b);
                painter.Stroke();
            }
        }

        // ------------------------------------------------------------------ разбор узла

        private void BuildDetail(List<CnSkillNodeRow> picked, Dictionary<string, CnSkillNodeRow> byId,
            HashSet<string> foreign)
        {
            _det.Clear();

            CnSkillNodeRow row = default;
            bool found = false;
            for (int i = 0; i < picked.Count; i++)
            {
                if (picked[i].skillId != _selected) continue;
                row = picked[i];
                found = true;
                break;
            }

            if (!found)
            {
                _det.Add(CnStyle.Label(picked.Count > 0 ? "ВЫБЕРИ УЗЕЛ" : "НЕЧЕГО ПОКАЗАТЬ", "cn-skg__det-hint"));
                _det.Add(CnStyle.Label(picked.Count > 0
                    ? "клик по узлу покажет состояние, цену изучения и предпосылки"
                    : "видимых навыков нет: знания о навыках открываются в игре, а не в этом окне",
                    "cn-skg__det-note"));
                return;
            }

            _det.Add(CnStyle.Label(row.displayName, "cn-skg__det-title"));

            var meta = CnStyle.Row("cn-skg__det-meta");
            meta.Add(CnStyle.Label(row.learned ? "ВЫУЧЕН" : "ИЗВЕСТЕН", "cn-skg__det-state"));
            meta.Add(CnStyle.Label(row.active ? "АКТИВНЫЙ" : "ПАССИВНЫЙ", "cn-skg__det-type"));
            if (foreign.Contains(row.skillId)) meta.Add(CnStyle.Label("ДРУГОЙ РАЗДЕЛ", "cn-skg__det-foreign"));
            _det.Add(meta);

            DetailRow("КУЛДАУН", row.active ? row.cooldown.ToString("0.0") + " с" : "не применяется");
            DetailRow("ИЗУЧЕНИЕ", row.learnXp > 0f ? row.learnXp.ToString("0") + " XP" : "бесплатно");

            var requirements = new StringBuilder(48);
            if (row.requiredStrength > 0) requirements.Append("STR ур. ").Append(row.requiredStrength).Append("  ");
            if (row.requiredDexterity > 0) requirements.Append("DEX ур. ").Append(row.requiredDexterity).Append("  ");
            if (row.requiredIntelligence > 0) requirements.Append("INT ур. ").Append(row.requiredIntelligence);
            DetailRow("ТРЕБУЕТ", requirements.Length > 0 ? requirements.ToString().TrimEnd() : "нет");

            if (!string.IsNullOrEmpty(row.description))
                _det.Add(CnStyle.Label(row.description, "cn-skg__det-desc"));

            _det.Add(CnStyle.Label("ПРЕДПОСЫЛКИ", "cn-skg__det-sec"));
            if (row.prerequisites == null || row.prerequisites.Length == 0)
            {
                _det.Add(CnStyle.Label("нет", "cn-skg__det-v"));
            }
            else
            {
                for (int i = 0; i < row.prerequisites.Length; i++)
                {
                    string id = row.prerequisites[i];
                    bool resolved = byId.TryGetValue(id, out var pre);
                    bool learnedPre = resolved && pre.learned;

                    string label = (learnedPre ? "✓ " : "✕ ") + (resolved ? pre.displayName : id);
                    if (!ContainsSkill(picked, id)) label += " · другой раздел";

                    _det.Add(CnStyle.Label(label, learnedPre ? "cn-skg__prereq is-on" : "cn-skg__prereq"));
                }
            }

            _det.Add(CnStyle.Label("изучение и назначение на слот — в окне персонажа (P); это окно только показывает",
                "cn-skg__det-note"));
        }

        private static bool ContainsSkill(List<CnSkillNodeRow> picked, string skillId)
        {
            for (int i = 0; i < picked.Count; i++)
                if (picked[i].skillId == skillId) return true;
            return false;
        }

        private void DetailRow(string key, string value)
        {
            var row = CnStyle.Row("cn-skg__det-row");
            row.Add(CnStyle.Label(key, "cn-skg__det-k"));
            row.Add(CnStyle.Box("cn-grow"));
            row.Add(CnStyle.Label(value, "cn-skg__det-v"));
            _det.Add(row);
        }
    }
}
