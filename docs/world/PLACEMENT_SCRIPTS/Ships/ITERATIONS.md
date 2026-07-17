# Итерации разработки — ShipPresetCreator

## Итерация от 2026-07-18 (v1.7 — правки Heavy preset)

**Задача:** Скорректировать физические параметры класса Heavy по эталону.

**Исправления:**
- `thrustForce`: 5 500 → 100 000
- `verticalForce`: 800 → 50 000
- `massMultiplier`: 10 → 25

---

## Итерация от 2026-07-18 (v1.6 — NavMesh-слои, NpcSpawner, порядок компонентов)

**Задача:** После создания через ShipPresetCreator корабль имел отличия от эталонного префаба `DONT-DELETE-NPC REFERENCE SHIP`: не работал NavMesh, отсутствовал NpcSpawner, неверные слои.

**Коммит:** `601f52a` — T-SHIP06: fix — NavMesh-слои, NpcSpawner, порядок компонентов ShipPresetCreator

**Сравнение с эталоном:** `Ship_old_1.prefab` vs `DONT-DELETE-NPC REFERENCE SHIP.prefab`

**Исправления:**

| # | Проблема | Было | Стало |
|---|----------|------|-------|
| 1 | Platform слой | `Default` | `ShipDeck` |
| 2 | Platform тег | (отсутствовал) | `Ship` |
| 3 | NavMeshSurface.collectObjects | `Volume` | `All` |
| 4 | NavMeshSurface.useGeometry | (Render Meshes) | `PhysicsColliders` |
| 5 | DeckNavSurface Y | `1.01` | `-0.37` |
| 6 | NpcSpawner на корне | отсутствовал | добавлен + загрузка `NpcSpawner_ship_deck.asset` |
| 7 | ShipRootReference на корне | был (лишний) | удалён |
| 8 | NpcProximityZone.avoidanceRadius | `80` | `120` |
| 9 | NpcProximityZone.drawGizmos | `false` | `true` |
| 10 | NpcShipController.schedule | назначался | не назначается (None) |

**Ключевой инсайт по NavMesh:** эталон использует `collectObjects=All` + `useGeometry=PhysicsColliders` — запекает все Physics Colliders в иерархии (включая BoxCollider на Platform → слой ShipDeck). Предыдущий подход с `Volume` + `RenderMeshes` не работал, т.к. не учитывал слой коллайдера. Platform должна быть на слое `ShipDeck` (6), чтобы NPC могли по ней ходить.

**Затронутые файлы:**
- `Assets/_Project/Editor/ShipPresetCreator.cs` — все правки
- Добавлен `using ProjectC.AI;`

---

## Итерация от 2026-07-17 (v1.5 — Floor-коллайдер + дверь)

**Задача:** Персонаж проваливается сквозь палубу; дверь была проходимой (isTrigger=true).

**Коммит:** `223bedd`

**Анализ:**
- `PilotSeatController.Awake()` принудительно ставит `isTrigger=true` — поэтому персонаж проходит сквозь PilotSeat
- `DoorController` НЕ трогает `isTrigger` — дверь должна быть твёрдой
- `Ship_Light_root` имеет `Platform` на Y=0 с твёрдым BoxCollider — это то, на чём стоит персонаж
- MainVisual на Y=0.51 висит выше земли, CharacterController не может на него забраться

**Исправления:**
- Добавлен `Floor` — невидимый дочерний объект на Y=0 с BoxCollider (слой ShipDeck), аналог Platform
- Door: `isTrigger = false` (было `true`)

---

## Итерация от 2026-07-17 (v1.4 — коллизия палубы + Medium preset)

**Задача:** Персонаж проваливается сквозь визуал; Medium имел неверные значения thrust/yaw/vertical.

**Коммит:** `a456d9b` — T-SHIP06: fix — Medium preset, коллизия палубы (ShipDeck layer), NavMeshSurface Volume

