# Current Input State — карта существующего ввода

> **Дата:** 2026-06-28
> **Метод:** grep по всему проекту `Assets/_Project/`

---

## 1. Сводная таблица всех файлов, читающих ввод

| Файл | Что читает | Стиль | Заметки |
|------|-----------|-------|---------|
| `Scripts/Player/PlayerInputReader.cs` | WASD, Shift, Space, E, F, T, Mouse delta, K, ЛКМ | `Keyboard.current.*` (Input System) | Центральный reader на NetworkPlayer. **Мёртвый код** — events не подписаны нигде кроме legacy. |
| `Scripts/Player/NetworkPlayer.cs:432-647` | F, P, T (пеший+корабль), WASD, Space, Shift, E, K, ЛКМ | `Keyboard.current.*` + `Mouse.current.*` | **Дубликат** PlayerInputReader. Реально работает. |
| `Scripts/Player/ShipController.cs:1530-1554` | legacy helper `IsKeyDown(KeyCode)` | `#if ENABLE_INPUT_SYSTEM` ветка | Только для легаси fallback, реально не используется. |
| `Scripts/Player/ShipInputReader.cs:62-155` | WASD, EQ, Shift, MouseY, CVZX | `Keyboard.current.*` + `Mouse.current.*` | Работает только когда игрок в корабле. |
| `Scripts/Skills/SkillInputService.cs` | — (читает только `System.Func<ulong>` от NetworkPlayer) | ничего не читает сам | Принимает вызовы от NetworkPlayer через `TryActivate(slot)`. |
| `Scripts/Skills/UI/SkillTreeWindow.cs:168-173` | Esc | `Keyboard.current.escapeKey` | Закрывает окно. |
| `Scripts/UI/Client/CharacterWindow.cs:467-481` | Esc | `UnityEngine.InputSystem.Keyboard.current.escapeKey` | Закрывает окно. |
| `Scripts/UI/NetworkUI.cs:140-148` | Esc | `Keyboard.current.escapeKey` | Toggle disconnect button (главное меню). |
| `Scripts/UI/UIManager.cs:85-100` | `CloseKey` (настраиваемый, сейчас Esc) | `KeyCodeToInputKey` + `Keyboard.current[key]` | Закрывает верхнюю панель в стеке. |
| `Scripts/Crafting/UI/CraftingWindow.cs:107-117` | Esc | `Keyboard.current.escapeKey` | Закрывает окно. |
| `Quests/UI/DialogWindow.cs:515-519` | Esc | `UnityEngine.InputSystem.Keyboard.current.escapeKey` | Завершает диалог. |
| `Scripts/Ship/MeziyStatusHUD_Legacy.cs:68-82` | F4 | dual-mode (`#if ENABLE_INPUT_SYSTEM`) | Toggle HUD visibility. |
| `Scripts/Ship/ShipDebugHUD.cs:42-54` | F3 | dual-mode | Toggle debug HUD. |
| `Scripts/Core/TestStormSpawner.cs:26` | `Keyboard.current` | сохраняет в поле | Test-only. |

**Итого: 14 файлов. Из них:**
- **Реально работающих с Input System: 11**
- **Dual-mode (Input System + legacy): 2** (MeziyStatusHUD_Legacy, ShipDebugHUD)
- **Чисто legacy (Input.Get*): 0** — старый менеджер полностью убран

---

## 2. Существующий API SkillInputService

**Файл:** `Assets/_Project/Scripts/Skills/SkillInputService.cs` (238 строк)

### Поля
```csharp
public static SkillInputService Instance { get; private set; }
public System.Func<ulong> TargetFinder;  // NetworkPlayer задаёт свой
[SerializeField] private string _defaultAttackTrigger = "Attack";
```

### Слоты
```csharp
public enum SkillInputSlot : byte {
    None = 0, Primary = 1, Secondary = 2,
    Slot1 = 10, Slot2 = 11, Slot3 = 12, Slot4 = 13,
}
```

### Public API
| Метод | Что делает |
|-------|------------|
| `Initialize(NetworkPlayer, Func<ulong>)` | Owner-only setup. Зовёт NetworkPlayer в OnNetworkSpawn. |
| `TryActivate(SkillInputSlot)` | Главная точка входа. **Returns true** если RPC отправлен. **false** если: slot пуст, нет owner, cooldown, нет target, нет server. Primary/Secondary без бинда → unarmed. |
| `BindSlot(SkillInputSlot, string skillId)` | Привязка навыка к слоту (через CharacterWindow или будущий InputBindingsConfig). |
| `GetSkillForSlot(SkillInputSlot)` → `string` | Получить skillId для слота. |
| `IsSlotBound(SkillInputSlot)` → `bool` | Есть ли привязка. |
| `IsOnCooldown(SkillInputSlot)` → `bool` | Локальный cooldown (для UI отзывчивости). |
| `ClearAllCooldowns()` | Сбросить все кулдауны. |

