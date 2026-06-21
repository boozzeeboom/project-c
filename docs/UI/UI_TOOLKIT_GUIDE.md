# UI Toolkit — Universal Layout & Theme Guide (Project C)

> **Дата создания:** 2026-06-20
> **Назначение:** Один документ на все случаи жизни с UI Toolkit в этом проекте.
> Когда окно выглядит криво, кнопки на весь экран, ничего не рендерится — **сначала сюда**.
> Проверено на реальных багах: `CharacterWindow` (2026-06-05, ~6 фиксов подряд),
> `CommPanelWindow` (2026-06-20, ~8 фиксов подряд). Один и тот же набор проблем.

---

## TL;DR — Чек-лист «почему окно кривое»

Если окно выглядит не так, как в `*.uxml` описано — **проверь 5 вещей по порядку**:

1. ☐ **`UIDocument.visualTreeAsset` назначен в инспекторе?** (если null — UIDocument ничего не рендерит)
2. ☐ **В скрипте: `EnsureBuilt` НЕ делает `Clear() + CloneTree()`?** (UIDocument сам грузит UXML)
3. ☐ **В скрипте: `Resources.Load<StyleSheet>("UI/...")` НЕ используется?** (часто возвращает мусор — `inlineStyle` от Default Runtime Theme)
4. ☐ **В `PanelSettings.asset` назначен `themeUss`?** (без темы UI не рендерит нормально — кнопки растягиваются)
5. ☐ **USS class-стили имеют `!important`?** (theme + базовый Unity stylesheet имеют выше specificity)

Если все 5 пунктов ✅ и всё ещё криво — проблема в логике USS (flex-shrink, flex-basis, `align-self: stretch`). См. **§4 ниже**.

---

## 1. Анатомия UI Toolkit окна

```
[CommPanelWindow] (GameObject на сцене) ← это MonoBehaviour singleton
  ├── UIDocument (UnityEngine.UIElements.UIDocument) ← обязателен, авто-добавляется [RequireComponent]
  │     ├── visualTreeAsset (VisualTreeAsset)  ← ССЫЛКА на .uxml (назначить в Inspector!)
  │     ├── panelSettings (PanelSettings)        ← ССЫЛКА на PanelSettings.asset
  │     └── sortingOrder (int)                   ← для z-order относительно других окон
  └── CommPanelWindow (MonoBehaviour)             ← скрипт-контроллер
        ├── commPanelUxml (VisualTreeAsset)       ← обычно ссылается на тот же .uxml
        └── commPanelUss (StyleSheet)             ← ССЫЛКА на .uss (назначить в Inspector!)
```

**Unity автоматически** при загрузке сцены:
- Находит `UIDocument`
- Загружает `visualTreeAsset` → кладёт в `_doc.rootVisualElement` как `TemplateContainer`
- Применяет `panelSettings.themeUss` (если назначен)
- Применяет `panelSettings` (scaleMode, sortingOrder, ...)

**Unity НЕ делает** автоматически:
- Не загружает custom USS (`commPanelUss`) — это **твоя** задача
- Не инициализирует кнопки/обработчики — это **твоя** задача

---

## 2. 5 фатальных ошибок (повторяются в каждом окне)

### ❌ Ошибка 1: `Resources.Load<StyleSheet>("UI/...")` для USS

**Симптом:** Кнопки растянуты на весь экран, фон/рамка не применяются, текст дефолтный белый.

**Причина:** `Resources.Load<StyleSheet>` по какому-то пути может вернуть **мусорный StyleSheet** — например `inlineStyle` от UnityDefaultRuntimeTheme, вместо твоего `.uss`. Unity **молча** позволяет оба файла быть ScriptedImporter'ами и не выдаёт warning.

```csharp
// ❌ НЕ ДЕЛАЙ ТАК в коде окна (после назначения в инспекторе)
private void Awake() {
    if (commPanelUss == null)
        commPanelUss = Resources.Load<StyleSheet>("UI/CommPanel");  // ← вернёт inlineStyle мусор
}

// ✅ Делай fallback только для UXML (VisualTreeAsset работает нормально)
private void Awake() {
    if (commPanelUxml == null)
        commPanelUxml = Resources.Load<VisualTreeAsset>("UI/CommPanel");
    // Для USS — никакого fallback, инспектор-ссылка обязательна
}
```

