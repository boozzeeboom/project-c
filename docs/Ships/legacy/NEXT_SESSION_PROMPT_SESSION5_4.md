# Промпт для Сессии 5_4: UI, Thrust Module, и Полировка

> **Контекст:** Этот документ — готовый промпт для запуска сессии 5_4 для Project C: The Clouds.

---

## ПРОМПТ НАЧАЛО

Ты начинаешь **Сессию 5_4: UI, Thrust Module, и Полировка** для проекта Project C: The Clouds.

**Ветка:** `qwen-gamestudio-agent-dev`
**Дата:** Апрель 2026
**Предыдущая сессия:** Сессия 5_3 — passive/active/overheat архитектура (✅ работает, `docs/Ships/SESSIONS_1_TO_5_3_RETROSPECTIVE.md`)

---

## ЧТО УЖЕ ГОТОВО

### Работает
- Smooth movement — корабль плавно летит, инерция, стабилизация
- 4 класса кораблей (Light/Medium/Heavy/HeavyII)
- Altitude Corridor System — коридоры высот, турбулентность, деградация
- Wind & Environmental Forces — зоны ветра, снос корабля
- Module System — yaw/pitch/lift enhancers, MODULE_ROLL
- Fuel system — расход/регенерация, дозаправка L
- **Meziy passive/active/overheat** — модули всегда установлены, пассивный +10% эффект, активный выхлоп на клавишах
- Управление между: C/V (pitch), Z/X (roll), Shift+A/D (yaw)
- WASD свободны для обычного пилотирования
- Частицы — появляются при активном выхлопе, выключены при перегреве
- Debug HUD F3 — отображается с рамкой и фоном

### НЕ работает / Отсутствует (задача сессии 5_4)
- **Нет UI для состояния между модулей** (перегрев, активность, топливо)
- **Нет MODULE_MEZIY_THRUST** (рывок вперёд/назад на Shift+W/Shift+S)
- **ModuleSlot Inspector валидация** — только warning, не блокирует (P2, открыт)
- **Нет индикации перегрева** — игрок не знает когда модуль остывает

---

## ЗАДАЧА СЕССИИ 5_4

### 1. Meziy Status UI (HUD overlay)

**Где:** Поверх Game View, правый нижний угол или рядом с HUD F3
**Что отображать для каждого между модуля:**

| Модуль | Индикатор |
|--------|-----------|
| MODULE_MEZIY_PITCH | 🟢 Passive | 🔵 Active | 🔴 Overheated |
| MODULE_MEZIY_ROLL | 🟢 Passive | 🔵 Active | 🔴 Overheated |
| MODULE_MEZIY_YAW | 🟢 Passive | 🔵 Active | 🔴 Overheated |

**Дополнительно:**
- Прогресс-бар перегрева (0-10 сек до перегрева)
- Прогресс-бар кулдауна (15 сек → 0)
- Текущий уровень топлива (fuel bar)

**Реализация:**
- Canvas → UI Text + Image элементы
- ShipDebugHUD расширяется или новый компонент `MeziyStatusHUD.cs`
- Читаем из `meziyActivator.GetState(moduleId)` каждый кадр

### 2. MODULE_MEZIY_THRUST — рывок вперёд/назад

**Новый модуль:** `MODULE_MEZIY_THRUST` (Propulsion, тир 2)
**Параметры:**
```
meziyForce = 800        // Сила рывка
meziyDuration = N/A     // continuous mode — пока зажата клавиша
meziyCooldown = 10s     // после перегрева
meziyFuelCost = 6       // Стоимость (повышенный расход)
```

**Управление:**
- `Shift+W` — ускорение вперёд (meziy thrust boost)
- `Shift+S` — торможение (meziy reverse thrust)

**Эффект:**
- Пассивный: бесплатный множитель тяги (+10% к thrustForce)
- Активный: дополнительный thrust = `meziyForce * dt` + частицы + расход
- Перегрев: 10 сек → кулдаун 10 сек, штраф топлива

**Визуал:**
- Те же MeziyThrusterVisual но с более интенсивным пламенем
- Можно использовать существующий визуал или добавить отдельный

**Реализация:**
- Добавить `MODULE_MEZIY_THRUST.asset` (через Editor или вручную)
- ShipController секция 1.85: добавить обработку Shift+W/S
- MeziyModuleActivator: новый case "MODULE_MEZIY_THRUST"
- ApplyMeziyEffects: применить thrust boost к Rigidbody

### 3. Исправить ModuleSlot Inspector валидацию

**Текущий баг:** `docs/bugs/SESSION4_MODULESLOT_TYPE_VALIDATION.md` — можно перетащить любой модуль в Inspector.

**Решение:** Вариант B из баг-репорта — уже реализован `OnValidate()`, но он только warning и очищает поле. Нужно:
- Убедиться что `OnValidate()` срабатывает
- Добавить `[CustomPropertyDrawer]` для более красивой валидации (опционально)

### 4. Полировка клавиш Input System

**Задача:** Убедиться что клавиши V, X корректно маппятся в Input System.

**Проверить:**
- `KeyCodeToKey(KeyCode.V)` → `Key.V` ✅ (уже есть)
- `KeyCodeToKey(KeyCode.X)` → `Key.X` ✅ (уже есть)
- RightShift тоже работает для между активации

---

## АГЕНТЫ ДЛЯ ВЫЗОВА

### 1. @unity-specialist — UI и Input System
**Задачи:**
- Создать `MeziyStatusHUD.cs` — UI overlay с индикаторами
- Проверить Input System маппинг для V/X/RightShift
- Добавить CustomPropertyDrawer для ModuleSlot (опционально)

