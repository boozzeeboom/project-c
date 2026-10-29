// Cloudy Navigator — средняя колонка: шапка, лента задач, полоса действий и нижний док КОРАБЛЬ | БАГАЖ (шаги 4 и 7).
//
// Док живёт ТОЛЬКО здесь: он не сжимает и не перемещает станцию (её колонка — отдельный ребёнок корня).
// Строки ленты — реальные квесты и контракты из CnDataFeed; порядок: отслеживаемый квест, затем активные
// квесты, затем контракты. Строка только ВЫБИРАЕТСЯ кликом: сами действия стоят бирками в полосе над доком
// и уходят в CnActions — единственное место порта, откуда вызываются игровые Request*-методы (02 §7).
//
// Чего окно не делает: не сдаёт квест (сдача идёт через диалог — показывается подпись «сдать у {NPC}»),
// не отказывается от квеста (сервера нет — честное сообщение Loc), не переносит груз вне зоны обменника.
//
// Подписи состояний — Derived-перевод членов QuestState / ContractState / ContractType; ключей локализации
// для них в проекте нет, поэтому они помечены и вынесены в одно место (03 §7 — заменить на Loc.Get, когда
// появятся ключи ui.cn.*).
using System.Text;
using ProjectC.Quests;
using ProjectC.Trade;
using ProjectCCloudyNavigator.Actions;
using ProjectCCloudyNavigator.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Шапка, лента задач, полоса действий и нижний док.</summary>
    public sealed class CnMainView
    {
        private static readonly string[] QuestStateLabels =
        {
            "ОБНАРУЖЕН", "ПРЕДЛОЖЕН", "АКТИВЕН", "ЗАВЕРШЁН", "ПРОВАЛЕН", "СДАН"
        };

        private static readonly string[] ContractTypeLabels = { "ОБЫЧНЫЙ", "СРОЧНЫЙ", "РАСПИСКА" };
        private static readonly string[] ContractStateLabels = { "ОЖИДАЕТ", "АКТИВЕН", "ВЫПОЛНЕН", "ПРОВАЛЕН" };

        /// <summary>Сколько плиток предметов помещается в половину дока (дальше — «ещё N»).</summary>
        private const int MaxChips = 6;

        /// <summary>Сколько держится сообщение действия в полосе (сек) — дальше возвращается название выбора.</summary>
        private const float MessageDuration = 4f;

        private readonly CnActions _actions;
        private readonly VisualElement _root;
        private readonly Label _date;
        private readonly Label _altitude;
        private readonly ScrollView _lines;

        private readonly VisualElement _actionsBar;
        private readonly Label _actionsTitle;
        private readonly VisualElement _actionsTags;

        private readonly Label _shipCapVal;
        private readonly Label _shipName;
        private readonly Label _shipHull;
        private readonly Label _shipFuel;
        private readonly VisualElement _shipChips;

        private readonly Label _bagZone;
        private readonly Label _bagSlots;
        private readonly Label _bagWeight;
        private readonly VisualElement _bagChips;

        private string _linesSignature = string.Empty;
        private string _actionsSignature = string.Empty;
        private string _chipsSignature = string.Empty;

        // Выбор строки: только он решает, какие бирки покажет полоса действий.
        private string _selectedKind = string.Empty;   // "quest" | "contract"
        private string _selectedId = string.Empty;
        private string _selectedTitle = string.Empty;
        private VisualElement _selectedLine;

        private string _message = string.Empty;
        private bool _messageOk;
        private float _messageTimer;
        private bool _zoneAllowedShown;

        public VisualElement Root => _root;

        public CnMainView(CnActions actions)
        {
            _actions = actions;

            _root = new VisualElement();
            _root.AddToClassList("cn-main");

            // ---------------- шапка
            var header = new VisualElement();
            header.AddToClassList("cn-hdr");
            header.Add(CnStyle.Label("ОБЛАЧНАЯ КАРТА", "cn-hdr__title"));

            var spacer = new VisualElement();
            spacer.AddToClassList("cn-hdr__spacer");
            header.Add(spacer);

            _date = CnStyle.Label("—", "cn-dim");
            _date.style.marginRight = 10f;
            header.Add(_date);

            _altitude = CnStyle.Label(string.Empty, "cn-brass");
            header.Add(_altitude);

            _root.Add(header);

            // ---------------- лента
            _lines = new ScrollView(ScrollViewMode.Vertical);
            _lines.AddToClassList("cn-lines");
            _lines.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _lines.verticalScrollerVisibility = ScrollerVisibility.Auto;
            _root.Add(_lines);

            // ---------------- полоса действий: название выбора либо сообщение последнего действия + бирки
            _actionsBar = new VisualElement();
            _actionsBar.AddToClassList("cn-actions");

            _actionsTitle = CnStyle.Label(string.Empty, "cn-actions__title");
            _actionsBar.Add(_actionsTitle);

            _actionsTags = new VisualElement();
            _actionsTags.AddToClassList("cn-actions__tags");
            _actionsBar.Add(_actionsTags);

            _root.Add(_actionsBar);

            // ---------------- док
            var dock = new VisualElement();
            dock.AddToClassList("cn-dock");

            var shipHalf = new VisualElement();
            shipHalf.AddToClassList("cn-dock__half");

            var shipCap = CnStyle.Row("cn-dock__cap");
            shipCap.Add(CnStyle.Cap("Корабль"));
            shipCap.Add(CnStyle.Spacer());
            _shipCapVal = CnStyle.Label("—", "cn-dock__capval");
            shipCap.Add(_shipCapVal);
            shipHalf.Add(shipCap);

            _shipName = AddDockRow(shipHalf, "корабль");
            _shipHull = AddDockRow(shipHalf, "прочность");
            _shipFuel = AddDockRow(shipHalf, "топливо", true);

            _shipChips = new VisualElement();
            _shipChips.AddToClassList("cn-chips");
            shipHalf.Add(_shipChips);

            dock.Add(shipHalf);

            var bagHalf = new VisualElement();
            bagHalf.AddToClassList("cn-dock__half");
            bagHalf.AddToClassList("cn-dock__half--bag");

            var bagCap = CnStyle.Row("cn-dock__cap");
            bagCap.Add(CnStyle.Cap("Багаж"));
            bagCap.Add(CnStyle.Spacer());
            _bagZone = CnStyle.Label("—", "cn-dock__capzone");
            bagCap.Add(_bagZone);
            bagHalf.Add(bagCap);

            _bagSlots = AddDockRow(bagHalf, "слоты", true);
            _bagWeight = AddDockRow(bagHalf, "вес");

            _bagChips = new VisualElement();
            _bagChips.AddToClassList("cn-chips");
            bagHalf.Add(_bagChips);

            dock.Add(bagHalf);
            _root.Add(dock);
        }

        /// <summary>Строка дока. optional — второстепенная: в узком доке её прячет USS, число остаётся в шапке половины.</summary>
        private static Label AddDockRow(VisualElement parent, string caption, bool optional = false)
        {
            var row = CnStyle.Row("cn-dock__row");
            if (optional) row.AddToClassList("cn-dock__row--optional");
            row.Add(CnStyle.Label(caption, "cn-dock__rowname"));
            var value = CnStyle.Label("—", "cn-dock__rowval");
            row.Add(value);
            parent.Add(row);
            return value;
        }

        /// <summary>Кадр окна. dt нужен сообщению действия: своего планировщика в порте нет (04 §1).</summary>
        public void Refresh(CnDataFeed feed, float dt)
        {
            if (feed == null) return;

            if (_messageTimer > 0f)
            {
                _messageTimer -= dt;
                if (_messageTimer <= 0f)
                {
                    _messageTimer = 0f;
                    _message = string.Empty;
                    _actionsSignature = string.Empty;   // сообщение ушло — вернуть название выбора
                }
            }

            ValidateSelection(feed);

            RefreshHeader(feed);
            RefreshLines(feed);
            RefreshActions(feed);
            RefreshDock(feed);
            RefreshChips(feed);
        }

        // ------------------------------------------------------------------ шапка

        private void RefreshHeader(CnDataFeed feed)
        {
            _date.text = feed.hasTime
                ? feed.monthName + " " + feed.day + " · " + feed.weekdayName
                : "календарь: нет данных";

            if (!feed.hasAltitude)
            {
                _altitude.text = "высота: нет данных";
                return;
            }

            _altitude.text = feed.altitude.ToString("0") + " м · " + feed.altitudeStatusName;
        }

        // ------------------------------------------------------------------ лента

        private void RefreshLines(CnDataFeed feed)
        {
            string signature = BuildLinesSignature(feed);
            if (signature == _linesSignature) return;
            _linesSignature = signature;

            _lines.Clear();
            _selectedLine = null;   // старый элемент ушёл из дерева; подсветку заново поставит MarkSelected

            int shown = 0;

            // Порядок ленты: отслеживаемый, активные, предложенные (по ним работают бирки «взять»),
            // завершённые (ждут сдачи у NPC). Обнаруженные без предложения не показываются: принять их нельзя.
            for (int i = 0; i < feed.quests.Length; i++)
            {
                var q = feed.quests[i];
                if (!q.isTracked) continue;
                _lines.Add(QuestLine(q));
                shown++;
            }

            for (int i = 0; i < feed.quests.Length; i++)
            {
                var q = feed.quests[i];
                if (q.isTracked || q.state != (byte)QuestState.Active) continue;
                _lines.Add(QuestLine(q));
                shown++;
            }

            for (int i = 0; i < feed.quests.Length; i++)
            {
                var q = feed.quests[i];
                if (q.isTracked || q.state != (byte)QuestState.Offered) continue;
                _lines.Add(QuestLine(q));
                shown++;
            }

            for (int i = 0; i < feed.quests.Length; i++)
            {
                var q = feed.quests[i];
                if (q.isTracked || q.state != (byte)QuestState.Completed) continue;
                _lines.Add(QuestLine(q));
                shown++;
            }

            for (int i = 0; i < feed.contracts.Length; i++)
            {
                var c = feed.contracts[i];
                if (!c.isActive && !CanAcceptHere(c)) continue;
                _lines.Add(ContractLine(c));
                shown++;
            }

            if (shown == 0)
            {
                var empty = CnStyle.Label("задач нет — открой задания у NPC", "cn-dim");
                empty.style.marginTop = 8f;
                _lines.Add(empty);
            }
        }

        private string BuildLinesSignature(CnDataFeed feed)
        {
            var sb = new StringBuilder(128);

            // Набор строк зависит от рынка: доступные контракты видны только в своём рынке.
            sb.Append(_actions.Zone.marketLocationId).Append('#');

            for (int i = 0; i < feed.quests.Length; i++)
            {
                var q = feed.quests[i];
                if (q.isTracked) continue;
                if (q.state != (byte)QuestState.Active && q.state != (byte)QuestState.Offered &&
                    q.state != (byte)QuestState.Completed) continue;
                sb.Append(q.questId).Append(':').Append(q.state).Append(';');
                for (int g = 0; g < q.goals.Length; g++)
                {
                    sb.Append(q.goals[g].currentValue).Append('/').Append(q.goals[g].requiredQuantity).Append(',');
                }
            }

            for (int i = 0; i < feed.contracts.Length; i++)
            {
                var c = feed.contracts[i];
                if (!c.isActive && !CanAcceptHere(c)) continue;
                sb.Append(c.contractId).Append(':').Append(c.state).Append(':')
                  .Append(Mathf.FloorToInt(c.timeRemaining)).Append(';');
            }

            return sb.ToString();
        }

        /// <summary>
        /// Доступный контракт показывается только в том рынке, откуда он стартует — тот же фильтр, что у
        /// легаси ContractsMarketTabController (fromLocationId == текущий рынок). Вне рынка взять нечего.
        /// </summary>
        private bool CanAcceptHere(in CnContractRow contract)
        {
            var zone = _actions.Zone;
            if (!zone.allowed || !zone.isMarket || string.IsNullOrEmpty(zone.marketLocationId)) return false;
            return string.Equals(contract.fromLocationId, zone.marketLocationId, System.StringComparison.OrdinalIgnoreCase);
        }

        private VisualElement QuestLine(CnQuestRow quest)
        {
            var line = new VisualElement();
            line.AddToClassList("cn-line");
            if (quest.isTracked) line.AddToClassList("cn-line--tracked");
            MarkSelected(line, "quest", quest.questId);
            line.RegisterCallback<ClickEvent>(_ => Select("quest", quest.questId, quest.displayName, line));

            var body = new VisualElement();
            body.AddToClassList("cn-line__body");
            body.Add(CnStyle.Label(quest.displayName, "cn-line__title"));

            int done = 0;
            for (int i = 0; i < quest.goals.Length; i++) if (quest.goals[i].completed) done++;
            string stateLabel = quest.state < QuestStateLabels.Length ? QuestStateLabels[quest.state] : "?";
            body.Add(CnStyle.Label(stateLabel + " · " + done + "/" + quest.goals.Length, "cn-line__sub"));
            line.Add(body);

            var right = new VisualElement();
            right.AddToClassList("cn-line__right");
            right.Add(CnStyle.Label(quest.isTracked ? "СЛЕДУЮ" : string.Empty, "cn-brass"));
            line.Add(right);

            return line;
        }

        private VisualElement ContractLine(CnContractRow contract)
        {
            var line = new VisualElement();
            line.AddToClassList("cn-line");
            line.AddToClassList("cn-line--contract");
            MarkSelected(line, "contract", contract.contractId);
            line.RegisterCallback<ClickEvent>(_ => Select("contract", contract.contractId, contract.displayName, line));

            var body = new VisualElement();
            body.AddToClassList("cn-line__body");
            body.Add(CnStyle.Label(contract.displayName, "cn-line__title"));

            string type = contract.type < ContractTypeLabels.Length ? ContractTypeLabels[contract.type] : "?";
            string state = contract.state < ContractStateLabels.Length ? ContractStateLabels[contract.state] : "?";
            body.Add(CnStyle.Label(
                contract.fromLocationId + " → " + contract.toLocationId + " · ×" + contract.quantity + " · " +
                type + " · " + state, "cn-line__sub"));
            line.Add(body);

            var right = new VisualElement();
            right.AddToClassList("cn-line__right");
            right.Add(CnStyle.Label("+" + contract.reward.ToString("0") + " CR", "cn-brass"));
            right.Add(CnStyle.Label(FormatTimer(contract.timeRemaining), "cn-dim"));
            line.Add(right);

            return line;
        }

        /// <summary>Формат игры: {min}:{sec:D2}, «∞» когда лимита нет.</summary>
        private static string FormatTimer(float seconds)
        {
            if (seconds <= 0f) return "∞";
            int total = Mathf.FloorToInt(seconds);
            return (total / 60) + ":" + (total % 60).ToString("00");
        }

        // ------------------------------------------------------------------ выбор строки

        private bool IsSelected(string kind, string id)
        {
            return _selectedKind == kind && _selectedId == id;
        }

        private void MarkSelected(VisualElement line, string kind, string id)
        {
            if (!IsSelected(kind, id)) return;

            _selectedLine = line;
            SetLineHighlight(line, true);
        }

        /// <summary>
        /// Подсветка выбранной строки. Делается классами на месте, без перестройки ленты: перестройка
        /// сбрасывала бы прокрутку в начало на каждый клик, а строки в списке может быть много.
        /// Полупрозрачная заливка — только через гамма-коррекцию (04 §2), подложка — вуаль окна.
        /// </summary>
        private static void SetLineHighlight(VisualElement line, bool on)
        {
            if (line == null) return;

            if (!on)
            {
                line.RemoveFromClassList("cn-line--selected");
                line.style.backgroundColor = StyleKeyword.Null;
                return;
            }

            line.AddToClassList("cn-line--selected");
            line.style.backgroundColor = CnStyle.Alpha(CnStyle.Accent, 0.14f, CnStyle.Veil);
        }

        private void Select(string kind, string id, string title, VisualElement line)
        {
            if (IsSelected(kind, id)) return;   // повторный клик по той же строке ничего не меняет

            SetLineHighlight(_selectedLine, false);
            _selectedLine = line;
            SetLineHighlight(line, true);

            _selectedKind = kind;
            _selectedId = id;
            _selectedTitle = string.IsNullOrEmpty(title) ? id : title;

            _actionsSignature = string.Empty;
        }

        private void ClearSelection()
        {
            SetLineHighlight(_selectedLine, false);
            _selectedLine = null;

            _selectedKind = string.Empty;
            _selectedId = string.Empty;
            _selectedTitle = string.Empty;
            _actionsSignature = string.Empty;
        }

        /// <summary>Выбранная строка могла исчезнуть из снапшота — тогда выбор снимается, а не висит мёртвым.</summary>
        private void ValidateSelection(CnDataFeed feed)
        {
            if (string.IsNullOrEmpty(_selectedId)) return;

            if (_selectedKind == "quest")
            {
                if (!TryGetQuest(feed, _selectedId, out _)) ClearSelection();
            }
            else if (!TryGetContract(feed, _selectedId, out _)) ClearSelection();
        }

        private static bool TryGetQuest(CnDataFeed feed, string questId, out CnQuestRow row)
        {
            for (int i = 0; i < feed.quests.Length; i++)
            {
                if (feed.quests[i].questId != questId) continue;
                row = feed.quests[i];
                return true;
            }

            row = default;
            return false;
        }

        private static bool TryGetContract(CnDataFeed feed, string contractId, out CnContractRow row)
        {
            for (int i = 0; i < feed.contracts.Length; i++)
            {
                if (feed.contracts[i].contractId != contractId) continue;
                row = feed.contracts[i];
                return true;
            }

            row = default;
            return false;
        }

        // ------------------------------------------------------------------ полоса действий

        private void RefreshActions(CnDataFeed feed)
        {
            string signature = BuildActionsSignature(feed);
            if (signature == _actionsSignature) return;
            _actionsSignature = signature;

            _actionsTags.Clear();

            if (_messageTimer > 0f && !string.IsNullOrEmpty(_message))
            {
                _actionsTitle.text = _message;
                _actionsTitle.style.color = _messageOk ? CnStyle.Brass : CnStyle.Danger;
                return;
            }

            if (string.IsNullOrEmpty(_selectedId))
            {
                _actionsTitle.text = "выбери задачу в ленте";
                _actionsTitle.style.color = CnStyle.Dim;
                return;
            }

            _actionsTitle.text = _selectedTitle;
            _actionsTitle.style.color = CnStyle.Muted;

            if (_selectedKind == "quest") BuildQuestTags(feed);
            else BuildContractTags(feed);
        }

        private string BuildActionsSignature(CnDataFeed feed)
        {
            var sb = new StringBuilder(64);
            sb.Append(_selectedKind).Append('|').Append(_selectedId).Append('|');
            sb.Append(_messageTimer > 0f ? _message : string.Empty).Append('|');

            for (int i = 0; i < feed.quests.Length; i++)
            {
                if (feed.quests[i].questId != _selectedId) continue;
                sb.Append(feed.quests[i].state).Append(feed.quests[i].isTracked);
            }

            for (int i = 0; i < feed.contracts.Length; i++)
            {
                if (feed.contracts[i].contractId != _selectedId) continue;
                sb.Append(feed.contracts[i].state).Append(feed.contracts[i].receiptCargoIssued);
            }

            return sb.ToString();
        }

        private void BuildQuestTags(CnDataFeed feed)
        {
            if (!TryGetQuest(feed, _selectedId, out var quest)) return;

            bool tracked = quest.isTracked;
            AddTag(tracked ? "снять" : "следить", null,
                () => Run(_actions.ToggleQuest(quest.questId, quest.displayName)));

            if (quest.state == (byte)QuestState.Offered)
            {
                AddTag("взять", "cn-tag--brass",
                    () => Run(_actions.AcceptQuest(quest.questId, quest.displayName)));
                AddTag("отказаться", "cn-tag--danger", () => Run(_actions.RejectQuest()));
            }

            // Сдать квест из окна нельзя: в игре сдача идёт через диалог с NPC (02 §7). Показываем только адрес.
            if (quest.state == (byte)QuestState.Completed)
            {
                string npc = feed.ResolveTurnInNpc(quest.questId);
                _actionsTags.Add(CnStyle.Label(
                    string.IsNullOrEmpty(npc) ? "сдать через диалог с NPC" : "сдать у " + npc, "cn-dim"));
            }
        }

        private void BuildContractTags(CnDataFeed feed)
        {
            if (!TryGetContract(feed, _selectedId, out var contract)) return;

            if (contract.state == (byte)ContractState.Pending)
            {
                AddTag("взять", "cn-tag--brass",
                    () => Run(_actions.AcceptContract(contract.contractId, contract.state)));
                return;
            }

            if (contract.state != (byte)ContractState.Active) return;

            AddTag("сдать", "cn-tag--brass",
                () => Run(_actions.CompleteContract(contract.contractId, contract.state)));
            AddTag("провалить", "cn-tag--danger",
                () => Run(_actions.FailContract(contract.contractId, contract.state)));

            if (!contract.isReceiptContract || contract.receiptCargoIssued) return;

            AddTag("груз", null, () => Run(_actions.ReceiveCargo(
                contract.contractId, contract.state, contract.isReceiptContract, contract.receiptCargoIssued)));
        }

        private void AddTag(string caption, string modifier, System.Action onClick)
        {
            var tag = CnStyle.Tag(caption, modifier);
            tag.RegisterCallback<ClickEvent>(_ => onClick());
            _actionsTags.Add(tag);
        }

        /// <summary>Показать результат действия в полосе и перестроить бирки по новому состоянию игры.</summary>
        private void Run(CnActionResult result)
        {
            _message = string.IsNullOrEmpty(result.message) ? (result.ok ? "готово" : "не выполнено") : result.message;
            _messageOk = result.ok;
            _messageTimer = MessageDuration;
            _actionsSignature = string.Empty;
        }

        // ------------------------------------------------------------------ док

        private void RefreshDock(CnDataFeed feed)
        {
            int shipIndex = TargetShipIndex(feed);
            if (shipIndex < 0)
            {
                _shipName.text = "—";
                _shipHull.text = "—";
                _shipFuel.text = "—";
                _shipCapVal.text = "—";
            }
            else
            {
                var ship = feed.ships[shipIndex];
                _shipName.text = ship.className + " · " + ship.displayName;
                _shipHull.text = ship.hullCurrent + " / " + ship.hullMax;
                _shipFuel.text = (ship.fuelNormalized * 100f).ToString("0.0") + "% (" +
                                  ship.fuelMax.ToString("0") + " max)";
                _shipCapVal.text = ship.cargoUsed + " / " + ship.cargoMax + " слотов · " +
                                   (ship.isEngineRunning ? "ON" : "OFF");
            }

            float weight = 0f;
            for (int i = 0; i < feed.bagItems.Length; i++)
                weight += feed.bagItems[i].quantity * feed.bagItems[i].weightKg;

            _bagSlots.text = feed.bagItems.Length + " / " + feed.bagMaxSlots;
            _bagWeight.text = weight.ToString("0.0") + " кг";

            // Зона обменника — реальное состояние игрока: рынок, консоль трюма либо «подойди к обменнику».
            var zone = _actions.Zone;
            if (_bagZone.text != zone.label) _bagZone.text = zone.label;

            if (_zoneAllowedShown != zone.allowed)
            {
                _zoneAllowedShown = zone.allowed;
                _bagZone.style.color = zone.allowed ? CnStyle.Brass : CnStyle.Dim;
            }
        }

        /// <summary>
        /// Корабль, к трюму которого окно имеет доступ прямо сейчас: в зоне консоли — именно её корабль,
        /// на рынке — первый свой корабль. -1 — доступа нет (консоль чужого корабля либо кораблей не видно).
        /// Показывает и переносит окно ОДИН и тот же корабль, поэтому плитки не врут о содержимом трюма.
        /// </summary>
        private int TargetShipIndex(CnDataFeed feed)
        {
            var zone = _actions.Zone;

            if (zone.allowed && !zone.isMarket && zone.consoleShipNetworkObjectId != 0)
            {
                for (int i = 0; i < feed.ships.Length; i++)
                    if (feed.ships[i].shipNetworkObjectId == zone.consoleShipNetworkObjectId) return i;
                return -1;
            }

            return feed.ships.Length > 0 ? 0 : -1;
        }

        // ------------------------------------------------------------------ плитки переноса

        private void RefreshChips(CnDataFeed feed)
        {
            string signature = BuildChipsSignature(feed);
            if (signature == _chipsSignature) return;
            _chipsSignature = signature;

            _bagChips.Clear();
            _shipChips.Clear();

            var zone = _actions.Zone;
            int shipIndex = TargetShipIndex(feed);
            ulong shipNetId = shipIndex >= 0 ? feed.ships[shipIndex].shipNetworkObjectId : 0;

            // Багаж → трюм: одна упаковка за нажатие, курс показан в подсказке.
            if (feed.bagItems.Length == 0)
            {
                _bagChips.Add(CnStyle.Label("пусто", "cn-chip__note"));
            }
            else
            {
                int shown = Mathf.Min(feed.bagItems.Length, MaxChips);
                for (int i = 0; i < shown; i++)
                {
                    var item = feed.bagItems[i];
                    int packs = _actions.PacksForStore(item.itemId, item.quantity);
                    string rate = _actions.StoreRateLabel(item.itemId);

                    string tip = string.IsNullOrEmpty(rate)
                        ? "курс обмена не задан"
                        : rate + " · полных упаковок: " + packs;
                    bool blocked = !zone.allowed || shipNetId == 0 || packs <= 0;

                    var chip = CnStyle.Chip(item.itemName + " ×" + item.quantity, tip, blocked);
                    int itemId = item.itemId;
                    int quantity = item.quantity;
                    string name = item.itemName;
                    chip.RegisterCallback<ClickEvent>(_ => Run(
                        _actions.StoreToShip(shipNetId, itemId, name, quantity, 1)));
                    _bagChips.Add(chip);
                }

                if (feed.bagItems.Length > shown)
                    _bagChips.Add(CnStyle.Label("ещё " + (feed.bagItems.Length - shown), "cn-chip__note"));
            }

            // Трюм → багаж: то же самое в обратную сторону, по строкам груза того же корабля.
            var cargo = shipIndex >= 0 ? feed.ships[shipIndex].cargoDetail : null;
            if (cargo == null || cargo.Length == 0)
            {
                _shipChips.Add(CnStyle.Label("трюм пуст", "cn-chip__note"));
                return;
            }

            int cargoShown = Mathf.Min(cargo.Length, MaxChips);
            for (int i = 0; i < cargoShown; i++)
            {
                var row = cargo[i];
                int packs = _actions.PacksForRetrieve(row.itemId, row.quantity);
                string rate = _actions.RetrieveRateLabel(row.itemId);

                string tip = string.IsNullOrEmpty(rate)
                    ? "курс обмена не задан"
                    : rate + " · полных упаковок: " + packs;
                bool blocked = !zone.allowed || shipNetId == 0 || packs <= 0;

                var chip = CnStyle.Chip(row.displayName + " ×" + row.quantity, tip, blocked);
                string itemId = row.itemId;
                int quantity = row.quantity;
                string name = row.displayName;
                chip.RegisterCallback<ClickEvent>(_ => Run(
                    _actions.RetrieveFromShip(shipNetId, itemId, name, quantity, 1)));
                _shipChips.Add(chip);
            }

            if (cargo.Length > cargoShown)
                _shipChips.Add(CnStyle.Label("ещё " + (cargo.Length - cargoShown), "cn-chip__note"));
        }

        private string BuildChipsSignature(CnDataFeed feed)
        {
            var sb = new StringBuilder(96);
            sb.Append(_actions.Zone.label).Append('|');

            int shipIndex = TargetShipIndex(feed);
            sb.Append(shipIndex >= 0 ? feed.ships[shipIndex].shipNetworkObjectId : 0).Append('|');

            for (int i = 0; i < feed.bagItems.Length; i++)
                sb.Append(feed.bagItems[i].itemId).Append(':').Append(feed.bagItems[i].quantity).Append(',');

            if (shipIndex >= 0 && feed.ships[shipIndex].cargoDetail != null)
            {
                var cargo = feed.ships[shipIndex].cargoDetail;
                sb.Append('|');
                for (int i = 0; i < cargo.Length; i++)
                    sb.Append(cargo[i].itemId).Append(':').Append(cargo[i].quantity).Append(',');
            }

            return sb.ToString();
        }
    }
}