**Проверка в Play Mode:**
```csharp
// В логе должно быть НЕ "inlineStyle", а твоё имя
var uss = Resources.Load<StyleSheet>("UI/CommPanel");
Debug.Log($"USS loaded: {uss?.name}");
// Если видишь "inlineStyle" — путь конфликтует с другим импортом
```

### ❌ Ошибка 2: `PanelSettings.themeUss = null`

**Симптом:** В лог валится `No Theme Style Sheet set to PanelSettings, UI will not render properly`. Кнопки выглядят как «большие блоки на весь экран».

**Причина:** `UnityDefaultRuntimeTheme` задаёт базовые стили (кнопки, лейблы). Без неё UI Toolkit рендерит элементы в **дефолтном flex-mode**, где у всех кнопок `flex-grow: 1` (они растягиваются).

**Лечение:**
```csharp
// Через SerializedObject (Editor-скрипт или MCP):
var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/.../CommPanelPanelSettings.asset");
var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss");
var so = new SerializedObject(ps);
so.FindProperty("themeUss").objectReferenceValue = theme;
so.ApplyModifiedPropertiesWithoutUndo();
AssetDatabase.SaveAssets();
```

**Альтернатива:** Через Inspector — выбрать PanelSettings → Theme Style Sheet → `UnityDefaultRuntimeTheme`.

**Важно:** НИКОГДА не ставь `themeUss = null` намеренно. Без темы UI не рендерится вовсе (проверено на character-window refactor 2026-06-05 §3).

### ❌ Ошибка 3: `UIDocument.visualTreeAsset` не назначен в инспекторе

**Симптом:** `_doc.rootVisualElement.childCount == 0`. Никаких ошибок в консоли (или только UXML не нашёл). Окно «не существует».

**Причина:** При создании GO + UIDocument вручную через скрипт или копированием — поле `visualTreeAsset` пустое.

**Лечение:**
```csharp
// Editor-скрипт или MCP — назначить UXML через SerializedObject
var ud = go.GetComponent<UIDocument>();
var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/.../MyWindow.uxml");
var so = new SerializedObject(ud);
so.FindProperty("m_PanelSettings").objectReferenceValue = ps;  // или m_VisualTreeAsset
// Полное имя поля зависит от Unity 6 версии — смотри через SerializedObject.GetIterator()
```

**Проверка:**
```csharp
Debug.Log($"vta={ud.visualTreeAsset?.name} ps={ud.panelSettings?.name} sortingOrder={ud.sortingOrder}");
// vta должен быть не null
```

### ❌ Ошибка 4: `EnsureBuilt` делает `Clear() + CloneTree()` вручную

**Симптом:** Окно открывается, но потом исчезает при ре-енabler; кнопки дублируются; layout ломается после domain reload.

**Причина:** UIDocument **сам** подгружает `visualTreeAsset` в `OnEnable`. Если ты тоже делаешь `CloneTree()` и `Add(rootVE)` — получаешь **двойной** подвес: один от UIDocument, второй от тебя. После domain reload порядок меняется.

```csharp
// ❌ НЕ ДЕЛАЙ ТАК
private void EnsureBuilt() {
    _doc.rootVisualElement.Clear();                              // ← убиваем дерево UIDocument
    _doc.rootVisualElement.styleSheets.Add(commPanelUss);
    _root = commPanelUxml.CloneTree();                          // ← создаём своё
    _doc.rootVisualElement.Add(_root);                          // ← вешаем второе
    // ...работает до первого domain reload
}

// ✅ Делай так — UIDocument всё грузит сам
private void EnsureBuilt() {
    _root = _doc.rootVisualElement;
    if (commPanelUss != null && !_root.styleSheets.Contains(commPanelUss)) {
        _root.styleSheets.Add(commPanelUss);  // USS добавляем сами (UIDocument не знает про наш extra USS)
    }
    // Теперь ищем элементы в _root
    _panel = _root.Q<VisualElement>("panel");
    // ...
}
```

### ❌ Ошибка 5: USS class-стили без `!important`

**Симптом:** Кнопка серая вместо синей, текст белый вместо цветного, padding не работает.

