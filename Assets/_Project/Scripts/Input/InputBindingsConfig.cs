// Project C: Input System — Phase 1.5 (full keybind coverage)
// InputBindingsConfig: ScriptableObject с ПОЛНЫМ списком биндов игры.
// MVP: настраивается через инспектор. Phase 2: через UI-меню.
//
// ПОКРЫТИЕ (все действия, которые сейчас хардкодятся в NetworkPlayer.Update + UI-окнах):
//   - CombatSkills:  10 (6 mouse combos + 4 digit slots) — боевые скиллы
//   - Movement:      WASD (4) + Run(Shift) + Jump(Space) — пеший режим
//   - Interaction:   Interact(E), Board(F), CharacterWindow(P), SkillTree(P)
//   - ShipControls:  Thrust(WS), Yaw(AD), Vertical(EQ), Boost(Shift)
//   - Communication: CommPanel(T) — диспетчерская
//   - Debug:         F3, F4 — debug HUDs
//
// ВАЖНО: Phase 1.5 хранит ТОЛЬКО данные (asset). Phase 2 добавит polling — сейчас
// NetworkPlayer.Update читает клавиши НАПРЯМУЮ через Keyboard.current.* (см. 10_CURRENT_STATE.md).
// Этот asset нужен для:
//   1) Игрок видит все свои клавиши в инспекторе (read-only view до Phase 2 UI).
//   2) Phase 2 UI Rebind будет менять значения и persist в PlayerPrefs.
//
// Design: docs/Character/input-system/30_INPUT_BINDINGS_CONFIG_DESIGN.md
//        docs/Character/input-system/40_MIGRATION_PLAN.md §Phase 1.5.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectC.Skills;  // SkillInputSlot enum

namespace ProjectC.Input
{
    /// <summary>
    /// ScriptableObject с дефолтными биндами ВСЕЙ игры (combat + movement + interaction + ship + debug).
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectC/Input/InputBindingsConfig", fileName = "InputBindingsConfig")]
    public class InputBindingsConfig : ScriptableObject
    {
        // ==================== Skill Binding (combat) ====================

        /// <summary>
        /// Один бинд для SkillInputSlot — комбинация мышь+клавиша+модификатор.
        /// </summary>
        [Serializable]
        public struct SkillKeyBinding
        {
            [Tooltip("Какой SkillInputSlot срабатывает при матче")]
            public SkillInputSlot slot;

            [Tooltip("Кнопка мыши (0=None, 1=LeftMouse, 2=RightMouse, 3=MiddleMouse)")]
            public int mouseButtonRaw;

            [Tooltip("Модификатор (None / LeftCtrl / LeftShift / LeftAlt)")]
            public Key modifier;

            [Tooltip("Fallback клавиша (например Key.K для Primary как legacy fallback)")]
            public Key fallbackKey;

            [Tooltip("Имя для UI ('ЛКМ', 'Ctrl+ЛКМ', '1', ...)")]
            public string displayName;

            [Tooltip("Только на суше (если true — игнорируется в корабле; для Q/R см. Q-INP-11)")]
            public bool onlyOnFoot;
        }

        [Header("Target Cycling (Q/E on foot, T-LOCK-01)")]
        [Tooltip("Клавиша для предыдущей цели. Только на суше (не в корабле).")]
        public Key targetPrevKey = Key.Q;
        [Tooltip("Клавиша для следующей цели. Только на суше (не в корабле).")]
        public Key targetNextKey = Key.E;

