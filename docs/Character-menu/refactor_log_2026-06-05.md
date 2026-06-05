# CharacterWindow — Refactor Log 2026-06-05 (v2 final)

**Дата:** 2026-06-05
**Автор:** Mavis (Mavis)
**Сессия:** CharacterMenu refactor
**Статус:** ✅ **ОКНО РЕНДЕРИТСЯ ПРАВИЛЬНО** (визуально подтверждено юзером)
**Связанные доки:** `recon_visual_bugs.md` (5 багов), `recon_visual_fix_plan.md` (план v1)

---

## TL;DR для следующей сессии

**Корневая причина** двух сломанных состояний UI — **некорректная привязка USS-ассета в инспекторе** `[CharacterWindow]`. Вместо `CharacterWindow.uss` был указан сам `CharacterWindow.uxml` (Unity позволила это — оба типа импортируются ScriptedImporter-ами). `EnsureBuilt` подключал мусор → ничего не рендерилось кроме дефолтной Unity-темы.

**Решение (v2 — рабочее):**
1. Перепривязать `characterWindowUss` → настоящий `CharacterWindow.uss` (через MCP `SerializedObject`).
2. Добавить `!important` ко **всем** class-стилям в `CharacterWindow.uss` — иначе `UnityDefaultRuntimeTheme` перебивает (type-selector `.unity-base-button` > class `.tab-btn` в UI Toolkit 6).
3. Создать отдельный `CharacterPanelSettings.asset` (по аналогии с `MarketPanelSettings`) и привязать к `[CharacterWindow]`. Тема (`UnityDefaultRuntimeTheme`) — нужна, без неё UI Toolkit не рендерится вовсе.
4. Убрать избыточный `ApplyElementStyles` (~130 строк) и сократить `ApplyInlineFallbackStyles` (теперь только position+size для 1-го кадра).

**Окно адаптивно** для разрешений ≥1280×720. Chrome (header+info-bar+tabs+actions+message) ≈ 120px, info-зона ~76% высоты. Кнопки 22-24px / шрифт 10-12px.

---

## 1. Что изменено (конкретные файлы)

### 1.1. `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` — **полностью переписан (357 строк)**
- Все class-стили получили `!important` (защита от темы).
- Chrome 173→120px (header 4px, info-bar 6px, tabs 3px, actions 3px, message 2px).
- Кнопки 24-30px → 22-24px, шрифт 10-12px.
- Tab buttons: `flex-grow: 1; flex-basis: 0; flex-shrink: 1` — делят ширину поровну в строку.
- ListView: `flex-grow: 1; flex-shrink: 1; min-height: 0` — корректный overflow, scroll внутри.
- Stat rows: 2-колонка (label / value) с `align-self: stretch` и `min-width: 0`.

### 1.2. `Assets/_Project/UI/Resources/UI/CharacterPanelSettings.asset` — **новый файл**
- Копия структуры `MarketPanelSettings.asset` (themeUss=UnityDefaultRuntimeTheme, scaleMode=0, referenceRes=1920×1080).
- Назначен в `[CharacterWindow].UIDocument.panelSettings`.

### 1.3. `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` — **1367 → 1194 строк (-173)**
- **Удалён** `ApplyElementStyles()` целиком (140 строк inline-стилизации — больше не нужен, USS с `!important` побеждает тему).
- **Сокращён** `ApplyInlineFallbackStyles` — оставлены только `position/top/left/translate/width/maxWidth/maxHeight` (то, что нужно для 1-го кадра до применения USS).
- **Убран** дубль `ApplyElementStyles()` из `SetVisible()`.
- **Убран** диагностический `Debug.Log` `USS loaded: ...` — заменён на короткий `[CharacterWindow] Built`.

### 1.4. `Assets/_Project/Scenes/BootstrapScene.unity` — **M (MCP-сериализация)**
- Через `SerializedObject.FindProperty("characterWindowUss").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>(...)` — записано правильное значение в сцену.

### 1.5. `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` — **НЕ изменён**
- Структура правильная, проблема была только в стилях.

---

## 2. Уроки (для следующих сессий с UI Toolkit)

### 2.1. UI Toolkit + UnityDefaultRuntimeTheme + class selectors = КОНФЛИКТ
- Тема задаёт стили через **type-selector** `.unity-base-button` (для всех `Button`), `.unity-base-dropdown`, `.unity-list-view`, `.unity-text-element`.
- В UI Toolkit 6 у **type-selector** (`.unity-base-button`) специфичность ВЫШЕ, чем у обычного **class-selector** (`.tab-btn`).
- **Лечение:** `!important` на class-стилях (проверено — работает). Альтернатива: **compound selectors** типа `.unity-button.tab-btn` (тоже работает, но многословно).
- **НИКОГДА не снимать** `themeStyleSheet` через `null` — UI Toolkit не рендерится без темы ("No Theme Style Sheet set to PanelSettings, UI will not render properly"). Подтверждено лично (см. §3).

