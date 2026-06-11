using System;
using ProjectC.Trade.Dto;
using UnityEngine;

namespace ProjectC.Trade.Client
{
    /// <summary>
    /// T-E03: Клиентская проекция обменника (MonoBehaviour).
    ///
    /// Получает результат Pack/Unpack от сервера через
    /// NetworkPlayer.ReceiveExchangeResultTargetRpc, дёргает событие для UI.
    ///
    /// Auto-spawned в NetworkManagerController.OnClientConnectedSession
    /// как root GameObject с DontDestroyOnLoad (паттерн MarketClientState).
    /// </summary>
    public class ExchangeClientState : MonoBehaviour
    {
        public static ExchangeClientState Instance { get; private set; }

        public event Action<ExchangeResultDto> OnResultReceived;

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
        /// Вызывается из NetworkPlayer.ReceiveExchangeResultTargetRpc.
        /// </summary>
        public void OnExchangeResultReceived(ExchangeResultDto result)
        {
            OnResultReceived?.Invoke(result);
        }
    }
}