**Причина:** В UI Toolkit 6+ **type-selector** (`.unity-base-button`) имеет **выше specificity**, чем обычный **class-selector** (`.my-button`). Без `!important` тема перебивает твои стили.

```css
/* ❌ НЕ ДЕЛАЙ ТАК — тема перебьёт */
.my-button {
    background-color: rgb(70, 130, 200);
}

/* ✅ Делай так — !important защищает от темы */
.my-button {
    background-color: rgb(70, 130, 200) !important;
}

/* Альтернатива: compound selector (многословно но без !important) */
.unity-button.my-button {
    background-color: rgb(70, 130, 200);
}
```

**См. подробнее:** `docs/Character/Character-menu/refactor_log_2026-06-05.md` §2.1.

---

## 3. Канонический шаблон UI Toolkit окна

```csharp
// MyWindow.cs
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.MyFeature.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class MyWindow : MonoBehaviour
    {
        public static MyWindow Instance { get; private set; }

        [Header("UI Assets (назначить в Inspector ОБЯЗАТЕЛЬНО)")]
        [SerializeField] private VisualTreeAsset myUxml;
        [SerializeField] private StyleSheet myUss;

        // UI refs (инициализируются в EnsureBuilt)
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _container;  // optional: первый VE из UXML с name=
        private Label _header;
        private Button _primaryButton;
        private bool _built = false;
        private bool _subscribed = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }

            _doc = GetComponent<UIDocument>();

            // ✅ UXML fallback на Resources (VisualTreeAsset работает)
            if (myUxml == null)
                myUxml = Resources.Load<VisualTreeAsset>("UI/MyWindow");
            // ❌ НЕ делаем Resources.Load<StyleSheet> fallback для USS — см. §2 Ошибка 1
        }

        private void OnEnable()
        {
            EnsureBuilt();
            TrySubscribe();
        }

        private void OnDisable() => TryUnsubscribe();
        private void OnDestroy() { TryUnsubscribe(); if (Instance == this) Instance = null; }

        private void EnsureBuilt()
        {
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc == null || _doc.rootVisualElement == null) return;
            if (myUxml == null)
            {
                Debug.LogError("[MyWindow] UXML не назначен ни в Inspector, ни в Resources/UI/", this);
                return;
            }

            // ✅ Используем rootVisualElement от UIDocument — он сам подгрузил UXML
            _root = _doc.rootVisualElement;

            // ✅ Добавляем USS ОДИН раз
            if (myUss != null && !_root.styleSheets.Contains(myUss))
                _root.styleSheets.Add(myUss);

            // ✅ sortingOrder — окно поверх других UI
            _doc.sortingOrder = 10;

            // ✅ Ищем элементы через Q<T>
            _container = _root.Q<VisualElement>("root");  // опционально
            _header = _root.Q<Label>("header");
            _primaryButton = _root.Q<Button>("primary-action-button");

            // ✅ De-dup подписок
            if (_primaryButton != null)
            {
                _primaryButton.clicked -= OnPrimaryClicked;
                _primaryButton.clicked += OnPrimaryClicked;
            }

            _built = true;
            if (_container != null) _container.style.display = DisplayStyle.None;
            else if (_root != null) _root.style.display = DisplayStyle.None;

            if (Debug.isDebugBuild)
                Debug.Log($"[MyWindow] Built: rootVE.children={_root.childCount}, styleSheets={_root.styleSheets.count}");
        }

        // ✅ PickingMode: Position когда открыто, Ignore когда закрыто
        public void SetOpen(bool open)
        {
            if (!_built) EnsureBuilt();
            if (!_built) return;

            var target = _container != null ? _container : _root;
            if (target != null)
            {
                target.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                target.pickingMode = open ? PickingMode.Position : PickingMode.Ignore;
            }

            // Cursor
            UnityEngine.Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            UnityEngine.Cursor.visible = open;
        }

        public void ToggleOpen() => SetOpen(!IsOpen);
        public bool IsOpen { get; private set; }

        private void OnPrimaryClicked() { /* ... */ }
        private void TrySubscribe() { /* ... */ }
        private void TryUnsubscribe() { /* ... */ }
    }
}
```

