// Cloudy Navigator — средняя колонка: шапка, лента задач и нижний док КОРАБЛЬ | БАГАЖ (шаг 4).
//
// Док живёт ТОЛЬКО здесь: он не сжимает и не перемещает станцию (её колонка — отдельный ребёнок корня).
// Строки ленты — реальные квесты и контракты из CnDataFeed; порядок: отслеживаемый квест, затем активные
// квесты, затем контракты. Ни наград, ни «сдать», ни отказов: в игре их нет (03 §3).
//
// Подписи состояний — Derived-перевод членов QuestState / ContractState / ContractType; ключей локализации
// для них в проекте нет, поэтому они помечены и вынесены в одно место (03 §7 — заменить на Loc.Get, когда
// появятся ключи ui.cn.*).
using ProjectCCloudyNavigator.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Шапка, лента задач и нижний док.</summary>
    public sealed class CnMainView
    {
        private static readonly string[] QuestStateLabels =
        {
            "ОБНАРУЖЕН", "ПРЕДЛОЖЕН", "АКТИВЕН", "ЗАВЕРШЁН", "ПРОВАЛЕН", "СДАН"
        };

        private static readonly string[] ContractTypeLabels = { "ОБЫЧНЫЙ", "СРОЧНЫЙ", "РАСПИСКА" };
        private static readonly string[] ContractStateLabels = { "ОЖИДАЕТ", "АКТИВЕН", "ВЫПОЛНЕН", "ПРОВАЛЕН" };

        private readonly VisualElement _root;
        private readonly Label _date;
        private readonly Label _altitude;
        private readonly ScrollView _lines;

        private readonly Label _shipName;
        private readonly Label _shipHull;
        private readonly Label _shipFuel;
        private readonly Label _shipCargo;
        private readonly Label _shipEngine;

        private readonly Label _bagSlots;
        private readonly Label _bagWeight;
        private readonly Label _bagItems;

        private string _linesSignature = string.Empty;

        public VisualElement Root => _root;

        public CnMainView()
        {
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

            // ---------------- док
            var dock = new VisualElement();
            dock.AddToClassList("cn-dock");

            var shipHalf = new VisualElement();
            shipHalf.AddToClassList("cn-dock__half");
            var shipCap = CnStyle.Row("cn-dock__cap");
            shipCap.Add(CnStyle.Cap("Корабль"));
            shipHalf.Add(shipCap);

            _shipName = AddDockRow(shipHalf, "корабль");
            _shipHull = AddDockRow(shipHalf, "прочность");
            _shipFuel = AddDockRow(shipHalf, "топливо");
            _shipCargo = AddDockRow(shipHalf, "груз");
            _shipEngine = AddDockRow(shipHalf, "двигатель");

            dock.Add(shipHalf);

            var bagHalf = new VisualElement();
            bagHalf.AddToClassList("cn-dock__half");
            bagHalf.AddToClassList("cn-dock__half--bag");
            var bagCap = CnStyle.Row("cn-dock__cap");
            bagCap.Add(CnStyle.Cap("Багаж"));
            bagHalf.Add(bagCap);

            _bagSlots = AddDockRow(bagHalf, "слоты");
            _bagWeight = AddDockRow(bagHalf, "вес");
            _bagItems = AddDockRow(bagHalf, "предметы");

            dock.Add(bagHalf);
            _root.Add(dock);
        }

        private static Label AddDockRow(VisualElement parent, string caption)
        {
            var row = CnStyle.Row("cn-dock__row");
            row.Add(CnStyle.Label(caption, "cn-dock__rowname"));
            var value = CnStyle.Label("—", "cn-dock__rowval");
            row.Add(value);
            parent.Add(row);
            return value;
        }

        public void Refresh(CnDataFeed feed)
        {
            if (feed == null) return;

            RefreshHeader(feed);
            RefreshLines(feed);
            RefreshDock(feed);
        }

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

        private void RefreshLines(CnDataFeed feed)
        {
            string signature = BuildLinesSignature(feed);
            if (signature == _linesSignature) return;
            _linesSignature = signature;

            _lines.Clear();

            int shown = 0;

            for (int i = 0; i < feed.quests.Length; i++)
            {
                var q = feed.quests[i];
                if (!q.isTracked && q.state != 2) continue;   // 2 = QuestState.Active
                _lines.Add(QuestLine(q));
                shown++;
            }

            for (int i = 0; i < feed.contracts.Length; i++)
            {
                var c = feed.contracts[i];
                if (!c.isActive) continue;
                _lines.Add(ContractLine(c));
                shown++;
            }

            if (shown == 0)
            {
                var empty = CnStyle.Label("активных задач нет — открой задания у NPC", "cn-dim");
                empty.style.marginTop = 8f;
                _lines.Add(empty);
            }
        }

        private static string BuildLinesSignature(CnDataFeed feed)
        {
            var sb = new System.Text.StringBuilder(128);

            for (int i = 0; i < feed.quests.Length; i++)
            {
                var q = feed.quests[i];
                if (!q.isTracked && q.state != 2) continue;
                sb.Append(q.questId).Append(':').Append(q.state).Append(';');
                for (int g = 0; g < q.goals.Length; g++)
                {
                    sb.Append(q.goals[g].currentValue).Append('/').Append(q.goals[g].requiredQuantity).Append(',');
                }
            }

            for (int i = 0; i < feed.contracts.Length; i++)
            {
                var c = feed.contracts[i];
                if (!c.isActive) continue;
                sb.Append(c.contractId).Append(':').Append(c.state).Append(':')
                  .Append(Mathf.FloorToInt(c.timeRemaining)).Append(';');
            }

            return sb.ToString();
        }

        private static VisualElement QuestLine(CnQuestRow quest)
        {
            var line = new VisualElement();
            line.AddToClassList("cn-line");
            if (quest.isTracked) line.AddToClassList("cn-line--tracked");

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

        private static VisualElement ContractLine(CnContractRow contract)
        {
            var line = new VisualElement();
            line.AddToClassList("cn-line");
            line.AddToClassList("cn-line--contract");

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

        private void RefreshDock(CnDataFeed feed)
        {
            if (feed.ships.Length == 0)
            {
                _shipName.text = "—";
                _shipHull.text = "—";
                _shipFuel.text = "—";
                _shipCargo.text = "—";
                _shipEngine.text = "—";
            }
            else
            {
                var ship = feed.ships[0];
                _shipName.text = ship.className + " · " + ship.displayName;
                _shipHull.text = ship.hullCurrent + " / " + ship.hullMax;
                _shipFuel.text = (ship.fuelNormalized * 100f).ToString("0.0") + "% (" +
                                  ship.fuelMax.ToString("0") + " max)";
                _shipCargo.text = ship.cargoUsed + " / " + ship.cargoMax + " слотов";
                _shipEngine.text = ship.isEngineRunning ? "ON" : "OFF";
            }

            float weight = 0f;
            for (int i = 0; i < feed.bagItems.Length; i++)
                weight += feed.bagItems[i].quantity * feed.bagItems[i].weightKg;

            _bagSlots.text = feed.bagItems.Length + " / " + feed.bagMaxSlots;
            _bagWeight.text = weight.ToString("0.0") + " кг";

            if (feed.bagItems.Length == 0)
            {
                _bagItems.text = "пусто";
            }
            else
            {
                var first = feed.bagItems[0];
                string text = first.itemName;
                if (feed.bagItems.Length > 1) text += " · +" + (feed.bagItems.Length - 1);
                _bagItems.text = text;
            }
        }
    }
}
