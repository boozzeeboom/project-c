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

        public ulong GetTargetId() => _clientId;
        public ulong ClientId => _clientId;

        public void Initialize(ulong clientId)
        {
            _clientId = clientId;
            // T-HP01: try immediate HP init (StatsServer may be ready)
            TryInitializeHp();
        }

        /// <summary>
        /// T-RTC06: Self-register в CombatServer при NetworkSpawn (server-side only).
        /// Решает race condition: NetworkPlayer.OnNetworkSpawn может сработать РАНЬШЕ
        /// CombatServer.OnNetworkSpawn. Push-down в CombatServer.OnNetworkSpawn страхует.
        /// T-HP01: также инициализирует HP из StatsServer (с retry если ещё не готов).
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;
            if (_clientId != 0 && CombatServer.Instance != null)
            {
                CombatServer.Instance.RegisterTarget(_clientId, this);
            }
            // T-HP01: schedule HP init retry (StatsServer may spawn after PlayerTarget)
            if (!_hpInitialized)
            {
                TryInitializeHp();
                if (!_hpInitialized)
                    Invoke(nameof(TryInitializeHp), 0.5f);
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (!IsServer) return;
            if (CombatServer.Instance != null && _clientId != 0)
            {
                CombatServer.Instance.UnregisterTarget(_clientId);
            }
        }

        /// <summary>
        /// T-HP01: Инициализирует HP из StatsServer (STR-based formula).
        /// Идемпотентен — если HP уже инициализирован, ничего не делает.
        /// Если StatsServer ещё не готов — возвращает false (вызывающий должен retry).
        /// </summary>
        private void TryInitializeHp()
        {
            if (_hpInitialized) return;
            if (_clientId == 0) return;

            var statsServer = ProjectC.Stats.StatsServer.Instance;
            if (statsServer == null) return;

            int maxHp = statsServer.ComputeMaxHp(_clientId);
            if (maxHp <= 0) return; // STR tier not loaded yet

            _maxHp.Value = maxHp;
            _currentHp.Value = maxHp;
            _hpInitialized = true;

            if (_debugLog)
                Debug.Log($"[PlayerTarget] HP initialized: {maxHp}/{maxHp} for client={_clientId}");
        }

        /// <summary>
        /// T-HP01: Публичный setter для HP (используется PlayerRespawnTracker при респавне).
        /// Server-only.
        /// </summary>
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
            // T-CB06: реальный подсчёт armorDefense из экипированной одежды.
            // До T-CB06 возвращал 0.
            if (EquipmentWorld.Instance == null || InventoryWorld.Instance == null) return 0;
            var equip = EquipmentWorld.Instance.GetEquipment(_clientId);
            int total = 0;
            // Armor slots: Head, Chest, Legs, Feet, Back. Module slots (Module1-3) — нет.
            // Hands/Accessory1-2 — оставим для будущего (может быть щит/кольцо).
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

        public bool IsAlive() => _currentHp.Value > 0;
        public bool IsPlayer() => true;
        public string GetDisplayName() => $"Player {_clientId}";

        public void ApplyDamage(DamageResult result, ulong attackerClientId)
        {
            if (!IsServer)
            {
                Debug.LogWarning($"[PlayerTarget] ApplyDamage called on non-server. client={_clientId}, attacker={attackerClientId}");
                return;
            }
            if (!result.isHit) return;
            if (_currentHp.Value <= 0) return;  // already dead

            int newHp = Mathf.Max(0, _currentHp.Value - result.finalDamage);
            _currentHp.Value = newHp;

            if (_debugLog || Debug.isDebugBuild)
            {
                Debug.Log($"[PlayerTarget] client={_clientId} took {result.finalDamage} from attacker={attackerClientId} (HP {_currentHp.Value + result.finalDamage} → {newHp}, isCrit={result.isCrit}, type={result.damageType})");
            }

            // T-NPC-12: Damage trigger на Animator (если жив). Death trigger если убит.
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

                    // T-HP01: Death → delayed respawn with HP restore.
                    if (_deathRespawnDelay > 0f)
                        Invoke(nameof(TriggerDeathRespawn), _deathRespawnDelay);
                    else
                        TriggerDeathRespawn();
                }
            }
            else if (newHp <= 0)
            {
                // No animator — respawn immediately.
                TriggerDeathRespawn();
            }
        }

        /// <summary>
        /// T-HP01: Вызывает респавн с восстановлением HP после смерти.
        /// Читает RespawnHpPercent из HealthConfig (StatsServer).
        /// </summary>
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
