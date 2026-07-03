// =====================================================================================
// ShipCargoClientState.cs — клиентская проекция cargo-операций (T-CARGO-UI-02)
// =====================================================================================
// Назначение: получает ShipCargoResultDto от сервера через NetworkPlayer RPC,
// дёргает событие для UI.
//
// Паттерн: ExchangeClientState (Trade/Scripts/Client/).
// =====================================================================================

using System;
using ProjectC.Trade.Dto;
using UnityEngine;

namespace ProjectC.Trade.Client
{
    public class ShipCargoClientState : MonoBehaviour
    {
        public static ShipCargoClientState Instance { get; private set; }

        public event Action<ShipCargoResultDto> OnResultReceived;

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
    }
}
