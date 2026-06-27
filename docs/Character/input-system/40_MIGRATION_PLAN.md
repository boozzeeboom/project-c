# Migration Plan — безопасный переход

> **Дата:** 2026-06-28
> **Гарантия:** ни одна существующая подсистема не ломается. Старые клавиши (бег, посадка, крафт, инвентарь, диалоги) работают как и раньше.

---

## Принципы

1. **ADDITIVE-ONLY.** Не удаляем, не переименовываем, не рефакторим. Только добавляем новые файлы и добавляем методы.
2. **Не трогаем NetworkPlayer.Update.** Оставляем WASD/Space/Shift/E/F/P/T/K/ЛКМ как есть. Primary attack работает через старый путь.
3. **Не трогаем PlayerInputReader.** Мёртвый код, оставить на случай будущей интеграции.
4. **Не трогаем UI-окна.** CharacterWindow, SkillTreeWindow, CraftingWindow, DialogWindow, NetworkUI, UIManager — все Esc-handlers остаются как есть.
5. **SkillInputService становится "smart".** Получает `Update()`, polling биндов. TryActivate уже есть, не трогаем.
6. **MVP — дефолты через инспектор.** UI rebind — Phase 2.

---

## Фаза 1 — MVP (1 сессия, ~2-3ч)

### Цель
Включить 6 биндов боевых навыков (ЛКМ, ПКМ, Ctrl+ЛКМ, Ctrl+ПКМ, Shift+ЛКМ, Shift+ПКМ) через InputBindingsConfig.

### Шаги

| # | Действие | Файл | Строки | Проверка |
|---|----------|------|--------|----------|
| 1 | Создать `InputBindingsConfig.cs` | NEW `Assets/_Project/Scripts/Input/InputBindingsConfig.cs` | ~50 | Compile clean |
| 2 | Создать `.asset` через меню или `manage_scriptable_object` | NEW `Assets/_Project/Resources/InputBindingsConfig.asset` | — | Inspector виден |
| 3 | Создать `InputBindingsRuntime.cs` (singleton loader) | NEW `Assets/_Project/Scripts/Input/InputBindingsRuntime.cs` | ~30 | Resources.Load работает |
| 4 | Добавить `Update()` в SkillInputService + `IsBindingPressed()` | EDIT `SkillInputService.cs` | +30 | Compile clean |
| 5 | **УБРАТЬ** ЛКМ-обработчик из NetworkPlayer.Update (НО оставить K как fallback через `TryActivate(SkillInputSlot.Primary)`) | EDIT `NetworkPlayer.cs:633-639` | -8 | K-fallback работает |
| 6 | **УБРАТЬ** K-обработчик из NetworkPlayer.Update (теперь в SkillInputService через `fallbackKey=K`) | EDIT `NetworkPlayer.cs:620-629` | -10 | ЛКМ работает |
| 7 | Compile + Play Mode verify | — | — | ЛКМ → primary, ПКМ → secondary, Ctrl+ЛКМ → Slot1, etc. |

### Что остаётся неизменным
- WASD/Space/Shift/E/F/P/T — без изменений в NetworkPlayer.Update.
- Все Esc-handlers.
- PlayerInputReader (мёртвый, оставить).
- Все UI-окна.
- Debug F3/F4 HUD.
- ShipController, ShipInputReader.

### Что добавляется
- 2 новых файла: `InputBindingsConfig.cs`, `InputBindingsRuntime.cs`.
- 1 новый asset: `InputBindingsConfig.asset`.
- `Update()` метод в `SkillInputService.cs` (29 строк).
- Удаление ~18 строк из `NetworkPlayer.cs` (ЛКМ и K handlers — теперь их работу делает SkillInputService.Update).

---

## Фаза 2 — UI Rebind (~3-4ч, отдельная сессия)

### Цель
Дать игроку меню настроек: Settings UI → нажал "Изменить" → нажал клавишу → bind сохранён.

### Шаги

| # | Действие | Файл | Строки |
|---|----------|------|--------|
| 1 | `InputBindingsRuntime.Apply()` — применить изменение | NEW `Assets/_Project/Scripts/Input/InputBindingsRuntime.cs` | +20 |
| 2 | `InputBindingsRuntime.Save()` / `Load()` — PlayerPrefs | EDIT тот же файл | +30 |
| 3 | `RebindListView.cs` — UI Toolkit component | NEW `Assets/_Project/Scripts/UI/Settings/RebindListView.cs` | ~80 |
| 4 | `SettingsWindow.uxml/.uss/.cs` | NEW (по образцу CharacterWindow) | ~150 |
| 5 | Кнопка `[Настройки]` в NetworkUI (главное меню) | EDIT `NetworkUI.cs` | +10 |
| 6 | Compile + Play Mode verify | — | — |

---

## Фаза 3 — Полный InputActionAsset (опционально, ~5-8ч)

### Цель
Перейти от low-level `Keyboard.current` на полноценные Input Actions с rebinding support из коробки.

### Когда делать
- Только если UI rebind из Phase 2 не справится.
- Только если понадобятся complex bindings (combo chains, double-tap, hold-for-X-seconds).

### Что нужно
- Создать `.inputactions` asset в `Assets/_Project/Input/`.
- Переписать PlayerInputReader и SkillInputService на `InputAction.performed +=`.
- Заменить все `Keyboard.current.*.wasPressedThisFrame` на `InputAction`.

### Почему НЕ делать сейчас
- Текущий код использует `Keyboard.current` напрямую — работает, читаемо.
- InputActionAsset добавляет слой абстракции, который нужно настраивать через Unity Editor GUI (`.meta` overhead).
- Прямой API проще отлаживать (видно в коде что нажато).

---

## Acceptance Criteria (Phase 1)

| Тест | Ожидание |
|------|----------|
| Compile | 0 errors |
| StartHost | `SkillInputService` логирует `InputBindingsConfig loaded: 6 bindings` |
| ЛКМ | Primary attack срабатывает (Console: `slot=Primary skill='' target=X trigger='Attack'`) |
| ПКМ | Secondary attack срабатывает (slot=Secondary) |
| Ctrl+ЛКМ | Slot1 срабатывает (slot=Slot1) |
| Ctrl+ПКМ | Slot2 срабатывает (slot=Slot2) |
| Shift+ЛКМ | Slot3 срабатывает (slot=Slot3, не Slot1) |
| Shift+ПКМ | Slot4 срабатывает (slot=Slot4) |
| K | Primary attack (fallback) — НЕ срабатывает (handler удалён) |
| Бег (Shift зажат, без клика) | Shift+ЛКМ НЕ срабатывает (Shift не wasPressed в этом кадре) |
| Движение WASD | Работает как раньше |
| Прыжок Space | Работает как раньше |
| Посадка F | Работает как раньше |
| CommPanel T | Работает как раньше |
| CharacterWindow P | Работает как раньше |
| Esc | Закрывает все окна как раньше |

---

## Что НЕ делать (явные НЕТ)

- ❌ Менять WASD/Shift/Space/E/F/P/T handlers в NetworkPlayer.Update.
- ❌ Менять Esc handlers в любом UI-окне.
- ❌ Удалять PlayerInputReader.cs.
- ❌ Переписывать SkillInputService.TryActivate (он работает).
- ❌ Переписывать NetworkPlayer.HandlePrimaryAttackInput (он останется как fallback-метод).
- ❌ Создавать InputActionAsset (Phase 3).
- ❌ Добавлять UI-меню настроек (Phase 2).
- ❌ Удалять `DebugAttackNearestNpc` (cleanup отдельная задача, не input).
