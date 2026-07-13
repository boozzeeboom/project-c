# Этап 3d — Лог реализации (2026-07-13)

> **Статус:** ✅ Завершён

---

## KeybindingsWindow как sub-page

**Изменения в `KeybindingsWindow.cs`:**

1. `OnBackRequested` (Action) — callback для Esc в embedded-режиме
2. `_isEmbedded` — флаг режима встраивания
3. `GetPageRoot()` — снимает `position: absolute` + фиксированные размеры, возвращает `_root` как flex-контейнер для встраивания в EscMenu
4. `Update()` Esc handler: embedded → `OnBackRequested`, standalone → `SetOpen(false)` (как раньше)

**Изменения в `EscMenuWindow.cs`:**

- `OpenKeybindingsSubPage()`: устанавливает `OnBackRequested = NavigateBack`, вызывает `GetPageRoot()`, передаёт в `NavigateTo()`

**Standalone-режим сохранён:** `Show()`/`Hide()`/`Toggle()` работают как прежде, `_isEmbedded = false` по умолчанию.

**Поведение:**
```
EscMenu → Настройки → Управление
  → KeybindingsWindow.GetPageRoot() → встроен в EscMenu
  → Esc внутри KeybindingsWindow → OnBackRequested → NavigateBack → Настройки
  → Esc в режиме прослушивания → CancelListening (не NavigateBack)
```