### Что уже умеет
- ✅ Owner-only guard (`_ownerPlayer == null || !_ownerPlayer.IsSpawned`)
- ✅ Server existence check (`CombatServer.Instance`)
- ✅ Cooldown per-slot (локальный, `Time.unscaledTime + 0.5f`)
- ✅ Target finder delegate
- ✅ Animation trigger на Animator
- ✅ RPC на сервер через `CombatServer.Instance.RequestAttackRpc(targetId, 0UL)`
- ✅ Unarmed для Primary/Secondary без бинда

### Чего НЕ хватает (пробел для миграции)
- ❌ **Нет TriggerForInput()** — не реагирует на саму клавишу, только принимает вызов от NetworkPlayer
- ❌ **Нет маппинга слот → комбинация клавиш** (Primary = ЛКМ? Secondary = ПКМ? А что с Ctrl+ЛКМ?)
- ❌ **Нет InputBindingsConfig** — все бинды хардкодом в NetworkPlayer.Update

---

## 3. Существующий API PlayerInputReader

**Файл:** `Assets/_Project/Scripts/Player/PlayerInputReader.cs` (156 строк)

### Events
| Event | Триггер | Подписан где? |
|-------|---------|---------------|
| `OnMoveInput(Vector2)` | WASD | **НИГДЕ** (reserved for future) |
| `OnJumpPressed()` | Space | **НИГДЕ** |
| `OnRunPressed()` | Shift.down | **НИГДЕ** |
| `OnRunReleased()` | Shift.up | **НИГДЕ** |
| `OnInteractPressed()` | E.down | **НИГДЕ** |
| `OnModeSwitchPressed()` | F.down | **НИГДЕ** |
| `OnMouseDelta(Vector2)` | Mouse delta | **НИГДЕ** |
| `OnCommPanelPressed()` | T.down | **НИГДЕ** |
| `OnAttackPressed()` | ЛКМ ИЛИ K | **НИГДЕ** (как event) |

### Поля
- `MoveInput` (Vector2) — текущий кадр
- `IsRunHeld` (bool)
- `MouseDelta` (Vector2)

### Вердикт
**PlayerInputReader — мёртвый компонент.** Все его events не подписаны, дублирующие чтения клавиш сделаны напрямую в NetworkPlayer.Update.

---

## 4. Все клавиши, читаемые в коде (полный список)

### Пеший режим (NetworkPlayer.Update, _inShip == false)
| Клавиша | Действие | Файл:строка | Guard |
|---------|----------|-------------|-------|
| W/S | Движение вперёд/назад | NetworkPlayer.cs:577-578 | IsOwner |
| A/D | Движение влево/вправо | NetworkPlayer.cs:579-580 | IsOwner |
| Space | Прыжок → SubmitJumpRpc | NetworkPlayer.cs:581, 585-588 | IsOwner |
| LShift | Run (bool флаг, не event) | NetworkPlayer.cs:582 | IsOwner |
| E | Interact (chest/market/npc/repair/meta) | NetworkPlayer.cs:693-717 | IsOwner |
| F | 🥇 PickupItem (подбор), затем ModeSwitch (board/exit/gather/crafting/door) | NetworkPlayer.cs:516-584 | IsOwner + NM ready |
| P | Toggle CharacterWindow | NetworkPlayer.cs:513-519 | IsOwner + NM ready |
| T | CommPanel (только если пилот) | NetworkPlayer.cs:522-534 | IsOwner + NM + piloting |
| K | Primary attack (SkillInputService) | NetworkPlayer.cs:620-629 | IsOwner + NM + CombatServer |
| ЛКМ | Primary attack (parallel к K) | NetworkPlayer.cs:633-639 | IsOwner + NM + CombatServer |

### Корабельный режим (NetworkPlayer.Update, _inShip == true)
| Клавиша | Действие | Файл:строка |
|---------|----------|-------------|
| W/S | Thrust | NetworkPlayer.cs:540-541 |
| A/D | Yaw | NetworkPlayer.cs:544-545 |
| MouseY.delta | Pitch | NetworkPlayer.cs:548-549 |
| E/Q | Vertical | NetworkPlayer.cs:552-553 |
| LShift | Boost | NetworkPlayer.cs:555 |
| E | Reserved (docking/refueling) | NetworkPlayer.cs:568 |

