// Cloudy Navigator — средняя колонка: шапка «СЕЙЧАС», лента канатных линий и док КОРАБЛЬ | БАГАЖ (#hdr/#lines/#dock мода).
//
// Здесь — единственное место, где реальные данные игры превращаются в модели линий (CnLineModel):
//   квест    — шаги = цели QuestProgressDto (description, currentValue/requiredQuantity, completed); предложенный квест
//              начинается со станции «Предложен», завершённый кончается станцией «Сдать у {NPC}»;
//   контракт — шаги = «Контракт» → («Груз» по расписке) → «Доставка» → «Сдача», награда и таймер — из ContractDto.
// Действия линий уходят в CnActions (02 §7): ВЗЯТЬ / СЛЕДИТЬ / СНЯТЬ у квеста, ВЗЯТЬ / ПОЛУЧИТЬ ГРУЗ / СДАТЬ у контракта,
// в карточке шага — ОТКАЗАТЬСЯ (честное «нет сервера») и ПРОВАЛИТЬ. Сдачи квеста из окна нет: только подпись.
//
// Наград квестов в данных нет (03 §3): терминал квеста показывает прогресс целей, а не выдуманную награду.
// Подписи состояний — Derived-перевод членов QuestState / ContractState / ContractType (D18).
using System.Collections.Generic;
using System.Text;
using ProjectC.Quests;
using ProjectC.Trade;
using ProjectCCloudyNavigator.Actions;
using ProjectCCloudyNavigator.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Шапка, лента линий и нижний док.</summary>
    public sealed class CnMainView
    {
        private static readonly string[] QuestStateLabels =
        {
            "обнаружен", "предложен", "активен", "завершён", "провален", "сдан"
        };

        private static readonly string[] ContractTypeLabels = { "ОБЫЧНЫЙ", "СРОЧНЫЙ", "РАСПИСКА" };
        private static readonly string[] ContractStateLabels = { "доступен", "принят", "выполнен", "провален" };

        private const float MessageDuration = 4f;
        private const float ModelInterval = 0.25f;
        private const float DockHysteresis = 12f;

        private readonly CnActions _actions;
        private readonly CnPopups _popups;
        private readonly VisualElement _root;
        private readonly VisualElement _header;
        private readonly Label _nowKey;
        private readonly Label _now;
        private readonly Label _nowTail;
        private readonly CnLinesView _lines;
        private readonly CnDockView _dock;
        private readonly CnTip _tip;

        private string _message = string.Empty;
        private bool _messageOk;
        private float _messageTimer;
        private float _modelTimer;
        private List<CnLineModel> _models = new List<CnLineModel>();

        // Последние числа раскладки дока. Пишутся в FitDock, читаются только пробником Probe():
        // по ним видно, из чего сложилась высота дока, и можно сравнить с dockLimits() мода.
        private float _dockFree;
        private float _dockCap;
        private float _dockTarget;
        private int _dockRows;
        private float _rowsMeasured;

        public VisualElement Root => _root;

        /// <summary>Свободное место ленты в правой колонке (мод: lim.free).</summary>
        public float DockFree => _dockFree;

        /// <summary>Потолок дока: min(0.4·H, free) с полом 96 (мод: lim.cap).</summary>
        public float DockCap => _dockCap;

        /// <summary>Высота, которую док просит сейчас (мод: lim.want).</summary>
        public float DockTarget => _dockTarget;

        /// <summary>Число строк ленты, участвовавшее в расчёте (мод: lim.rows).</summary>
        public int DockRows => _dockRows;

        /// <summary>Фактическая высота содержимого ленты (мод: rowsEl.scrollHeight).</summary>
        public float RowsMeasured => _rowsMeasured;

        /// <summary>Арифметическая оценка того же (для сверки дрейфа).</summary>
        public float RowsEstimated => _lines.ContentHeight;

        /// <summary>Текущая высота дока.</summary>
        public float DockHeight => _dock.Height;

        /// <summary>Высота шапки «СЕЙЧАС» (мод: hdr.offsetHeight).</summary>
        public float HeaderHeight => _header.layout.height;

        public CnMainView(CnActions actions, CnPopups popups, CnViewState view, CnTip tip, CnHover hover)
        {
            _actions = actions;
            _popups = popups;
            _tip = tip;

            _root = CnStyle.Box("cn-main");

            // ---- шапка (#hdr): легенда + клавиша, строка «СЕЙЧАС»
            _header = CnStyle.Box("cn-hdr");
            CnStyle.Fill(_header, CnStyle.Veil, 0.7f);

            var legend = CnStyle.Row("cn-hdr__legend");
            legend.Add(CnStyle.Label("Линии — твои дела · каретка — где ты сейчас · справа — что получишь", "cn-hdr__lt"));

            // Явный вход в «ЗАДАНИЯ»: линии открывают попап на своей строке, но без линий нужен вход тоже.
            var quests = CnStyle.Tag("задания");
            quests.AddToClassList("cn-hdr__tag");
            _tip?.Hover(quests, () => CnTipCard.Make("ЗАДАНИЯ", "журнал и контракты").Foot("клик — открыть"));
            quests.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                _popups?.OpenQuests(null, false);
            });
            legend.Add(quests);

            legend.Add(CnStyle.Label("[ — окно персонажа", "cn-hdr__key"));
            _header.Add(legend);

            var now = CnStyle.Row("cn-now");
            _nowKey = CnStyle.Label("СЕЙЧАС", "cn-now__k");
            now.Add(_nowKey);
            _now = CnStyle.Label("—", "cn-now__t");
            now.Add(_now);
            _nowTail = CnStyle.Label(string.Empty, "cn-now__tail");
            now.Add(_nowTail);
            _header.Add(now);
            _root.Add(_header);

            // ---- лента: клик по телу строки открывает полноразмерное окно заданий
            _lines = new CnLinesView(Show, line => popups?.OpenQuests(line.id, line.id.StartsWith("c:", System.StringComparison.Ordinal)), hover);
            _root.Add(_lines.Root);

            // ---- док: шапки корабля и багажа открывают свои окна, плитки переносят предметы
            _dock = new CnDockView(actions, view, Show, index => _popups?.OpenShip(index), () => _popups?.OpenBag(-1), tip, hover);
            _root.Add(_dock.Root);
        }

        /// <summary>Узкая колонка (.narrow мода): мельче имя линии.</summary>
        public void SetNarrow(bool narrow) => _lines.SetNarrow(narrow);

        /// <summary>Подсветка одной линии (main.js applyHover: наведение на линию).</summary>
        public void HighlightLine(string lineId)
        {
            var set = new HashSet<string>();
            if (!string.IsNullOrEmpty(lineId)) set.Add(lineId);
            _lines.SetHighlight(set);
        }

        /// <summary>
        /// Подсветка линий, которым нужен этот предмет: в моде это bag.usage() по AFFECTS, в игре реальная связь —
        /// груз контракта (ContractDto.itemId → displayName). Нет совпадений — глушим всё, как «__none__» мода.
        /// </summary>
        public void HighlightItem(string itemName)
        {
            var set = new HashSet<string>();
            if (!string.IsNullOrEmpty(itemName))
            {
                for (int i = 0; i < _models.Count; i++)
                {
                    var line = _models[i];
                    if (line != null && line.itemName == itemName) set.Add(line.id);
                }
            }

            if (set.Count == 0) set.Add("\u0000");
            _lines.SetHighlight(set);
        }

        public void HighlightNone() => _lines.SetHighlight(null);

        public void Refresh(CnDataFeed feed, float dt)
        {
            if (feed == null) return;

            if (_messageTimer > 0f)
            {
                _messageTimer -= dt;
                if (_messageTimer <= 0f) _message = string.Empty;
            }

            _modelTimer -= dt;
            if (_modelTimer <= 0f)
            {
                _modelTimer = ModelInterval;
                _models = BuildModels(feed);
                _lines.Render(_models);
            }

            _lines.Tick(dt);
            RefreshNow();
            FitDock();
            _dock.Refresh(feed);
        }

        /// <summary>Результат действия панели попапа показывается той же строкой «СЕЙЧАС».</summary>
        public void Report(CnActionResult result) => Show(result);

        /// <summary>Результат действия: строка «СЕЙЧАС» на 4 с показывает ответ (латунь — отправлено, красный — нет).</summary>
        private void Show(CnActionResult result)
        {
            _message = string.IsNullOrEmpty(result.message) ? (result.ok ? "готово" : "не выполнено") : result.message;
            _messageOk = result.ok;
            _messageTimer = MessageDuration;
            _modelTimer = 0f;   // перечитать данные сразу: состояние игры могло измениться
        }

        private void RefreshNow()
        {
            if (_messageTimer > 0f)
            {
                _nowKey.text = _messageOk ? "ГОТОВО" : "НЕЛЬЗЯ";
                _nowKey.style.color = _messageOk ? CnStyle.Brass : CnStyle.DangerInk;
                _now.text = _message;
                _nowTail.text = string.Empty;
                return;
            }

            _nowKey.text = "СЕЙЧАС";
            _nowKey.style.color = CnStyle.Accent;

            if (_models.Count == 0)
            {
                _now.text = "нет задач";
                _nowTail.text = string.Empty;
                return;
            }

            var top = _models[0];
            string step = top.cur < top.steps.Count ? top.steps[top.cur].label : "всё готово";
            _now.text = step + " — " + top.title;
            _nowTail.text = string.IsNullOrEmpty(top.rewardSmall) ? string.Empty : " · " + top.rewardSmall;
            _nowTail.EnableInClassList("cn-now__tail--alarm", top.alarm);
        }

        /// <summary>
        /// fitDock мода (main.js:199-243) — перенесён вместе с его формулой, а не заменён «сколько осталось места»:
        ///   colH   = max(120, H − hdr)                  правая колонка целиком
        ///   rowsH  = scrollHeight ленты                 ФАКТИЧЕСКИЙ замер, не оценка
        ///   free   = max(0, colH − rowsH − 6)           свободное место ленты
        ///   cap    = max(96, min(0.4·H, free))          потолок — место ленты, но не больше 40 % окна
        ///   byRows = cap − (cap − 96)·clamp(rows/5)     высота едет по числу строк: 0 строк → весь потолок, 5 → 96
        ///   want   = max(96, byRows)
        /// Гистерезис 12 px — тоже мода: иначе на промежуточных состояниях строка дрожит.
        /// Станция в расчёте не участвует, поэтому рост дока её не сжимает.
        /// </summary>
        private void FitDock()
        {
            float h = _root.layout.height;
            if (float.IsNaN(h) || h <= 0f) return;

            float header = _header.layout.height;
            if (float.IsNaN(header) || header < 0f) header = 0f;

            float rowsH = _lines.MeasuredHeight;
            if (float.IsNaN(rowsH) || rowsH < 0f) rowsH = 0f;

            float colH = Mathf.Max(120f, h - header);
            float free = Mathf.Max(0f, colH - rowsH - 6f);
            float windowCap = Mathf.Round(h * 0.4f);
            float cap = Mathf.Max(CnDockView.MinHeight, Mathf.Min(windowCap, free));

            int rows = _lines.RowCount;
            float byRows = Mathf.Round(cap - (cap - CnDockView.MinHeight) * Mathf.Clamp(rows / 5f, 0f, 1f));
            float want = Mathf.Max(CnDockView.MinHeight, byRows);

            _dockFree = free;
            _dockCap = cap;
            _dockTarget = want;
            _dockRows = rows;
            _rowsMeasured = rowsH;

            float current = _dock.Height;
            bool atBound = Mathf.Approximately(want, CnDockView.MinHeight) || Mathf.Approximately(want, cap);
            if (Mathf.Abs(want - current) > DockHysteresis || (atBound && !Mathf.Approximately(want, current)))
                _dock.SetHeight(want);
        }

        // ------------------------------------------------------------------ модели линий

        private List<CnLineModel> BuildModels(CnDataFeed feed)
        {
            var list = new List<CnLineModel>();

            // Порядок срочности: отслеживаемый → активные → предложенные → завершённые (ждут сдачи у NPC).
            for (int i = 0; i < feed.quests.Length; i++)
                if (feed.quests[i].isTracked) list.Add(QuestLine(feed, feed.quests[i]));
            AddQuests(feed, list, QuestState.Active);
            AddQuests(feed, list, QuestState.Offered);
            AddQuests(feed, list, QuestState.Completed);

            // Контракты: активные всегда; доступные — только в своём рынке (как ContractsMarketTabController).
            for (int i = 0; i < feed.contracts.Length; i++)
                if (feed.contracts[i].isActive) list.Add(ContractLine(feed.contracts[i]));
            for (int i = 0; i < feed.contracts.Length; i++)
                if (!feed.contracts[i].isActive && CanAcceptHere(feed.contracts[i])) list.Add(ContractLine(feed.contracts[i]));

            return list;
        }

        private void AddQuests(CnDataFeed feed, List<CnLineModel> list, QuestState state)
        {
            for (int i = 0; i < feed.quests.Length; i++)
            {
                var q = feed.quests[i];
                if (q.isTracked || q.state != (byte)state) continue;
                list.Add(QuestLine(feed, q));
            }
        }

        private bool CanAcceptHere(in CnContractRow contract)
        {
            var zone = _actions.Zone;
            if (!zone.allowed || !zone.isMarket || string.IsNullOrEmpty(zone.marketLocationId)) return false;
            return string.Equals(contract.fromLocationId, zone.marketLocationId, System.StringComparison.OrdinalIgnoreCase);
        }

        private CnLineModel QuestLine(CnDataFeed feed, CnQuestRow q)
        {
            var goals = q.goals ?? new CnQuestGoal[0];
            int done = 0;
            for (int i = 0; i < goals.Length; i++) if (goals[i].completed) done++;

            string state = q.state < QuestStateLabels.Length ? QuestStateLabels[q.state] : "?";
            bool offered = q.state == (byte)QuestState.Offered;
            bool completed = q.state == (byte)QuestState.Completed;
            string npc = completed ? feed.ResolveTurnInNpc(q.questId) : string.Empty;

            var line = new CnLineModel
            {
                id = "q:" + q.questId,
                title = string.IsNullOrEmpty(q.displayName) ? q.questId : q.displayName,
                tag = "КВЕСТ · " + state.ToUpperInvariant() + " · " + q.questId,
                color = CnStyle.Accent,
                tracked = q.isTracked,
                rewardBig = done + " / " + goals.Length,
                rewardSmall = "целей · " + state,
            };

            if (offered)
            {
                line.steps.Add(new CnStep
                {
                    label = "Предложен", sub = "взять задание", done = false,
                    need = "принять квест", have = "предложен", from = "NPC, предложивший квест", gives = "квест в журнале"
                });
            }

            for (int i = 0; i < goals.Length; i++)
            {
                var g = goals[i];
                line.steps.Add(new CnStep
                {
                    label = Short(g.description, 24),
                    sub = g.completed ? "готово" : g.currentValue + " / " + g.requiredQuantity,
                    done = g.completed && !offered,
                    need = g.description,
                    have = g.currentValue + " / " + g.requiredQuantity,
                    from = "—",
                    gives = g.completed ? "цель выполнена" : "—",
                });
            }

            if (completed)
            {
                line.steps.Add(new CnStep
                {
                    label = "Сдать", sub = string.IsNullOrEmpty(npc) ? "у NPC" : "у " + npc, done = false,
                    need = "сдать квест в диалоге", have = "все цели выполнены",
                    from = string.IsNullOrEmpty(npc) ? "NPC квеста" : npc, gives = "награда — у NPC"
                });
            }

            if (line.steps.Count == 0)
            {
                line.steps.Add(new CnStep { label = "Цели", sub = "нет данных", need = "—", have = "—", from = "—", gives = "—" });
            }

            line.cur = FirstOpen(line.steps);

            string questId = q.questId;
            string name = line.title;
            var toggle = new CnLineAction
            {
                verb = q.isTracked ? "снять" : "следить",
                run = () => _actions.ToggleQuest(questId, name),
            };

            if (offered)
            {
                line.action = new CnLineAction { verb = "взять", modifier = "cn-tag--brass", run = () => _actions.AcceptQuest(questId, name) };
                line.extra.Add(toggle);
                line.extra.Add(new CnLineAction { verb = "отказаться", modifier = "cn-tag--danger", run = () => _actions.RejectQuest() });
            }
            else if (completed)
            {
                // Сдача — только через диалог с NPC (02 §7): на тросе подпись, не кнопка.
                line.info = string.IsNullOrEmpty(npc) ? "сдать в диалоге с NPC" : "сдать у " + npc;
                line.extra.Add(toggle);
            }
            else
            {
                line.action = toggle;
            }

            var sig = new StringBuilder(64);
            sig.Append(line.id).Append(q.state).Append(q.isTracked).Append(npc);
            for (int i = 0; i < goals.Length; i++) sig.Append(goals[i].currentValue).Append('/').Append(goals[i].requiredQuantity).Append(goals[i].completed);
            line.signature = sig.ToString();
            return line;
        }

        private CnLineModel ContractLine(CnContractRow c)
        {
            string type = c.type < ContractTypeLabels.Length ? ContractTypeLabels[c.type] : "?";
            string state = c.state < ContractStateLabels.Length ? ContractStateLabels[c.state] : "?";
            bool pending = c.state == (byte)ContractState.Pending;
            bool active = c.state == (byte)ContractState.Active;
            string reward = "+" + c.reward.ToString("0") + " CR";
            string cargo = "×" + c.quantity + " " + c.displayName;
            string route = c.fromLocationId + " → " + c.toLocationId;

            var line = new CnLineModel
            {
                id = "c:" + c.contractId,
                title = "Доставка " + cargo,
                tag = "КОНТРАКТ · " + type + " · " + route,
                color = CnStyle.Warn,
                rewardBig = reward,
                rewardSmall = c.timeLimit > 0f ? "осталось " + FormatTimer(c.timeRemaining) : "без срока",
                alarm = c.timeLimit > 0f && c.timeRemaining > 0f && c.timeRemaining < 60f,
                itemName = c.displayName,
            };

            line.steps.Add(new CnStep
            {
                label = "Контракт", sub = state, done = active,
                need = "взять контракт", have = state, from = c.fromLocationId, gives = reward
            });

            if (c.isReceiptContract)
            {
                line.steps.Add(new CnStep
                {
                    label = "Груз", sub = c.receiptCargoIssued ? "выдан" : "по расписке", done = c.receiptCargoIssued,
                    need = cargo, have = c.receiptCargoIssued ? "выдан" : "не выдан", from = c.fromLocationId, gives = "груз к перевозке"
                });
            }

            line.steps.Add(new CnStep
            {
                label = "Доставка", sub = c.toLocationId, done = false,
                need = cargo + " в " + c.toLocationId, have = "в пути", from = route, gives = "—"
            });
            line.steps.Add(new CnStep
            {
                label = "Сдача", sub = reward, done = false,
                need = "сдать на рынке " + c.toLocationId, have = "—", from = c.toLocationId, gives = reward
            });

            line.cur = FirstOpen(line.steps);

            string id = c.contractId;
            byte st = c.state;
            bool receipt = c.isReceiptContract;
            bool issued = c.receiptCargoIssued;

            if (pending)
            {
                line.action = new CnLineAction { verb = "взять", value = reward, modifier = "cn-tag--brass", run = () => _actions.AcceptContract(id, st) };
            }
            else if (active && receipt && !issued)
            {
                line.action = new CnLineAction { verb = "получить груз", run = () => _actions.ReceiveCargo(id, st, receipt, issued) };
            }
            else if (active)
            {
                line.action = new CnLineAction { verb = "сдать", value = reward, modifier = "cn-tag--brass", run = () => _actions.CompleteContract(id, st) };
            }

            if (active)
                line.extra.Add(new CnLineAction { verb = "провалить", modifier = "cn-tag--danger", run = () => _actions.FailContract(id, st) });

            line.signature = line.id + c.state + c.receiptCargoIssued + Mathf.FloorToInt(c.timeRemaining) + c.reward;
            return line;
        }

        private static int FirstOpen(List<CnStep> steps)
        {
            for (int i = 0; i < steps.Count; i++) if (!steps[i].done) return i;
            return steps.Count;
        }

        private static string Short(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "—";
            return text.Length <= max ? text : text.Substring(0, max - 1).TrimEnd() + "…";
        }

        /// <summary>Формат игры: {min}:{sec:D2}.</summary>
        private static string FormatTimer(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (total / 60) + ":" + (total % 60).ToString("00");
        }
    }
}
