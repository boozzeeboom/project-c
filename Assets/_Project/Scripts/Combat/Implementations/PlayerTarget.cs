// Project C: Real-Time Combat Engine — T-RTC02 + T-HP01
// PlayerTarget: реализация IDamageTarget для NetworkPlayer (server-side authoritative).
// Design: docs/Character/Skills/real-time-combat/10_DESIGN.md §3.2.
//
// HP реплицируется через NetworkVariable<int>. ApplyDamage — server-only.
// T-HP01: HP = base + STR_flat * multiplier (HealthConfig). Death → respawn + 30% HP.
// armorDefense = 0 в MVP (T-CB06 ещё не сделан, поле armorDefense в ClothingItemData отсутствует).
//
// TODO (post T-CB06): заменить GetArmorDefense() на реальный подсчёт суммы armorDefense
// из экипированной ClothingItemData (Head+Chest+Legs+Feet+Back).

using System.Collections;
using UnityEngine;
using Unity.Netcode;
using ProjectC.Combat.Core;
using ProjectC.Equipment;
using ProjectC.Items;

namespace ProjectC.Combat
{
    public class PlayerTarget : NetworkBehaviour, IDamageTarget
    {
        [Header("HP (server-authoritative, replicated via NetworkVariable)")]
        [SerializeField] private NetworkVariable<int> _currentHp = new NetworkVariable<int>(0);
        [SerializeField] private NetworkVariable<int> _maxHp = new NetworkVariable<int>(0);

        [Header("Death Respawn")]
        [Tooltip("Задержка в секундах перед респавном после смерти.")]
        [SerializeField] private float _deathRespawnDelay = 1.5f;

        [Header("Debug")]
        [SerializeField] private bool _debugLog = false;

        private ulong _clientId;
        private bool _hpInitialized;
        private bool _hpFallbackUsed;        // R3: true если HP был инициализирован fallback=100
        private Coroutine _hpInitCoroutine;
        private float _deathRespawnTimer = -1f;

        public ulong GetTargetId() => ResolvedClientId;
        public ulong ClientId => _clientId;

        public void Initialize(ulong clientId)
        {
            _clientId = clientId;
            if (IsServer && !_hpInitialized)
            {
                TryInitializeHp();
                if (!_hpInitialized && _hpInitCoroutine == null)
                    _hpInitCoroutine = StartCoroutine(InitHpRetryLoop());
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;

            if (_clientId == 0 && NetworkObject != null && NetworkObject.OwnerClientId != 0)
                _clientId = NetworkObject.OwnerClientId;

            if (_clientId != 0 && CombatServer.Instance != null)
            {
                CombatServer.Instance.RegisterTarget(_clientId, this);
            }
            if (!_hpInitialized && _clientId != 0 && _hpInitCoroutine == null)
            {
                TryInitializeHp();
                if (!_hpInitialized)
                    _hpInitCoroutine = StartCoroutine(InitHpRetryLoop());
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (!IsServer) return;
            if (_hpInitCoroutine != null)
            {
                StopCoroutine(_hpInitCoroutine);
                _hpInitCoroutine = null;
            }
            _deathRespawnTimer = -1f;
            if (CombatServer.Instance != null && _clientId != 0)
            {
                CombatServer.Instance.UnregisterTarget(_clientId);
            }
        }

        private IEnumerator InitHpRetryLoop()
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                yield return new WaitForSeconds(0.1f);
                TryInitializeHp();
                // R3: если был fallback — продолжаем пытаться пока StatsServer не загрузится
                if (_hpInitialized && !_hpFallbackUsed)
                {
                    _hpInitCoroutine = null;
                    yield break;
                }
            }
            _hpInitCoroutine = null;
            if (_hpFallbackUsed)
            {
                if (_debugLog) Debug.LogWarning($"[PlayerTarget] HP still on fallback=100 after 20 retries for client={_clientId} (StatsServer never loaded?)");
            }
            else
            {
                Debug.LogWarning($"[PlayerTarget] HP init FAILED after 20 retries for client={_clientId}");
            }
        }

