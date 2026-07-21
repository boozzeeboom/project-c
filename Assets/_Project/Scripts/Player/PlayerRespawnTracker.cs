using Unity.Netcode;
using UnityEngine;
using ProjectC.World;

namespace ProjectC.Player
{
    /// <summary>
    /// Отслеживает состояние игрока для респавна.
    /// Должен быть на том же GameObject что и NetworkPlayer/CharacterController.
    ///
    /// Server-authoritative: проверка Y < 0 и телепорт на сервере.
    /// Клиент получает позицию через ClientRpc.
    ///
    /// OnTriggerEnter: смена активной точки респавна при входе в триггер-зону.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerRespawnTracker : NetworkBehaviour
    {
        [Header("Fall Threshold")]
        [Tooltip("Y-координата, ниже которой считается что игрок упал и нужен респавн.")]
        [SerializeField] private float _deathY = 0f;

        [Tooltip("Задержка в секундах перед респавном после обнаружения падения.")]
        [SerializeField] private float _respawnDelay = 0.5f;

        [Header("Ship Respawn Proximity")]
        [Tooltip("Максимальная дистанция до owned корабля для респавна на нём.")]
        [SerializeField] private float _ownedShipRespawnRadius = 200f; // T-PLAYER-PERSIST D5

        [Header("Debug")]
        [Tooltip("Логировать события респавна.")]
        [SerializeField] private bool _debugLog = false;

        // Состояние
        private CharacterController _controller;
        private RespawnManager _respawnManager;
        private int _currentRespawnIndex = -1;
        private float _fallStartTime = float.MaxValue;
        private bool _isRespawning;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void Start()
        {
            // Находим RespawnManager (должен быть в BootstrapScene)
            _respawnManager = FindAnyObjectByType<RespawnManager>();
            if (_respawnManager == null)
            {
                Debug.LogWarning("[PlayerRespawnTracker] RespawnManager not found in scene. Respawn will not work.");
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _currentRespawnIndex = -1;
            _fallStartTime = float.MaxValue;
            _isRespawning = false;
        }

        private void Update()
        {
            // R1 (ARCHITECTURE_AUDIT): унифицированный IsServer guard — NM.Singleton.IsServer
            // вместо NB.IsServer (тот же баг NGO 2.x, что был в PlayerTarget).
            bool isServer = Unity.Netcode.NetworkManager.Singleton != null
                         && Unity.Netcode.NetworkManager.Singleton.IsServer;
            if (!isServer) return;
            if (_isRespawning) return;
            if (_respawnManager == null) return;

            float y = transform.position.y;

            if (y <= _deathY)
            {
                if (_fallStartTime > Time.time)
                {
                    _fallStartTime = Time.time;
                }

                if (Time.time - _fallStartTime >= _respawnDelay)
                {
                    PerformRespawn();
                }
            }
            else
            {
                // Сброс таймера если игрок выше порога
                _fallStartTime = float.MaxValue;
            }
        }

        /// <summary>
        /// Выполнить телепорт на точку респавна.
        /// Возвращает true если телепорт был инициирован, false если пропущен
        /// (RespawnManager не найден, или уже в процессе респавна).
        /// </summary>
        private bool PerformRespawn()
        {
            if (_isRespawning)
            {
                if (_debugLog) Debug.LogWarning($"[PlayerRespawnTracker] PerformRespawn skipped: already respawning (client={OwnerClientId})");
                return false;
            }
            _isRespawning = true;
            _fallStartTime = float.MaxValue;

            // T-PLAYER-PERSIST: если игрок был на корабле — респавним на нём
            var np = GetComponent<NetworkPlayer>();
            if (np != null && np.IsInShip && np.CurrentShip != null && np.CurrentShip.IsSpawned)
            {
                Vector3 shipPos = np.CurrentShip.GetExitPosition();
                TeleportToClientRpc(shipPos);
                if (_debugLog)
                    Debug.Log($"[PlayerRespawnTracker] Ship-proximity respawn (IsInShip) for client={OwnerClientId} at ship exit {shipPos}");

                if (IsServer && !IsClient)
                    Invoke(nameof(ResetRespawningFlag), 0.5f);
                return true;
            }

            // T-PLAYER-PERSIST: fallback — LastShip (игрок вышел из корабля и упал)
            if (np != null && np.LastShip != null && np.LastShip.IsSpawned)
            {
                Vector3 shipPos = np.LastShip.GetExitPosition();
                TeleportToClientRpc(shipPos);
                if (_debugLog)
                    Debug.Log($"[PlayerRespawnTracker] Ship-proximity respawn (LastShip) for client={OwnerClientId} at ship exit {shipPos}");

                if (IsServer && !IsClient)
                    Invoke(nameof(ResetRespawningFlag), 0.5f);
                return true;
            }

            // T-PLAYER-PERSIST: поиск ближайшего owned корабля (D5)
            if (TryFindNearestOwnedShip(np, out Vector3 nearestShipPos))
            {
                TeleportToClientRpc(nearestShipPos);
                if (_debugLog)
                    Debug.Log($"[PlayerRespawnTracker] Nearest-owned-ship respawn for client={OwnerClientId} at {nearestShipPos}");

                if (IsServer && !IsClient)
                    Invoke(nameof(ResetRespawningFlag), 0.5f);
                return true;
            }

            // T-HP01: защита от null если RespawnManager не найден в сцене
            if (_respawnManager == null)
            {
                _respawnManager = FindAnyObjectByType<RespawnManager>();
                if (_respawnManager == null)
                {
                    Debug.LogError($"[PlayerRespawnTracker] CRITICAL: RespawnManager not found in any loaded scene! Teleport skipped. client={OwnerClientId}");
                    _isRespawning = false;
                    return false;
                }
            }

            // Если точка не назначена — используем fallback (индекс 0)
            int index = _currentRespawnIndex >= 0 ? _currentRespawnIndex : 0;
            Vector3 targetPos = _respawnManager.GetEffectivePosition(index);

            if (_debugLog) Debug.Log($"[PlayerRespawnTracker] Respawning player {OwnerClientId} to index={index} pos={targetPos} (respawnPoints.Count={_respawnManager.Count})");

            TeleportToClientRpc(targetPos);

            // T-HP01: сброс флага на сервере (ClientRpc не доходит до dedicated server)
            if (IsServer && !IsClient)
                Invoke(nameof(ResetRespawningFlag), 0.5f);

            return true;
        }

        /// <summary>
        /// T-PLAYER-PERSIST (D5): поиск ближайшего owned корабля для респавна.
        /// </summary>
        private bool TryFindNearestOwnedShip(NetworkPlayer np, out Vector3 nearestPos)
        {
            nearestPos = Vector3.zero;

            if (np == null || Unity.Netcode.NetworkManager.Singleton == null) return false;

            ulong clientId = np.OwnerClientId;
            var allShips = FindObjectsByType<ShipController>(FindObjectsSortMode.None);
            ShipController nearestShip = null;
            float nearestDistSq = float.MaxValue;
            float proximityThresholdSq = _ownedShipRespawnRadius * _ownedShipRespawnRadius;

            foreach (var ship in allShips)
            {
                if (!ship.IsSpawned) continue;

                // Проверка владения через MetaRequirementRegistry
                bool isOwned = ProjectC.MetaRequirement.MetaRequirementRegistry.Instance != null
                    && ProjectC.MetaRequirement.MetaRequirementRegistry.Instance.CanPlayerUse(clientId, ship.NetworkObjectId);

                if (!isOwned) continue;

                float distSq = (ship.transform.position - transform.position).sqrMagnitude;
                if (distSq < nearestDistSq && distSq <= proximityThresholdSq)
                {
                    nearestDistSq = distSq;
                    nearestShip = ship;
                }
            }

            if (nearestShip != null)
            {
                nearestPos = nearestShip.GetExitPosition();
                return true;
            }

            return false;
        }

        private void ResetRespawningFlag()
        {
            _isRespawning = false;
        }

        [ClientRpc]
        private void TeleportToClientRpc(Vector3 targetPosition)
        {
            // Отключаем CharacterController чтобы избежать конфликта с ручной установкой позиции
            if (_controller != null)
            {
                _controller.enabled = false;
            }

            transform.position = targetPosition;

            if (_controller != null)
            {
                _controller.enabled = true;
            }

            // Сбрасываем скорость (чтобы не продолжить падение)
            var networkPlayer = GetComponent<NetworkPlayer>();
            if (networkPlayer != null)
            {
                networkPlayer.ResetVelocity();
            }

            Physics.SyncTransforms();

            _isRespawning = false;

            if (_debugLog && IsOwner)
            {
                Debug.Log($"[PlayerRespawnTracker] Client teleported to {targetPosition}");
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            _isRespawning = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_respawnManager == null) return;

            int index = _respawnManager.FindRespawnIndex(other);
            if (index >= 0)
            {
                _currentRespawnIndex = index;
                if (_debugLog && (IsOwner || IsServer))
                {
                    Debug.Log($"[PlayerRespawnTracker] Respawn point set to index={index} via trigger '{other.name}'");
                }
            }
        }

        /// <summary>
        /// T-HP01: Сброс таймера падения. Вызывается после респавна чтобы не тригерился мгновенно.
        /// </summary>
        public void ResetFallTimer()
        {
            _fallStartTime = float.MaxValue;
        }

        /// <summary>
        /// Публичный метод для принудительной установки индекса респавна из кода.
        /// </summary>
        public void SetRespawnIndex(RespawnManager manager, int index)
        {
            if (manager != null) _respawnManager = manager;
            _currentRespawnIndex = index;
        }

        /// <summary>
        /// Публичный ServerRpc для ручного запроса респавна.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestRespawnServerRpc()
        {
            PerformRespawn();
        }

        /// <summary>
        /// T-HP01: Респавн с восстановлением HP до процента от максимума.
        /// Вызывается из PlayerTarget.ApplyDamage при смерти (серверный код).
        /// Выполняет стандартный телепорт + восстанавливает HP на PlayerTarget.
        /// </summary>
        public void RespawnWithHpRestore(float hpPercent)
        {
            bool isServer = Unity.Netcode.NetworkManager.Singleton != null
                         && Unity.Netcode.NetworkManager.Singleton.IsServer;

            if (!isServer)
            {
                Debug.LogError($"[PlayerRespawnTracker] RespawnWithHpRestore: not server. Aborting. client={OwnerClientId}");
                return;
            }

            if (_debugLog) Debug.Log($"[PlayerRespawnTracker] RespawnWithHpRestore START: hpPercent={hpPercent:F2}, client={OwnerClientId}");

            bool teleported = PerformRespawn();
            if (!teleported)
            {
                // Телепорт не удался — НЕ восстанавливаем HP и НЕ включаем ввод.
                // Иначе персонаж оживёт на месте смерти без перемещения.
                Debug.LogError($"[PlayerRespawnTracker] RespawnWithHpRestore ABORTED: PerformRespawn failed. HP and input NOT restored. client={OwnerClientId}");
                return;
            }

            // R2 (ARCHITECTURE_AUDIT): сброс таймера падения после успешного телепорта.
            // Если точка респавна ниже _deathY, без сброса игрок мгновенно упадёт снова.
            ResetFallTimer();

            // T-HP01-fix: сбросить Death-анимацию, иначе персонаж продолжит лежать трупом.
            var anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                anim.ResetTrigger("Death");
                if (anim.HasState(0, Animator.StringToHash("Idle")))
                    anim.Play("Idle", 0, 0f);
                if (_debugLog) Debug.Log($"[PlayerRespawnTracker] Animator: Death reset, Idle played. client={OwnerClientId}");
            }

            var playerTarget = GetComponent<ProjectC.Combat.PlayerTarget>();
            if (playerTarget != null)
            {
                int restoreHp = Mathf.Max(1, Mathf.RoundToInt(playerTarget.GetMaxHp() * hpPercent));
                playerTarget.SetHp(restoreHp);
                if (_debugLog) Debug.Log($"[PlayerRespawnTracker] HP restored to {restoreHp}/{playerTarget.GetMaxHp()} ({hpPercent:P0}) for client={OwnerClientId}");
            }

            // T-HP01: перевключить управление после респавна
            var np = GetComponent<NetworkPlayer>();
            if (np != null)
            {
                np.SetInputEnabled(true);
                if (_debugLog) Debug.Log($"[PlayerRespawnTracker] Input ENABLED. client={OwnerClientId}");
            }

            if (_debugLog) Debug.Log($"[PlayerRespawnTracker] RespawnWithHpRestore COMPLETE. client={OwnerClientId}");
        }
    }
}
