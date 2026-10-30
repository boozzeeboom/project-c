# 03. Floating Origin и большие миры в Unity для Project C

> Исследование подготовлено для MMO-sandbox Project C (Unity 6000.5.2f1, URP 17.5.0, NGO 2.13.0). Миры: 80×80 км террейн, 24 стриминговые сцены 6×4 (`SCENE_SIZE = 79999f`), 23 чанка города PRIMUM. Floating Origin уже реализован (`GlobalMotionControlledRebaseSlice`, F8/авто/F9). Отчёт не содержит кода.

## 1. Контекст проекта (кратко)

- **Сцены:** `BootstrapScene` (build [0]) + 24 `WorldScene_X_Z` (сетка 6×4), фокус `WorldScene_0_0`.
- **FO-механика:** сдвиг корней сцен + игроки `position += T` с серверным `NetworkTransform.Teleport` (`NetworkPublished`), локальные кэши сдвигаются тем же `T` под флагами, broadcast `FO06_REBASE_SHIFT` вторым клиентам, откат возвращает книги (`_frame`, кумулятив сейва).
- **Пайплайн нового контента:** `09S_NEW_CONTENT_PIPELINE.md` — две независимые подсистемы: A (сдвиг мира) и B (допуск при старте — каталог/маркер/digest для `NetworkObject`).
- **FO-классы контента:** 🟢 простой (без `NetworkObject`), 🟡 скрипт с мировыми `Vector3`, 🔴 рантайм-спавн вне корней, 💾 сейвы координат.

## 2. Floating Origin в Unity

### 2.1. Суть и порог

Floating Origin (он же origin shift / world recentering) — техника, при которой мир сдвигается так, чтобы игрок/камера оставались близко к `(0,0,0)`, а не двигался игрок в мировых координатах. Причина — потеря точности `float32` (7 значащих цифр): на расстоянии 1000 ед. шаг ~0.0001, на 10 000 — ~0.001, на 100 000 — ~0.01, на 131 072 — ~0.0156 м (1.5 см). Это вызывает jitter меши, flicker теней, нестабильность физики.

**Типичные пороги:** 2 000–10 000 ед. для VR/FPS (jitter виден уже с 2 км), 256 м в нашем проекте для авто-ребейса (по README), threshold обычно степень двойки (512/1024/2048/4096) — потому что точность падает по степеням двойки.

**Минимальный шаг float по дистанции (из Unity Discussions):**

| Дистанция от origin | Min step | Реальный масштаб |
|---|---|---|
| 4 096 | 0.000488 | ~0.5 мм |
| 8 192 | 0.000976 | ~1.0 мм (порог) |
| 16 384 | 0.001953 | ~2.0 мм |
| 32 768 | 0.003906 | ~4.0 мм |
| 65 536 | 0.007812 | ~8.0 мм |
| 131 072 | 0.015625 | ~1.5 см |

**Вывод для проекта:** 80 км террейн без FO невозможен. Наш FO держит активные координаты малыми — это правильный путь. Но precision-проблемы остаются для: (а) серверных регионов (Rigidbody у удалённого игрока), (б) сохранённых мировых координат, (в) мешей с большими локальными вершинами (городской FBX — не измерено, не трогать без измерений).

### 2.2. Техники и best practices

1. **Сдвиг корней сцен** (наш подход): `transform.position += offset` для всех root GameObject'ов загруженных сцен. Дёшево, но требует перечисления всех корней.
2. **Отслеживание логического мирового offset в `double`**: геймплей, сейвы, UI сообщают истинные координаты через аккумулятор `double`, хотя рендер остаётся около нуля. Критично для сейвов и карты.
3. **Сдвиг физики в том же кадре**: Rigidbodies, CharacterController, joints — через `Rigidbody.position`/`MovePosition` или `Teleport` (NGO). Важно не нарушить скорости/связи.
4. **Сдвиг частиц (world-space)**: `ParticleSystem.GetParticles`/`SetParticles` со сдвигом позиций.
5. **Сдвиг трейлов/лайнов**: массивы позиций `TrailRenderer`/`LineRenderer`.
6. **Камера и collision**: SpringArm/camera collision кэши (в нашем проекте `deathY`, камера+collision, платформа) — сдвигать отдельно.
7. **Авторебейс с кулдауном** (у нас 35 с, опрос 5 с, порог 256 м) — чтобы не дёргать каждый кадр.
8. **Rollback/откат** (F9) — для тестов и восстановления.

