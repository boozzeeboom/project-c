// Project C: Real-Time Combat Engine — Damage Numbers
// DamageNumberConfig: SO с настройками отображения всплывающих цифр урона.
// Design: docs/Character/Skills/real-time-combat/110_DAMAGE_NUMBERS.md
//
// Настраивается дизайнером через инспектор. Загружается DamageNumberService из Resources/Combat/.

using UnityEngine;

namespace ProjectC.Combat.Config
{
    [CreateAssetMenu(fileName = "DamageNumberConfig_Default", menuName = "Project C/Combat/Damage Number Config")]
    public class DamageNumberConfig : ScriptableObject
    {
        [Header("Colors by Damage Type")]
        [Tooltip("Physical урон (мечи, кинжалы, bare-fist).")]
        public Color physicalColor = new Color(1f, 0.85f, 0.3f, 1f);    // жёлтый
        [Tooltip("Ballistic урон (стрелы, болты, пули).")]
        public Color ballisticColor = new Color(1f, 0.55f, 0.1f, 1f);    // оранжевый
        [Tooltip("Antigrav урон (g-волна, антиграв клинок).")]
        public Color antigravColor = new Color(0.3f, 0.8f, 1f, 1f);      // голубой
        [Tooltip("Explosive урон (гранаты, мины, взрывы).")]
        public Color explosiveColor = new Color(1f, 0.25f, 0.1f, 1f);    // красный
        [Tooltip("Mesium урон (токсины, мезиевая винтовка).")]
        public Color mesiumColor = new Color(0.2f, 1f, 0.4f, 1f);        // зелёный

        [Header("Crit Override")]
        [Tooltip("Цвет текста при критическом ударе. Если не задан — используется цвет типа урона.")]
        public bool useCustomCritColor = true;
        public Color critColor = new Color(1f, 0.15f, 0.05f, 1f);       // ярко-красный
        [Tooltip("Множитель размера шрифта при крите.")]
        public float critFontSizeMultiplier = 1.3f;
        [Tooltip("Символ '!' после цифры при крите.")]
        public bool showCritExclamation = true;

        [Header("Font Sizes")]
        [Tooltip("Размер шрифта для обычного урона.")]
        public float normalFontSize = 36f;
        [Tooltip("Базовый цвет текста (перекрывается цветом типа урона).")]
        public Color baseTextColor = Color.white;

        [Header("Animation")]
        [Tooltip("Скорость всплытия (метров в секунду).")]
        public float floatSpeed = 2.5f;
        [Tooltip("Кривая прозрачности: X=0..1 (время), Y=alpha (0=прозрачный, 1=непрозрачный).")]
        public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [Tooltip("Случайный разброс по горизонтали (±метры).")]
        public float randomSpreadX = 0.4f;

        [Header("World Offset")]
        [Tooltip("Смещение над целью (метры).")]
        public float worldOffsetY = 2.2f;

        // === Public API ===

        public Color GetColorForType(Combat.Core.DamageType type)
        {
            switch (type)
            {
                case Combat.Core.DamageType.Physical:  return physicalColor;
                case Combat.Core.DamageType.Ballistic: return ballisticColor;
                case Combat.Core.DamageType.Antigrav:  return antigravColor;
                case Combat.Core.DamageType.Explosive: return explosiveColor;
                case Combat.Core.DamageType.Mesium:    return mesiumColor;
                default:                               return baseTextColor;
            }
        }

        public Color GetEffectiveColor(Combat.Core.DamageType type, bool isCrit)
        {
            if (isCrit && useCustomCritColor) return critColor;
            return GetColorForType(type);
        }

        public float GetEffectiveFontSize(bool isCrit)
        {
            return isCrit ? normalFontSize * critFontSizeMultiplier : normalFontSize;
        }
    }
}
