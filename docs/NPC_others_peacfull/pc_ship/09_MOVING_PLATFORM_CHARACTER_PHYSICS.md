# Moving-Platform Character Physics

> **Scope:** Что происходит с физикой персонажей/NPC/предметов на движущейся платформе (палубе корабля). Общая спека для всех «райдеров».

**См. также:**
- `docs/Character/Skills/real-time-combat/npc-enemy/01_CREW_ON_MOVING_SHIP.md` — канонический сценарий NPC на борту (T-CREW-01..05, done)
- `Assets/_Project/Scripts/Core/PlatformRideHelper.cs` — общая утилита probe + carry (используют NetworkPlayer и NpcBrain)
- `Assets/_Project/Scripts/Core/PickupDeckRide.cs` — T-PICKUP-RIDE-01 (этот тикет) — pickup'ы на палубе

---

## 1. Проблема

Любой объект, стоящий на движущейся палубе NPC-корабля, **уезжает** вместе с ней, если сам не едет. Причины:

- `CharacterController` (NetworkPlayer) не наследует движение платформы под ногами — он знает только дельту, которую ему передали.
- `NavMeshAgent` (NpcBrain) привязан к **мировому** NavMesh, и NPC «сдувает» с корабля.
- `MonoBehaviour` (PickupItem / NpcLootPickup) вообще не знает, что он на палубе — `transform.position` остаётся в мировой точке, корабль уходит.

Два уровня проблемы:
1. **Что движется:** NPC-корабли пилотируются прямой записью `rb.linearVelocity` + `rb.MoveRotation` в `NpcShipController.NavTick` (M3.2). Это **не** идёт через `NetworkTransform` — `rb.linearVelocity` сервера не реплицируется на клиент. Поэтому на клиенте палуба движется плавно (через `NetworkTransform` корня корабля), но **локальный** carry-расчёт должен идти по дельте `transform.position` платформы, а не по velocity.
2. **Что не движется:** CharacterController персонажа, transform NPC, transform pickup — все они имеют свой `updatePosition`/`updateRotation` или просто transform.position, и `Physics.SphereCast` + ручной carry нужны.

## 2. Решение — три уровня carry

В проекте уже есть два уровня (для персонажа и NPC). Этот документ добавляет третий — для pickup'ов. Все три используют общий `PlatformRideHelper` для probe + формулы.

| Уровень | Кто | Файл | Сторона | Где в цикле |
|---|---|---|---|---|
| **L1 — пеший персонаж** | `NetworkPlayer` (owner) | `Player/NetworkPlayer.cs:837` | owner-only | в `ApplyPlatformCarry` перед `ProcessMovement` |
| **L2 — NPC на борту** | `NpcBrain` (server) | `AI/NpcBrain.cs:276` | server-side (NetworkTransform реплицирует) | `FixedUpdate` (NGO-parented) или с carry-формулой |
| **L3 — pickup'ы** | `PickupItem` / `NpcLootPickup` | `Core/PickupDeckRide.cs` (NEW, T-PICKUP-RIDE-01) | local (каждый клиент + server считает) | `LateUpdate` (поверх Update бобаинга) |

### Почему L3 — local, не server-authoritative

У pickup'ов нет `NetworkTransform` (только `NetworkObject` + `SynchronizeTransform=1` — этого недостаточно для репликации каждый кадр). Если сервер будет двигать pickup, у клиентов будет рассинхрон (задержка RPC). Поэтому **каждый peer считает carry локально**, а сервер остаётся source of truth для pickup'а только в момент спавна/деспавна. Это совпадает с паттерном `NetworkPlayer.ApplyPlatformCarry` (owner-only) и `NpcBrain` (server-side) — все три уровня считают carry независимо каждый на своей стороне. Если геометрия и transform палубы на сервере и клиенте совпадают (а они должны — это детерминированная физика), результат идентичен.

### Почему parent (вариант b), не carry-формула

Carry-формула (Δпозиции каждый кадр) подходит для NPC и персонажа: они двигаются каждый кадр сами (NavMeshAgent, CharacterController.Move), и прибавить Δ палубы в том же Move — естественно. Для pickup'а transform.position управляется **только** бобаингом (`Update`), никакой физики нет. Переписывать Update, чтобы каждый кадр добавлять Δ — лишняя работа, и легко забыть это в `OnNetworkSpawn`, в момент включения/выключения и т.д. **Local parent** делает всё это бесплатно: меняется parent.transform → Unity сама пересчитывает transform.position для ребёнка. Bobbing остаётся как сейчас, но выражается в **local space** палубы (или возвращается в мировые координаты, если parent=null).

