# 05. Как чанки лягут на FO-архитектуру

Дата: 2026-09-27. База: `docs/world/floatingorigin/README.md`,
`09S_NEW_CONTENT_PIPELINE.md` (две подсистемы A/B), `06N_REBASE_TRANSACTION_CONTRACT.md`,
`00_ARCHITECTURE_AND_PLAN.md` §3.1 (по ссылке из README).

## 1. Напоминание: две подсистемы FO (ошибка парома — не повторять)

| # | Подсистема | Вопрос | Правило «под рут» | Источник |
|---|---|---|---|---|
| A | Сдвиг мира (`GlobalMotionControlledRebaseSlice`, F8/авто/F9) | Едут ли объекты вместе с миром | ✅ Достаточно: дети корней `WorldScene_*` едут бесплатно; кэши `Vector3` — через `ApplyRebaseTranslation`; рантайм-спавн вне корней — регистрация участником | `09S_NEW_CONTENT_PIPELINE.md:29` |
| B | Допуск при старте (`GlobalMotionPilotRuntime` → `GlobalSceneNativeExecutor.BuildPreparation`) | Нет ли неизвестных `NetworkObject` | ❌ Недостаточно: каждый scene-placed `NetworkObject` обязан иметь `GlobalSceneSourceMarker` + запись каталога + сходящийся digest, иначе Host fail-closed | `09S_NEW_CONTENT_PIPELINE.md:30,76-116` |

Прецедент: `ParomRoute_01` (`T-PAROM-01`) уронил Host
`uncontrolled_network_source_before_native_sweep` — вылечено маркером + observation/entry (151 запись)
+ `SceneLayoutDigest` (`09S_NEW_CONTENT_PIPELINE.md:3-20`). Любой чанк-контент с `NetworkObject`
пройдёт тем же путём — исключений нет.

## 2. Где обязаны жить чанки (иерархия)

1. **Контейнер чанков — строго под корнем своей сцены.** Legacy `ChunkLoader.CreateChunkRoot`
   (`ChunkLoader.cs:182-198`) создаёт `ChunksContainer` в воздухе (родитель — null → новый GO
   в активной сцене, позиция `Vector3.zero`). По правилам A это **красный случай**
   «рантайм-спавн вне корней сцен» (`09S_NEW_CONTENT_PIPELINE.md:69`): при F8 контейнер не поедет,
   чанки останутся в старых координатах. Правильно: `ChunksContainer` — заранее созданный ребёнок
   `WorldRoot_0_0` (как `Terrain_0_0` и `RuinsValleys` — `terrain/README.md:39-41`), чанки — его дети;
   `CreateChunkRoot` обязан `SetParent(пре baked контейнер, worldPositionStays: false)` + assert,
   иначе fail-closed в Editor (а не молча в рантайме).
2. **Один контейнер — одна сцена.** Чанки `WorldScene_0_0` не могут лежать под `WorldRoot_1_0`:
   выгрузка сцены (`UnloadSceneCoroutine`, `ClientSceneLoader.cs:988-1017`) убьёт чужие чанки;
   `layoutHash` предков посчитается неверно (`09S_NEW_CONTENT_PIPELINE.md:106-109`).
3. **DDOL запрещён для чанков.** `ClientSceneLoader`/`WorldSceneManager` — `DontDestroyOnLoad`
   (`ClientSceneLoader.cs:34`, `WorldSceneManager.cs:106`); чанки — нет: DDOL-корни не являются
   world-space static content и исключены из participant set
   (`06N_REBASE_TRANSACTION_CONTRACT.md:40-48`). `ViewDistanceRuntime`-хост (`ViewDistanceApplier.cs:138-149`)
   — можно (он не хранит позиций), чанки — нельзя.
4. **Bake-каталог обязан знать контейнер.** Пилотный каталог (`GlobalMotionPilotSceneCatalog`,
   151 запись) — снапшот; новый `ChunksContainer` + любой scene-placed NO внутри — полный §3.2 пайплайна
   (маркер `sourceId = {sceneGuid}:{targetObjectId}:{targetPrefabId}`, observation, entry
   `Unmanaged(5)` + ownership по наличию `Rigidbody`, нулевые pose для non-spatial, непустая `reviewNote`,
   пересчёт `layoutHash` предков + `dependencyHash` сцены + `DigestHex` в профиль —
   `09S_NEW_CONTENT_PIPELINE.md:78-116`). Тулза — `Bake Pilot Catalog (Preview + Apply)`
   (`09S_NEW_CONTENT_PIPELINE.md:137-148`); ручной `SourceId` выдумывать запрещено.

