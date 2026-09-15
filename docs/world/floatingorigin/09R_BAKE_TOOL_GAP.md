# T-FO09R — Нет тулзы запекания каталога сцены (Bake): что отсутствует и зачем понадобится

Date: 2026-09-14. Статус: зафиксировано, НЕ реализовано. Вопрос пользователя:
«добавил корабль на сцену — вешать ли GlobalSceneSourceMarker, какой SourceId,
или есть тулза?»

## Что есть сейчас

- `GlobalSceneSourceMarker` («паспорт» объекта: `_sourceId` формата
  `guid_префаба:fileID_объекта:...`, `_frameId`, `_activateWhenReady`) —
  только в сцене, не в префабе. Так задумано: ID привязан к инстансу.
- `GlobalSceneNativeExecutor` собирает кандидатов ТОЛЬКО по маркерам и требует
  каждый `SourceId` в запечённом каталоге (`GlobalMotionPilotSceneCatalog.asset`
  + план), иначе fail-closed исключение при старте
  (`missing_duplicate_wrong_scene_baked_source_marker`).
- Все пункты меню `ProjectC/World/Floating Origin/...` — **Read Only**
  (аудиты/валидаторы). `grep AddComponent<GlobalSceneSourceMarker>` по проекту —
  пусто: штамповать маркеры и печь каталог не умеет никто.

## Чего нет

Editor-окна **«Bake Scene Catalog»**, которое должно:

1. Сканировать корни загруженных сцен (`WorldScene_*`, `BootstrapScene`).
2. Штамповать `GlobalSceneSourceMarker` на объектах без маркера, генерируя
   `SourceId` по правилу `guid:fileID:...` (руками НЕ писать — будет crash).
3. Дописывать записи в `GlobalMotionPilotSceneCatalog.asset` + план
   (ownership: `ShipOrRigidbodyRoot` для кораблей с Rigidbody,
   `SceneOwnedNetworkGameplay` для сетевых без Rigidbody,
   `AuthoredSceneContent` для статики — см. `ValidateOwnership` в маркере).
4. Прогонять валидаторы (`Validate*` в `Assets/_Project/Editor/FloatingOrigin/`)
   и показывать diff «что добавилось» до сохранения.

## Зачем понадобится

- Сейчас: ни за чем. Сдвиг F8 едет через корни сцен и работает без маркеров;
  новый контент (корабли, сундуки, NPC) добавляется БЕЗ маркеров и всё переживает.
- Когда строгий режим исполнителя станет нужен (identity-верификация каждого
  объекта, мультиплеер v2, closed-world гарантии): без Bake любой новый объект
  либо вне верификации (без маркера), либо крашит старт (маркер без записи
  в каталоге). Bake — gate перед активацией строгости на изменённой сцене.
- Оценка: ~полдня (сканирование + генерация ID + запись ассета + прогон
  валидаторов уже есть готовые).

## Текущий регламент (пока Bake нет)

1. Новый мировой контент — БЕЗ маркеров, под корни `WorldScene_*`.
2. `Audit Scene Catalog Candidates` покажет его как кандидата — ожидаемо.
3. Маркер руками НЕ ставить, `SourceId` НЕ выдумывать, каталог руками НЕ править.
4. Правила хуков/участников/сейвов — см. раздел Floating Origin в `AGENTS.md`.
