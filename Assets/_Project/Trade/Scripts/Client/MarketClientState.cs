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

        // FIX (2026-06-05): Per-ship клиентский кэш cargo всех кораблей в зоне.
        // Сервер шлёт shipCargos[] в каждом MarketSnapshotDto; здесь собираем
        // Dictionary<shipId, cargo[]> для мгновенного переключения в ship-selector
        // (без ожидания следующего snapshot / RPC roundtrip). Ключ — shipNetworkObjectId,
        // значение — копия массива WarehouseEntryDto (immutable с точки зрения клиента).
        // Если корабль отсутствует в кэше — cargo трактуется как пустой массив.
        // См. docs/Markets/FIXES_HISTORY.md 2026-06-05.
        public IReadOnlyDictionary<ulong, WarehouseEntryDto[]> CurrentShipCargos { get; private set; }
            = new Dictionary<ulong, WarehouseEntryDto[]>();

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
            Debug.Log($"[MarketClientState] OnSnapshotReceived: loc={snapshot.locationId} items={(snapshot.items?.Length ?? 0)} wh={(snapshot.warehouse?.Length ?? 0)} ships={(snapshot.nearbyShips?.Length ?? 0)} shipCargos={(snapshot.shipCargos?.Length ?? 0)} credits={snapshot.credits:F0}");
            CurrentSnapshot = snapshot;

            // FIX (2026-06-05): собрать per-ship кэш cargo из snapshot.shipCargos.
            // Копируем массивы (immutable view для подписчиков) — чтобы UI случайно
            // не мутировал серверные данные и не возникло рассинхрона с будущим snapshot.
            var newShipCargos = new Dictionary<ulong, WarehouseEntryDto[]>();
            if (snapshot.shipCargos != null)
            {
                for (int i = 0; i < snapshot.shipCargos.Length; i++)
                {
                    var sc = snapshot.shipCargos[i];
                    if (sc.shipNetworkObjectId == 0) continue;
                    newShipCargos[sc.shipNetworkObjectId] = sc.cargo != null
                        ? (WarehouseEntryDto[])sc.cargo.Clone()
                        : Array.Empty<WarehouseEntryDto>();
                }
            }
            CurrentShipCargos = newShipCargos;

            OnSnapshotUpdated?.Invoke(snapshot);
        }

        public void OnTradeResultReceived(TradeResultDto result)
        {
            LastResult = result;
            OnTradeResult?.Invoke(result);
        }

        /// <summary>
        /// FIX (2026-06-05): обновить per-ship кэш cargo для одного корабля
        /// (например, после успешного Load/Unload — TradeResultDto.updatedCargoSnapshot).
        /// UI вызывает это в MarketWindow.HandleTradeResult, чтобы при следующем
        /// переключении на этот корабль cargo был корректен без ожидания snapshot.
        /// </summary>
        public void UpdateShipCargo(ulong shipNetworkObjectId, WarehouseEntryDto[] cargo)
        {
            if (shipNetworkObjectId == 0) return;
            var newCache = new Dictionary<ulong, WarehouseEntryDto[]>(CurrentShipCargos);
            newCache[shipNetworkObjectId] = cargo != null
                ? (WarehouseEntryDto[])cargo.Clone()
                : Array.Empty<WarehouseEntryDto>();
            CurrentShipCargos = newCache;
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
            return ProjectC.Localization.Loc.Get($"sys.market.{ProjectC.Localization.Loc.ToSnakeCase(code.ToString())}");
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
