// Project C: Skills VFX — Phase 1
// SkillVfxService: client-side singleton-сервис для VFX скилов.
// Создаётся в NetworkManagerController (как DamageNumberService).
// Делегирует всю работу ISkillVfxProvider.
//
// Точки инжекции:
//   - PlayCastVfx: SkillInputService.TryActivate (начало каста)
//   - PlayProjectileVfx: SkillInputService.TryActivate (throwables/arrows)
//   - PlayImpactVfx: CombatClientState.HandleAttackLanded (попадание)

using ProjectC.Combat.Core;
using UnityEngine;

namespace ProjectC.Skills.Vfx
{
    /// <summary>
    /// Client-side VFX service. Спавнит cast/projectile/impact эффекты через ISkillVfxProvider.
    /// </summary>
    public class SkillVfxService : MonoBehaviour
    {
        public static SkillVfxService Instance { get; private set; }

        [Header("Pool")]
        [Tooltip("Максимальное количество prewarm-объектов на префаб.")]
        [SerializeField] private int _poolPrewarm = 5;

        private ISkillVfxProvider _provider;
        private VfxObjectPool _pool;

        // ==================== Lifecycle ====================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[SkillVfxService] Duplicate instance, destroying.");
                Destroy(this);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Создаём пул с корневым объектом для хранения неактивных экземпляров.
            var poolRoot = new GameObject("[VfxPool]").transform;
            poolRoot.SetParent(transform);
            _pool = new VfxObjectPool(poolRoot);

            // Дефолтный провайдер — ParticleSystem-based.
            // В Phase 3 заменится на SpriteVfxProvider или HybridVfxProvider.
            _provider = new ParticleSystemVfxProvider(_pool, this);

            if (Debug.isDebugBuild)
                Debug.Log("[SkillVfxService] Awake: singleton created, provider=ParticleSystemVfxProvider");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _pool?.Clear();
        }

        // ==================== Public API ====================

        /// <summary>
        /// Проиграть cast VFX. Вызывается из SkillInputService.TryActivate.
        /// </summary>
        public void PlayCastVfx(SkillNodeConfig config, Transform character)
        {
            if (config == null || character == null) return;
            if (config.castVfxPrefab == null && config.twoDVfxAnimation == null) return;

            if (Debug.isDebugBuild)
                Debug.Log($"[SkillVfxService] PlayCastVfx: skill='{config.skillId}' prefab='{config.castVfxPrefab?.name ?? "null"}'");

            _provider.PlayCastVfx(config, character);
        }

        /// <summary>
        /// Запустить снаряд. Вызывается из SkillInputService.TryActivate.
        /// </summary>
        public void PlayProjectileVfx(SkillNodeConfig config, Vector3 from, Vector3 to, System.Action onArrived = null)
        {
            if (config == null) return;

            if (Debug.isDebugBuild)
                Debug.Log($"[SkillVfxService] PlayProjectileVfx: skill='{config.skillId}' from={from} to={to} speed={config.projectileSpeed}");

            // Если задан префаб или скорость > 0 — запускаем через провайдер.
            // Иначе fallback на примитив внутри ParticleSystemVfxProvider.
            _provider.PlayProjectileVfx(config, from, to, onArrived);
        }

        /// <summary>
        /// Проиграть impact VFX. Вызывается из CombatClientState.HandleAttackLanded.
        /// </summary>
        public void PlayImpactVfx(SkillNodeConfig config, Vector3 position, DamageType damageType, bool isCrit)
        {
            if (config == null) return;
            if (config.impactVfxPrefab == null && config.twoDVfxAnimation == null) return;

            if (Debug.isDebugBuild)
                Debug.Log($"[SkillVfxService] PlayImpactVfx: skill='{config.skillId}' pos={position} dmgType={damageType} crit={isCrit}");

            _provider.PlayImpactVfx(config, position, damageType, isCrit);
        }

        // ==================== Provider swap (Phase 3) ====================

        /// <summary>
        /// Заменить провайдер (Phase 3: переключение 3D → 2D).
        /// </summary>
        public void SetProvider(ISkillVfxProvider newProvider)
        {
            _provider = newProvider ?? _provider;
            Debug.Log($"[SkillVfxService] Provider swapped to {_provider.GetType().Name}");
        }
    }
}
