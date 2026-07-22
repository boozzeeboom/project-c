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
        private static SkillVfxService _instance;

        /// <summary>
        /// Self-healing singleton: если NMC не создал — находим существующий или создаём новый.
        /// </summary>
        public static SkillVfxService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<SkillVfxService>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[SkillVfxService]");
                        _instance = go.AddComponent<SkillVfxService>();
                        Debug.Log("[SkillVfxService] Lazy-initialized (NMC didn't create — self-spawned)");
                    }
                }
                return _instance;
            }
        }

        private ISkillVfxProvider _provider;
        private VfxObjectPool _pool;

        // ==================== Lifecycle ====================

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[SkillVfxService] Duplicate instance, destroying.");
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Создаём пул с корневым объектом для хранения неактивных экземпляров.
            var poolRoot = new GameObject("[VfxPool]").transform;
            poolRoot.SetParent(transform);
            _pool = new VfxObjectPool(poolRoot);

            // Дефолтный провайдер — ParticleSystem-based.
            _provider = new ParticleSystemVfxProvider(_pool, this);

            Debug.Log("[SkillVfxService] Awake: singleton created, provider=ParticleSystemVfxProvider");
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
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
        /// Если config=null — использует generic impact (melee sparks).
        /// </summary>
        public void PlayImpactVfx(SkillNodeConfig config, Vector3 position, DamageType damageType, bool isCrit)
        {
            // Generic impact: если config не передан — используем дефолтный impact (melee sparks).
            if (config == null)
            {
                // Спавним примитивную сферу-вспышку (fallback когда нет skill-конфига)
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.position = position;
                go.transform.localScale = Vector3.one * 0.3f;
                var rend = go.GetComponent<MeshRenderer>();
                if (rend != null)
                {
                    rend.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    rend.material.color = DamageTypeColors.Get(damageType);
                }
                Object.Destroy(go.GetComponent<Collider>());
                Object.Destroy(go, 0.3f);
                if (Debug.isDebugBuild)
                    Debug.Log($"[SkillVfxService] PlayImpactVfx: GENERIC (no config) pos={position} dmgType={damageType}");
                return;
            }

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
