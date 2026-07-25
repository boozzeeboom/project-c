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
GameObject (в игровой сцене, например world0_0)
├── SplineContainer         ← рисуешь сплайн в Scene View
└── SplineWindZone          ← пассивный дескриптор (НЕТ FixedUpdate)
    ├── windData            ← WindZoneData SO (тот же, что у обычных WindZone)
    ├── corridorRadius      ← радиус коридора (м)
    ├── directionMode       ← AlongSpline / Custom
    ├── reverseDirection    ← разворот потока на 180°
    └── centeringStrength   ← сила притяжения к центру трубы

WindManager (BootstrapScene, DontDestroyOnLoad)
├── FixedUpdate → ProcessSplineWindZones()
│   ├── Снапшот AllShips (lock+copy, один раз на кадр)
│   ├── Round-robin: детекция 1 зоны за FixedUpdate
│   │   └── GetNearestPoint × корабли → кэш _zoneStates[zone].entries
│   └── ApplyAllCachedForces: для ВСЕХ зон из кэша → AddForce
└── _splineZonesPerFrame = 1 (настройка агрессивности)
```

- **Обнаружение**: WindManager делает снапшот статического `SplineWindZone.AllShips`
  (корабли регистрируются при спавне) → round-robin по `AllZones` → одна зона за кадр.
- **Применение силы**: `ShipController.ApplyExternalForce()` — тот же метод, что у обычных WindZone.
- **SplineWindZone — пассивный**: только данные + Gizmos. Никакой своей логики в FixedUpdate.


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

- **ApplyAllCachedForces** каждый FixedUpdate — O(зоны × корабли_в_зоне), <0.5ms.
- **Round-robin + per-zone throttling:** `_splineDetectionStep=5` — каждая зона детектится раз в 5 вызовов (~10 Гц).
  Снапшот кораблей (lock+copy) только в кадре детекции.
- **Детекция:** `GetNearestPoint` × корабли — только для выбранной зоны. O(N_ships × log(segments)).
- При 2 зонах и 50 кораблях: средняя ~**1ms/кадр** (4 из 5 кадров — 0.5ms, 1 из 5 — ~3ms).
- Много зон (>5): увеличить `_splineZonesPerFrame` или уменьшить `_splineDetectionStep`.



## Взаимодействие с другими системами

| Система | Взаимодействие |
|---------|---------------|
| **WindManager** | Центральный дирижёр — читает `AllZones`, процессит в `FixedUpdate`. |
| **WindZone (триггерный)** | Параллельно. Обе зоны применяют силы через `ApplyExternalForce` — аддитивно. |
| **ShipController.ApplyWind()** | Работает как обычно (триггерные зоны через `RegisterWindZone`). |
| **ShipController.ApplyGlobalWind()** | Глобальный ветер. Суммируется со сплайн-зонами аддитивно. |
| **ShipController.AllShips** | Статический реестр на `SplineWindZone`. Корабли регистрируются при спавне. |


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
- **Агрессивность детекции** — `_splineZonesPerFrame` в WindManager (1 = round-robin экономно, all = каждая зона каждый кадр).
- **Fixed Timestep** — уменьшить для более плавного ветра (0.01с вместо 0.02с), ценой CPU.


## Файлы

| Файл | Назначение |
|------|-----------|
| `Assets/_Project/Scripts/Ship/SplineWindZone.cs` | Пассивный дескриптор сплайновой зоны (данные + Gizmos) |
| `Assets/_Project/Scripts/Core/WindManager.cs` | Центральный дирижёр: глобальный ветер + round-robin SplineWindZone |
| `Assets/_Project/Scripts/Ship/WindZoneData.cs` | ScriptableObject с параметрами (общий) |
| `Assets/_Project/Scripts/Ship/WindZone.cs` | Обычная триггерная зона (параллельная система) |
| `docs/world/Winds/GlobalWind_Ships.md` | Документация глобального ветра |
| `docs/world/Winds/SplineWindZone.md` | Этот файл |
| `docs/world/Winds/ITERATIONS.md` | История итераций |

