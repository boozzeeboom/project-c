// =====================================================================================
// ShipCargoClientState.cs — клиентская проекция cargo-операций (T-CARGO-UI-02)
// =====================================================================================
// Назначение: получает ShipCargoResultDto от сервера через NetworkPlayer RPC,
// дёргает событие для UI.
//
// Паттерн: ExchangeClientState (Trade/Scripts/Client/).
// =====================================================================================

using System;
using System.Collections.Generic;
using ProjectC.Ship.Network;
using ProjectC.Trade.Dto;
using ProjectC.Trade.Network;
using UnityEngine;

namespace ProjectC.Trade.Client
{
    public class ShipCargoClientState : MonoBehaviour
    {
        public static ShipCargoClientState Instance { get; private set; }

        public event Action<ShipCargoResultDto> OnResultReceived;

        /// <summary>T-CARGO-UI-03: ответ на приватный запрос деталей трюма.
        /// Аргументы: (shipNetId, success, reason).</summary>
        public event Action<ulong, bool, string> OnCargoDetailUpdated;

        /// <summary>T-CARGO-UI-03: приватный кэш деталей (заполняется только
        /// targeted-ответами сервера, не broadcast-телеметрией).</summary>
        private readonly Dictionary<ulong, ShipCargoDetailState> _detailByShip
            = new Dictionary<ulong, ShipCargoDetailState>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Вызывается из NetworkPlayer.ReceiveShipCargoResultTargetRpc.
        /// </summary>
        public void OnShipCargoResultReceived(ShipCargoResultDto result)
        {
            OnResultReceived?.Invoke(result);
        }

        /// <summary>
        /// T-CARGO-UI-03: запросить приватные детали трюма (ответ придёт только
        /// владельцу — сервер проверяет IsOwnerOfShip).
        /// </summary>
        public void RequestDetail(ulong shipNetId)
        {
            if (shipNetId == 0) return;
            var server = ShipCargoServer.Instance;
            if (server == null) return;
            server.RequestCargoDetailRpc(shipNetId);
        }

        /// <summary>
        /// T-CARGO-UI-03: приватные детали трюма по netId.
        /// null если не запрошены / в пути / отказано.
        /// </summary>
        public ShipCargoDetailState? GetCargoDetail(ulong shipNetId)
        {
            if (_detailByShip.TryGetValue(shipNetId, out var s)) return s;
            return null;
        }

        /// <summary>
        /// T-CARGO-UI-03: забыть детали (при закрытии окна).
        /// </summary>
        public void ClearCargoDetail(ulong shipNetId)
        {
            _detailByShip.Remove(shipNetId);
        }

        /// <summary>
        /// Вызывается из NetworkPlayer.ReceiveShipCargoDetailTargetRpc.
        /// </summary>
        public void OnCargoDetailReceived(ulong shipNetId, ShipCargoDetailState detail, bool success, string reason)
        {
            if (success) _detailByShip[shipNetId] = detail;
            else _detailByShip.Remove(shipNetId);
            OnCargoDetailUpdated?.Invoke(shipNetId, success, reason);
        }
    }
}
