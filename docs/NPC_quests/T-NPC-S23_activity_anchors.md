# T-NPC-S23: Activity Anchors для NpcSocialBrain

**Дата:** 2026-07-14
**Файлы:**
- `Assets/_Project/Scripts/AI/NpcSocialBrain.cs`
- `Assets/_Project/Scripts/AI/Editor/NpcSocialBrainEditor.cs`

## Проблема

NPC, не использующие `NpcSpawner` (например, hand-placed префабы вроде Mira), не могли задавать маршруты патрулирования и точки активностей. `patrolWaypoints` в NpcSocialBrain — это `Vector3[]` (мировые координаты), неудобные для ручной расстановки дизайнером.

## Решение

### 1. `patrolWaypointMarkers` (Transform[])
Новое поле в `NpcSocialBrain` — аналог `NpcSpawner.patrolWaypointMarkers`. Дизайнер создаёт Empty-объекты в сцене и перетаскивает их в массив. Приоритет над `patrolWaypoints` (Vector3[]).

### 2. Activity Anchors (Transform)
Для каждой idle-активности добавлен `Transform`-якорь:

| Поле | Активность | Поведение |
|---|---|---|
| `workAnchor` | Work | NPC идёт к якорю → играет рабочую анимацию |
| `sleepAnchor` | Sleep | NPC идёт к якорю → засыпает |
| `sitAnchor` | Sit | NPC идёт к якорю → сидит (без поиска SitPoint) |
| `socializeAnchor` | Socialize | Точка сбора для общения |
| `wanderAnchor` | Wander | Центр зоны блуждания (fallback: `_brain.SpawnPoint`) |

### 3. Логика приоритета
- Если NPC часть `NpcSpawner` → спавнеровские `patrolWaypointMarkers` передаются как `waypointsOverride` в `ApplySpawnerConfig()` и перезаписывают `patrolWaypoints` (старое поведение без изменений).
- Если NPC hand-placed → дизайнер заполняет `patrolWaypointMarkers` и activity anchors прямо в инспекторе `NpcSocialBrain`.

### 4. Инспектор
- В секцию «Idle Activities» добавлено поле `Patrol Waypoint Markers` (Transform[]).
- При выборе активности Work/Sleep/Sit/Socialize — появляется секция «Activity Anchors (T-NPC-S23)» с соответствующим полем.
- При выборе Wander — появляется поле `Wander Anchor`.

## Использование (hand-placed NPC)

1. Создать Empty child-объекты под NPC в сцене (например `PatrolPoint1`, `WorkSpot`, `SleepSpot`).
2. В инспекторе `NpcSocialBrain`:
   - Перетащить patrol-точки в `Patrol Waypoint Markers`.
   - Выбрать `Idle Activity = Patrol` / `Work` / etc.
   - Перетащить соответствующий Anchor в появившееся поле.
3. NPC будет использовать эти точки вместо мировых координат.