**Известные asset-решения:** Floating Origin Network (Asset Store), Origin Shift — Multiplayer Infinite World Solution (Mirror), coherence SDK (world origin shifting с разделением global double и индивидуальных client origins). coherence — пример, не рекомендация мигрировать сеть.

### 2.3. Координаты

- **Глобальные `double`** (истинные мировые позиции) + **локальные `float`** (относительно текущего origin). Это наш путь (explicit global-double/local-frame, см. `01_AUDIT_AND_SOURCES.md`).
- **Не смешивать**: `Vector3` (float) не использовать для сохранения мировых позиций на больших дистанциях. `SceneTransitionData` уже несёт `SceneID + local float` — проверять диапазон local внутри большой сцены.
- **Wire-формат NGO остаётся float** (NetworkTransform шлёт `Vector3`); даже успешная замена значения не превращает wire в double. Поэтому нужен собственный spatial replication протокол с явным coordinate type (INetworkSerializable). Переопределение двух notification hooks в NGO 2.13 недостаточно (порядок проверен в исходнике).
- **Client rebase vs серверные регионы:** отдельный origin на каждом remote-клиенте улучшает его представление, но сам по себе не исправляет удалённую серверную Rigidbody-симуляцию. Нужны серверные регионы/physics query routing.

### 2.4. Physics

- PhysX использует float → jitter при больших координатах. Решение — держать симулируемые тела близко к origin (наш FO делает это для локального игрока; для удалённых — регионы).
- `Physics.SyncTransforms` синхронизирует изменения Transform с физикой — вызывать после rebase, иначе коллайдеры/рейкасты устареют.
- **Rigidbody velocities/joints** при rebase: использовать `Rigidbody.position`/`MovePosition` или NGO `Teleport`, чтобы не сломать скорости и пружины. При `Teleport` PhysX может ресетить состояния — проверять.
- **Sleep states, interpolation:** после сдвига тела могут «проснуться» или дрожать из-за interpolation; временно отключать interpolation или ресетить.
- **CharacterController:** `Move`/`SimpleMove` с большими координатами — jitter; держать около origin.
- **Независимые physics scenes** (Unity 6 multi-scene physics) — основа серверных регионов, но не готовая MMO-система.

### 2.5. Networking (NGO 2.13)

- **Server authority:** сервер — источник истины для не-игровых объектов; игрок имеет local authority (client-side prediction).
- **NetworkTransform.Teleport** — наш способ сдвинуть игрока на сервере и клиенте синхронно.
- **Object visibility (interest management):** `NetworkObject.CheckObjectVisibility` callback, `NetworkShow`/`NetworkHide`, `ContinuallyCheckVisibility`. NGO шлёт трафик только видимым объектам; при скрытии — despawn на клиенте.
- **Scene management:** в проекте `ClientSceneLoader` грузит сцены обычным `SceneManager.LoadSceneAsync`, не через `NetworkSceneManager`. Не рефакторить без явного запроса.
- **Scene-placed NetworkObject** с `InScenePlacedSourceGlobalObjectIdHash == 0` спавнятся через `ScenePlacedObjectSpawner`, иначе их `[Rpc]` падают с NRE.
- **Broadcast shift** (`FO06_REBASE_SHIFT`) вторым клиентам — наш механизм синхронизации origin между клиентами.
- **Late join:** initial-rebase между сессиями отложен (не host-скоп). Один frame не держит двух удалённых игроков одновременно возле нуля.

