using UnityEngine;
using UnityEngine.InputSystem;
using ProjectC.Trade;

namespace ProjectC.Trade
{
    /// <summary>
    /// Триггер для открытия торговли. Игрок входит в зону -> нажимает E -> открывается TradeUI.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TradeTrigger : MonoBehaviour
    {
        [Header("Trade")]
        public LocationMarket market;
        public string npcName = "Торговец";

        private bool _playerInside;
        private ProjectC.Player.NetworkPlayer _player;

        private TradeUI _tradeUI;

        private TradeUI TradeUI
        {
            get
            {
                if (_tradeUI == null)
                {
                    var go = GameObject.Find("TradeUI");
                    if (go != null)
                    {
                        _tradeUI = go.GetComponent<TradeUI>();
                    }
                    else
                    {
                        // Создаём TradeUI автоматически
                        go = new GameObject("TradeUI");
                        _tradeUI = go.AddComponent<TradeUI>();
                        Debug.Log("[TradeTrigger] TradeUI GameObject создан автоматически");
                    }
                }
                return _tradeUI;
            }
        }

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponent<ProjectC.Player.NetworkPlayer>();
            if (player == null) return;
            _playerInside = true;
            _player = player;
            Debug.Log($"[TradeTrigger] {npcName}: Игрок вошёл в зону торговли");
        }

        private void OnTriggerExit(Collider other)
        {
            var player = other.GetComponent<ProjectC.Player.NetworkPlayer>();
            if (player == null || player != _player) return;
            _playerInside = false;
            _player = null;
            if (TradeUI != null)
                TradeUI.CloseTrade();
            Debug.Log($"[TradeTrigger] {npcName}: Игрок вышел из зоны торговли");
        }

        private void Update()
        {
            if (!_playerInside) return;
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                if (_player != null && _player.IsInShip) return;
                if (TradeUI == null) { Debug.LogError("[TradeTrigger] TradeUI не найден!"); return; }
                if (market == null) { Debug.LogError("[TradeTrigger] Market не назначен!"); return; }
                Debug.Log($"[TradeTrigger] {npcName}: Открываю торговлю");
                TradeUI.OpenTrade(market);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.3f);
            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
            }
        }
    }
}
