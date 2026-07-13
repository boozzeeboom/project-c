# Этапы 4-5 — Лог реализации (2026-07-13)

> **Статус:** 4 ✅, 5 ✅

---

## Этап 4: Выход в главное меню

| Элемент | Реализация |
|---|---|
| Кнопка «ВЫХОД В МЕНЮ» | `esc-exit-btn` с классом `esc-btn-warning` |
| Диалог подтверждения | `ShowExitConfirmation()` — создаёт панель с сообщением и кнопками ВЫЙТИ/ОТМЕНА |
| Подтверждение | `ExecuteExitToMenu()`: `NetworkManager.Shutdown()` + `SceneManager.LoadScene("BootstrapScene")` |
| Отмена | `NavigateBack()` — возврат в корень меню |

Файл: `EscMenuWindow.cs` — методы `ShowExitConfirmation()`, `ExecuteExitToMenu()`

---

## Этап 5: Анимации переходов

| Анимация | Реализация |
|---|---|
| Root fade-in/out | USS: `transition: opacity 0.15s ease` на `.esc-root` |
| Stagger кнопок | `AnimateEntrance()` в `NavigateTo()` — добавляет классы `esc-btn-stagger` → `esc-btn-visible` с задержкой 40ms между кнопками |
| Stagger строк настроек | Аналогично для `.esc-setting-row`: `esc-row-stagger` → `esc-row-visible` |
| Hover/focus | Уже были: `transition: background-color 0.15s ease` на `.esc-btn:hover` |

Файлы: `EscMenuStyles.uss` (стили), `EscMenuWindow.cs` (`AnimateEntrance()`)
