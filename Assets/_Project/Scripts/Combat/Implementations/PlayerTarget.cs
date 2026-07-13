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
            // T-HP01: try immediate HP init, then retry loop if not ready
            if (IsServer && !_hpInitialized)
            {
                TryInitializeHp();
                if (!_hpInitialized && _hpInitCoroutine == null)
                    _hpInitCoroutine = StartCoroutine(InitHpRetryLoop());
            }
        }

        /// <summary>
        /// T-RTC06: Self-register в CombatServer при NetworkSpawn (server-side only).
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;

            // T-HP01 fix: авто-резолв _clientId если Initialize не был вызван (предсуществующий баг)
            if (_clientId == 0 && NetworkObject != null && NetworkObject.OwnerClientId != 0)
                _clientId = NetworkObject.OwnerClientId;

            if (_clientId != 0 && CombatServer.Instance != null)
            {
                CombatServer.Instance.RegisterTarget(_clientId, this);
            }
            // T-HP01: если Initialize ещё не вызывалась — запустим retry loop из OnNetworkSpawn
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

        /// <summary>
        /// T-HP01: Coroutine retry loop — пытается инициализировать HP каждые 0.1с
        /// до 20 попыток (2 секунды). Защищает от любых race conditions спавна.
        /// </summary>
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

        /// <summary>
        /// T-HP01: Инициализирует HP из StatsServer (STR-based formula).
        /// Идемпотентен.
        /// </summary>
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

        /// <summary>
        /// T-HP01: Игрок считается живым пока HP не проинициализирован (защита от race condition).
        /// После инициализации — стандартная проверка currentHp > 0.
        /// </summary>
        public bool IsAlive() => !_hpInitialized || _currentHp.Value > 0;
        public bool IsPlayer() => true;
        public string GetDisplayName() => $"Player {ResolvedClientId}";

        /// <summary>
        /// T-HP01 fix: авто-резолв clientId если Initialize не был вызван.
        /// Использует NetworkObject.OwnerClientId как fallback.
        /// </summary>
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
            // T-HP01 fix: авто-резолв _clientId (предсуществующий баг — Initialize может не вызваться)
            if (_clientId == 0 && NetworkObject != null && NetworkObject.OwnerClientId != 0)
                _clientId = NetworkObject.OwnerClientId;

            // T-HP01 fix: ApplyDamage всегда вызывается из server-side кода (CombatServer/NpcBrain).
            // Проверка IsServer убрана — для scene-placed/поздно-спавненых экземпляров NetworkObject.IsServer = false.
            if (!result.isHit) return;

            // T-HP01: если HP ещё не инициализирован — вычисляем сейчас (защита от 0 HP)
            if (!_hpInitialized)
                TryInitializeHp();

            if (_currentHp.Value <= 0 && _hpInitialized) return;  // already dead (only if HP was initialized)

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