        [Header("Combat Skills (Q-INP-02)")]
        [Tooltip("6 биндов: ЛКМ/ПКМ (с модификаторами и без) → Primary/Secondary/Slot1-4. " +
                 "Фолбэк-клавиши (Digit1..4) удалены — Slot1-4 доступны ТОЛЬКО через Ctrl/Shift+мышь, " +
                 "чтобы не конкурировать с фолбэком Primary (K) и не путать игрока.")]
        public List<SkillKeyBinding> combatSkills = new List<SkillKeyBinding>
        {
            new SkillKeyBinding { slot = SkillInputSlot.Primary,   mouseButtonRaw = 1, modifier = Key.None,       fallbackKey = Key.K,      displayName = "ЛКМ",         onlyOnFoot = false },
            new SkillKeyBinding { slot = SkillInputSlot.Secondary, mouseButtonRaw = 2, modifier = Key.None,       fallbackKey = Key.None,   displayName = "ПКМ",         onlyOnFoot = false },
            new SkillKeyBinding { slot = SkillInputSlot.Slot1,     mouseButtonRaw = 1, modifier = Key.LeftCtrl,  fallbackKey = Key.None,   displayName = "Ctrl+ЛКМ",    onlyOnFoot = false },
            new SkillKeyBinding { slot = SkillInputSlot.Slot2,     mouseButtonRaw = 2, modifier = Key.LeftCtrl,  fallbackKey = Key.None,   displayName = "Ctrl+ПКМ",    onlyOnFoot = false },
            new SkillKeyBinding { slot = SkillInputSlot.Slot3,     mouseButtonRaw = 1, modifier = Key.LeftShift, fallbackKey = Key.None,   displayName = "Shift+ЛКМ",   onlyOnFoot = false },
            new SkillKeyBinding { slot = SkillInputSlot.Slot4,     mouseButtonRaw = 2, modifier = Key.LeftShift, fallbackKey = Key.None,   displayName = "Shift+ПКМ",   onlyOnFoot = false },
        };

        // ==================== Generic Action Binding ====================

        /// <summary>
        /// Категория действия (для группировки в UI и фильтрации).
        /// </summary>
        public enum ActionCategory { Movement, Interaction, Ship, Communication, Debug, UI }

        /// <summary>
        /// Идентификатор игрового действия. Phase 1.5 — read-only enum; Phase 2 UI rebind будет использовать его для rebind flow.
        /// </summary>
        public enum GameAction
        {
            // Movement (пеший)
            MoveForward, MoveBackward, MoveLeft, MoveRight,
            Jump, Run,

            // Interaction (пеший)
            Interact,        // E — pickup/chest/market/npc
            ModeSwitch,      // F — board/exit/gather/crafting/door
            OpenCharacter,   // P — CharacterWindow
            OpenSkillTree,   // P (доп. binding) ИЛИ отдельная клавиша — Phase 2 решит
            OpenInventory,   // Tab — TODO (см. NetworkPlayer.cs:27 comment)

            // Ship
            ShipThrustForward, ShipThrustBackward,
            ShipYawLeft, ShipYawRight,
            ShipVerticalUp, ShipVerticalDown,
            ShipBoost,

            // Communication
            CommPanel,       // T (только пилот)

            // Debug
            DebugF3, DebugF4,

            // UI
            CloseTopPanel,   // Esc

            // Ship (продолжение — новые действия добавлять ТОЛЬКО в конец, не ломать сериализацию!)
            ShipToggleEngine, // ENTER — запуск/остановка двигателя

            // Interaction (продолжение)
            PickupItem,       // F — подбор предметов (высший приоритет на F)
        }

        /// <summary>
        /// Generic action binding — одно действие → одна кнопка/клавиша (без модификаторов и комбинаций).
        /// Для боевых скиллов используется SkillKeyBinding (там есть модификаторы).
        /// </summary>
        [Serializable]
        public struct ActionBinding
        {
            public GameAction action;
            public ActionCategory category;

            [Tooltip("Кнопка мыши (0=None, 1=LeftMouse, 2=RightMouse, 3=MiddleMouse)")]
            public int mouseButtonRaw;

            [Tooltip("Клавиша (Key.None = используется только мышь)")]
            public Key key;

            [Tooltip("Имя для UI ('W', 'E', 'ЛКМ', 'Space', ...)")]
            public string displayName;
        }