        private void TryInitializeHp()
        {
            // R3: разрешаем пересчёт если был fallback (StatsServer мог загрузиться позже)
            if (_hpInitialized && !_hpFallbackUsed) return;
            if (_clientId == 0) return;

            var statsServer = ProjectC.Stats.StatsServer.Instance;
            if (statsServer == null) return;

            int maxHp = statsServer.ComputeMaxHp(_clientId);
            if (maxHp <= 0) return;

            // R3: если был fallback и игрок получил урон — не перезаписываем currentHp на max
            if (_hpFallbackUsed)
                _currentHp.Value = Mathf.Min(_currentHp.Value, maxHp);

            _maxHp.Value = maxHp;
            if (!_hpFallbackUsed)
                _currentHp.Value = maxHp;

            _hpInitialized = true;
            _hpFallbackUsed = false;

            if (_debugLog)
                Debug.Log($"[PlayerTarget] HP initialized: {_currentHp.Value}/{maxHp} for client={_clientId}");
        }

        public void SetHp(int hp)
        {
            // T-HP01-fix: NetworkManager.IsServer вместо NB.IsServer (тот же баг NGO 2.x)
            bool isServer = Unity.Netcode.NetworkManager.Singleton != null
                         && Unity.Netcode.NetworkManager.Singleton.IsServer;
            if (!isServer) return;
            _currentHp.Value = Mathf.Clamp(hp, 0, _maxHp.Value);
        }

        public Vector3 GetPosition() => transform.position;
        public int GetCurrentHp() => _currentHp.Value;
        public int GetMaxHp() => _maxHp.Value;

        public int GetArmorDefense()
        {
            if (EquipmentWorld.Instance == null || InventoryWorld.Instance == null) return 0;
            var equip = EquipmentWorld.Instance.GetEquipment(_clientId);
            int total = 0;
            var armorSlots = new[] {
                ProjectC.Equipment.EquipSlot.Head,
                ProjectC.Equipment.EquipSlot.Chest,
                ProjectC.Equipment.EquipSlot.Legs,
                ProjectC.Equipment.EquipSlot.Feet,
                ProjectC.Equipment.EquipSlot.Back,
            };
            foreach (var slot in armorSlots)
            {
                if (equip.TryGetItemId(slot, out int itemId) && itemId > 0)
                {
                    var data = InventoryWorld.Instance.GetItemDefinition(itemId);
                    if (data is ProjectC.Equipment.ClothingItemData c)
                    {
                        total += c.armorDefense;
                    }
                }
            }
            return total;
        }

        public bool IsAlive() => !_hpInitialized || _currentHp.Value > 0;
        public bool IsPlayer() => true;
        public string GetDisplayName() => $"Player {ResolvedClientId}";

        /// <summary>
        /// T-HP01-fix: Timer-based death respawn (вместо Coroutine — таймер в Update надёжнее,
        /// т.к. корутина на NetworkBehaviour теряет контекст IsServer).
        /// </summary>
        private void Update()
        {
            if (_deathRespawnTimer < 0f) return;
            if (Time.time < _deathRespawnTimer) return;

            _deathRespawnTimer = -1f;
            if (_debugLog) Debug.Log($"[PlayerTarget] Death timer elapsed at t={Time.time:F1}, calling TriggerDeathRespawn. client={_clientId}");
            TriggerDeathRespawn();
        }

        private ulong ResolvedClientId
        {
            get
            {
                if (_clientId != 0) return _clientId;
                if (NetworkObject != null && NetworkObject.OwnerClientId != 0)
                    return NetworkObject.OwnerClientId;
                return 0;
            }
        }

