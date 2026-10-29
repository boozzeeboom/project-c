// Cloudy Navigator — полноэкранный багаж (мод v4-2: src/cargowin.js, src/cargo.js).
//
// Один интерфейс для двух источников: багаж игрока (InventoryClientState) и груз корабля (тельеметрия трюма).
// Переключение фокуса — бирки «У ИГРОКА» и корабли; сортировка — ТИП / ВЕС / КОЛИЧЕСТВО / ОПАСНЫЕ / ХРУПКИЕ
// (состояние вида, не игры). Перенос — тот же запрос, что в доке: упаковками по реальному курсу обмена и только
// в зоне рынка либо консоли трюма этого корабля; вне зоны действие недоступно и объясняет причину.
//
// Второго инвентаря окно не заводит: строки читаются из тех же массивов CnDataFeed, что у дока и корабля.
using System;
using System.Collections.Generic;
using System.Text;
using ProjectC.Items;
using ProjectCCloudyNavigator.Actions;
using ProjectCCloudyNavigator.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Багаж игрока и трюм выбранного корабля: фокус, сортировка, перенос.</summary>
    public sealed class CnBagPanel
    {
        private static readonly string[] SortNames = { "ТИП", "ВЕС", "КОЛИЧЕСТВО", "ОПАСНЫЕ", "ХРУПКИЕ" };

        private readonly CnActions _actions;
        private readonly CnViewState _view;
        private readonly Action<CnActionResult> _onResult;
        private readonly CnTip _tip;
        private readonly CnHover _hover;

        private readonly VisualElement _root;
        private readonly VisualElement _focus;
        private readonly Label _summary;
        private readonly VisualElement _sorts;
        private readonly ScrollView _scroll;
        private readonly VisualElement _rows;
        private readonly Label _footer;

        private string _signature = string.Empty;

        private struct Entry
        {
            public bool fromShip;
            public string name;
            public string typeLabel;
            public int quantity;
            public float weight;
            public bool dangerous;
            public bool fragile;
            public ulong shipId;
            public string cargoItemId;
            public int inventoryItemId;
            public int packs;
            public string rate;
            public bool allowed;
        }

        public CnBagPanel(CnActions actions, CnViewState view, Action<CnActionResult> onResult, CnTip tip, CnHover hover)
        {
            _actions = actions;
            _view = view;
            _onResult = onResult;
            _tip = tip;
            _hover = hover;

            _root = CnStyle.Box("cn-bgp");

            _focus = CnStyle.Row("cn-bgp__focus");
            _root.Add(_focus);

            var head = CnStyle.Row("cn-bgp__head");
            _summary = CnStyle.Label(string.Empty, "cn-bgp__sum");
            head.Add(_summary);
            head.Add(CnStyle.Box("cn-grow"));
            _footer = CnStyle.Label(string.Empty, "cn-bgp__zone");
            head.Add(_footer);
            _root.Add(head);

            _sorts = CnStyle.Row("cn-bgp__sorts");
            _root.Add(_sorts);

            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("cn-bgp__scroll");
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _rows = CnStyle.Box("cn-bgp__rows");
            _scroll.Add(_rows);
            _root.Add(_scroll);
        }

        public VisualElement Build() => _root;

        public void Open() => _signature = string.Empty;

        public void Refresh(CnDataFeed feed)
        {
            if (feed == null) return;

            string signature = Signature(feed);
            if (signature == _signature) return;
            _signature = signature;

            BuildFocus(feed);
            BuildSorts();
            BuildRows(feed);
        }

        private string Signature(CnDataFeed feed)
        {
            var sb = new StringBuilder(192);
            sb.Append(_view.bagFocus).Append('#').Append(_view.bagSort).Append('#').Append(_actions.Zone.label).Append('#');

            var ships = feed.ships;
            if (ships != null)
            {
                for (int i = 0; i < ships.Length; i++)
                {
                    var s = ships[i];
                    sb.Append(s.displayName).Append(s.cargoUsed).Append('/').Append(s.cargoMax).Append(';');
                    if (s.cargoDetail != null)
                        for (int k = 0; k < s.cargoDetail.Length; k++)
                            sb.Append(s.cargoDetail[k].itemId).Append(':').Append(s.cargoDetail[k].quantity).Append(',');
                }
            }

            sb.Append('#').Append(feed.bagMaxSlots);

            var items = feed.bagItems;
            if (items != null)
                for (int i = 0; i < items.Length; i++)
                    sb.Append(items[i].itemId).Append(':').Append(items[i].quantity).Append(',');

            return sb.ToString();
        }

        // ------------------------------------------------------------------ фокус и сортировка

        private void BuildFocus(CnDataFeed feed)
        {
            _focus.Clear();

            var ships = feed.ships ?? new CnShipRow[0];
            if (_view.bagFocus >= ships.Length) _view.bagFocus = ships.Length - 1;
            if (_view.bagFocus < -1) _view.bagFocus = -1;

            _focus.Add(FocusTag("У ИГРОКА", _view.bagFocus == -1, -1));
            for (int i = 0; i < ships.Length; i++)
            {
                string name = string.IsNullOrEmpty(ships[i].displayName) ? "корабль" : ships[i].displayName;
                _focus.Add(FocusTag(FocusLetter(i) + " · " + name, _view.bagFocus == i, i));
            }

            _focus.Add(CnStyle.Box("cn-grow"));
        }

        private VisualElement FocusTag(string label, bool active, int index)
        {
            var tag = CnStyle.Box("cn-bgp__ftag");
            if (_focus.childCount == 0) tag.AddToClassList("is-first");
            if (active) tag.AddToClassList("is-on");
            tag.Add(CnStyle.Label(label, "cn-bgp__ftag-t"));
            tag.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                if (_view.bagFocus == index) return;
                _view.bagFocus = index;
                _signature = string.Empty;
            });
            return tag;
        }

        private static string FocusLetter(int index)
        {
            if (index < 0) return "—";
            return index < 26 ? ((char)('A' + index)).ToString() : (index + 1).ToString();
        }

        private void BuildSorts()
        {
            _sorts.Clear();
            _sorts.Add(CnStyle.Label("СОРТИРОВКА", "cn-bgp__sortk"));

            for (int i = 0; i < SortNames.Length; i++)
            {
                int sort = i;
                var tag = CnStyle.Box("cn-bgp__stab");
                if (_view.bagSort == sort) tag.AddToClassList("is-on");
                tag.Add(CnStyle.Label(SortNames[i], "cn-bgp__stab-t"));
                tag.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    if (_view.bagSort == sort) return;
                    _view.bagSort = sort;
                    _signature = string.Empty;
                });
                _sorts.Add(tag);
            }
        }

        // ------------------------------------------------------------------ строки

        private void BuildRows(CnDataFeed feed)
        {
            _rows.Clear();

            var zone = _actions.Zone;
            var entries = new List<Entry>(32);

            int focus = _view.bagFocus;
            if (focus < 0)
            {
                int target = CnViewState.TargetShip(feed, _view, zone);
                ulong shipId = target >= 0 ? feed.ships[target].shipNetworkObjectId : 0;
                bool allowed = CnViewState.ZoneAllows(zone, shipId);

                var items = feed.bagItems ?? new CnBagItem[0];
                for (int i = 0; i < items.Length; i++)
                {
                    var it = items[i];
                    entries.Add(new Entry
                    {
                        fromShip = false,
                        name = string.IsNullOrEmpty(it.itemName) ? "#" + it.itemId : it.itemName,
                        typeLabel = ItemTypeNames.GetDisplayName((ItemType)it.type),
                        quantity = it.quantity,
                        weight = it.quantity * it.weightKg,
                        shipId = shipId,
                        inventoryItemId = it.itemId,
                        packs = _actions.PacksForStore(it.itemId, it.quantity),
                        rate = _actions.StoreRateLabel(it.itemId),
                        allowed = allowed
                    });
                }

                float weight = 0f;
                for (int i = 0; i < entries.Count; i++) weight += entries[i].weight;
                _summary.text = "СЛОТЫ " + items.Length + " / " + feed.bagMaxSlots + " · " + weight.ToString("0.0") + " КГ";
            }
            else
            {
                var ships = feed.ships ?? new CnShipRow[0];
                if (focus >= ships.Length) focus = ships.Length - 1;

                if (focus < 0)
                {
                    _rows.Add(CnStyle.Label("кораблей нет", "cn-bgp__empty"));
                    _summary.text = string.Empty;
                    _footer.text = zone.label;
                    return;
                }

                var ship = ships[focus];
                bool allowed = CnViewState.ZoneAllows(zone, ship.shipNetworkObjectId);

                var cargo = ship.cargoDetail ?? new CnCargoRow[0];
                float weight = 0f;
                int pieces = 0;

                for (int i = 0; i < cargo.Length; i++)
                {
                    var c = cargo[i];
                    weight += c.quantity * c.unitWeight;
                    pieces += c.quantity;

                    entries.Add(new Entry
                    {
                        fromShip = true,
                        name = c.displayName,
                        typeLabel = "груз",
                        quantity = c.quantity,
                        weight = c.quantity * c.unitWeight,
                        dangerous = c.dangerous,
                        fragile = c.fragile,
                        shipId = ship.shipNetworkObjectId,
                        cargoItemId = c.itemId,
                        packs = _actions.PacksForRetrieve(c.itemId, c.quantity),
                        rate = _actions.RetrieveRateLabel(c.itemId),
                        allowed = allowed
                    });
                }

                string shipName = string.IsNullOrEmpty(ship.displayName) ? "корабль" : ship.displayName;
                _summary.text = shipName.ToUpperInvariant() + " · " + cargo.Length + " ПОЗ. · " + pieces + " ШТ · " +
                                weight.ToString("0.0") + " КГ · СЛОТЫ " + ship.cargoUsed + " / " + ship.cargoMax;
            }

            entries.Sort(Compare);

            if (entries.Count == 0)
            {
                _rows.Add(CnStyle.Label(_view.bagFocus < 0 ? "багаж пуст" : "трюм пуст", "cn-bgp__empty"));
            }
            else
            {
                for (int i = 0; i < entries.Count; i++) _rows.Add(Row(entries[i], i == 0));
            }

            _footer.text = zone.label;
            _footer.EnableInClassList("cn-bgp__zone--on", zone.allowed);
        }

        private int Compare(Entry a, Entry b)
        {
            switch (_view.bagSort)
            {
                case CnViewState.SortWeight:
                    return b.weight.CompareTo(a.weight);
                case CnViewState.SortQuantity:
                    return b.quantity.CompareTo(a.quantity);
                case CnViewState.SortDangerous:
                    if (a.dangerous != b.dangerous) return a.dangerous ? -1 : 1;
                    return string.CompareOrdinal(a.name, b.name);
                case CnViewState.SortFragile:
                    if (a.fragile != b.fragile) return a.fragile ? -1 : 1;
                    return string.CompareOrdinal(a.name, b.name);
                default:
                    int byType = string.CompareOrdinal(a.typeLabel, b.typeLabel);
                    return byType != 0 ? byType : string.CompareOrdinal(a.name, b.name);
            }
        }

        private VisualElement Row(Entry entry, bool first)
        {
            var row = CnStyle.Row("cn-bgp__row");
            if (first) row.AddToClassList("is-first");

            row.Add(CnStyle.Label(entry.name, "cn-bgp__name"));

            if (!entry.fromShip)
            {
                row.Add(CnStyle.Label(entry.typeLabel, "cn-bgp__type"));
            }
            else
            {
                string flags = (entry.dangerous ? " ⚠" : string.Empty) + (entry.fragile ? " ❄" : string.Empty);
                row.Add(CnStyle.Label(flags.Length > 0 ? flags.Trim() : "—", "cn-bgp__type"));
            }

            row.Add(CnStyle.Label("×" + entry.quantity, "cn-bgp__q"));
            row.Add(CnStyle.Label(entry.weight.ToString("0.0") + " кг", "cn-bgp__w"));

            bool available = entry.allowed && entry.packs > 0 && entry.shipId != 0;

            var act = CnStyle.Label(entry.fromShip ? "В БАГАЖ" : "В ТРЮМ", "cn-bgp__act");
            act.EnableInClassList("is-blocked", !available);
            row.Add(act);

            // Карточка строки (cargo.js tooltip): предмет, количество, вес, курс обмена и честная причина отказа.
            var card = CnTipCard.Make(entry.name, "×" + entry.quantity + " · " + entry.weight.ToString("0.0") + " кг");
            if (!string.IsNullOrEmpty(entry.typeLabel)) card.Row("тип", entry.typeLabel);
            if (entry.dangerous) card.Row("свойство", "опасный ⚠");
            if (entry.fragile) card.Row("свойство", "хрупкий ❄");
            card.Row("курс", string.IsNullOrEmpty(entry.rate) ? "обмена нет" : entry.rate + " · упаковок: " + entry.packs);
            card.Foot(available
                ? (entry.fromShip ? "клик — одна упаковка в багаж" : "клик — одна упаковка в трюм")
                : (!entry.allowed ? _actions.Zone.label : entry.packs <= 0 ? "нет полной упаковки по курсу" : "нет корабля для переноса"),
                available ? (byte)0 : (byte)1);
            _tip?.Hover(row, () => card);

            // Наведение на предмет — субъект «предмет»: лента подсвечивает линии, которым он нужен.
            string hoverName = entry.name;
            row.RegisterCallback<PointerEnterEvent>(_ => _hover?.SetItem(hoverName));
            row.RegisterCallback<PointerLeaveEvent>(_ => _hover?.Clear());

            if (available)
            {
                Entry captured = entry;
                row.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    if (captured.fromShip)
                        _onResult?.Invoke(_actions.RetrieveFromShip(captured.shipId, captured.cargoItemId,
                            captured.name, captured.quantity, 1));
                    else
                        _onResult?.Invoke(_actions.StoreToShip(captured.shipId, captured.inventoryItemId,
                            captured.name, captured.quantity, 1));
                });
            }

            return row;
        }

        private string BuildTooltip(Entry entry, bool available)
        {
            var sb = new StringBuilder(96);
            sb.Append(entry.name).Append(" ×").Append(entry.quantity).Append(" · ").Append(entry.weight.ToString("0.0"))
              .Append(" кг");

            if (!string.IsNullOrEmpty(entry.rate)) sb.Append('\n').Append("курс: ").Append(entry.rate).Append(" · упаковок: ").Append(entry.packs);

            if (!available)
            {
                sb.Append('\n').Append(!entry.allowed
                    ? _actions.Zone.label
                    : entry.packs <= 0 ? "нет полной упаковки по курсу" : "нет корабля для переноса");
            }

            return sb.ToString();
        }
    }
}
