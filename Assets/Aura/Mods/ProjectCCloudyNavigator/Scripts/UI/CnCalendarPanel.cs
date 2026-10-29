// Cloudy Navigator — полноэкранный календарь (мод v4-2: src/calendar.js).
//
// Показывается весь год: каждый месяц — блок с сеткой недель. Дни недели не выдумываются: первый день года
// считается от текущей даты (weekday − (dayOfYear − 1)), дальше обычной арифметикой 7-дневной недели, поэтому
// календарь совпадает с легаси-датой. Число месяцев и дней — из того же CalendarConfig, что читает игра.
//
// Клик по дню выбирает его и наполняет правую колонку (calendar.js .cp-day): события этого дня, форма заметки и
// кнопки дня. События берутся из двух настоящих источников:
//   * заметки игрока (CnNotesStore) — личные записи, аналог localStorage мода; файл лежит вне Assets;
//   * сроки активных контрактов (ContractDto.timeRemaining), пересчитанные в игровые дни по длине игровых суток
//     CalendarConfig.dayCycleRealHours — единственной величине, которая связывает реальные секунды с игровым
//     временем. Если длина суток неизвестна, сроки в сетку не ставятся: связь временем не выдумывается.
// Своего календаря окно не держит: год, месяц, день, неделя, имена месяцев и правила года — из CnDataFeed.
using System;
using System.Collections.Generic;
using System.Text;
using ProjectCCloudyNavigator.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Календарь года: сетка месяцев, выбранный день, его события и заметки.</summary>
    public sealed class CnCalendarPanel
    {
        private const float CellSize = 16f;
        private const float CellGap = 2f;
        private const float WeekDays = 7f;
        private const float BlockWidth = WeekDays * CellSize + (WeekDays - 1f) * CellGap;

        /// <summary>Типы заметок мода (calendar.js TYPES) в порядке кнопок.</summary>
        private static readonly string[] NoteTypes =
        {
            CnNotesStore.Remember, CnNotesStore.Business, CnNotesStore.Meeting
        };

        /// <summary>
        /// Открытая форма заметки. Текст и часы живут здесь, а не в подписи панели: иначе каждый кадр пересборки
        /// пересоздавал бы поле и оно теряло бы фокус прямо во время набора.
        /// </summary>
        private sealed class DayForm
        {
            public string noteId;                              // null — новая заметка
            public string text = string.Empty;
            public string type = CnNotesStore.Remember;
            public int hours = 12;                             // calendar.js: form.hours по умолчанию 12
        }

        private readonly VisualElement _root;
        private readonly Label _yearLabel;
        private readonly CnTip _tip;
        private readonly Label _dateLabel;
        private readonly Label _clockLabel;
        private readonly Label _rulesLabel;
        private readonly ScrollView _scroll;
        private readonly VisualElement _months;
        private readonly VisualElement _side;
        private readonly CnNotesStore _notes;

        // Адрес клетки — абсолютный номер дня, поэтому сетка и панель дня говорят на одном языке.
        private readonly Dictionary<int, VisualElement> _cells = new Dictionary<int, VisualElement>(400);
        private readonly Dictionary<int, VisualElement> _dots = new Dictionary<int, VisualElement>(400);
        private readonly Dictionary<string, VisualElement> _typeButtons = new Dictionary<string, VisualElement>(3);
        private readonly List<CnNote> _dayNotes = new List<CnNote>(8);

        private string _gridSignature = string.Empty;
        private string _sideSignature = string.Empty;
        private string _marksSignature = string.Empty;

        // Правила года на момент последней сборки сетки: без них номер дня не перевести в месяц и неделю.
        private int _daysPerMonth = 30;
        private int _monthsPerYear = 12;
        private int _year = 1;
        private int _yearStart;
        private int _yearStartWeekday;
        private int _totalDays = 360;

        private int _today = -1;
        private int _selected = -1;
        private bool _selectToday;
        private DayForm _form;

        /// <summary>День, на котором нужно проиграть calhl2; выставляется кликом и снимается в ApplyMarks.</summary>
        private int _pulseDay = int.MinValue;

        public CnCalendarPanel(CnTip tip, CnNotesStore notes = null)
        {
            _tip = tip;
            _notes = notes ?? new CnNotesStore();

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

            // Панель дня прокручивается отдельно: форма заметки и список сроков вместе выше окна.
            var dayScroll = new ScrollView(ScrollViewMode.Vertical);
            dayScroll.AddToClassList("cn-calp__dscroll");
            dayScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _side = CnStyle.Box("cn-calp__side");
            dayScroll.Add(_side);
            cols.Add(dayScroll);

            _root.Add(cols);
        }

        public VisualElement Build() => _root;

        /// <summary>
        /// Показ: сетка пересобирается при следующем Refresh (данные успели смениться, пока окно было закрыто).
        /// Открываемся на сегодняшнем дне — calendar.js open_() делает то же самое.
        /// </summary>
        public void Open()
        {
            _gridSignature = string.Empty;
            _sideSignature = string.Empty;
            _marksSignature = string.Empty;
            _form = null;
            _selectToday = true;
            _notes.EnsureLoaded();
        }

        public void Refresh(CnDataFeed feed)
        {
            if (feed == null) return;

            if (!feed.hasTime)
            {
                ShowWithoutTime(feed);
                return;
            }

            _daysPerMonth = feed.hasCalendarRules && feed.daysPerMonth > 0 ? feed.daysPerMonth : 30;
            _monthsPerYear = feed.hasCalendarRules && feed.monthsPerYear > 0 ? feed.monthsPerYear : 12;
            _totalDays = _daysPerMonth * _monthsPerYear;
            _year = feed.year > 0 ? feed.year : 1;
            _yearStart = (_year - 1) * _totalDays;

            // Первый день года: от сегодняшней даты назад по дневному номеру, чтобы недели совпали с игрой.
            _yearStartWeekday = ((feed.weekday - (feed.dayOfYear - 1)) % 7 + 7) % 7;
            _today = _yearStart + Mathf.Clamp(feed.dayOfYear - 1, 0, _totalDays - 1);

            if (_selectToday || _selected < _yearStart || _selected >= _yearStart + _totalDays)
            {
                _selected = _today;
                _selectToday = false;
            }

            _clockLabel.text = Clock(feed.timeOfDay);
            _yearLabel.text = "ГОД " + _year;
            _dateLabel.text = feed.monthName + " " + feed.day + " · " + feed.weekdayName;
            _rulesLabel.text = feed.hasCalendarRules
                ? feed.monthsPerYear + " месяцев × " + feed.daysPerMonth + " дней · неделя " + CnDataFeed.DaysPerWeek
                : "правила календаря не заданы";

            string grid = GridSignature(feed);
            if (grid != _gridSignature)
            {
                _gridSignature = grid;
                BuildMonths(feed, true);
                _marksSignature = string.Empty;
            }

            string marks = MarksSignature(feed);
            if (marks != _marksSignature)
            {
                _marksSignature = marks;
                ApplyMarks(feed);
            }

            string side = SideSignature(feed);
            if (side != _sideSignature)
            {
                _sideSignature = side;
                BuildSide(feed);
            }
        }

        /// <summary>
        /// Время игры не прочитано: дату и часы показать нечем, но правила года известны из CalendarConfig.
        /// Сетка строится из правил, без подсветки «сегодня»: пустой экран хуже честной сетки без даты.
        /// День не выбирается — без даты номер дня некуда отнести, и заметка легла бы на выдуманный год.
        /// </summary>
        private void ShowWithoutTime(CnDataFeed feed)
        {
            _clockLabel.text = string.Empty;
            _yearLabel.text = "ГОД —";
            _dateLabel.text = "время игры недоступно";
            _rulesLabel.text = feed.hasCalendarRules
                ? feed.monthsPerYear + " месяцев × " + feed.daysPerMonth + " дней · неделя " + CnDataFeed.DaysPerWeek
                : "правила календаря не заданы";

            _selected = -1;
            _today = -1;
            _form = null;

            string emptyGrid = "none#" + feed.hasCalendarRules + "#" + feed.daysPerMonth + "x" + feed.monthsPerYear;
            if (emptyGrid != _gridSignature)
            {
                _gridSignature = emptyGrid;
                _marksSignature = string.Empty;

                _daysPerMonth = feed.hasCalendarRules && feed.daysPerMonth > 0 ? feed.daysPerMonth : 30;
                _monthsPerYear = feed.hasCalendarRules && feed.monthsPerYear > 0 ? feed.monthsPerYear : 12;
                _totalDays = _daysPerMonth * _monthsPerYear;
                _yearStart = 0;
                _yearStartWeekday = 0;

                if (feed.hasCalendarRules && feed.daysPerMonth > 0 && feed.monthsPerYear > 0)
                {
                    BuildMonths(feed, false);
                }
                else
                {
                    _months.Clear();
                    _cells.Clear();
                    _dots.Clear();
                    _months.Add(CnStyle.Label("время игры недоступно — календарь не прочитан", "cn-calp__empty"));
                }
            }

            const string emptySide = "none";
            if (emptySide != _sideSignature)
            {
                _sideSignature = emptySide;
                _side.Clear();
                _typeButtons.Clear();
                _side.Add(CnStyle.Label("СЕГОДНЯ", "cn-calp__side-t"));
                _side.Add(CnStyle.Label("дата и время не пришли от сервера", "cn-calp__note"));
                _side.Add(CnStyle.Label("сетка построена по правилам года из CalendarConfig", "cn-calp__note"));
                _side.Add(CnStyle.Label("выбрать день нельзя: без даты номер дня некуда отнести", "cn-calp__note"));
            }
        }

        // ------------------------------------------------------------------ подписи состояния

        private static string Clock(float timeOfDay)
        {
            int hours = Mathf.FloorToInt(timeOfDay) % 24;
            int minutes = Mathf.FloorToInt((timeOfDay - Mathf.Floor(timeOfDay)) * 60f);
            return hours.ToString("00") + ":" + minutes.ToString("00");
        }

        /// <summary>Игровые минуты суток в ЧЧ:ММ (calendar.js hm).</summary>
        private static string Hm(int minutesOfDay)
        {
            int value = ((minutesOfDay % 1440) + 1440) % 1440;
            return (value / 60).ToString("00") + ":" + (value % 60).ToString("00");
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

        /// <summary>Выбор, «сегодня» и дни с событиями — всё, от чего зависят точки и подсветка клеток.</summary>
        private string MarksSignature(CnDataFeed feed)
        {
            var sb = new StringBuilder(64);
            sb.Append(_selected).Append('|').Append(_today).Append('|').Append(_notes.DaySignature());

            var contracts = feed.contracts;
            if (contracts != null)
            {
                for (int i = 0; i < contracts.Length; i++)
                {
                    var c = contracts[i];
                    if (!c.isActive || c.timeLimit <= 0f) continue;
                    sb.Append(c.contractId).Append(':').Append(TryDeadline(feed, c, out int day) ? day : -1).Append(';');
                }
            }

            return sb.ToString();
        }

        /// <summary>Панель дня: выбор, заметки и — пока форма закрыта — остатки сроков, которые идут каждую секунду.</summary>
        private string SideSignature(CnDataFeed feed)
        {
            var sb = new StringBuilder(96);
            sb.Append(_selected).Append('#').Append(_today).Append('#').Append(_notes.DaySignature());
            sb.Append('#').Append(_form == null ? "-" : _form.noteId ?? "new");

            // Пока открыта форма, панель не пересобирается по секундам: иначе поле текста теряло бы фокус на тике.
            if (_form != null) return sb.ToString();

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

        private void BuildMonths(CnDataFeed feed, bool interactive)
        {
            _months.Clear();
            _cells.Clear();
            _dots.Clear();

            for (int m = 1; m <= _monthsPerYear; m++) _months.Add(MonthBlock(feed, m, interactive));
        }

        private VisualElement MonthBlock(CnDataFeed feed, int month, bool interactive)
        {
            var block = CnStyle.Box("cn-calp__m");
            block.style.width = BlockWidth;
            block.style.marginRight = 10f;
            block.style.marginBottom = 10f;

            string monthName = feed.MonthName(month);

            var head = CnStyle.Row("cn-calp__mh");
            head.Add(CnStyle.Label(monthName, "cn-calp__mn"));
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

            int firstDayOfYear = (month - 1) * _daysPerMonth + 1;
            int lead = (_yearStartWeekday + firstDayOfYear - 1) % 7;
            if (lead < 0) lead += 7;

            for (int i = 0; i < lead; i++) grid.Add(BlankCell(lead + i));

            for (int d = 1; d <= _daysPerMonth; d++)
            {
                int slot = lead + d - 1;
                int weekday = (_yearStartWeekday + firstDayOfYear + d - 2) % 7;
                int absolute = _yearStart + firstDayOfYear + d - 2;

                var cell = CnStyle.Box("cn-calp__d");
                cell.style.width = CellSize;
                cell.style.height = CellSize;
                cell.style.marginLeft = slot % 7 == 0 ? 0f : CellGap;
                cell.style.marginTop = slot < 7 ? 0f : CellGap;
                CnStyle.Fill(cell, CnStyle.VeilSoft, 0.55f);

                // Точка события (calendar.js .dot): цвет первой записи дня, здесь — признак «есть события».
                var dot = CnStyle.Deco("cn-calp__dot");
                dot.style.display = DisplayStyle.None;
                cell.Add(dot);

                int dayNumber = d;
                int weekdayIndex = weekday;
                int dayAbsolute = absolute;
                _tip?.Hover(cell, () =>
                {
                    var card = CnTipCard.Make(dayNumber + " " + monthName, WeekdayName(feed, weekdayIndex));
                    if (dayAbsolute == _today) card.Row("день", "сегодня");
                    int notes = _notes.CountDay(dayAbsolute);
                    if (notes > 0) card.Row("заметок", notes.ToString());
                    return interactive ? card.Foot("клик — открыть день") : card;
                });

                if (interactive)
                {
                    // 06 §6: .cm-d/.cp-d > .halo-calhl2 — рамка 4 px, rgba(185,145,90,.95). 06 §8: calhl2 .85 s
                    // (scale 1 → 1.9, opacity .95 → 0). Кольцо погашено, импульс играет ApplyMarks.
                    var selPulse = CnStyle.Ring(cell, 0f, 4f, CnStyle.Brass, 0.95f);
                    if (selPulse != null)
                    {
                        selPulse.AddToClassList(CnHaloFx.PulseClass);
                        selPulse.style.display = DisplayStyle.None;
                    }

                    cell.RegisterCallback<ClickEvent>(evt =>
                    {
                        evt.StopPropagation();
                        SelectDay(dayAbsolute);
                    });
                }

                _cells[absolute] = cell;
                _dots[absolute] = dot;
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

        /// <summary>Состояние клеток: «сегодня», выбор и точки событий. Заливка инлайн — класс USS её не перебьёт.</summary>
        private void ApplyMarks(CnDataFeed feed)
        {
            foreach (var pair in _cells)
            {
                int day = pair.Key;
                bool today = day == _today;
                bool selected = day == _selected;

                pair.Value.EnableInClassList("is-today", today);
                pair.Value.EnableInClassList("is-sel", selected);
                CnStyle.Fill(pair.Value, today || selected ? CnStyle.GasMid : CnStyle.VeilSoft,
                    selected ? 0.98f : today ? 0.95f : 0.55f);
            }

            foreach (var pair in _dots)
                pair.Value.style.display = EventsOn(feed, pair.Key) > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            // calhl2 (06 §8): клетка уже получила класс is-sel в этом же проходе, поэтому импульс виден на выбранной.
            if (_pulseDay != int.MinValue)
            {
                if (_cells.TryGetValue(_pulseDay, out var pulseCell)) CnHaloFx.Pulse(pulseCell, 0.85f, 1.9f, 0.95f);
                _pulseDay = int.MinValue;
            }
        }

        private void SelectDay(int day)
        {
            if (day < _yearStart || day >= _yearStart + _totalDays) return;
            if (day == _selected) return;

            _selected = day;
            _pulseDay = day;   // calhl2 играет в ApplyMarks: клик дерево не строит (InvalidateDay)
            InvalidateDay();
        }

        /// <summary>Панель дня и точки пересоберутся на ближайшем кадре: обработчик клика не строит дерево сам.</summary>
        private void InvalidateDay()
        {
            _sideSignature = string.Empty;
            _marksSignature = string.Empty;
        }

        // ------------------------------------------------------------------ панель дня

        private void BuildSide(CnDataFeed feed)
        {
            _side.Clear();
            _typeButtons.Clear();

            int index = Mathf.Clamp(_selected - _yearStart, 0, _totalDays - 1);
            int month = index / _daysPerMonth + 1;
            int dayNumber = index % _daysPerMonth + 1;
            int weekday = (_yearStartWeekday + index) % CnDataFeed.DaysPerWeek;

            _side.Add(CnStyle.Label(dayNumber + " " + feed.MonthName(month) + ", " + WeekdayName(feed, weekday),
                "cn-calp__dh"));
            _side.Add(CnStyle.Label("год " + _year + " · день " + (index + 1) + " / " + _totalDays, "cn-calp__dsub"));

            int events = 0;

            // Сроки контрактов: день истечения считается по длине игровых суток, поэтому попадает в настоящую сетку.
            var contracts = feed.contracts;
            if (contracts != null)
            {
                for (int i = 0; i < contracts.Length; i++)
                {
                    var c = contracts[i];
                    if (!c.isActive || c.timeLimit <= 0f) continue;
                    if (!TryDeadline(feed, c, out int day) || day != _selected) continue;

                    events++;
                    _side.Add(ContractRow(c, DeadlineMinute(feed, c)));
                }
            }

            _notes.ForDay(_selected, _dayNotes);
            for (int i = 0; i < _dayNotes.Count; i++)
            {
                events++;
                _side.Add(NoteRow(_dayNotes[i]));
            }

            if (events == 0) _side.Add(CnStyle.Label("на этот день событий нет", "cn-calp__none"));

            if (_form != null) BuildForm();

            _side.Add(DayButtons());
            _side.Add(CnStyle.Label("событий сегодня: " + EventsOn(feed, _today) + " · заметок: " + _notes.Count,
                "cn-calp__note"));

            BuildDeadlines(feed);

            _side.Add(CnStyle.Label("игровые сутки — " + feed.dayCycleRealHours.ToString("0.#") + " ч реального времени",
                "cn-calp__note"));
            _side.Add(CnStyle.Label("срок контракта пересчитан в игровые дни по длине суток", "cn-calp__note"));
            _side.Add(CnStyle.Label("заметки — личные записи: файл порта вне Assets, на игру не влияет", "cn-calp__note"));
        }

        /// <summary>Строка события-контракта: название, время истечения и остаток (calendar.js:686 detailOf).</summary>
        private VisualElement ContractRow(CnContractRow contract, int minute)
        {
            var row = CnStyle.Row("cn-calp__ev");

            var mark = CnStyle.Deco("cn-calp__ev-i");
            CnStyle.Fill(mark, CnStyle.Warn, 0.9f);
            row.Add(mark);

            string name = string.IsNullOrEmpty(contract.displayName) ? contract.contractId : contract.displayName;

            var body = CnStyle.Box("cn-calp__ev-b");
            body.Add(CnStyle.Label(name, "cn-calp__ev-t"));
            body.Add(CnStyle.Label(Hm(minute) + " · истекает · " + FormatTimer(contract.timeRemaining),
                "cn-calp__ev-s"));
            row.Add(body);
            row.Add(CnStyle.Box("cn-grow"));

            _tip?.Hover(row, () =>
            {
                var card = CnTipCard.Make(name, "срок контракта");
                card.Row("истекает", Hm(minute));
                card.Row("осталось", FormatTimer(contract.timeRemaining));
                return card.Foot("срок пересчитан по длине игровых суток");
            });

            return row;
        }

        /// <summary>Строка заметки: цвет-метка, текст, время с типом и кнопка удаления (calendar.js cp-del).</summary>
        private VisualElement NoteRow(CnNote note)
        {
            var row = CnStyle.Row("cn-calp__ev");

            var mark = CnStyle.Deco("cn-calp__ev-i");
            CnStyle.Fill(mark, CnStyle.Accent, 0.9f);
            row.Add(mark);

            var body = CnStyle.Box("cn-calp__ev-b");
            body.Add(CnStyle.Label(string.IsNullOrEmpty(note.text) ? "Заметка" : note.text, "cn-calp__ev-t"));
            body.Add(CnStyle.Label(Hm(note.time) + " · " + CnNotesStore.TypeLabel(note.type), "cn-calp__ev-s"));
            row.Add(body);
            row.Add(CnStyle.Box("cn-grow"));

            // calendar.js:766 selectNote(): правка — та же форма, но с текстом заметки.
            body.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                OpenForm(note);
            });
            _tip?.Hover(body, () => CnTipCard.Make("ЗАМЕТКА", note.text).Foot("клик — изменить"));

            var delete = CnStyle.Box("cn-calp__ev-x");
            delete.Add(CnStyle.Label("УДАЛИТЬ", "cn-calp__ev-x-t"));
            delete.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                _notes.Delete(note.id);
                InvalidateDay();
            });
            _tip?.Hover(delete, () => CnTipCard.Make("УДАЛИТЬ", "заметку этого дня"));
            row.Add(delete);

            return row;
        }

        /// <summary>Форма заметки: текст, тип, часы 0–24 и сохранение (calendar.js .cp-form).</summary>
        private void BuildForm()
        {
            var form = CnStyle.Box("cn-calp__form");

            form.Add(CnStyle.Label("текст заметки", "cn-calp__flbl"));

            var text = new TextField { multiline = true, value = _form.text ?? string.Empty };
            text.label = null;
            text.AddToClassList("cn-calp__ta");
            ApplyFieldFont(text);
            text.RegisterValueChangedCallback(evt =>
            {
                if (_form != null) _form.text = evt.newValue;
            });
            form.Add(text);

            form.Add(CnStyle.Label("тип", "cn-calp__flbl"));

            var types = CnStyle.Box("cn-calp__types");
            for (int i = 0; i < NoteTypes.Length; i++)
            {
                string key = NoteTypes[i];
                var chip = Chip(CnNotesStore.TypeCap(key), () => SetFormType(key));
                chip.EnableInClassList("is-on", _form.type == key);
                _typeButtons[key] = chip;
                types.Add(chip);
            }
            form.Add(types);

            form.Add(CnStyle.Label("время, часы 0–24", "cn-calp__flbl"));

            var hours = new IntegerField { value = Mathf.Clamp(_form.hours, 0, 24) };
            hours.label = null;
            hours.AddToClassList("cn-calp__hour");
            ApplyFieldFont(hours);
            hours.RegisterValueChangedCallback(evt =>
            {
                if (_form != null) _form.hours = Mathf.Clamp(evt.newValue, 0, 24);
            });
            form.Add(hours);

            var buttons = CnStyle.Row("cn-calp__btns");
            buttons.Add(Chip("СОХРАНИТЬ ЗАМЕТКУ", CommitForm));
            buttons.Add(Chip("ОТМЕНА", () =>
            {
                _form = null;
                InvalidateDay();
            }));
            form.Add(buttons);

            _side.Add(form);
        }

        /// <summary>Кнопки дня мода: СЕГОДНЯ, + ЗАМЕТКА, ОЧИСТИТЬ ДЕНЬ.</summary>
        private VisualElement DayButtons()
        {
            var row = CnStyle.Row("cn-calp__btns");
            row.Add(Chip("СЕГОДНЯ", () =>
            {
                if (_today >= 0) SelectDay(_today);
            }));
            row.Add(Chip("+ ЗАМЕТКА", () => OpenForm(null)));
            row.Add(Chip("ОЧИСТИТЬ ДЕНЬ", () =>
            {
                _notes.ClearDay(_selected);
                InvalidateDay();
            }));
            return row;
        }

        private VisualElement Chip(string text, Action onClick)
        {
            var chip = CnStyle.Box("cn-calp__btn");
            chip.Add(CnStyle.Label(text, "cn-calp__btn-t"));

            var handler = onClick;
            chip.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                handler?.Invoke();
            });

            return chip;
        }

        /// <summary>Форма открывается без пересборки: тип переключается классом на готовых кнопках.</summary>
        private void SetFormType(string key)
        {
            if (_form == null) return;

            _form.type = key;
            foreach (var pair in _typeButtons) pair.Value.EnableInClassList("is-on", pair.Key == key);
        }

        private void OpenForm(CnNote note)
        {
            _form = note == null
                ? new DayForm()
                : new DayForm
                {
                    noteId = note.id,
                    text = note.text,
                    type = note.type,
                    hours = Mathf.Clamp(note.time / 60, 0, 24)
                };

            InvalidateDay();
        }

        /// <summary>Сохранение: часы 0–24 переводятся в минуты суток, как calendar.js:753 (24 ч даёт 00:00).</summary>
        private void CommitForm()
        {
            if (_form == null) return;

            int minutes = Mathf.Clamp(_form.hours, 0, 24) * 60 % 1440;
            string text = (_form.text ?? string.Empty).Trim();

            CnNote editing = _notes.Find(_form.noteId);
            if (editing != null) _notes.Edit(editing, _selected, minutes, _form.type, text);
            else _notes.Add(_selected, minutes, _form.type, text);

            _form = null;
            InvalidateDay();
        }

        /// <summary>Шрифт поля: тема панели ставит свой, поэтому SDF задаётся и полю, и его полю ввода.</summary>
        private static void ApplyFieldFont(VisualElement field)
        {
            CnStyle.ApplyFont(field);

            var input = field.Q(className: "unity-base-field__input");
            if (input != null) CnStyle.ApplyFont(input);
        }

        // ------------------------------------------------------------------ сроки и сегодня

        /// <summary>
        /// День истечения контракта в игровом времени. ContractDto.timeRemaining — реальные секунды, а связь
        /// реального времени с игровым задаёт длина игровых суток (CalendarConfig.dayCycleRealHours). Если длина
        /// суток неизвестна или время игры не прочитано, срок в сетку не ставится.
        /// </summary>
        private bool TryDeadline(CnDataFeed feed, CnContractRow contract, out int day)
        {
            day = -1;
            if (_today < 0 || feed.dayCycleRealHours <= 0f) return false;
            if (contract.timeRemaining <= 0f) return false;

            float minutesPerSecond = 1440f / (feed.dayCycleRealHours * 3600f);
            float absolute = _today * 1440f + Mathf.Clamp(feed.timeOfDay, 0f, 24f) * 60f
                             + contract.timeRemaining * minutesPerSecond;
            if (absolute < 0f) return false;

            day = Mathf.FloorToInt(absolute / 1440f);
            return true;
        }

        private int DeadlineMinute(CnDataFeed feed, CnContractRow contract)
        {
            if (!TryDeadline(feed, contract, out int day)) return 0;

            float minutesPerSecond = 1440f / (feed.dayCycleRealHours * 3600f);
            float absolute = _today * 1440f + Mathf.Clamp(feed.timeOfDay, 0f, 24f) * 60f
                             + contract.timeRemaining * minutesPerSecond;
            return Mathf.Clamp(Mathf.FloorToInt(absolute - day * 1440f), 0, 1439);
        }

        /// <summary>Сколько событий на дне: заметки игрока и сроки контрактов, истекающие в этот день.</summary>
        private int EventsOn(CnDataFeed feed, int day)
        {
            if (day < 0) return 0;

            int count = _notes.CountDay(day);

            var contracts = feed.contracts;
            if (contracts != null)
            {
                for (int i = 0; i < contracts.Length; i++)
                {
                    var c = contracts[i];
                    if (!c.isActive || c.timeLimit <= 0f) continue;
                    if (TryDeadline(feed, c, out int deadline) && deadline == day) count++;
                }
            }

            return count;
        }

        private void BuildDeadlines(CnDataFeed feed)
        {
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
