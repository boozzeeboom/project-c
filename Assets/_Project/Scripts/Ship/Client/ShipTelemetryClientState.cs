// =====================================================================================
// ShipTelemetryClientState.cs — client-side агрегатор ship telemetry (R2-SHIP-KEY-003, T-KEY-07)
// =====================================================================================
// P1-refactor: ownership читается из ShipTelemetryState.ownerClientId,
// ShipOwnershipRegistry удалён (дублировал KeyRodInstanceWorld).
// =====================================================================================

using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectC.Ship.Network;          // ShipTelemetryState
using ProjectC.Player;                // ShipController

namespace ProjectC.Ship.Client
{
    /// <summary>
    /// Client-side агрегатор ship telemetry + ownership filter.
    /// Singleton, MonoBehaviour (НЕ NetworkBehaviour — клиент-сайд).
    /// P1-refactor: ownership из ShipTelemetryState.ownerClientId (не из отдельного registry).
    /// </summary>
    public class ShipTelemetryClientState : MonoBehaviour
    {
        public static ShipTelemetryClientState Instance { get; private set; }

        // ===========================================================
        // State
        // ===========================================================

        /// <summary>shipNetId → последний известный state. Агрегируется через
        /// подписки на NetworkVariable каждого ShipController.</summary>
        private readonly Dictionary<ulong, ShipTelemetryState> _allShips = new Dictionary<ulong, ShipTelemetryState>();

        /// <summary>T-SHIP-FIX07: shipNetId → детали груза (отдельный NetworkVariable,
        /// обновляется по событиям смены груза, не 5 Hz).</summary>
        private readonly Dictionary<ulong, ShipCargoDetailState> _cargoByShip = new Dictionary<ulong, ShipCargoDetailState>();

        // ===========================================================
        // Events
        // ===========================================================

        /// <summary>Изменился state конкретного корабля. Аргумент: shipNetId.</summary>
        public event Action<ulong> OnShipStateChanged;

        /// <summary>Изменился ownership (при изменении ownerClientId в telemetry любого корабля).</summary>
        public event Action OnOwnershipUpdated;

        // ===========================================================
        // Public API
        // ===========================================================

        /// <summary>Все корабли текущего клиента (по ownerClientId в telemetry).</summary>
        public IEnumerable<KeyValuePair<ulong, ShipTelemetryState>> MyShips
        {
            get
            {
                ulong myClientId = LocalClientId;
                foreach (var kvp in _allShips)
                {
                    if (kvp.Value.ownerClientId == myClientId)
                        yield return kvp;
                }
            }
        }

        /// <summary>Ship state по netId. null если не подписан / не синхронизирован.</summary>
        public ShipTelemetryState? GetShipState(ulong shipNetId)
        {
            if (_allShips.TryGetValue(shipNetId, out var s)) return s;
            return null;
        }

        /// <summary>T-SHIP-FIX07: детали груза по netId. null если не подписаны / не синхронизированы.</summary>
        public ShipCargoDetailState? GetShipCargoDetail(ulong shipNetId)
        {
            if (_cargoByShip.TryGetValue(shipNetId, out var s)) return s;
            return null;
        }

        /// <summary>True если этот клиент владеет кораблем (есть key в инвентаре).
        /// P1-refactor: читает из telemetry, не из отдельного registry.</summary>
        public bool IsMyShip(ulong shipNetId)
        {
            ulong myClientId = LocalClientId;
            return _allShips.TryGetValue(shipNetId, out var s) && s.ownerClientId == myClientId;
        }

        /// <summary>Total ships в кэше (для отладки).</summary>
        public int TrackedShipCount => _allShips.Count;

        // ===========================================================
        // Client-side subscription API
        // ===========================================================

        /// <summary>Подписаться на NetworkVariable конкретного корабля.</summary>
        public void SubscribeToShip(ShipController ship)
        {
            if (ship == null) return;
            ulong shipNetId = ship.NetworkObjectId;

            ship.OnTelemetryStateChanged += (prev, next) => OnShipTelemetryUpdated(shipNetId, next);
            var initial = ship.TelemetryState;
            _allShips[shipNetId] = initial;
            // T-SHIP-FIX07: детали груза — отдельная подписка + seed.
            ship.OnTelemetryCargoChanged += (prev, next) => OnShipCargoUpdated(shipNetId, next);
            _cargoByShip[shipNetId] = ship.TelemetryCargoState;
            if (Debug.isDebugBuild)
                Debug.Log($"[ShipTelemetryClientState] SubscribeToShip: ship={shipNetId} ({initial.displayName})");
        }

        /// <summary>Отписаться (при despawn ShipController).</summary>
        public void UnsubscribeFromShip(ulong shipNetId)
        {
            if (_allShips.Remove(shipNetId))
                Debug.Log($"[ShipTelemetryClientState] UnsubscribeFromShip: ship={shipNetId}");
            // T-SHIP-FIX07: чистим и cargo-кэш (отписка от события корабля — FIX15, как у быстрого).
            _cargoByShip.Remove(shipNetId);
        }

        // ===========================================================
        // Lifecycle
        // ===========================================================

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ===========================================================
        // Helpers
        // ===========================================================

        private ulong LocalClientId
        {
            get
            {
                var nm = NetworkManager.Singleton;
                if (nm == null) return 0;
                if (nm.IsListening) return nm.LocalClientId;
                return 0;
            }
        }

        private void OnShipTelemetryUpdated(ulong shipNetId, ShipTelemetryState newState)
        {
            ulong oldOwner = _allShips.TryGetValue(shipNetId, out var prev) ? prev.ownerClientId : ulong.MaxValue;
            _allShips[shipNetId] = newState;
            OnShipStateChanged?.Invoke(shipNetId);

            // P1-refactor: детектим ownership change из telemetry (вместо отдельного NetworkList)
            if (oldOwner != newState.ownerClientId)
                OnOwnershipUpdated?.Invoke();
        }

        /// <summary>T-SHIP-FIX07: обновить кэш деталей груза. Поднимаем то же
        /// OnShipStateChanged — оба UI (MyShipsTab, ShipCargoConsoleWindow) уже подписаны.</summary>
        private void OnShipCargoUpdated(ulong shipNetId, ShipCargoDetailState newState)
        {
            _cargoByShip[shipNetId] = newState;
            OnShipStateChanged?.Invoke(shipNetId);
        }
    }
}
