using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Trade.Network
{
    /// <summary>
    /// Scene-placed маркер зоны NPC-агента НП (доска контрактов).
    /// Аналог <c>MarketZone</c> для рынков, но без shipDockRadius (у NPC нет
    /// причала — игрок подходит пешком).
    ///
    /// На сервере:
    ///   • Содержит trigger (sphere), который детектит игроков внутри зоны.
    ///   • Регистрирует себя в <see cref="ContractZoneRegistry"/> по locationId.
    ///   • Хранит список playerId, находящихся в зоне (для position-check при RPC).
    ///
    /// На клиенте:
    ///   • Регистрируется, чтобы <see cref="Client.ContractInteractor"/> мог найти
    ///     ближайшую зону при нажатии E.
    ///   • Poll'ит OverlapSphere, чтобы LocalPlayerZone обновлялся даже если
    ///     OnTriggerEnter пропустил (race condition: CharacterController + SphereCollider).
    ///
    /// C2-этап миграции контрактов на v2-архитектуру (см. docs/dev/CONTRACT_V2_MIGRATION.md).
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class ContractZone : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("ID локации (primium/secundus/tertius/quartus) — должен совпадать с MarketZone")]
        [SerializeField] private string locationId = "";

        [Tooltip("Отображаемое имя зоны для UI заголовка")]
        [SerializeField] private string displayName = "";

        [Header("Zone")]
        [SerializeField, Min(0.1f)] private float tradeRadius = 5f;
        [SerializeField] private bool drawGizmos = true;

        public string LocationId => locationId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? locationId : displayName;
        public float TradeRadius => tradeRadius;

        // Серверные данные
        private readonly HashSet<ulong> _playersInZone = new HashSet<ulong>();
        public IReadOnlyCollection<ulong> PlayersInZone => _playersInZone;

        private SphereCollider _sphere;
        private float _pollTimer;

        // FIX: debounce counter для PollLocalPlayerZone
        private readonly Dictionary<ulong, int> _missingTicks = new Dictionary<ulong, int>();
        private const int MISS_THRESHOLD = 3; // ~0.75с пропусков перед удалением

        private void Awake()
        {
            _sphere = GetComponent<SphereCollider>();
            _sphere.isTrigger = true;
            _sphere.radius = tradeRadius;
        }

        private void OnEnable()
        {
            ContractZoneRegistry.Register(this);
        }

        private void OnDisable()
        {
            ContractZoneRegistry.Unregister(this);
        }

        private void Update()
        {
            // Poll'инг для клиента (LocalPlayerZone) и сервера (_playersInZone).
            // Делаем с фиксированной частотой чтобы не делать OverlapSphere каждый кадр.
            _pollTimer -= Time.deltaTime;
            if (_pollTimer > 0f) return;
            _pollTimer = 0.25f;

            PollPlayersInRadius();
        }

        private void PollPlayersInRadius()
        {
            // Серверный сценарий: обновляем _playersInZone для position-check.
            // Клиентский сценарий: обновляем ContractZoneRegistry.LocalPlayerZone.
            // Логика общая — ищем NetworkPlayer, IsOwner=true, в радиусе.
            var players = FindObjectsByType<ProjectC.Player.NetworkPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var seenClientIds = new HashSet<ulong>();

            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p == null || !p.IsSpawned) continue;
                if (!p.IsOwner) continue;
                // Skip scene-placed ghost
                if (p.GetComponent<ProjectC.Network.NetworkPlayerSpawner>() != null) continue;

                ulong cid = p.OwnerClientId;
                seenClientIds.Add(cid);

                Vector3 playerPos = p.GetEffectivePosition();
                float dSqr = (transform.position - playerPos).sqrMagnitude;
                float r = tradeRadius;
                if (dSqr <= r * r)
                {
                    _playersInZone.Add(cid);
                    _missingTicks.Remove(cid);
                    // Клиент: ставим LocalPlayerZone (одна зона — последний ближайший)
                    ContractZoneRegistry.LocalPlayerZone = this;
                }
                else
                {
                    // Debounce: только после N пропусков удаляем
                    _missingTicks.TryGetValue(cid, out int miss);
                    miss++;
                    _missingTicks[cid] = miss;
                    if (miss >= MISS_THRESHOLD)
                    {
                        _playersInZone.Remove(cid);
                        _missingTicks.Remove(cid);
                        if (ContractZoneRegistry.LocalPlayerZone == this)
                            ContractZoneRegistry.LocalPlayerZone = null;
                    }
                }
            }
        }

        // ========================================================
        // SERVER-ONLY API
        // ========================================================

        /// <summary>На сервере: проверить, что clientId находится в зоне.</summary>
        public bool IsPlayerInZone(ulong clientId)
        {
            return _playersInZone.Contains(clientId);
        }

        // ========================================================
        // GIZMOS
        // ========================================================

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
            var collider = GetComponent<SphereCollider>();
            float radius = collider != null ? collider.radius : tradeRadius;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
