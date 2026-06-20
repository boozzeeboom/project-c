// T-DOCK-04: StationComponentLocator — единый путь поиска DockStationController от любой части.
// Используется внешними системами (OuterCommZone, DockingPadTriggerBox) вместо разрозненных
// GetComponentInParent / InChildren. По канону ShipComponentLocator.
//
// Паттерн: см. Assets/_Project/Ship/ShipComponentLocator.cs.

using UnityEngine;
using ProjectC.Docking.Network; // DockStationController

namespace ProjectC.Docking.Stations
{
    public static class StationComponentLocator
    {
        /// <summary>
        /// Найти DockStationController, начиная поиск с указанного GameObject.
        /// </summary>
        /// <param name="from">GameObject, с которого начинается поиск (collider триггера, дочерний объект)</param>
        /// <returns>DockStationController или null</returns>
        public static DockStationController FindDockStationController(GameObject from)
        {
            if (from == null) return null;

            // 1. Быстрый путь: маркер (кеширует в Awake)
            var refComp = from.GetComponentInParent<StationRootReference>();
            if (refComp != null && refComp.StationController != null)
                return refComp.StationController;

            // 2. Прямой поиск
            var dsc = from.GetComponent<DockStationController>();
            if (dsc != null) return dsc;

            // 3. Поиск на родителях
            dsc = from.GetComponentInParent<DockStationController>();
            if (dsc != null) return dsc;

            // 4. Поиск в детях (для триггера-родителя, у которого DockStationController на дочернем)
            return from.GetComponentInChildren<DockStationController>();
        }

        /// <summary>
        /// Найти DockStationController от компонента. Удобно из OnTriggerEnter.
        /// </summary>
        public static DockStationController FindDockStationController(Component from)
        {
            return from == null ? null : FindDockStationController(from.gameObject);
        }
    }
}
