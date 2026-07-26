# UIElementsRepaintPanels: 3.1 MB/кадр GC Alloc от UI Toolkit — план исследования

**Дата:** 2026-07-26
**Контекст:** профилирование после фикса NavMesh-спайков (T-PERF01). Основные лаги устранены, но остаётся фоновая проблема.

---

## Симптомы (из профайлера)

Каждый кадр, даже в спокойном состоянии, `PostLateUpdate.UIElementsRepaintPanels` аллоцирует **~3.1 MB** GC памяти. Структура:

| Панель | GC Alloc/кадр |
|--------|---------------|
| `CharacterPanelSettings.PrepareRepaint` | ~390 KB |
| `MarketPanelSettings.PrepareRepaint` | ~283 KB |
| Остальные панели (EscMenu, MetaRequirement, ShipKey, Dialog, CommPanel) | ~2.4 MB суммарно |

Внутри каждой панели основная аллокация:
```
UIElements.UpdateRenderData → RenderTreeManager.Process → GC.Alloc: 118 KB
RenderTree.UpdateVisuals → UIR.ConvertEntriesToCommands → GC.Alloc: ~3 KB
```

**Эффект:** 3.1 MB/кадр × 60 fps = **186 MB/сек** GC pressure. GC.Collect срабатывает часто, давая микростаттеры.

---

## Что исследовать

### 1. Какие панели реально видны/активны?

UI Toolkit перерисовывает ВСЕ панели с `PanelSettings`, даже скрытые. Нужно определить:
- Сколько всего `PanelSettings`-ассетов в проекте?
- Какие из них привязаны к активным `UIDocument` в сцене?
- Можно ли для скрытых панелей делать `UIDocument.enabled = false` или удалять `UIDocument`?

**Инструменты:**
```csharp
// В ExecuteCode найти все UIDocument в сцене
var docs = Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include);
foreach (var d in docs)
    Debug.Log($"[UIDocument] {d.name} enabled={d.enabled} panelSettings={d.panelSettings?.name}");
```

### 2. `CharacterPanelSettings` — почему 390 KB даже когда закрыт?

CharacterWindow — одна из самых тяжёлых панелей. Надо проверить:
- Не перестраивается ли дерево каждый кадр (нет ли `MarkDirtyRepaint` в Update)?
- Не обновляются ли data bindings каждый кадр?
- Может быть `CharacterWindow.IsVisible()` возвращает `true` даже когда окно скрыто?

### 3. `MarketPanelSettings` — 283 KB

Аналогично CharacterWindow. Рынок может быть скрыт, но PanelSettings всё ещё активен.

### 4. Можно ли отключить `PanelSettings` для невидимых панелей?

Гипотеза: если панель скрыта (display:none), но `UIDocument` активен, UI Toolkit всё равно проходит цикл repaint. Решения:
- **A)** `UIDocument.enabled = false` когда панель не нужна
- **B)** Отцеплять `UIDocument` от GameObject (`Destroy`/`AddComponent` по требованию)
- **C)** Использовать `PanelSettings.clearColor` и проверять — помогает ли UI Toolkit оптимизации при нулевой альфе?

### 5. Есть ли кастомные `VisualElement` с `MarkDirtyRepaint` в Update?

Поискать по проекту:
- `MarkDirtyRepaint()`
- `generateVisualContent`
- Кастомные `VisualElement`-наследники

---

## Приоритетный порядок исследования

1. **Посчитать активные UIDocument в сцене** (ExecuteCode выше) — 5 минут
2. **Проверить CharacterWindow.IsVisible() логику** — не дёргает ли Repaint каждый кадр
3. **Проверить MarketWindow — аналогично**
4. **Протестировать гипотезу:** вручную отключить `UIDocument` у скрытых панелей через инспектор, снять новый профайлер
5. **Если подтвердится** — сделать автоматическое отключение `UIDocument` при скрытии панели

---

## Ключевые файлы для проверки

- `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` — поиск `MarkDirtyRepaint`, `schedule.Execute`, `RegisterCallback`
- `Assets/_Project/Trade/Client/MarketWindow.cs` — аналогично
- `Assets/_Project/Scripts/UI/UIManager.cs` — управление видимостью панелей
- Все `*.uss` файлы в `Assets/_Project/Resources/UI/` — нет ли `display: flex` на скрытых элементах
