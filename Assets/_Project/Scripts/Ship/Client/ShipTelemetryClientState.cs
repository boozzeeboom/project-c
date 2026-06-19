// =====================================================================================
// ShipTelemetryClientState.cs — client-side агрегатор ship telemetry (R2-SHIP-KEY-003, T-KEY-07)
// =====================================================================================
// Документация:
//   • docs/Ships/Key-subsystem/22_SHIP_TELEMETRY_PLAN.md §2.5
//   • docs/Ships/Key-subsystem/23_ROADMAP.md T-KEY-07
//
// Назначение: client-side singleton, агрегирует ShipTelemetryState со всех кораблей
// и OwnershipEntry из ShipOwnershipRegistry. UI/HUD подписывается на OnShipStateChanged.
//
// Клиент НЕ получает telemetry для ВСЕХ кораблей автоматически — только когда
// клиент подписан через NetworkVariable (который синхронизируется только на
// клиентов с активным NetworkObject). Сейчас NGO синхронизирует NetworkVariable
// всем клиентам по умолчанию (см. plan §2.4 — фильтрация делается клиентом).
//
// Если в будущем понадобится отфильтровать сервер-сайд (только владельцу),
// это будет Phase 2 (см. plan §5 "Открытые вопросы").
// =====================================================================================

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using ProjectC.Ship.Network;          // ShipTelemetryState, ShipOwnershipRegistry
using ProjectC.Player;                // ShipController

namespace ProjectC.Ship.Client
{
    /// <summary>
    /// Client-side агрегатор ship telemetry + ownership cache.
    /// Singleton, MonoBehaviour (НЕ NetworkBehaviour — клиент-сайд).
    /// </summary>
    public class ShipTelemetryClientState : MonoBehaviour
    {
        public static ShipTelemetryClientState Instance { get; private set; }

        // ===========================================================
        // State
        // ===========================================================

        /// <summary>shipNetId → последний известный state. Агрегируется через
        /// подписки на NetworkVariable каждого ShipController (см. SubscribeToShip).</summary>
        private readonly Dictionary<ulong, ShipTelemetryState> _allShips = new Dictionary<ulong, ShipTelemetryState>();

        /// <summary>shipNetId → owner (кэш из ShipOwnershipRegistry.OnOwnershipListChanged).</summary>
        private readonly Dictionary<ulong, ulong> _ownershipCache = new Dictionary<ulong, ulong>();

        // ===========================================================
        // Events
        // ===========================================================

        /// <summary>Изменился state конкретного корабля. Аргумент: shipNetId.</summary>
        public event Action<ulong> OnShipStateChanged;

        /// <summary>Обновился ownership список (после любого ShipOwnershipRegistry change).</summary>
        public event Action OnOwnershipUpdated;

        // ===========================================================
        // Public API
        // ===========================================================

        /// <summary>Все корабли текущего клиента (по ownership filter).</summary>
        public IEnumerable<KeyValuePair<ulong, ShipTelemetryState>> MyShips
        {
            get
            {
                ulong myClientId = LocalClientId;
                foreach (var kvp in _allShips)
                {
                    if (_ownershipCache.TryGetValue(kvp.Key, out var owner) && owner == myClientId)
                    {
                        yield return kvp;
                    }
                }
            }
        }

        /// <summary>Ship state по netId. null если не подписан / не синхронизирован.</summary>
        public ShipTelemetryState? GetShipState(ulong shipNetId)
        {
            if (_allShips.TryGetValue(shipNetId, out var s)) return s;
            return null;
        }

        /// <summary>True если этот клиент владеет кораблем (есть key в инвентаре).</summary>
        public bool IsMyShip(ulong shipNetId)
        {
            ulong myClientId = LocalClientId;
            return _ownershipCache.TryGetValue(shipNetId, out var owner) && owner == myClientId;
        }

        /// <summary>Total ships в кэше (для отладки).</summary>
        public int TrackedShipCount => _allShips.Count;

        // ===========================================================
        // Client-side subscription API
        // ===========================================================

        /// <summary>Подписаться на NetworkVariable конкретного корабля (вызывается из
        /// клиента при спавне ShipController — обычно NGO делает автоматически, но для
        /// полноты можно вызывать вручную).</summary>
        public void SubscribeToShip(ShipController ship)
        {
            if (ship == null) return;
            ulong shipNetId = ship.NetworkObjectId;

            // Подписка на NetworkVariable (ShipTelemetryState)
            ship.OnTelemetryStateChanged += (prev, next) => OnShipTelemetryUpdated(shipNetId, next);
            var initial = ship.TelemetryState;
            _allShips[shipNetId] = initial;
            Debug.Log($"[ShipTelemetryClientState] SubscribeToShip: ship={shipNetId} ({initial.displayName})");
        }

        /// <summary>Отписаться (при despawn ShipController).</summary>
        public void UnsubscribeFromShip(ulong shipNetId)
        {
            if (_allShips.Remove(shipNetId))
            {
                Debug.Log($"[ShipTelemetryClientState] UnsubscribeFromShip: ship={shipNetId}");
            }
        }

        /// <summary>Подписаться на ShipOwnershipRegistry NetworkList.</summary>
        public void SubscribeToRegistry(ShipOwnershipRegistry registry)
        {
            if (registry == null) return;
            registry.OnOwnershipListChanged += HandleOwnershipListChanged;
            // Initial snapshot
            foreach (var entry in registry.OwnershipList)
            {
                _ownershipCache[entry.shipNetworkObjectId] = entry.ownerClientId;
            }
            Debug.Log($"[ShipTelemetryClientState] SubscribeToRegistry: {_ownershipCache.Count} entries loaded");
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
                if (nm.IsListening)
                {
                    return nm.LocalClientId;
                }
                return 0;
            }
        }

        private void OnShipTelemetryUpdated(ulong shipNetId, ShipTelemetryState newState)
        {
            _allShips[shipNetId] = newState;
            OnShipStateChanged?.Invoke(shipNetId);
        }

        private void HandleOwnershipListChanged()
        {
            // NetworkList изменился на клиенте — обновить кэш
            var registry = ShipOwnershipRegistry.Instance;
            if (registry == null) return;

            _ownershipCache.Clear();
            foreach (var entry in registry.OwnershipList)
            {
                _ownershipCache[entry.shipNetworkObjectId] = entry.ownerClientId;
            }
            Debug.Log($"[ShipTelemetryClientState] Ownership updated: {_ownershipCache.Count} entries");
            OnOwnershipUpdated?.Invoke();
        }
    }
}