### 2.6. NavMesh

- `NavMesh.AddNavMeshData` допускает placement при регистрации — можно сдвигать NavMesh-данные при rebase.
- **Ground nav** и **ship sandbox** (ShipDeckNav со своим `navFrameOrigin`) обрабатываются раздельно — нельзя автоматически вычитать один offset из всего.
- NavMesh baked в сцене под корнем сцены едет с корнем бесплатно; runtime NavMesh (additive) — перерегистрация.
- При rebase нужно обновлять `navFrameOrigin`/`lastRegisteredShipPos` и прокси-точки деки.

### 2.7. UI

- **World-space UI** дрожит при больших координатах (как и меши). Держать UI в screen space или прикреплять к камере.
- Canvas/Panel на объектах, которые едут с миром, получают сдвиг автоматически (как дети корней), но world-space canvas нужно проверять.
- В Bootstrap есть `DistantFocus`, `ChartWindow`, `VeilController` (без NO, plain-корни) — известный дрейф каталога, не блокирует Host.

### 2.8. Audio

- Пространственные `AudioSource` привязаны к Transform → едут с объектами автоматически.
- `AudioListener` на камере (около origin) — ок.
- Проблемы: доплер при rebase (скорость «скачка»), reverb zones, 3D-панорамирование. Обычно не критично, но стоит отключать доплер или сглаживать.
- `AudioMixer`/DSP-граф не зависит от координат.

### 2.9. Частицы, трейлы, лайны, проектили

- World-space `ParticleSystem` — сдвигать через `GetParticles`/`SetParticles`.
- `TrailRenderer`/`LineRenderer` — массивы позиций.
- VFX Graph — проверять simulation space; world-space симуляция требует сдвига.
- Проектили с кэшированными `impactPos` — rebase может произойти до выполнения callback (зафиксировано в `NpcBrain`).

### 2.10. Камера: far plane, z-fighting

- **Camera far plane:** держать как можно меньше; дальние объекты — через LOD/impostor/HLOD.
- **Z-fighting:** при больших координатах mesh faces слипаются. Решения: camera-relative rendering (HDRP имеет встроенный; в URP — нет нативного, но есть GPU Resident Drawer + GPU culling), logarithmic depth buffer (multi-frustum как в Cesium), tight near/far planes, FO (держать координаты малыми).
- **Cinemachine 3.x** (наш стек): виртуальные камеры следуют за таргетом; при rebase таргет сдвигается — камера должна корректно обновиться (в нашем проекте camera history сдвигается, см. `06DD`).
- **Pixel-perfect 2D:** обратная проблема (pixel snapping) — не наш случай (3D).

## 3. Большие миры: streaming, LOD, HLOD, impostor, cell-based, spatial partitioning, interest management, server authority

### 3.1. Streaming сцен

- **Additive loading** сцен-чанков вокруг игрока (наш подход: 24 сцены 6×4, загрузка соседей). Cell size — tradeoff churn vs spike: маленькие ячейки — больше IO, большие — больше память. UE5 World Partition default 256 м; для нас 79999/6 ≈ 13.3 км на ячейку сетки — грубо, но ок для 80 км.
- **Addressables** для ассетов (Pantheon MMO использует Addressables + GPU Resident Drawer + Instantiate Prefabs).
- **Occlusion culling baked** (cell-based) — для городских чанков PRIMUM.
- **Distance-based activation** компонентов (CullingGroup API) — для логики/NPC/AI, не только рендер.
- **Не загружать всё:** память и CPU на локальную область.

### 3.2. LOD / HLOD / impostor

