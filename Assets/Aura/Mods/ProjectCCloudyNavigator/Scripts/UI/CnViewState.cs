// Cloudy Navigator — состояние ВИДА окна: какой корабль выбран, какая вкладка корабля открыта,
// куда смотрит багаж и как он отсортирован.
//
// Это не игровое состояние: «активного» корабля на сервере нет (выбор клиентский, 03 §5), а сортировка
// и фокус багажа — свойства представления. Живёт в памяти окна и никуда не сохраняется.
using ProjectCCloudyNavigator.Actions;
using ProjectCCloudyNavigator.Data;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Выбор корабля, вкладка корабля, фокус и сортировка багажа.</summary>
    public sealed class CnViewState
    {
        public const int SortType = 0;
        public const int SortWeight = 1;
        public const int SortQuantity = 2;
        public const int SortDangerous = 3;
        public const int SortFragile = 4;

        public const int TabOverview = 0;
        public const int TabModules = 1;
        public const int TabCargo = 2;

        /// <summary>Фильтр багажа «всё» — никакого отбора.</summary>
        public const int FilterAll = -1;

        /// <summary>Фильтр багажа «надеваемое»: у предмета ItemData.equipSlot ≠ None.</summary>
        public const int FilterEquippable = -2;

        /// <summary>Выбранный кликом корабль (индекс в CnDataFeed.ships).</summary>
        public int selectedShip;

        /// <summary>Вкладка окна корабля: обзор / модули / груз.</summary>
        public int shipTab;

        /// <summary>Фокус багажа: −1 — багаж игрока, иначе индекс корабля.</summary>
        public int bagFocus = -1;

        /// <summary>Сортировка багажа (SortType…SortFragile).</summary>
        public int bagSort;

        /// <summary>Фильтр багажа: FilterAll, FilterEquippable или значение ItemType.</summary>
        public int bagFilter = FilterAll;

        /// <summary>
        /// Корабль, с которым работает док и перенос из багажа игрока: у консоли трюма — только её корабль,
        /// иначе — выбранный кликом. −1 — кораблей нет либо консоль чужого корабля.
        /// </summary>
        public static int TargetShip(CnDataFeed feed, CnViewState view, in CnExchangeZone zone)
        {
            if (feed == null || feed.ships == null || feed.ships.Length == 0) return -1;

            if (zone.allowed && !zone.isMarket && zone.consoleShipNetworkObjectId != 0)
            {
                for (int i = 0; i < feed.ships.Length; i++)
                    if (feed.ships[i].shipNetworkObjectId == zone.consoleShipNetworkObjectId) return i;
                return -1;
            }

            int index = view != null ? view.selectedShip : 0;
            if (index < 0) index = 0;
            if (index >= feed.ships.Length) index = feed.ships.Length - 1;
            return index;
        }

        /// <summary>
        /// Разрешена ли зона обменника именно для этого корабля: рынок — для любого, консоль трюма — только
        /// для своего. Проверка нужна, чтобы окно не обещало перенос в чужой трюм.
        /// </summary>
        public static bool ZoneAllows(in CnExchangeZone zone, ulong shipNetworkObjectId)
        {
            if (!zone.allowed || shipNetworkObjectId == 0) return false;
            return zone.isMarket || zone.consoleShipNetworkObjectId == shipNetworkObjectId;
        }
    }
}
