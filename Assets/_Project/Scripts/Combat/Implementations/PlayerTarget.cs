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
        private Coroutine _hpInitCoroutine;

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
                if (_hpInitialized)
                {
                    _hpInitCoroutine = null;
                    yield break;
                }
            }
            _hpInitCoroutine = null;
            Debug.LogWarning($"[PlayerTarget] HP init FAILED after 20 retries for client={_clientId}");
        }

        private void TryInitializeHp()
        {
            if (_hpInitialized) return;
            if (_clientId == 0) return;

            var statsServer = ProjectC.Stats.StatsServer.Instance;
            if (statsServer == null) return;

            int maxHp = statsServer.ComputeMaxHp(_clientId);
            if (maxHp <= 0) return;

            _maxHp.Value = maxHp;
            _currentHp.Value = maxHp;
            _hpInitialized = true;

            if (_debugLog)
                Debug.Log($"[PlayerTarget] HP initialized: {maxHp}/{maxHp} for client={_clientId}");
        }

        public void SetHp(int hp)
        {
            if (!IsServer) return;
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
                TryInitializeHp();

            if (_currentHp.Value <= 0 && _hpInitialized) return;

            int newHp = Mathf.Max(0, _currentHp.Value - result.finalDamage);
            _currentHp.Value = newHp;

            // T-HP01: push updated HP to StatsServer → snapshot → CharacterWindow UI
            var ss = ProjectC.Stats.StatsServer.Instance;
            if (ss != null) ss.RecomputeAndSendSnapshot(_clientId);

            // T-HP01: disable input/movement on death
            if (newHp <= 0)
            {
                var np = GetComponent<ProjectC.Player.NetworkPlayer>();
                if (np != null) np.SetInputEnabled(false);
            }

            if (_debugLog || Debug.isDebugBuild)
            {
                Debug.Log($"[PlayerTarget] client={_clientId} took {result.finalDamage} from attacker={attackerClientId} (HP {_currentHp.Value + result.finalDamage} → {newHp}, isCrit={result.isCrit}, type={result.damageType})");
            }

            var anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                if (newHp > 0)
                {
                    foreach (var p in anim.parameters)
                    {
                        if (p.type == AnimatorControllerParameterType.Trigger && p.name == "Damage")
                        {
                            anim.SetTrigger("Damage");
                            break;
                        }
                    }
                }
                else
                {
                    foreach (var p in anim.parameters)
                    {
                        if (p.type == AnimatorControllerParameterType.Trigger && p.name == "Death")
                        {
                            anim.SetTrigger("Death");
                            break;
                        }
                    }

                    if (_deathRespawnDelay > 0f)
                        Invoke(nameof(TriggerDeathRespawn), _deathRespawnDelay);
                    else
                        TriggerDeathRespawn();
                }
            }
            else if (newHp <= 0)
            {
                TriggerDeathRespawn();
            }
        }

        private void TriggerDeathRespawn()
        {
            if (!IsServer) return;

            var tracker = GetComponent<ProjectC.Player.PlayerRespawnTracker>();
            if (tracker != null)
            {
                float hpPercent = 0.3f;
                var statsServer = ProjectC.Stats.StatsServer.Instance;
                if (statsServer != null && statsServer.HealthConfig != null)
                    hpPercent = statsServer.HealthConfig.RespawnHpPercent;

                tracker.RespawnWithHpRestore(hpPercent);
            }
            else
            {
                Debug.LogWarning($"[PlayerTarget] PlayerRespawnTracker not found on same GameObject — cannot respawn client={_clientId}");
            }
        }
    }
}
