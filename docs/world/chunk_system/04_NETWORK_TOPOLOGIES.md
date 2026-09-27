# 04. Как это работает на host-client / server-client среди N клиентов

Дата: 2026-09-27. Пин версий: NGO 2.13.0, `[Rpc(SendTo.X)]`, UTP (`AGENTS.md`);
`NetworkManager` один на рантайм (`DontDestroyOnLoad`); `NetworkPrefabsList` не присвоен
(динамический спавн сломан — отдельный тикет).

## 1. Три топологии проекта (различать обязательно)

| Топология | Что это у нас | Кто рендерит | Кто решает видимость |
|---|---|---|---|
| **H1. Host-client (хост-одиночка)** | Сервер-хост + 1 клиент в одном процессе; текущий закрытый скоуп FO (`floatingorigin/README.md:3-4`) | Хост (он же клиент) | Локально: сцены + ViewDistance |
| **H2. Dedicated server + N клиентов** | Целевая MMO-топология; `06DH` dormant (`floatingorigin/README.md:46`) | Только клиенты (сервер headless) | Сервер: interest/видимость; клиенты: визуальный LOD |
| **H3. Shared host + гости** | Хост играет и обслуживает гостей (промежуточная) | Хост + гости | Смешанно (самая сложная, см. §3) |

Ошибка legacy: `PlayerChunkTracker` + `WorldStreamingManager` не различают H1/H2/H3 —
один и тот же `LoadChunksAroundPlayer` вызывается и на сервере, и на клиентах
(`WorldStreamingManager.cs:325-376` vs `PlayerChunkTracker.cs:266-315`), а `FloatingOriginMP.mode`
переключается эвристикой (`FloatingOriginMP.cs:62-96,415-487`).

## 2. H1 Host-client: что происходит сейчас и почему чанки не нужны

- Хост грузит `WorldScene_0_0` (+ соседей 3×3 при старте — `ClientSceneLoader.cs:843-902`),
  `ScenePlacedObjectSpawner` спавнит scene-placed (`ScenePlacedObjectSpawner.cs:168-213`),
  игрок телепортируется в центр + 3000Y (`ClientSceneLoader.cs:436`).
- FO-слайс двигает корни + игрока одной транзакцией; второй клиент (если подключится)
  получает `FO06_REBASE_SHIFT` (см. `floatingorigin/README.md:9-17`).
- Legacy-чанки в H1 дали бы **двойную генерацию**: `WorldStreamingManager.UpdateStreaming`
  (`WorldStreamingManager.cs:644-658`) на хосте грузит вокруг «локального игрока»
  (поиск `IsOwner` + tag + `Camera.main` fallback — `WorldStreamingManager.cs:136-209`),
  а `PlayerChunkTracker` (тоже на хосте, `IsServer == true`) шлёт `ClientRpc` самому себе.
  Плюс `FloatingOriginMP.LateUpdate` (`FloatingOriginMP.cs:415-487`) сдвигает тот же мир
  по своим 150K — параллельно с FO-слайсом (256 м). Два контроллера origin = рассинхрон
  `_totalOffset` vs FO-кадр (подробно — в `05_*`).

## 3. H2 Dedicated server + N клиентов: правильная схема (без чанков)

### 3.1. Разделение ответственности (принцип)

- **Сервер (headless, не рендерит):** владеет **interest** — какие сцены/объекты реплицировать
  каждому `clientId`. Визуальные чанки/LOD на сервере отсутствуют как класс.
- **Клиент:** владеет **сценами** (`ClientSceneLoader`: грузит то, что сказал сервер +
  preload соседей) и **визуальным LOD** (`ViewDistanceApplier`, `LODGroup`, импосторы).
  Никаких `LoadChunkByServerCommand` (`WorldStreamingManager.cs:385-404`) — клиент грузит сцены,
  а не чанки по команде.

### 3.2. Что уже есть и что hardening'а требует

Есть (`ServerSceneManager.cs`):

- Per-client карты сцена/загруженное/трансформы (`ServerSceneManager.cs:30-37`), тик 0.5 с,
  антидребезг 1.0 с (`ServerSceneManager.cs:22,46-48`).
- Таргетные `ClientRpc` инициализации/транзишена/выгрузки (`ServerSceneManager.cs:267-374`).
- Реестр `SceneID → NetworkObject` + `NetworkHide/Show` (`ServerSceneManager.cs:407-486`).

Требует hardening'а (не чанков):

1. **Реестр заполнять автоматически** (сейчас только ручной `RegisterSceneObject`):
   при `ScenePlacedObjectSpawner.SpawnInScene` регистрировать `sceneId → netObj`;
   при `UnloadSceneCoroutine` — дерегистрировать (иначе `NetworkShow` по stale-ссылкам).
2. **Подтверждение загрузки** вместо `WaitForSeconds(1f)` (`ServerSceneManager.cs:242-246`):
   клиентский ACK (`[Rpc(SendTo.Server)] SceneLoadedAck(scene)`) → только потом `NetworkShow`.
   Иначе гость видит `NetworkShow` раньше, чем сцена загружена (пустые ссылки / NRE в `__endSendRpc` —
   та же семья, что в `ScenePlacedObjectSpawner.cs:10-16`).
