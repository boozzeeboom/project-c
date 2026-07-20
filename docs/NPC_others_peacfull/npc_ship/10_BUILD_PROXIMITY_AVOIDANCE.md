# 10 — Ship-to-Build Proximity Avoidance (обход зданий NPC-кораблями)

> **Project C: The Clouds** | Unity 6000.4.1f1 | NGO 2.11.0
> **Статус:** M4 — реализовано (2026-07-15)
> **Связано с:** `07_SHIP_PROXIMITY_AVOIDANCE.md` (ship-to-ship), `NpcProximityZone`, `NpcShipController`

---

## 1. Назначение

NPC-корабли должны обходить статичные препятствия (здания, портовые конструкции) тем же avoidance-манёвром, что и другие корабли. Корабль, приближаясь к avoidance-зоне здания, начинает трёхфазный манёвр расхождения, после чего возвращается на прежний курс.

**Отличие от ship-to-ship:** здания не двигаются, их avoidance-зона задаётся набором BoxCollider'ов (или Sphere/Capsule/convex-Mesh), аппроксимирующих форму здания, а не одной сферой.

---

## 2. Почему не AABB / не MeshCollider.ClosestPoint

`Collider.ClosestPoint()` в Unity **не работает с не-convex MeshCollider** — ошибка:
> *"Physics.ClosestPoint can only be used with BoxCollider, SphereCollider, CapsuleCollider and convex MeshCollider."*

`Collider.bounds` даёт AABB (осе-ориентированный бокс) — для T/Г-образных зданий это огромная коробка, захватывающая пустое пространство.

**Решение:** дизайнер вручную расставляет на здании дочерние GameObject'ы с BoxCollider'ами (или Sphere/Capsule), аппроксимируя форму. `NpcProximityZoneBuilds` собирает их и использует для avoidance.

---

## 3. Компоненты

### 3.1 `NpcBuildZoneRegistry` (новый)

`Assets/_Project/Scripts/PeacefulShip/Network/NpcBuildZoneRegistry.cs`

Статический реестр `List<NpcProximityZoneBuilds>`. Pattern: `NpcShipZoneRegistry`. Регистрация в `OnEnable`, удаление в `OnDisable`. Server-only — потокобезопасность не требуется.

### 3.2 `NpcProximityZoneBuilds` (новый)

`Assets/_Project/Scripts/PeacefulShip/Stations/NpcProximityZoneBuilds.cs`

- Вешается на корень здания (рядом с MeshCollider)
- Сканирует все Collider'ы на GameObject и детях (`includeChildren = true`)
- Использует только те, где `ClosestPoint` корректен: `BoxCollider`, `SphereCollider`, `CapsuleCollider`, convex `MeshCollider`
- **`avoidancePadding`** (float, default 30 м) — отступ от поверхности коллайдеров
- **`ClosestPoint(Vector3)`** — ближайшая точка среди всех валидных коллайдеров
- **`IsIntruding(shipPos, shipAvoidRadius)`** — проверка: дистанция до ближайшей точки < `shipAvoidRadius + avoidancePadding`
- **Gizmo:** если есть валидные коллайдеры — оранжевые wireframe'ы + метка; если нет — красная сфера с предупреждением
- **Context Menu:** `Refresh Colliders` — пересканировать (после добавления дочерних коллайдеров), `Test ClosestPoint` — проверить в редакторе

### 3.3 `NpcProximityZone` (правка)

`Assets/_Project/Scripts/PeacefulShip/Stations/NpcProximityZone.cs`

- Добавлен `[SerializeField] bool considerBuildings = true` — галочка в инспекторе
- Добавлен `FindClosestBuildConflict(out float dist)` — итерирует `NpcBuildZoneRegistry.All`, возвращает ближайшую building-зону где `IsIntruding == true`
- Добавлен `verboseBuildLogging` для диагностики

### 3.4 `NpcShipController` (правка)

`Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipController.cs`

- **Новые поля:** `_avoidBuild` (`NpcProximityZoneBuilds`), `_avoidFromPos` (`Vector3`) — единая точка для вектора «прочь»
- **`EnterAvoid(rb, other)`** — обновлён: сохраняет `_avoidFromPos`, сбрасывает `_avoidBuild`
- **`EnterAvoid(rb, build)`** — новый: сохраняет `_avoidFromPos = build.ClosestPoint(rb.position)`
- **`TickAvoid`** — использует `_avoidFromPos` вместо `_avoidOther.transform.position` (работает для обоих типов)
- **`IsClearOfConflict`** — для building: `!_avoidBuild.IsIntruding(pos, ClearRadius)`
- **`ResumeFromAvoid`** — сбрасывает оба `_avoidOther` и `_avoidBuild`
- **`NavTick`** — проверка зданий во **всех свободных режимах**: `Lifting`, `Yawing`, `Cruising` (ship-to-ship только в `Cruising`)

---

## 4. Параметры

**`NpcProximityZoneBuilds` (per-building):**

| Поле | Дефолт | Смысл |
|------|--------|-------|
| `avoidancePadding` | 30 | Отступ от коллайдеров (м), добавляется к `avoidanceRadius` корабля |
| `includeChildren` | true | Искать коллайдеры в дочерних объектах |

**`NpcProximityZone` (per-ship, новая галочка):**

| Поле | Дефолт | Смысл |
|------|--------|-------|
| `considerBuildings` | true | Учитывать building-зоны при поиске конфликтов |

---

## 5. Как настроить здание

