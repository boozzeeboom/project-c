// =====================================================================================
// ShipOwnershipRequirement.cs — проверка владения кораблём по ключу (R2-SHIP-KEY-003, T-KEY-03)
// =====================================================================================
// Документация:
//   • docs/Ships/Key-subsystem/21_SHIP_OWNERSHIP_MODEL.md §2.2
//   • docs/Ships/Key-subsystem/23_ROADMAP.md T-KEY-03
//
// Назначение: аналог MetaRequirement для кораблей. Вместо ItemData[] проверяет,
// что у игрока в инвентаре есть KeyRodInstance с registeredShipId == этот корабль,
// а у instance ownerPlayerId == clientId.
//
// Отличия от MetaRequirement:
//   • НЕ использует _requiredItems[] — привязка неявная через registeredShipId
//   • НЕ дёргает InventoryWorld.HasAllItems — использует KeyRodInstanceWorld.IsOwnerOfShip
//   • Display name берётся из ShipController._customDisplayName (Q6, T-KEY-00)
//     если пусто — из ShipFlightClass + "#" + NetworkObjectId
//
// Lifecycle:
//   • OnNetworkSpawn — регистрируется в MetaRequirementRegistry
//   • OnNetworkDespawn — отписывается
// =====================================================================================

using Unity.Netcode;
using UnityEngine;
using ProjectC.Player;
using ProjectC.MetaRequirement;  // T-KEY-03: MetaRequirementRegistry

namespace ProjectC.Ship.Key
{
    /// <summary>
    /// Компонент-замок для кораблей. Регистрируется в MetaRequirementRegistry
    /// как приоритетный проверяльщик для shipNetworkObjectId.
    /// Server-only: работает только на сервере (IsServer).
    /// </summary>
    [DisallowMultipleComponent]
    public class ShipOwnershipRequirement : NetworkBehaviour
    {
        // ===========================================================
        // State
        // ===========================================================

        /// <summary>Кэшированная ссылка на ShipController (для displayName, Q6).
        /// Может быть null на клиенте (нас это устраивает — проверка server-only).</summary>
        private ShipController _shipController;

        // ===========================================================
        // Lifecycle
        // ===========================================================

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;

            _shipController = GetComponent<ShipController>();

            var registry = MetaRequirementRegistry.Instance;
            if (registry != null)
            {
                registry.RegisterShipOwnership(NetworkObjectId, this);
                Debug.Log($"[ShipOwnershipRequirement] Registered: netId={NetworkObjectId}, " +
                          $"displayName='{ResolveDisplayName()}'");
            }
            else
            {
                Debug.LogWarning($"[ShipOwnershipRequirement] OnNetworkSpawn: MetaRequirementRegistry.Instance==null. " +
                                 $"Registration skipped for netId={NetworkObjectId}.");
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                var registry = MetaRequirementRegistry.Instance;
                if (registry != null)
                {
                    registry.UnregisterShipOwnership(NetworkObjectId);
                }
            }
            base.OnNetworkDespawn();
        }

        // ===========================================================
        // Public API (server-only)
        // ===========================================================

        /// <summary>Server-only: проверяет что клиент владеет ключом от этого корабля.
        /// Не требует MetaRequirement (ItemData) — владение определяется через
        /// KeyRodInstanceWorld.IsOwnerOfShip.</summary>
        /// <param name="clientId">Id игрока (владельца).</param>
        /// <param name="reason">Human-readable причина отказа если not allowed.</param>
        public bool CanPlayerUse(ulong clientId, out string reason)
        {
            reason = "";
            if (!IsServer) { reason = "not_server"; return false; }
            if (!ProjectC.Ship.Key.KeyRodInstanceWorld.IsInitialized)
            {
                reason = "Сервер ключей не инициализирован";
                return false;
            }

            bool isOwner = ProjectC.Ship.Key.KeyRodInstanceWorld.IsOwnerOfShip(clientId, NetworkObjectId);

            if (!isOwner)
            {
                reason = $"Нет ключа корабля ({ResolveDisplayName()})";
                Debug.Log($"[ShipOwnershipRequirement] Denied: client={clientId}, ship={NetworkObjectId}, " +
                          $"displayName='{ResolveDisplayName()}'");
            }

            return isOwner;
        }

        // ===========================================================
        // Helpers
        // ===========================================================

        /// <summary>Display name: приоритет — ShipController._customDisplayName (Q6),
        /// fallback — ShipFlightClass + "#" + NetworkObjectId.</summary>
        private string ResolveDisplayName()
        {
            if (_shipController != null)
            {
                string custom = _shipController.CustomDisplayName;
                if (!string.IsNullOrEmpty(custom)) return custom;

                var flightClass = _shipController.ShipFlightClass;
                return $"{flightClass} #{NetworkObjectId}";
            }
            return $"Корабль #{NetworkObjectId}";
        }
    }
}