// =====================================================================================
// ShipCargoRegistry.cs — статический реестр ShipController по NetworkObjectId (T-CARGO-06)
// =====================================================================================
// TradeWorld (серверная) логика вызывается по (shipId, shipClass) и не имеет
// ссылки на GameObject. Этот реестр — мост: server-side серверный код
// (TryLoadToShip, GetSpeedPenalty) получает ShipController, дёргает
// GetEffectiveCargoLimits() вместо хардкодного ShipClassLimits.Get(cls).
//
// Поток:
//   ShipController.OnNetworkSpawn  → registry.Register(this)
//   ShipController.OnNetworkDespawn → registry.Unregister(NetworkObjectId)
//
// Очищается автоматически при shutdown. Не сериализуется (static).
// =====================================================================================
using System.Collections.Generic;
using ProjectC.Trade.Core; // ShipClass (для fallback в GetEffectiveCargoLimits)

namespace ProjectC.Ship
{
    /// <summary>
    /// Эффективные лимиты трюма с учётом base-параметров ShipController и бонусов модулей.
    /// </summary>
    public struct CargoLimits
    {
        public int maxSlots;
        public float maxWeight;
        public float maxVolume;
        public float penaltyFactor;
    }

    /// <summary>
    /// Server-side статический реестр ShipController'ов по NetworkObjectId.
    /// Используется TradeWorld и MarketServer для получения per-instance лимитов.
    /// </summary>
    public static class ShipCargoRegistry
    {
        private static readonly Dictionary<ulong, ProjectC.Player.ShipController> _byNetworkId = new Dictionary<ulong, ProjectC.Player.ShipController>();

        public static void Register(ProjectC.Player.ShipController ship)
        {
            if (ship == null) return;
            _byNetworkId[ship.NetworkObjectId] = ship;
        }

        public static void Unregister(ulong networkObjectId)
        {
            _byNetworkId.Remove(networkObjectId);
        }

        public static ProjectC.Player.ShipController Get(ulong networkObjectId)
        {
            _byNetworkId.TryGetValue(networkObjectId, out var ship);
            return ship;
        }

        /// <summary>
        /// Получить эффективные лимиты трюма для корабля.
        /// Если корабль не зарегистрирован (например, сервер ещё не OnNetworkSpawn),
        /// возвращает null — вызывающий код использует fallback.
        /// </summary>
        public static CargoLimits? GetEffectiveLimits(ulong networkObjectId)
        {
            var ship = Get(networkObjectId);
            if (ship == null) return null;
            return ship.GetEffectiveCargoLimits();
        }

        public static void Clear()
        {
            _byNetworkId.Clear();
        }
    }
}