1. На корень здания (где висит сложный MeshCollider) вешаешь `NpcProximityZoneBuilds`
2. Добавляешь дочерние GameObject'ы с `BoxCollider`'ами, аппроксимируя форму здания
3. ПКМ → `Refresh Colliders` — скрипт подхватит валидные коллайдеры
4. Настраиваешь `avoidancePadding` (чем больше — тем раньше корабль начнёт обход)
5. Проверяешь gizmo: оранжевые wireframe'ы = зоны, красная сфера = нет валидных коллайдеров

---

## 6. Крайние случаи

| Случай | Реакция |
|--------|---------|
| Корабль в Yawing въезжает в зону здания | Avoidance срабатывает во всех свободных режимах (Lifting/Yawing/Cruising) |
| Корабль без `NpcProximityZone` | Avoidance выключен (компонент опционален) |
| `considerBuildings = false` | Здания игнорируются этим кораблём |
| Здание без валидных коллайдеров | `FindClosestBuildConflict` пропускает (IsIntruding = false) |
| Несколько зданий рядом | Выбирается ближайшее, avoidance идёт от ближайшей точки на нём |
| Avoidance не срабатывает | Включить `verboseBuildLogging` на `NpcProximityZone`, `verboseLogging` на `NpcProximityZoneBuilds`, `debugMode` на `NpcShipController` — проверить консоль |

---

## 7. Тикеты

| # | Тикет | Объём |
|---|-------|-------|
| T-NS-BZ01 | `NpcBuildZoneRegistry` — статический реестр | ~40 LOC |
| T-NS-BZ02 | `NpcProximityZoneBuilds` — компонент на здании | ~130 LOC |
| T-NS-BZ03 | `NpcProximityZone`: `considerBuildings` + `FindClosestBuildConflict` | ~35 LOC |
| T-NS-BZ04 | `NpcShipController`: `EnterAvoid(build)` + `_avoidFromPos` + правка NavTick | ~40 LOC |
| T-NS-BZ05 | `AvoidancePriority`: приоритет расхождения (NpcInstanceId) + `NavMode.AvoidYield` | ~30 LOC |
| T-NS-BZ06 | `ZoneShape` (Sphere/Box): выбор формы avoidance-зоны корабля | ~170 LOC |
| T-NS-BZ07 | Raycast escape corridor: поиск выхода из Π-доков через веер лучей | ~35 LOC |

---

## 8. Приоритет расхождения (T-NS-BZ05)

### Проблема
Два корабля при симметричном avoidance могут «танцевать» вокруг одной точки, мешая друг другу.

### Решение
Приоритет авто-назначается из `NpcInstanceId` (детерминированно, без сетевой коммуникации):

| Приоритет | Действие |
|-----------|---------|
| Выше | Полный avoidance-манёвр (Separate → Stop → BackOff) |
| Ниже | **AvoidYield** — нулевая скорость, ждёт пока high-priority уедет |
| Здание (0) | Корабль всегда делает full avoidance |

Оба корабля независимо приходят к одному решению — сравнение детерминировано.

### Изменения
- `NavMode.AvoidYield` — новый режим в enum
- `AvoidancePriority` → `(uint)npcInstanceId`
- `EnterAvoid(rb, other)` — сравнение приоритетов: выше → Avoiding, ниже → AvoidYield
- `TickAvoidYield` — `linearVelocity=0`, ждёт `IsClearOfConflict`
- `ResumeFromAvoid` / `RestoreFromSave` — обрабатывают `AvoidYield`
- `IsAvoidable` — включает `AvoidYield` (застывший корабль = препятствие)

---

## 9. Zone Shape — Sphere / Box (T-NS-BZ06)

### Назначение
Дизайнер выбирает форму avoidance-зоны корабля: Sphere (по умолчанию) или Box (вокруг коллайдера).

### Параметры
| Поле | Смысл |
|------|-------|
| `zoneShape` | Sphere (радиусы) или Box (коллайдер + padding) |
| `avoidancePadding` | Отступ от коллайдера для Box mode |
| `avoidanceRadius` | Радиус avoidance-сферы (только Sphere) |

Awareness-зона всегда сфера (не зависит от `zoneShape`).

### Геометрия (mixed shapes)
`FindClosestConflict` поддерживает любые комбинации:
- Sphere ↔ Sphere: `centerDist < r1 + r2`
- Sphere ↔ Box: `centerDist - closestOnBox < r1`
- Box ↔ Box: проверка пересечения AABB

`IsClearOfConflict` использует те же проверки с гистерезисом (`clearHysteresis`).

### Поиск коллайдера
`FindLargestCollider()` — сканирует все дочерние коллайдеры, выбирает самый большой не-триггерный.

---

## 10. Raycast Escape Corridor (T-NS-BZ07)

### Проблема
В Π-образных доках avoidance толкает корабль от стен, запирая его внутри.

### Решение
При входе в avoidance выполняется веер из `avoidEscapeRays` горизонтальных лучей. Луч с максимальной дистанцией до попадания = самое открытое направление (выход из дока). Итоговый вектор движения: `lerp(away, escapeDir, avoidEscapeBlend)`.

### Параметры
| Поле | Дефолт | Смысл |
|------|--------|-------|
| `avoidEscapeRays` | 16 | Количество лучей (4-36) |
| `avoidEscapeMaxDist` | 200 | Макс. дистанция луча (м) |
| `avoidEscapeBlend` | 0.6 | Вес escape: 0=только away, 1=только escape |

### Edge cases
- Открытое пространство: все лучи `maxDist` → `escapeDir` случаен, но `away` доминирует
- Все лучи короткие (заперт): `_escapeDir = zero` → чистый `away`
- Корабль внутри MeshCollider: лучи не бьются об этот коллайдер → поведение как в открытом пространстве