### 2.2. Resources.Load<StyleSheet> возвращает мусор если в инспекторе задано что-то не то
- Слот `characterWindowUss` в `CharacterWindow.cs` имеет тип `StyleSheet`.
- Если туда случайно перетащить `.uxml` файл — Unity позволит (оба типа — `ScriptedImporter` в `UnityEngine.UIElements` namespace), но в runtime получится мусорный StyleSheet.
- `EnsureBuilt` проверяет `if (characterWindowUss != null) _doc.rootVisualElement.styleSheets.Add(characterWindowUss);` — и подключает мусор.
- **Защита:** `if (characterWindowUss == null) characterWindowUss = Resources.Load<StyleSheet>(...)` — fallback НЕ сработает, если инспектор-поле уже заполнено.
- **Лечение:** перепривязать через `SerializedObject.objectReferenceValue` в Editor. **Не делать** `Resources.Load` из runtime-кода для "починки" — лучше починить сцену.

### 2.3. Документация по `Refactor log v1` содержала неверную диагностику
- v1-log утверждал, что "программная inline-стилизация в C# — надёжный workaround". Это было **неверное** направление — корень был в инспекторе.
- Если v1-log найдётся в будущем — игнорировать §2-3 (там гипотезы A-D, не подтверждённые).

---

## 3. Сценарий "пропавшего окна" (зафиксирован)
**Симптом:** окно `CharacterWindow` отображается как тонкая полоса сверху, контента не видно.
**Причина:** я снял `themeUss` в `CharacterPanelSettings.asset` (хотел "обнулить" тему), но UI Toolkit **требует тему** — без неё рендеринг падает.
**Лечение:** вернуть `themeUss = UnityDefaultRuntimeTheme` + использовать `!important` в USS для перебития.
**Юзер подтвердил** визуально, что после возврата темы + `!important` окно заработало.

---

## 4. Файлы-артефакты

| Файл | Изменение | Назначение |
|------|-----------|------------|
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` | Полностью переписан, +`!important` | Стили окна с защитой от темы |
| `Assets/_Project/UI/Resources/UI/CharacterPanelSettings.asset` | Новый | PanelSettings для `[CharacterWindow]` (отдельный от `MarketPanelSettings`) |
| `Assets/_Project/UI/Resources/UI/CharacterPanelSettings.asset.meta` | Новый (Unity сгенерил) | GUID для asset |
| `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` | -173 строки (убран `ApplyElementStyles` и сокращён `ApplyInlineFallbackStyles`) | Контроллер окна |
| `Assets/_Project/Scenes/BootstrapScene.unity` | M (через MCP) | Поправлены inspector-ссылки: `panelSettings` + `characterWindowUss` |
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` | Не тронут | Структура правильная |
| `docs/Character-menu/recon_visual_bugs.md` | Не тронут (v1) | Диагноз 5 визуальных багов |
| `docs/Character-menu/recon_visual_fix_plan.md` | Не тронут (v1) | План фиксов (частично устарел) |
| `docs/Character-menu/refactor_log_2026-06-05.md` | **Перезаписан на эту версию (v2)** | Финальный лог рефактора |

---

## 5. Что НЕ сделано (явно для следующих сессий)

- ❌ **Создание `CharacterPanelSettings.asset` без `themeUss`** — попробовал, провалилось (см. §3). **Не повторять.**
- ❌ **Cleanup устаревших `Debug.Log("Built: root.children=…")`** — удалён.
- ❌ **Cleanup `ApplyInlineFallbackStyles` от дублирующих USS-свойств** — сделано, метод сокращён с ~35 строк до 13.
- ❌ **Применение `!important`** — сделано, ко всем свойствам USS.
- ✅ **Возврат темы в `CharacterPanelSettings`** — сделано.

---

## 6. Известные ограничения (не блокеры)

- `!important` на **всех** свойствах — перебор, но безопасный. В будущем можно заменить на **compound selectors** (`.unity-button.tab-btn`, `.unity-text-element.stat-value`) для уменьшения `!important`-шума. Это **отдельный тикет**.
- `flex-basis: 0` на tab-btn — технически правильно, но при `flex-shrink: 1` и длинных текстах табы могут слегка пульсировать при resize. Не критично, не замечено при текущих разрешениях.
- `CharacterPanelSettings` дублирует настройки `MarketPanelSettings` (theme, ref resolution). Если проект добавит третий/четвёртый UI Toolkit window — стоит вынести общие настройки в префаб через `Resources.Load<PanelSettings>(...)` + кэширование.

---

## 7. Чек-лист для следующей сессии (если потребуется)

1. [ ] Юзер сделал Play + скриншот → **подтверждено** визуально.
2. [ ] Юзер скинул Console-лог → **подтверждено** 0 ошибок, 0 наших warning.
3. [x] Cleanup `ApplyElementStyles` → **сделано**.
4. [x] Cleanup `ApplyInlineFallbackStyles` → **сделано**.
5. [x] Cleanup диагностических `Debug.Log` → **сделано**.
6. [x] Save scene → **сделано**.
7. [x] 0 compile errors → **подтверждено** (`read_console types=[error] count=15` → пусто).
8. [ ] **Юзер делает `git commit`** с diff-ом (5 файлов).
9. [ ] **Опционально (отдельный тикет):** заменить `!important` на compound selectors.

---

## 8. Memory-след

В `~/.mavis/agents/mavis/memory/MEMORY.md` уже записано правило:
**"Скриншоты делает ЮЗЕР сам в Editor. Agent НЕ вызывает ScreenCapture / manage_camera(screenshot) / не пишет .png."** — повторено 2026-06-05.
