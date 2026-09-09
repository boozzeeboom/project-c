# T-FO04G — player spawn bootstrap и initial placement

Дата: 2026-09-09. Baseline: `f3977311`. Статус: **ограниченный dormant player bootstrap реализован; compile PASS; 278 pure checks PASS / 0 FAIL**. Полный T-FO04 и runtime floating origin НЕ завершены. Global mode/world shift не включены; scenes/prefabs/profile assets не изменялись.

## 1. Конкретный результат и обязательная внешняя зависимость

Добавлен `GlobalMotionPlayerBootstrap : IGlobalMotionSpawnBootstrap, IGlobalMotionSpawnBootstrapLifecycle`. Он сам регистрирует подготовленные frames, обрабатывает очередь approved peers, создаёт player instances, вызывает `SpawnAsPlayerObject`, связывает adapter и первый global stream. Это уже не только интерфейс из E.

**Реализации `IGlobalMotionPlayerSpawnSource` в этом этапе нет.** Этот trusted источник должен предоставить реально подготовленный контент/физическое представление, immutable frame definitions, авторитетную global spawn pose из identity/persistence и game-specific owner rules, а на клиенте — явный выбор своего frame. Его отсутствие блокирует startup. Нельзя подставлять источник с фиктивным `ValidatePreparedContent=true`, старый `Transform.position` или default spawn ради прохождения gates.

`PreparedFrames` — локальные runtime descriptors (`id`, `LocalCoordinateFrame`, уже загруженная `Scene`/`PhysicsScene`), а не scene manifest, не loader и не сериализуемые scene handles. Полный census, coverage и подготовка контента ещё не доказаны.

## 2. Подтверждённые API и порядок

Исследованы **установленные** NGO 2.13.0 исходники, без изменения PackageCache:

- `GlobalMotionNetworkStartup.Approve` уже устанавливает **CreatePlayerObject=false**; автоматического player spawn в global approval нет.
- `NetworkPrefabInstanceHandlerWithData<T>` получает собственный payload до создания экземпляра. `NetworkPrefabHandler.SetInstantiationData` сериализует его на authority.
- `NetworkSpawnManager.SpawnNetworkObjectLocallyCommon` выставляет object ID/owner/player flags, вызывает все `OnNetworkSpawn`, обновляет PlayerObject. `AuthorityLocalSpawn` затем вызывает `OnNetworkPostSpawn`.
- `NetworkObject.SpawnInternal` отправляет spawn после `AuthorityLocalSpawn`: initial control и seed можно подготовить в protected `OnNetworkPostSpawn`, до spawn serialization.
- Получатель не перезаписывает factory pose при `HasTransform=false`; выбранный prefab обязан иметь `SynchronizeTransform=false`.
- Native spawn path проверяет null результата factory (`NetworkSpawnManager.cs`, 996–999). Отказ replica factory не создаёт запасной объект в origin; запрашивается shutdown, затем возвращается null. Фактическое сетевое поведение отказа ещё не проверено.
- `NetworkObject.NetworkManagerOwner` — internal. Поэтому этот bootstrap явно ограничен текущим `NetworkManager.Singleton`, проверяет его повторно и не использует private-field reflection. Несколько NetworkManager в одном процессе этим слоем не поддержаны; несколько PhysicsScene/frame одного manager допустимы как подготовленные данные.

Официальная API-документация дополнительно сверена для typed instance handler, OnNetworkPostSpawn и inactive-parent Instantiate. Источник истины по установленной версии — локальный package source.

## 3. Последовательность

