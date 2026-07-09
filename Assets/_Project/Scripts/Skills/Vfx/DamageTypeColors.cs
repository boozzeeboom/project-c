// Project C: Skills VFX — Phase 1
// DamageTypeColors: статический хелпер для маппинга DamageType → Color.
// Используется ParticleSystemVfxProvider и будущим SpriteVfxProvider.

using ProjectC.Combat.Core;
using UnityEngine;

namespace ProjectC.Skills.Vfx
{
    /// <summary>
    /// Маппинг DamageType → Color для окраски impact VFX.
    /// </summary>
    public static class DamageTypeColors
    {
        public static Color Get(DamageType type) => type switch
        {
            DamageType.Physical  => new Color(0.8f, 0.2f, 0.1f),   // красный (кровь)
            DamageType.Ballistic => new Color(1.0f, 0.8f, 0.1f),   // жёлтый (болт/пуля)
            DamageType.Explosive => new Color(1.0f, 0.4f, 0.05f),  // оранжевый (взрыв)
            DamageType.Antigrav  => new Color(0.3f, 0.7f, 1.0f),   // голубой (антиграв)
            DamageType.Mesium    => new Color(0.6f, 0.1f, 0.9f),   // фиолетовый (мезий)
            _                    => Color.white
        };
    }
}