        public void ApplyDamage(DamageResult result, ulong attackerClientId)
        {
            if (_clientId == 0 && NetworkObject != null && NetworkObject.OwnerClientId != 0)
                _clientId = NetworkObject.OwnerClientId;

            if (!result.isHit) return;

            if (!_hpInitialized)
            {
                TryInitializeHp();
                if (!_hpInitialized)
                {
                    _maxHp.Value = 100;
                    _currentHp.Value = 100;
                    _hpInitialized = true;
                    _hpFallbackUsed = true;  // R3: флаг — HP будет пересчитан когда StatsServer загрузится
                    if (_hpInitCoroutine != null) StopCoroutine(_hpInitCoroutine);
                    _hpInitCoroutine = StartCoroutine(InitHpRetryLoop());  // R3: продолжаем пытаться
                    if (_debugLog) Debug.LogWarning($"[PlayerTarget] HP fallback 100 used for client={_clientId} (StatsServer not ready, retrying...)");
                }
            }

            if (_currentHp.Value <= 0 && _hpInitialized) return;

            int newHp = Mathf.Max(0, _currentHp.Value - result.finalDamage);
            _currentHp.Value = newHp;

            var ss = ProjectC.Stats.StatsServer.Instance;
            if (ss != null)
            {
                ss.RecomputeAndSendSnapshot(_clientId);
                if (_debugLog) Debug.Log($"[PlayerTarget] Snapshot sent after damage: HP={newHp}/{_maxHp.Value}, client={_clientId}");
            }
            else
            {
                Debug.LogWarning($"[PlayerTarget] StatsServer.Instance is null — cannot send HP snapshot");
            }

            if (_debugLog)
                Debug.Log($"[PlayerTarget] client={_clientId} took {result.finalDamage} from attacker={attackerClientId} (HP {_currentHp.Value + result.finalDamage} → {newHp}, isCrit={result.isCrit}, type={result.damageType})");

            if (newHp <= 0)
            {
                // T-HP01: disable input/movement on death
                var np = GetComponent<ProjectC.Player.NetworkPlayer>();
                if (np != null) np.SetInputEnabled(false);

                // Trigger death animation + schedule respawn
                var anim = GetComponentInChildren<Animator>();
                if (anim != null && anim.runtimeAnimatorController != null)
                {
                    foreach (var p in anim.parameters)
                    {
                        if (p.type == AnimatorControllerParameterType.Trigger && p.name == "Death")
                        {
                            anim.SetTrigger("Death");
                            break;
                        }
                    }
                }

                if (_debugLog) Debug.Log($"[PlayerTarget] DEATH: HP=0, scheduling respawn in {_deathRespawnDelay}s via timer. client={_clientId}, IsServer={IsServer}");
                _deathRespawnTimer = Time.time + _deathRespawnDelay;
            }
        }

        /// <summary>
        /// T-HP01-fix: Death → teleport + HP restore.
        /// Делегирует в PlayerRespawnTracker.RespawnWithHpRestore, который выполняет
        /// телепорт на точку респавна, восстанавливает HP, сбрасывает аниматор и включает управление.
        /// </summary>
        private void TriggerDeathRespawn()
        {
            // T-HP01-fix: NetworkManager.Singleton.IsServer вместо NetworkBehaviour.IsServer —
            // NB.IsServer может быть false в корутине/timer'е из-за бага NGO 2.x.
            bool isServer = Unity.Netcode.NetworkManager.Singleton != null
                         && Unity.Netcode.NetworkManager.Singleton.IsServer;

            if (_debugLog) Debug.Log($"[PlayerTarget] TriggerDeathRespawn CALLED. NM.IsServer={isServer}, NB.IsServer={IsServer}, IsSpawned={IsSpawned}, client={_clientId}");

            if (!isServer)
            {
                Debug.LogError($"[PlayerTarget] TriggerDeathRespawn: not server (NetworkManager). Aborting.");
                return;
            }

            float hpPercent = 0.3f;
            var statsServer = ProjectC.Stats.StatsServer.Instance;
            if (statsServer != null && statsServer.HealthConfig != null)
                hpPercent = statsServer.HealthConfig.RespawnHpPercent;

            if (_debugLog) Debug.Log($"[PlayerTarget] TriggerDeathRespawn: hpPercent={hpPercent:F2}, looking for PlayerRespawnTracker...");

            var tracker = GetComponent<ProjectC.Player.PlayerRespawnTracker>();
            if (tracker != null)
            {
                if (_debugLog) Debug.Log($"[PlayerTarget] TriggerDeathRespawn: tracker found, calling RespawnWithHpRestore. client={_clientId}");
                tracker.RespawnWithHpRestore(hpPercent);
            }
            else
            {
                Debug.LogError($"[PlayerTarget] TriggerDeathRespawn: PlayerRespawnTracker NOT FOUND! Using fallback. client={_clientId}");
                int restoreHp = Mathf.Max(1, Mathf.RoundToInt(_maxHp.Value * hpPercent));
                _currentHp.Value = restoreHp;
                var np = GetComponent<ProjectC.Player.NetworkPlayer>();
                if (np != null) np.SetInputEnabled(true);
                if (statsServer != null) statsServer.RecomputeAndSendSnapshot(_clientId);
            }

            if (_debugLog) Debug.Log($"[PlayerTarget] TriggerDeathRespawn DONE. client={_clientId}");
        }
    }
}
