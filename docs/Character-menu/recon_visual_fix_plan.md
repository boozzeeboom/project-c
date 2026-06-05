# CharacterWindow — Visual Fix Plan (2026-06-05)

**Автор:** Mavis (self-analysis, после того как UI-subagent завис на таймауте)
**Вердикт:** ОКНО ВАЛИДНОЕ ТЕХНИЧЕСКИ, LAYOUT СЛОМАН В USS. Все 5 видимых багов — следствие 4-х корневых ошибок в USS + 1 в C# inline-fallback.

---

## A. Root cause analysis (с цитатами из кода)

### B1. Табы в столбик (5 строк × 30px = 150px впустую)
**Файл:** `Assets/_Project/UI/Resources/UI/CharacterWindow.uss`
**Строки 72-86:**
```css
.tabs {
    flex-direction: row;
    flex-wrap: wrap;
    margin-bottom: 8px;
}
.tab-btn {
    flex-grow: 1;
    height: 30px;
    ...
    -unity-font-style: bold;
}
```
**Корень:** `.tab-btn` не имеет `flex-basis: 0` и `flex-shrink: 1` (по дефолту). Текст лейблов "ПЕРСОНАЖ"/"РЕПУТАЦИЯ"/"ИНВЕНТАРЬ" — широкий (>100px на каждом). При `flex-grow: 1` без `flex-basis: 0` минимальный размер = content size → 5 табов не влезают в 696px → `flex-wrap: wrap` (line 74) переносит в 5 строк.
**Дополнительно:** пользователь сказал "кнопки должны быть мелкие, текст небольшой" — нужно убрать bold и уменьшить font-size с 14 (наследуется от window) до 11-12.

### B2. stats-grid 1-колонка, значения наезжают
**Файл:** USS строки 137-150
```css
.stat-row {
    flex-direction: row;
    justify-content: space-between;
    padding: 3px 6px;
    border-bottom-width: 1px;
    border-bottom-color: rgba(80, 100, 130, 0.15);
}
```
**Корень:** `.stat-row` — ребёнок flex-column `.stats-grid` (line 131-136). В UI Toolkit `justify-content: space-between` в flex-row **деградирует в "все слева"** если ребёнок не имеет явной ширины (нет `align-self: stretch` или `width: 100%`). Ребёнок flex-column по дефолту получает `align-self: auto` = `stretch` только если у родителя **flex-direction: row**. У нас `flex-direction: column` — `align-self: auto` дефолт **НЕ** stretch в этом направлении, и ребёнок берёт min-content размер. `space-between` пытается разнести, но дети слиплись в min-content. **Лечение:** явно `align-self: stretch` или `width: 100%` + `min-width: 0` для значения (для длинных названий).

### B3. Message label выходит за нижнюю границу
**Файл:** USS
**Корень:** контент-секции (`.list-section` line 112-115) не имеют `flex-grow: 1; flex-shrink: 1; min-height: 0`. ListView внутри них (`item-list` line 121-128) имеет `max-height: 280px` — ок, но родительская `.list-section` не ограничивает свою высоту внутри flex-column. Когда сумма всех детей > `max-height: 90%` окна → последние дети (actions, message) выдавливаются за границу. **Лечение:** `.list-section { flex-grow: 1; flex-shrink: 1; min-height: 0; }` + `.item-list` явно `flex-shrink: 1` (ListView по дефолту `flex-shrink: 0`).

### B4. Close button перекрывает stats
**Файл:** USS строки 163-168
```css
.actions {
    flex-direction: row;
    flex-wrap: wrap;
    margin-top: 8px;
    min-height: 40px;
}
```
**Корень:** `.actions` — flex-column ребёнок, без `flex-shrink: 0`. Когда контента много, actions сжимаются до min-height и накладываются. **Лечение:** `.actions { flex-shrink: 0; }`.

