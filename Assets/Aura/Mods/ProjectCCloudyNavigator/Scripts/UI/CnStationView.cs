// Cloudy Navigator — левая станция (шаг 4).
//
// Станция недвижима: её ширина приходит из окна (--homeW мода), высоту и слоты не меняет ни док, ни лента.
// Внутри — только чтение CnDataFeed: HP (PlayerTarget), кредиты (MarketClientState), слоты экипировки,
// модули корабля, репутация с тиром, календарь. Свои значения окно не придумывает: если источника нет —
// прочерк, а не заглушка.
//
// 3D-фигура персонажа и шесть боевых слотов навыков (4.3) приходят следующими срезами шага 4: здесь под фигуру
// оставлена рамка, а не «почти похожий» силуэт.
using ProjectCCloudyNavigator.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Левая панель персонажа: HP, кредиты, слоты, модули, репутация, календарь.</summary>
    public sealed class CnStationView
    {
        private const int MaxReputationRows = 6;   // Derived: столько строк влезает в панель при 226 px
        private const int EquipSlotCount = 10;     // как EquipSlot в игре

        private readonly VisualElement _root;
        private readonly VisualElement _previewHost;
        private readonly Label _hpValue;
        private readonly VisualElement _hpFill;
        private readonly Label _credits;
        private readonly VisualElement _modules;
        private readonly Label _repCap;
        private readonly VisualElement _repRows;
        private readonly Label _calDate;
        private readonly Label _calDay;

        private string _moduleSignature = string.Empty;
        private string _reputationSignature = string.Empty;

        public VisualElement Root => _root;

        /// <summary>Рамка под превью фигуры: элемент 3D-сцены вставляет сюда окно (шаг 6).</summary>
        public VisualElement PreviewHost => _previewHost;

        public CnStationView()
        {
            _root = new VisualElement();
            _root.AddToClassList("cn-station");

            _previewHost = new VisualElement();
            _previewHost.AddToClassList("cn-silhouette");
            _root.Add(_previewHost);

            var hpRow = CnStyle.Row(null);
            hpRow.Add(CnStyle.Cap("HP"));
            hpRow.Add(CnStyle.Spacer());
            _hpValue = CnStyle.Label("—", "cn-value");
            hpRow.Add(_hpValue);
            _root.Add(hpRow);

            var hp = new VisualElement();
            hp.AddToClassList("cn-hp");
            _hpFill = new VisualElement();
            _hpFill.AddToClassList("cn-hp__fill");
            hp.Add(_hpFill);
            _root.Add(hp);

            var creditsRow = CnStyle.Row("cn-credits");
            creditsRow.Add(CnStyle.Cap("Кредиты"));
            creditsRow.Add(CnStyle.Spacer());
            _credits = CnStyle.Label("—", "cn-value");
            creditsRow.Add(_credits);
            _root.Add(creditsRow);

            _root.Add(CnStyle.Hair());

            var slots = new VisualElement();
            slots.AddToClassList("cn-slots");
            for (int i = 0; i < EquipSlotCount; i++)
            {
                var slot = new VisualElement();
                slot.AddToClassList("cn-slot");
                slot.Add(CnStyle.Label("·", "cn-slot__glyph"));
                slots.Add(slot);
            }
            _root.Add(slots);

            _modules = new VisualElement();
            _modules.style.flexDirection = FlexDirection.Column;
            _root.Add(_modules);

            _root.Add(CnStyle.Hair());

            _repCap = CnStyle.Cap("Репутация");
            _root.Add(_repCap);

            _repRows = new VisualElement();
            _repRows.AddToClassList("cn-rep");
            _root.Add(_repRows);

            _root.Add(CnStyle.Hair());

            _calDate = CnStyle.Label("—", "cn-dim");
            _root.Add(_calDate);
            _calDay = CnStyle.Label(string.Empty, "cn-dim");
            _root.Add(_calDay);
        }

        /// <summary>Обновляет подписи станции из последнего снимка. Ряды пересобираются только при изменении.</summary>
        public void Refresh(CnDataFeed feed)
        {
            if (feed == null) return;

            RefreshHp(feed);
            RefreshCredits(feed);
            RefreshModules(feed);
            RefreshReputation(feed);
            RefreshCalendar(feed);
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
        }

        private void RefreshCredits(CnDataFeed feed)
        {
            // Формат легаси: «Кредиты: {0:F0} CR» (03 §1, решение №4 — источник MarketClientState).
            _credits.text = feed.hasCredits ? Mathf.FloorToInt(feed.credits) + " CR" : "—";
        }

        private void RefreshModules(CnDataFeed feed)
        {
            var ship = feed.ships.Length > 0 ? feed.ships[0] : default;

            string signature = ship.displayName + "|" + ship.modules.Length;
            for (int i = 0; i < ship.modules.Length; i++)
            {
                signature += "|" + ship.modules[i].slotType + ":" + ship.modules[i].displayName;
            }

            if (signature == _moduleSignature) return;
            _moduleSignature = signature;

            _modules.Clear();

            // Порядок как в модели мода: МОДУЛЬ I / II / III — это Propulsion / Utility / Special.
            AddModuleRow("Модуль I", FindModule(ship, "Propulsion"));
            AddModuleRow("Модуль II", FindModule(ship, "Utility"));
            AddModuleRow("Модуль III", FindModule(ship, "Special"));
        }

        private static CnModuleRow FindModule(CnShipRow ship, string slotType)
        {
            if (ship.modules == null) return default;

            for (int i = 0; i < ship.modules.Length; i++)
            {
                if (ship.modules[i].slotType == slotType) return ship.modules[i];
            }

            return default;
        }

        private void AddModuleRow(string caption, CnModuleRow module)
        {
            var row = CnStyle.Row("cn-modrow");
            row.Add(CnStyle.Label(caption, "cn-dim"));

            var name = CnStyle.Label(
                string.IsNullOrEmpty(module.displayName) ? "пусто" : module.displayName, "cn-modrow__name");
            name.style.marginLeft = 4f;
            row.Add(name);

            _modules.Add(row);
        }

        private void RefreshReputation(CnDataFeed feed)
        {
            _repCap.text = "РЕПУТАЦИЯ · " + feed.factions.Length;

            string signature = feed.factions.Length.ToString();
            int shown = Mathf.Min(feed.factions.Length, MaxReputationRows);
            for (int i = 0; i < shown; i++)
            {
                var f = feed.factions[i];
                signature += "|" + f.wireId + ":" + f.value + ":" + (f.hasTiers ? f.tier : "-");
            }

            if (signature == _reputationSignature) return;
            _reputationSignature = signature;

            _repRows.Clear();
            for (int i = 0; i < shown; i++)
            {
                var f = feed.factions[i];
                var row = CnStyle.Row("cn-rep__row");
                row.Add(CnStyle.Label(f.displayName, "cn-rep__name"));

                // Диапазон фракций — −100…+100 (решение №5); «тиры не заданы» — честное состояние D3.
                var value = CnStyle.Label(f.value.ToString(), "cn-rep__val");
                value.style.color = f.hasTiers ? f.tierColor : CnStyle.Muted;
                row.Add(value);

                _repRows.Add(row);
            }

            if (feed.factions.Length == 0)
            {
                _repRows.Add(CnStyle.Label("нет известных фракций", "cn-dim"));
            }
        }

        private void RefreshCalendar(CnDataFeed feed)
        {
            if (!feed.hasTime)
            {
                _calDate.text = "календарь: нет данных";
                _calDay.text = string.Empty;
                return;
            }

            _calDate.text = feed.monthName + " " + feed.day + " · " + feed.weekdayName;

            int hours = Mathf.FloorToInt(feed.timeOfDay);
            int minutes = Mathf.FloorToInt((feed.timeOfDay - hours) * 60f);
            _calDay.text = feed.year + " г. · день года " + feed.dayOfYear + " · " +
                           hours.ToString("00") + ":" + minutes.ToString("00");
        }
    }
}
