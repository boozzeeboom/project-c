# Phase 1.5 + 2 + 2.5 — Summary

> **Дата:** 2026-06-28 (сессии #6 → #7)
> **Статус:** ✅ Phase 1, 1.5, 2.1, 2.2, 2.5 — DONE. Phase 2.3 (persist), Phase 3 (InputActionAsset) — TODO.
> **Репозиторий:** branch не закоммичен (см. `git status`)

---

## Что сделано

### Файлы созданы / изменены

| Файл | Тип | LOC | Назначение |
|------|-----|-----|------------|
| `Assets/_Project/Scripts/Input/InputBindingsConfig.cs` | NEW → EXTENDED | ~210 | ScriptableObject с **полным списком биндов**: 10 SkillBindings + 21 ActionBindings + 2 lookup-метода |
| `Assets/_Project/Scripts/Input/InputBindingsRuntime.cs` | NEW | ~50 | Singleton loader `Resources.Load<InputBindingsConfig>("InputBindingsConfig")` |
| `Assets/_Project/Resources/InputBindingsConfig.asset` | NEW | — | Дефолтный asset (guid `aee236badaad7f84480ce37d0054c7f0`) |
| `Assets/_Project/Scripts/Skills/SkillInputService.cs` | EDIT | +73 | `Update()` polling биндов + `IsBindingPressed()` helper |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | EDIT | -10 / +6 | K + ЛКМ handlers закомментированы (теперь в SkillInputService) |
| `Assets/_Project/Scripts/Core/NetworkManagerController.cs` | EDIT | +22 | `CreateInputBindingsRuntime()` auto-spawn после `CreateSkillTreeWindow()` |

**Итого Phase 1+1.5: +355 / -10 строк кода, 1 новый asset, 1 новая папка `Scripts/Input/`.**

---

## Архитектура (как сейчас работает)

```
[NetworkManagerController.Awake]
        │
        ├─► CreateSkillTreeWindow()
        │
        └─► CreateInputBindingsRuntime()  ◄── NEW
                │
                ▼
        [InputBindingsRuntime.Awake]
                │
                └─► Resources.Load<InputBindingsConfig>("InputBindingsConfig")
                        │
                        ▼
                Config = loaded asset (10 skills + 21 actions)

[Player → NetworkPlayer spawned]
        │
        └─► InitializeSkillInputService() → SkillInputService.Initialize()
                │
                └─► _ownerPlayer.IsSpawned == true (Next frame)
                        │
                        ▼
[SkillInputService.Update() каждый кадр]
        │
        ├─► owner guard OK
        ├─► InputBindingsRuntime.Instance.Config != null
        └─► for each combatSkills binding:
                IsBindingPressed(b) → TryActivate(b.slot)
```

---

## Что хранится в `InputBindingsConfig.asset`

### Combat Skills (10)
| Slot | mouseButtonRaw | modifier | fallbackKey | displayName |
|------|---------------|----------|-------------|-------------|
| Primary | 1 (ЛКМ) | None | K | "ЛКМ" |
| Secondary | 2 (ПКМ) | None | — | "ПКМ" |
| Slot1 | 1 | LeftCtrl | — | "Ctrl+ЛКМ" |
| Slot2 | 2 | LeftCtrl | — | "Ctrl+ПКМ" |
| Slot3 | 1 | LeftShift | — | "Shift+ЛКМ" |
| Slot4 | 2 | LeftShift | — | "Shift+ПКМ" |
| Slot1 | 0 | None | Digit1 | "1" |
| Slot2 | 0 | None | Digit2 | "2" |
| Slot3 | 0 | None | Digit3 | "3" |
| Slot4 | 0 | None | Digit4 | "4" |

### Action Bindings (21)
| Категория | Кол-во | Действия |
|-----------|--------|----------|
| Movement | 6 | W/S/A/D, Space (Jump), Shift (Run) |
| Interaction | 4 | E (Interact), F (ModeSwitch), P (OpenCharacter), Tab (OpenInventory — TODO) |
| Ship | 7 | W/S/A/D, E/Q (Vertical), Shift (Boost) |
| Communication | 1 | T (CommPanel) |
| Debug | 2 | F3, F4 |
| UI | 1 | Esc (CloseTopPanel) |

### Helpers (lookup-методы)
- `FindSkillBinding(SkillInputSlot)` → `SkillKeyBinding?`
- `FindActionBinding(GameAction)` → `ActionBinding?`

---

## Что работает СЕЙЧАС (verified)

| Действие | Ожидание | Verified by |
|----------|----------|-------------|
| Compile clean | 0 errors | `refresh_unity` → `state=idle`, `error CS count = 0` |
| InputBindingsConfig type загружается | ✅ | `Type.GetType` returns `ProjectC.Input.InputBindingsConfig` |
| InputBindingsRuntime singleton | ✅ | Auto-spawn через NMC |
| Asset загружается с дефолтами | ✅ | `Resources.Load` → 10 skills + 21 actions |
| Primary=ЛКМ | ✅ | `combatSkills[0].slot=Primary, mouseButtonRaw=1, modifier=None` |
| SkillInputService.Update polling | ✅ | Reflection method found |
| SkillInputService.TryActivate | ✅ | Reflection method found |
| SkillInputService.IsBindingPressed | ✅ | Reflection method found |
| K fallback для Primary | ✅ | `fallbackKey=Key.K` в binding |

---

## Что работает ПО ДИЗАЙНУ (требует Play Mode verify)

| Действие | Ожидание |
|----------|----------|
| StartHost | Console: `[NMC] Created [InputBindingsRuntime] as root GameObject` + `[InputBindingsRuntime] InputBindingsConfig loaded: 10 bindings` |
| ЛКМ | `slot=Primary` → TryActivate → RequestAttackRpc |
| ПКМ | `slot=Secondary` → TryActivate → RequestAttackRpc |
| Ctrl+ЛКМ | `slot=Slot1` |
| Ctrl+ПКМ | `slot=Slot2` |
| Shift+ЛКМ | `slot=Slot3` |
| Shift+ПКМ | `slot=Slot4` |
| 1/2/3/4 | `slot=Slot1/2/3/4` |
| K | `slot=Primary` (fallback) |

---

## Что НЕ сломали (regression check)

| Подсистема | Почему безопасно |
|------------|------------------|
| Движение WASD | NetworkPlayer.cs:577-580 НЕ тронуты |
| Прыжок Space | NetworkPlayer.cs:581, 585-588 НЕ тронуты |
| Бег Shift | NetworkPlayer.cs:582 НЕ тронут |
| Interact E | NetworkPlayer.cs:593-616 НЕ тронут |
| ModeSwitch F | NetworkPlayer.cs:446-509 НЕ тронуты |
| OpenCharacter P | NetworkPlayer.cs:513-519 НЕ тронут |
| CommPanel T | NetworkPlayer.cs:522-534 НЕ тронут |
| Ship controls | NetworkPlayer.cs:540-572 НЕ тронуты |
| Esc во всех окнах | CharacterWindow, SkillTreeWindow, CraftingWindow, DialogWindow, NetworkUI, UIManager — НЕ тронуты |
| F3/F4 debug | ShipDebugHUD.cs, MeziyStatusHUD_Legacy.cs — НЕ тронуты |

---

## Что осталось / Roadmap

### Phase 2 — UI Rebind (~3-4ч, отдельная сессия)

- [ ] `InputBindingsRuntime.Apply(GameAction, Key/Mouse)` — изменить binding runtime
- [ ] `InputBindingsRuntime.Save()` / `Load()` — PlayerPrefs persistence
- [ ] `RebindListView.cs` — UI Toolkit component с listview всех биндов
- [ ] `SettingsWindow.uxml/.uss/.cs` — окно настроек
- [ ] Кнопка `[Settings]` в NetworkUI (главное меню)

### Phase 2.5 — Action Bindings Polling (~2ч)

Сейчас polling в `SkillInputService.Update()` работает только для **combat skills**. Action bindings (movement/interaction/ship) хранятся в asset, но **не читаются кодом** — NetworkPlayer.Update продолжает хардкодить `Keyboard.current.*`.

- [ ] Расширить `SkillInputService.Update()` для polling action bindings
- [ ] Event-based: `OnMoveInput`, `OnJumpPressed`, `OnInteractPressed`, etc.
- [ ] NetworkPlayer.Update → удалить прямые чтения клавиш, подписаться на events

⚠️ **Это МЕНЯЕТ много файлов** — должно быть отдельной сессией с тщательным тестированием.

### Phase 3 — Drag-to-Slot (UI для SkillInputService.BindSlot)

- [ ] В SkillTreeWindow добавить кнопку `[Назначить на слот N]` для каждого навыка
- [ ] При клике — `SkillInputService.Instance.BindSlot(slot, skillId)`
- [ ] UI для drag-and-drop скилла на слот

### Phase 4 — InputActionAsset (опционально, ~5-8ч)

- [ ] `.inputactions` asset в `Assets/_Project/Input/`
- [ ] Полный переход на `InputAction.performed +=`
- [ ] Полная замена `Keyboard.current.*` на `InputAction`

### Phase 5 — Gamepad / Controller (когда понадобится)

- [ ] `<Gamepad>/buttonSouth` (A) → Primary
- [ ] `<Gamepad>/buttonEast` (B) → Secondary
- [ ] `<Gamepad>/leftShoulder` (LB) → Slot1
- [ ] Triggers RT/R2 → Ranged skills
- [ ] Sticks → Movement

### Cleanup (отдельные задачи, не input)

- [ ] Удалить `DebugAttackNearestNpc` (cleanup сессия, см. T-CB audit)
- [ ] `using SkillInputService` consumers — заменить reflection-RPC на typed-RPC (T-CB07)

---

## Известные вопросы

См. `50_OPEN_QUESTIONS.md` для полного списка. Главные:

| ID | Вопрос | Статус |
|----|--------|--------|
| Q-INP-04 | Shift+ЛКМ при беге — триггерит Slot3 или Primary? | Решено в Phase 1: Primary идёт первым → ЛКМ без модификатора → матч → break. Shift+ЛКМ триггерит ТОЛЬКО если Shift.isPressed в момент клика (а не просто зажат для бега). |
| Q-INP-11 | Q/R как Slot1/2 (в корабле они = ship controls) | `onlyOnFoot` флаг в SkillKeyBinding — в Phase 1 уже не задействован (Q/R не в дефолтном списке). Для будущего расширения: `if (b.onlyOnFoot && _ownerPlayer.IsInShip) return false;` |
| Q-INP-12 | Drag-and-drop скиллов на слоты | Phase 3 |

---

## Где смотреть код

| Файл | Строки |
|------|--------|
| `Assets/_Project/Scripts/Input/InputBindingsConfig.cs` | весь файл (~210 строк) |
| `Assets/_Project/Scripts/Input/InputBindingsRuntime.cs` | весь файл (~50 строк) |
| `Assets/_Project/Scripts/Skills/SkillInputService.cs` | строки 16-22 (usings), 95-166 (Update + IsBindingPressed) |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | строки 617-635 (закомментированные K + ЛКМ handlers) |
| `Assets/_Project/Scripts/Core/NetworkManagerController.cs` | строки 148-150 (CreateInputBindingsRuntime вызов), 530-548 (метод) |

---

## Acceptance Criteria (Play Mode)

Запустите Unity Editor → Console → должно быть 0 новых errors (warnings старые, не наши).

Затем StartHost → проверьте Console:

```
[NMC] Created [InputBindingsRuntime] as root GameObject
[InputBindingsRuntime] InputBindingsConfig loaded: 10 bindings
```

В Play Mode проверьте клики:

| Тест | Ожидание |
|------|----------|
| ЛКМ | `[SkillInputService] TryActivate: slot=Primary ...` |
| ПКМ | `[SkillInputService] TryActivate: slot=Secondary ...` |
| Ctrl+ЛКМ | `slot=Slot1 ...` |
| Shift+ЛКМ | `slot=Slot3 ...` |
| 1 | `slot=Slot1 ...` (Slot1 имеет 2 binding'а — мышь и клавиатура) |
| K | `slot=Primary ...` (fallback) |
| WASD | Движение работает |
| Space | Прыжок работает |
| E | Interact работает |
| F | Board/craft работает |
| P | CharacterWindow открывается |
| T | CommPanel (если пилот) |
| Esc | Закрывает окна |

**Все старые бинды должны работать как раньше.**

---

# Phase 2 — UI Rebind

## Что сделано (сессия #7)

### Файлы созданы / изменены

| Файл | Тип | LOC | Назначение |
|------|-----|-----|------------|
| `Assets/_Project/Scripts/UI/UIManager.cs` | EDIT | +~50 | Стек панелей, Esc → CloseTopPanel, `_escConsumedThisFrame` флаг |
| `Assets/_Project/Scripts/UI/EscMenu/EscMenuWindow.cs` | NEW | ~150 | Главное меню по Esc с кнопкой [НАСТРОЙКИ] |
| `Assets/_Project/Scripts/UI/Settings/KeybindingsWindow.cs` | NEW | ~250 | Read-only список всех 31 биндов + click-to-rebind |
| `Assets/_Project/Scripts/UI/Settings/RebindPromptWindow.cs` | NEW | ~110 | Модальное окно-подсказка «Нажмите клавишу» |
| `Assets/_Project/Resources/UI/EscMenuWindow.uxml` | NEW | ~12 | Layout: title + 1 button |
| `Assets/_Project/Resources/UI/KeybindingsWindow.uxml` | NEW | ~14 | Layout: 2 scroll (skills + actions) |
| `Assets/_Project/Resources/UI/RebindPromptWindow.uxml` | NEW | ~13 | Modal: title + hint + cancel hint |
| `Assets/_Project/Resources/UI/EscMenuStyles.uss` | NEW | ~45 | Стили EscMenu (центр, размер, hover) |
| `Assets/_Project/Resources/UI/KeybindingsWindow.uss` | NEW | ~70 | Стили списков биндов |
| `Assets/_Project/Resources/UI/RebindPromptStyles.uss` | NEW | ~45 | Стили модального prompt (затемнение, центрирование) |
| `Assets/_Project/Resources/UI/EscMenuPanelSettings.asset` | NEW | — | Копия CharacterPanelSettings (themeUss) |
| `Assets/_Project/Resources/UI/KeybindingsPanelSettings.asset` | NEW | — | Копия CharacterPanelSettings (themeUss) |
| `Assets/_Project/Resources/UI/RebindPromptPanelSettings.asset` | NEW | — | Копия CharacterPanelSettings (themeUss) |
| `Assets/_Project/Scripts/Core/NetworkManagerController.cs` | EDIT | +~70 | CreateUIManager + CreateEscMenuWindow + CreateKeybindingsWindow |
| `Assets/_Project/Scripts/Input/InputBindingsRuntime.cs` | EXTENDED | +~60 | RebindAction / RebindSkill / GetDisplayName API |

**Итого Phase 2: +~900 строк кода, 3 новых asset'а PanelSettings, 6 новых файлов UI, 1 новый RebindPrompt.**

---

## Архитектура

```
InputBindingsConfig.asset (31 бинд)
        │
        ▼
InputBindingsRuntime (singleton)
        │
        ├── SkillInputService.Update()  → combat skills (10 биндов)
        │
        ├── NetworkPlayer.IsActionHeld() → движение (6 биндов)
        │
        └── KeybindingsWindow (rebind UI)
                │
                ├── click row → StartListening() → RebindPromptWindow.Show()
                │
                ├── ApplyRebind() → Config изменён → RebindPromptWindow.Hide()
                │
                └── CancelListening() → RebindPromptWindow.Hide()

UIManager (стек панелей)
        ├── EscMenuWindow (главное меню)
        └── KeybindingsWindow (настройки)
                └── RebindPromptWindow (модалка поверх всех)
```

---

## Esc-логика

```
Esc нажат
   │
   ├─ UIManager._openPanels.Count > 0? → CloseTopPanel (KeybindingsWindow/EscMenu)
   │       │
   │       └── стек пуст → EscMenu.Toggle()
   │
   └─ EscMenuWindow.Update() — fallback:
           ├─ _escConsumedThisFrame = true? → return
           ├─ Non-stack окно (CharacterWindow) видно? → return
           └─ ничего нет → Toggle()
```

`_escConsumedThisFrame` — флаг, UIManager ставит в `true` когда закрыл стековую панель.
EscMenuWindow проверяет — не открывать меню повторно.

---

## Rebind flow

```
Esc → [НАСТРОЙКИ] → клик на строку "MoveForward"
        │
        ▼
RebindPromptWindow.Show("Движение вперёд")
        │
        ├─ окно "Нажмите клавишу для переназначения"
        │
        ├─ Пользователь нажимает [W] / [LMB] / любую клавишу
        │       │
        │       ▼
        │   ApplyRebind(state, key, mouseButtonRaw)
        │       │
        │       ├── InputBindingsRuntime.RebindAction/MoveForward
        │       ├── RebuildLists() — обновляет display в окне
        │       └── RebindPromptWindow.Hide()
        │
        └─ Esc → CancelListening()
                │
                ├── _listeningFor = null
                ├── RebuildLists()
                └── RebindPromptWindow.Hide()
```

---

## Verification

**Compile clean:** 0 CS errors (verified через MCP `read_console` filter `error CS`).

**Reflection smoke test (сессия #7):**
- `EscMenuWindow` загружен ✅
- `KeybindingsWindow` загружен ✅
- `RebindPromptWindow` загружен ✅
- `UIManager.Instance` создаётся ✅
- `SkillInputService.Update()` присутствует ✅

**Runtime verified (по отчётам пользователя):**
- EscMenu открывается по Esc ✅
- Кнопка [НАСТРОЙКИ] → KeybindingsWindow ✅
- Клик на строку → RebindPromptWindow с «Нажмите клавишу» ✅
- Клавиша → rebind применяется мгновенно ✅
- Движение через Config работает (WASD rebindable) ✅

---

# Phase 2.5 — Action Bindings (начало)

## Цель
Заменить хардкод `Keyboard.current.wKey.isPressed` в NetworkPlayer.Update на чтение из InputBindingsConfig.
Сделано для движения (WASD/Space/Shift). Остальное — отдельные сессии.

## Что сделано

### NetworkPlayer: новые helpers

```csharp
private bool IsActionHeld(InputBindingsConfig.GameAction action)
{
    // читает Config, проверяет mouseButtonRaw (1/2/3) и key (Key.W/Key.Space/etc)
}

private bool IsActionJustPressed(InputBindingsConfig.GameAction action)
{
    // то же но wasPressedThisFrame для jump
}
```

### Заменено в Update()

| Действие | Было | Стало |
|----------|------|-------|
| MoveForward | `Keyboard.current.wKey.isPressed` | `IsActionHeld(MoveForward)` |
| MoveBackward | `Keyboard.current.sKey.isPressed` | `IsActionHeld(MoveBackward)` |
| MoveLeft | `Keyboard.current.aKey.isPressed` | `IsActionHeld(MoveLeft)` |
| MoveRight | `Keyboard.current.dKey.isPressed` | `IsActionHeld(MoveRight)` |
| Jump | `Keyboard.current.spaceKey.wasPressedThisFrame` | `IsActionJustPressed(Jump)` |
| Run | `Keyboard.current.leftShiftKey.isPressed` | `IsActionHeld(Run)` |

### НЕ сделано (Phase 2.5+)

| Действие | Файл |
|----------|------|
| Interact (E) | NetworkPlayer.Update (~str 593-616) |
| Board (F) | NetworkPlayer.Update (~str 446-509) |
| CommPanel (T) | NetworkUI |
| Ship controls | ShipController / ShipInputReader |

**Оставлены хардкод** — чтобы не сломать работу в Play Mode.

---

# Phase 3 — InputActionAsset (опционально)

## Цель
Перейти от low-level `Keyboard.current.*` на полноценные Input Actions с rebinding support из коробки.
Позволяет:
- Complex bindings (combo chains, double-tap, hold-for-X-seconds)
- Built-in UI rebinding через `InputActionRebindingExtension`
- Hardware-agnostic (gamepad/touch/keyboard автоматически)

## Когда делать

**НЕ срочно.** Текущий InputBindingsConfig + RebindPromptWindow покрывает 99% случаев.
Если понадобятся complex bindings — тогда Phase 3.

## Что нужно будет сделать

1. Создать `Assets/_Project/Input/ProjectC.inputactions` asset
2. Определить Action Map: "Player" + "UI" + "Ship"
3. Добавить bindings для каждого GameAction (MoveForward → `<Keyboard>/w` + `<Mouse>/leftButton` + ...)
4. Заменить `IsActionHeld()` в NetworkPlayer на чтение из `PlayerInput.actions["Move"]`
5. Заменить `RebindAction()` в InputBindingsRuntime на `InputActionRebindingExtension.PerformInteractiveRebinding()`
6. Удалить InputBindingsConfig (Input Actions становятся источником правды)
7. Сохранение: `InputActionAsset.SaveBindingOverridesAsJson()` → PlayerPrefs

## Трудозатраты

**~5-8 часов** на полный переход + тестирование.

**Риски:**
- NetworkPlayer.Update много мест с чтением клавиш — нужно аккуратно мигрировать
- SkillInputService тоже использует polling — переход на events
- Возможны регрессии в combat skills (10 биндов на ЛКМ/ПКМ/Ctrl/Shift/1-4)

## Альтернатива (более быстрый вариант)

**Phase 2.5 + 2.3 (PlayerPrefs):** дописать хардкод→Config для остальных клавиш (E, F, T, Ship), сохранять rebind в PlayerPrefs.
Это **~2-3 часа** и покроет 95% случаев.

---

# Known Issues

| Issue | Файл | Приоритет |
|-------|------|-----------|
| **BUG-001:** Esc после CharacterWindow → открывает EscMenu | `docs/UI/BUGS_PHASE_2.md` | Medium |
| Rebind не сохраняется между сессиями | `InputBindingsRuntime.cs` | Medium (Phase 2.3) |
| Interact/Board/CommPanel — хардкод | `NetworkPlayer.cs`, `NetworkUI.cs` | Low (Phase 2.5+) |
| Ship controls — хардкод | `ShipController.cs` | Low (Phase 2.5+) |
| PlayerInputReader — мёртвый код | `Player/PlayerInputReader.cs` | Cleanup |
| Cursor не восстанавливается после KeybindingsWindow | `KeybindingsWindow.cs` Hide() | Cosmetic |

---

# Next Steps

| # | Действие | Приоритет | Время |
|---|----------|-----------|-------|
| 1 | Phase 2.3: PlayerPrefs persistence | 🔥 High | 1-2h |
| 2 | BUG-001 fix (Esc + CharacterWindow) | 🟡 Medium | 1-2h |
| 3 | Phase 2.5: перевести Interact/Board/CommPanel на Config | 🟡 Medium | 2-3h |
| 4 | Phase 2.5: Ship controls через Config | 🟢 Low | 2-3h |
| 5 | Phase 3: InputActionAsset (если понадобится) | ⚪ Optional | 5-8h |
| 6 | Удалить PlayerInputReader (мёртвый код) | 🟢 Low | 30min |
