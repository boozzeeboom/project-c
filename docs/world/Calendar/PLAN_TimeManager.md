# TimeManager — План реализации

**Дата:** 2026-07-27
**Статус:** ✅ Реализовано (6/6 итераций)

---

## 1. Цели

1. **Календарь:** добавить реальный календарь (день недели, число, месяц, год) поверх существующего `_timeOfDay` в `ServerWeatherController`.
2. **Персистенция:** сохранять/загружать время на сервере (по образцу `ShipPositionServer`).
3. **UI:** заполнить существующий `time-info-label` в хедере `CharacterWindow`.
4. **События:** предоставить входные точки для подписок: смена дня, недели, месяца, фазы (уже есть).
5. **Не сломать:** все существующие потребители должны работать без изменений.

---

## 2. Текущее состояние (см. DEEP_ANALYSIS.md)

### 2.1 ServerWeatherController
- `_timeOfDay` (float, 0-24)
- `TotalGameDays` (float, только инкремент)
- НЕТ календаря, НЕТ персистенции
- Синглтон, NetworkBehaviour, в BootstrapScene

### 2.2 Потребители (не трогаем)
| Потребитель | Что читает | Как |
|-------------|-----------|-----|
| DayNightController | `TimeOfDay` | Событие + polling |
| ConstellationController | `TimeOfDay` | Событие |
| MoonController | `TimeOfDay`, `TotalGameDays` | Событие + polling |
| MarketTimeService | `TimeOfDay` (опционально) | Ссылка на Instance |
| QuestServer | `DayNightPhaseChangedEvent` | WorldEventBus |
| Диалоги | `TimeOfDay` через DayNightController | `DialogueConditionType.TimeOfDayIn` |

### 2.3 UI
- `CharacterWindow._timeInfoLabel` — лейбл в UXML есть, но не заполняется
- `MarketWindow._timeInfoLabel` — заполняется (`"Скорость рынка: x1.0 | Тик через: 42с"`)

### 2.4 Персистенция (образец)
- `ShipPositionServer` → `JsonShipPositionRepository` → `ShipPositions.json`
- Save каждые 5 сек, Restore через 3.5s после старта сервера

---

## 3. Архитектурное решение

### 3.1 Где живёт время
**Решение:** Расширить `ServerWeatherController` (не создавать новый компонент).

**Обоснование:**
- Single source of truth для времени уже здесь
- 7 потребителей уже ссылаются на `ServerWeatherController.Instance`
- Добавление календаря — это расширение данных, а не новая ответственность
- NetworkBehaviour → время автоматически синхронизируется

### 3.2 Структура данных: `GameTimeData`

```csharp
[System.Serializable]
public struct GameTimeData
{
    public int Year;          // например, 1200 (игровой год)
    public int Month;         // 1-12
    public int Day;           // 1-30 (30 дней в игровом месяце)
    public int DayOfYear;     // 1-360
    public int Weekday;       // 0-6 (названия: "Manday", "Tirsday", "Wotanday", "Thorsday", "Freyday", "Saturnight", "Sunsrest")
    public float HourOfDay;   // 0-24 (то же что _timeOfDay)
}
```

### 3.3 Логика календаря

- 1 игровой день = 24 игровых часа
- 1 игровой месяц = 30 игровых дней (ровно, без вариаций)
- 1 игровой год = 12 месяцев = 360 дней
- Смена дня: когда `_timeOfDay` переваливает через 24→0
- `TotalGameDays` становится целочисленным счётчиком дней (сейчас float, но используется как floor)

### 3.4 WorldEvent'ы (новые)

```csharp
// Уже есть:
DayNightPhaseChangedEvent  // смена фазы (Morning→Midday→...)

// Добавить:
GameDayChangedEvent        // новый игровой день (рассвет)
GameWeekChangedEvent       // новая неделя
GameMonthChangedEvent      // новый месяц
GameYearChangedEvent       // новый год
```

### 3.5 Персистенция

По образцу `ShipPositionServer`:
- `ITimeRepository` интерфейс + `JsonTimeRepository`
- Сохранять в `Application.persistentDataPath/time_state.json`
- Save: каждые 30 секунд (время меняется медленно, не нужно 5с)
- Restore: при `OnServerStarted`

---

## 4. План по фазам

### Фаза 1: GameTimeData + календарь в ServerWeatherController

**Файлы:**
- `Assets/_Project/Scripts/Core/GameTimeData.cs` — новый файл (структура)
- `Assets/_Project/Scripts/Core/ServerWeatherController.cs` — расширить

**Изменения в ServerWeatherController:**
```csharp
// Добавить поля:
[Header("Calendar")]
[SerializeField] private GameTimeData _gameTime = new GameTimeData { Year = 1, Month = 1, Day = 1, Weekday = 0 };

// Свойства:
public GameTimeData CurrentGameTime => _gameTime;
public int CurrentYear => _gameTime.Year;
public int CurrentMonth => _gameTime.Month;
public int CurrentDay => _gameTime.Day;
public int CurrentWeekday => _gameTime.Weekday;

// События (добавить к существующим):
public event System.Action<GameTimeData> OnCalendarChanged;

// Логика в Update:
// При _timeOfDay >= 24f: продвинуть календарь на 1 день
// Пересчитать Day, Weekday, Month, Year
```

