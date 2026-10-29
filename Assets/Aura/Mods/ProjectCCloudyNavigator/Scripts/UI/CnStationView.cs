// Cloudy Navigator — левая станция по src/home.js мода.
//
// Порядок блоков как в моде: шапка → прибор-календарь → STR/DEX/INT → кольцо слотов (5 | ниша + кредиты | 5)
// → МОДУЛИ I–III → репутация (последний и единственный гибкий блок, тянется до низа панели).
// Станция недвижима: ширину задаёт окно, высоту — сама колонка; ни лента, ни док её не меняют.
//
// Всё только читает CnDataFeed. Слоты — реальная экипировка (EquipmentClientState, EquipSlot Head…Module3);
// HP — PlayerTarget; кредиты — MarketClientState (как легаси); репутация — ReputationClientState + тиры ассетов.
// Где источника нет — прочерк, а не заглушка.
//
// Ступени компактности (home.js STAGES): если колонке не хватает высоты, ужимается только кольцо слотов
// (ниша под фигуру), остальные блоки и ширина станции не меняются.
using ProjectCCloudyNavigator.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Левая панель персонажа.</summary>
    public sealed class CnStationView
    {
        private const float RingHeight = 200f;      // 5 слотов по 36 + 4 зазора по 5 (home.js --nicheH)
        private const float RingMin = 108f;          // последняя ступень STAGES
        private const float SlotGap = 5f;
        private const float RepRowHeight = 27f;

        /// <summary>EquipSlot Head…WeaponOff → 0…9, Module1…3 → 10…12 (EquipmentData.SlotToIndex).</summary>
        private static readonly int[] LeftSlots = { 0, 1, 2, 3, 4 };      // Head, Chest, Legs, Feet, Back
        private static readonly int[] RightSlots = { 5, 6, 7, 8, 9 };     // Hands, Accessory1/2, WeaponMain/Off
        private static readonly int[] ModuleSlots = { 10, 11, 12 };       // Module1…3

        private static readonly string[] SlotNames =
        {
            "Голова", "Торс", "Ноги", "Обувь", "Спина", "Руки", "Аксессуар I", "Аксессуар II",
            "Основное оружие", "Вторая рука", "Модуль I", "Модуль II", "Модуль III"
        };

        private static readonly string[] SlotCodes =
        {
            "ГОЛ", "ТОР", "НОГ", "ОБВ", "СПН", "РУК", "АК I", "АК II", "ОРЖ", "ЛЕВ", "I", "II", "III"
        };

        private readonly VisualElement _root;
        private readonly VisualElement _previewHost;
        private readonly VisualElement _fig;
        private readonly VisualElement[] _slots = new VisualElement[13];
        private readonly Label[] _slotText = new Label[13];

        private readonly Label _calDate;
        private readonly VisualElement[] _calCells = new VisualElement[CnDataFeed.DaysPerWeek];
        private readonly Label _calTime;
        private readonly Label _calCount;

        private readonly Label _str;
        private readonly Label _dex;
        private readonly Label _int;

        private readonly Label _hpValue;
        private readonly VisualElement _hpFill;
        private readonly Label _credits;

        private readonly VisualElement _rel;
        private readonly Label _relCount;
        private readonly VisualElement _relRows;

        private string _equipSignature = string.Empty;
        private string _repSignature = string.Empty;
        private int _calWeekday = -1;
        private float _lastHeight = -1f;
        private int _repFit = 6;

        public VisualElement Root => _root;

        /// <summary>Ниша под превью фигуры (шаг 6): сюда окно кладёт элемент CnPreviewStage.</summary>
        public VisualElement PreviewHost => _previewHost;

        public CnStationView()
        {
            _root = CnStyle.Box("cn-station");
            CnStyle.Fill(_root, CnStyle.Veil, 0.70f);
            _root.RegisterCallback<GeometryChangedEvent>(_ => Fit());

            // ---- шапка
            _root.Add(CnStyle.Label("ДОМАШНЯЯ СТАНЦИЯ", "cn-home__kicker"));

            // ---- прибор-календарь (calendar.js .cal)
            var cal = CnStyle.Box("cn-cal");
            CnStyle.Fill(cal, CnStyle.Veil, 0.6f);
            cal.Add(CnStyle.Label("СЕГОДНЯ", "cn-cal__k"));
            _calDate = CnStyle.Label("—", "cn-cal__date");
            cal.Add(_calDate);

            var week = CnStyle.Row("cn-cal__week");
            for (int i = 0; i < _calCells.Length; i++)
            {
                var cell = CnStyle.Box("cn-cal__cell");
                if (i == 0) cell.AddToClassList("is-first");
                CnStyle.Fill(cell, CnStyle.VeilSoft, 0.7f);
                _calCells[i] = cell;
                week.Add(cell);
            }
            cal.Add(week);

            var foot = CnStyle.Row("cn-cal__foot");
            _calTime = CnStyle.Label("—", "cn-cal__time");
            foot.Add(_calTime);
            foot.Add(CnStyle.Box("cn-grow"));
            _calCount = CnStyle.Label(string.Empty, "cn-cal__count");
            foot.Add(_calCount);
            cal.Add(foot);
            _root.Add(cal);

            // ---- характеристики
            var stats = CnStyle.Row("cn-stats");
            _str = AddStat(stats, "STR", true);
            _dex = AddStat(stats, "DEX", false);
            _int = AddStat(stats, "INT", false);
            _root.Add(stats);

            // ---- кольцо слотов: 5 | ниша + кредиты | 5
            _fig = CnStyle.Row("cn-fig");
            _fig.style.alignItems = Align.Stretch;
            _fig.Add(SlotColumn(LeftSlots));

            var cent = CnStyle.Box("cn-cent");

            var niche = CnStyle.Box("cn-niche");
            var hpRow = CnStyle.Row("cn-hp");
            hpRow.Add(CnStyle.Label("HP", "cn-hp__k"));
            hpRow.Add(CnStyle.Box("cn-grow"));
            _hpValue = CnStyle.Label("—", "cn-hp__v");
            hpRow.Add(_hpValue);
            niche.Add(hpRow);

            var hpBar = CnStyle.Box("cn-hp__bar");
            CnStyle.Fill(hpBar, CnStyle.DangerLine, 0.35f);
            _hpFill = CnStyle.Box("cn-hp__fill");
            hpBar.Add(_hpFill);
            niche.Add(hpBar);

            _previewHost = CnStyle.Box("cn-niche__fig");
            niche.Add(_previewHost);
            cent.Add(niche);

            var cr = CnStyle.Box("cn-cr");
            CnStyle.Fill(cr, CnStyle.Veil, 0.68f);
            cr.Add(CnStyle.Label("КРЕДИТЫ", "cn-cr__k"));
            _credits = CnStyle.Label("—", "cn-cr__v");
            cr.Add(_credits);
            cent.Add(cr);

            _fig.Add(cent);
            _fig.Add(SlotColumn(RightSlots));
            _root.Add(_fig);

            // ---- модули I–III (вне кольца, высота не сжимается)
            var mods = CnStyle.Row("cn-mods");
            for (int i = 0; i < ModuleSlots.Length; i++)
            {
                var slot = MakeSlot(ModuleSlots[i]);
                slot.AddToClassList("cn-slot--mod");
                if (i == 0) slot.AddToClassList("is-first");
                mods.Add(slot);
            }
            _root.Add(mods);

            // ---- репутация: последний гибкий блок
            _rel = CnStyle.Box("cn-rel");
            var relHead = CnStyle.Row("cn-rel__head");
            relHead.Add(CnStyle.Label("РЕПУТАЦИЯ", "cn-rel__t"));
            relHead.Add(CnStyle.Box("cn-grow"));
            _relCount = CnStyle.Label(string.Empty, "cn-rel__n");
            relHead.Add(_relCount);
            _rel.Add(relHead);
            _relRows = CnStyle.Box("cn-rel__rows");
            _rel.Add(_relRows);
            _rel.RegisterCallback<GeometryChangedEvent>(_ => FitReputation());
            _root.Add(_rel);
        }

        // ------------------------------------------------------------------ постройка

        private static Label AddStat(VisualElement row, string key, bool first)
        {
            var cell = CnStyle.Box("cn-stat");
            if (first) cell.AddToClassList("is-first");
            CnStyle.Fill(cell, CnStyle.Veil, 0.6f);
            cell.Add(CnStyle.Label(key, "cn-stat__k"));
            var value = CnStyle.Label("—", "cn-stat__v");
            cell.Add(value);
            row.Add(cell);
            return value;
        }

        private VisualElement SlotColumn(int[] indices)
        {
            var col = CnStyle.Box("cn-col");
            for (int i = 0; i < indices.Length; i++)
            {
                var slot = MakeSlot(indices[i]);
                if (i == 0) slot.AddToClassList("is-first");
                col.Add(slot);
            }
            return col;
        }

        private VisualElement MakeSlot(int index)
        {
            var slot = CnStyle.Box("cn-slot");
            slot.AddToClassList("cn-slot--empty");
            CnStyle.Fill(slot, CnStyle.Veil, 0.4f);
            var text = CnStyle.Label(SlotCodes[index], "cn-slot__t");
            slot.Add(text);
            slot.tooltip = SlotNames[index] + " · слот пуст";
            _slots[index] = slot;
            _slotText[index] = text;
            return slot;
        }

        // ------------------------------------------------------------------ данные

        public void Refresh(CnDataFeed feed)
        {
            if (feed == null) return;

            RefreshCalendar(feed);
            RefreshStats(feed);
            RefreshHp(feed);
            _credits.text = feed.hasCredits ? FormatCredits(feed.credits) : "—";
            RefreshEquipment(feed);
            RefreshReputation(feed);
        }

        /// <summary>Формат легаси «{0:F0} CR» с разрядами, как fmtCR мода.</summary>
        public static string FormatCredits(float credits)
        {
            return Mathf.FloorToInt(credits).ToString("#,0").Replace(',', ' ').Replace('\u00a0', ' ') + " CR";
        }

        private void RefreshCalendar(CnDataFeed feed)
        {
            if (!feed.hasTime)
            {
                _calDate.text = "нет данных календаря";
                _calTime.text = "—";
                _calCount.text = string.Empty;
                SetToday(-1, null);
                return;
            }

            _calDate.text = feed.monthName + " " + feed.day + " · " + feed.weekdayName;

            int hours = Mathf.FloorToInt(feed.timeOfDay) % 24;
            int minutes = Mathf.FloorToInt((feed.timeOfDay - Mathf.Floor(feed.timeOfDay)) * 60f);
            _calTime.text = hours.ToString("00") + ":" + minutes.ToString("00");
            _calCount.text = feed.year + " Г. · Д " + feed.dayOfYear;

            SetToday(feed.weekday, feed.weekdayNames);
        }

        private void SetToday(int weekday, string[] names)
        {
            if (weekday == _calWeekday) return;
            _calWeekday = weekday;

            for (int i = 0; i < _calCells.Length; i++)
            {
                bool today = i == weekday;
                var cell = _calCells[i];
                cell.EnableInClassList("cn-cal__cell--today", today);
                if (today) cell.style.backgroundColor = CnStyle.GasMid;
                else CnStyle.Fill(cell, CnStyle.VeilSoft, 0.7f);
                cell.tooltip = names != null && i < names.Length ? names[i] : string.Empty;
            }
        }

        private void RefreshStats(CnDataFeed feed)
        {
            _str.text = feed.hasStats ? Mathf.RoundToInt(feed.strength).ToString() : "—";
            _dex.text = feed.hasStats ? Mathf.RoundToInt(feed.dexterity).ToString() : "—";
            _int.text = feed.hasStats ? Mathf.RoundToInt(feed.intelligence).ToString() : "—";
        }

        private void RefreshHp(CnDataFeed feed)
        {
            if (!feed.hasHp)
            {
                _hpValue.text = "—";
                _hpFill.style.width = Length.Percent(0f);
                return;
            }

            _hpValue.text = feed.currentHp + " / " + feed.maxHp;
            float pct = feed.maxHp > 0 ? Mathf.Clamp01((float)feed.currentHp / feed.maxHp) : 0f;
            _hpFill.style.width = Length.Percent(pct * 100f);
            _hpFill.style.backgroundColor = pct > 0.5f ? CnStyle.GasAccent : pct > 0.25f ? CnStyle.Warn : CnStyle.DangerLine;
        }

        private void RefreshEquipment(CnDataFeed feed)
        {
            var names = feed.equipNames;
            string signature = feed.hasEquipment ? "1" : "0";
            if (names != null) signature += "|" + string.Join("|", names);
            if (signature == _equipSignature) return;
            _equipSignature = signature;

            for (int i = 0; i < _slots.Length; i++)
            {
                string item = names != null && i < names.Length ? names[i] : string.Empty;
                bool empty = string.IsNullOrEmpty(item);

                var slot = _slots[i];
                slot.EnableInClassList("cn-slot--empty", empty);
                CnStyle.Fill(slot, CnStyle.Veil, empty ? 0.4f : 0.72f);

                _slotText[i].text = empty ? SlotCodes[i] : Abbreviate(item);
                slot.tooltip = SlotNames[i] + " · " + (empty
                    ? (feed.hasEquipment ? "слот пуст" : "нет данных экипировки")
                    : item);
            }
        }

        /// <summary>Подпись предмета в слоте 36 px: первые буквы имени капсом.</summary>
        private static string Abbreviate(string name)
        {
            string trimmed = name.Trim();
            int length = Mathf.Min(4, trimmed.Length);
            return trimmed.Substring(0, length).ToUpperInvariant();
        }

        private void RefreshReputation(CnDataFeed feed)
        {
            var factions = feed.factions;
            _relCount.text = factions.Length > 0 ? factions.Length.ToString() : string.Empty;

            var sb = new System.Text.StringBuilder(64);
            sb.Append(_repFit).Append('#');
            for (int i = 0; i < factions.Length; i++)
                sb.Append(factions[i].wireId).Append(':').Append(factions[i].value).Append(':').Append(factions[i].tier).Append(';');

            string signature = sb.ToString();
            if (signature == _repSignature) return;
            _repSignature = signature;

            _relRows.Clear();
            if (factions.Length == 0)
            {
                _relRows.Add(CnStyle.Label("нет известных фракций", "cn-rel__empty"));
                return;
            }

            int shown = Mathf.Min(factions.Length, _repFit);
            for (int i = 0; i < shown; i++) _relRows.Add(RepRow(factions[i], i == 0));

            if (factions.Length > shown)
                _relRows.Add(CnStyle.Label("ещё " + (factions.Length - shown), "cn-rel__more"));
        }

        /// <summary>Строка фракции: имя, тир, значение и шкала −100…+100 от центра (решение №5).</summary>
        private static VisualElement RepRow(CnFactionRow faction, bool first)
        {
            var row = CnStyle.Box("cn-rep");
            if (first) row.AddToClassList("is-first");

            var top = CnStyle.Row("cn-rep__top");
            top.Add(CnStyle.Label(faction.displayName, "cn-rep__name"));
            var tier = CnStyle.Label(faction.hasTiers ? faction.tier : "тиры не заданы", "cn-rep__tier");
            tier.style.color = faction.hasTiers ? faction.tierColor : CnStyle.Dim;
            top.Add(tier);
            var value = CnStyle.Label((faction.value > 0 ? "+" : string.Empty) + faction.value, "cn-rep__val");
            top.Add(value);
            row.Add(top);

            var bar = CnStyle.Box("cn-rep__bar");
            CnStyle.Fill(bar, CnStyle.VeilEdge, 0.45f);
            bar.Add(CnStyle.Deco("cn-rep__mid"));

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

        // ------------------------------------------------------------------ ступени компактности

        /// <summary>
        /// home.js fit(): если высоты колонки не хватает, ужимается кольцо слотов (200 → 108 px). Слоты кольца
        /// делят его высоту поровну, ширина колонок слотов 36 px не меняется.
        /// </summary>
        private void Fit()
        {
            float height = _root.resolvedStyle.height;
            if (float.IsNaN(height) || height <= 0f || Mathf.Abs(height - _lastHeight) < 1f) return;
            _lastHeight = height;

            // Всё, кроме кольца и гибкой репутации, — фиксированной высоты (меряем по факту).
            float others = 0f;
            foreach (var child in _root.Children())
            {
                if (child == _fig || child == _rel) continue;
                var s = child.resolvedStyle;
                others += child.layout.height + s.marginTop + s.marginBottom;
            }

            float padding = _root.resolvedStyle.paddingTop + _root.resolvedStyle.paddingBottom;
            float free = height - padding - others - _fig.resolvedStyle.marginTop - _rel.resolvedStyle.marginTop - 72f;
            float ring = Mathf.Clamp(Mathf.Floor(free), RingMin, RingHeight);

            _fig.style.height = ring;
            float slot = Mathf.Floor((ring - 4f * SlotGap) / 5f);
            for (int i = 0; i < 10; i++) _slots[i].style.height = slot;
        }

        /// <summary>Сколько строк фракций помещается в гибкий блок без обрезки.</summary>
        private void FitReputation()
        {
            float h = _relRows.layout.height;
            if (float.IsNaN(h) || h <= 0f) return;
            int fit = Mathf.Max(1, Mathf.FloorToInt((_rel.layout.height - 22f) / RepRowHeight));
            if (fit == _repFit) return;
            _repFit = fit;
            _repSignature = string.Empty;
        }
    }
}
