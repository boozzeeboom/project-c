# Known UI Bugs

> **Дата:** 2026-06-28
> **Каталог:** UI Toolkit баги, зафиксированные в сессиях Phase 2

---

## BUG-001: Esc после закрытия окна открывает меню

**Статус:** ❌ Не исправлен (Phase 2.1)
**Сессия:** #7 (2026-06-28)
**Файлы:** `Scripts/UI/UIManager.cs`, `Scripts/UI/EscMenu/EscMenuWindow.cs`

**Симптом:** Когда игрок открывает CharacterWindow (P), затем нажимает Esc — окно закрывается, но **сразу же открывается EscMenu**. Игрок хотел просто закрыть CharacterWindow, без меню.

**Ожидание:** Esc → закрыть фронтальное окно → ничего больше.

**Реальность:** Esc → CharacterWindow закрывается → EscMenu открывается (на том же или следующем кадре).

**Корень:** CharacterWindow.handleEsc (собственный handler в Update) не зарегистрирован в UIManager. UIManager не знает что окно было открыто и закрыто. При проверке `_openPanels.Count == 0` и отсутствии других флагов — открывает EscMenu.

**Попытки исправления:**
1. ❌ Cooldown `_lastCloseTime` — CharacterWindow не в стеке UIManager, cooldown не срабатывал
2. ❌ `AnyNonManagedWindowOpen()` проверка — CharacterWindow успевал скрыться до проверки (порядок Update не гарантирован)
3. ❌ LateUpdate кэш `_externalWasVisible` — работало нестабильно из-за порядка вызовов
4. ❌ `_escConsumedThisFrame` флаг — только для стековых панелей, CharacterWindow не в стеке
5. ❌ `[DefaultExecutionOrder(-100)]` — сломал открытие меню на первый Esc

**Вывод:** Требуется рефакторинг Esc-логики в проекте. CharacterWindow (и другие окна) НЕ должны сами обрабатывать Esc. Единый EscHandler с приоритетом ДО всех окон — правильное решение, но требует изменений в CharacterWindow и других окнах, что не было сделано в этой сессии.

**Временное решение:** Игрок может нажать Esc второй раз чтобы закрыть EscMenu (если оно открылось). Навигация: Esc → (закрывает окно + открывает меню) → Esc → (закрывает меню).

---

## BUG-002: Esc не восстанавливает курсор после закрытия KeybindingsWindow

**Статус:** ❌ (косметика)
**Сессия:** #7

**Симптом:** После закрытия KeybindingsWindow через Esc, курсор остаётся разблокированным.

**Корень:** KeybindingsWindow.Hide() не восстанавливает Cursor.lockState.

---

## BUG-003: USS EscMenuWindow не грузится через Resources.Load

**Статус:** ✅ Исправлен (переименование файла)
**Сессия:** #7

**Симптом:** `Resources.Load<StyleSheet>("UI/EscMenuWindow")` возвращал `inlineStyle` (дефолт) вместо файла `.uss`.

**Причина:** UI_TOOLKIT_GUIDE.md §2 Ошибка 1 — `Resources.Load<StyleSheet>` по некоторым путям возвращает мусор. Конфликт имён `EscMenuWindow.uss` / `EscMenuWindow.uxml` в Resources.

**Исправление:** Переименован `.uss` в `EscMenuStyles.uss`, путь в коде обновлён.