```xml
<!-- MyWindow.uxml -->
<?xml version="1.0" encoding="utf-8"?>
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
    <ui:VisualElement name="root" class="my-window-root">
        <ui:VisualElement name="panel" class="my-window-panel">
            <ui:Label name="header" text="Title" class="my-window-header" />
            <ui:Label name="message" text="..." class="my-window-message" />
            <ui:VisualElement class="my-window-buttons">
                <ui:Button name="primary-action-button" text="OK" class="my-window-button-primary" />
                <ui:Button name="secondary-action-button" text="Cancel" class="my-window-button-secondary" />
            </ui:VisualElement>
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

```css
/* MyWindow.uss */
/* ✅ !important на каждом свойстве — см. §2 Ошибка 5 */
.my-window-root {
    position: absolute !important;
    top: 0 !important; left: 0 !important; right: 0 !important; bottom: 0 !important;
    align-items: center !important;
    justify-content: center !important;
    background-color: rgba(0, 0, 0, 0.4) !important;
    display: flex !important;
}
.my-window-panel {
    width: 560px !important;
    max-width: 92% !important;
    background-color: rgb(15, 25, 38) !important;
    border-width: 2px !important;
    border-color: rgb(80, 110, 145) !important;
    border-radius: 6px !important;
    padding: 16px 20px !important;
    flex-direction: column !important;
}
.my-window-button-primary {
    height: 28px !important;
    background-color: rgb(70, 130, 200) !important;
    color: rgb(240, 240, 245) !important;
    border-width: 0 !important;
    border-radius: 4px !important;
    font-size: 13px !important;
    -unity-font-style: bold !important;
}
/* и т.д. для всех элементов */
```

```yaml
# MyWindowPanelSettings.asset (если нужен отдельный — иначе используем существующий)
# Через Inspector: Theme Style Sheet = UnityDefaultRuntimeTheme
# Через Editor script:
#   var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss");
#   so.FindProperty("themeUss").objectReferenceValue = theme;
```

---

## 4. Layout gotchas (когда 5 фатальных OK, а всё ещё криво)

### 4.1. Кнопка/лейбл вылезает за границу панели

**Причина:** у ребёнка `width: 100%`, но родитель — `flex-column`. По дефолту `align-self` ребёнка = `auto` = `stretch` только в направлении родителя.

```css
/* ✅ Растянуть ребёнка по ширине в flex-column */
.stat-row {
    align-self: stretch !important;  /* или width: 100% */
    min-width: 0 !important;          /* для длинных значений */
}
```

### 4.2. Табы в столбик вместо строки

**Причина:** `flex-grow: 1` без `flex-basis: 0` → min-size = content size → не влезают в строку → wrap.

```css
.tab-btn {
    flex-grow: 1 !important;
    flex-basis: 0 !important;        /* ✅ делит поровну */
    flex-shrink: 1 !important;
}
```

### 4.3. Список/контент сжимается до 0

**Причина:** `flex-shrink: 1` + `min-height: 0` → сжимается до нуля в flex-column.

```css
.list-section {
    flex-grow: 1 !important;
    flex-shrink: 1 !important;
    min-height: 0 !important;        /* ✅ для scroll внутри */
    overflow: hidden !important;
}
```

### 4.4. Кнопки/header «уезжают» за нижнюю границу

**Причина:** контент-секция с `flex-grow: 1` выдавливает actions за границу.

```css
/* ✅ actions/message всегда видны */
.actions {
    flex-shrink: 0 !important;
}
```

### 4.5. Контент overflow в окне

**Причина:** нет scroll, нет flex-shrink. Решение: внутри `.list-section` ставим ListView (`fixedItemHeight` обязателен для scroll).

### 4.6. Полупрозрачный фон — видны другие UI

**Причина:** `background-color: rgba(r,g,b,0.95)` — 5% прозрачности.

```css
/* ✅ Полностью непрозрачный */
.character-window {
    background-color: rgb(20, 25, 35);  /* БЕЗ alpha */
}
```

---

## 5. Чек-лист для нового UI окна

Когда создаёшь новое UI Toolkit окно:

1. ☐ Создать GameObject `[MyWindow]` на сцене (или через префаб)
2. ☐ Добавить `UIDocument` (или авто через `[RequireComponent]`)
3. ☐ Назначить `UIDocument.visualTreeAsset` → `MyWindow.uxml`
4. ☐ Назначить `UIDocument.panelSettings` → `MyWindowPanelSettings.asset`
5. ☐ Назначить `MyWindowPanelSettings.themeUss` → `UnityDefaultRuntimeTheme.tss`
6. ☐ Назначить `MyWindow.cs.myUxml` → `MyWindow.uxml`
7. ☐ Назначить `MyWindow.cs.myUss` → `MyWindow.uss`
8. ☐ Написать `.uxml` со всеми нужными `name=` (для `Q<T>()`)
9. ☐ Написать `.uss` с `!important` на каждом свойстве
10. ☐ НЕ использовать `Resources.Load<StyleSheet>` fallback
11. ☐ НЕ делать `Clear() + CloneTree()` в `EnsureBuilt`
12. ☐ Управлять видимостью через `_container` или `_root.style.display`
13. ☐ De-dup подписок: `_btn.clicked -= handler; _btn.clicked += handler;`
14. ☐ Play Mode: проверить `styleSheets.count > 0`, `rootVE.childCount > 0`, размеры кнопок

---

## 6. Диагностические команды (Editor, через MCP)

```csharp
// 1) Найти все UIDocuments и их состояние
var docs = Resources.FindObjectsOfTypeAll<UIDocument>();
foreach (var d in docs) {
    Debug.Log($"GO={d.gameObject.name} vta={d.visualTreeAsset?.name} ps={d.panelSettings?.name} childCount={d.rootVisualElement?.childCount}");
}

