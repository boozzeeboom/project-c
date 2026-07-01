# 01 — NPC на движущемся корабле (экипаж / враги): не сдувать + корректная навигация

**Статус:** Фаза 1 — **реализовано**. Фаза 2 — **в работе** (фундамент + инструкция bake).
**Связано:** `70_NPC_ENEMIES.md`, `docs/NPC_others_peacfull/npc_ship/09_MOVING_PLATFORM_CHARACTER_PHYSICS.md`,
`NpcBrain.cs`, `NetworkPlayer.cs`, `PlatformRideHelper.cs`, `ShipDeckNav.cs`.

## 1. Задача

Живые NPC (экипаж, враги-абордажники) должны:
- **(A)** не «сдуваться» с палубы движущегося корабля — **решено в Фазе 1**;
- **(B)** корректно перемещаться по палубе (бродить, преследовать/атаковать игрока на палубе) —
  **Фаза 2**.

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
- **Ограничение:** решает только (A). На борту агент паузится → навигации по палубе нет (это Фаза 2).

## 4. Фаза 2 — навигация по палубе (в работе, T-CREW-02/03)

**Пакет:** `com.unity.ai.navigation` 2.0.11 — **установлен** (проверено в `Packages/manifest.json`),
`NavMeshSurface` доступен.

### 4.1. Почему НЕ «двигать навмеш каждый кадр»

`NavMeshDataInstance` **нельзя переместить** — только `Remove()` + повторный
`NavMesh.AddNavMeshData(data, pos, rot)`. Пере-регистрация каждый кадр рвёт пути агентов
(на кадр агент оказывается «вне навмеша») → рывки. Отвергнуто.

### 4.2. Выбранный подход — фиксированный nav-фрейм + прокси-агент (локальные координаты палубы)

Идея: навмеш палубы регистрируется **один раз** в отдельной «нав-песочнице» (фиксированная
мировая точка, у каждого корабля своя, без пересечений) и **не двигается**. Вся навигация
идёт в **локальной системе палубы**:

1. Палуба печётся в префабе при корабле **в origin/identity** → `NavMeshData` в координатах
   «относительно ShipRoot».
2. `ShipDeckNav` регистрирует этот `NavMeshData` в точке `navFrameOrigin = slot * separation`
   (уникальный слот на корабль). Точка → карта: `deck-local p → nav-world = navFrameOrigin + p`.
3. Для каждого бортового NPC — **прокси-агент** (скрытый `NavMeshAgent`) в нав-фрейме:
   - цель = `navFrameOrigin + ShipRoot.InverseTransformPoint(playerWorld)` (позиция игрока в
     координатах палубы, перенесённая в нав-фрейм);
   - `proxy.SetDestination(target)`; читаем `proxyLocal = proxy.position - navFrameOrigin`;
   - мировая позиция NPC = `ShipRoot.TransformPoint(proxyLocal)`; поворот — по `proxy` velocity.
4. Пока NPC навигирует по палубе, **carry Фазы 1 выключается** (позицию задаёт прокси-нав), но
   вертикаль/крен корабля по-прежнему учитываются через `ShipRoot.TransformPoint`.

Плюсы: навмеш статичен → пути не рвутся; работает на быстром/кренящемся корабле; серверно.
Минус: сложнее (прокси-объекты, offset-фреймы). Компонент `ShipDeckNav` — фундамент этого.

### 4.3. Компонент `ShipDeckNav` (реализован, фундамент)

`Assets/_Project/Scripts/Ship/ShipDeckNav.cs`. Вешается на ShipRoot. Держит ссылку на
запечённый `NavMeshData`, регистрирует его в уникальном nav-фрейме (server-only по умолчанию),
даёт конвертации `WorldToDeckLocal` / `DeckLocalToNav` / `NavToDeckLocalWorld` и `SampleOnDeck`.
Использует только `UnityEngine.AI` (без рантайм-зависимости на `Unity.AI.Navigation`).

### 4.4. Осталось по Фазе 2

- `NpcBrain`: прокси-агент и навигация в нав-фрейме, когда `ShipDeckNav` готов (T-CREW-03).
- Перевод `aggroRange`/`leashRange` в локальные дистанции палубы.

---

## 5. ИНСТРУКЦИЯ: запекание NavMesh для КАЖДОГО корабля отдельно (per-ship bake)

> Навмеш палубы печётся **офлайн в префабе корабля**, отдельно для каждого класса корабля с
> разной геометрией палубы. Рантайм-bake на движущемся корабле НЕ используется.

### 5.1. Разовая подготовка проекта
1. Package Manager → убедиться, что установлен **AI Navigation** (`com.unity.ai.navigation`, ≥2.0). ✅ уже есть.
2. Создать/выбрать **Agent Type** для экипажа: `Window → AI → Navigation → Agents` (radius/height
   под габарит NPC). Запомнить его — тот же тип должен стоять у `NavMeshAgent` на NPC.
