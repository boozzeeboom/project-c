# Known UI Bugs

> **Дата:** 2026-06-28
> **Каталог:** UI Toolkit баги, зафиксированные в сессиях Phase 2

---

## BUG-001: Esc после закрытия окна открывает меню ✅ ИСПРАВЛЕН

**Статус:** ✅ Исправлен (Phase 2.5, 2026-06-28, сессия #7)
**Файлы:** `Scripts/UI/UIManager.cs`, `Scripts/UI/EscMenu/EscMenuWindow.cs`

### Симптом
Когда игрок открывает CharacterWindow (P), затем нажимает Esc — окно закрывается, но **сразу же открывается EscMenu**. Игрок хотел просто закрыть CharacterWindow, без меню.

### Финальное решение

**3 файла:**

#### 1. `EscMenuWindow.cs` — убран Update-метод
```diff
- private void Update() { ... слушал Esc ... }
+ // Esc-handler удалён. UIManager.HandleGlobalInput — единственный Esc-handler.
```

#### 2. `UIManager.cs` — единственный Esc-handler
```csharp
[DefaultExecutionOrder(-200)]  // бежим ДО CharacterWindow
public class UIManager : MonoBehaviour
{
    private void Update() { HandleGlobalInput(); }

    private void HandleGlobalInput()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

        // 1. Стековая панель → закрыть
        if (_openPanels.Count > 0) { CloseTopPanel(); return; }

        // 2. CharacterWindow видна → НЕ трогаем
        if (IsCharacterWindowVisible()) return;

        // 3. Открыть/закрыть EscMenu
        EscMenuWindow.Instance.Toggle();
    }

    private static bool IsCharacterWindowVisible()
    {
        var cw = CharacterWindow.Instance;
        if (cw == null) return false;
        try { return cw.IsVisible(); } catch { return false; }
    }
}
```

#### 3. `CharacterWindow.cs` — НЕ ТРОНУТ
Его собственный Esc-handler (Update строка 470) остался. Он скрывает окно по Esc. Но теперь UIManager первым проверяет `IsVisible()` (благодаря `DefaultExecutionOrder(-200)`) и не открывает меню параллельно.

### Почему работает (порядок Update)

| Кадр | UIManager (-200) | CharacterWindow (default 0) | Результат |
|------|------------------|------------------------------|-----------|
| Кадр N | Esc → IsVisible() == true → return | (не бежит, уже поздно) | CharacterWindow ещё открыта, UIManager не мешает |
| Кадр N | (уже отработал) | Esc → Hide() | CharacterWindow закрывается |
| Кадр N+1 | Esc (повторно) → IsVisible() == false → Toggle | — | EscMenu открывается |

### История попыток (все провалились до финального решения)

1. ❌ Cooldown `_lastCloseTime` — CharacterWindow не в стеке UIManager, cooldown не срабатывал
2. ❌ `AnyNonManagedWindowOpen()` в HandleGlobalInput — CharacterWindow успевал скрыться до проверки
3. ❌ LateUpdate кэш `_externalWasVisible` — нестабильно из-за порядка вызовов
4. ❌ `_escConsumedThisFrame` флаг — CharacterWindow не в стеке
5. ❌ Удаление Update из EscMenuWindow + перенос логики в UIManager **без** ExecutionOrder — race condition
6. ✅ **DefaultExecutionOrder(-200) на UIManager** — UIManager первым видит `IsVisible() == true`, не открывает меню

### Чему научились
- В Unity `MonoBehaviour.Update()` порядок вызова **не гарантирован** без атрибута `[DefaultExecutionOrder]`
- Для глобальных input-handler'ов (Esc, F1, etc) обязательно ставить **отрицательный** ExecutionOrder
- Если два класса обрабатывают одну клавишу — **один** должен быть главным (с ExecutionOrder), остальные не должны

---

## BUG-002: Cursor не восстанавливается после KeybindingsWindow

**Статус:** ❌ (косметика, не критично)
**Сессия:** #7

**Симптом:** После закрытия KeybindingsWindow через Esc, курсор остаётся разблокированным.

**Корень:** KeybindingsWindow.Hide() не восстанавливает `Cursor.lockState` и `Cursor.visible`.

**Фикс:** в KeybindingsWindow.SetOpen(false) добавить:
```csharp
if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
{
    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
    UnityEngine.Cursor.visible = false;
}
```

---

## BUG-003: USS EscMenuWindow не грузится через Resources.Load

**Статус:** ✅ Исправлен (переименование файла)
**Сессия:** #7

**Симптом:** `Resources.Load<StyleSheet>("UI/EscMenuWindow")` возвращал `inlineStyle` (дефолт) вместо файла `.uss`.

**Причина:** UI_TOOLKIT_GUIDE.md §2 Ошибка 1 — `Resources.Load<StyleSheet>` по некоторым путям возвращает мусор. Конфликт имён `EscMenuWindow.uss` / `EscMenuWindow.uxml` в Resources.

**Исправление:** Переименован `.uss` в `EscMenuStyles.uss`, путь в коде обновлён.