1. **Read-only validation** проверяет источник, descriptors и строгую конфигурацию selected player prefab. Frames в World пока не регистрируются: до старта `World.IsRunning=false`.
2. Startup lease устанавливает callback/payload, затем вызывает отдельный installation hook. Он добавляет только prefab handler, сохраняет descriptors и при необходимости подключает существующий World к manager, созданному позднее его OnEnable. Частичная установка имеет cleanup. Чужой handler не заменяется: AddHandler=false прекращает запуск. Регистрация эксклюзивна; её замена сторонним кодом во время активной lease не поддерживается.
3. Approved server peers получают bounded queue tickets: epoch + client ID + serial. Дубликаты игнорируются, cancel/reconnect не переиспользуют ticket; очередь ограничена 256 pending, до четырёх готовых spawn за Update. Ожидание плана — до 60 секунд. Host ID=0 настоящий, не фиктивный пользователь.
4. Frames регистрируются **после** World.IsRunning. Host callback до OnServerStarted лишь ставит request в очередь. Проверяются уникальные ID/PhysicsScene, loaded scene и сохранность immutable frame lease. Потеря/подмена frame не вызывает скрытый rebase или auto-rebind.
5. Сервер получает явный world spawn plan. Remote-owner stream требует переданных game-specific rules; default rules/spawn/origin не выдумываются.
6. Prefab клонируется под inactive staging parent в нужной scene. Клон остаётся inactive, отделяется в root, получает `GlobalPosition - frame.Origin` (double subtraction до float), rotation/scale и initial controller hold. Лишь затем включается: **локальная поза/сцена установлены до Awake/OnEnable**, без скрытия renderer и без отключения Animator. Staging уничтожается.
7. После всех OnNetworkSpawn в PostSpawn выполняются Bind → StartWorldStream → IsBaselinePlaced → exact-binding initial latch release → readiness/ACK. **Server first spawn намеренно синхронный world-only**. Дополнительные native participants не допускаются validation; общая асинхронная native preparation здесь не реализована.
8. Перед spawn serialization создаётся `GlobalMotionSpawnSeed`: version=1, session/object/spawn identity, owner ID, **global** position, world rotation и scale. Client-frame origin и local frame ID по сети не передаются. Начальная поза не берётся из float-аргументов NGO factory.
9. Клиент выбирает собственный prepared frame через источник, проецирует seed до активации prefab, затем принимает authoritative control/baseline. Seed — лишь initial placement input, **не** authority/discontinuity authorization. Готовность требует совпадения lifetime/owner и настоящего baseline. Ожидание parent baseline на клиенте ограничено 60 секундами; неподготовленный parent не заменяется произвольным объектом.
10. Seed обновляется для ready server players на network ticks из frame-aware capture. Это не frozen initial spawn position; payload может отставать от актуального baseline на tick. Окончательная поза/authority берутся из motion control. Boundary handoff/AOI и причинная согласованность seed с изменениями регионов остаются отдельной интеграционной задачей.

## 4. Scope и failure policy

- Только selected **player prefab**, enabled NetworkPlayer и root CharacterController. Stock NetworkTransform не допускается даже disabled; Rigidbody/joints/nav/2D и дополнительные colliders запрещены. Остальные root IGlobalMotionActorParticipant, кроме NetworkPlayer, требуют расширенного coordinator.
- Обязательны выключенные SynchronizeTransform/AutoObjectParentSync/ActiveSceneSynchronization/SceneMigrationSynchronization и отсутствие DontDestroyWithOwner. Combat NB по-прежнему должны быть baked согласно E.
- Initial release включает CC только у owner, с учётом существующих input/death/piloting flags. HP, velocities, `_inputEnabled`, docking, Animator и game identity не сбрасываются. `SetInputEnabled` не вызывается как coordinate pause; его существующий setter лишь уважает initial gate.
- First-spawn latch сбрасывается при новом lifetime/despawn. Обычный later parent/authority/discontinuity не повторяет initial activation и не включает контроллер поверх piloting state. Это не новый общий native rebase approval.
- Enable/disable, pool reuse, родительские gameplay transitions и region migration **не объявлены поддержанными end-to-end**. Handler использует destroy, не pool. Неизвестный player, созданный в обход factory, блокирует сессию.
- Ошибочный/просроченный план remote peer отклоняет peer до создания объекта. Ошибка первого post-spawn/initial seed/сохранности prepared world приводит к shutdown с диагностикой. Ошибка host initial spawn останавливает host, а не создаёт фиктивного владельца. Это fail-closed политика, не HA recovery.
- Unspawned failures удаляют bookkeeping и экземпляр; spawned server failure использует Despawn там, где сеть ещё позволяет. Despawn уведомляет bootstrap. Release/shutdown/OnDestroy удаляют регистрацию и pending state. Корректность реальных NGO callback races остаётся UNTESTED.
- Активный legacy ClientSceneLoader отклоняется preflight: он читает local Transform как large-world Vector3. Сам loader не отключается и не переписывается здесь; его должен заменить content bridge. Аналогично проверка источника не является автоматической миграцией остальных spatial writers.
- NPC/ships/general scene objects, scene catalog, child lifecycle, deck/proxy/native navigation и physics regions этим player factory не реализованы. Родительский baseline на late join требует уже подготовленного внешним bridge parent.