### Pitch/roll — игнорируются

Намеренно. Все три уровня переносят только `position` + `yaw`. Если наклонить палубу, персонаж/предмет не наклонится вместе с ней. Это соглашение уровня проекта (см. комментарий `PlatformRideHelper.cs:8-9`).

## 3. Алгоритм L3 — pickup carry (carry-формула, не parent)

### 3.1 История (3 failed attempts → финальное решение)

| Попытка | Что пробовали | Почему не сработало |
|---------|--------------|---------------------|
| **A. `transform.SetParent`** | Локальный parent на платформу через `Transform.SetParent` | NGO спамил `Invalid parenting, NetworkObject moved under a non-NetworkObject parent` |
| **B. `NetworkObject.TrySetParent`** | NGO parenting (как NpcBrain parented-путь) | NGO проверяет `TryGetComponent` на **direct** parent. Дочерние BoxCollider палубы на child GO без NetworkObject → NGO отказывает |
| **C. Carry-формула** (была в первой версии второго дня) | `PlatformRideHelper.ComputeCarryDelta`, `transform.position += deltaPos` в LateUpdate | **Работало**, но через 3-4 секунды pickup «слетал» и дрейфил. Причина: `_startPosition` в `PickupItem.Update` устаревала. Pickup ехал с кораблём, потом spherecast промахивался (стык коллайдеров) → `_platform = null` → Update писал `_startPosition + bob` → pickup прыгал в старую спавн-точку |
| **D. ✅ Финальный: carry + RefreshWorldBase** | Carry-формула + на палубе **не писать** `transform.position` + вне палубы `RefreshWorldBase()` | Предмет едет с кораблём, при отлипании не прыгает обратно. База бобаинга = текущая позиция, не устаревшая _startPosition. **Работает.** |

### 3.2 Алгоритм

```
LateUpdate (PickupDeckRide):
    platform = SphereCast(transform.position + up*probeUp, radius, down, dist, mask)
    if platform == null:
        missFrames++
        if missFrames >= clear (8) && _platform != null:
            _platform = null       # сошли с палубы
    elif platform != _platform:
        _platform = platform
        _platformLastPos = platform.position
        _platformLastRot = platform.rotation
        return                     # не carry в первом кадре
    else:
        missFrames = 0
        deltaPos = ComputeCarryDelta(platform, pos, lastPos, lastRot, carryYaw, out deltaYaw)
        transform.position += deltaPos
        if carryYaw && |deltaYaw| > eps:
            transform.rotation = AngleAxis(deltaYaw, up) * transform.rotation
        _platformLastPos = platform.position
        _platformLastRot = platform.rotation
```

```
Update (PickupItem / NpcLootPickup):
    if _deckRide != null && _deckRide.DeckParent != null:
        # На палубе → НЕ пишем transform.position (carry уже двигает).
        # Любая запись сюда сломает carry.
    else:
        # Свободный режим → бобаинг от текущей позиции.
        _deckRide.RefreshWorldBase()                     # фиксирует transform.position как новую базу
        transform.position = _deckRide.WorldBasePosition + bob
```

### 3.3 Почему это наконец работает

Ключевое открытие — `Update` пишет `transform.position` **до** `LateUpdate`. Если на палубе писать `transform.position = _startPosition + bob`, то:
1. Первый кадр после attach: `_startPosition` = мировая спавн-точка. OK.
2. После 8 кадров carry: pickup в точке B (корабль улетел). SphereCast промахнулся → `_platform = null`.
3. Update: `DeckParent == null` → `transform.position = _startPosition + bob` → **прыжок обратно в A**.
4. Визуально: pickup «попрыгал-попрыгал и дрифтом улетел».

**Фикс:** когда `_platform == null`, `RefreshWorldBase()` первым делом записывает **текущий** `transform.position` как `_worldBasePosition`. Затем `WorldBasePosition + bob` даёт бобаинг вокруг точки B, а не A. Никакого прыжка.

### 3.4 Почему не NGO parenting (только для истории)

Два провала:
1. `Transform.SetParent(parent)` → `NetworkObject.OnTransformParentChanged` видит non-NetworkObject parent → ошибка `Invalid parenting` + откат parent.
2. `NetworkObject.TrySetParent(shipNo, worldPositionStays: true)` → работает если shipNo — spawned NetworkObject. Но у NPC-корабля root — ship c `ShipController : NetworkBehaviour`, а BoxCollider палубы — на child GO без NetworkObject. `TrySetParent` к shipNo срабатывает, но `_netObject.transform.parent` проверяет только direct parent. NGO фактически не применяет parent на клиентах (хотя на сервере parent стоит).

