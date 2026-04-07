using UnityEngine;
using ProjectC.Trade;
using ProjectC.Player;

namespace ProjectC.Trade
{
    /// <summary>
    /// Автоматическая настройка торговой системы в сцене.
    /// Прикрепляет CargoSystem на ShipController, создаёт TradeTrigger, PlayerTradeStorage, TradeUI.
    /// Сессия 4: рынок-склад-корабль.
    /// </summary>
    public class TradeSceneSetup : MonoBehaviour
    {
        [Header("Market")]
        public LocationMarket market;

        [Header("Trade Location (точка торговли)")]
        public Transform tradeLocation;

        private void Awake()
        {
            SetupCargoSystemsOnShips();
            SetupPlayerTradeStorage();
            SetupTradeUI();
            SetupTradeTrigger();
        }

        private void SetupCargoSystemsOnShips()
        {
            var ships = FindObjectsByType<ShipController>(FindObjectsInactive.Exclude);
            foreach (var ship in ships)
            {
                var cargo = ship.GetComponent<CargoSystem>();
                if (cargo == null)
                {
                    cargo = ship.gameObject.AddComponent<CargoSystem>();
                    Debug.Log($"[TradeSceneSetup] Добавлен CargoSystem на {ship.gameObject.name}");
                }
                // ShipController.cargoSystem — serialized field, назначается через инспектор
                // В runtime это не критично — TradeUI сам ищет CargoSystem рядом
            }
        }

        private void SetupPlayerTradeStorage()
        {
            var storage = FindAnyObjectByType<PlayerTradeStorage>();
            if (storage == null)
            {
                var go = new GameObject("PlayerTradeStorage");
                storage = go.AddComponent<PlayerTradeStorage>();
                storage.Load();
                Debug.Log("[TradeSceneSetup] Создан PlayerTradeStorage");
            }
        }

        private void SetupTradeUI()
        {
            var tradeUI = FindAnyObjectByType<TradeUI>();
            if (tradeUI == null)
            {
                var go = new GameObject("TradeUI");
                tradeUI = go.AddComponent<TradeUI>();
                Debug.Log("[TradeSceneSetup] Создан TradeUI");
            }

            if (market != null && tradeUI.currentMarket == null)
                tradeUI.currentMarket = market;

            var storage = FindAnyObjectByType<PlayerTradeStorage>();
            if (storage != null && tradeUI.playerStorage == null)
                tradeUI.playerStorage = storage;

            if (tradeLocation != null && tradeUI.tradeLocation == null)
                tradeUI.tradeLocation = tradeLocation;
            else if (tradeLocation == null && tradeUI.tradeLocation == null)
                tradeUI.tradeLocation = transform;
        }

        private void SetupTradeTrigger()
        {
            var trigger = FindAnyObjectByType<TradeTrigger>();
            if (trigger == null)
            {
                var go = new GameObject("TradeTrigger");
                go.transform.position = tradeLocation != null ? tradeLocation.position : transform.position;

                var col = go.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(5f, 4f, 5f);

                trigger = go.AddComponent<TradeTrigger>();
                Debug.Log("[TradeSceneSetup] Создан TradeTrigger");
            }

            if (market != null && trigger.market == null)
                trigger.market = market;
        }
    }
}