## 3. Транзакция rebase: чанки как явные участники

Closed participant set (`06N_REBASE_TRANSACTION_CONTRACT.md:22-48`): `CITY_STATIC`, `WORLD_ANCHORS`,
`SHIP_ROOT[n]`, `SHIP_DECK_NAV[n]`, `PLAYER_FRAME`, `CAMERA`, `NETWORK_GAMEPLAY_ROOT[n]`.
Чанки ложатся так:

- **Визуальные (несетевые) чанки** (террейн-фон, импосторы, руины-LOD — всё без `NetworkObject`):
  входят в `CITY_STATIC` как reviewed static content. Условие — только entries из reviewed manifest;
  «общий `SetParent(WorldRoot)` не является способом допуска; unknown descendants блокируют transaction»
  (`06N_REBASE_TRANSACTION_CONTRACT.md:28`). То есть каждый чанк-корень — именованная запись,
  а не «всё, что оказалось под контейнером».
- **Сетевые чанки** (сундуки/NPC/квесты — `ChunkNetworkSpawner.cs:141-202`):
  каждый корень — отдельный `NETWORK_GAMEPLAY_ROOT[n]` с classification + participant token;
  «перемещение неизвестных NetworkObject вместе с городом» запрещено
  (`06N_REBASE_TRANSACTION_CONTRACT.md:34,39`). Спавн обязан идти через participant-aware путь
  (эволюция `ScenePlacedObjectSpawner`, а не `Instantiate + Spawn()` из чанк-корутины).
- **Фазы** (`06N_REBASE_TRANSACTION_CONTRACT.md:52-72`): чанки участвуют в PREFLIGHT
  (manifest digest, ownership, scene handles), CAPTURE (rollback snapshot до первой записи),
  APPLY (отдельные адаптеры, не общий `SetParent`), REBUILD (`Physics.SyncTransforms`,
  `ShipDeckNav` re-registration где применимо), VALIDATE (exact poses, count/digest),
  PUBLISH (только после Validate — remote peers не получают ACK раньше).
  Ошибка до PUBLISH → ABORTED + reverse-order rollback; недоказанный rollback → frozen fault state.
- **Инварианты** (`06N_REBASE_TRANSACTION_CONTRACT.md:74-84`): `FloatingOriginMP` не включается
  (см. §5 ниже); общий `SetParent` для NO/city/ship запрещён; player-only shift запрещён;
  static-shift не доказывает ship/deck/player/camera readiness.

## 4. Кэши, сейвы, тики (где чанк-код обычно гниёт)

По правилам A (`09S_NEW_CONTENT_PIPELINE.md:63-72`, `AGENTS.md` §Floating Origin):

| Что кэширует чанк-система | Правило FO | Legacy-статус |
|---|---|---|
| `ChunkId _currentCenterChunk`, `_lastPreloadChunk`, `_preloadQueue`, `_loadedChunks` (`WorldStreamingManager.cs:97-107`) | 🟡 Хранит мировые `Vector3` между кадрами → `ApplyRebaseTranslation` + вызов в shift-блоке **обоих путей** (success и rollback) | ❌ Нет хуков вообще |
| `WorldBounds` чанков (`WorldChunk.cs:69`, `WorldChunkManager.cs:271-285`) | 🟡 То же; bounds считается от `GridX * ChunkSize` — после сдвига обязан пересчитаться тем же `T` | ❌ Считается один раз в `Awake`/`BuildChunkRegistry` |
| `playerTransform` / `_cachedLocalPlayerTransform` (`WorldStreamingManager.cs:113-115`, `PlayerChunkTracker.cs:57`) | 🟡 Участник сдвига (игрок едет `position += T` с `NetworkTransform.Teleport`); кэш обязан обновиться, а не указывать в старые координаты | ⚠️ Кэш с интервалом 1 с (`PLAYER_SEARCH_INTERVAL`) — после F8 до секунды указывает в прошлое |
| `deathY`, камера+collision, платформа, carry пикапов/NPC, шторма, AABB ветра, коридоры, fallback-точки (перечень из FO README) | Аналогично — любой чанк-геймплей (урон от падения, триггеры ферм) обязан сдвинуть свои пороги | ❌ Не покрыты |
| Сейвы координат чанков (какие квадраты «открыты») | 💾 Хранить + применять кумулятив при загрузке (паттерн `ShipPositionServer`, `terrain/README.md:41`) | ❌ Сейвов нет; `PlayerChunkTracker` держит всё в памяти (`PlayerChunkTracker.cs:40-62`) |
| Тики стриминга (0.5 с) vs freeze транзакции | Во FREEZE новые movement/parenting/nav/rebase блокируются (`06N` §4.2) — стриминг обязан встать на паузу и дождаться RELEASE | ❌ `UpdateStreaming`/`UpdatePreload` тикают всегда (`WorldStreamingManager.cs:278-289,668-694`) |

