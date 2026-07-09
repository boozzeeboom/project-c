// Project C: Skills VFX — Phase 0 (stub)
// SpriteAnimationAsset: ScriptableObject для 2D покадровой анимации.
// Phase 3 реализует полноценный рендер через SpriteVfxProvider.
//
// Пока — заглушка, чтобы SkillNodeConfig.twoDVfxAnimation компилировался.

using UnityEngine;

namespace ProjectC.Skills
{
    /// <summary>
    /// Stub: контейнер для 2D-анимации (спрайт-лист + fps + loop).
    /// Phase 3 заменит на полноценную реализацию с SpriteVfxProvider.
    /// </summary>
    [CreateAssetMenu(fileName = "SpriteAnim_", menuName = "Project C/VFX/Sprite Animation", order = 50)]
    public class SpriteAnimationAsset : ScriptableObject
    {
        [Tooltip("Массив спрайтов для покадровой анимации.")]
        public Sprite[] frames = System.Array.Empty<Sprite>();

        [Tooltip("Кадров в секунду.")]
        [Range(4, 60)] public int fps = 12;

        [Tooltip("Зацикливать анимацию.")]
        public bool loop = true;
    }
}
