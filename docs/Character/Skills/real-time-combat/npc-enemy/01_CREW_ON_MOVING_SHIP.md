# 01 — NPC на движущемся корабле (экипаж / враги): не сдувать + корректная навигация

**Статус:** анализ (design), не реализовано.
**Связано:** `70_NPC_ENEMIES.md`, `docs/NPC_others_peacfull/npc_ship/09_MOVING_PLATFORM_CHARACTER_PHYSICS.md`,
`NpcBrain.cs`, `NetworkPlayer.cs` (ApplyPlatformCarry).

## 1. Задача

Живые NPC (экипаж, враги-абордажники) должны:
- **(A)** не «сдуваться» с палубы движущегося корабля (та же проблема, что у игрока);
- **(B)** корректно перемещаться по палубе (бродить как экипаж, преследовать/атаковать
  игрока, который на палубе).

## 2. Почему решение игрока НЕ портируется напрямую

| | Игрок (`NetworkPlayer`) | Враг-NPC (`NpcBrain`) |
|---|---|---|
| Кто считает движение | **owner-клиент** | **сервер** (`if (!IsServer) enabled=false`) |
| Чем движется | `CharacterController.Move` (ручная локомоция) | **`NavMeshAgent`** (`SetDestination`, `agent.velocity`) |
| Репликация | NetworkTransform (server-auth) | NetworkTransform (server-auth) |
| Carry-фикс | `ApplyPlatformCarry` на owner по transform-дельте | **не подходит как есть** |

Ключевое (evidence из `NpcBrain.cs`):
- `[RequireComponent(typeof(NavMeshAgent))]`, движение = `_agent.SetDestination(targetPos)`,
  проверки `_agent.isOnNavMesh`, скорость из `_agent.velocity`. Строки 46, 154, 336–338, 434.
- Вся логика **server-side** (`OnNetworkSpawn`: `if (!IsServer) { enabled=false; return; }`).

## 3. Корневая несовместимость: NavMeshAgent ↔ движущаяся палуба

`NavMeshAgent` привязан к **запечённому NavMesh в мировых координатах**. NavMesh **не
движется** вместе с кораблём. Следствия, если ничего не менять:
- Палуба летит, а NavMesh остаётся на месте → `agent.isOnNavMesh` на палубе = false, либо
  агент «приклеен» к мировому мешу под кораблём и остаётся позади (тот же эффект «сдувает»,
  что у игрока, но усугублённый тем, что агент вообще не понимает палубу).
- `SetDestination` в мировых координатах бессмысленен на подвижной палубе.

Вывод: **нельзя просто добавить carry поверх NavMeshAgent** — надо развести «езду на
платформе» и «навигацию по палубе».

## 4. Варианты

| Вариант | Суть | (A) не сдувает | (B) навигация | Стоимость |
|---|---|---|---|---|
| **V1. Parenting к ShipRoot** | серверный `NetworkObject.TrySetParent(ship)`, NetworkTransform `InLocalSpace=true` | ✅ бесплатно (иерархия) | ❌ сам по себе нет | низкая |
| **V2. Server-side carry** | компонент `PlatformRider` на сервере: transform-дельта корабля → transform NPC (как у игрока, но на сервере читаем корабль напрямую) | ✅ | ❌ | низкая |
| **V3. Локальный NavMeshSurface на корабле** | `com.unity.ai.navigation`: запечь меш палубы как surface-ребёнок ShipRoot, агент навигирует в **локальной** системе | ✅ (через parent) | ✅ полноценно | высокая |
| **V4. Простой deck-steering** | пока аборд: отключить агент, двигать к цели по локальной плоскости палубы + raycast вниз «держись палубы» | ✅ (через parent/carry) | ⚠️ примитивно (без обхода препятствий) | средняя |

## 5. Рекомендация — фазовый гибрид

### Фаза 1 (parity «не сдувает») — V1 + V2
1. **Крю-атач через parenting.** На сервере, когда NPC становится «на борту» (крю по
   дизайну или detection probe вниз как у игрока), `npcNetworkObject.TrySetParent(shipRoot, true)`.
   NGO требует: оба — `NetworkObject`, вызов **на сервере**; на NetworkTransform NPC
   включить **`InLocalSpace = true`**, чтобы позиция реплицировалась относительно корабля.
   - Транслация + поворот наследуются иерархией → NPC не сдувает **бесплатно**.
   - **Нюанс pitch/roll:** parent наследует и крен/тангаж корабля → NPC наклонится вместе с
     палубой. Для «баржи», которая почти не кренится, приемлемо. Если критично — держать
     визуал вертикально: контр-поворот дочернего visual-меша по pitch/roll (yaw оставить).
