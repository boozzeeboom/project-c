// Project C: Skills VFX — Phase 1
// ISkillVfxProvider: абстракция над типом рендера VFX.
// Позволяет подменять 3D-партиклы ↔ 2D-спрайты без изменения кода скилов.
//
// Реализации:
//   - ParticleSystemVfxProvider — 3D ParticleSystem-based (Phase 1)
//   - SpriteVfxProvider — 2D SpriteRenderer frame-by-frame (Phase 3)

using ProjectC.Combat.Core;
using UnityEngine;

namespace ProjectC.Skills.Vfx
{
    /// <summary>
    /// Абстракция рендера VFX скила.
    /// Код скилов (SkillInputService, CombatClientState) вызывает эти методы,
    /// не зная какая конкретная реализация подставлена.
    /// </summary>
    public interface ISkillVfxProvider
    {
        /// <summary>
        /// Проиграть cast VFX (muzzle flash, заряд, свечение) в начале каста.
        /// </summary>
        /// <param name="config">Конфиг навыка.</param>
        /// <param name="character">Transform персонажа (для определения костей).</param>
        void PlayCastVfx(SkillNodeConfig config, Transform character);

        /// <summary>
        /// Запустить снаряд от from до to. Вызывает onArrived при достижении цели.
        /// </summary>
        /// <param name="config">Конфиг навыка (projectileVfxPrefab, projectileSpeed, projectileArcHeight).</param>
        /// <param name="from">Позиция старта.</param>
        /// <param name="to">Точка назначения.</param>
        /// <param name="onArrived">Callback при достижении цели (для impact VFX).</param>
        void PlayProjectileVfx(SkillNodeConfig config, Vector3 from, Vector3 to, System.Action onArrived);

        /// <summary>
        /// Проиграть impact VFX при попадании в цель.
        /// </summary>
        /// <param name="config">Конфиг навыка (impactVfxPrefab, impactVfxDuration).</param>
        /// <param name="position">Позиция попадания.</param>
        /// <param name="damageType">Тип урона (для окраски).</param>
        /// <param name="isCrit">Критический удар.</param>
        void PlayImpactVfx(SkillNodeConfig config, Vector3 position, DamageType damageType, bool isCrit);
    }
}
