using UnityEngine;
using ProjectC.Player;

namespace ProjectC.Ship
{
    /// <summary>
    /// ShipComponentLocator — единый путь поиска ShipController из любой части корабля.
    /// Используется внешними системами (WindZone, NetworkPlayer, будущие модули) вместо
    /// разрозненных GetComponentInParent / InChildren / GetComponent.
    ///
    /// Порядок поиска (от дешёвого к дорогому):
    ///   1. ShipRootReference на самом объекте или его родителях
    ///   2. ShipController на самом объекте
    ///   3. ShipController на родителях
    ///   4. ShipController в детях (для прямых триггеров-родителей)
    ///
    /// Phase 0: замена ручного поиска в WindZone (опционально).
    /// </summary>
    public static class ShipComponentLocator
    {
        /// <summary>
        /// Найти ShipController, начиная поиск с указанного GameObject.
        /// Можно передать collider игрока, collider триггера, или дочерний объект корабля.
        /// </summary>
        /// <param name="from">GameObject, с которого начинается поиск</param>
        /// <returns>ShipController или null если не нашли</returns>
        public static ShipController FindShipController(GameObject from)
        {
            if (from == null) return null;

            // 1. Быстрый путь: маркер-ссылка (кеширует результат в Awake)
            var refComp = from.GetComponentInParent<ShipRootReference>();
            if (refComp != null && refComp.ShipController != null)
            {
                return refComp.ShipController;
            }

            // 2. Прямой поиск — сам GameObject
            var sc = from.GetComponent<ShipController>();
            if (sc != null) return sc;

            // 3. Поиск на родителях
            sc = from.GetComponentInParent<ShipController>();
            if (sc != null) return sc;

            // 4. Поиск в детях (если это объект-родитель, а ShipController на дочернем)
            sc = from.GetComponentInChildren<ShipController>();
            return sc;
        }

        /// <summary>
        /// Найти ShipController, начиная поиск с компонента.
        /// Удобно из OnTriggerEnter: <c>ShipComponentLocator.FindShipController(other.gameObject)</c>.
        /// </summary>
        public static ShipController FindShipController(Component from)
        {
            return from == null ? null : FindShipController(from.gameObject);
        }
    }
}
