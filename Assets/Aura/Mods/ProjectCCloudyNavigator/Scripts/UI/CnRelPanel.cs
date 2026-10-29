// Cloudy Navigator — полноэкранный список отношений (мод v4-1/v4-2: всплывающий список репутации).
//
// Показываются только реальные данные: фракции из ReputationClientState с тирами из FactionDefinition
// (диапазон отображения −100…+100, решение №5) и люди из NpcAttitudeClientState (их диапазон остаётся −100…+200).
// Граф связей (кто кому что предлагает) в этот слой не входит: он строится по ассетам, а не по снапшотам, и
// переносится отдельным этапом — здесь окно не рисует рёбер, которых не может проверить.
using System.Text;
using ProjectCCloudyNavigator.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Репутация фракций и отношение людей: список с значениями, тирами и шкалами.</summary>
    public sealed class CnRelPanel
    {
        private readonly VisualElement _root;
        private readonly Label _factionCount;
        private readonly Label _npcCount;
        private readonly VisualElement _factions;
        private readonly VisualElement _npcs;

        private string _signature = string.Empty;

        public CnRelPanel()
        {
            _root = CnStyle.Box("cn-relp");

            var head = CnStyle.Row("cn-relp__head");
            head.Add(CnStyle.Label("РЕПУТАЦИЯ", "cn-relp__title"));
            head.Add(CnStyle.Label("фракции −100…+100 · люди −100…+200", "cn-relp__note"));
            head.Add(CnStyle.Box("cn-grow"));
            _factionCount = CnStyle.Label(string.Empty, "cn-relp__count");
            head.Add(_factionCount);
            _root.Add(head);

            var cols = CnStyle.Row("cn-relp__cols");
            cols.style.alignItems = Align.Stretch;

            var left = CnStyle.Box("cn-relp__col");
            var leftHead = CnStyle.Row("cn-relp__colhead");
            leftHead.Add(CnStyle.Label("ФРАКЦИИ", "cn-relp__colt"));
            left.Add(leftHead);
            _factions = CnStyle.Box("cn-relp__rows");
            left.Add(Wrap(_factions));
            cols.Add(left);

            var right = CnStyle.Box("cn-relp__col");
            right.AddToClassList("cn-relp__col--alt");
            var rightHead = CnStyle.Row("cn-relp__colhead");
            rightHead.Add(CnStyle.Label("ЛЮДИ", "cn-relp__colt"));
            _npcCount = CnStyle.Label(string.Empty, "cn-relp__coln");
            rightHead.Add(_npcCount);
            right.Add(rightHead);
            _npcs = CnStyle.Box("cn-relp__rows");
            right.Add(Wrap(_npcs));
            cols.Add(right);

            _root.Add(cols);

            _root.Add(CnStyle.Label(
                "связи между субъектами (фракция NPC, кто предлагает квест, кто с кем враждует) строятся по ассетам — " +
                "граф связей переносится отдельным этапом", "cn-relp__footer"));
        }

        public VisualElement Build() => _root;

        public void Open() => _signature = string.Empty;

        public void Refresh(CnDataFeed feed)
        {
            if (feed == null) return;

            string signature = Signature(feed);
            if (signature == _signature) return;
            _signature = signature;

            var factions = feed.factions ?? new CnFactionRow[0];
            var npcs = feed.npcs ?? new CnNpcRow[0];

            _factionCount.text = factions.Length > 0 ? factions.Length.ToString() : string.Empty;
            _npcCount.text = npcs.Length > 0 ? npcs.Length.ToString() : string.Empty;

            _factions.Clear();
            if (factions.Length == 0)
            {
                _factions.Add(CnStyle.Label("нет известных фракций", "cn-relp__empty"));
            }
            else
            {
                for (int i = 0; i < factions.Length; i++) _factions.Add(FactionRow(factions[i], i == 0));
            }

            _npcs.Clear();
            if (npcs.Length == 0)
            {
                _npcs.Add(CnStyle.Label("нет известных людей", "cn-relp__empty"));
            }
            else
            {
                for (int i = 0; i < npcs.Length; i++) _npcs.Add(NpcRow(npcs[i], i == 0));
            }
        }

        private static string Signature(CnDataFeed feed)
        {
            var sb = new StringBuilder(128);

            var factions = feed.factions;
            if (factions != null)
            {
                for (int i = 0; i < factions.Length; i++)
                    sb.Append(factions[i].wireId).Append(':').Append(factions[i].value).Append(':').Append(factions[i].tier).Append(';');
            }

            sb.Append('#');

            var npcs = feed.npcs;
            if (npcs != null)
            {
                for (int i = 0; i < npcs.Length; i++)
                    sb.Append(npcs[i].npcId).Append(':').Append(npcs[i].value).Append(';');
            }

            return sb.ToString();
        }

        private static ScrollView Wrap(VisualElement content)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("cn-relp__scroll");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.Add(content);
            return scroll;
        }

        // ------------------------------------------------------------------ строки

        /// <summary>Фракция: имя, тир и шкала от центра −100…+100.</summary>
        private static VisualElement FactionRow(CnFactionRow faction, bool first)
        {
            var row = CnStyle.Box("cn-relp__row");
            if (first) row.AddToClassList("is-first");

            var top = CnStyle.Row("cn-relp__top");
            top.Add(CnStyle.Label(faction.displayName, "cn-rep__name"));

            var tier = CnStyle.Label(faction.hasTiers ? faction.tier : "тиры не заданы", "cn-rep__tier");
            tier.style.color = faction.hasTiers ? faction.tierColor : CnStyle.Dim;
            top.Add(tier);

            top.Add(CnStyle.Label((faction.value > 0 ? "+" : string.Empty) + faction.value, "cn-rep__val"));
            row.Add(top);

            var bar = CnStyle.Box("cn-relp__bar");
            CnStyle.Fill(bar, CnStyle.VeilEdge, 0.45f);

            var mid = CnStyle.Deco("cn-relp__mid");
            bar.Add(mid);

            float v = Mathf.Clamp(faction.value, -100f, 100f) / 100f;
            var fill = CnStyle.Deco("cn-rep__fill");
            fill.style.left = Length.Percent(v >= 0f ? 50f : 50f + v * 50f);
            fill.style.width = Length.Percent(Mathf.Abs(v) * 50f);
            fill.style.backgroundColor = faction.hasTiers ? faction.tierColor : CnStyle.Muted;
            bar.Add(fill);
            row.Add(bar);

            row.tooltip = faction.displayName + " · " + faction.value + " (−100…+100)";
            return row;
        }

        /// <summary>
        /// Человек: значение отношения −100…+200. Шкала трёхзонная, ноль на трети — так же, как считается
        /// серверное отношение (03 §4), без своих порогов и подписей.
        /// </summary>
        private static VisualElement NpcRow(CnNpcRow npc, bool first)
        {
            var row = CnStyle.Box("cn-relp__row");
            if (first) row.AddToClassList("is-first");

            var top = CnStyle.Row("cn-relp__top");
            top.Add(CnStyle.Label(string.IsNullOrEmpty(npc.displayName) ? npc.npcId : npc.displayName, "cn-rep__name"));

            var value = CnStyle.Label((npc.value > 0 ? "+" : string.Empty) + npc.value, "cn-rep__val");
            value.style.color = npc.value < 0 ? CnStyle.DangerInk : npc.value > 0 ? CnStyle.Accent : CnStyle.Ink;
            top.Add(value);
            row.Add(top);

            var bar = CnStyle.Box("cn-relp__bar");
            CnStyle.Fill(bar, CnStyle.VeilEdge, 0.45f);

            var mid = CnStyle.Deco("cn-relp__mid");
            mid.AddToClassList("cn-relp__mid--zero");
            bar.Add(mid);

            const float zero = 100f / 300f;   // 0 отношения на шкале −100…+200
            float pct = Mathf.Clamp((npc.value + 100f) / 300f, 0f, 1f);

            var fill = CnStyle.Deco("cn-rep__fill");
            fill.style.left = Length.Percent(Mathf.Min(zero, pct) * 100f);
            fill.style.width = Length.Percent(Mathf.Abs(pct - zero) * 100f);
            fill.style.backgroundColor = npc.value < 0 ? CnStyle.DangerLine : CnStyle.Accent;
            bar.Add(fill);
            row.Add(bar);

            row.tooltip = (string.IsNullOrEmpty(npc.displayName) ? npc.npcId : npc.displayName) + " · " + npc.value +
                          " (−100…+200)";
            return row;
        }
    }
}
