# Keybind Inventory — сводная таблица

> **Дата:** 2026-06-28
> **Источник:** grep по всему проекту (см. `10_CURRENT_STATE.md`)

---

## Занятые клавиши (НЕ ТРОГАТЬ)

| Клавиша | Действие | Контекст | Файл:строка | Реакция на изменение |
|---------|----------|----------|-------------|----------------------|
| **W** | Движение вперёд (пеший) | Walk/Run | NetworkPlayer.cs:577 + ShipInputReader.cs:75 | ЛОМАЕТ |
| **S** | Движение назад (пеший) | Walk/Run | NetworkPlayer.cs:578 + ShipInputReader.cs:76 | ЛОМАЕТ |
| **A** | Движение влево (пеший) | Walk/Run | NetworkPlayer.cs:579 + ShipInputReader.cs:79 | ЛОМАЕТ |
| **D** | Движение вправо (пеший) | Walk/Run | NetworkPlayer.cs:580 + ShipInputReader.cs:80 | ЛОМАЕТ |
| **Space** | Прыжок (пеший) → SubmitJumpRpc | Walk | NetworkPlayer.cs:581-588 | ЛОМАЕТ DEX XP |
| **LShift / RShift** | Бег (пеший) + Boost (корабль) + Meziy модификатор | Walk/Ship | NetworkPlayer.cs:582, 555 + ShipInputReader.cs:69-70 | ЛОМАЕТ |
| **E** | Interact (pickup/chest/market/npc) | Walk | NetworkPlayer.cs:593-616 | ЛОМАЕТ много подсистем |
| **E** (зажата) | Vertical up (корабль) | Ship | NetworkPlayer.cs:552 | ЛОМАЕТ ship movement |
| **F** | Mode switch (board/exit/gather/crafting/door) | Walk | NetworkPlayer.cs:446-509 | ЛОМАЕТ boarding/gathering |
| **T** | CommPanel toggle (только пилот) | Walk/Ship | NetworkPlayer.cs:522-534 | ЛОМАЕТ docking |
| **P** | CharacterWindow toggle | Walk | NetworkPlayer.cs:513-519 | ЛОМАЕТ UI |
| **K** | Primary attack (K-fallback) | Walk | NetworkPlayer.cs:620-629 | ЛОМАЕТ combat |
| **ЛКМ (mouse 0)** | Primary attack + Mouse delta for camera | Walk | NetworkPlayer.cs:633-639 + Camera | ЛОМАЕТ combat + look |
| **MouseY delta** | Pitch (корабль) | Ship | NetworkPlayer.cs:548-549 | ЛОМАЕТ ship |
| **Q** (зажата) | Vertical down (корабль) | Ship | NetworkPlayer.cs:553 | ЛОМАЕТ ship |
| **C, V, Z, X** | Meziy модули | Ship | ShipInputReader.cs:150-153 | ЛОМАЕТ ship modules |
| **Shift+A, Shift+D, Shift+W, Shift+S** | Meziy yaw/thrust | Ship | ShipInputReader.cs:210, 231 | ЛОМАЕТ ship modules |
| **Esc** | Закрытие окон | UI | CharacterWindow.cs:467, SkillTreeWindow.cs:170, CraftingWindow.cs:107, DialogWindow.cs:515, NetworkUI.cs:140, UIManager.cs:85 | ЛОМАЕТ UI |
| **F3** | Ship debug HUD | Debug | ShipDebugHUD.cs:42 | OK (debug-only) |
| **F4** | Meziy status HUD | Debug | MeziyStatusHUD_Legacy.cs:68 | OK (debug-only) |

---

## Свободные клавиши (МОЖНО ИСПОЛЬЗОВАТЬ)

| Клавиша | Сейчас | Зарезервировано для |
|---------|--------|---------------------|
| **ПКМ (mouse 1)** | ничего | **Secondary skill (блок/парирование)** |
| **Ctrl+ЛКМ** | ничего | **Combo skill 3** |
| **Ctrl+ПКМ** | ничего | **Combo skill 4** |
| **Shift+ЛКМ** | ничего (Shift = бег, но LShift.isPressed + ЛКМ — нет обработчика) | **Combo skill 5** |
| **Shift+ПКМ** | ничего | **Combo skill 6** |
| **Q** (не зажата, **tap**) | nothing (только `isPressed` в корабле) | **Skill Slot1 / Tap-Q skill** |
| **R** | ничего | **Skill Slot2 / Reload** |
| **1, 2, 3, 4** | ничего | **Skill Slot1..4 (уже есть в enum)** |
| **Tab** | ничего | **Inventory wheel (TODO from NetworkPlayer.cs:27 comment)** |
| **I, J, L, M, N** | ничего | **Свободны** |
| **Alt (без Ctrl)** | ничего | **Свободен** |
| **CapsLock** | ничего | **Свободен** |

