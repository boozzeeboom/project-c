// Project C: Real-Time Combat Engine — T-RTC03
// NpcTarget: реализация IDamageTarget для NPC-врага. NetworkBehaviour с NetworkVariable<int> HP.
// Design: docs/Character/Skills/real-time-combat/10_DESIGN.md §3.4.

using System;
using UnityEngine;
using Unity.Netcode;
using ProjectC.Combat.Core;

namespace ProjectC.Combat
{
    public class NpcTarget : NetworkBehaviour, IDamageTarget
    {
        [SerializeField] private NpcCombatData _data;
        [SerializeField] private int _maxHpOverride = 0;  // если >0 — переопределяет _data.maxHp

        [Header("Debug")]
        [Tooltip("Включить подробные логи в консоль.")]
        [SerializeField] private bool _debugLog = false;

        private NetworkVariable<int> _currentHp = new NetworkVariable<int>(30);
        private NetworkVariable<int> _maxHp = new NetworkVariable<int>(30);
        private ulong _targetId;  // = NetworkObjectId по дизайну, но у нас override

        // T-NPC-12: loot config from spawner.
        private GameObject _lootPrefab;
        private Items.LootTable _lootTable;

        // T-NPC-14: событие изменения HP (server-side). Подписывается NpcBrain для
        // отслеживания cumulative damage / passive aggro. Параметры: (newHp, deltaHp).
        public event Action<int, int> OnHpChanged;

        public void Initialize(NpcCombatData data, ulong targetId)
        {
            _data = data;
            _targetId = targetId;
            int hp = _maxHpOverride > 0 ? _maxHpOverride : (data != null ? data.maxHp : 30);
            _maxHp.Value = hp;
            _currentHp.Value = hp;
        }

        /// <summary>
        /// T-RTC06 (v0.1 fix): Self-register в CombatServer при NetworkSpawn (server-side only).
        /// Также: если <c>Initialize</c> не был вызван (например, для scene-placed NPC, созданных
        /// вручную через Edit Mode без explicit Initialize call), инициализируем HP из <c>_data</c>.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;

            // Fallback init: если _targetId == 0 → Initialize не вызывался → init HP из _data
            if (_targetId == 0 && _data != null)
            {
                int hp = _maxHpOverride > 0 ? _maxHpOverride : _data.maxHp;
                _maxHp.Value = hp;
                _currentHp.Value = hp;
                _targetId = NetworkObjectId;  // используем реальный NetworkObjectId
                if (_debugLog) Debug.Log($"[NpcTarget] OnNetworkSpawn fallback-init: name={gameObject.name}, targetId={_targetId}, HP={hp}");
            }

            if (CombatServer.Instance != null)
            {
                CombatServer.Instance.RegisterTarget(GetTargetId(), this);
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (!IsServer) return;
            if (CombatServer.Instance != null)
            {
                CombatServer.Instance.UnregisterTarget(GetTargetId());
            }
        }

        public Vector3 GetPosition() => transform.position;
        public int GetCurrentHp() => _currentHp.Value;
        public int GetMaxHp() => _maxHp.Value;

        public int GetArmorDefense()
        {
            // MVP: NPC без брони. После T-CB06 + NPC-armor-design — реальный подсчёт.
            return 0;
        }

        public bool IsAlive() => _currentHp.Value > 0;
        public bool IsPlayer() => false;
        public string GetDisplayName() => _data != null ? _data.displayName : "NPC";

        public ulong GetTargetId() => _targetId != 0 ? _targetId : (NetworkObject != null ? NetworkObject.NetworkObjectId : 0UL);

        public void ApplyDamage(DamageResult result, ulong attackerClientId)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[NpcTarget] ApplyDamage called on non-server.");
                return;
            }
            if (!result.isHit) return;
            if (_currentHp.Value <= 0) return;

            int newHp = Mathf.Max(0, _currentHp.Value - result.finalDamage);
            int delta = _currentHp.Value - newHp;  // positive = damage taken
            _currentHp.Value = newHp;
            OnHpChanged?.Invoke(newHp, delta);  // T-NPC-14: passive aggro tracking

            if (_debugLog)
            {
                Debug.Log($"[NpcTarget] npc={_targetId} took {result.finalDamage} from attacker={attackerClientId} (HP {_currentHp.Value + result.finalDamage} → {newHp}, isCrit={result.isCrit}, type={result.damageType})");
            }

