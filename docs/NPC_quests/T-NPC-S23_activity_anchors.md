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
| `workAnchors` (Transform[]) | Work | NPC ходит между точками, на каждой играет анимацию |
| `sleepAnchors` (Transform[]) | Sleep | NPC идёт к выбранной точке → спит, потом следующая |
| `sitAnchors` (Transform[]) | Sit | NPC ходит между точками и сидит (без поиска SitPoint) |
| `socializeAnchors` (Transform[]) | Socialize | Точки сбора для общения |
| `wanderAnchor` (Transform) | Wander | Центр зоны блуждания (fallback: `_brain.SpawnPoint`) |
=======
REPLACE

### 3. Логика приоритета
- Если NPC часть `NpcSpawner` → спавнеровские `patrolWaypointMarkers` передаются как `waypointsOverride` в `ApplySpawnerConfig()` и перезаписывают `patrolWaypoints` (старое поведение без изменений).
- Если NPC hand-placed → дизайнер заполняет `patrolWaypointMarkers` и activity anchors прямо в инспекторе `NpcSocialBrain`.

### 4. Инспектор
- **«Socialize & Work Tuning»**: массивы `Socialize Anchors`, `Work Anchors`, `Sit Anchors`, `Sleep Anchors` — каждый под своим заголовком.
- **«Idle Activities» → Wander**: поле `Wander Anchor` (одиночное, центр блуждания).

### 5. Циклический обход
Для массивов (Work, Sit, Sleep, Socialize) NPC последовательно обходит точки:
- Дошёл до точки → выполняет активность → переходит к следующей.
- Работает как Loop (по кругу).
=======
REPLACE

## Использование (hand-placed NPC)

1. Создать Empty child-объекты под NPC в сцене (например `PatrolPoint1`, `WorkSpot`, `SleepSpot`).
2. В инспекторе `NpcSocialBrain`:
   - Перетащить patrol-точки в `Patrol Waypoint Markers`.
   - Выбрать `Idle Activity = Patrol` / `Work` / etc.
   - Перетащить соответствующий Anchor в появившееся поле.
3. NPC будет использовать эти точки вместо мировых координат.