3. Завести слой `ShipDeck` для коллайдеров палуб (используется и `_platformMask` Фазы 1).

### 5.2. Порядок запекания для одного корабля (повторить для каждого класса)
1. Открыть **префаб корабля** в Prefab Mode (обязательно, чтобы bake сохранился в префаб).
2. Временно поставить корень префаба (`ShipRoot`) в **position (0,0,0), rotation (0,0,0),
   scale (1,1,1)**. *(Критично: `NavMeshData` печётся в этой позе = «deck-local». `ShipDeckNav`
   рассчитывает на origin/identity.)*
3. Добавить дочерний GameObject `DeckNavSurface` под ShipRoot, повесить **`NavMeshSurface`**:
   - `Agent Type` = тот же, что у NPC (шаг 5.1.2);
   - `Collect Objects` = `Children` (только геометрия корабля, не весь мир);
   - `Include Layers` = только `ShipDeck` (+ препятствия палубы, если нужно);
   - `Use Geometry` = `Physics Colliders` (стабильнее для палубных коллайдеров).
4. Нажать **`Bake`** на `NavMeshSurface`. Появится ассет `NavMesh-<...>.asset` рядом с префабом.
5. Повесить на `ShipRoot` компонент **`ShipDeckNav`** и назначить в поле `Deck NavMesh Data`
   свежезапечённый `NavMeshData` (шаг 4).
   - `ShipDeckNav` регистрирует навмеш сам в рантайме → компонент `NavMeshSurface` при желании
     можно **отключить** (его собственная авто-регистрация статична и не нужна). `ShipDeckNav`
     дропает её через `RemoveData`-эквивалент (мы не используем surface в рантайме).
6. Вернуть ShipRoot в исходную сцену/спавн-позу (или оставить префаб как есть — инстанс в сцене
   встанет куда нужно; `NavMeshData` уже «вшит» в префаб как ассет-ссылка).
7. Проверить: на NPC-префабе `NavMeshAgent.agentTypeID` = тот же Agent Type (шаг 5.1.2).

### 5.3. Несколько кораблей одновременно
- Каждый инстанс `ShipDeckNav` берёт **уникальный `navFrameOrigin`** (slot × `_navFrameSeparation`),
  поэтому навмеши разных кораблей не пересекаются в нав-песочнице. Отдельный bake на класс
  корабля переиспользуется всеми инстансами этого класса (ассет один, фреймы разные).
- `_navFrameSeparation` держать заведомо больше габарита палубы (по умолчанию 5000 м).

### 5.4. Чек-лист
- [ ] Agent Type экипажа создан и одинаков на NPC и на `NavMeshSurface`.
- [ ] Bake сделан в Prefab Mode при ShipRoot в origin/identity.
- [ ] `NavMeshData` назначен в `ShipDeckNav.Deck NavMesh Data`.
- [ ] Слой палубы в `_platformMask` (Фаза 1) и в `Include Layers` surface (Фаза 2).

---

## 6. Сетевые заметки (NGO 2.11)

- Фаза 1 обходится **без reparent** (transform авторитетен на сервере, реплицируется NetworkTransform).
- Навмеш и агенты — **серверная** симуляция (NPC-логика server-only), клиенту навмеш не нужен.
- Если позже понадобится reparent крю — только `NetworkObject.TrySetParent` на сервере +
  NetworkTransform `InLocalSpace=true`.

## 7. Pitfalls / открытые вопросы

- **Bake-поза:** если печь не в origin/identity — `ShipDeckNav`-конвертации «уедут». Всегда origin.
- **Pitch/roll:** прокси-подход даёт корректную мировую позицию через `TransformPoint`, но визуал
  NPC наклонится вместе с палубой (как и carry Фазы 1). Для баржи ок; иначе — контр-поворот визуала.
- **Габарит агента:** Agent Type должен совпадать (NPC ↔ surface), иначе `SamplePosition`/пути пусты.
- **Много NPC:** переиспользовать реестр «кто на борту» (паттерн `NpcShipZoneRegistry`).

## 8. Roadmap

- **T-CREW-01** — carry «не сдувает» для NPC (`PlatformRideHelper` + `NpcBrain.FixedUpdate`). ✅ **DONE**.
- **T-CREW-02** — `ShipDeckNav` (фундамент: регистрация навмеша в фиксированном нав-фрейме,
  конвертации, `SampleOnDeck`). ✅ **DONE (фундамент)**; per-ship bake — по инструкции §5.
- **T-CREW-03** — прокси-агент в `NpcBrain`: навигация по палубе в нав-фрейме, отключение carry
  на время нав-движения, локальные `aggroRange`/`leashRange`. ⏳ next.
- **T-CREW-04** — контр-поворот визуала для вертикали (опц.).
- **T-CREW-05** — тест: враг забегает на летящий корабль, преследует игрока по палубе, не сваливается.
