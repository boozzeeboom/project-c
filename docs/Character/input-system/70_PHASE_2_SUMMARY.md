# Phase 2 — Summary (сессия #7, 2026-06-28)

> **Статус:** ✅ Phase 1 + 2.1 + 2.2 + 2.5 (начало) — реализованы
> **Результат:** полноценное меню настроек клавиш с runtime rebind, движение через конфиг

---

## Что сделано

### Phase 1 — InputBindingsConfig + боевые навыки

| Компонент | Файлы | Статус |
|-----------|-------|--------|
| `InputBindingsConfig` SO | `Scripts/Input/InputBindingsConfig.cs` (210 строк) | ✅ |
| `InputBindingsRuntime` singleton | `Scripts/Input/InputBindingsRuntime.cs` (110 строк) | ✅ |
| Дефолтный `.asset` | `Resources/InputBindingsConfig.asset` | ✅ |
| `SkillInputService.Update()` polling | `Scripts/Skills/SkillInputService.cs` (+73 строки) | ✅ |

**10 биндов:** ЛКМ, ПКМ, Ctrl+ЛКМ, Ctrl+ПКМ, Shift+ЛКМ, Shift+ПКМ, 1, 2, 3, 4.

### Phase 2.1 — EscMenu + UIManager

| Компонент | Файлы | Статус |
|-----------|-------|--------|
| EscMenuWindow | `Scripts/UI/EscMenu/` + `Resources/UI/EscMenuWindow.*` | ✅ |
| KeybindingsWindow (read-only) | `Scripts/UI/Settings/` + `Resources/UI/KeybindingsWindow.*` | ✅ |
| UIManager (стек панелей) | `Scripts/UI/UIManager.cs` | ✅ |
| Auto-spawn через NMC | `Scripts/Core/NetworkManagerController.cs` | ✅ |
| USS стили | `Resources/UI/EscMenuWindow.uss` → переименован (баг фикс) | ✅ |

### Phase 2.2 — Runtime Rebind

| Компонент | Статус |
|-----------|--------|
| Click-to-rebind (клик → слушаем → нажатие → обновление) | ✅ |
| `InputBindingsRuntime.RebindAction(GameAction, Key, mouseButton)` | ✅ |
| `InputBindingsRuntime.RebindSkill(SkillInputSlot, mouseButton, modifier, fallbackKey)` | ✅ |
| Esc отмена rebind | ✅ |
| Rebind сразу применяется | ✅ |
| PlayerPrefs persistence | ❌ Phase 2.3 |

### Phase 2.5 — Action Bindings (начало)

| Действие | Было | Стало |
|----------|------|-------|
| MoveForward (W) | `Keyboard.current.wKey.isPressed` | `IsActionHeld(MoveForward)` |
| MoveBackward (S) | `Keyboard.current.sKey.isPressed` | `IsActionHeld(MoveBackward)` |
| MoveLeft (A) | `Keyboard.current.aKey.isPressed` | `IsActionHeld(MoveLeft)` |
| MoveRight (D) | `Keyboard.current.dKey.isPressed` | `IsActionHeld(MoveRight)` |
| Jump (Space) | `Keyboard.current.spaceKey.wasPressedThisFrame` | `IsActionJustPressed(Jump)` |
| Run (Shift) | `Keyboard.current.leftShiftKey.isPressed` | `IsActionHeld(Run)` |

**Методы:** `IsActionHeld(GameAction)` / `IsActionJustPressed(GameAction)` в `NetworkPlayer.cs`.

### Созданные файлы

```
Assets/_Project/Scripts/
├── Input/
│   ├── InputBindingsConfig.cs     (210 строк)
│   └── InputBindingsRuntime.cs    (110 строк)
├── UI/
│   ├── EscMenu/
│   │   └── EscMenuWindow.cs       (150 строк)
│   ├── Settings/
│   │   └── KeybindingsWindow.cs   (250 строк)
│   └── UIManager.cs               (120 строк)
├── Skills/
│   └── SkillInputService.cs       (+73 строки, Update + IsBindingPressed)
└── Player/
    └── NetworkPlayer.cs           (+80 строк, IsActionHeld/JustPressed)

Assets/_Project/Resources/UI/
├── EscMenuWindow.uxml / EscMenuWindow.uss
├── KeybindingsWindow.uxml / KeybindingsWindow.uss
├── EscMenuPanelSettings.asset
└── KeybindingsPanelSettings.asset
```

---

## Известные проблемы

| Bug | Статус |
|-----|--------|
| **BUG-001:** Esc после CharacterWindow → открывает EscMenu | ❌ В `docs/UI/BUGS_PHASE_2.md` |
| Rebind не сохраняется между сессиями | ❌ Phase 2.3 |
| Ship controls хардкод (WASD/EQ/Shift) | ❌ не тронуто |
| Interact (E), Board (F), CommPanel (T) хардкод | ❌ не тронуто |
| PlayerInputReader мёртвый (events не подписаны) | ❌ не тронуто |

---

## Roadmap

| Фаза | Что | Приоритет |
|------|-----|-----------|
| 2.3 | PlayerPrefs persistence (сохранение rebind'ов) | Средний |
| 2.5 завершение | Ship, E, F, T, F3/F4 через конфиг | Низкий |
| 3 | InputActionAsset (полный переход) | Опционально |
| — | Исправить BUG-001 (Esc + CharacterWindow) | Средний |
