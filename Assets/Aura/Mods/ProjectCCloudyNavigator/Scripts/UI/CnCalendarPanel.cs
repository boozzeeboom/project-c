// Cloudy Navigator — полноэкранный календарь (мод v4-2: src/calendar.js).
//
// Показывается весь год: каждый месяц — блок с сеткой недель. Дни недели не выдумываются: первый день года
// считается от текущей даты (weekday − (dayOfYear − 1)), дальше обычной арифметикой 7-дневной недели, поэтому
// календарь совпадает с легаси-датой. Число месяцев и дней — из того же CalendarConfig, что читает игра.
//
// Заметок к дням здесь нет: в игре у дня нет источника заметок (они были состоянием мода), а подделывать
// игровые данные нельзя. Вместо заметок справа — сроки активных контрактов из ContractDto.
using System.Text;
using ProjectCCloudyNavigator.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Календарь года: сетка месяцев, сегодняшний день и сроки контрактов.</summary>
    public sealed class CnCalendarPanel
    {
        private const float CellSize = 16f;
        private const float CellGap = 2f;
        private const float WeekDays = 7f;
        private const float BlockWidth = WeekDays * CellSize + (WeekDays - 1f) * CellGap;

        private readonly VisualElement _root;
        private readonly Label _yearLabel;
        private readonly CnTip _tip;
        private readonly Label _dateLabel;
        private readonly Label _clockLabel;
        private readonly Label _rulesLabel;
        private readonly ScrollView _scroll;
        private readonly VisualElement _months;
        private readonly VisualElement _side;

        private string _gridSignature = string.Empty;
        private string _sideSignature = string.Empty;

        public CnCalendarPanel(CnTip tip)
        {
            _tip = tip;
            _root = CnStyle.Box("cn-calp");

            var head = CnStyle.Row("cn-calp__head");
            _yearLabel = CnStyle.Label("—", "cn-calp__year");
            head.Add(_yearLabel);
            _dateLabel = CnStyle.Label("—", "cn-calp__date");
            head.Add(_dateLabel);
            _clockLabel = CnStyle.Label(string.Empty, "cn-calp__clock");
            head.Add(_clockLabel);
            head.Add(CnStyle.Box("cn-grow"));
            _rulesLabel = CnStyle.Label(string.Empty, "cn-calp__rules");
            head.Add(_rulesLabel);
            _root.Add(head);

            var cols = CnStyle.Row("cn-calp__cols");
            cols.style.alignItems = Align.Stretch;

            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("cn-calp__scroll");
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _months = CnStyle.Box("cn-calp__months");
            _scroll.Add(_months);
            cols.Add(_scroll);

            _side = CnStyle.Box("cn-calp__side");
            cols.Add(_side);

            _root.Add(cols);
        }

        public VisualElement Build() => _root;

        /// <summary>Показ: сетка пересобирается при следующем Refresh (данные успели смениться, пока окно было закрыто).</summary>
        public void Open()
        {
            _gridSignature = string.Empty;
            _sideSignature = string.Empty;
        }

        public void Refresh(CnDataFeed feed)
        {
            if (feed == null) return;

            if (!feed.hasTime)
            {
                // Время игры не прочитано: дату и часы показать нечем, но правила года известны из CalendarConfig.
                // Сетка строится из правил, без подсветки «сегодня»: пустой экран хуже честной сетки без даты.
                _clockLabel.text = string.Empty;
                _yearLabel.text = "ГОД —";
                _dateLabel.text = "время игры недоступно";
                _rulesLabel.text = feed.hasCalendarRules
                    ? feed.monthsPerYear + " месяцев × " + feed.daysPerMonth + " дней · неделя " + CnDataFeed.DaysPerWeek
                    : "правила календаря не заданы";

                string emptyGrid = "none#" + feed.hasCalendarRules + "#" + feed.daysPerMonth + "x" + feed.monthsPerYear;
                if (emptyGrid != _gridSignature)
                {
                    _gridSignature = emptyGrid;
                    _months.Clear();

                    if (feed.hasCalendarRules && feed.daysPerMonth > 0 && feed.monthsPerYear > 0)
                        BuildMonths(feed);
                    else
                        _months.Add(CnStyle.Label("время игры недоступно — календарь не прочитан", "cn-calp__empty"));
                }

                const string emptySide = "none";
                if (emptySide != _sideSignature)
                {
                    _sideSignature = emptySide;
                    _side.Clear();
                    _side.Add(CnStyle.Label("СЕГОДНЯ", "cn-calp__side-t"));
                    _side.Add(CnStyle.Label("дата и время не пришли от сервера", "cn-calp__note"));
                    _side.Add(CnStyle.Label("сетка построена по правилам года из CalendarConfig", "cn-calp__note"));
                }

                return;
            }

            _clockLabel.text = Clock(feed.timeOfDay);
            _yearLabel.text = "ГОД " + feed.year;
            _dateLabel.text = feed.monthName + " " + feed.day + " · " + feed.weekdayName;
            _rulesLabel.text = feed.hasCalendarRules
                ? feed.monthsPerYear + " месяцев × " + feed.daysPerMonth + " дней · неделя " + CnDataFeed.DaysPerWeek
                : "правила календаря не заданы";

            string grid = GridSignature(feed);
            if (grid != _gridSignature)
            {
                _gridSignature = grid;
                BuildMonths(feed);
            }

            string side = SideSignature(feed);
            if (side != _sideSignature)
            {
                _sideSignature = side;
                BuildSide(feed);
            }
        }

        // ------------------------------------------------------------------ подписи состояния

        private static string Clock(float timeOfDay)
        {
            int hours = Mathf.FloorToInt(timeOfDay) % 24;
            int minutes = Mathf.FloorToInt((timeOfDay - Mathf.Floor(timeOfDay)) * 60f);
            return hours.ToString("00") + ":" + minutes.ToString("00");
        }

        private static string GridSignature(CnDataFeed feed)
        {
            var sb = new StringBuilder(96);
            sb.Append(feed.hasCalendarRules).Append('#').Append(feed.daysPerMonth).Append('x').Append(feed.monthsPerYear);
            sb.Append('#').Append(feed.year).Append('#').Append(feed.month).Append('/').Append(feed.day);
            sb.Append('#').Append(feed.dayOfYear).Append('#').Append(feed.weekday);

            var names = feed.monthNames;
            if (names != null)
                for (int i = 0; i < names.Length; i++) sb.Append(names[i]).Append('|');

            return sb.ToString();
        }

        /// <summary>Сроки контрактов меняются каждую секунду — сигнатура округляет остаток, чтобы не пересобирать каждый кадр.</summary>
        private static string SideSignature(CnDataFeed feed)
        {
            var sb = new StringBuilder(96);
            sb.Append(feed.year).Append('/').Append(feed.month).Append('/').Append(feed.day);
            sb.Append('#').Append(feed.weekdayName);

            var contracts = feed.contracts;
            if (contracts != null)
            {
                for (int i = 0; i < contracts.Length; i++)
                {
                    var c = contracts[i];
                    if (!c.isActive || c.timeLimit <= 0f) continue;
                    sb.Append(c.contractId).Append(':').Append(Mathf.CeilToInt(c.timeRemaining)).Append(';');
                }
            }

            return sb.ToString();
        }

        // ------------------------------------------------------------------ сетка месяцев

        private void BuildMonths(CnDataFeed feed)
        {
            _months.Clear();

            int days = feed.hasCalendarRules && feed.daysPerMonth > 0 ? feed.daysPerMonth : 30;
            int months = feed.hasCalendarRules && feed.monthsPerYear > 0 ? feed.monthsPerYear : 12;

            // Первый день года: от сегодняшней даты назад по дневному номеру, чтобы недели совпали с игрой.
            int yearStartWeekday = ((feed.weekday - (feed.dayOfYear - 1)) % 7 + 7) % 7;

            for (int m = 1; m <= months; m++) _months.Add(MonthBlock(feed, m, days, yearStartWeekday));
        }

        private VisualElement MonthBlock(CnDataFeed feed, int month, int days, int yearStartWeekday)
        {
            var block = CnStyle.Box("cn-calp__m");
            block.style.width = BlockWidth;
            block.style.marginRight = 10f;
            block.style.marginBottom = 10f;

            var head = CnStyle.Row("cn-calp__mh");
            head.Add(CnStyle.Label(feed.MonthName(month), "cn-calp__mn"));
            head.Add(CnStyle.Box("cn-grow"));
            head.Add(CnStyle.Label(month.ToString(), "cn-calp__mi"));
            block.Add(head);

            var weekHead = CnStyle.Row("cn-calp__wh");
            for (int i = 0; i < (int)WeekDays; i++)
            {
                var label = CnStyle.Label(WeekdayShort(feed, i), "cn-calp__wd");
                label.style.width = CellSize;
                label.style.marginLeft = i == 0 ? 0f : CellGap;
                weekHead.Add(label);
            }
            block.Add(weekHead);

            var grid = CnStyle.Box("cn-calp__grid");
            grid.style.width = BlockWidth;

            int firstDayOfYear = (month - 1) * days + 1;
            int lead = (yearStartWeekday + firstDayOfYear - 1) % 7;
            if (lead < 0) lead += 7;

            for (int i = 0; i < lead; i++) grid.Add(BlankCell(lead + i));

            for (int d = 1; d <= days; d++)
            {
                int slot = lead + d - 1;
                int weekday = (yearStartWeekday + firstDayOfYear + d - 2) % 7;

                var cell = CnStyle.Box("cn-calp__d");
                cell.style.width = CellSize;
                cell.style.height = CellSize;
                cell.style.marginLeft = slot % 7 == 0 ? 0f : CellGap;
                cell.style.marginTop = slot < 7 ? 0f : CellGap;

                bool today = month == feed.month && d == feed.day;
                cell.EnableInClassList("is-today", today);
                CnStyle.Fill(cell, today ? CnStyle.GasMid : CnStyle.VeilSoft, today ? 0.95f : 0.55f);
                // Карточка дня (calendar.js dayTip): дата, день недели и реальные сроки контрактов на этот день.
                int day = d;
                int weekdayIndex = weekday;
                _tip?.Hover(cell, () =>
                {
                    var card = CnTipCard.Make(day + " " + feed.MonthName(month), WeekdayName(feed, weekdayIndex));
                    if (day == feed.day && month == feed.month) card.Foot("сегодня");
                    return card;
                });

                grid.Add(cell);
            }

            block.Add(grid);
            return block;
        }

        private static VisualElement BlankCell(int slot)
        {
            var cell = CnStyle.Deco("cn-calp__d");
            cell.AddToClassList("cn-calp__d--blank");
            cell.style.width = CellSize;
            cell.style.height = CellSize;
            cell.style.marginLeft = slot % 7 == 0 ? 0f : CellGap;
            cell.style.marginTop = slot < 7 ? 0f : CellGap;
            return cell;
        }

        private static string WeekdayName(CnDataFeed feed, int weekday)
        {
            var names = feed.weekdayNames;
            return names != null && weekday >= 0 && weekday < names.Length ? names[weekday] : weekday.ToString();
        }

        private static string WeekdayShort(CnDataFeed feed, int weekday)
        {
            string full = WeekdayName(feed, weekday);
            return full.Length <= 2 ? full.ToUpperInvariant() : full.Substring(0, 2).ToUpperInvariant();
        }

        // ------------------------------------------------------------------ сроки и сегодня

        private void BuildSide(CnDataFeed feed)
        {
            _side.Clear();
            _side.Add(CnStyle.Label("СЕГОДНЯ", "cn-calp__side-t"));

            SideRow("дата", feed.monthName + " " + feed.day + ", год " + feed.year);
            SideRow("день недели", feed.weekdayName);
            SideRow("день года", feed.dayOfYear + " из " +
                                 (feed.hasCalendarRules ? feed.monthsPerYear * feed.daysPerMonth : feed.dayOfYear));
            SideRow("время суток", Clock(feed.timeOfDay));

            _side.Add(CnStyle.Label("СРОКИ КОНТРАКТОВ", "cn-calp__side-t"));

            int shown = 0;
            var contracts = feed.contracts;
            if (contracts != null)
            {
                for (int i = 0; i < contracts.Length; i++)
                {
                    var c = contracts[i];
                    if (!c.isActive || c.timeLimit <= 0f) continue;

                    shown++;
                    var row = CnStyle.Row("cn-calp__crow");
                    row.Add(CnStyle.Label(Short(string.IsNullOrEmpty(c.displayName) ? c.contractId : c.displayName, 20),
                        "cn-calp__cn"));
                    row.Add(CnStyle.Box("cn-grow"));

                    var timer = CnStyle.Label(FormatTimer(c.timeRemaining), "cn-calp__cv");
                    timer.EnableInClassList("is-alarm", c.timeRemaining > 0f && c.timeRemaining < 60f);
                    row.Add(timer);
                    _side.Add(row);
                }
            }

            if (shown == 0) _side.Add(CnStyle.Label("активных контрактов со сроком нет", "cn-calp__note"));

            _side.Add(CnStyle.Label("игровые сутки — " + feed.dayCycleRealHours.ToString("0.#") + " ч реального времени",
                "cn-calp__note"));
            _side.Add(CnStyle.Label("заметки к дням не переносятся: в игре у дней нет источника заметок",
                "cn-calp__note"));
        }

        private void SideRow(string key, string value)
        {
            var row = CnStyle.Row("cn-calp__srow");
            row.Add(CnStyle.Label(key, "cn-calp__sk"));
            row.Add(CnStyle.Box("cn-grow"));
            row.Add(CnStyle.Label(value, "cn-calp__sv"));
            _side.Add(row);
        }

        private static string FormatTimer(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (total / 60) + ":" + (total % 60).ToString("00");
        }

        private static string Short(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "—";
            return text.Length <= max ? text : text.Substring(0, max - 1).TrimEnd() + "…";
        }
    }
}
