// Project C: Input System — Phase 1 (MVP)
// InputBindingsConfig: ScriptableObject с дефолтными биндами боевых навыков.
// MVP: настраивается через инспектор. Phase 2: через UI-меню.
//
// Что внутри:
//   - combatSkills (List<SkillKeyBinding>): 6 биндов (ЛКМ, ПКМ, Ctrl+ЛКМ, Ctrl+ПКМ, Shift+ЛКМ, Shift+ПКМ)
//     + опциональные 4 цифровых (1-4 для Slot1-4).
//
// Важно: не хардкодит клавиши в коде. Игрок может менять asset в инспекторе.
// SkillInputService читает из InputBindingsRuntime.Instance.Config в Update().
//
// Design: docs/Character/input-system/30_INPUT_BINDINGS_CONFIG_DESIGN.md

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectC.Skills;

namespace ProjectC.Input
{
    /// <summary>
    /// ScriptableObject с дефолтными биндами боевых навыков.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectC/Input/InputBindingsConfig", fileName = "InputBindingsConfig")]
    public class InputBindingsConfig : ScriptableObject
    {
        /// <summary>
        /// Один бинд: слот SkillInputService → комбинация мышь+клавиша+модификатор.
        /// </summary>
        [Serializable]
        public struct SkillKeyBinding
        {
            [Tooltip("Какой SkillInputSlot срабатывает при матче")]
            public SkillInputSlot slot;

            [Tooltip("Кнопка мыши (None = без мыши, чисто клавиатура). UnityEngine.UIElements.MouseButton enum.")]
            public int mouseButtonRaw;

            [Tooltip("Модификатор (None = без модификатора, LeftCtrl, LeftShift, LeftAlt)")]
            public Key modifier;

            [Tooltip("Fallback клавиша (None = нет; например Key.K для Primary)")]
            public Key fallbackKey;

            [Tooltip("Имя для UI (напр. 'ЛКМ', 'Ctrl+ЛКМ')")]
            public string displayName;

            [Tooltip("Только на суше (если true — игнорируется когда игрок в корабле)")]
            public bool onlyOnFoot;
        }

        [Header("Боевые навыки (Q-INP-02)")]
        [Tooltip("6 биндов мыши + опционально 4 цифровых (1-4 для Slot1-4)")]
        public List<SkillKeyBinding> combatSkills = new List<SkillKeyBinding>
        {
            // 6 боевых комбинаций (Q-INP-02)
            // mouseButtonRaw: 0=None, 1=LeftMouse, 2=RightMouse, 3=MiddleMouse
            new SkillKeyBinding { slot = SkillInputSlot.Primary,   mouseButtonRaw = 1, modifier = Key.None,       fallbackKey = Key.K,      displayName = "ЛКМ",         onlyOnFoot = false },
            new SkillKeyBinding { slot = SkillInputSlot.Secondary, mouseButtonRaw = 2, modifier = Key.None,       fallbackKey = Key.None,   displayName = "ПКМ",         onlyOnFoot = false },
            new SkillKeyBinding { slot = SkillInputSlot.Slot1,     mouseButtonRaw = 1, modifier = Key.LeftCtrl,  fallbackKey = Key.None,   displayName = "Ctrl+ЛКМ",    onlyOnFoot = false },
            new SkillKeyBinding { slot = SkillInputSlot.Slot2,     mouseButtonRaw = 2, modifier = Key.LeftCtrl,  fallbackKey = Key.None,   displayName = "Ctrl+ПКМ",    onlyOnFoot = false },
            new SkillKeyBinding { slot = SkillInputSlot.Slot3,     mouseButtonRaw = 1, modifier = Key.LeftShift, fallbackKey = Key.None,   displayName = "Shift+ЛКМ",   onlyOnFoot = false },
            new SkillKeyBinding { slot = SkillInputSlot.Slot4,     mouseButtonRaw = 2, modifier = Key.LeftShift, fallbackKey = Key.None,   displayName = "Shift+ПКМ",   onlyOnFoot = false },

            // 4 цифровых слота (Phase 1 bonus, см. Q-INP-10)
            new SkillKeyBinding { slot = SkillInputSlot.Slot1, mouseButtonRaw = 0, modifier = Key.None, fallbackKey = Key.Digit1, displayName = "1", onlyOnFoot = false },
            new SkillKeyBinding { slot = SkillInputSlot.Slot2, mouseButtonRaw = 0, modifier = Key.None, fallbackKey = Key.Digit2, displayName = "2", onlyOnFoot = false },
            new SkillKeyBinding { slot = SkillInputSlot.Slot3, mouseButtonRaw = 0, modifier = Key.None, fallbackKey = Key.Digit3, displayName = "3", onlyOnFoot = false },
            new SkillKeyBinding { slot = SkillInputSlot.Slot4, mouseButtonRaw = 0, modifier = Key.None, fallbackKey = Key.Digit4, displayName = "4", onlyOnFoot = false },
        };
    }
}
