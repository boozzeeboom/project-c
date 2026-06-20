# Docking System — Bug Audit
> Проект C: The Clouds | Все найденные проблемы с корневыми причинами
> Создан: 2026-06-20

## P0 — Полный блокер (без фикса система не работает)

### B-001: SO asset corruption — `m_Script: {fileID: 0}`

**Симптом**: `[DockStationController:DockStation_Primium] dockStationDefinition is null!`
→ `[DockingZoneRegistry] station DockStation_Primium has no StationId`

**Корневая причина**: `DockingAssetCreator.CreateStationDef()` создаёт SO через `ScriptableObject.CreateInstance(Type)` где Type получен через `System.Type.GetType("...", Assembly-CSharp)`. В Unity 6 `CreateInstance(Type)` не проставляет корректный `m_Script` GUID в .asset мета-дату. Последующая попытка `MonoScript.FromScriptableObject(def)` возвращает null, т.к. MonoScript не может быть найден для типа, созданного runtime.

**Файл**: `Assets/_Project/Editor/DockingAssetCreator.cs:25-45`
**Ассет**: `Assets/_Project/Docking/Resources/Data/DockStationDefinition_Primium.asset`

**Доказательство**: после `RecreateAll()` `grep "m_Script" .asset` → `{fileID: 0}`.

**Решение (3 варианта)**:
1. **(Рекомендуется)** Использовать `ScriptableObject.CreateInstance<DockStationDefinition>()` с compile-time типом, добавив `using ProjectC.Docking.Core;`. Затем найти MonoScript через `MonoBehaviour.FromScriptableObject(def)` — будет работать, т.к. runtime тип совпадает.
2. Найти MonoScript через `AssetDatabase.FindAssets("t:MonoScript DockStationDefinition")` и назначить через SerializedProperty.
3. Создать SO вручную через Unity Editor → Assets > Create > ProjectC > Docking > DockStationDefinition.

### B-002: NullReferenceException в DockingAssetCreator

**Симптом**: При запуске `RecreateAll()` ошибка NullReferenceException на строке 53.

**Корневая причина**: В `CreateStationDef()` строки:
```csharp
so2.FindProperty("commRange").floatValue = 1000;        // строка 53
so2.FindProperty("stationShipFlightClass").enumValueIndex = 0; // строка 54
```
Эти полей НЕТ в классе `DockStationDefinition`! `commRange` — поле `OuterCommZone`, `stationShipFlightClass` нигде не существует. `FindProperty()` возвращает null → NullReferenceException.

**Файл**: `Assets/_Project/Editor/DockingAssetCreator.cs:53-54`

**Статус**: Исправлено в версии автора (удалены строки), но НЕ откомпилировано и не запущено.

### B-003: UI sortingOrder не задан

**Симптом**: «за HUD панелью» — UI рисуется ПОЗАДИ HUD.

**Корневая причина**: В `CommPanelWindow.EnsureBuilt()` нет `_doc.sortingOrder = 10;`. По умолчанию `sortingOrder = 0`, HUD-панели имеют >0. UIDocument рисуется как камера overlay, порядок = sortingOrder.

**Файл**: `Assets/_Project/Scripts/Docking/UI/CommPanelWindow.cs:115-163`

**Решение**: Добавить `_doc.sortingOrder = 10;` в конец `EnsureBuilt()`.

### B-004: PickingMode.Ignore на TemplateContainer

**Симптом**: «не жмется» — кнопки в CommPanel не реагируют на клик.

**Корневая причина**: `_root.pickingMode = PickingMode.Ignore;` (стока 144). TemplateContainer с Ignore может блокировать picking для всех вложенных элементов в Unity 6.

**Файл**: `Assets/_Project/Scripts/Docking/UI/CommPanelWindow.cs:144`