### B5. Полупрозрачный фон (видны другие UI)
**Файл:** USS line 23: `background-color: rgba(20, 25, 35, 0.95);`
**И:** `CharacterWindow.cs:ApplyInlineFallbackStyles` (~line 1114) ставит inline `backgroundColor = new Color(0.078f, 0.098f, 0.137f, 0.95f);` — **inline-стиль перебивает USS** (specificity inline > USS class).
**Корень:** 0.95 alpha = 5% прозрачности. **Лечение:** поднять до 1.0 в USS (и не дублировать в C# — убрать строку backgroundColor из ApplyInlineFallbackStyles, USS сам справится).

---

## B. Concrete fix list (точные диффы)

### Fix B1: табы в строку + мелкий шрифт
**Файл:** `Assets/_Project/UI/Resources/UI/CharacterWindow.uss`, строки 71-93
```css
.tabs {
    flex-direction: row;
    flex-wrap: wrap;
    margin-bottom: 6px;
    flex-shrink: 0;       /* NEW: не сжимается при нехватке */
}
.tab-btn {
    flex-grow: 1;
    flex-basis: 0;          /* NEW: критично — делит поровну независимо от контента */
    flex-shrink: 1;
    height: 24px;           /* CHANGED: 30→24 — мельче, как просил пользователь */
    font-size: 11px;        /* NEW: мелкий шрифт */
    background-color: rgba(60, 80, 120, 0.4);
    color: rgb(220, 220, 230);
    border-width: 0;
    border-bottom-width: 2px;
    border-color: transparent;
    /* убрать -unity-font-style: bold */
}
.tab-btn:hover { background-color: rgba(80, 100, 140, 0.6); }
.tab-btn.active {
    border-bottom-color: rgb(255, 220, 130);
    background-color: rgba(80, 110, 160, 0.6);
}
```

### Fix B2: stats-grid 2-колонка
**Файл:** USS строки 137-143
```css
.stat-row {
    flex-direction: row;
    justify-content: space-between;
    align-self: stretch;     /* NEW: критично */
    min-width: 0;            /* NEW: для длинных значений */
    padding: 3px 6px;
    border-bottom-width: 1px;
    border-bottom-color: rgba(80, 100, 130, 0.15);
}
.stat-label {
    color: rgb(180, 180, 200);
    min-width: 0;            /* NEW */
}
.stat-value {
    color: rgb(220, 220, 230);
    -unity-text-align: middle-right;  /* NEW: прижать вправо */
    min-width: 0;            /* NEW */
}
```

### Fix B3+B4: секция растягивается, actions/message защищены
**Файл:** USS
```css
.list-section {
    min-height: 90px;
    margin-bottom: 6px;
    flex-grow: 1;            /* NEW */
    flex-shrink: 1;          /* NEW */
    min-height: 0;           /* CHANGED: перебиваем 90px (для scroll внутри) */
    overflow: hidden;        /* NEW */
}
.item-list {
    min-height: 80px;
    max-height: none;        /* CHANGED: убрать 280px, пусть растёт до родителя */
    flex-grow: 1;            /* NEW */
    flex-shrink: 1;          /* NEW */
    min-height: 0;           /* NEW */
    background-color: rgba(30, 40, 60, 0.4);
    border-width: 1px;
    border-color: rgba(80, 100, 130, 0.4);
    border-radius: 4px;
}
.actions {
    flex-direction: row;
    flex-wrap: wrap;
    margin-top: 6px;
    min-height: 32px;
    flex-shrink: 0;          /* NEW: всегда виден внизу */
}
.action-btn {
    flex-grow: 1;
    height: 30px;            /* CHANGED: 36→30 */
    font-size: 11px;         /* NEW */
    margin: 1px;             /* CHANGED: 2→1 */
    color: rgb(240, 240, 240);
    /* убрать -unity-font-style: bold */
    border-width: 0;
    border-radius: 3px;
}
.message-label {
    margin-top: 4px;         /* CHANGED: 8→4 */
    padding: 4px 6px;        /* CHANGED */
    color: rgb(220, 220, 230);
    font-size: 11px;         /* CHANGED: 12→11 */
    background-color: rgba(0, 0, 0, 0.5);
    border-radius: 3px;
    -unity-text-align: middle-center;
    flex-shrink: 0;          /* NEW */
}
```

### Fix B5: убрать прозрачность фона
**Файл:** USS line 23
```css
.character-window {
    ...
    background-color: rgb(20, 25, 35);   /* CHANGED: убрать alpha */
    ...
}
```

**Файл:** `CharacterWindow.cs` — в методе `ApplyInlineFallbackStyles` (~line 1114) **УДАЛИТЬ строку:**
```csharp
main.style.backgroundColor = new Color(0.078f, 0.098f, 0.137f, 0.95f);
```
(остальные inline-стили нужны — они защищают от resolvedStyle=initial на 1-м кадре)

### Фикс окна целиком
**Файл:** USS `.character-window` block (lines 15-33)
```css
.character-window {
    position: absolute;
    top: 5%;
    left: 50%;
    translate: -50% 0;
    width: 720px;
    max-width: 90%;
    max-height: 90%;
    background-color: rgb(20, 25, 35);   /* CHANGED */
    border-width: 2px;
    border-color: rgba(120, 150, 200, 0.8);
    border-radius: 8px;
    padding: 8px;                         /* CHANGED: 12→8 */
    color: rgb(220, 220, 230);
    font-size: 14px;
    display: flex;
    flex-direction: column;
    align-items: stretch;
}
```

---

## C. Адаптивный layout (для разных разрешений)

| Разрешение | Что происходит | ОК? |
|------------|----------------|-----|
| 3840×2160 (4K) | Окно 720px, max 90% = 3456px → фиксируется 720px | ✅ |
| 2560×1440 (QHD) | Окно 720px | ✅ |
| 1920×1080 (FHD) | Окно 720px | ✅ |
| 1366×768 (HD) | max-width 90% = 1229px → окно 720px помещается | ✅ |
| 1280×720 (HD720) | max-width 90% = 1152px → окно 720px помещается | ✅ |
| 800×600 (минимум) | max-width 90% = 720px → окно 720px (тютелька в тютельку) | ⚠️ |
| <720×600 | Окно не влезает → max-width сработает, padding съест контент | ⚠️ |

**Вывод:** окно адаптивно для 1280×720+. На совсем маленьких экранах (<720×600) нужно либо уменьшить base width (например 600px), либо дать user scaling. Сейчас — оставляем 720px как разумный минимум для 5-табового окна с боковой инфой.

**Внутри окна:** после фиксов секция будет scroll-vertical внутри (благодаря `flex-grow: 1; min-height: 0; overflow: hidden` на `.list-section` + `flex-shrink: 1` на ListView). Это решает адаптивность контента.

---

## D. Глобальный риск

1. **`ApplyInlineFallbackStyles` vs USS** — этот метод задаёт inline-стили, которые перебивают USS по specificity. Нужно оставить его для **позиционирования и sizing** (это критично для 1-го кадра), но убрать дублирующие свойства (background-color, padding, border, font-size, color). Это безопасно — USS применится после layout pass и перепишет inline для этих свойств.

2. **`flex-shrink: 0` на tabs/actions/message** — гарантирует что header/tabs/actions всегда видны, а скролл идёт ТОЛЬКО в content. Это правильно для info-меню.

3. **Вертикальный scroll в ListView** — у `ListView` в Unity 6 vertical scroll работает из коробки (если `fixedItemHeight` задан — он у меня задан 32). Не нужно дополнительной обвязки.

4. **Закрытие message label из скриншота** — в ApplyInlineFallbackStyles нет width для main, и когда `.character-window` имеет `width: 720px` (inline), message label как flex-column ребёнок может схлопнуться по высоте. После фикса B5+B3 должно работать.

5. **Не трогать pickingMode/cursor logic в C#** — 4 FIX'а (pickingMode, inline-fallback, cursor, MarkDirtyRepaint) работают правильно, проверены на MarketWindow.

---

## E. Что НЕ делаю

- Не создаю .meta / .asmdef (правила AGENTS.md)
- Не добавляю новые стили (только меняю существующие)
- Не правлю CharacterWindow.cs кроме удаления одной строки в ApplyInlineFallbackStyles
- Не делаю функциональных изменений (содержимое табов, фильтры, etc. — работают)
- Не делаю .uxml изменений (структура правильная, проблема в USS)

---

## F. Порядок применения (для Mavis)

1. **Patch USS** — заменить ~30 строк (через patch tool, 4 правки: .tabs+.tab-btn, .stat-row+.stat-label+.stat-value, .list-section+.item-list+.actions+.action-btn+.message-label, .character-window background-color+padding)
2. **Patch C#** — удалить одну строку `main.style.backgroundColor = ...` в `ApplyInlineFallbackStyles`
3. **refresh_unity** + read_console → 0 errors
4. **Play mode test** — `execute_code` с StartHost + EnterPlay + cw.Show() + screenshot
5. **Скриншоты всех 5 табов** для визуальной верификации