Carry-формула проще и не зависит от NGO.

## 4. HARD RULES

### R1: Carry-формула (как NetworkPlayer), НЕ NGO parenting
`PickupDeckRide` использует `PlatformRideHelper.ComputeCarryDelta` — ту же формулу, что `NetworkPlayer.ApplyPlatformCarry`. Никакого `NetworkObject.TrySetParent`, никакого локального `Transform.SetParent`. Только Δпозиции платформы каждый кадр.

**Почему не NGO TrySetParent:** пробовали (см. §3 «Почему не NGO parenting») — NGO проверяет `transform.parent.TryGetComponent(out NetworkObject)` на **direct parent**. Дочерние коллайдеры палубы NPC-корабля лежат на child GO без NetworkObject → NGO отказывает, parenting не срабатывает. Carry-формула работает с любым transform (не требует NetworkObject на платформе).

**Бонус:** carry работает на ВСЕХ движущихся объектах под маской — даже на декоративных палубах без NetworkBehaviour (например стационарные платформы-турбины). Главное — `Physics.SphereCast` находит solid collider.

### R2: Гистерезис detach — 8 кадров
SphereCast промахивается при проезде через стык двух MeshCollider-ов палубы или при резком ускорении корабля. `_missFramesToClear = 8` даёт запас — pickup не отцепляется при кратковременной потере контакта.

### R3: Update НЕ пишет transform.position на палубе
`PickupItem.Update` и `NpcLootPickup.Update` проверяют `_deckRide.DeckParent != null`. Если да — `transform.position` не трогается. Любая запись сюда compound-ит с carry-формулой и через несколько кадров вызывает прыжок (см. §3.3).

### R4: RefreshWorldBase на каждом свободном кадре
Когда `DeckParent == null`, перед бобаингом вызывается `_deckRide.RefreshWorldBase()`. Это фиксирует текущую `transform.position` как новую базу. Без этого pickup «прыгает» в старую точку спавна после отцепки от палубы.

### R5: Pitch/roll палубы НЕ применяются к предмету
`ComputeCarryDelta` переносит только позицию + yaw. Если палуба кренится, предмет остаётся горизонтальным. Это дисциплина уровня проекта (см. `PlatformRideHelper.cs:8-9`).

### R6: Документ 09_..._PHYSICS.md ведётся
Теперь содержит не только постановку, но и историю трёх провальных попыток, и финальное решение.

## 5. Файлы

### Создать
- `Assets/_Project/Scripts/Core/PickupDeckRide.cs` — новый компонент

### Изменить
- `Assets/_Project/Scripts/Core/PickupItem.cs` — Update() теперь читает `_startPosition` ИЛИ local position
- `Assets/_Project/Scripts/AI/NpcLootPickup.cs` — то же самое

### Не трогать
- `Assets/_Project/Prefabs/PickupItem_Test.prefab` — компонент добавляется программно при server-spawn (через InventoryServer), не в префабе
- `Assets/_Project/Items/Network/InventoryServer.cs` — там только `Instantiate + Spawn`; `PickupDeckRide` вешается автоматически в `PickupItem.Start` (если есть родитель-палуба)

## 6. Verification (пользователь делает)

```powershell
# Compile
# Открыть Unity Editor → Console → 0 errors

# Play Mode сценарий:
# 1. Запустить хост
# 2. Найти NPC-корабль (палуба имеет layer в _platformMask; по умолчанию ~0 = ALL)
# 3. Подойти к NPC, встать на палубу (NetworkPlayer _platformMask ~0, должно работать)
# 4. Выбросить предмет из инвентаря (E на инвентаре → Drop)
# 5. Подождать пока корабль улетит
# Ожидаемо: предмет визуально едет с кораблём (НЕ остаётся в мировой точке)
```

## 7. Что НЕ делаем

- ❌ Не пишем NetworkTransform для pickup'ов (overkill, лишний network traffic)
- ❌ Не используем NGO `TrySetParent` (R1)
- ❌ Не переносим pitch/roll (R5)
- ❌ Не обрабатываем NpcLootPickup отдельным кодом — общий `PickupDeckRide` для обоих
- ❌ Не добавляем .meta файлы вручную — Unity сгенерит сам после `refresh_unity scope=all`

## 8. История

| Дата | Сессия | Изменения |
|------|--------|-----------|
| 2026-07-02 | T-PICKUP-RIDE-01 | Документ создан (был пустой). 3 итерации: SetParent ❌ → TrySetParent ❌ → carry сломался (_startPosition устаревала) ❌ → carry + RefreshWorldBase ✅ **Работает**. |