- **LODGroup** (Unity): переключение мешей по дистанции, cross-fade (dithering/alpha). Каждый LOD-уровень — отдельный draw call; SRP Batcher не батчит разные LOD-уровни.
- **Mesh LOD** (Unity 6): автоматическая генерация LOD, меньше overhead чем LODGroup, но меньше контроля.
- **HLOD (Hierarchical LOD):** один объединённый меш-прокси для группы объектов на дистанции — снижает draw calls. UE5 World Partition строит HLOD для дальних ячеек. В Unity нет встроенного HLOD (MegaCity demo использовал свой).
- **Impostors (octahedral):** билборд с текстурами с разных ракурсов — для дальних деревьев/зданий. Pixyz/Asset Transformer Toolkit умеют генерировать. Важно: генерировать at runtime если объекты меняются (иначе missing tree появится на импосторе).
- **GPU Resident Drawer (Unity 6, URP):** GPU-driven rendering, снижает CPU-стоимость draw calls до 50% в object-dense сценах. Требует отключения Static Batching. Лучше всего с одинаковыми мешами.
- **GPU occlusion culling** (URP/HDRP) — работает в связке с Resident Drawer.
- **Occlusion culling baked** — для статической геометрии.

### 3.3. Cell-based / spatial partitioning

- **Grid / quadtree / spatial hash** — для запросов (кто рядом, что в радиусе). UE5 World Partition использует 2D runtime spatial hash grid.
- **Interest management (MMO):** отправлять клиенту только сущности в радиусе/видимости. NGO: `CheckObjectVisibility`, `NetworkShow`/`Hide`.
- **Server authority + client prediction + reconciliation:** сервер — истина, клиент предсказывает, сервер корректирует. NGO NetworkTransform поддерживает interpolation.
- **Network culling (visibility sets):** динамические наборы; NGO поддерживает per-client visibility.
- **Server regions / physics query routing:** для больших миров сервер не может симулировать всё в одном origin; нужны регионы с локальными origin и маршрутизация запросов.

### 3.4. Оптимизация кораблей/движущихся объектов с кучей деталей

