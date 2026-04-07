using UnityEngine;
using UnityEngine.InputSystem;
using ProjectC.Player;

namespace ProjectC.Trade
{
    /// <summary>
    /// Автономная торговая зона. НЕ вешать на корабль!
    /// Создай пустой GameObject рядом с кораблём/платформой → добавь AutoTradeZone.
    /// Сам создаст TradeUI/PlayerTradeStorage/CargoSystem при старте.
    /// </summary>
    public class AutoTradeZone : MonoBehaviour
    {
        [Header("Market")]
        [Tooltip("Назначить: Market_Primium_v01.asset или оставить пустым для авто-поиска")]
        public LocationMarket market;

        [Header("Trigger Settings")]
        [Tooltip("Радиус зоны торговли")]
        public float triggerRadius = 4f;

        private SphereCollider _triggerCollider;
        private TradeUI _tradeUI;
        private PlayerTradeStorage _storage;
        private NetworkPlayer _localPlayer;
        private bool _playerInside;

        private void Awake()
        {
            // Проверяем что market назначен
            if (market == null)
            {
                market = FindAnyMarket();
                if (market == null)
                {
                    Debug.LogError("[AutoTradeZone] Не найден ни один LocationMarket!");
                    enabled = false;
                    return;
                }
                Debug.Log($"[AutoTradeZone] Найден рынок: {market.locationName}");
            }

            // Создаём/находим TradeUI
            _tradeUI = FindAnyObjectByType<TradeUI>();
            if (_tradeUI == null)
            {
                var go = new GameObject("[TradeUI]");
                _tradeUI = go.AddComponent<TradeUI>();
                Debug.Log("[AutoTradeZone] Создан TradeUI");
            }

            // Создаём/находим PlayerTradeStorage
            _storage = FindAnyObjectByType<PlayerTradeStorage>();
            if (_storage == null)
            {
                var go = new GameObject("[PlayerTradeStorage]");
                _storage = go.AddComponent<PlayerTradeStorage>();
                Debug.Log("[AutoTradeZone] Создан PlayerTradeStorage");
            }

            // Связываем
            _tradeUI.currentMarket = market;
            _tradeUI.playerStorage = _storage;
            _tradeUI.tradeLocation = transform;

            // Добавляем CargoSystem на все корабли
            var ships = FindObjectsByType<ProjectC.Player.ShipController>(FindObjectsInactive.Exclude);
            foreach (var ship in ships)
            {
                if (ship.GetComponent<CargoSystem>() == null)
                {
                    ship.gameObject.AddComponent<CargoSystem>();
                    Debug.Log($"[AutoTradeZone] Добавлен CargoSystem на {ship.gameObject.name}");
                }
            }

            // Создаём триггер-зону (отдельный дочерний объект)
            SetupTrigger();

            // Загружаем данные
            _storage.Load();

            Debug.Log($"[AutoTradeZone] Торговая зона готова: {market.locationName}");
            Debug.Log($"[AutoTradeZone] Позиция: {transform.position}, Радиус: {triggerRadius}");
        }

        private void SetupTrigger()
        {
            // Ищем существующий SphereCollider на себе
            _triggerCollider = GetComponent<SphereCollider>();
            if (_triggerCollider == null)
            {
                _triggerCollider = gameObject.AddComponent<SphereCollider>();
            }
            _triggerCollider.isTrigger = true;
            _triggerCollider.radius = triggerRadius;
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponent<NetworkPlayer>();
            if (player == null) return;
            _playerInside = true;
            _localPlayer = player;
            Debug.Log("[AutoTradeZone] Игрок вошёл в зону. Нажми E для торговли.");
        }

        private void OnTriggerExit(Collider other)
        {
            var player = other.GetComponent<NetworkPlayer>();
            if (player == null || player != _localPlayer) return;
            _playerInside = false;
            _localPlayer = null;
            _tradeUI.CloseTrade();
            Debug.Log("[AutoTradeZone] Игрок вышел из зоны.");
        }

        private void Update()
        {
            if (!_playerInside) return;
            if (_localPlayer != null && _localPlayer.IsInShip) return;

            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                _tradeUI.OpenTrade(market);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, triggerRadius);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, triggerRadius);
        }

        private LocationMarket FindAnyMarket()
        {
            // В Editor: ищем через AssetDatabase
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:LocationMarket");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                var market = UnityEditor.AssetDatabase.LoadAssetAtPath<LocationMarket>(path);
                if (market != null && market.items != null && market.items.Count > 0 && market.items[0].item != null)
                    return market;
            }
#endif
            // Фоллбэк: создаём программатически из TradeDatabase
            Debug.LogWarning("[AutoTradeZone] Market не найден. Создаю временный из TradeDatabase.");
            var db = FindTradeDatabase();

            if (db == null || db.allItems.Count == 0)
            {
                Debug.LogError("[AutoTradeZone] TradeDatabase не найден!");
                return null;
            }

            var marketRes = ScriptableObject.CreateInstance<LocationMarket>();
            marketRes.locationId = "primium";
            marketRes.locationName = "Примум (Столица)";
            marketRes.items = new System.Collections.Generic.List<MarketItem>();
            foreach (var itemDef in db.allItems)
            {
                if (itemDef == null) continue;
                marketRes.items.Add(new MarketItem
                {
                    item = itemDef,
                    basePrice = itemDef.basePrice,
                    currentPrice = itemDef.basePrice,
                    availableStock = 100,
                    demandFactor = 0,
                    supplyFactor = 0
                });
            }
            Debug.Log($"[AutoTradeZone] Создан временный рынок с {marketRes.items.Count} товарами");
            return marketRes;
        }

        private static TradeDatabase FindTradeDatabase()
        {
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:TradeDatabase");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                return UnityEditor.AssetDatabase.LoadAssetAtPath<TradeDatabase>(path);
            }
#endif
            return Resources.Load<TradeDatabase>("Trade/TradeItemDatabase");
        }
    }
}
