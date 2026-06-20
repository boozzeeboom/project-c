# Docking System — Refactor Plan
> Проект C: The Clouds | Поэтапный план исправления и доработки
> Создан: 2026-06-20

## Фаза 0: Быстрые фиксы P0 (1-2 часа)

> Цель: система компилируется, SO создаётся с корректным m_Script, UI видим и кликабелен.

### Шаг 0.1 — Исправить DockingAssetCreator.cs
- [ ] Удалить строки `so2.FindProperty("commRange")` и `so2.FindProperty("stationShipFlightClass")` из `CreateStationDef()`
- [ ] Изменить поиск MonoScript: вместо `MonoScript.FromScriptableObject(def)` использовать `AssetDatabase.FindAssets("t:MonoScript DockStationDefinition")` и явно назначить `m_Script`
- [ ] Альтернатива: добавить `using ProjectC.Docking.Core;` и использовать `ScriptableObject.CreateInstance<DockStationDefinition>()`
- [ ] Компилировать → проверить отсутствие ошибок

### Шаг 0.2 — Пересоздать SO ассеты
- [ ] Запустить `DockingAssetCreator.RecreateAll()` через MCP `execute_code`
- [ ] Проверить `grep "m_Script" *.asset` → должен быть корректный GUID
- [ ] Перепривязать новый SO к `DockStation_Primium` GameObject в WorldScene_0_0 через SerializedProperty

### Шаг 0.3 — Исправить CommPanelWindow.cs
- [ ] Добавить `_doc.sortingOrder = 10;` в конец `EnsureBuilt()`
- [ ] Убрать `_root.pickingMode = PickingMode.Ignore;` (строка 144)
- [ ] В `SetOpen()` добавить `MarkDirtyRepaint()` для форсирования USS
- [ ] Компилировать → проверить

### Шаг 0.4 — Проверка Play Mode
- [ ] Открыть WorldScene → Play Mode
- [ ] Проверить консоль: нет ошибок `dockStationDefinition is null`, нет `no StationId`
- [ ] Подлететь к станции → нажать T → CommPanel виден, кликабелен
- [ ] **Не проверять стыковку** (RPC flow может ещё не работать — Phase 1)

---

## Фаза 1: Функциональные фиксы (2-3 часа)

> Цель: полный цикл "запросить посадку → подтвердить → приземлиться → отстыковаться" работает.

### Шаг 1.1 — DockingServer.Instance timing fix
- [ ] В `DockingPadTriggerBox.OnTriggerEnter`: если `DockingServer.Instance == null`, запустить coroutine retry (3 попытки × 0.5с delay)
- [ ] В `DockingServer.OnNetworkSpawn`: убедиться, что Instance устанавливается до регистрации коллбэков

### Шаг 1.2 — DockingServer в DontDestroyOnLoad
- [ ] Перенести DockingServer из BootstrapScene в WorldScene или добавить DontDestroyOnLoad
- [ ] Либо: проверить, что BootstrapScene не выгружается

### Шаг 1.3 — Check DockingWorld.ConfirmTouchdown для WrongPad
- [ ] Если игрок коснулся pad'а, который ему НЕ назначен → статус WrongPad
- [ ] Проверить, что CommPanel отображает "Борт, вы на чужом pad'е (#{padId}). Перепаркуйтесь."
- [ ] Проверить, что pad НЕ блокируется (не входит в occupied)

---

## Фаза 2: Документация зон и доработка сцены

> Согласно начальному описанию: 2 зоны, диалог, сканирование, триггербоксы.

### Шаг 2.1 — Разметка зон
- [ ] **OuterCommZone** (большая сфера, ~1000м) — уже есть, используется
- [ ] **DockingPadTriggerBox** (BoxCollider на каждый pad) — уже есть
- [ ] Проверить, что BoxCollider по размеру `.triggerBoxSize`
- [ ] Настроить Gizmos для отладки

### Шаг 2.2 — Система диалога с диспетчером
- [ ] CommPanel уже реализует: приветствие → запрос → назначение → подтверждение
- [ ] Проверить все состояния: Greeting, Assigning, Assigned, AwaitingConfirmation, Touchdown, WrongPad, Takeoff, WindowExpired, Occupied
- [ ] Voice lines из `DispatcherVoiceLines` SO

### Шаг 2.3 — Сканирование площадок и тип корабля
- [ ] `DockingWorld.AssignPad` проверяет `ShipFlightClass` против `PadDefinition.compatibleShipClasses`
- [ ] Если пустой массив → совместим со всеми
- [ ] Если не подходит → статус "NO_SUITABLE_PAD"

---

## Фаза 3 (Phase 2): Автопилот и маршрутизация

> За рамками MVP, архитектурная заметка.

- [ ] Новый компонент `AutopilotController` на корабле
- [ ] `DockingAssignmentDto` содержит `approachPoint`, `approachAltitude`, `approachHeading`
- [ ] После подтверждения — автопилот ведёт к pad'у
- [ ] Подсистема Departure (T-DEPART-*) — вылет из зоны через запрос

---

## Критерий готовности MVP

1. Нет красных ошибок в консоли при старте сцены
2. T открывает CommPanel ПОВЕРХ HUD
3. Кнопки кликабельны
4. [Запросить посадку] → диспетчер назначает pad №5
5. [Хорошо] → статус Assigned, прогресс-бар
6. Вход в DockingPadTriggerBox → Docked
7. Отстыковка → завершено
8. Вход на чужой pad → WrongPad toast