---

## Mapping для боевых навыков (Q-INP-02)

Из 6 биндов на основной набор (ЛКМ / ПКМ / Ctrl+ЛКМ / Ctrl+ПКМ / Shift+ЛКМ / Shift+ПКМ) уже занята только **ЛКМ** (primary attack).

| Slot | Комбинация | Сейчас | Планируется |
|------|-----------|--------|-------------|
| `Primary` | ЛКМ | ✅ Работает (TryActivate→RequestAttackRpc) | Оставить |
| `Secondary` | ПКМ | ❌ не подключен | **Phase 2: подключить через SkillInputService.TryActivate** |
| `Slot1` (или новый) | Ctrl+ЛКМ | ❌ не подключен | **Phase 2: подключить** |
| `Slot2` (или новый) | Ctrl+ПКМ | ❌ не подключен | **Phase 2: подключить** |
| `Slot3` (или новый) | Shift+ЛКМ | ❌ не подключен | **Phase 2: подключить** |
| `Slot4` (или новый) | Shift+ПКМ | ❌ не подключен | **Phase 2: подключить** |

**Вердикт:** добавление 5 новых биндов НЕ конфликтует ни с одной существующей клавишей, кроме Shift+ЛКМ. У Shift+ЛКМ есть нюанс: Shift сейчас зажат во время бега. Нужно различать "бег" (Shift alone, hold) и "Shift+ЛКМ" (down+click в одном фрейме) — решается через `wasPressedThisFrame` для Shift в момент клика ЛКМ.

---

## Дерево зависимостей (что открывает что)

```
CharacterWindow (открывается по P)
  └─ SkillTreeWindow (открывается по клику [ИЗУЧИТЬ НАВЫК] в CharacterWindow)

Inventory (открывается по Tab — TODO, НЕ РЕАЛИЗОВАНО)
  └─ ? (sub-windows)

CraftingWindow (открывается по F при подходе к станции)

CommPanel (открывается по T при пилотировании + OuterCommZone)

DialogWindow (открывается по E при подходе к NPC)

NetworkUI / Main Menu (Esc при отсутствии подключения → toggle disconnect)

UIManager (стек панелей, CloseKey по умолчанию Esc)
```

**Приоритет Esc-handler:**
1. **UIManager** — если зарегистрирован в стеке, закрывает TopPanel
2. **Конкретные окна** — если IsOpen, Hide()
3. **MainMenu (NetworkUI)** — если не подключен, toggle disconnect
4. **DialogWindow** — закрывает диалог

Текущая проблема: каждое окно слушает Esc самостоятельно в `Update()`. Если открыты сразу два (например CharacterWindow И CraftingWindow), Esc может закрыть оба. UIManager решает эту проблему через стек — нужно проверить, все ли окна в него регистрируются.

---

## Где в коде хардкод клавиш, которые игрок захочет настроить

| Клавиша | Где | Действие | Решение |
|---------|-----|----------|---------|
| ЛКМ | NetworkPlayer.cs:633 | Primary attack | **Вынести в InputBindingsConfig (MVP)** |
| ПКМ | — | — | Новое (через InputBindingsConfig) |
| Ctrl+ЛКМ | — | — | Новое (через InputBindingsConfig) |
| K | NetworkPlayer.cs:620 | K-fallback для primary | **Оставить как есть** (для обратной совместимости) ИЛИ **вынести в InputBindingsConfig как альтернативу** |
| 1, 2, 3, 4 | — | Slot1..4 | Новое (через InputBindingsConfig) |
| Q | — | — | Новое (Slot1 в SkillInputService enum) |
| R | — | — | Новое (Slot2) |
| F3, F4 | ShipDebugHUD, MeziyHUD | debug | OK оставить хардкод |