- **LOD для корабля:** корпус (LOD0-2), детали (антенны, тросы, кабинка) — отдельные LOD/cull.
- **Impostor для дальнего корабля** (особенно для других игроков/трафика).
- **Culling:** frustum + occlusion + distance. Детали интерьера не рендерить снаружи.
- **Runtime spawn:** пулинг (ObjectPool), не Instantiate/Destroy в рантайме. NGO: `INetworkPrefabInstanceHandler` + пул (пример в issue #3190).
- **Parenting:** корабль — корень сцены/участник FO; детали — дети корабля (едут с ним). Не кэшировать мировые Vector3 без rebase-адаптера.
- **Rigidbody:** один корневой Rigidbody, детали — коллайдеры/фиксированные joints. Избегать множества независимых Rigidbody на деталях (jitter при rebase).
- **NavMesh на деке:** отдельный sandbox (`ShipDeckNav`), не общий ground NavMesh.
- **Static batching для корабля — нет:** корабль двигается → нельзя static-batch. Использовать SRP Batcher / GPU Resident Drawer / GPU instancing.
- **GPU Resident Drawer** особенно полезен для городских чанков PRIMUM (много одинаковых пропсов).

## 4. Наполненность сцены: объекты, рендереры, draw calls, batching, GPU-driven, memory

- **Draw calls:** каждый уникальный mesh+material = draw call. Цель — минимизировать. Stats: Batches, SetPass calls.
- **Static batching:** объединяет статические меши в один (на старте, в мировых координатах). **Конфликт с FO:** статически сбатченные объекты нельзя двигать (mesh запечён в мировые координаты) → для FO-мира не использовать static batching на объектах, которые едут с миром. Либо держать их вне FO (не двигаются).
- **Dynamic batching:** для маленьких мешей (ограничение по вершинам), автоматически.
- **SRP Batcher:** батчит по материалам/шейдерам, не объединяя меши. Хорош для URP. Не батчит разные LOD-уровни.
- **GPU instancing:** для множества одинаковых мешей (деревья, пропсы). Один draw call на mesh+material.
- **GPU Resident Drawer (Unity 6):** GPU-driven culling/batching через BatchRendererGroup, до 50% снижения CPU-стоимости в object-dense сценах. Требует отключения Static Batching. Лучше всего с повторяющимися мешами. Конфликт: Static Batching должен быть выключен.
- **GPU occlusion culling:** работает с Resident Drawer.
- **Memory:** текстуры (mipmaps, compression), mesh data (compression), Addressables для стриминга. Pantheon: Addressables + Instantiate Prefabs + GPU Resident Drawer.
- **Практические лимиты (ориентиры, не абсолюты):** на современном ПК — десятки тысяч draw calls при SRP Batcher/GPU Resident Drawer; без оптимизации — сотни-тысячи. Город PRIMUM (23 чанка) — кандидат на HLOD + impostors + GPU Resident Drawer.
- **LODGroup overhead:** больше памяти и CPU чем Mesh LOD; выбирать по нуждам.
- **Occlusion culling baked:** для статической геометрии городских чанков.

## 5. MMO: interest management, client-side prediction, server authority, network culling

- **Server authority** (наш стек NGO): сервер — истина для не-игровых NetworkObject; игроки — local authority (client-side prediction).
- **Client-side prediction + server reconciliation:** клиент предсказывает движение, сервер корректирует (сглаживание, rollback). NGO NetworkTransform поддерживает interpolation.
- **Lag compensation:** для стрельбы/взаимодействий — сервер отматывает время (Valve methods). В NGO нет встроенного; реализуется вручную.
- **Interest management / relevancy:** отправлять только релевантные сущности (по дистанции/видимости/зоне). NGO: `CheckObjectVisibility` callback, `NetworkShow`/`NetworkHide`, `ContinuallyCheckVisibility`. Слишком tight → pop-in; слишком loose → over-send.
- **Critical entities** (игроки, цели) — всегда relevant, независимо от дистанции.
- **Network culling (visibility sets):** динамические наборы; NGO поддерживает per-client visibility.
- **Area of Interest (AoI):** spatial hash grid / quadtree на сервере; рассылка дельт при пересечении границ ячеек.
- **Server regions / physics query routing:** для больших миров сервер не может симулировать всё в одном origin; нужны регионы с локальными origin. Наш проект пока host-only (сервер=хост), dedicated server отложен.
- **Late join / initial rebase между сессиями:** отложено (не host-скоп).
- **NetworkTransform wire format остаётся float** → для точных больших координат нужен собственный протокол (см. §2.3).

## 6. Unity DOTS/ECS для больших миров

- **ECS (Entity Component System):** данные-ориентированный подход; сущности вместо GameObject'ов; лучшая cache locality и производительность для тысяч объектов.
- **Jobs (C# Job System):** параллелизм на многоядерных CPU; безопасный доступ к данным.
- **Burst Compiler:** компиляция C# в нативный код через LLVM; CPU intrinsics.
- **SubScenes:** конвертация GameObject'ов в ECS-сущности в редакторе, сохранение на диск; быстрая загрузка.
- **Hybrid Renderer:** рендерит ECS-сущности через BatchRendererGroup; SRP Batcher.
- **Применимость для Project C:**
  - ✅ Точечно: вегетация/пропсы городских чанков PRIMUM, crowd/NPC-симуляция, частицы, distance queries.
  - ⚠️ Частичный переход: сложная интеграция с NGO (Netcode for Entities отдельный пакет, другой API). В проекте уже MonoBehaviour + NGO 2.13; полный переход — большой риск и объём.
  - ❌ Не переходить целиком сейчас: FO-архитектура, каталоги, NetworkObject-пайплайн завязаны на MonoBehaviour. DOTS имеет смысл как оптимизация горячих путей, не как переписывание.
  - Примеры продакшена: Megacity Metro (128+ игроков, ECS + Netcode for Entities), V Rising (Stunlock — ECS для open-world streaming), Zenith MMO (Ramen VR).
- **Double precision в DOTS mathlib:** Burst/DOTS поддерживают double — можно хранить точные мировые координаты в ECS, конвертируя в float только относительно текущего origin. Но это не решает PhysX/Transform (они float).

## 7. Известные проблемы (сводка)

| Проблема | Причина | Решение |
|---|---|---|
| Float precision jitter | 7 значащих цифр, min step растёт с дистанцией | FO, держать координаты малыми |
| Physics jitter | PhysX float, sleep/interpolation | FO, регионы, SyncTransforms, отключать interpolation при rebase |
| Camera far jitter | Трекинг объектов на больших координатах | FO, camera-relative rendering, tight far plane |
| Z-fighting | Недостаток depth precision на дистанции | FO, logarithmic depth, multi-frustum, camera-relative (HDRP), tight near/far |
| Transform hierarchies | Дети едут с родителем, но кэши Vector3 — нет | ApplyRebaseTranslation для всех кэшей, обе ветки (success/rollback) |
| Parenting при rebase | Рантайм-спавн вне корней не едет | Регистрация участником или parent под участника |
| Joints/springs | Teleport может рвать связи | Использовать MovePosition/Teleport аккуратно, тестировать |
| NavMesh | Baked едет с корнем; runtime — перерегистрация | Отдельный sandbox для кораблей/дек |
| Particles/trails | World-space симуляция не сдвигается автоматически | GetParticles/SetParticles, массивы позиций |
| Shaders с абсолютными координатами | height/noise по мировым координатам | Проверять шейдеры; учитывать сдвиг |
| Static batching vs FO | Mesh запечён в мировые координаты | Не static-batch объекты, которые едут с миром |
| Static поля C# | Не сдвигаются при rebase | Запрет static с мировыми Vector3 |
| Camera history | Lag/delay при rebase | Сдвигать history (наш 06DD) |

## 8. Что можно применить к проекту Project C

### 8.1. FO-классы (из `09S`, дополненные)

| Класс | Правило | Применение |
|---|---|---|
| 🟢 Простой контент (без `NetworkObject`) | Под корень `WorldScene_*` — едет бесплатно; маркер/каталог не нужны | Здания PRIMUM под корнями сцен, статик, разметка, декор |
| 🟡 Скрипт с мировыми `Vector3` | `ApplyRebaseTranslation` + вызов в shift-блоке (оба пути: success и rollback) | Все кэши: `deathY`, камера+collision, платформа, carry, шторм, AABB ветра, коридоры высот, fallback-точки, NPC targets, ship deck nav |
| 🔴 Рантайм-спавн вне корней сцен | Регистрация участником или parent под участника | Спавн кораблей/лута/NPC в рантайме, пулы |
| 💾 Сейвы мировых координат | Хранить + применять кумулятив при загрузке | `ShipPositionSaveData`, player persistence, чекпоинты |
| 🔵 Сетевой объект (с `NetworkObject`) | Полный пайплайн §3.2 `09S`: маркер + каталог + digest + ownership validation | Корабли, паром, `Road to Quartus`, квест-трекеры |
| ⚫ Запрещённое | `static` поля с мировыми `Vector3`; static batching на движущихся объектах; рантайм-спавн без регистрации | Код-ревью, CI-проверки |

### 8.2. Пайплайн нового контента (действует, из `09S`)

1. **Классифицировать объект** (вопрос: есть ли `NetworkObject` в сабтри?).
2. **Без NO → ветка «простой контент» (§3.1):** маркер не ставить, каталог не трогать. Действуют правила A: 🟢/🟡/🔴/💾. Проверка: F8 → `runtimeRebase.Completed` + объект на месте + 0 errors; F9.
3. **С NO → ветка «сетевой объект» (§3.2):** положить под корень сцены → проверить физику (Rigidbody для `ShipOrRigidbodyRoot`, отсутствие для `SceneOwnedNetworkGameplay`) → запечь `GlobalSceneSourceMarker` → добавить observation в каталог → entry → обновить хэши соседей → пересчитать digest → проверки перед Host.
4. **Bake-тулза** `BakePilotSceneCatalog` выполняет §3.2 одной кнопкой (с аттестацией человека через reviewNote).
5. **Что можно менять без перерегистрации:** позиции якорей вне сабтри, сериализованные поля. Структурные изменения сабтри → пересчёт хэша + digest.

### 8.3. Запрет статики (расширенный)

- **C# `static` поля с мировыми координатами — запрет:** они не сдвигаются при rebase → баг. Все кэши должны быть экземплярными или зарегистрированными участниками rebase.
- **Unity Static флаги (Batching/Occlusion/Contribute GI/Navigation):** не ставить на объекты, которые должны ехать с миром (здания PRIMUM под корнями сцен едут с корнями, но static-batched mesh запечён в мировые координаты → не сдвинется). Либо:
  - держать их вне FO (не двигаются, например дальний фон),
  - использовать SRP Batcher / GPU Resident Drawer / GPU instancing вместо static batching.
- **Static batching + GPU Resident Drawer несовместимы:** при переходе на GPU-driven отключать Static Batching в Player Settings.
- **Mesh-local вершины большого городского FBX:** числовые пределы не измерены; не назначать re-export без измерений (из `01_AUDIT`).

### 8.4. Рекомендации по оптимизации (применимо сейчас)

1. **Включить GPU Resident Drawer (URP)** для городских чанков PRIMUM (много повторяющихся пропсов) + GPU occlusion culling; отключить Static Batching. Проверить на целевом железе.
2. **LOD для кораблей и зданий PRIMUM:** LODGroup или Mesh LOD; impostors для дальних.
3. **Occlusion culling baked** для городских чанков.
4. **Addressables** для ассетов чанков (вместо Resources).
5. **Пулинг** для рантайм-спавна (корабли, лут, NPC) вместо Instantiate/Destroy; NGO `INetworkPrefabInstanceHandler`.
6. **CullingGroup API** для логики/NPC (не только рендер).
7. **Не использовать static batching на движущихся объектах.**
8. **Держать camera far plane минимальным**, использовать LOD/impostor для дальних.
9. **Не переходить на DOTS целиком** сейчас; точечно оптимизировать горячие пути (вегетация, crowd).
10. **Проверять шейдеры** на абсолютные мировые координаты (height noise) — при FO они сдвигаются.

### 8.5. Что остаётся открытым (не делать без запроса)

- Мультиплеер v2 (топология, late join, owner-rules) — dormant.
- Ship-adapter v2 (миграция NT→Replicator) — `08C`.
- Initial-rebase между сессиями, spawn-from-save рантайм-кораблей.
- Dedicated server / server regions (пока host-only).
- Полный DOTS-переход.

## 9. Источники

### Внутренние документы проекта

- `AGENTS.md` (корень) — tech stack, scene architecture, coding conventions, FO-rules.
- `docs/world/floatingorigin/README.md` — точка входа FO.
- `docs/world/floatingorigin/01_AUDIT_AND_SOURCES.md` — границы координат, NGO 2.13, physics/nav.
- `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md` — global static-world pilot, каталог, digest.
- `docs/world/floatingorigin/09S_NEW_CONTENT_PIPELINE.md` — пайплайн нового контента, FO-классы.
- `docs/Character/INVESTIGATION_CHARACTER_MICRO_JITTER_SOLUTIONS_RESEARCH.md` (упомянуто в 01_AUDIT).

### Внешние источники (web)

- Unity Discussions: "Large World Coordinates" (https://discussions.unity.com/t/large-world-coordinates/1716339) — float precision table, 100k limit.
- Unity Discussions: "Large world coordinates - double precision data - ECS" (https://discussions.unity.com/t/large-world-coordinates-double-precision-data/902530) — DOTS mathlib double, HDRP camera-relative.
- Unity Discussions: "Due to floating point precision limitations..." (https://discussions.unity.com/t/unity-warning-due-to-floating-point-precision-limitations/1577274).
- Unity Manual: Network Authority (UNet deprecated, NGO) — https://docs.unity3d.com/...
- NGO Object Visibility (https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@1.15/manual/basics/object-visibility.html) — CheckObjectVisibility, NetworkShow/Hide.
- Unity Manual: Camera-Relative Rendering (HDRP) — https://docs.unity.cn/Packages/com.unity.render-pipelines.high-definition@9.0/manual/Camera-Relative-Rendering.html.
- Unity Manual: GPU Resident Drawer (URP) — https://docs.unity.com/en-us/engine/6000.6/manual/.../gpu-resident-drawer.
- Unity Manual: Optimize mesh rendering using LOD — https://docs.unity.com/en-us/engine/6000.5/manual/.../lod.
- Unity Manual: LOD Group — https://docs.unity3d.com/2021.3/Documentation/Manual/class-LODGroup.html.
- Unity Manual: CullingGroup API — https://docs.unity3d.com/6000.1/Documentation/Manual/CullingGroupAPI.html.
- Unity Manual: Mesh LOD — https://docs.unity.com/en-us/engine/6000.5/manual/.../mesh-lod.
- Unity: "The multiplayer stack behind Pantheon: Rise of the Fallen" (https://unity.com/vn/blog/multiplayer-stack-visionary-realms-mmorpg-pantheon) — Addressables, GPU Resident Drawer, occlusion culling.
- Unity DOTS page (https://unity.com/dots) — ECS, Jobs, Burst, Megacity Metro, V Rising, Zenith.
- Wikipedia: Floating origin (https://en.wikipedia.org/wiki/Floating_origin).
- Cesium: Hybrid Multi-Frustum Logarithmic Depth Buffer (https://cesium.com/blog/2018/05/24/logarithmic-depth) — z-fighting, multi-frustum.
- Bugnet Blog: "How to Fix Physics and Camera Jitter in a Large Unity World With Floating Origin" (https://bugnet.io/blog/how-to-fix-unity-floating-origin-physics-jitter-large-world).
- Bugnet Blog: "How to Fix Network Interest Management (Relevancy) Issues" (https://bugnet.io/blog/how-to-fix-network-interest-management-issues).
- Mighty Professional Tutorials: Open-World Streaming (https://mightyprofessionalgaming.com/tutorials/world-streaming-from-scratch.html) — cell streaming, HLOD.
- altheragames: UE5 World Partition Guide (https://altheragames.com/en/blog/ue5-world-partition-guide) — cell size tradeoffs, HLOD.
- Godot docs: Visibility ranges (HLOD) (https://docs.godotengine.org/en/stable/tutorials/3d/visibility_ranges.html) — HLOD/impostor концепты.
- GitHub gist: FloatingOrigin.cs (marcospgp, brihernandez) — реализации FO.
- coherence docs: World Origin Shifting (https://docs.coherence.io/1.2/coherence-sdk-for-unity/world-origin-shifting) — пример разделения origins.
- Asset Store: Floating Origin Network, Origin Shift — Multiplayer Infinite World Solution — примеры multiplayer FO.
- Pixyz/Asset Transformer Toolkit: Create Impostor (https://docs.unity3d.com/Packages/com.unity.industry.toolkit@3.1/manual/Actions/Autogenerated/BakeImpostor.html).
- Kodeco: Unity Job System and Burst Compiler (https://www.kodeco.com/7880445-unity-job-system-and-burst-compiler-getting-started).
- Storm (Procedural Worlds) — streaming + floating-point fix + impostors + MMO collider export (https://www.procedural-worlds.com/products/professional/storm/).

*Отчёт подготовлен как исследовательский материал. Код не написан; рекомендации применяются через отдельные тикеты с одобрения пользователя.*