### 2. @engine-programmer — MODULE_MEZIY_THRUST
**Задачи:**
- Создать `MODULE_MEZIY_THRUST.asset`
- Добавить обработку Shift+W/S в ShipController секция 1.85
- Реализовать thrust boost в ApplyMeziyEffects
- Добавить passive thrust multiplier в GetPassiveModifier

### 3. @qa-tester — проверка
**Задачи:**
- Проверить UI отображение
- Проверить MODULE_MEZIY_THRUST
- Проверить RightShift работу
- Проверить ModuleSlot валидацию

---

## СВЯЗАННЫЕ ФАЙЛЫ (ЧИТАТЬ ПЕРЕД РАБОТОЙ)

| Файл | Зачем |
|------|-------|
| `docs/Ships/SESSIONS_1_TO_5_3_RETROSPECTIVE.md` | Полная история багов и решений |
| `docs/Ships/SESSION_5_2_COMPLETE.md` | Что работает/сломано после 5_2 |
| `docs/bugs/SESSION4_MODULESLOT_TYPE_VALIDATION.md` | Баг валидации слота |
| `Assets/_Project/Scripts/Ship/MeziyModuleActivator.cs` | Текущая реализация |
| `Assets/_Project/Scripts/Player/ShipController.cs` | v2.6 — основной файл |
| `Assets/_Project/Scripts/Ship/ShipDebugHUD.cs` | HUD для расширения |
| `Assets/_Project/Data/Modules/MODULE_MEZIY_*.asset` | Параметры модулей |

---

## ПОШАГОВЫЙ ПЛАН СЕССИИ

### Шаг 1: Meziy Status UI (30 мин)
```
1. Создать MeziyStatusHUD.cs — canvas overlay
2. Для каждого модуля: status indicator (🟢/🔵/🔴)
3. Прогресс-бар перегрева (fill amount)
4. Топливо bar
5. Проверить в Play Mode
```

### Шаг 2: MODULE_MEZIY_THRUST (30 мин)
```
1. Создать MODULE_MEZIY_THRUST.asset
2. Добавить обработку Shift+W/S в ShipController
3. Реализовать thrust boost в ApplyMeziyEffects
4. Добавить passive thrust multiplier
5. Проверить в Play Mode
```

### Шаг 3: ModuleSlot валидация (10 мин)
```
1. Проверить OnValidate() в ModuleSlot.cs
2. При необходимости усилить валидацию
3. Проверить в Inspector
```

### Шаг 4: Тестирование и коммит
```
1. Проверить все 4 между модуля (PITCH, ROLL, YAW, THRUST)
2. Проверить UI overlay
3. Проверить ModuleSlot валидацию
4. Git commit
```

---

## КРИТЕРИИ ПРИЁМКИ СЕССИИ 5_4

| Критерий | Метрика | Pass/Fail |
|----------|---------|-----------|
| Meziy Status UI виден в Game View | 3 индикатора + топливо | ☐ |
| Перегрев отображается (🔴 + прогресс) | UI обновляется каждый кадр | ☐ |
| Кулдаун отображается (обратный отсчёт) | 15 сек → 0 | ☐ |
| MODULE_MEZIY_THRUST создан | ScriptableObject существует | ☐ |
| Shift+W → ускорение вперёд | thrust boost + частицы | ☐ |
| Shift+S → торможение | reverse thrust + частицы | ☐ |
| Passive thrust multiplier (+10%) | тяга увеличена | ☐ |
| RightShift работает для активации | LShift и RShift равнозначны | ☐ |
| ModuleSlot валидация блокирует | несовместимый модуль не ставится | ☐ |
| Компиляция без ошибок | Unity Console = 0 errors | ☐ |

---

## ВАЖНЫЕ ПРЕДОСТЕРЕЖЕНИЯ

### НЕ ЛОМАТЬ
- **RPC сигнатуры:** SubmitShipInputRpc, AddPilotRpc, RemovePilotRpc
- **Fuel система:** расход/регенерация уже работает
- **Thrust:** корабль летит вперёд — не менять базовую логику
- **Meziy passive/active архитектура:** уже работает, только расширять
- **Input System:** IsKeyDown() работает для всех клавиш

### ПРОВЕРЯТЬ
- **Компиляцию в Unity** после каждого изменения
- **UI работает в Game View** (не только в Scene)
- **Shift+W/S НЕ конфликтует** с обычным thrust от W

---

## ИНСТРУКЦИЯ ПО ТЕСТИРОВАНИЮ (для пользователя)

### 1. Тест UI
```
1. Запустить Unity → Play Mode
2. Убедиться что Meziy Status UI виден в Game View
3. Зажать C → PITCH индикатор меняется на 🔵 Active
4. Зажать 10+ сек → индикатор 🔴 Overheated, прогресс-бар кулдауна
```

### 2. Тест MODULE_MEZIY_THRUST
```
1. Убедиться что модуль установлен в ModuleSlot
2. Зажать Shift+W → корабль ускоряется вперёд, частицы, fuel уходит
3. Зажать Shift+S → корабль тормозит, частицы
4. Отпустить → обычный thrust возвращается
```

### 3. Тест ModuleSlot валидации
```
1. Выбрать корабль в сцене
2. Добавить ModuleSlot с типом Utility
3. Перетащить Propulsion модуль → warning + очистка поля
```

---

*Промпт подготовлен на основе: SESSIONS_1_TO_5_3_RETROSPECTIVE.md, ShipRegistry.md*
*Текущая версия ShipController: v2.6 | Следующая версия после 5_4: v2.7*