3. **Interest по сценам, не по чанкам:** сервер держит `clientId → {currentScene + соседи 3×3}`;
   `HideSceneObjectsFromClient` при выходе, `Show` при ACK. Чанк-гранулярность (2 км) не нужна,
   пока нет плотности, требующей её (см. пороги в `02_*` §3).
4. **`NetworkPrefabsList` починить первым** (блокер любого динамического спавна, включая
   будущие импосторы/пропсы, `01_*` §4). Без него — только scene-placed + визуальные (несетевые) LOD.
5. **Лимиты:** на N клиентов сервер хранит суперсет сцен (`∪` интересов всех клиентов);
   выгружать сцену на сервере (точнее — прекращать её репликацию) можно, только если
   `GetPlayerCountInScene(scene) == 0` (`ServerSceneManager.cs:396-401`) **и** нет late-join окна.

### 3.3. Bandwidth-математика: почему чанк-RPC не масштабируется

Legacy (`PlayerChunkTracker.cs:266-315,334-374`): на каждое пересечение границы чанка —
`Load × 25 + Unload × M` RPC; вещание всем (`LoadChunkClientRpc` без `ClientRpcParams`-таргета —
`PlayerChunkTracker.cs:334-347`), фильтрация на клиенте (`if clientId != LocalClientId return`).

| Сценарий | Legacy чанк-RPC | Scene-interest (предлагаемое) |
|---|---|---|
| 1 клиент, спокойный полёт (пересечение 2K-границы каждые ~20 с при 100 м/с) | ~25 load + ~10 unload = ~35 RPC / 20 с | 0 (внутри сцены 80 км; только LOD локально) |
| 1 клиент, граница сцен (раз в ~13 мин при 100 м/с) | то же + сценарная загрузка (двойная работа) | 1 транзишен + N Show/Hide |
| 10 клиентов, разлетелись по 10 сценам | 10 × 35 RPC + каждая волна — всем (бродкаст-шторм) | 10 таргетных транзишенов, независимых |
| 10 клиентов, кучно (1 сцена) | 350 RPC на волну, 90% — дубликаты одного набора чанков | 1 набор Show, 10 ACK |

Дополнительно: `ChunkId.NetworkSerialize` (`WorldChunkManager.cs:45-49`) + `SceneTransitionData`
(`SceneID.cs:122-150`) — мелкие, но при шторме дают фрагментацию UTP-пайплайна.
Правило: **частота сетевых событий обязана масштабироваться сценами (80 км), а не чанками (2 км)** —
иначе скорость полёта 100–300 м/с превращает границу чанка в DDoS собственного сервера.

### 3.4. Late join / reconnect (граница с FO v2, `06DH` dormant)

Минимальный join-пакет (без чанков):

1. `clientId → SceneID` (сервер вычисляет из спавна, `ServerSceneManager.cs:164-169`).
2. `InitializeSceneClientRpc` → клиент грузит сцену (+ соседей) → `SceneLoadedAck`.
3. Сервер `Show` только объекты этой сцены (реестр §3.2) + текущий FO-кадр
   (frame generation + кумулятивный сдвиг — см. `05_*`; без него поздний клиент встанет
   в старую систему координат).
4. Чекпоинт/позиция — по `GlobalPlayerCheckpoint*` (`05_*` §4), не по чанку.

Reconnect после обрыва: шаги 1–4 + `UnloadAllScenesExcept` + сброс `_clientLoadedChunks`
(аналог `ClientSceneLoader.ResetForMainMenu`, `ClientSceneLoader.cs:696-750`).
Чанк-уровень состояния (какие 2K-квадраты «загружены») не персистится и не нужен —
персистятся сцена + глобальная позиция + кумулятив FO.

## 4. H3 Shared host + гости: самая сложная (отложить)

Хост одновременно: (а) рендерит и LOD'ит как клиент, (б) решает interest как сервер,
(в) участвует в FO-транзакции как authority. Legacy смешивал (а)+(б) в одном `Update`
(`WorldStreamingManager.UpdateStreaming` + `PlayerChunkTracker.Update` — оба тикают 0.5 с
и оба ищут «локального игрока» разными способами). Правило H3: **два независимых контура**
(клиентский визуальный + серверный interest) с разными тиками и разными источниками позиции;
общий код — только математика `SceneID.FromWorldPosition`. Детали — задача мультиплеера v2
(`06DH`); данный ресёрч фиксирует только запрет «один Update на двоих».

## 5. Что это значит для «клиентского чанк-лоадера» (ответ)

- H1: не нужен (1 сцена, локальный LOD покрывает).
- H2: не нужен как сущность; нужен **серверный scene-interest + клиентский LOD**.
  Слово «чанк» в сети не произносится вообще.
- H3: не проектировать до v2; любой прототип обязан развести контуры (а) и (б).
- Late join/reconnect: проектировать от сцен + FO-кадра, не от чанков.

`NOT VERIFIED` (нужен Play с 2+ клиентами, делает пользователь): ACK-цикл сцены,
`NetworkShow` после ACK, поведение `ScenePlacedObjectSpawner` при позднем join'е
(кто спавнит scene-placed для опоздавшего — хост один раз или каждый join),
профайлер UTP при 10 клиентах кучно/врозь.