**Решение**: Убрать `_root.pickingMode = PickingMode.Ignore;`. Если нужен клик-сквозь пустую область, установить `pickingMode = PickingMode.Ignore` на фоновый элемент (VisualElement#root), НЕ на TemplateContainer.

### B-005: USS не применяется на TemplateContainer

**Симптом**: Верстка "вверху без стилей" — USS классы не подхватываются.

**Корневая причина (вероятная)**: В `EnsureBuilt()` после `_doc.rootVisualElement.Clear()` USS добавляется к `rootVisualElement`. Затем `_root = commPanelUxml.CloneTree()` создаёт TemplateContainer, который НЕ имеет класса `comm-panel-root` — этот класс есть на внутреннем `VisualElement#root`.

Дерево после EnsureBuilt:
```
rootVisualElement
  ├── styleSheets: [commPanelUss]  ← USS добавлен
  └── TemplateContainer                     ← TemplateContainer НЕ имеет comm-panel-root
        └── VisualElement#root (.comm-panel-root)  ← имеет, но USS selectors применяются только к его детям
```

USS `.comm-panel-root { ... }` должен применяться к `VisualElement#root` — да, это работает, селекторы по классу не зависят от TemplateContainer.

**Истинная причина**: На первом кадре USS не успевает примениться к элементам, добавленным через `CloneTree()`. Метод `ApplyInlineFallbackStyles()` пытается это исправить, но применяет стили только к `_panel`, а не ко всему дереву.

**Решение**: После `_doc.rootVisualElement.Add(_root)` вызвать `_root.RegisterCallback<GeometryChangedEvent>(...)` для форсирования layout.

## P1 — Блокирует функциональность после фикса P0

### B-006: DockingServer.Instance timing

**Симптом**: `DockingPadTriggerBox.OnTriggerEnter` → `DockingServer.Instance == null` → RPC не отправляется.

**Корневая причина**: DockingServer — scene-placed NetworkBehaviour в BootstrapScene. Известный проект-баг (см. memory): scene-placed NetworkObject'ы не гарантируют `OnNetworkSpawn` до первого Update. Если игрок подлетает к pad'у до того как DockingServer зарегистрировался, Instance будет null.

**Файл**: `Assets/_Project/Scripts/Docking/Stations/DockingPadTriggerBox.cs:53-54`

**Решение**: Добавить retry в `OnTriggerEnter` через `StartCoroutine` (3 попытки с задержкой 0.5с). Или использовать `DockingWorld.Instance` как fallback — он создаётся из DockingServer.OnNetworkSpawn и живёт в DontDestroyOnLoad.

### B-007: Дублирование FindLocalPlayer() в 3 файлах

**Simptom**: copy-paste кода, рассинхронизация.

**Файлы**:
- `OuterCommZone.cs:184-194`
- `DockingPadTriggerBox.cs:68-78`
- `CommPanelWindow.cs:532-542`

**Решение**: Вынести в хелпер (extension method или статический метод NetworkingUtils).

### B-008: DockingServer — cross-dependency на BootstrapScene

**Проблема**: DockingServer (в BootstrapScene) и DockStationController (в WorldScene) — разные сцены. DockingServer удалён от WorldScene на момент подгрузки.

**Риск**: Если BootstrapScene выгружается при загрузке WorldScene (обычный flow в MMO), DockingServer уничтожается, `Instance = null`.

**Файл**: `Assets/_Project/Scripts/Docking/Network/DockingServer.cs`
**Решение**: Перенести DockingServer в WorldScene или сделать его DontDestroyOnLoad.

## P2 — Архитектурные замечания

### B-009: DockingClientState.AutoCreate vs DockingServer.Instance

`DockingClientState` использует `[RuntimeInitializeOnLoadMethod]` для авто-создания. `DockingWorld` создаётся из `DockingServer.OnNetworkSpawn`. Если `DockingClientState` создаётся до `DockingServer`, он может не найти Instance на момент подписки.

### B-010: Нет checks на `FindLocalPlayer()` == null

`CommPanelWindow.RequestDocking()` (строка 496) вызывает `GetLocalShipNetworkObjectId()` который возвращает 0 если `localPlayer == null`. Но нет визуальной обратной связи — игрок нажал кнопку но ничего не произошло.

### B-011: .uss отключение display из двух мест

В USS `.comm-panel-progress` имеет `display: none`. В UI тоже управляется через `EnableInClassList("is-active")`. Конфликта нет, но избыточно.

---

## Сводка приоритетов для фикса

| # | Баг | Приоритет | Зависимости | Оценка |
|---|-----|-----------|-------------|--------|
| B-001 | SO m_Script: {fileID: 0} | P0 | — | 1 час |
| B-002 | NRE в DockingAssetCreator (commRange) | P0 | B-001 | 5 мин |
| B-003 | sortingOrder не задан | P0 | — | 2 мин |
| B-004 | PickingMode.Ignore на TemplateContainer | P0 | — | 2 мин |
| B-005 | USS не применяется на 1-м кадре | P1 | — | 20 мин |
| B-006 | DockingServer.Instance timing | P1 | B-001 | 30 мин |
| B-007 | Дублирование FindLocalPlayer() | P2 | — | 15 мин |
| B-008 | Cross-scene dependency Bootstrap → World | P2 | — | 1 час |

После фикса B-001..B-005 система должна:
- Не выдавать null ref на dockStationDefinition
- Иметь StationId
- Показывать CommPanel ПОВЕРХ HUD
- Кликабельные кнопки
