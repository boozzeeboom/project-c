// Cloudy Navigator — полноэкранное окно корабля (мод v4-2: src/ships.js).
//
// Слева список кораблей игрока (A/B/C…), справа вкладки ОБЗОР / МОДУЛИ / ГРУЗ. Всё — из телеметрии
// ShipTelemetryState / TeleportCargoState и слотов ShipModuleManager: имя, класс, прочность, топливо, груз
// (слоты, не килограммы), позиция, двигатель, модули по слотам и строки груза.
//
// Выбор корабля — клиентский: на сервере «активного» корабля нет (03 §5). Перенос из трюма в багаж идёт тем же
// запросом, что в доке, и только в зоне обменника этого корабля — рынок либо его консоль трюма.
//
// 3D-вид корабля не переносится: в моде это процедурная заглушка, а настоящий префаб тащить в окно нельзя
// (в нём сетевые компоненты). Здесь корабль показан только числами телеметрии.
using System;
using System.Text;
using ProjectCCloudyNavigator.Actions;
using ProjectCCloudyNavigator.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Окно корабля: список, обзор, модули по слотам и груз с переносом в багаж.</summary>
    public sealed class CnShipPanel
    {
        private static readonly string[] TabNames = { "ОБЗОР", "МОДУЛИ", "ГРУЗ" };

        private readonly CnActions _actions;
        private readonly CnViewState _view;
        private readonly Action<CnActionResult> _onResult;
        private readonly Action<int> _onOpenBag;

        private readonly VisualElement _root;
        private readonly VisualElement _list;
        private readonly VisualElement _tabs;
        private readonly ScrollView _body;
        private readonly VisualElement _content;

        private string _signature = string.Empty;
        private string _listSignature = string.Empty;

        public CnShipPanel(CnActions actions, CnViewState view, Action<CnActionResult> onResult, Action<int> onOpenBag)
        {
            _actions = actions;
            _view = view;
            _onResult = onResult;
            _onOpenBag = onOpenBag;

            _root = CnStyle.Box("cn-shp");

            _list = CnStyle.Box("cn-shp__list");
            _root.Add(_list);

            var main = CnStyle.Box("cn-shp__main");

            _tabs = CnStyle.Row("cn-shp__tabs");
            main.Add(_tabs);

            _body = new ScrollView(ScrollViewMode.Vertical);
            _body.AddToClassList("cn-shp__body");
            _body.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _content = CnStyle.Box("cn-shp__content");
            _body.Add(_content);
            main.Add(_body);

            _root.Add(main);
        }

        public VisualElement Build() => _root;

        /// <summary>Показ: всегда начинается с обзора выбранного корабля.</summary>
        public void Open()
        {
            _view.shipTab = CnViewState.TabOverview;
            _signature = string.Empty;
            _listSignature = string.Empty;
        }

        public void Refresh(CnDataFeed feed)
        {
            if (feed == null) return;

            var ships = feed.ships ?? new CnShipRow[0];
            int index = ships.Length == 0 ? -1 : Mathf.Clamp(_view.selectedShip, 0, ships.Length - 1);
            if (index >= 0) _view.selectedShip = index;

            string listSignature = ListSignature(feed, index);
            if (listSignature != _listSignature)
            {
                _listSignature = listSignature;
                BuildList(feed, index);
            }

            string signature = BodySignature(feed, index);
            if (signature != _signature)
            {
                _signature = signature;
                BuildTabs();
                BuildBody(feed, index);
            }
        }

        // ------------------------------------------------------------------ сигнатуры

        private string ListSignature(CnDataFeed feed, int index)
        {
            var sb = new StringBuilder(96);
            sb.Append(index).Append('|').Append(_view.shipTab).Append('|');

            var ships = feed.ships;
            if (ships != null)
            {
                for (int i = 0; i < ships.Length; i++)
                {
                    var s = ships[i];
                    sb.Append(s.displayName).Append(s.className).Append(s.cargoUsed).Append('/').Append(s.cargoMax)
                      .Append(s.hullCurrent).Append(';');
                }
            }

            return sb.ToString();
        }

        private string BodySignature(CnDataFeed feed, int index)
        {
            var sb = new StringBuilder(192);
            sb.Append(index).Append('#').Append(_view.shipTab).Append('#').Append(_actions.Zone.label).Append('#');

            if (index < 0 || feed.ships == null || index >= feed.ships.Length) return sb.ToString();

            var ship = feed.ships[index];
            sb.Append(ship.displayName).Append(ship.className).Append(ship.hullCurrent).Append('/').Append(ship.hullMax)
              .Append(Mathf.RoundToInt(ship.fuelNormalized * 1000f)).Append('/').Append(Mathf.RoundToInt(ship.fuelMax))
              .Append(ship.cargoUsed).Append('/').Append(ship.cargoMax).Append(ship.isEngineRunning)
              .Append(ship.position.x.ToString("0.0")).Append(ship.position.y.ToString("0.0"))
              .Append(ship.position.z.ToString("0.0"));

            if (ship.modules != null)
            {
                for (int i = 0; i < ship.modules.Length; i++)
                {
                    var m = ship.modules[i];
                    sb.Append(m.slotType).Append(m.isOccupied).Append(m.displayName).Append(m.powerConsumption)
                      .Append(m.cargoSlotsBonus).Append(';');
                }
            }

            if (ship.cargoDetail != null)
            {
                for (int i = 0; i < ship.cargoDetail.Length; i++)
                {
                    var c = ship.cargoDetail[i];
                    sb.Append(c.itemId).Append(':').Append(c.quantity).Append(':').Append(c.dangerous ? 1 : 0)
                      .Append(c.fragile ? 1 : 0).Append(';');
                }
            }

            return sb.ToString();
        }

        // ------------------------------------------------------------------ список кораблей

        private void BuildList(CnDataFeed feed, int index)
        {
            _list.Clear();

            var ships = feed.ships ?? new CnShipRow[0];
            if (ships.Length == 0)
            {
                _list.Add(CnStyle.Label("кораблей нет", "cn-shp__empty"));
                _list.Add(CnStyle.Label("ключ корабля выдаётся в игре, окно его не создаёт", "cn-shp__note"));
                return;
            }

            for (int i = 0; i < ships.Length; i++)
            {
                var ship = ships[i];
                bool selected = i == index;

                var item = CnStyle.Box("cn-shp__item");
                if (i == 0) item.AddToClassList("is-first");
                if (selected) item.AddToClassList("is-sel");
                CnStyle.Fill(item, CnStyle.VeilSoft, selected ? 0.9f : 0.45f);

                var top = CnStyle.Row("cn-shp__itop");
                top.Add(CnStyle.Label(Letter(i), "cn-shp__iletter"));
                top.Add(CnStyle.Label(string.IsNullOrEmpty(ship.displayName) ? "корабль" : ship.displayName,
                    "cn-shp__iname"));
                item.Add(top);

                var sub = CnStyle.Row("cn-shp__isub");
                sub.Add(CnStyle.Label(string.IsNullOrEmpty(ship.className) ? "класс не задан" : ship.className.ToUpperInvariant(),
                    "cn-shp__iclass"));
                sub.Add(CnStyle.Box("cn-grow"));
                sub.Add(CnStyle.Label(ship.cargoUsed + "/" + ship.cargoMax, "cn-shp__icargo"));
                item.Add(sub);

                int shipIndex = i;
                item.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    _view.selectedShip = shipIndex;
                    _listSignature = string.Empty;
                    _signature = string.Empty;
                });

                _list.Add(item);
            }
        }

        private static string Letter(int index)
        {
            if (index < 0) return "—";
            return index < 26 ? ((char)('A' + index)).ToString() : (index + 1).ToString();
        }

        // ------------------------------------------------------------------ вкладки и тело

        private void BuildTabs()
        {
            _tabs.Clear();

            for (int i = 0; i < TabNames.Length; i++)
            {
                int tab = i;
                bool active = _view.shipTab == tab;

                var tag = CnStyle.Box("cn-shp__tab");
                if (i == 0) tag.AddToClassList("is-first");
                if (active) tag.AddToClassList("is-on");
                tag.Add(CnStyle.Label(TabNames[i], "cn-shp__tab-t"));
                tag.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    if (_view.shipTab == tab) return;
                    _view.shipTab = tab;
                    _signature = string.Empty;
                });
                _tabs.Add(tag);
            }

            _tabs.Add(CnStyle.Box("cn-grow"));
            _tabs.Add(CnStyle.Label("выбор корабля — клиентский: на сервере активного корабля нет", "cn-shp__hint"));
        }

        private void BuildBody(CnDataFeed feed, int index)
        {
            _content.Clear();

            if (index < 0)
            {
                _content.Add(CnStyle.Label("нечего показать", "cn-shp__empty"));
                _content.Add(CnStyle.Label("кораблей нет либо консоль трюма принадлежит чужому кораблю", "cn-shp__note"));
                return;
            }

            var ship = feed.ships[index];
            if (_view.shipTab == CnViewState.TabModules) BuildModules(ship);
            else if (_view.shipTab == CnViewState.TabCargo) BuildCargo(ship);
            else BuildOverview(ship, index);
        }

        private void BuildOverview(CnShipRow ship, int index)
        {
            var head = CnStyle.Row("cn-shp__ohead");
            head.Add(CnStyle.Label(string.IsNullOrEmpty(ship.displayName) ? "корабль" : ship.displayName,
                "cn-shp__oname"));
            if (!string.IsNullOrEmpty(ship.className)) head.Add(CnStyle.Label(ship.className.ToUpperInvariant(), "cn-shp__oclass"));

            var eng = CnStyle.Row("cn-shp__oeng");
            var lamp = CnStyle.Deco("cn-shp__lamp");
            lamp.style.backgroundColor = ship.isEngineRunning ? CnStyle.Accent : CnStyle.VeilEdge;
            eng.Add(lamp);
            var engText = CnStyle.Label(ship.isEngineRunning ? "ENGINE ON" : "ENGINE OFF", "cn-shp__oengt");
            engText.EnableInClassList("cn-shp__oengt--off", !ship.isEngineRunning);
            eng.Add(engText);
            head.Add(eng);
            _content.Add(head);

            var cells = CnStyle.Row("cn-shp__cells");
            cells.Add(Cell("прочность", ship.hullCurrent + " / " + ship.hullMax, true));
            cells.Add(Cell("топливо", (ship.fuelNormalized * 100f).ToString("0.0") + "%", false));
            cells.Add(Cell("груз", ship.cargoUsed + " / " + ship.cargoMax + " слотов", false));
            _content.Add(cells);

            DetailRow("позиция", "📍 (" + ship.position.x.ToString("0.0") + ", " + ship.position.y.ToString("0.0") + ", " +
                                 ship.position.z.ToString("0.0") + ")");
            DetailRow("топливо, максимум", ship.fuelMax.ToString("0") + " ед.");
            DetailRow("двигатель", ship.isEngineRunning ? "запущен" : "остановлен");
            DetailRow("модули", OccupiedModules(ship) + " из " + (ship.modules != null ? ship.modules.Length : 0));
            DetailRow("ключ корабля", ship.keyInstanceId > 0 ? "#" + ship.keyInstanceId : "не ключевой");

            var acts = CnStyle.Row("cn-shp__acts");
            var cargo = CnStyle.Tag("груз");
            cargo.AddToClassList("is-first");
            cargo.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                _view.shipTab = CnViewState.TabCargo;
                _signature = string.Empty;
            });
            acts.Add(cargo);

            var modules = CnStyle.Tag("модули");
            modules.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                _view.shipTab = CnViewState.TabModules;
                _signature = string.Empty;
            });
            acts.Add(modules);

            var bag = CnStyle.Tag("багаж");
            bag.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                _onOpenBag?.Invoke(index);
            });
            acts.Add(bag);
            _content.Add(acts);
        }

        private static int OccupiedModules(CnShipRow ship)
        {
            if (ship.modules == null) return 0;

            int count = 0;
            for (int i = 0; i < ship.modules.Length; i++) if (ship.modules[i].isOccupied) count++;
            return count;
        }

        private void BuildModules(CnShipRow ship)
        {
            var modules = ship.modules ?? new CnModuleRow[0];
            if (modules.Length == 0)
            {
                _content.Add(CnStyle.Label("слотов модулей нет", "cn-shp__empty"));
                _content.Add(CnStyle.Label("модули ставятся в игре, окно их только показывает", "cn-shp__note"));
                return;
            }

            for (int i = 0; i < modules.Length; i++)
            {
                var m = modules[i];

                var row = CnStyle.Row("cn-shp__mrow");
                if (i == 0) row.AddToClassList("is-first");
                if (!m.isOccupied) row.AddToClassList("is-empty");

                row.Add(CnStyle.Label(m.slotType, "cn-shp__mslot"));
                row.Add(CnStyle.Label(m.isOccupied ? m.displayName : "пусто", "cn-shp__mname"));
                row.Add(CnStyle.Box("cn-grow"));
                row.Add(CnStyle.Label(m.isOccupied ? m.powerConsumption + " ед. · груз +" + m.cargoSlotsBonus : "—",
                    "cn-shp__mpower"));

                row.tooltip = m.isOccupied
                    ? m.slotType + " · " + m.displayName + " · " + m.powerConsumption + " ед. питания · груз +" +
                      m.cargoSlotsBonus + " слота"
                    : m.slotType + " · слот пуст";
                _content.Add(row);
            }

            _content.Add(CnStyle.Label("питание указано по модулю (powerConsumption); общий баланс энергии считает игра",
                "cn-shp__note"));
        }

        private void BuildCargo(CnShipRow ship)
        {
            var cargo = ship.cargoDetail ?? new CnCargoRow[0];
            if (cargo.Length == 0)
            {
                _content.Add(CnStyle.Label("трюм пуст", "cn-shp__empty"));
                _content.Add(CnStyle.Label("груз появляется от контрактов и переноса из багажа", "cn-shp__note"));
                return;
            }

            bool allowed = CnViewState.ZoneAllows(_actions.Zone, ship.shipNetworkObjectId);

            for (int i = 0; i < cargo.Length; i++)
            {
                var c = cargo[i];
                string flags = (c.dangerous ? " ⚠" : string.Empty) + (c.fragile ? " ❄" : string.Empty);

                var row = CnStyle.Row("cn-shp__crow");
                if (i == 0) row.AddToClassList("is-first");

                row.Add(CnStyle.Label("×" + c.quantity, "cn-shp__cq"));
                row.Add(CnStyle.Label(c.displayName + flags, "cn-shp__cname"));
                row.Add(CnStyle.Label("(" + (c.quantity * c.unitWeight).ToString("0") + " кг)", "cn-shp__cw"));
                row.Add(CnStyle.Box("cn-grow"));

                int packs = _actions.PacksForRetrieve(c.itemId, c.quantity);
                string rate = _actions.RetrieveRateLabel(c.itemId);
                bool available = allowed && packs > 0;

                var act = CnStyle.Label("В БАГАЖ", "cn-shp__cact");
                act.EnableInClassList("is-blocked", !available);
                row.Add(act);

                row.tooltip = available
                    ? "курс: " + rate + " · упаковок: " + packs
                    : !allowed ? _actions.Zone.label : string.IsNullOrEmpty(rate) ? "курс обмена не задан" : "нет полной упаковки";

                if (available)
                {
                    string itemId = c.itemId;
                    string name = c.displayName;
                    int quantity = c.quantity;
                    ulong shipId = ship.shipNetworkObjectId;
                    row.RegisterCallback<ClickEvent>(evt =>
                    {
                        evt.StopPropagation();
                        _onResult?.Invoke(_actions.RetrieveFromShip(shipId, itemId, name, quantity, 1));
                    });
                }

                _content.Add(row);
            }

            _content.Add(CnStyle.Label("перенос идёт упаковками по курсу обмена и только в зоне рынка или консоли трюма",
                "cn-shp__note"));
        }

        // ------------------------------------------------------------------ мелкие строки

        private VisualElement Cell(string key, string value, bool first)
        {
            var cell = CnStyle.Box("cn-shp__cell");
            if (first) cell.AddToClassList("is-first");
            CnStyle.Fill(cell, CnStyle.Veil, 0.55f);
            cell.Add(CnStyle.Label(key.ToUpperInvariant(), "cn-shp__cellk"));
            cell.Add(CnStyle.Label(value, "cn-shp__cellv"));
            return cell;
        }

        private void DetailRow(string key, string value)
        {
            var row = CnStyle.Row("cn-shp__drow");
            row.Add(CnStyle.Label(key, "cn-shp__dk"));
            row.Add(CnStyle.Box("cn-grow"));
            row.Add(CnStyle.Label(value, "cn-shp__dv"));
            _content.Add(row);
        }
    }
}
