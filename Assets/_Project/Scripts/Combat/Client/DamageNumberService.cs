// Project C: Real-Time Combat Engine — Damage Numbers
// DamageNumberService: client-side singleton, подписывается на CombatClientState.OnDamageDealt,
// спавнит world-space цифры урона через object pool.
// Design: docs/Character/Skills/real-time-combat/110_DAMAGE_NUMBERS.md
//
// Паттерн создания: NetworkManagerController.CreateDamageNumberService() (root GO, DontDestroyOnLoad).
// Аналог TargetHighlightService / TargetLockService.

using System.Collections.Generic;
using UnityEngine;
using ProjectC.Combat.Core;
using ProjectC.Combat.Config;

namespace ProjectC.Combat.Client
{
    /// <summary>
    /// Client-only singleton. Спавнит всплывающие цифры урона над целями.
    /// Создаётся в NetworkManagerController.CreateDamageNumberService().
    /// </summary>
    public class DamageNumberService : MonoBehaviour
    {
        public static DamageNumberService Instance { get; private set; }

        [Header("Config")]
        [Tooltip("DamageNumberConfig из Resources/Combat/. Если null — загружается автоматически.")]
        [SerializeField] private DamageNumberConfig _config;

        [Header("Pool")]
        [Tooltip("Префаб PF_DamageNumber (world-space Canvas + TMP). Если null — загружается из Resources/Prefabs/.")]
        [SerializeField] private GameObject _prefab;
        [Tooltip("Начальный размер пула.")]
        [SerializeField] private int _poolPrewarm = 10;

        // Пул
        private readonly Queue<DamageNumberInstance> _pool = new Queue<DamageNumberInstance>();

        // Подписка на CombatClientState
        private CombatClientState _combatState;

        // === Lifecycle ===

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[DamageNumberService] Replacing existing Instance (duplicate).");
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Load config from Resources if not assigned.
            if (_config == null)
            {
                _config = Resources.Load<DamageNumberConfig>("Combat/DamageNumberConfig_Default");
                if (_config == null)
                {
                    Debug.LogWarning("[DamageNumberService] DamageNumberConfig_Default not found in Resources/Combat/. Damage numbers disabled.");
                }
            }

            // Load prefab from Resources if not assigned.
            if (_prefab == null)
            {
                _prefab = Resources.Load<GameObject>("Prefabs/PF_DamageNumber");
                if (_prefab == null)
                {
                    Debug.LogWarning("[DamageNumberService] PF_DamageNumber not found in Resources/Prefabs/. Damage numbers disabled.");
                }
            }
        }

        private void Start()
        {
            // Prewarm pool
            PrewarmPool();

            // Subscribe to combat events
            _combatState = CombatClientState.Instance;
            if (_combatState != null)
            {
                _combatState.OnDamageDealt += OnDamageDealt;
            }
            else
            {
                Debug.LogWarning("[DamageNumberService] CombatClientState.Instance is null. Damage numbers won't appear until it's created.");
            }
        }

        private void OnDestroy()
        {
            if (_combatState != null)
            {
                _combatState.OnDamageDealt -= OnDamageDealt;
                _combatState = null;
            }

            // Clear pool
            foreach (var inst in _pool)
            {
                if (inst != null) Destroy(inst.gameObject);
            }
            _pool.Clear();

            if (Instance == this) Instance = null;
        }

        // === Event Handlers ===

        private void OnDamageDealt(DamageResult result)
        {
            // Check global toggle
            var combatConfig = CombatConfig.Instance;
            if (combatConfig != null && !combatConfig.showDamageNumbers) return;

            // Validate
            if (!result.isHit || result.finalDamage <= 0) return;
            if (_config == null || _prefab == null) return;

            // Get world position for target
            Vector3 worldPos = GetTargetPosition(result.targetId);
            if (worldPos == Vector3.zero) worldPos = result.targetPosition;

            // Offset над головой + случайный разброс
            worldPos.y += _config.worldOffsetY;
            worldPos.x += Random.Range(-_config.randomSpreadX, _config.randomSpreadX);

            // Duration из CombatConfig или дефолт
            float duration = combatConfig?.damageNumberDuration ?? 1.5f;

            // Spawn
            var instance = GetOrCreate();
            if (instance == null) return;

            instance.Spawn(_config, worldPos, result.finalDamage, result.isCrit,
                result.damageType, duration, ReturnToPool);
        }

        // === Target Position Lookup ===

        /// <summary>
        /// Найти мировую позицию цели по targetId.
        /// Возвращает Vector3.zero если цель не найдена.
        /// </summary>
        private Vector3 GetTargetPosition(ulong targetId)
        {
            if (targetId == 0UL) return Vector3.zero;

            // Search NpcTarget
            foreach (var npc in FindObjectsByType<NpcTarget>(FindObjectsInactive.Exclude))
            {
                if (npc != null && npc.GetTargetId() == targetId)
                    return npc.transform.position;
            }

            // Search PlayerTarget
            foreach (var pt in FindObjectsByType<PlayerTarget>(FindObjectsInactive.Exclude))
            {
                if (pt != null && pt.GetTargetId() == targetId)
                    return pt.transform.position;
            }

            return Vector3.zero;
        }

        // === Object Pool ===

        private void PrewarmPool()
        {
            if (_prefab == null) return;
            for (int i = 0; i < _poolPrewarm; i++)
            {
                var go = Instantiate(_prefab, transform);
                go.SetActive(false);
                var inst = go.GetComponent<DamageNumberInstance>();
                if (inst == null)
                {
                    inst = go.AddComponent<DamageNumberInstance>();
                    Debug.LogWarning("[DamageNumberService] PF_DamageNumber missing DamageNumberInstance component — added at runtime.");
                }
                _pool.Enqueue(inst);
            }
            Debug.Log($"[DamageNumberService] Pool prewarmed: {_pool.Count} instances.");
        }

        private DamageNumberInstance GetOrCreate()
        {
            if (_pool.Count > 0) return _pool.Dequeue();

            if (_prefab == null) return null;

            var go = Instantiate(_prefab, transform);
            var inst = go.GetComponent<DamageNumberInstance>();
            if (inst == null)
            {
                inst = go.AddComponent<DamageNumberInstance>();
                Debug.LogWarning("[DamageNumberService] PF_DamageNumber missing DamageNumberInstance component — added at runtime.");
            }
            return inst;
        }

        private void ReturnToPool(DamageNumberInstance instance)
        {
            if (instance == null) return;
            instance.gameObject.SetActive(false);
            _pool.Enqueue(instance);
        }
    }
}