// 2) Найти все PanelSettings без темы
var allPs = AssetDatabase.FindAssets("t:PanelSettings");
foreach (var g in allPs) {
    var p = AssetDatabase.GUIDToAssetPath(g);
    var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(p);
    var so = new SerializedObject(ps);
    var theme = so.FindProperty("themeUss").objectReferenceValue;
    if (theme == null) Debug.LogWarning($"NO THEME: {ps.name} at {p}");
}

// 3) Назначить тему для всех PanelSettings разом
var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss");
foreach (var g in AssetDatabase.FindAssets("t:PanelSettings")) {
    var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(AssetDatabase.GUIDToAssetPath(g));
    var so = new SerializedObject(ps);
    if (so.FindProperty("themeUss").objectReferenceValue == null) {
        so.FindProperty("themeUss").objectReferenceValue = theme;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ps);
    }
}
AssetDatabase.SaveAssets();

// 4) Проверить Resources.Load на мусор
var uss = Resources.Load<StyleSheet>("UI/CommPanel");
if (uss != null && uss.name == "inlineStyle") Debug.LogError("USS path conflicts with theme!");
```

---

## 7. История фиксов (для traceability)

| Дата | Окно | Баг | Фикс |
|------|------|-----|------|
| 2026-06-05 | CharacterWindow | `characterWindowUss` в инспекторе = сам UXML | Перепривязать через SerializedObject |
| 2026-06-05 | CharacterWindow | USS без `!important` | Переписать USS с `!important` |
| 2026-06-05 | CharacterWindow | `themeUss = null` | Назначить `UnityDefaultRuntimeTheme` |
| 2026-06-20 | CommPanelWindow | Те же 5 фатальных ошибок | Тот же рефакторинг по канону |

**Эти баги будут повторяться в каждом новом окне.** Этот документ — single source of truth.

---

## 8. Ссылки

- `docs/Character/Character-menu/recon_visual_bugs.md` — 5 багов UI с скриншотами
- `docs/Character/Character-menu/recon_visual_fix_plan.md` — детальный план фиксов CharacterWindow
- `docs/Character/Character-menu/refactor_log_2026-06-05.md` — финальный лог рефактора (канон)
- `docs/Markets/FIXES_HISTORY.md` — FIX 4 (pickingMode) + FIX 4b (list-section flex-shrink) для MarketWindow
- Unity 6 PanelSettings API: https://docs.unity3d.com/6000.4/Documentation/ScriptReference/UIElements.PanelSettings.html
- UI Toolkit USS specificity: https://docs.unity3d.com/6000.4/Manual/UIE-USS-Selectors.html

---

*Создано: 2026-06-20 после рефакторинга CommPanel. Если найдёшь новый UI-баг, не описанный здесь — добавь в §2 или §4, чтобы не повторять.*