## 5. Совместимость и проверка

Global native protocol поднят **0xF002 → 0xF003**. F/E peers отвергаются; F000–F0FF остаётся reserved. Legacy protocol=0 и unassigned profile сохраняют прежнюю рабочую ветку. Snapshot/control formats не изменены; новый seed имеет собственный version=1.

Compile исправления: namespace collision с `ProjectC.World.Scene` устранён fully-qualified Scene; новые Editor codec tests переведены с internal NGO serializer constructors на существующие public WriteNetworkSerializable/ReadNetworkSerializableInPlace; разделены short-circuit projection assertions для definite assignment. Production API visibility не расширялась ради тестов.

После исправлений фактически выполнен `Temp/Aura/ValidateFo04G.cs` в стабильном Edit Mode:

| Suite | PASS | FAIL |
|---|---:|---:|
| ValidateGlobalMotionSpawn | 44 | 0 |
| ValidateGlobalMotionHierarchy | 40 | 0 |
| ValidateGlobalMotionNetworkContracts | 48 | 0 |
| ValidateGlobalMotionActorReadiness | 26 | 0 |
| ValidateGlobalMotionApplication | 32 | 0 |
| ValidateGlobalMotionTransport | 32 | 0 |
| ValidateGlobalMotionProtocol | 33 | 0 |
| ValidateFloatingOriginFoundation | 23 | 0 |
| **Итого** | **278** | **0** |

Артефакт: `04G_STATIC_VALIDATION.json`. Проверены DTO codec/atomic rejection, identities, far double projection в независимые frames, явный origin=0 против invalid default, owner rules, bounded ticket queue/reconnect/session invalidation, initial latch и compiled seams. Они **не исполняют реальный prefab handler/CC/NGO lifecycle**.

Повторный read-only guard: prefab lists=1, candidates=58, opt-in candidates=0, profile assets=0, loaded enabled profiles=0, loaded opt-in adapters=0. Это прежняя ограниченная выборка, не полный census всех unloaded scenes.

**UNTESTED:** настоящий Host + 2 clients, callback ordering в живой сети, CharacterController/player/camera lifecycle, scene sync/DDOL во время загрузки, late join/reconnect/parent destruction/pooling, physics/nav/content integration, GC/network traffic. Play Mode, тестовые GameObject, physics simulation, screenshots и игровой runtime не запускались. Jitter fixed не заявляется.

## 6. Файлы и продолжение

Новые: GlobalMotionSpawnContracts.cs, GlobalMotionPlayerBootstrap.cs, Editor ValidateGlobalMotionSpawn.cs и три Unity-generated meta. Изменены NetworkPlayer.cs, GlobalMotionNetworkStartup.cs, GlobalMotionWorld.cs, GlobalMotionReplicator.cs, GlobalMotionNetworkContract.cs и исторический hierarchy validator (семантическая проверка допускает новый совместимый hierarchy-capable protocol). Исторические F/E отчёты и JSON не переписаны.

Обновлены этот отчёт, G JSON, roadmap и единственный `Assets/_Project/Docs/ITERATIONS.md`. Temp runner и несвязанный LiberationSans fallback не включаются в коммит. Один коммит кода/meta/документации, без отдельного собственного хеша.

Следующая ограниченная часть T-FO04: closed scene catalog и scene-object/child lifecycle contract/provider, затем реальные источники prepared content/global persistence в согласовании с T-FO05–08. Для полноценной интеграции остаются prefab migration, native bridges и пользовательский runtime gate. Этот этап не даёт разрешения назначать profile или включать world shift.
