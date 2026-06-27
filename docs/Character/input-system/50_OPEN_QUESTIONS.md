# Open Questions — input system

> **Дата:** 2026-06-28
> **Что решено:** см. `00_README.md` и `40_MIGRATION_PLAN.md`
> **Что НЕ решено:** этот файл

---

## Q-INP-04: Shift+ЛКМ при беге

**Проблема:** когда игрок бежит (Shift зажат) и кликает ЛКМ — должно ли сработать `Slot3` (Shift+ЛКМ) или просто `Primary` (ЛКМ)?

**Гипотеза A:** `Primary` (ЛКМ имеет приоритет без модификатора).
**Гипотеза B:** `Slot3` (Shift модифицирует ЛКМ как combo).

**Решение:** TBD в Phase 1 implementation. Предлагаю гипотезу B (Shift+ЛКМ = combo skill), потому что:
- Соответствует Q-INP-02 (Shift+ЛКМ = один из 6 биндов).
- Логично для игрока: "зажал Shift и кликнул — выполнил особый скилл".
- Конфликт решается через `wasPressedThisFrame` на ЛКМ (один кадр — один матч).

**Проверить в Play Mode:** одновременно зажать Shift и кликнуть ЛКМ — должно сработать `Slot3`, не `Primary`.

---

## Q-INP-05: Mouse delta во время боевых кликов

**Проблема:** камера читает `Mouse.current.delta` для look-around. Когда игрок кликает ЛКМ для атаки, камера НЕ дёргается (это не движение мыши, а клик). Но если игрок зажмёт ЛКМ и потащит — это сейчас камера не вращается (в коде только delta, не press).

**Решение:** ничего не делать. Камера работает через delta, атаки через wasPressed. Конфликта нет.

---

## Q-INP-06: Должен ли `DebugAttackNearestNpc` остаться?

**Сейчас:** есть legacy `DebugAttackNearestNpc` в NetworkPlayer.cs (НЕ показано в grep, но упоминается в audit T-RTC06). Не используется нигде с тех пор как добавили SkillInputService.

**Решение:** удалить в **Phase 2 cleanup**, не в Phase 1. Не блокирует миграцию.

---

## Q-INP-07: Кнопка `[ИЗУЧИТЬ НАВЫК]` в CharacterWindow — клик мышью

**Сейчас:** открывает SkillTreeWindow по клику мыши (через `RegisterCallback<ClickEvent>`).

**Q-INP-02:** Q-INP-02 разрешает только боевые скиллы, не UI buttons. **Не конфликт.**

---

## Q-INP-08: Конфликт Esc в SkillTreeWindow + CraftingWindow одновременно

**Сценарий:** игрок открыл CharacterWindow → SkillTreeWindow → crafted → CraftingWindow. Три окна в стеке. Esc → что закрывается?

**Решение:** UIManager решает (стек TopPanel). Если все три окна зарегистрированы в UIManager — закрывается верхнее. Проверить регистрацию в Phase 1.

---

## Q-INP-09: Gamepad / Controller support

**Q-INP-02** не упоминает геймпад. **Решение:** MVP — только клавиатура + мышь. Gamepad — отдельный этап (после Phase 2 rebind UI).

**Когда делать:** если/когда понадобится (PvP, mobile port). Не блокирует.

---

## Q-INP-10: Числовые клавиши 1-4 для Slot1-4

**Сейчас:** SkillInputService имеет слоты `Slot1..Slot4`, но NetworkPlayer.Update их не триггерит.

**Решение Phase 1:** добавить в InputBindingsConfig:
```csharp
new SkillKeyBinding { slot = SkillInputSlot.Slot1, mouseButton = MouseButton.None, modifier = Key.None, fallbackKey = Key.Digit1, displayName = "1" },
new SkillKeyBinding { slot = SkillInputSlot.Slot2, mouseButton = MouseButton.None, modifier = Key.None, fallbackKey = Key.Digit2, displayName = "2" },
new SkillKeyBinding { slot = SkillInputSlot.Slot3, mouseButton = MouseButton.None, modifier = Key.None, fallbackKey = Key.Digit3, displayName = "3" },
new SkillKeyBinding { slot = SkillInputSlot.Slot4, mouseButton = MouseButton.None, modifier = Key.None, fallbackKey = Key.Digit4, displayName = "4" },
```

Это даст 10 биндов всего (6 мышиных + 4 цифровых). Игрок может настроить любой навык на любую клавишу через InputBindingsConfig.asset.

---

## Q-INP-11: Что если игрок хочет Q / R как слоты

**Решение Phase 1:** добавить в InputBindingsConfig:
```csharp
new SkillKeyBinding { slot = SkillInputSlot.Slot1, mouseButton = MouseButton.None, modifier = Key.None, fallbackKey = Key.Q, displayName = "Q" },  // override Digit1
new SkillKeyBinding { slot = SkillInputSlot.Slot2, mouseButton = MouseButton.None, modifier = Key.None, fallbackKey = Key.R, displayName = "R" },  // override Digit2
```

**Но!** Q зажата в корабле (vertical down). Конфликт: Q-tap в корабле = Meziy vertical down. Q-tap на суше = skill. Различить через `_inShip` guard.

**Решение:** в `IsBindingPressed()` добавить проверку:
```csharp
// Q/R только на суше (в корабле они — ship controls)
if ((b.fallbackKey == Key.Q || b.fallbackKey == Key.R) && /* игрок в корабле */) return false;
```

`NetworkPlayer.IsInShip` публичный. SkillInputService может его читать.

**TBD:** реализовать или оставить на Phase 2.

---

## Q-INP-12: Слоты скиллов должны иметь КАСТОМНЫЕ навыки

**Сейчас:** `BindSlot(slot, skillId)` — слоты пустые по умолчанию.

**Нужно:** UI для drag-and-drop skill → slot. Упоминался в audit как "Drag-to-slot (skill → slot bar)".

**Статус:** ❌ НЕ в Phase 1. Phase 2: добавить в SkillTreeWindow кнопки "Назначить на слот N".

**MVP:** слоты работают, но без биндов (TryActivate вернёт false для пустого слота). Можно настраивать через `SkillInputService.Instance.BindSlot(...)` из любого скрипта (например, из console через `execute_code`).