Отдельно: `GetWorldPosition` legacy (`FloatingOriginMP.cs:279-378`) с приоритетом
`positionSource → NetworkPlayer(IsOwner) → Player-tag → ThirdPersonCamera → Camera.main`
несовместим с пилотом (интерполяция NGO, `TradeZones`-камера, `NetworkPlayer(Clone)` у origin).
Пилот использует `ActorRebound` + `NetworkPublished` truth — чанк-трекер обязан брать позицию
из того же источника, а не из своего поиска.

## 5. Запрет на воскрешение FloatingOriginMP (явно)

- Инвариант `06N` §5: «`FloatingOriginMP` не включается» (`06N_REBASE_TRANSACTION_CONTRACT.md:76-78`).
- FO README §7: legacy heuristic shift controller не входит в новую транзакцию.
- Причины из кода: порог 150K vs 256 м пилота (`FloatingOriginMP.cs:92`); округление 10K
  (`FloatingOriginMP.cs:96`) vs точный `T` транзакции; поиск корней `GameObject.Find`
  (`FloatingOriginMP.cs:794-839`); спец-казуистика `TradeZones` (`FloatingOriginMP.cs:945-1012`);
  режимы Local/ServerSynced/ServerAuthority с `BroadcastWorldShiftRpc`/`RequestWorldShiftRpc`
  (`FloatingOriginMP.cs:497-603`) — параллельный контур управления миром рядом с
  `GlobalMotionControlledRebaseSlice` + `FO06_REBASE_SHIFT`. Два authority = split-brain.
- Следовательно: любые ссылки на `FloatingOriginMP` в будущих чанк-дизайнах — только как
  «deprecated predecessor», хуки `OnWorldShifted`/`OnFloatingOriginTriggered` (`FloatingOriginMP.cs:48,246-254`)
  не использовать; подписываться — на события пилота (`runtimeRebase.Completed`).

## 6. Приёмка чанков (если/когда они появятся): F8/F9 + каталог

По `09S_NEW_CONTENT_PIPELINE.md:59-116` и FO README:

1. **Простой (несетевой) чанк-контент:** F8 → `runtimeRebase.Completed` + объект на месте + 0 errors;
   F9 → возврат + книги (`_frame`, кумулятив). Консоль 0 errors; `AuditGlobalSceneCatalog` без новых
   расхождений по своей сцене (чужие stale — отдельной задачей, `09S` §4).
2. **Сетевой чанк-контент:** сначала полный §3.2 (маркер → observation → entry → хэши соседей →
   digest → `TryCompile=True` + `TryMatchDigest=True`), затем п.1 + Host-старт без
   `uncontrolled_network_source_before_native_sweep` + второй клиент получает `FO06_REBASE_SHIFT`
   и встаёт в новый кадр (`ActorRebound(ok=True)`).
3. **Структурный дрейф** (переименование чанка, ±дети, сдвиг корня): Host НЕ падает, но каталог stale —
   пересчитать `layoutHash` + digest отдельной правкой (`09S_NEW_CONTENT_PIPELINE.md:118-125`).
   Новая ветка/новый NO — полный §3.2 с нуля.

## 7. Вывод для ресёрча

Чанки не являются «третьей сущностью рядом с FO» — они либо **reviewed static content под корнями сцен**
(и тогда FO их не замечает, как `Terrain_0_0`), либо **явные участники транзакции с токенами**
(и тогда проектируются от `06N`-контракта, а не от legacy-корутин). Промежуточного «чуток генерим мимо
каталога» не существует — гейт B fail-closed. Именно поэтому визуальные импосторы (`03_*`) —
единственный дешёвый путь: они полностью укладываются в первый вариант.
