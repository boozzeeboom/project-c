# 01 — NPC на движущемся корабле (экипаж / враги): не сдувать + корректная навигация

**Статус:** Фаза 1 — **реализовано**. Фаза 2 — **код реализован** (T-CREW-02/03), нужен per-ship bake для теста.
**Связано:** `70_NPC_ENEMIES.md`, `docs/NPC_others_peacfull/npc_ship/09_MOVING_PLATFORM_CHARACTER_PHYSICS.md`,
`NpcBrain.cs`, `NetworkPlayer.cs`, `PlatformRideHelper.cs`, `ShipDeckNav.cs`.

## 1. Задача

Живые NPC (экипаж, враги-абордажники) должны:
- **(A)** не «сдуваться» с палубы движущегося корабля — **решено в Фазе 1**;
- **(B)** корректно перемещаться по палубе (бродить, преследовать/атаковать игрока на палубе) —
  **Фаза 2 (код готов)**.

## 2. Почему решение игрока НЕ портируется напрямую

| | Игрок (`NetworkPlayer`) | Враг-NPC (`NpcBrain`) |
|---|---|---|
| Кто считает движение | **owner-клиент** | **сервер** (`if(!IsServer) enabled=false`) |
| Чем движется | `CharacterController.Move` | **`NavMeshAgent`** (`SetDestination`) |
| Репликация | NetworkTransform (server-auth) | NetworkTransform (server-auth) |

`NavMeshAgent` привязан к **мировому NavMesh**, который **не движется** с кораблём.

## 3. Фаза 1 — «не сдувает» (РЕАЛИЗОВАНО, T-CREW-01)

- **`Assets/_Project/Scripts/Core/PlatformRideHelper.cs`** — общий хелпер (`DetectPlatform`,
  `ComputeCarryDelta`): позиция + yaw, pitch/roll игнор. Единая формула для игрока и NPC.
- **`NpcBrain.cs`** — серверный `FixedUpdate`-carry: probe вниз по `_platformMask`, возит NPC
  за палубой (`transform.position += Δ` + yaw). На борту **паузит автоуправление агента**
  (`updatePosition/updateRotation=false`), при сходе — восстанавливает + `_agent.Warp`.
  Поля: `_platformCarryEnabled`, `_platformMask`, `_platformProbeUp`, `_platformProbeDistance`,
  `_platformProbeRadius`, `_carryYaw`, `_platformMissFramesToClear` (без magic numbers).
- **Настройка Inspector:** задать `_platformMask` = слой палуб (по умолчанию 0 = выкл).
- **Ограничение:** сам по себе решает только (A); навигация по палубе — Фаза 2 ниже.

## 4. Фаза 2 — навигация по палубе (КОД РЕАЛИЗОВАН, T-CREW-02/03)

**Пакет:** `com.unity.ai.navigation` 2.0.11 — **установлен** (`Packages/manifest.json`), `NavMeshSurface` доступен.

### 4.1. Почему НЕ «двигать навмеш каждый кадр»

`NavMeshDataInstance` **нельзя переместить** — только `Remove()` + повторный
`NavMesh.AddNavMeshData(data, pos, rot)`. Пере-регистрация каждый кадр рвёт пути агентов
(на кадр агент вне навмеша) → рывки. Отвергнуто.

### 4.2. Выбранный подход — фиксированный nav-фрейм + прокси-агент (локальные координаты палубы)

1. Палуба печётся в префабе при корабле **в origin/identity** → `NavMeshData` в координатах «относительно ShipRoot».
2. `ShipDeckNav` регистрирует этот `NavMeshData` в точке `navFrameOrigin = slot * separation`
   (уникальный слот на корабль): `deck-local p → nav-world = navFrameOrigin + p`.