        [Header("Generic Action Bindings")]
        [Tooltip("Все остальные действия: движение, интеракции, корабль, отладка, UI")]
        public List<ActionBinding> actions = new List<ActionBinding>
        {
            // ---- Movement (пеший) ----
            new ActionBinding { action = GameAction.MoveForward,     category = ActionCategory.Movement,     key = Key.W,        mouseButtonRaw = 0, displayName = "W" },
            new ActionBinding { action = GameAction.MoveBackward,    category = ActionCategory.Movement,     key = Key.S,        mouseButtonRaw = 0, displayName = "S" },
            new ActionBinding { action = GameAction.MoveLeft,        category = ActionCategory.Movement,     key = Key.A,        mouseButtonRaw = 0, displayName = "A" },
            new ActionBinding { action = GameAction.MoveRight,       category = ActionCategory.Movement,     key = Key.D,        mouseButtonRaw = 0, displayName = "D" },
            new ActionBinding { action = GameAction.Jump,            category = ActionCategory.Movement,     key = Key.Space,    mouseButtonRaw = 0, displayName = "Space" },
            new ActionBinding { action = GameAction.Run,             category = ActionCategory.Movement,     key = Key.LeftShift, mouseButtonRaw = 0, displayName = "Shift" },

            // ---- Interaction ----
            new ActionBinding { action = GameAction.Interact,        category = ActionCategory.Interaction,  key = Key.E,        mouseButtonRaw = 0, displayName = "E" },
            new ActionBinding { action = GameAction.ModeSwitch,      category = ActionCategory.Interaction,  key = Key.F,        mouseButtonRaw = 0, displayName = "F" },
            new ActionBinding { action = GameAction.OpenCharacter,   category = ActionCategory.Interaction,  key = Key.P,        mouseButtonRaw = 0, displayName = "P" },
            new ActionBinding { action = GameAction.OpenInventory,   category = ActionCategory.Interaction,  key = Key.Tab,      mouseButtonRaw = 0, displayName = "Tab" },

            // ---- Ship ----
            new ActionBinding { action = GameAction.ShipThrustForward,  category = ActionCategory.Ship, key = Key.W,        mouseButtonRaw = 0, displayName = "W" },
            new ActionBinding { action = GameAction.ShipThrustBackward, category = ActionCategory.Ship, key = Key.S,        mouseButtonRaw = 0, displayName = "S" },
            new ActionBinding { action = GameAction.ShipYawLeft,        category = ActionCategory.Ship, key = Key.A,        mouseButtonRaw = 0, displayName = "A" },
            new ActionBinding { action = GameAction.ShipYawRight,       category = ActionCategory.Ship, key = Key.D,        mouseButtonRaw = 0, displayName = "D" },
            new ActionBinding { action = GameAction.ShipVerticalUp,     category = ActionCategory.Ship, key = Key.E,        mouseButtonRaw = 0, displayName = "E" },
            new ActionBinding { action = GameAction.ShipVerticalDown,   category = ActionCategory.Ship, key = Key.Q,        mouseButtonRaw = 0, displayName = "Q" },
            new ActionBinding { action = GameAction.ShipBoost,          category = ActionCategory.Ship, key = Key.LeftShift, mouseButtonRaw = 0, displayName = "Shift" },

            // ---- Communication ----
            new ActionBinding { action = GameAction.CommPanel,       category = ActionCategory.Communication, key = Key.T, mouseButtonRaw = 0, displayName = "T" },

            // ---- Debug ----
            new ActionBinding { action = GameAction.DebugF3,         category = ActionCategory.Debug, key = Key.F3, mouseButtonRaw = 0, displayName = "F3" },
            new ActionBinding { action = GameAction.DebugF4,         category = ActionCategory.Debug, key = Key.F4, mouseButtonRaw = 0, displayName = "F4" },

            // ---- UI ----
            new ActionBinding { action = GameAction.CloseTopPanel,  category = ActionCategory.UI, key = Key.Escape, mouseButtonRaw = 0, displayName = "Esc" },

            // ---- Ship (продолжение) — новые действия в конец, не ломать сериализацию! ----
            new ActionBinding { action = GameAction.ShipToggleEngine,   category = ActionCategory.Ship, key = Key.Enter,    mouseButtonRaw = 0, displayName = "Enter" },

            // ---- Interaction (продолжение) ----
            new ActionBinding { action = GameAction.PickupItem,     category = ActionCategory.Interaction,  key = Key.F,        mouseButtonRaw = 0, displayName = "F" },
        };

        // ==================== Helper Lookup ====================

        /// <summary>Найти SkillKeyBinding по slot. Returns null если не найдено.</summary>
        public SkillKeyBinding? FindSkillBinding(SkillInputSlot slot)
        {
            for (int i = 0; i < combatSkills.Count; i++)
            {
                if (combatSkills[i].slot == slot) return combatSkills[i];
            }
            return null;
        }

        /// <summary>Найти ActionBinding по GameAction. Returns null если не найдено.</summary>
        public ActionBinding? FindActionBinding(GameAction action)
        {
            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i].action == action) return actions[i];
            }
            return null;
        }
    }
}