2. **Отключить управление позицией у агента, пока на борту.** `NavMeshAgent` не должен
   бороться с parent'ом и мировым мешем: `_agent.updatePosition = false; _agent.updateRotation
   = false;` (или `_agent.enabled = false` в MVP). Иначе агент будет тянуть NPC на мировой
   NavMesh под кораблём.
3. **Альтернатива без parenting — `PlatformRider` (V2).** Если reparent NetworkObject
   нежелателен (ломает spawn-иерархию/despawn), сделать серверный компонент, повторяющий
   логику `ApplyPlatformCarry`, но на сервере и с **прямым чтением** transform корабля
   (сервер авторитетен): каждый FixedUpdate `transform.position += shipDeltaPos` + yaw.
   Общая рекомендация: вынести формулу carry (Δpos + Δyaw, без pitch/roll) в переиспользуемый
   помощник, чтобы игрок и NPC считали одинаково.

### Фаза 2 (корректная навигация по палубе) — V3
1. Подключить пакет **`com.unity.ai.navigation`** (NavMeshSurface). *Проверить, установлен ли
   он в проекте — сейчас `NpcBrain` использует стоковый `UnityEngine.AI.NavMeshAgent`.*
2. На префаб корабля добавить дочерний **`NavMeshSurface`** (child ShipRoot), запечь по
   геометрии палубы (collision/render mesh палубы + фальшборты как препятствия).
3. Агент навигирует **в локальной системе корабля**: держать `agent.updatePosition=false`,
   брать `agent.nextPosition`/`desiredVelocity`, применять смещение в локальных координатах
   палубы, а мировое размещение отдать parenting'у (Фаза 1). Так `SetDestination` считается
   относительно палубы и не «съезжает» при полёте корабля.
4. `NpcBrain` доработки: цель (игрок) переводить в локальные координаты палубы перед
   `SetDestination`; `leash`/`aggroRange` считать по локальной дистанции на палубе.

### Фаза 2-MVP (если V3 дорого) — V4
Пока не готов локальный NavMesh: на борту отключить агент и рулить простым steering по
локальной плоскости палубы (двигать к локальной позиции цели, `Physics.Raycast` вниз по
слою палубы, чтобы не свалиться), клампить в bounds палубы. Годится для маленькой плоской
палубы и MVP-абордажа; без обхода препятствий.

## 6. Сетевые заметки (NGO 2.11)

- Reparent **только на сервере** через `NetworkObject.TrySetParent` (не `transform.SetParent`
  для spawned NetworkObject — иначе рассинхрон). Клиенты получают parent через
  `AutoObjectParentSync`.
- NetworkTransform NPC: **`InLocalSpace = true`** на время нахождения на борту, иначе
  дельты корабля и репликация будут конфликтовать. При сходе — вернуть в world space.
- Прецедент parenting под корабль в проекте уже есть (игрок-пилот:
  `transform.SetParent(_currentShip.ShipRoot, true)` в `NetworkPlayer.SubmitSwitchModeRpc`),
  но для NetworkObject-NPC нужен именно `TrySetParent`.

## 7. Триггер «на борту / сошёл»

- **Крю (дизайн):** назначается при спавне на конкретный корабль (crew-slot на ShipRoot) —
  атачится сразу, не зависит от probe.
- **Абордажник/забежал на палубу:** detection probe вниз по `_platformMask` (как у игрока) →
  атач/detach с гистерезисом. Всё на сервере.

## 8. Pitfalls / открытые вопросы

- **Пакет навигации:** подтвердить наличие `com.unity.ai.navigation`; без него Фаза 2
  невозможна (стоковый bake — только глобальный статичный NavMesh).
- **Pitch/roll наклон** parented NPC — принять или контр-поворачивать визуал (см. Фаза 1.1).
- **Стоимость баке per-ship:** NavMeshSurface печь **офлайн** в префабе, не в рантайме
  (рантайм-bake на движущемся корабле дорог и не нужен, если геометрия палубы статична).
- **Docked/Lifting:** на `Docked` (isKinematic) атач безопасен; на `Lifting`
  (`detectCollisions=false`) probe-детект может не найти палубу — крю-атач по дизайну это
  переживает (не зависит от probe), probe-абордажники — нет.
- **Множество NPC на палубе:** избегать N×FindObjects; переиспользовать `NpcShipZoneRegistry`
  паттерн для списка «кто на борту».

## 9. Roadmap (предложение)

- **T-CREW-01** — общий помощник carry (Δpos+Δyaw, без pitch/roll) + `PlatformRider`/атач,
  parity «не сдувает» для NPC (Фаза 1).
- **T-CREW-02** — крю-слоты на ShipRoot + серверный атач/детач через `TrySetParent`,
  NetworkTransform InLocalSpace.
- **T-CREW-03** — локальный `NavMeshSurface` на корабле + навигация агента в локальной
  системе; интеграция в `NpcBrain` (Фаза 2).
- **T-CREW-04** — deck-steering fallback (Фаза 2-MVP) и/или контр-поворот визуала для
  вертикали.
- **T-CREW-05** — тест: враг забегает на летящий корабль, преследует игрока по палубе, не
  сваливается; крю бродит по палубе в круизе.