3. Для каждого бортового NPC — **прокси-`NavMeshAgent`** в нав-фрейме:
   - цель = `navFrameOrigin + ShipRoot.InverseTransformPoint(playerWorld)`;
   - `proxy.SetDestination(target)`; `proxyLocal = proxy.position - navFrameOrigin`;
   - мировая позиция NPC = `ShipRoot.TransformPoint(proxyLocal)`; поворот — по `proxy.velocity`.
4. Пока NPC навигирует по палубе, **carry Фазы 1 выключается** (позицию задаёт прокси-нав), но
   вертикаль/крен корабля учитываются через `ShipRoot.TransformPoint`.

Плюсы: навмеш статичен → пути не рвутся; работает на быстром/кренящемся корабле; серверно.

### 4.3. Компонент `ShipDeckNav` (РЕАЛИЗОВАНО, T-CREW-02)

`Assets/_Project/Scripts/Ship/ShipDeckNav.cs`. Вешается на ShipRoot. Держит ссылку на
запечённый `NavMeshData`, регистрирует его в уникальном nav-фрейме (server-only по умолчанию),
даёт конвертации `WorldToDeckLocal` / `DeckLocalToWorld` / `DeckLocalToNav` / `NavToDeckLocal`
и `SampleOnDeck`. Использует только `UnityEngine.AI` (без рантайм-зависимости на `Unity.AI.Navigation`).

### 4.4. `NpcBrain`: навигация по палубе через прокси (РЕАЛИЗОВАНО, T-CREW-03)

- `BeginRide` резолвит `ShipDeckNav` (`GetComponentInParent`), создаёт/варпит прокси-агент,
  `_deckNavActive=true`.
- `DriveDeckNav` (в `FixedUpdate`): цель Chase → `DeckLocalToNav(WorldToDeckLocal(target))`,
  `proxy.SetDestination`; мировая поза NPC = `DeckLocalToWorld(NavToDeckLocal(proxy.pos))`;
  поворот — по `proxy.velocity` через `ShipRoot.TransformVector`. Carry Фазы 1 при этом **не применяется**.
- Leash в мировых координатах **отключён** пока `_deckNavActive` (иначе улетающий корабль рвал бы chase).
- `UpdateAnimator` берёт скорость из прокси. `EndRide`/`OnNetworkDespawn` — teardown прокси
  (`GameObject` прокси server-only, `HideAndDontSave`, уничтожается на despawn).

**Настройка NPC-префаба:** `NavMeshAgent.AgentType` = тот же Agent Type, что запечён у палубы
(§5.1.2); `_platformMask` = слой палубы. Без совпадения Agent Type пути будут пустыми.

### 4.5. Осталось для запуска
- Запечь палубу хотя бы одного корабля по §5 и назначить `NavMeshData` в `ShipDeckNav`.
- PIE-тест (T-CREW-05).

---

## 5. ИНСТРУКЦИЯ: запекание NavMesh для КАЖДОГО корабля отдельно (per-ship bake)

> Навмеш палубы печётся **офлайн в префабе корабля**, отдельно для каждого класса корабля с
> разной геометрией палубы. Рантайм-bake на движущемся корабле НЕ используется.

### 5.1. Разовая подготовка проекта
1. Package Manager → **AI Navigation** (`com.unity.ai.navigation`, ≥2.0). ✅ уже есть.
2. Создать **Agent Type** для экипажа: `Window → AI → Navigation → Agents` (radius/height под
   габарит NPC). Тот же тип должен стоять у `NavMeshAgent` на NPC-префабе.
3. Завести слой `ShipDeck` для коллайдеров палуб (используется и `_platformMask` Фазы 1).

### 5.2. Порядок запекания для одного корабля (повторить для каждого класса)
1. Открыть **префаб корабля** в Prefab Mode (обязательно, чтобы bake сохранился в префаб).
2. Временно поставить `ShipRoot` в **position (0,0,0), rotation (0,0,0), scale (1,1,1)**.
   *(Критично: `NavMeshData` печётся в этой позе = «deck-local»; `ShipDeckNav` рассчитывает на origin/identity.)*
