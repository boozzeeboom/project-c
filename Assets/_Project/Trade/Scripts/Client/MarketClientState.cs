using System;
using System.Collections.Generic;
using ProjectC.Player;
using ProjectC.Trade.Dto;
using ProjectC.Trade.Network;
using UnityEngine;

namespace ProjectC.Trade.Client
{
    /// <summary>
    /// Клиентская проекция серверного состояния рынка.
    /// Один инстанс на клиентский процесс (НЕ NetworkBehaviour).
    /// Получает snapshot'ы и trade results от сервера, держит последний
    /// известный снепшот, дёргает события для UI.
    ///
    /// UI читает ИСКЛЮЧИТЕЛЬНО из этого класса. Никаких FindObjectsByType,
    /// никаких дублирующих кэшей. Сервер — single source of truth, этот
    /// класс — projection layer.
    /// </summary>
    public class MarketClientState : MonoBehaviour
    {
        public static MarketClientState Instance { get; private set; }

        [Header("Lifecycle")]
        [Tooltip("Не уничтожать при загрузке сцены (клиент переживает стриминг)")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        public MarketSnapshotDto? CurrentSnapshot { get; private set; }
        public string CurrentLocationId => CurrentSnapshot.HasValue ? CurrentSnapshot.Value.locationId : null;

        // Последний результат (для UI feedback)
        public TradeResultDto? LastResult { get; private set; }

        // Подписки
        public event Action<MarketSnapshotDto> OnSnapshotUpdated;
        public event Action<TradeResultDto> OnTradeResult;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void OnSnapshotReceived(MarketSnapshotDto snapshot)
        {
            Debug.Log($"[MarketClientState] OnSnapshotReceived: loc={snapshot.locationId} items={(snapshot.items?.Length ?? 0)} wh={(snapshot.warehouse?.Length ?? 0)} ships={(snapshot.nearbyShips?.Length ?? 0)} credits={snapshot.credits:F0}");
            CurrentSnapshot = snapshot;
            OnSnapshotUpdated?.Invoke(snapshot);
        }

        public void OnTradeResultReceived(TradeResultDto result)
        {
            LastResult = result;
            OnTradeResult?.Invoke(result);
        }

        // ========================================================
        // CONVENIENCE API для UI и NetworkPlayer
        // ========================================================

        /// <summary>
        /// Попросить сервер прислать актуальный snapshot для locationId
        /// (вызывается из NetworkPlayer при нажатии E в зоне).
        /// </summary>
        public void RequestSubscribeMarket(string locationId)
        {
            if (string.IsNullOrEmpty(locationId))
            {
                Debug.LogWarning("[MarketClientState] RequestSubscribeMarket: locationId is empty");
                return;
            }
            if (MarketServer.Instance == null)
            {
                Debug.LogWarning("[MarketClientState] RequestSubscribeMarket: MarketServer.Instance is NULL (network not started?)");
                return;
            }
            Debug.Log($"[MarketClientState] RequestSubscribeMarket: locationId={locationId}");
            MarketServer.Instance.SubscribeMarketRpc(locationId);
        }

        public void RequestBuy(string locationId, string itemId, int quantity)
        {
            if (MarketServer.Instance == null) return;
            MarketServer.Instance.RequestBuyRpc(locationId, itemId, quantity);
        }

        public void RequestSell(string locationId, string itemId, int quantity)
        {
            if (MarketServer.Instance == null) return;
            MarketServer.Instance.RequestSellRpc(locationId, itemId, quantity);
        }

        public void RequestLoadToShip(string locationId, string itemId, int quantity, ulong shipNetworkObjectId)
        {
            if (MarketServer.Instance == null) return;
            MarketServer.Instance.RequestLoadToShipRpc(locationId, itemId, quantity, shipNetworkObjectId);
        }

        public void RequestUnloadFromShip(string locationId, string itemId, int quantity, ulong shipNetworkObjectId)
        {
            if (MarketServer.Instance == null) return;
            MarketServer.Instance.RequestUnloadFromShipRpc(locationId, itemId, quantity, shipNetworkObjectId);
        }

        // FIX (2026-06-04): Сообщить серверу, какой корабль сейчас выбран в UI
        // (ship-selector). Сервер будет включать cargo этого корабля в snapshot,
        // иначе UI не знал реальный cargo и показывал stale из _cargoCache.
        public void RequestSetSelectedShip(string locationId, ulong shipNetworkObjectId)
        {
            if (MarketServer.Instance == null) return;
            MarketServer.Instance.SetSelectedShipRpc(locationId, shipNetworkObjectId);
        }

        public void RequestSetTimeMultiplier(float multiplier)
        {
            if (MarketServer.Instance == null) return;
            MarketServer.Instance.RequestSetTimeMultiplierRpc(multiplier);
        }

        // ========================================================
        // LOCALIZATION (минимальная — для feedback сообщений)
        // ========================================================

        public static string LocalizeResultCode(TradeResultCode code)
        {
            switch (code)
            {
                case TradeResultCode.Ok: return "OK";
                case TradeResultCode.InvalidArgs: return "Некорректный запрос";
                case TradeResultCode.InternalError: return "Внутренняя ошибка";
                case TradeResultCode.NotInZone: return "Вы должны быть в зоне рынка";
                case TradeResultCode.RateLimited: return "Слишком много запросов";

                case TradeResultCode.MarketNotFound: return "Рынок не найден";
                case TradeResultCode.ItemNotInMarket: return "Товар не продаётся здесь";
                case TradeResultCode.InsufficientStock: return "Нет в наличии";
                case TradeResultCode.ItemBuyDisabled: return "Здесь нельзя купить";
                case TradeResultCode.ItemSellDisabled: return "Здесь нельзя продать";
                case TradeResultCode.PriceInvalid: return "Ошибка цены";
                case TradeResultCode.FactionRestricted: return "Торговля для вашей фракции закрыта";

                case TradeResultCode.ItemNotInWarehouse: return "Товара нет на складе";
                case TradeResultCode.WarehouseFullWeight: return "Склад переполнен по весу";
                case TradeResultCode.WarehouseFullVolume: return "Склад переполнен по объёму";
                case TradeResultCode.WarehouseFullTypes: return "Склад переполнен по типам";

                case TradeResultCode.ShipNotFound: return "Корабль не найден";
                case TradeResultCode.ShipNotInZone: return "Корабль не в зоне причала";
                case TradeResultCode.ItemNotInCargo: return "Товара нет в трюме";
                case TradeResultCode.CargoFullWeight: return "Трюм переполнен по весу";
                case TradeResultCode.CargoFullVolume: return "Трюм переполнен по объёму";
                case TradeResultCode.CargoFullSlots: return "Трюм переполнен по слотам";

                case TradeResultCode.InsufficientCredits: return "Недостаточно кредитов";
                default: return code.ToString();
            }
        }

        private static NetworkPlayer FindLocalPlayer()
        {
            var players = FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Exclude);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].IsOwner) return players[i];
            }
            return null;
        }
    }
}
