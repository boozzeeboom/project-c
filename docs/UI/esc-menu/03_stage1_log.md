# Этап 1 — Лог реализации (2026-07-13)

> **Статус:** ✅ Завершён
> **Коммит:** pending

---

## 1. Ошибки первой попытки (reverted)

### Что пошло не так

Первая реализация полностью сломала работающую Esc-логику, которая правилась ~10 сессий:

1. **Убран `Clear()+CloneTree()`** — замена на канонический `_root = _doc.rootVisualElement` паттерн из UI_TOOLKIT_GUIDE.md §3. Это сломало загрузку UXML/USS: UIDocument не имел `visualTreeAsset` (NMC его не назначает), `<Style src>` в UXML не загрузил USS (BUG-003), `childCount == 0` guard блокировал EnsureBuilt навсегда.

2. **`HandleGlobalEscape()` вызывал `Hide()` без `ClosePanel()`** — панель оставалась в стеке UIManager навсегда, Esc переставал работать после первого закрытия.

3. **Изменена структура SetOpen/IsOpen** — `_container` вместо `_root`, поломана логика `pickingMode` и `display`.

### Почему Clear+CloneTree НЕЛЬЗЯ убирать сейчас

- `Clear()+CloneTree()+Add()` — **рабочий паттерн** для runtime-созданных UIDocument (как в NMC)
- `[SerializeField]` поля + Resources fallback — единственный надёжный способ загрузки USS (BUG-003: `Resources.Load<StyleSheet>` работает только после переименования `.uss`)
- Канонический паттерн из UI_TOOLKIT_GUIDE.md §3 предполагает, что `visualTreeAsset` назначен в **инспекторе**, а не создаётся через NMC в runtime
- **Решение:** отложить удаление Clear+CloneTree до отдельного тикета, когда все окна перейдут на префаб-модель

---

## 2. Финальная реализация (safe)

### Изменённые файлы

| Файл | Что изменилось |
|---|---|
| `EscMenuWindow.uxml` | Добавлен header (back-btn + title), ScrollView (esc-content), 3 кнопки корневого меню вместо 1 |
| `EscMenuStyles.uss` | Размер 480×440 (было 360×280), стили back-btn/ScrollView/section-header/warning-btn |
| `EscMenuWindow.cs` | Добавлен `Stack<VisualElement>`, `NavigateTo/NavigateBack/NavigateToRoot`, `IsInSubmenu()`, `[DefaultExecutionOrder(-150)]`. Публичный API нетронут. Clear+CloneTree сохранён. |
| `UIManager.cs` | +4 строки: делегация Esc → `NavigateBack()` только для submenu; корень → старый `CloseTopPanel()` |

### Ключевое архитектурное решение

**UIManager делегирует Esc в EscMenuWindow ТОЛЬКО когда открыто подменю** (`IsInSubmenu() == true`). Корневой Esc идёт через старый `CloseTopPanel()` → `ClosePanel("EscMenu")` → `Hide()`. Это сохраняет 100% обратную совместимость с отлаженной Esc-логикой.

### Трассировка Esc (без изменений)

```
EscMenu закрыт + Esc  → Toggle → Show → OpenPanel("EscMenu",100,Hide) → NavigateToRoot
EscMenu корень + Esc  → CloseTopPanel → ClosePanel → Hide           (СТАРЫЙ ПУТЬ)
EscMenu подменю + Esc → IsInSubmenu()=true → NavigateBack           (НОВЫЙ ПУТЬ)
ПРОДОЛЖИТЬ            → Hide (панель в стеке, след. Esc уберёт)     (как раньше)
```

### Что отложено

- Удаление `Clear()+CloneTree()` — отдельный тикет (требует перевода всех UIDocument-окон на префабы)
- `HandleGlobalEscape()` метод заменён на `IsInSubmenu()` + `NavigateBack()` — проще и надёжнее

---

## 3. Соответствие плану

| Пункт плана | Статус |
|---|---|
| Убрать Clear+CloneTree | ⚠️ Отложено (сломает runtime-UIDocument) |
| Stack<VisualElement> для подменю | ✅ `NavigateTo/NavigateBack/NavigateToRoot` |
| HandleGlobalEscape через UIManager | ✅ `IsInSubmenu()` + делегация |
| Размер 480×440 + ScrollView | ✅ |
| Кнопка НАЗАД | ✅ (показывается в подменю, скрыта на корне) |
| DefaultExecutionOrder(-150) | ✅ |