### Корабль отдельно (ShipInputReader)
| Клавиша | Действие | Файл:строка |
|---------|----------|-------------|
| W/S | Thrust | ShipInputReader.cs:75-76 |
| A/D | Yaw | ShipInputReader.cs:79-80 |
| E/Q | Vertical | ShipInputReader.cs:83-84 |
| MouseY.delta | Pitch | ShipInputReader.cs:90 |
| LShift/RShift | Boost | ShipInputReader.cs:69-70 |
| C/V | Meziy pitch up/down | ShipInputReader.cs:150-151 |
| Z/X | Meziy roll left/right | ShipInputReader.cs:152-153 |
| A/W | Meziy yaw left/right | ShipInputReader.cs:154-155 |
| Shift+D | Meziy yaw right | ShipInputReader.cs:210 |
| Shift+S | Meziy thrust bwd | ShipInputReader.cs:231 |

### UI-окна (Esc-handlers)
| Окно | Файл:строка | Действие |
|------|-------------|----------|
| CharacterWindow | Scripts/UI/Client/CharacterWindow.cs:467-474 | Hide() |
| SkillTreeWindow | Scripts/Skills/UI/SkillTreeWindow.cs:170-172 | SetOpen(false) |
| CraftingWindow | Scripts/Crafting/UI/CraftingWindow.cs:107-117 | Close() |
| DialogWindow | Quests/UI/DialogWindow.cs:515-519 | EndConversation() |
| NetworkUI (main menu) | Scripts/UI/NetworkUI.cs:140-148 | Toggle disconnect button |
| UIManager (panel stack) | Scripts/UI/UIManager.cs:85-100 | CloseTopPanel() |

### Debug / Test
| Клавиша | Файл:строка | Действие |
|---------|-------------|----------|
| F3 | Scripts/Ship/ShipDebugHUD.cs:42-54 | Toggle debug HUD |
| F4 | Scripts/Ship/MeziyStatusHUD_Legacy.cs:68-82 | Toggle Meziy HUD |

---

## 5. InputManager.asset (legacy Unity axes)

`ProjectSettings/InputManager.asset` — стандартные Unity axes. **НЕ используются кодом** (везде `Keyboard.current.*`, не `Input.GetAxis`).

| Axis | Default binding |
|------|-----------------|
| Horizontal | right/left + d/a |
| Vertical | up/down + w/s |
| Fire1 | left ctrl + mouse 0 |
| Fire2 | left alt + mouse 1 |
| Fire3 | left shift + mouse 2 |
| Jump | space |
| Mouse X, Mouse Y | (delta) |
| Mouse ScrollWheel | (delta) |
| Submit | return + enter + space |

**Вердикт:** InputManager.asset — dead code, оставить как есть. При полном переходе на Input System файл можно игнорировать.

---

## 6. .inputactions файлы

`find` по проекту: **0 .inputactions файлов** в `Assets/`. Input System подключён как пакет, но никаких `.inputactions` asset'ов не создано. Весь ввод — прямой `Keyboard.current.*` / `Mouse.current.*` (low-level API).

---

## 7. Что НЕ покрыто вводом (потенциальные баги)

| Действие | Сейчас | Риск |
|----------|--------|------|
| Q / R / 1 / 2 / 3 / 4 | Никто не слушает | SkillInputService имеет слоты Slot1..4, но NetworkPlayer.Update их **не триггерит**. Слоты работают только через прямой вызов `SkillInputService.Instance.TryActivate(Slot1)` (Phase 2). |
| Ctrl / Alt | Используются как модификаторы только в скрытом виде (Fire1=Ctrl, Fire2=Alt в InputManager, но не в коде) | Если игрок захочет настроить Ctrl+ЛКМ — захардкожено не будет. |
| Tab | Никто не слушает | Inventory wheel упоминался в комментарии NetworkPlayer.cs:27, но реально Tab не обработан. |
| I, J, L | Никто не слушает | Свободны. |
| X (sheathe weapon) | Упоминалось в audit как TODO | Свободно. |

---

## 8. Потенциальные конфликты

| Конфликт | Где | Сейчас | Решение |
|----------|-----|--------|---------|
| Esc в нескольких окнах одновременно | CharacterWindow, SkillTreeWindow, CraftingWindow, DialogWindow, NetworkUI, UIManager | Каждое окно слушает Esc само. UIManager закрывает верхнее в стеке. | Если окна используют UIManager для регистрации в стеке — UIManager приоритетнее. Проверить регистрацию (Phase 1 research). |
| K и ЛКМ оба → primary attack | NetworkPlayer.cs:620, 633 | Двойной вызов `HandlePrimaryAttackInput()` если зажаты обе | TryActivate сам отбрасывает по cooldown (0.5s). Двойной вызов безвреден. |
| T (CommPanel) и T (SkillTree) | T в NetworkPlayer → CommPanel, но Esc в SkillTree | Нет конфликта (T ≠ Esc). | OK. |
| Ctrl+ЛКМ (новая комбинация) vs Ctrl (Fire1) | InputManager.asset имеет Ctrl как Fire1, но код не читает Fire1 | Безопасно. | OK. |
