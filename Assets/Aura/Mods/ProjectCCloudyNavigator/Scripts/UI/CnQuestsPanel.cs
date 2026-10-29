// Cloudy Navigator — полноразмерное окно «ЗАДАНИЯ» (мод v4-2: src/quests.js).
//
// Внутри — свои вкладки-бирки КВЕСТЫ / КОНТРАКТЫ со счётчиками, фильтры и две колонки: список слева, разбор справа.
// Всё берётся из CnDataFeed (QuestProgressDto / ContractDto), действия идут только через CnActions — тех же
// публичных запросов игры, что у легаси-окна. Отказа от квеста на сервере нет: бирка «ОТКАЗАТЬСЯ» отдаёт честное
// сообщение игры (ui.quest.reject_unavailable), сдача квеста из окна не вызывается — она идёт через диалог с NPC.
//
// Строки: квест 44 px (глиф состояния · имя · done/total), контракт 48 px (маршрут · ×кол-во · награда · таймер).
// Пересборка — только когда изменилась сигнатура данных, чтобы не рвать прокрутку на каждом кадре.
using System;
using System.Text;
using ProjectC.Quests;
using ProjectC.Trade;
using ProjectCCloudyNavigator.Actions;
using ProjectCCloudyNavigator.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Окно заданий: квесты и контракты, фильтры, деталь с целями и действиями.</summary>
    public sealed class CnQuestsPanel
    {
        private const int ModeQuests = 0;
        private const int ModeContracts = 1;

        private static readonly string[] QuestStates = { "обнаружен", "предложен", "активен", "завершён", "провален", "сдан" };
        private static readonly string[] ContractTypes = { "ОБЫЧНЫЙ", "СРОЧНЫЙ", "РАСПИСКА" };
        private static readonly string[] ContractStates = { "доступен", "принят", "выполнен", "провален" };

        private static readonly string[] QuestFilters = { "АКТИВНЫЕ", "ЗАВЕРШЁННЫЕ", "ОБНАРУЖЕННЫЕ" };
        private static readonly string[] ContractFilters = { "ПРИНЯТЫЕ", "ДОСТУПНЫЕ" };

        private readonly CnActions _actions;
        private readonly Action<CnActionResult> _onResult;
        private readonly CnTip _tip;

        private readonly VisualElement _root;
        private readonly VisualElement _tabs;
        private readonly VisualElement _filters;
        private readonly ScrollView _list;
        private readonly VisualElement _rows;
        private readonly VisualElement _detail;

        private CnDataFeed _feed;
        private int _mode;
        private int _filter;
        private string _selected = string.Empty;
        private string _signature = string.Empty;

        public CnQuestsPanel(CnActions actions, Action<CnActionResult> onResult, CnTip tip)
        {
            _actions = actions;
            _onResult = onResult;
            _tip = tip;

            _root = CnStyle.Box("cn-qp");

            _tabs = CnStyle.Row("cn-qp__tabs");
            _root.Add(_tabs);

            _filters = CnStyle.Row("cn-qp__filters");
            _root.Add(_filters);

            var cols = CnStyle.Row("cn-qp__cols");
            cols.style.alignItems = Align.Stretch;

            _list = new ScrollView(ScrollViewMode.Vertical);
            _list.AddToClassList("cn-qp__list");
            _list.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _rows = CnStyle.Box("cn-qp__rows");
            _list.Add(_rows);
            cols.Add(_list);

            _detail = CnStyle.Box("cn-qp__det");
            cols.Add(_detail);

            _root.Add(cols);
        }

        public VisualElement Build() => _root;

        /// <summary>Открыть на нужной вкладке и выделить строку (id линии: «q:questId» или «c:contractId»).</summary>
        public void Focus(string lineId)
        {
            if (string.IsNullOrEmpty(lineId)) return;

            if (lineId.StartsWith("c:", StringComparison.Ordinal))
            {
                _mode = ModeContracts;
                _filter = 0;
            }
            else
            {
                _mode = ModeQuests;
                _filter = 0;
            }

            _selected = lineId;
            _signature = string.Empty;
        }

        private void SetMode(int mode)
        {
            if (_mode == mode) return;
            _mode = mode;
            _filter = 0;
            _selected = string.Empty;
            _signature = string.Empty;
        }

        private void SetFilter(int filter)
        {
            if (_filter == filter) return;
            _filter = filter;
            _selected = string.Empty;
            _signature = string.Empty;
        }

        /// <summary>Кадр попапа: пересборка по сигнатуре (данные игры меняются под окном).</summary>
        public void Refresh(CnDataFeed feed)
        {
            _feed = feed;
            if (feed == null) return;

            string signature = Signature(feed);
            if (signature == _signature) return;
            _signature = signature;

            BuildTabs(feed);
            BuildFilters();
            BuildList(feed);
            BuildDetail(feed);
        }

        private string Signature(CnDataFeed feed)
        {
            var sb = new StringBuilder(160);
            sb.Append(_mode).Append('#').Append(_filter).Append('#').Append(_selected).Append('#');
            sb.Append(_actions.Zone.allowed ? '1' : '0').Append(_actions.Zone.marketLocationId).Append('#');

            if (_mode == ModeContracts)
            {
                var contracts = feed.contracts ?? new CnContractRow[0];
                for (int i = 0; i < contracts.Length; i++)
                {
                    var c = contracts[i];
                    sb.Append(c.contractId).Append(c.state).Append(c.isActive ? 'a' : 'p')
                      .Append(c.receiptCargoIssued ? 'y' : 'n').Append(Mathf.FloorToInt(c.timeRemaining))
                      .Append(c.reward).Append(c.quantity).Append(';');
                }
                return sb.ToString();
            }

            var quests = feed.quests ?? new CnQuestRow[0];
            for (int i = 0; i < quests.Length; i++)
            {
                var q = quests[i];
                sb.Append(q.questId).Append(q.state).Append(q.isTracked ? 't' : '-');
                var goals = q.goals ?? new CnQuestGoal[0];
                for (int g = 0; g < goals.Length; g++)
                    sb.Append(goals[g].currentValue).Append('/').Append(goals[g].requiredQuantity).Append(goals[g].completed ? 'c' : '-');
                sb.Append(';');
            }

            return sb.ToString();
        }

        // ------------------------------------------------------------------ шапка вкладок и фильтров

        private void BuildTabs(CnDataFeed feed)
        {
            _tabs.Clear();

            var quests = feed.quests ?? new CnQuestRow[0];
            var contracts = feed.contracts ?? new CnContractRow[0];

            // Подписи бирок — как в моде (src/quests.js:515,519): «ЗАДАНИЯ · N» и «КОНТРАКТЫ · N».
            var q = Tab("ЗАДАНИЯ · " + quests.Length, _mode == ModeQuests, true, () => SetMode(ModeQuests));
            if (_mode == ModeQuests) q.AddToClassList("is-first");
            _tabs.Add(q);

            var c = Tab("КОНТРАКТЫ · " + contracts.Length, _mode == ModeContracts, false, () => SetMode(ModeContracts));
            _tabs.Add(c);

            _tabs.Add(CnStyle.Box("cn-grow"));
            _tabs.Add(CnStyle.Label("клик по строке ленты открывает эту вкладку", "cn-qp__hint"));
        }

        private void BuildFilters()
        {
            _filters.Clear();
            var labels = _mode == ModeQuests ? QuestFilters : ContractFilters;

            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                var tag = Tab(labels[i], _filter == i, i == 0, () => SetFilter(index));
                _filters.Add(tag);
            }

            _filters.Add(CnStyle.Box("cn-grow"));
            _filters.Add(CnStyle.Label(_actions.Zone.allowed
                ? "зона обмена: " + _actions.Zone.label
                : "вне зоны обмена", "cn-qp__zone"));
        }

        private VisualElement Tab(string label, bool active, bool first, Action onSelect)
        {
            var tag = CnStyle.Box("cn-qp__tab");
            if (first) tag.AddToClassList("is-first");
            if (active) tag.AddToClassList("is-on");
            tag.Add(CnStyle.Label(label, "cn-qp__tab-t"));
            tag.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                onSelect();
            });
            return tag;
        }

        // ------------------------------------------------------------------ список

        private void BuildList(CnDataFeed feed)
        {
            _rows.Clear();

            if (_mode == ModeContracts) BuildContractList(feed);
            else BuildQuestList(feed);

            if (_rows.childCount == 0)
            {
                var empty = CnStyle.Box("cn-qp__empty");
                empty.Add(CnStyle.Label("НИЧЕГО НЕТ", "cn-qp__empty-t"));
                empty.Add(CnStyle.Label(_mode == ModeContracts
                    ? "контракты появляются на рынке"
                    : "квесты берутся в диалоге с NPC", "cn-qp__empty-s"));
                _rows.Add(empty);
            }
        }

        private void BuildQuestList(CnDataFeed feed)
        {
            var quests = feed.quests ?? new CnQuestRow[0];
            bool any = false;

            for (int i = 0; i < quests.Length; i++)
            {
                var q = quests[i];
                if (!QuestPasses(q)) continue;

                any = true;
                string id = "q:" + q.questId;
                _rows.Add(QuestRow(q, id));
            }

            if (!any) return;
        }

        private bool QuestPasses(CnQuestRow q)
        {
            byte active = (byte)QuestState.Active, offered = (byte)QuestState.Offered;
            byte completed = (byte)QuestState.Completed, turnedIn = (byte)QuestState.TurnedIn;
            byte failed = (byte)QuestState.Failed, discovered = (byte)QuestState.Discovered;

            switch (_filter)
            {
                case 1: return q.state == completed || q.state == turnedIn || q.state == failed;
                case 2: return q.state == discovered;
                default: return q.state == active || q.state == offered || q.isTracked;
            }
        }

        private VisualElement QuestRow(CnQuestRow q, string id)
        {
            var goals = q.goals ?? new CnQuestGoal[0];
            int done = 0;
            for (int i = 0; i < goals.Length; i++) if (goals[i].completed) done++;

            var row = CnStyle.Row("cn-qrow");
            row.AddToClassList("cn-qrow--quest");
            if (id == _selected) row.AddToClassList("is-selected");

            row.Add(CnStyle.Label(Glyph(q.state), "cn-qrow__g"));
            var name = CnStyle.Label(string.IsNullOrEmpty(q.displayName) ? q.questId : q.displayName, "cn-qrow__t");
            row.Add(name);

            if (q.isTracked) row.Add(CnStyle.Label("СЛЕДУЮ", "cn-qrow__follow"));

            row.Add(CnStyle.Box("cn-grow"));
            row.Add(CnStyle.Label(done + " / " + goals.Length, "cn-qrow__n"));
            row.Add(CnStyle.Label(q.state < QuestStates.Length ? QuestStates[q.state] : "?", "cn-qrow__s"));

            row.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                _selected = id;
                _signature = string.Empty;
            });
            return row;
        }

        private static string Glyph(byte state)
        {
            if (state == (byte)QuestState.Active) return "●";
            if (state == (byte)QuestState.Offered) return "◆";
            if (state == (byte)QuestState.Completed || state == (byte)QuestState.TurnedIn) return "✓";
            return "○";
        }

        private void BuildContractList(CnDataFeed feed)
        {
            var contracts = feed.contracts ?? new CnContractRow[0];

            for (int i = 0; i < contracts.Length; i++)
            {
                var c = contracts[i];
                bool active = c.isActive || c.state == (byte)ContractState.Active;
                if (_filter == 0 && !active) continue;
                if (_filter == 1 && active) continue;

                string id = "c:" + c.contractId;
                _rows.Add(ContractRow(c, active, id));
            }
        }

        private VisualElement ContractRow(CnContractRow c, bool active, string id)
        {
            var row = CnStyle.Row("cn-qrow");
            row.AddToClassList("cn-qrow--contract");
            if (id == _selected) row.AddToClassList("is-selected");

            row.Add(CnStyle.Label(active ? "◆" : "○", "cn-qrow__g"));

            var stack = CnStyle.Box("cn-qrow__stack");
            stack.Add(CnStyle.Label(c.fromLocationId + " → " + c.toLocationId, "cn-qrow__t"));
            stack.Add(CnStyle.Label("×" + c.quantity + " " + c.displayName, "cn-qrow__sub"));
            row.Add(stack);

            row.Add(CnStyle.Box("cn-grow"));
            row.Add(CnStyle.Label("+" + c.reward.ToString("0") + " CR", "cn-qrow__n"));

            string timer = c.timeLimit > 0f ? FormatTimer(c.timeRemaining) : "∞";
            var time = CnStyle.Label(timer, "cn-qrow__s");
            if (c.timeLimit > 0f && c.timeRemaining < 60f) time.AddToClassList("cn-qrow__s--alarm");
            row.Add(time);

            row.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                _selected = id;
                _signature = string.Empty;
            });
            return row;
        }

        // ------------------------------------------------------------------ деталь

        private void BuildDetail(CnDataFeed feed)
        {
            _detail.Clear();

            if (_mode == ModeContracts) BuildContractDetail(feed);
            else BuildQuestDetail(feed);
        }

        private void BuildQuestDetail(CnDataFeed feed)
        {
            var quests = feed.quests ?? new CnQuestRow[0];
            CnQuestRow quest = default;
            bool found = false;

            for (int i = 0; i < quests.Length; i++)
            {
                if ("q:" + quests[i].questId != _selected) continue;
                quest = quests[i];
                found = true;
                break;
            }

            if (!found)
            {
                _detail.Add(CnStyle.Label("ВЫБЕРИ ЗАДАНИЕ", "cn-qp__det-k"));
                _detail.Add(CnStyle.Label("слева — список; строка ленты тоже открывает нужную вкладку", "cn-qp__det-v"));
                return;
            }

            var goals = quest.goals ?? new CnQuestGoal[0];
            int done = 0;
            for (int i = 0; i < goals.Length; i++) if (goals[i].completed) done++;

            _detail.Add(CnStyle.Label(string.IsNullOrEmpty(quest.displayName) ? quest.questId : quest.displayName, "cn-qp__det-title"));

            var meta = CnStyle.Row("cn-qp__det-meta");
            meta.Add(CnStyle.Label(quest.state < QuestStates.Length ? QuestStates[quest.state] : "?", "cn-qp__det-state"));
            meta.Add(CnStyle.Label("прогресс " + done + " / " + goals.Length, "cn-qp__det-progress"));
            if (quest.isTracked) meta.Add(CnStyle.Label("СЛЕДУЮ", "cn-qp__det-track"));
            _detail.Add(meta);

            var list = CnStyle.Box("cn-qp__goals");
            for (int i = 0; i < goals.Length; i++)
            {
                var g = goals[i];
                var line = CnStyle.Row("cn-qp__goal");
                if (g.completed) line.AddToClassList("is-done");
                line.Add(CnStyle.Label(g.completed ? "☑" : "☐", "cn-qp__goal-g"));
                line.Add(CnStyle.Label(g.description, "cn-qp__goal-t"));
                line.Add(CnStyle.Box("cn-grow"));
                line.Add(CnStyle.Label("(" + g.currentValue + "/" + g.requiredQuantity + ")", "cn-qp__goal-n"));
                list.Add(line);
            }
            if (goals.Length == 0) list.Add(CnStyle.Label("целей в данных нет", "cn-qp__goal-t"));
            _detail.Add(list);

            string npc = feed.ResolveTurnInNpc(quest.questId);
            _detail.Add(CnStyle.Label(
                quest.state == (byte)QuestState.Completed
                    ? (string.IsNullOrEmpty(npc) ? "сдать в диалоге с NPC" : "сдать у " + npc)
                    : "награда и сдача — у NPC в диалоге; квестовую награду окно не показывает, в данных её нет",
                "cn-qp__det-note"));

            var acts = CnStyle.Row("cn-qp__acts");
            acts.style.flexWrap = Wrap.Wrap;

            if (quest.state == (byte)QuestState.Offered)
            {
                string id = quest.questId, name = quest.displayName;
                acts.Add(Tag("ВЗЯТЬ", null, "cn-tag--brass", null, () => _actions.AcceptQuest(id, name)));
                acts.Add(Tag("СЛЕДИТЬ", null, null, null, () => _actions.ToggleQuest(id, name)));
                acts.Add(Tag("ОТКАЗАТЬСЯ", null, "cn-tag--danger", null, () => _actions.RejectQuest()));
            }
            else if (quest.state == (byte)QuestState.Completed)
            {
                string id = quest.questId, name = quest.displayName;
                acts.Add(Tag("СЛЕДИТЬ", null, null, null, () => _actions.ToggleQuest(id, name)));
                acts.Add(Tag("СДАТЬ", null, null, "сдача идёт через диалог с NPC", null));
            }
            else
            {
                string id = quest.questId, name = quest.displayName;
                bool tracked = quest.isTracked;
                acts.Add(Tag(tracked ? "СНЯТЬ" : "СЛЕДИТЬ", null, null, null, () => _actions.ToggleQuest(id, name)));
            }

            _detail.Add(acts);
            _detail.Add(CnStyle.Label(quest.questId, "cn-qp__det-id"));
        }

        private void BuildContractDetail(CnDataFeed feed)
        {
            var contracts = feed.contracts ?? new CnContractRow[0];
            CnContractRow contract = default;
            bool found = false;

            for (int i = 0; i < contracts.Length; i++)
            {
                if ("c:" + contracts[i].contractId != _selected) continue;
                contract = contracts[i];
                found = true;
                break;
            }

            if (!found)
            {
                _detail.Add(CnStyle.Label("ВЫБЕРИ КОНТРАКТ", "cn-qp__det-k"));
                _detail.Add(CnStyle.Label("принятые видны всегда, доступные — на рынке отправления", "cn-qp__det-v"));
                return;
            }

            var c = contract;
            string type = c.type < ContractTypes.Length ? ContractTypes[c.type] : "?";
            string state = c.state < ContractStates.Length ? ContractStates[c.state] : "?";
            bool active = c.isActive || c.state == (byte)ContractState.Active;
            bool pending = c.state == (byte)ContractState.Pending;

            _detail.Add(CnStyle.Label("Доставка ×" + c.quantity + " " + c.displayName, "cn-qp__det-title"));

            var meta = CnStyle.Row("cn-qp__det-meta");
            meta.Add(CnStyle.Label(type, "cn-qp__det-state"));
            meta.Add(CnStyle.Label(state, "cn-qp__det-progress"));
            meta.Add(CnStyle.Label(c.timeLimit > 0f ? "осталось " + FormatTimer(c.timeRemaining) : "без срока", "cn-qp__det-track"));
            _detail.Add(meta);

            var rows = CnStyle.Box("cn-qp__goals");
            rows.Add(DetailLine("МАРШРУТ", c.fromLocationId + " → " + c.toLocationId));
            rows.Add(DetailLine("ГРУЗ", "×" + c.quantity + " " + c.displayName));
            rows.Add(DetailLine("НАГРАДА", "+" + c.reward.ToString("0") + " CR"));
            if (c.isReceiptContract) rows.Add(DetailLine("РАСПИСКА", c.receiptCargoIssued ? "груз выдан" : "груз не выдан"));
            _detail.Add(rows);

            var acts = CnStyle.Row("cn-qp__acts");
            acts.style.flexWrap = Wrap.Wrap;

            string id = c.contractId;
            byte st = c.state;

            if (pending)
            {
                bool here = _actions.Zone.allowed && _actions.Zone.isMarket &&
                            string.Equals(_actions.Zone.marketLocationId, c.fromLocationId, StringComparison.OrdinalIgnoreCase);
                acts.Add(Tag("ВЗЯТЬ", "+" + c.reward.ToString("0") + " CR", "cn-tag--brass",
                    here ? null : "контракт берут на рынке " + c.fromLocationId,
                    () => _actions.AcceptContract(id, st)));
            }
            else if (active)
            {
                if (c.isReceiptContract && !c.receiptCargoIssued)
                    acts.Add(Tag("ПОЛУЧИТЬ ГРУЗ", null, null, null, () => _actions.ReceiveCargo(id, st, true, false)));
                else
                    acts.Add(Tag("СДАТЬ", "+" + c.reward.ToString("0") + " CR", "cn-tag--brass", null, () => _actions.CompleteContract(id, st)));

                acts.Add(Tag("ПРОВАЛИТЬ", null, "cn-tag--danger", null, () => _actions.FailContract(id, st)));
            }

            _detail.Add(acts);
            _detail.Add(CnStyle.Label(c.contractId, "cn-qp__det-id"));
        }

        private static VisualElement DetailLine(string key, string value)
        {
            var line = CnStyle.Row("cn-qp__goal");
            line.Add(CnStyle.Label(key, "cn-qp__det-k"));
            line.Add(CnStyle.Box("cn-grow"));
            line.Add(CnStyle.Label(value, "cn-qp__det-v"));
            return line;
        }

        /// <summary>Бирка действия: результат уходит в строку «СЕЙЧАС» главного окна.</summary>
        private VisualElement Tag(string verb, string value, string modifier, string disabled, Func<CnActionResult> run)
        {
            var tag = CnStyle.Tag(verb, value, modifier, 0f);
            if (disabled != null)
            {
                tag.AddToClassList("cn-tag--off");
                tag.tooltip = null;

                // Карточка запертой бирки (quests.js tag title): причина берётся из того же сообщения игры, что в «СЕЙЧАС».
                _tip?.Hover(tag, () => CnTipCard.Make(verb, value).Row("состояние", "недоступно").Foot(disabled, 1));
            }

            tag.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                if (disabled != null)
                {
                    _onResult?.Invoke(CnActionResult.Fail(disabled));
                    return;
                }

                if (run == null) return;
                _signature = string.Empty;   // состояние игры могло измениться — перечитать список
                _onResult?.Invoke(run());
            });
            return tag;
        }

        /// <summary>Формат игры: {min}:{sec:D2}; нулевой срок — «∞».</summary>
        private static string FormatTimer(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (total / 60) + ":" + (total % 60).ToString("00");
        }
    }
}