**Ключевой момент:** `_timeOfDay` остаётся главным счётчиком. Календарь обновляется из него. Это гарантирует обратную совместимость.

### Фаза 2: Новые WorldEvent'ы

**Файлы:**
- `Assets/_Project/Core/WorldEvent.cs` — добавить 4 новых класса

```csharp
public sealed class GameDayChangedEvent : WorldEvent
{
    public int Day;
    public int Month;
    public int Year;
    public int Weekday;
}

public sealed class GameWeekChangedEvent : WorldEvent
{
    public int Day;
    public int Month;
    public int Year;
}

public sealed class GameMonthChangedEvent : WorldEvent
{
    public int Month;
    public int Year;
}

public sealed class GameYearChangedEvent : WorldEvent
{
    public int Year;
}
```

**Publisher:** `ServerWeatherController` при продвижении календаря.
**Subscribers:** QuestServer (для квестовых триггеров), любые другие системы.

### Фаза 3: Персистенция

**Файлы:**
- `Assets/_Project/Scripts/Core/TimePersistence/ITimeRepository.cs`
- `Assets/_Project/Scripts/Core/TimePersistence/JsonTimeRepository.cs`

**Логика:**
- Сохраняем `GameTimeData` + `_timeOfDay` в JSON каждые 30 сек
- При старте сервера: загружаем, применяем, broadcast'им клиентам
- Старый файл несовместимого формата → перезаписываем (graceful degradation)

### Фаза 4: Интеграция в CharacterWindow

**Файлы:**
- `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` — заполнить `_timeInfoLabel`

**Отображение:**
```
"Thorsday, 14-й день Весны, год 1200 | 14:32 | ☀️ Полдень"
```

**Источник данных:** `ServerWeatherController.Instance.CurrentGameTime` + `TimeOfDay` + `DayNightController.Instance.CurrentPhase?.phaseName`

**Обновление:** 
- При открытии окна (Show)
- По событию `OnTimeOfDayChanged` (периодический polling через `UpdateTimeDisplay`)

### Фаза 5: Триггеры для квестов

**Файлы:**
- `Assets/_Project/Quests/Triggers/ConcreteTriggers.cs` — добавить `GameDayTrigger`, `GameWeekdayTrigger`, `GameMonthTrigger`

```csharp
public sealed class GameDayTrigger : IQuestTrigger { public int RequiredDay; }
public sealed class GameWeekdayTrigger : IQuestTrigger { public int RequiredWeekday; }
public sealed class GameMonthTrigger : IQuestTrigger { public int RequiredMonth; }
```

**Интеграция с QuestServer:** подписаться на новые события (как уже сделано для `DayNightPhaseChangedEvent`).

---

## 5. Файлы (создать / изменить)

| Файл | Действие |
|------|---------|
| `Assets/_Project/Scripts/Core/GameTimeData.cs` | **Создать** |
| `Assets/_Project/Scripts/Core/ServerWeatherController.cs` | **Изменить** (календарь + события) |
| `Assets/_Project/Core/WorldEvent.cs` | **Изменить** (4 новых класса) |
| `Assets/_Project/Scripts/Core/TimePersistence/ITimeRepository.cs` | **Создать** |
| `Assets/_Project/Scripts/Core/TimePersistence/JsonTimeRepository.cs` | **Создать** |
| `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` | **Изменить** (`_timeInfoLabel`) |
| `Assets/_Project/Quests/Triggers/ConcreteTriggers.cs` | **Изменить** (3 новых триггера) |
| `Assets/_Project/Quests/Network/QuestServer.cs` | **Изменить** (подписки) |

---

## 6. Проверка обратной совместимости

| Контракт | Как сохраняем |
|----------|--------------|
| `TimeOfDay` (float) | Не трогаем, та же логика |
| `TotalGameDays` (float) | Остаётся, дополняем `CurrentGameTime.Day` |
| `OnTimeOfDayChanged` (event) | Не трогаем |
| `OnTemperatureChanged` (event) | Не трогаем |
| `BroadcastTimeOfDayClientRpc` | Не трогаем |
| `SetTimeOfDayServerRpc` | Расширяем: теперь обновляет и календарь |
| `DayNightPhaseChangedEvent` | Не трогаем (публикует DayNightController) |

---

## 7. Тест-план

1. **Запуск сервера** → проверить `GameTimeData` (должен быть Year=1, Month=1, Day=1 или загруженный)
2. **Авто-продвижение** → дождаться смены дня, проверить Weekday/Day/Month
3. **Broadcast на клиент** → проверить `OnCalendarChanged` событие
4. **UI** → открыть CharacterWindow (P) → проверить `time-info-label`
5. **Персистенция** → остановить/запустить сервер → время должно восстановиться
6. **Квесты** → DayNightPhaseTrigger всё ещё работает

---

## 8. Итерации

| # | Что | Оценка |
|---|-----|--------|
| 1 | GameTimeData + календарь в ServerWeatherController | ⬜ |
| 2 | WorldEvent'ы (Day/Week/Month/Year) | ⬜ |
| 3 | Персистенция (ITimeRepository + JsonTimeRepository) | ⬜ |
| 4 | CharacterWindow — отображение времени | ⬜ |
| 5 | Квестовые триггеры (GameDay/Weekday/Month) | ⬜ |
| 6 | Интеграция с QuestServer | ⬜ |
