# Сплайновые Ветровые Коридоры (SplineWindZone)

## Что это
`SplineWindZone` — ветровая зона, заданная **сплайном** (SplineContainer) вместо
триггерного коллайдера. Позволяет рисовать «ветровые коридоры» произвольной формы
прямо в сцене — например, ущелья, каньоны, джет-стримы между островами.

Работает **параллельно** с обычными `WindZone` (триггерными) и **не конфликтует**
с глобальным `WindManager` (из BootstrapScene).

## Отличия от обычного WindZone

| | WindZone (триггерный) | SplineWindZone (сплайновый) |
|---|---|---|
| Форма зоны | Box/Sphere Collider | Произвольный сплайн + радиус коридора |
| Детекция кораблей | `OnTriggerEnter`/`Exit` (физика) | Distance-based: расстояние до сплайна ≤ `corridorRadius` |
| Направление ветра | Только `WindZoneData.windDirection` | **AlongSpline** (по касательной) или Custom |
| Визуализация | Полупрозрачный box + стрелка | Труба вдоль сплайна + стрелки |
| Размещение | Любая сцена | **Рабочие игровые сцены** (НЕ BootstrapScene) |

## Архитектура

```
GameObject
├── SplineContainer         ← рисуешь сплайн в Scene View
└── SplineWindZone          ← новый компонент
    ├── windData            ← WindZoneData SO (тот же, что у обычных WindZone)
    ├── corridorRadius      ← радиус коридора (м)
    ├── directionMode       ← AlongSpline / Custom
    └── _shipCacheRefreshInterval ← интервал обновления кэша кораблей
```

- **Обнаружение**: каждые `_shipCacheRefreshInterval` сек кэшируются все `ShipController`
  через `FindObjectsByType`, затем в `FixedUpdate` проверяется расстояние до сплайна.
- **Применение силы**: напрямую через `ShipController.ApplyExternalForce()` —
  тот же метод, что используют обычные `WindZone`. Server-only проверка внутри.
- **WindManager не трогается**: сплайновая зона — полностью независимая система.

## Как создать

1. Создать пустой GameObject в игровой сцене.
2. Добавить компонент `SplineContainer` (Unity Splines).
3. В Scene View нарисовать сплайн инструментом Spline Editing.
4. Добавить компонент `SplineWindZone`.
5. Назначить `WindZoneData` (создать через Create → ProjectC → Ship → Wind Zone Data,
   или реюзать существующий).
6. Настроить:
   - `Corridor Radius` — ширина коридора вокруг сплайна (м).
   - `Direction Mode` — **AlongSpline** (ветер по касательной) или **Custom** (из WindZoneData).
7. Сплайн визуализируется в Scene View через Gizmos (полупрозрачная труба + стрелки).

## Режимы направления (Direction Mode)

### AlongSpline (по умолчанию)
Ветер дует **вдоль сплайна**, по касательной в ближайшей точке.
Идеально для «ветровых коридоров» — корабль, попавший в зону, сносится вдоль сплайна.

### Custom
Направление берётся из `WindZoneData.windDirection` (как у обычных WindZone).
Полезно когда нужен сплайновый коридор с фиксированным направлением ветра.

## Профили ветра (из WindZoneData)

Те же три профиля, что у обычных WindZone:
- **Constant** — постоянный ветер без изменений.
- **Gust** — порывистый ветер с синусоидальными колебаниями (параметр `gustInterval`).
- **Shear** — сила зависит от высоты (`shearGradient`, Н/м).

Сила ветра (`windForce`) в ньютонах. Направление/сила вычисляются в методе
`GetWindForceAtPosition()`, идентично оригинальному `WindZone`.

## Производительность

- Кэш `ShipController[]` обновляется раз в `_shipCacheRefreshInterval` (по умолчанию 1 сек).
- `FindObjectsByType` дорогой, но вызывается редко. При ≤50 кораблей на сцене — ОК.
- Проверка расстояния до сплайна — `SplineUtility.GetNearestPoint` — выполняется каждый
  `FixedUpdate`, но только для кораблей в кэше. O(N_ships × log(N_spline_segments)).
- Если в сцене много сплайновых зон — увеличить `_shipCacheRefreshInterval` до 2-3 сек.

## Взаимодействие с другими системами

| Система | Взаимодействие |
|---------|---------------|
| **WindManager** | НЕ трогается. Сплайн-зоны работают независимо. |
| **WindZone (триггерный)** | Параллельно. Обе зоны применяют силы через `ApplyExternalForce` — аддитивно. |
| **ShipController.ApplyWind()** | НЕ регистрируется через `RegisterWindZone`. Использует свой цикл. |
| **ShipController.ApplyGlobalWind()** | Продолжает работать как обычно. |
| **Глобальный ветер (WindManager)** | Применяется отдельно в `ApplyGlobalWind`. Суммируется со сплайн-зоной. |

## Gizmos (Scene View)

В редакторе сплайновая зона визуализируется:
- **Труба** из полупрозрачных колец вдоль сплайна (радиус = `corridorRadius`).
- **Стрелки** направления ветра (цвет: синий → красный по силе).
- **Подпись** с именем, профилем и силой (N).

Требует активный Gizmos в Scene View.

## Проверка

1. Создать сплайновую зону в WorldScene (см. «Как создать»).
2. В Play (host/server): подлететь кораблём к сплайну.
3. Корабль должен получить снос вдоль сплайна (если `AlongSpline`) или в направлении
   `windDirection` (если `Custom`).
4. Выйти из коридора — снос должен прекратиться.
5. Убедиться, что глобальный ветер (WindManager) продолжает работать — корабль
   сносит и глобальным ветром, и сплайном одновременно (аддитивно).

## Тюнинг

- **Ширина коридора** — `corridorRadius`.
- **Сила** — `windForce` в `WindZoneData`.
- **Плавность** — больше точек в сплайне = точнее коридор.
- **Частота обновления кэша** — `_shipCacheRefreshInterval` (меньше = отзывчивее, но дороже).

## Файлы

| Файл | Назначение |
|------|-----------|
| `Assets/_Project/Scripts/Ship/SplineWindZone.cs` | Компонент сплайновой зоны ветра |
| `Assets/_Project/Scripts/Ship/WindZoneData.cs` | ScriptableObject с параметрами (общий) |
| `Assets/_Project/Scripts/Ship/WindZone.cs` | Обычная триггерная зона (параллельная система) |
| `Assets/_Project/Scripts/Core/WindManager.cs` | Глобальный ветер (BootstrapScene) |
| `docs/world/Winds/GlobalWind_Ships.md` | Документация глобального ветра |
| `docs/world/Winds/SplineWindZone.md` | Этот файл |