            // T-NPC-12: Damage trigger на Animator (если NPC жив после удара).
            if (newHp > 0)
            {
                var anim = GetComponentInChildren<Animator>();
                if (anim != null && anim.runtimeAnimatorController != null)
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
            }

            // T-NPC-01 v0.2: при смерти — death animation + loot spawn + 3s corpse delay.
            if (newHp == 0)
            {
                if (_debugLog) Debug.Log($"[NpcTarget] npc={_targetId} killed. Spawning loot + Destroy in 3s.");
                OnKilled(attackerClientId);
                Destroy(gameObject, 3.0f);
            }
        }

        /// <summary>
        /// T-NPC-01 v0.2: death handler — death animation trigger + spawn NpcLootPickup (credits).
        /// </summary>
        private void OnKilled(ulong attackerClientId)
        {
            // Trigger Death animation (на child Animator, если есть).
            var animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                // Безопасный вызов: проверяем что параметр существует.
                foreach (var p in animator.parameters)
                {
                    if (p.type == AnimatorControllerParameterType.Trigger && p.name == "Death")
                    {
                        animator.SetTrigger("Death");
                        break;
                    }
                }
            }

            // T-NPC-03: Spawn NpcLootPickup на месте смерти с credits из NpcCombatData.
            SpawnLootPickup(attackerClientId);
        }

        /// <summary>
        /// T-NPC-12: установить конфиг лута от спавнера.
        /// Вызывается NpcSpawner'ом ДО NetworkObject.Spawn().
        /// </summary>
        public void SetLootConfig(GameObject lootPrefab, Items.LootTable lootTable)
        {
            _lootPrefab = lootPrefab;
            _lootTable = lootTable;
        }

        /// <summary>
        /// T-NPC-03 + T-NPC-04 v2 (T-NPC-12): server-only spawn loot на текущей позиции NPC.
        /// Использует: _lootTable для credits (GenerateCredits), _lootPrefab для визуала.
        /// Fallback: если оба null — programmatic жёлтая сфера + credits = maxHp/4 (backward compat).
        /// </summary>
        private void SpawnLootPickup(ulong attackerClientId)
        {
            // Определяем credits.
            int credits = 0;
            if (_lootTable != null)
            {
                credits = _lootTable.GenerateCredits();
            }
            else
            {
                // Backward compat: formula maxHp/4.
                if (_data != null)
                    credits = Mathf.Max(5, _data.maxHp / 4);
            }

            if (credits <= 0 && _lootTable == null) return;
            // Если credits=0 но lootTable есть — всё равно спавним (могут быть items).

            GameObject loot;

            if (_lootPrefab != null)
            {
                // Инстанциируем префаб дропа.
                loot = Instantiate(_lootPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
                loot.name = $"Loot_{_data?.displayName ?? "NPC"}";
            }
            else
            {
                // Backward compat: programmatic создание.
                loot = new GameObject($"Loot_{_data?.displayName ?? "NPC"}_CR{credits}");
                loot.transform.position = transform.position + Vector3.up * 0.5f;

                // Visual: small yellow sphere (placeholder).
                var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visual.name = "VisualMarker";
                visual.transform.SetParent(loot.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                var col = visual.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);
                var mr = visual.GetComponent<Renderer>();
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                var mat = new Material(shader);
                mat.color = new Color(1f, 0.85f, 0.2f, 1f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", mat.color);
                mr.sharedMaterial = mat;
            }

            // NpcLootPickup: если на префабе уже есть — используем, иначе добавляем.
            var pickup = loot.GetComponent<ProjectC.AI.NpcLootPickup>();
            if (pickup == null)
                pickup = loot.AddComponent<ProjectC.AI.NpcLootPickup>();

            pickup.creditsAmount = credits;
            pickup.displayName = _data != null ? $"{_data.displayName} Loot" : "Loot";
            pickup.interactionRadius = 2.0f;
            pickup.autoDespawnSeconds = 120f;

            // NetworkObject: если на префабе уже есть — используем, иначе добавляем.
            var netObj = loot.GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj == null)
                netObj = loot.AddComponent<Unity.Netcode.NetworkObject>();

            netObj.Spawn(destroyWithScene: true);
        }
    }
}