3. Добавить дочерний GameObject `DeckNavSurface` под ShipRoot, повесить **`NavMeshSurface`**:
   - `Agent Type` = тот же, что у NPC (5.1.2);
   - `Collect Objects` = `Children`;
   - `Include Layers` = только `ShipDeck` (+ препятствия палубы при необходимости);
   - `Use Geometry` = `Physics Colliders`.
4. Нажать **`Bake`**. Появится ассет `NavMesh-<...>.asset` рядом с префабом.
5. Повесить на `ShipRoot` компонент **`ShipDeckNav`**, назначить `Deck NavMesh Data` = свежий ассет (шаг 4).
   `NavMeshSurface` в рантайме не нужен — можно **отключить** (его авто-регистрация статична).
6. Вернуть ShipRoot в рабочую позу (или оставить префаб; ассет `NavMeshData` уже вшит ссылкой).
7. На NPC-префабе проверить `NavMeshAgent.AgentType` = Agent Type из 5.1.2.

### 5.3. Несколько кораблей одновременно
- Каждый инстанс `ShipDeckNav` берёт **уникальный `navFrameOrigin`** (slot × `_navFrameSeparation`),
  поэтому навмеши разных кораблей не пересекаются. Bake на класс переиспользуется всеми инстансами.
- `_navFrameSeparation` держать заведомо больше габарита палубы (по умолчанию 5000 м).

### 5.4. Чек-лист
- [ ] Agent Type экипажа создан и одинаков на NPC и на `NavMeshSurface`.
- [ ] Bake в Prefab Mode при ShipRoot в origin/identity.
- [ ] `NavMeshData` назначен в `ShipDeckNav.Deck NavMesh Data`.
- [ ] Слой палубы в `_platformMask` (Фаза 1) и в `Include Layers` surface (Фаза 2).

---

## 6. Сетевые заметки (NGO 2.11)

- Обходимся **без reparent** (transform авторитетен на сервере, реплицируется NetworkTransform).
- Навмеш, прокси-агенты — **серверная** симуляция; клиенту не нужны.
- Если позже понадобится reparent крю — только `NetworkObject.TrySetParent` на сервере + NetworkTransform `InLocalSpace=true`.

## 7. Pitfalls / открытые вопросы

- **Bake-поза:** печь только в origin/identity, иначе конвертации `ShipDeckNav` «уедут».
- **Pitch/roll:** мировая позиция корректна через `TransformPoint`, но визуал NPC наклонится с
  палубой (как и carry Фазы 1). Для баржи ок; иначе — контр-поворот визуала (T-CREW-04).
- **Agent Type:** должен совпадать (NPC ↔ surface), иначе `SamplePosition`/пути пусты.
- **Как NPC попадает на палубу:** крю — разместить на палубе при спавне; абордажник — detection
  probe (Фаза 1) поймает палубу, но навигация оживёт только если у корабля есть готовый `ShipDeckNav`.
- **Много NPC:** переиспользовать реестр «кто на борту» (паттерн `NpcShipZoneRegistry`).

## 8. Roadmap

- **T-CREW-01** — carry «не сдувает» (`PlatformRideHelper` + `NpcBrain.FixedUpdate`). ✅ **DONE**.
- **T-CREW-02** — `ShipDeckNav` (регистрация навмеша в фикс. нав-фрейме, конвертации, `SampleOnDeck`). ✅ **DONE**.
- **T-CREW-03** — прокси-агент в `NpcBrain`: навигация по палубе, отключение carry на время нав-движения,
  deck-aware leash, анимация от прокси. ✅ **DONE (код)**; нужен per-ship bake + PIE-тест.
- **T-CREW-04** — контр-поворот визуала для вертикали (опц.).
- **T-CREW-05** — тест: враг забегает на летящий корабль, преследует игрока по палубе, не сваливается.