**Исправления:**
- Medium: thrust=30000, yaw=150000, vertical=30000 (было 4000/25/120)
- MainVisual теперь на слое `ShipDeck` (6) — CharacterController игрока корректно детектит палубу через Physics.SphereCast
- NavMeshSurface: `CollectObjects.Children→Volume` с bounding box по visualScale — `BuildNavMesh()` находит MainVisual и печёт реальный navmesh
- Убран reflection-хак `useGeometry` (не нужен при Volume)

---

## Итерация от 2026-07-17 (v1.3 — ходьба + Key)

**Задача:** Персонаж проваливался сквозь палубу; ключ корабля был пустой (itemName, description, itemType не заполнялись).

**Коммит:** `f8d441f` — T-SHIP05: fix — ходьба по палубе (NavMesh печётся при создании), Key ItemData заполняется корректно

**Исправления:**
- NavMesh: вместо пустого `new NavMeshData()` — вызов `NavMeshSurface.BuildNavMesh()` и сохранение запечённого NavMeshData в ассет
- Key ItemData: `itemName`, `itemType`, `description` — публичные поля, `SetPrivateField` (NonPublic binding) их не видел → прямое присваивание
- `itemType = ItemType.Key` (8), а не int 0

---

## Итерация от 2026-07-17 (v1.2 — багфиксы по ревью)

**Задача:** Убрана лишняя Platform, починены розовые материалы, восстановлена ходьба по палубе.

**Коммит:** `2a26128` — T-SHIP04: фикс префаб-генератора — убрана лишняя Platform, починены розовые материалы, восстановлена ходьба по палубе

**Изменения:**
- Удалён дочерний объект Platform — на NPC-кораблях его нет, MainVisual с BoxCollider сам служит палубой
- `CreateChildCube`: вместо `new Material(Shader.Find("URP/Lit"))` используется `new Material(AssetDatabase.LoadAssetAtPath<Material>(URP_LitMat))` — материалы больше не розовые
- Ходьба: без перекрывающего коллайдера Platform, NavMeshSurface корректно печёт navmesh по MainVisual

---

## Итерация от 2026-07-17 (v1.1 fix)

**Задача:** Исправление багов после ревью: ShipRootReference wiring, Key naming, Meziy fuel/module, CargoVisual spawnZone.

**Коммит:** `0176568` — T-SHIP01: fix — ShipRootReference wiring, Key naming, Meziy fuel/module, CargoVisual spawnZone

**Исправления:**
- ShipRootReference: всем дочерним объектам (MainVisual, Platform, PilotSeat, Door, CargoVisual, Exchanger) проставляются ссылки на корневые _shipController / _rigidbody / _networkObject / _root
- Ключ: имя ассета = `Key_{ShipName}` (вместо `Key_{class}_ship`)
- MeziyModuleActivator: проставлены fuelSystem и moduleManager
- ShipCargoVisual: BoxCollider добавляется ДО присвоения в _spawnZone

---

## Итерация от 2026-07-17

**Задача:** Создать универсальный Editor-тул для генерации префаба корабля (Player + NPC) на основе анализа реальных кораблей Ship_Light_root и NPC_Ship_HeavyII_03 из сцены world0_0.

**Коммит:** `82447b2` — T-SHIP01: ShipPresetCreator — универсальный Editor-тул создания кораблей

**Изменения:**
- Удалён `CreateTestShip.cs` (устаревший, неверные данные)
- Создан `Assets/_Project/Editor/ShipPresetCreator.cs` — EditorWindow + полный билдер префаба
- Создана документация `docs/world/PLACEMENT_SCRIPTS/Ships/README.md`

**Архитектура:**
- 4 пресета (Light/Medium/Heavy/HeavyII) на основе анализа живых кораблей
- Универсальный префаб: ВСЕ компоненты (Player: FuelSystem/ModuleManager/Hull/Key + NPC: NpcShipController/ProximityZone/DeckNav)
- Автосоздание зависимых SO-ассетов (ключ, damage config, schedule, navmesh)
- Визуальный паттерн от NPC: root scale=1, MainVisual дочерний
