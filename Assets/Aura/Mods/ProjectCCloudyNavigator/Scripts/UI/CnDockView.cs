// Cloudy Navigator — нижний док КОРАБЛЬ | БАГАЖ по src/ships.js и #dock/#bag мода.
//
// Две половины с волоском-разделителем, у каждой шапка 32 px и своё тело:
//   КОРАБЛЬ — имя 15 px, бирка класса, лампа двигателя, ячейки «прочность / топливо / груз», строки груза;
//   БАГАЖ   — шапка с зоной обменника и сводкой «слоты · кг», полоса плиток 56 × 56.
// Высоту дока задаёт CnMainView из свободного места ленты (fitDock мода): 96 px минимум, 40 % окна максимум.
//
// Перенос: клик по плитке багажа — одна упаковка в трюм, клик по строке груза — одна упаковка в багаж. Оба идут
// только через CnActions (зона обменника + кратность курсу); вне зоны плитки приглушены, а нажатие называет
// причину «подойди к обменнику». Выбор корабля — клиентский (на сервере «активного» корабля нет): клик по шапке
// переключает корабль, у консоли трюма всегда показан корабль этой консоли.
using System;
using System.Text;
using ProjectCCloudyNavigator.Actions;
using ProjectCCloudyNavigator.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Док КОРАБЛЬ | БАГАЖ.</summary>
    public sealed class CnDockView
    {
        public const float MinHeight = 96f;
        private const float Tile = 56f;
        private const float TileGap = 6f;
        private const float CargoRowH = 15f;

        private readonly CnActions _actions;
        private readonly CnViewState _view;
        private readonly CnTip _tip;
        private readonly CnHover _hover;
        private readonly Action<CnActionResult> _onResult;
        private readonly Action<int> _onOpenShip;
        private readonly Action _onOpenBag;

        private readonly VisualElement _root;
        private readonly Label _shipSum;
        private readonly VisualElement _shipBody;
        private readonly Label _bagHint;
        private readonly Label _bagSum;
        private readonly VisualElement _strip;

        private int _shipCount;
        private string _shipSignature = string.Empty;
        private string _bagSignature = string.Empty;
        private float _height = MinHeight;

        public VisualElement Root => _root;

        public CnDockView(CnActions actions, CnViewState view, Action<CnActionResult> onResult, Action<int> onOpenShip,
            Action onOpenBag, CnTip tip, CnHover hover)
        {
            _actions = actions;
            _view = view != null ? view : new CnViewState();
            _tip = tip;
            _hover = hover;
            _onResult = onResult;
            _onOpenShip = onOpenShip;
            _onOpenBag = onOpenBag;

            _root = CnStyle.Row("cn-dock");
            _root.style.alignItems = Align.Stretch;
            CnStyle.Fill(_root, CnStyle.Veil, 0.86f);

            // ---- КОРАБЛЬ: шапка открывает окно корабля, счётчик «1 / 2» листает корабль
            var ships = CnStyle.Box("cn-ships");
            var shipHead = CnStyle.Row("cn-dock__head");
            shipHead.AddToClassList("cn-clickable");
            shipHead.tooltip = null;
            _tip?.Hover(shipHead, () => CnTipCard.Make("КОРАБЛЬ", "телеметрия выбранного корабля").Foot("клик — окно корабля"));
            shipHead.Add(CnStyle.Label("КОРАБЛЬ", "cn-dock__t"));
            shipHead.Add(CnStyle.Label("клик — окно корабля", "cn-dock__hint"));
            shipHead.Add(CnStyle.Box("cn-grow"));
            _shipSum = CnStyle.Label(string.Empty, "cn-dock__sum");
            _shipSum.tooltip = null;
            _tip?.Hover(_shipSum, () => CnTipCard.Make("КОРАБЛИ", _shipSum.text).Foot("клик — следующий корабль"));
            shipHead.Add(_shipSum);
            shipHead.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                _onOpenShip?.Invoke(TargetShipIndexForView());
            });
            _shipSum.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                NextShip();
            });
            ships.Add(shipHead);

            _shipBody = CnStyle.Box("cn-ships__body");
            ships.Add(_shipBody);
            _root.Add(ships);

            // ---- БАГАЖ: шапка открывает полный багаж
            var bag = CnStyle.Box("cn-bag");
            var bagHead = CnStyle.Row("cn-dock__head");
            bagHead.AddToClassList("cn-clickable");
            bagHead.tooltip = null;
            _tip?.Hover(bagHead, () => CnTipCard.Make("БАГАЖ", _bagSum.text).Foot("клик — полный багаж и трюм"));
            bagHead.Add(CnStyle.Label("БАГАЖ", "cn-dock__t"));
            _bagHint = CnStyle.Label(string.Empty, "cn-dock__hint");
            bagHead.Add(_bagHint);
            bagHead.Add(CnStyle.Box("cn-grow"));
            _bagSum = CnStyle.Label(string.Empty, "cn-dock__sum");
            bagHead.Add(_bagSum);
            bagHead.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                _onOpenBag?.Invoke();
            });
            bag.Add(bagHead);

            _strip = CnStyle.Row("cn-bag__strip");
            _strip.style.flexWrap = Wrap.Wrap;
            _strip.style.alignItems = Align.FlexStart;
            _strip.RegisterCallback<GeometryChangedEvent>(_ => _bagSignature = string.Empty);
            bag.Add(_strip);
            _root.Add(bag);
        }

        /// <summary>Высота дока из свободного места ленты; меняется только при сдвиге больше гистерезиса.</summary>
        public void SetHeight(float height)
        {
            if (Mathf.Abs(height - _height) < 1f) return;
            _height = height;
            _root.style.height = height;
            _shipSignature = string.Empty;
            _bagSignature = string.Empty;
        }

        public float Height => _height;

        public void Refresh(CnDataFeed feed)
        {
            if (feed == null) return;
            RefreshShip(feed);
            RefreshBag(feed);
        }

        // ------------------------------------------------------------------ корабль

        /// <summary>
        /// Корабль дока: у консоли трюма — корабль этой консоли (переносить можно только в него), иначе —
        /// выбранный кликом по шапке. -1 — кораблей нет либо консоль чужого корабля.
        /// </summary>
        public int TargetShipIndex(CnDataFeed feed) => CnViewState.TargetShip(feed, _view, _actions.Zone);

        /// <summary>Тот же выбор, но по последним прочитанным данным — для обработчика клика.</summary>
        private int TargetShipIndexForView() => _view.selectedShip;

        private void NextShip()
        {
            if (_shipCount <= 1) return;
            _view.selectedShip = (_view.selectedShip + 1) % _shipCount;
            _shipSignature = string.Empty;
            _bagSignature = string.Empty;
        }

        private void RefreshShip(CnDataFeed feed)
        {
            _shipCount = feed.ships.Length;
            int index = TargetShipIndex(feed);

            var sb = new StringBuilder(128);
            sb.Append(index).Append('|').Append(_actions.Zone.label).Append('|');
            if (index >= 0)
            {
                var s = feed.ships[index];
                sb.Append(s.displayName).Append(s.className).Append(s.hullCurrent).Append('/').Append(s.hullMax)
                  .Append(Mathf.RoundToInt(s.fuelNormalized * 1000f)).Append(s.cargoUsed).Append('/').Append(s.cargoMax)
                  .Append(s.isEngineRunning);
                if (s.cargoDetail != null)
                    for (int i = 0; i < s.cargoDetail.Length; i++)
                        sb.Append(s.cargoDetail[i].itemId).Append(':').Append(s.cargoDetail[i].quantity).Append(',');
            }

            string signature = sb.ToString();
            if (signature == _shipSignature) return;
            _shipSignature = signature;

            _shipSum.text = feed.ships.Length > 1 ? (index + 1) + " / " + feed.ships.Length : feed.ships.Length == 1 ? "1 корабль" : string.Empty;
            _shipBody.Clear();

            if (index < 0)
            {
                _shipBody.Add(CnStyle.Label(
                    feed.ships.Length == 0 ? "кораблей нет — нужен ключ корабля" : "консоль чужого корабля",
                    "cn-dock__empty"));
                return;
            }

            var ship = feed.ships[index];

            var main = CnStyle.Row("cn-sh__main");
            main.Add(CnStyle.Label(string.IsNullOrEmpty(ship.displayName) ? "корабль" : ship.displayName, "cn-sh__name"));
            if (!string.IsNullOrEmpty(ship.className))
                main.Add(CnStyle.Label(ship.className.ToUpperInvariant(), "cn-sh__class"));
            main.Add(CnStyle.Box("cn-grow"));

            var eng = CnStyle.Row("cn-sh__eng");
            var lamp = CnStyle.Deco("cn-sh__lamp");
            lamp.style.backgroundColor = ship.isEngineRunning ? CnStyle.Accent : CnStyle.VeilEdge;
            eng.Add(lamp);
            var engText = CnStyle.Label(ship.isEngineRunning ? "ENGINE ON" : "ENGINE OFF", "cn-sh__engt");
            engText.EnableInClassList("cn-sh__engt--off", !ship.isEngineRunning);
            eng.Add(engText);
            main.Add(eng);
            _shipBody.Add(main);

            var grid = CnStyle.Row("cn-sh__grid");
            grid.Add(Cell("прочность", ship.hullCurrent + " / " + ship.hullMax, true));
            grid.Add(Cell("топливо", (ship.fuelNormalized * 100f).ToString("0.0") + "%", false));
            grid.Add(Cell("груз", ship.cargoUsed + " / " + ship.cargoMax, false));
            _shipBody.Add(grid);

            // Строки груза: сколько влезает в остаток высоты (шапка 32 + main 19 + grid 34 + зазоры).
            int rows = Mathf.Max(0, Mathf.FloorToInt((_height - 32f - 19f - 34f - 14f) / CargoRowH));
            var cargo = ship.cargoDetail;
            if (rows <= 0) return;

            if (cargo == null || cargo.Length == 0)
            {
                var empty = CnStyle.Label("трюм пуст", "cn-sh__crow");
                empty.AddToClassList("cn-sh__crow--empty");
                _shipBody.Add(empty);
                return;
            }

            var zone = _actions.Zone;
            int shown = Mathf.Min(cargo.Length, rows);
            if (cargo.Length > rows) shown = Mathf.Max(0, rows - 1);
            for (int i = 0; i < shown; i++) _shipBody.Add(CargoRow(cargo[i], ship.shipNetworkObjectId, zone.allowed));
            if (cargo.Length > shown)
            {
                var more = CnStyle.Label("ещё " + (cargo.Length - shown) + " поз.", "cn-sh__crow");
                more.AddToClassList("cn-sh__crow--more");
                _shipBody.Add(more);
            }
        }

        /// <summary>Ячейка телеметрии: подпись, значение и карточка наведения с тем же значением.</summary>
        private VisualElement Cell(string key, string value, bool first)
        {
            var cell = CnStyle.Box("cn-sh__cell");
            if (first) cell.AddToClassList("is-first");
            CnStyle.Fill(cell, CnStyle.Veil, 0.55f);
            cell.Add(CnStyle.Label(key.ToUpperInvariant(), "cn-sh__k"));
            var label = CnStyle.Label(value, "cn-sh__v");
            cell.Add(label);
            _tip?.Hover(cell, () => CnTipCard.Make(key, label.text).Foot("телеметрия корабля"));
            return cell;
        }

        private VisualElement CargoRow(CnCargoRow row, ulong shipNetId, bool zoneAllowed)
        {
            string flags = (row.dangerous ? " ⚠" : string.Empty) + (row.fragile ? " ❄" : string.Empty);
            var line = CnStyle.Row("cn-sh__crow");
            line.Add(CnStyle.Label("×" + row.quantity, "cn-sh__cq"));
            line.Add(CnStyle.Label(row.displayName + flags, "cn-sh__cn"));
            line.Add(CnStyle.Label("(" + (row.quantity * row.unitWeight).ToString("0") + " кг)", "cn-sh__cw"));

            int packs = _actions.PacksForRetrieve(row.itemId, row.quantity);
            string rate = _actions.RetrieveRateLabel(row.itemId);

            // Карточка строки груза: предмет, вес и курс переноса — всё настоящее, без «кг в слотах».
            var tips = CnTipCard.Make(row.displayName, "×" + row.quantity + " · " +
                (row.quantity * row.unitWeight).ToString("0") + " кг");
            if (row.dangerous) tips.Row("свойство", "опасный ⚠");
            if (row.fragile) tips.Row("свойство", "хрупкий ❄");
            tips.Row("в багаж", string.IsNullOrEmpty(rate) ? "курс обмена не задан" : rate + " · упаковок: " + packs);
            tips.Foot(zoneAllowed ? "клик — одна упаковка в багаж" : "подойди к обменнику", zoneAllowed ? (byte)0 : (byte)1);
            _tip?.Hover(line, () => tips);

            // Наведение на груз — тот же субъект «предмет», что у плитки багажа: лента подсвечивает свои линии.
            string hoverName = row.displayName;
            line.RegisterCallback<PointerEnterEvent>(_ => _hover?.SetItem(hoverName));
            line.RegisterCallback<PointerLeaveEvent>(_ => _hover?.Clear());

            line.EnableInClassList("is-blocked", !zoneAllowed || packs <= 0);

            string itemId = row.itemId;
            string name = row.displayName;
            int quantity = row.quantity;
            line.RegisterCallback<ClickEvent>(_ => _onResult?.Invoke(_actions.RetrieveFromShip(shipNetId, itemId, name, quantity, 1)));
            return line;
        }

        // ------------------------------------------------------------------ багаж

        private void RefreshBag(CnDataFeed feed)
        {
            var zone = _actions.Zone;
            _bagHint.text = zone.label;
            _bagHint.EnableInClassList("cn-dock__hint--zone", zone.allowed);

            float weight = 0f;
            for (int i = 0; i < feed.bagItems.Length; i++) weight += feed.bagItems[i].quantity * feed.bagItems[i].weightKg;
            _bagSum.text = "СЛОТЫ " + feed.bagItems.Length + " / " + feed.bagMaxSlots + " · " + weight.ToString("0.0") + " КГ";

            int shipIndex = TargetShipIndex(feed);
            ulong shipNetId = shipIndex >= 0 ? feed.ships[shipIndex].shipNetworkObjectId : 0;

            var sb = new StringBuilder(128);
            sb.Append(zone.label).Append('|').Append(shipNetId).Append('|');
            for (int i = 0; i < feed.bagItems.Length; i++)
                sb.Append(feed.bagItems[i].itemId).Append(':').Append(feed.bagItems[i].quantity).Append(',');

            string signature = sb.ToString();
            if (signature == _bagSignature) return;
            _bagSignature = signature;

            _strip.Clear();
            if (feed.bagItems.Length == 0)
            {
                _strip.Add(CnStyle.Label("багаж пуст", "cn-dock__empty"));
                return;
            }

            float w = _strip.layout.width;
            float h = _strip.layout.height;
            int cols = float.IsNaN(w) || w <= 0f ? 6 : Mathf.Max(1, Mathf.FloorToInt((w + TileGap) / (Tile + TileGap)));
            int lines = float.IsNaN(h) || h <= 0f ? 1 : Mathf.Max(1, Mathf.FloorToInt((h + TileGap) / (Tile + TileGap)));
            int capacity = cols * lines;

            int shown = feed.bagItems.Length <= capacity ? feed.bagItems.Length : Mathf.Max(0, capacity - 1);
            for (int i = 0; i < shown; i++)
            {
                _strip.Add(BagTile(feed.bagItems[i], shipNetId, zone.allowed, i % cols == 0, i < cols));
            }

            if (feed.bagItems.Length > shown)
            {
                var more = CnStyle.Box("cn-bag__more");
                more.Add(CnStyle.Label("ЕЩЁ " + (feed.bagItems.Length - shown), "cn-bag__moret"));
                more.tooltip = null;
                _tip?.Hover(more, () => CnTipCard.Make("БАГАЖ", "ещё " + (feed.bagItems.Length - shown) + " поз.").Foot("клик — полный багаж"));
                more.AddToClassList("cn-clickable");
                more.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    _onOpenBag?.Invoke();
                });
                if (shown % cols == 0) more.AddToClassList("is-line-first");
                if (shown < cols) more.AddToClassList("is-first-line");
                _strip.Add(more);
            }
        }

        private VisualElement BagTile(CnBagItem item, ulong shipNetId, bool zoneAllowed, bool lineFirst, bool firstLine)
        {
            var tile = CnStyle.Box("cn-tile");
            if (lineFirst) tile.AddToClassList("is-line-first");
            if (firstLine) tile.AddToClassList("is-first-line");
            CnStyle.Fill(tile, CnStyle.VeilSoft, 0.85f);

            string name = string.IsNullOrEmpty(item.itemName) ? "#" + item.itemId : item.itemName;
            tile.Add(CnStyle.Label(name, "cn-tile__name"));
            tile.Add(CnStyle.Label("×" + item.quantity, "cn-tile__q"));

            int packs = _actions.PacksForStore(item.itemId, item.quantity);
            string rate = _actions.StoreRateLabel(item.itemId);

            // Карточка плитки (bag.js tooltip.hover): имя, количество, вес и курс — реальные значения снапшота.
            var tips = CnTipCard.Make(name, "×" + item.quantity + " · " + (item.quantity * item.weightKg).ToString("0.0") + " кг");
            tips.Row("в трюм", string.IsNullOrEmpty(rate) ? "курс обмена не задан" : rate + " · упаковок: " + packs);
            bool canStore = zoneAllowed && shipNetId != 0 && packs > 0;
            tips.Foot(canStore ? "клик — одна упаковка в трюм" : "подойди к обменнику", canStore ? (byte)0 : (byte)1);
            _tip?.Hover(tile, () => tips);

            string hoverName = name;
            tile.RegisterCallback<PointerEnterEvent>(_ => _hover?.SetItem(hoverName));
            tile.RegisterCallback<PointerLeaveEvent>(_ => _hover?.Clear());

            tile.EnableInClassList("is-blocked", !zoneAllowed || shipNetId == 0 || packs <= 0);

            int itemId = item.itemId;
            int quantity = item.quantity;
            tile.RegisterCallback<ClickEvent>(_ => _onResult?.Invoke(_actions.StoreToShip(shipNetId, itemId, name, quantity, 1)));
            return tile;
        }
    }
}
